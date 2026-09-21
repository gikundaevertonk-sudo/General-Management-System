Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Contracts
Imports GMS.Core.Enums
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    ''' <summary>
    ''' The only component that changes <c>Product.QuantityOnHand</c>. Every change
    ''' is written to the <c>StockMovement</c> ledger, and the cached quantity is
    ''' kept equal to the signed sum of that ledger.
    ''' </summary>
    Public NotInheritable Class InventoryService
        Inherits ServiceBase

        Private ReadOnly _audit As AuditService

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, tenantContext As ITenantContext, clock As IClock, audit As AuditService)
            MyBase.New(uow, currentUser, tenantContext, clock)
            _audit = Guard.NotNull(audit)
        End Sub

        ''' <summary>Manual stock correction. Positive quantity, direction chosen by caller.</summary>
        Public Function Adjust(productId As Integer, direction As StockMovementDirection,
                               quantity As Decimal, note As String) As Result(Of StockMovement)

            If Denied(PermissionCodes.Inventory.Adjust) Then Return Forbidden(Of StockMovement)()
            If quantity <= 0D Then Return Result(Of StockMovement).Fail("Quantity must be greater than zero.")

            Dim products = Uow.Repository(Of Product)()
            Dim product = products.GetById(productId)
            If product Is Nothing Then Return NotFound(Of StockMovement)("Product")

            If direction = StockMovementDirection.Out AndAlso product.QuantityOnHand < quantity Then
                Return Result(Of StockMovement).Fail(
                    $"Only {product.QuantityOnHand:0.###} {product.UnitOfMeasure} of '{product.Name}' are on hand.")
            End If

            Dim movement = ApplyMovement(product, direction, quantity, StockMovementReason.Adjustment, Nothing, note)
            products.Update(product)
            _audit.Record(NameOf(Product), product.Id.ToString(), AuditAction.Update,
                          New Dictionary(Of String, FieldChange) From {
                            {"QuantityOnHand", New FieldChange(movement.QuantityAfter - movement.SignedQuantity, movement.QuantityAfter)}})
            Uow.SaveChanges()
            Return Result(Of StockMovement).Ok(movement)
        End Function

        ''' <summary>
        ''' Posts the stock effect of a confirmed transaction. Called by
        ''' <see cref="TransactionService"/>; assumes permission was already checked.
        ''' </summary>
        Friend Function PostForTransaction(txn As Transaction) As Result
            Dim direction = DirectionFor(txn.Type)
            Dim reason = ReasonFor(txn.Type)
            Dim products = Uow.Repository(Of Product)()

            ' Validate availability up-front so a multi-line sale is all-or-nothing.
            If direction = StockMovementDirection.Out Then
                For Each line In txn.Lines
                    Dim p = products.GetById(line.ProductId)
                    If p Is Nothing Then Return NotFound($"Product {line.ProductId}")
                    If p.QuantityOnHand < line.Quantity Then
                        Return Result.Fail($"Not enough stock of '{p.Name}' (need {line.Quantity:0.###}, have {p.QuantityOnHand:0.###}).")
                    End If
                Next
            End If

            For Each line In txn.Lines
                Dim product = products.GetById(line.ProductId)
                ApplyMovement(product, direction, line.Quantity, reason, line.Id,
                              $"{txn.Type} {txn.TransactionNumber}")
                If txn.Type = TransactionType.Purchase AndAlso line.UnitPrice > 0D Then
                    product.CostPrice = line.UnitPrice ' keep last cost current
                End If
                products.Update(product)
            Next
            Return Result.Ok()
        End Function

        ''' <summary>Writes compensating movements when a confirmed transaction is cancelled.</summary>
        Friend Function ReverseForTransaction(txn As Transaction) As Result
            Dim originalDirection = DirectionFor(txn.Type)
            Dim opposite = If(originalDirection = StockMovementDirection.In,
                              StockMovementDirection.Out, StockMovementDirection.In)
            Dim products = Uow.Repository(Of Product)()

            If opposite = StockMovementDirection.Out Then
                For Each line In txn.Lines
                    Dim p = products.GetById(line.ProductId)
                    If p Is Nothing OrElse p.QuantityOnHand < line.Quantity Then
                        Return Result.Fail(
                            $"Cannot cancel: stock of '{If(p?.Name, "?")}' has already been used and would go negative.")
                    End If
                Next
            End If

            For Each line In txn.Lines
                Dim product = products.GetById(line.ProductId)
                ApplyMovement(product, opposite, line.Quantity, StockMovementReason.CancellationReversal,
                              line.Id, $"Reversal of {txn.TransactionNumber}")
                products.Update(product)
            Next
            Return Result.Ok()
        End Function

        Public Function GetLedger(productId As Integer, options As QueryOptions) As Result(Of PagedResult(Of StockLedgerRow))
            If Denied(PermissionCodes.Inventory.View) Then Return Forbidden(Of PagedResult(Of StockLedgerRow))()
            If Uow.Repository(Of Product)().GetById(productId) Is Nothing Then
                Return NotFound(Of PagedResult(Of StockLedgerRow))("Product")
            End If

            Dim linesById = Uow.Repository(Of TransactionLine)().Query().ToDictionary(Function(l) l.Id, Function(l) l.TransactionId)
            Dim txnNumbers = Uow.Repository(Of Transaction)().Query().ToDictionary(Function(t) t.Id, Function(t) t.TransactionNumber)

            Dim q = Uow.Repository(Of StockMovement)().Query().Where(Function(m) m.ProductId = productId)
            Dim total = q.Count()
            Dim rows = q.OrderByDescending(Function(m) m.MovedAtUtc).ThenByDescending(Function(m) m.Id).
                Skip(options.Skip).Take(options.PageSize).ToList().
                Select(Function(m) New StockLedgerRow With {
                    .MovedAtUtc = m.MovedAtUtc,
                    .Direction = m.Direction,
                    .Reason = m.Reason,
                    .Quantity = m.Quantity,
                    .QuantityAfter = m.QuantityAfter,
                    .Note = m.Note,
                    .TransactionNumber = ResolveTxnNumber(m.TransactionLineId, linesById, txnNumbers)
                }).ToList()

            Return Result(Of PagedResult(Of StockLedgerRow)).Ok(
                New PagedResult(Of StockLedgerRow)(rows, total, options.Page, options.PageSize))
        End Function

        Public Function GetValuation() As Result(Of IReadOnlyList(Of InventoryValuationRow))
            If Denied(PermissionCodes.Inventory.View) Then Return Forbidden(Of IReadOnlyList(Of InventoryValuationRow))()

            Dim rows = Uow.Repository(Of Product)().Query().Where(Function(p) p.IsActive).
                OrderBy(Function(p) p.Name).
                Select(Function(p) New InventoryValuationRow With {
                    .ProductId = p.Id,
                    .Sku = p.Sku,
                    .ProductName = p.Name,
                    .QuantityOnHand = p.QuantityOnHand,
                    .UnitCost = p.CostPrice,
                    .ValueAtCost = Math.Round(p.QuantityOnHand * p.CostPrice, 2),
                    .BelowReorderLevel = p.QuantityOnHand <= p.ReorderLevel
                }).ToList()

            Return Result(Of IReadOnlyList(Of InventoryValuationRow)).Ok(rows)
        End Function

        ''' <summary>Recomputes a product's cached quantity from its ledger (repair tool).</summary>
        Public Function Reconcile(productId As Integer) As Result(Of Decimal)
            If Denied(PermissionCodes.Inventory.Adjust) Then Return Forbidden(Of Decimal)()
            Dim products = Uow.Repository(Of Product)()
            Dim product = products.GetById(productId)
            If product Is Nothing Then Return NotFound(Of Decimal)("Product")

            Dim fromLedger = Uow.Repository(Of StockMovement)().Query().
                Where(Function(m) m.ProductId = productId).
                Sum(Function(m) If(m.Direction = StockMovementDirection.In, m.Quantity, -m.Quantity))

            product.QuantityOnHand = fromLedger
            products.Update(product)
            Uow.SaveChanges()
            Return Result(Of Decimal).Ok(fromLedger)
        End Function

        Private Function ApplyMovement(product As Product, direction As StockMovementDirection,
                                       quantity As Decimal, reason As StockMovementReason,
                                       transactionLineId As Integer?, note As String) As StockMovement
            Dim delta = If(direction = StockMovementDirection.In, quantity, -quantity)
            product.QuantityOnHand += delta

            Dim movement As New StockMovement With {
                .ProductId = product.Id,
                .TransactionLineId = transactionLineId,
                .Direction = direction,
                .Reason = reason,
                .Quantity = quantity,
                .QuantityAfter = product.QuantityOnHand,
                .Note = If(note, String.Empty),
                .MovedAtUtc = Clock.UtcNow,
                .UserId = CurrentUser.UserId
            }
            Uow.Repository(Of StockMovement)().Add(movement)
            Return movement
        End Function

        Friend Shared Function DirectionFor(type As TransactionType) As StockMovementDirection
            Select Case type
                Case TransactionType.Purchase, TransactionType.AdjustmentIn
                    Return StockMovementDirection.In
                Case Else
                    Return StockMovementDirection.Out
            End Select
        End Function

        Private Shared Function ReasonFor(type As TransactionType) As StockMovementReason
            Select Case type
                Case TransactionType.Sale : Return StockMovementReason.Sale
                Case TransactionType.Purchase : Return StockMovementReason.Purchase
                Case Else : Return StockMovementReason.Adjustment
            End Select
        End Function

        Private Shared Function ResolveTxnNumber(lineId As Integer?,
                                                 linesById As Dictionary(Of Integer, Integer),
                                                 txnNumbers As Dictionary(Of Integer, String)) As String
            If Not lineId.HasValue Then Return String.Empty
            Dim txnId As Integer
            If Not linesById.TryGetValue(lineId.Value, txnId) Then Return String.Empty
            Return txnNumbers.GetValueOrDefault(txnId, String.Empty)
        End Function
    End Class

End Namespace

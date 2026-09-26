Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Enums
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    Public NotInheritable Class TransactionLineInput
        Public Property ProductId As Integer
        Public Property Quantity As Decimal
        ''' <summary>Null takes the product's default (sale) or current cost (purchase).</summary>
        Public Property UnitPrice As Decimal?
        ''' <summary>Null takes the configured default tax rate.</summary>
        Public Property TaxRatePercent As Decimal?
        Public Property Description As String = String.Empty
    End Class

    Public NotInheritable Class TransactionService
        Inherits ServiceBase

        Private ReadOnly _inventory As InventoryService
        Private ReadOnly _notifications As NotificationService
        Private ReadOnly _settings As SettingsService
        Private ReadOnly _audit As AuditService

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, tenantContext As ITenantContext, clock As IClock,
                       inventory As InventoryService, notifications As NotificationService,
                       settings As SettingsService, audit As AuditService)
            MyBase.New(uow, currentUser, tenantContext, clock)
            _inventory = Guard.NotNull(inventory)
            _notifications = Guard.NotNull(notifications)
            _settings = Guard.NotNull(settings)
            _audit = Guard.NotNull(audit)
        End Sub

        Public Function Search(options As QueryOptions,
                               Optional type As TransactionType? = Nothing,
                               Optional status As TransactionStatus? = Nothing) As Result(Of PagedResult(Of Transaction))
            If Denied(PermissionCodes.Transactions.View) Then Return Forbidden(Of PagedResult(Of Transaction))()

            Dim q = Uow.Repository(Of Transaction)().Query()
            If type.HasValue Then
                Dim t = type.Value
                q = q.Where(Function(x) x.Type = t)
            End If
            If status.HasValue Then
                Dim s = status.Value
                q = q.Where(Function(x) x.Status = s)
            End If
            If Not String.IsNullOrWhiteSpace(options.Search) Then
                Dim term = options.Search.Trim().ToLower()
                q = q.Where(Function(x) x.TransactionNumber.ToLower().Contains(term) OrElse x.Notes.ToLower().Contains(term))
            End If

            Dim total = q.Count()
            Dim items = q.OrderByDescending(Function(x) x.TransactionDate).ThenByDescending(Function(x) x.Id).
                Skip(options.Skip).Take(options.PageSize).ToList()
            Return Result(Of PagedResult(Of Transaction)).Ok(
                New PagedResult(Of Transaction)(items, total, options.Page, options.PageSize))
        End Function

        Public Function GetById(id As Integer) As Result(Of Transaction)
            If Denied(PermissionCodes.Transactions.View) Then Return Forbidden(Of Transaction)()
            Dim txn = LoadWithLines(id)
            Return If(txn Is Nothing, NotFound(Of Transaction)("Transaction"), Result(Of Transaction).Ok(txn))
        End Function

        Public Function CreateDraft(type As TransactionType, partyId As Integer?,
                                    transactionDate As DateTime, notes As String,
                                    Optional customerName As String = Nothing) As Result(Of Transaction)
            If Denied(PermissionCodes.Transactions.Create) Then Return Forbidden(Of Transaction)()

            Dim whenUtc = If(transactionDate = Date.MinValue, Clock.UtcNow,
                             DateTime.SpecifyKind(transactionDate, DateTimeKind.Utc))
            Dim txn As New Transaction With {
                .Type = type,
                .Status = TransactionStatus.Draft,
                .TransactionDate = whenUtc,
                .Notes = If(notes, String.Empty).Trim(),
                .CreatedAtUtc = Clock.UtcNow,
                .CreatedByUserId = CurrentUser.UserId
            }

            Dim partyError = AssignParty(txn, type, partyId, customerName)
            If partyError IsNot Nothing Then Return Result(Of Transaction).Fail(partyError)

            txn.TransactionNumber = NextNumber(type, txn.TransactionDate)
            Uow.Repository(Of Transaction)().Add(txn)
            _audit.Record(NameOf(Transaction), txn.Id.ToString(), AuditAction.Create)
            Uow.SaveChanges()
            Return Result(Of Transaction).Ok(txn)
        End Function

        Public Function AddLine(transactionId As Integer, input As TransactionLineInput) As Result(Of Transaction)
            If Denied(PermissionCodes.Transactions.Create) Then Return Forbidden(Of Transaction)()

            Dim txn = LoadWithLines(transactionId)
            If txn Is Nothing Then Return NotFound(Of Transaction)("Transaction")
            If txn.Status <> TransactionStatus.Draft Then Return Result(Of Transaction).Fail("Only draft transactions can be edited.")
            If input Is Nothing OrElse input.Quantity <= 0D Then Return Result(Of Transaction).Fail("Quantity must be greater than zero.")

            Dim product = Uow.Repository(Of Product)().GetById(input.ProductId)
            If product Is Nothing Then Return Result(Of Transaction).Fail("The selected product was not found.")

            Dim defaultPrice = If(txn.Type = TransactionType.Purchase, product.CostPrice, product.UnitPrice)
            Dim unitPrice = If(input.UnitPrice.HasValue, input.UnitPrice.Value, defaultPrice)
            If unitPrice < 0D Then Return Result(Of Transaction).Fail("Unit price cannot be negative.")

            Dim taxRate = If(input.TaxRatePercent.HasValue, input.TaxRatePercent.Value,
                             _settings.GetDecimal(SettingKeys.DefaultTaxRatePercent, 0D))

            Dim line As New TransactionLine With {
                .TransactionId = txn.Id,
                .ProductId = product.Id,
                .Description = If(String.IsNullOrWhiteSpace(input.Description), product.Name, input.Description.Trim()),
                .Quantity = input.Quantity,
                .UnitPrice = unitPrice,
                .TaxRate = taxRate
            }
            ComputeLine(line)
            Uow.Repository(Of TransactionLine)().Add(line)
            txn.Lines.Add(line)

            Recalculate(txn)
            Uow.Repository(Of Transaction)().Update(txn)
            Uow.SaveChanges()
            Return Result(Of Transaction).Ok(LoadWithLines(txn.Id))
        End Function

        Public Function RemoveLine(transactionId As Integer, lineId As Integer) As Result
            If Denied(PermissionCodes.Transactions.Create) Then Return Forbidden()
            Dim txn = LoadWithLines(transactionId)
            If txn Is Nothing Then Return NotFound("Transaction")
            If txn.Status <> TransactionStatus.Draft Then Return Result.Fail("Only draft transactions can be edited.")

            Dim line = txn.Lines.FirstOrDefault(Function(l) l.Id = lineId)
            If line Is Nothing Then Return NotFound("Line")

            Uow.Repository(Of TransactionLine)().Remove(line)
            txn.Lines.Remove(line)
            Recalculate(txn)
            Uow.Repository(Of Transaction)().Update(txn)
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        Public Function Confirm(transactionId As Integer) As Result
            If Denied(PermissionCodes.Transactions.Confirm) Then Return Forbidden()
            Dim txn = LoadWithLines(transactionId)
            If txn Is Nothing Then Return NotFound("Transaction")
            If txn.Status <> TransactionStatus.Draft Then Return Result.Fail("This transaction is not a draft.")
            If Not txn.Lines.Any() Then Return Result.Fail("Add at least one line before confirming.")

            Dim posted = _inventory.PostForTransaction(txn)
            If posted.Failed Then Return posted

            txn.Status = TransactionStatus.Confirmed
            txn.ConfirmedAtUtc = Clock.UtcNow
            txn.UpdatedAtUtc = Clock.UtcNow
            txn.UpdatedByUserId = CurrentUser.UserId
            Uow.Repository(Of Transaction)().Update(txn)
            _audit.Record(NameOf(Transaction), txn.Id.ToString(), AuditAction.Update,
                          New Dictionary(Of String, FieldChange) From {{"Status", New FieldChange("Draft", "Confirmed")}})
            Uow.SaveChanges()

            _notifications.RaiseLowStockFor(txn.Lines.Select(Function(l) l.ProductId).Distinct())
            Return Result.Ok()
        End Function

        Public Function Cancel(transactionId As Integer, reason As String) As Result
            If Denied(PermissionCodes.Transactions.Cancel) Then Return Forbidden()
            Dim txn = LoadWithLines(transactionId)
            If txn Is Nothing Then Return NotFound("Transaction")

            Select Case txn.Status
                Case TransactionStatus.Cancelled
                    Return Result.Fail("This transaction is already cancelled.")
                Case TransactionStatus.Draft
                    txn.Status = TransactionStatus.Cancelled
                    txn.CancelledAtUtc = Clock.UtcNow
                    Uow.Repository(Of Transaction)().Update(txn)
                    Uow.SaveChanges()
                    Return Result.Ok()
                Case Else
                    Dim reversed = _inventory.ReverseForTransaction(txn)
                    If reversed.Failed Then Return reversed

                    txn.Status = TransactionStatus.Cancelled
                    txn.CancelledAtUtc = Clock.UtcNow
                    txn.Notes = String.Join(Environment.NewLine,
                        {txn.Notes, $"Cancelled {Clock.UtcNow:u}: {If(reason, "no reason given")}"}.
                        Where(Function(s) Not String.IsNullOrWhiteSpace(s)))
                    Uow.Repository(Of Transaction)().Update(txn)
                    _audit.Record(NameOf(Transaction), txn.Id.ToString(), AuditAction.Update,
                                  New Dictionary(Of String, FieldChange) From {{"Status", New FieldChange("Confirmed", "Cancelled")}})
                    Uow.SaveChanges()
                    Return Result.Ok()
            End Select
        End Function

        Private Function AssignParty(txn As Transaction, type As TransactionType, partyId As Integer?,
                                     walkInName As String) As String
            Select Case type
                Case TransactionType.Sale
                    ' A sale needs a buyer, but not necessarily one on the customer list. Either
                    ' pick an existing customer or type a name for a one-off; a typed name is
                    ' recorded on the transaction and creates no customer account.
                    If partyId.HasValue Then
                        If Uow.Repository(Of Customer)().GetById(partyId.Value) Is Nothing Then Return "The selected customer was not found."
                        txn.CustomerId = partyId
                        txn.CustomerName = String.Empty
                    ElseIf Not String.IsNullOrWhiteSpace(walkInName) Then
                        txn.CustomerId = Nothing
                        txn.CustomerName = walkInName.Trim()
                    Else
                        Return "Choose a customer, or type a name for a one-off sale."
                    End If
                Case TransactionType.Purchase
                    If Not partyId.HasValue Then Return "A supplier is required for a purchase."
                    If Uow.Repository(Of Supplier)().GetById(partyId.Value) Is Nothing Then Return "The selected supplier was not found."
                    txn.SupplierId = partyId
                Case Else
                    ' Adjustments have no party.
            End Select
            Return Nothing
        End Function

        Private Function NextNumber(type As TransactionType, [date] As DateTime) As String
            Dim prefix = Select_Prefix(type)
            Dim year = [date].Year
            Dim countThisYear = Uow.Repository(Of Transaction)().Query().
                Count(Function(t) t.Type = type AndAlso t.TransactionDate.Year = year)
            Return $"{prefix}-{year}-{(countThisYear + 1):0000}"
        End Function

        Private Shared Function Select_Prefix(type As TransactionType) As String
            Select Case type
                Case TransactionType.Sale : Return "SAL"
                Case TransactionType.Purchase : Return "PUR"
                Case TransactionType.AdjustmentIn : Return "ADJ-IN"
                Case Else : Return "ADJ-OUT"
            End Select
        End Function

        Private Shared Sub ComputeLine(line As TransactionLine)
            line.LineSubtotal = Math.Round(line.Quantity * line.UnitPrice, 2)
            line.LineTax = Math.Round(line.LineSubtotal * line.TaxRate / 100D, 2)
            line.LineTotal = line.LineSubtotal + line.LineTax
        End Sub

        Private Shared Sub Recalculate(txn As Transaction)
            txn.Subtotal = txn.Lines.Sum(Function(l) l.LineSubtotal)
            txn.TaxTotal = txn.Lines.Sum(Function(l) l.LineTax)
            txn.Total = txn.Subtotal + txn.TaxTotal
        End Sub

        Private Function LoadWithLines(id As Integer) As Transaction
            Dim txn = Uow.Repository(Of Transaction)().GetById(id)
            If txn Is Nothing Then Return Nothing
            txn.Lines = Uow.Repository(Of TransactionLine)().Query().
                Where(Function(l) l.TransactionId = id).OrderBy(Function(l) l.Id).ToList()
            Return txn
        End Function
    End Class

End Namespace

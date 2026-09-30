Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Contracts
Imports GMS.Core.Enums
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    ''' <summary>
    ''' The only component that changes how much stock exists and where it sits. Every change is
    ''' written to the <c>StockMovement</c> ledger; <c>Product.QuantityOnHand</c> stays equal to
    ''' the signed sum of that ledger, and each <c>ShopStock</c> row stays equal to the signed sum
    ''' of the movements at its shop.
    ''' </summary>
    ''' <remarks>
    ''' Stock lives at a location. A movement with no shop belongs to the central pool - the
    ''' organization's own store room - and one with a shop belongs to that branch. Central is
    ''' never stored: it is <c>Product.QuantityOnHand</c> minus everything the shops hold, so the
    ''' totals cannot drift apart. An organization with no shops therefore behaves exactly as it
    ''' did before shops existed, with every movement central and central equal to the total.
    '''
    ''' Availability is always checked at the location the stock is leaving, never against the
    ''' organization total. A shop that is short must fail even when the company as a whole has
    ''' plenty, or a branch could sell goods that are sitting in another branch.
    ''' </remarks>
    Public NotInheritable Class InventoryService
        Inherits ServiceBase

        Public Const CentralLocationName As String = "Central"

        Private ReadOnly _audit As AuditService

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, tenantContext As ITenantContext, clock As IClock, audit As AuditService)
            MyBase.New(uow, currentUser, tenantContext, clock)
            _audit = Guard.NotNull(audit)
        End Sub

        ''' <summary>
        ''' Manual stock correction at one location. Positive quantity, direction chosen by caller.
        ''' </summary>
        ''' <param name="shopId">
        ''' Where to correct; Nothing is the central pool. Ignored for a caller pinned to a shop,
        ''' who can only ever correct their own.
        ''' </param>
        Public Function Adjust(productId As Integer, direction As StockMovementDirection,
                               quantity As Decimal, note As String,
                               Optional shopId As Integer? = Nothing) As Result(Of StockMovement)

            If Denied(PermissionCodes.Inventory.Adjust) Then Return Forbidden(Of StockMovement)()
            If quantity <= 0D Then Return Result(Of StockMovement).Fail("Quantity must be greater than zero.")

            Dim location = ResolveShopScope(shopId)
            Dim locationError = ValidateShop(location)
            If locationError IsNot Nothing Then Return Result(Of StockMovement).Fail(locationError)

            Dim products = Uow.Repository(Of Product)()
            Dim product = products.GetById(productId)
            If product Is Nothing Then Return NotFound(Of StockMovement)("Product")

            Dim balances = NewBalanceSet()
            Dim available = BalanceAt(balances, product, location)
            If direction = StockMovementDirection.Out AndAlso available < quantity Then
                Return Result(Of StockMovement).Fail(
                    $"Only {available:0.###} {product.UnitOfMeasure} of '{product.Name}' " &
                    $"are held at {LocationName(location)}.")
            End If

            Dim movement = ApplyMovement(balances, product, location, available, direction, quantity,
                                         StockMovementReason.Adjustment, Nothing, note, affectsOrgTotal:=True)
            products.Update(product)
            _audit.Record(NameOf(Product), product.Id.ToString(), AuditAction.Update,
                          New Dictionary(Of String, FieldChange) From {
                            {"QuantityOnHand", New FieldChange(product.QuantityOnHand - movement.SignedQuantity, product.QuantityOnHand)}})
            Uow.SaveChanges()
            Return Result(Of StockMovement).Ok(movement)
        End Function

        ''' <summary>
        ''' Moves stock from one location to another: central to a shop, a shop back to central,
        ''' or shop to shop. This is what a manager does to stock a branch.
        ''' </summary>
        ''' <remarks>
        ''' The organization's total is deliberately untouched - nothing was bought, sold or lost,
        ''' it only changed hands - so the two ledger rows this writes cancel out in
        ''' <c>Product.QuantityOnHand</c> while moving the two <c>ShopStock</c> balances.
        ''' </remarks>
        ''' <param name="unitPriceAtDestination">
        ''' What the product should sell for at the destination shop from now on. Nothing leaves
        ''' the destination's price as it was - its own if it already had one, otherwise the
        ''' catalogue's. Ignored when the destination is the central pool, which does not sell.
        ''' </param>
        Public Function Allocate(productId As Integer, fromShopId As Integer?, toShopId As Integer?,
                                 quantity As Decimal, note As String,
                                 Optional unitPriceAtDestination As Decimal? = Nothing) As Result

            If Denied(PermissionCodes.Shops.Allocate) Then Return Forbidden()
            If quantity <= 0D Then Return Result.Fail("Quantity must be greater than zero.")
            If unitPriceAtDestination.HasValue AndAlso unitPriceAtDestination.Value < 0D Then
                Return Result.Fail("Price cannot be negative.")
            End If

            ' Not ResolveShopScope: an allocation names two locations, and silently rewriting
            ' either of them to the caller's own shop would move stock somewhere nobody asked for.
            ' A pinned caller may only send their own shop's stock somewhere, never help
            ' themselves to another branch's.
            If OutsideShopScope(fromShopId) Then
                Return Result.Fail("You can only send stock out of your own shop.")
            End If

            If NullableEquals(fromShopId, toShopId) Then
                Return Result.Fail("Choose two different locations.")
            End If

            Dim fromError = ValidateShop(fromShopId)
            If fromError IsNot Nothing Then Return Result.Fail(fromError)
            Dim toError = ValidateShop(toShopId, mustBeActive:=True)
            If toError IsNot Nothing Then Return Result.Fail(toError)

            Dim products = Uow.Repository(Of Product)()
            Dim product = products.GetById(productId)
            If product Is Nothing Then Return NotFound("Product")

            Dim balances = NewBalanceSet()
            Dim available = BalanceAt(balances, product, fromShopId)
            If available < quantity Then
                Return Result.Fail(
                    $"{LocationName(fromShopId)} holds only {available:0.###} {product.UnitOfMeasure} of '{product.Name}'.")
            End If

            Dim reference = If(String.IsNullOrWhiteSpace(note), String.Empty, $": {note.Trim()}")
            Dim destinationBefore = BalanceAt(balances, product, toShopId)

            ApplyMovement(balances, product, fromShopId, available, StockMovementDirection.Out, quantity,
                          StockMovementReason.Allocation, Nothing,
                          $"To {LocationName(toShopId)}{reference}", affectsOrgTotal:=False)
            ApplyMovement(balances, product, toShopId, destinationBefore, StockMovementDirection.In, quantity,
                          StockMovementReason.Allocation, Nothing,
                          $"From {LocationName(fromShopId)}{reference}", affectsOrgTotal:=False)

            Dim changes As New Dictionary(Of String, FieldChange) From {
                {"Location", New FieldChange(LocationName(fromShopId), LocationName(toShopId))},
                {"Quantity", New FieldChange(0D, quantity)}}

            ' Priced after the movement, so the row is guaranteed to exist: the In above created it
            ' if this is the first time the product has reached that shop.
            If toShopId.HasValue AndAlso unitPriceAtDestination.HasValue Then
                Dim priced = balances.SetPrice(toShopId.Value, product.Id, unitPriceAtDestination.Value)
                changes("UnitPrice") = New FieldChange(If(priced.WasInheriting, CObj("(catalogue)"), CObj(priced.OldPrice)),
                                                       unitPriceAtDestination.Value)
            End If

            _audit.Record(NameOf(ShopStock), product.Id.ToString(), AuditAction.Update, changes)
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        ''' <summary>
        ''' Sets - or clears - what a product sells for at one shop, without moving any stock.
        ''' </summary>
        ''' <param name="unitPrice">
        ''' Nothing puts the shop back on the catalogue price, so a branch can be returned to the
        ''' standard list rather than being stuck with whatever it was last given.
        ''' </param>
        ''' <remarks>
        ''' Gated on Allocate rather than on a price permission of its own: the same people who
        ''' decide what a branch stocks decide what it charges, and the price is most often set
        ''' in the same breath as the allocation.
        ''' </remarks>
        Public Function SetShopPrice(shopId As Integer, productId As Integer, unitPrice As Decimal?) As Result
            If Denied(PermissionCodes.Shops.Allocate) Then Return Forbidden()
            If OutsideShopScope(shopId) Then Return ForbiddenShop()
            If unitPrice.HasValue AndAlso unitPrice.Value < 0D Then Return Result.Fail("Price cannot be negative.")

            Dim shopError = ValidateShop(shopId)
            If shopError IsNot Nothing Then Return Result.Fail(shopError)
            Dim product = Uow.Repository(Of Product)().GetById(productId)
            If product Is Nothing Then Return NotFound("Product")

            Dim balances = NewBalanceSet()
            Dim priced = balances.SetPrice(shopId, productId, unitPrice)
            _audit.Record(NameOf(ShopStock), productId.ToString(), AuditAction.Update,
                          New Dictionary(Of String, FieldChange) From {
                            {"UnitPrice", New FieldChange(If(priced.WasInheriting, CObj("(catalogue)"), CObj(priced.OldPrice)),
                                                          If(unitPrice.HasValue, CObj(unitPrice.Value), CObj("(catalogue)")))}})
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        ''' <summary>
        ''' What a product sells for at one location: the shop's own price when it has one, the
        ''' catalogue price otherwise. The central pool always uses the catalogue price.
        ''' </summary>
        Friend Function SalePriceAt(shopId As Integer?, product As Product) As Decimal
            If product Is Nothing Then Return 0D
            If Not shopId.HasValue Then Return product.UnitPrice
            Dim only = shopId.Value
            Dim own = Uow.Repository(Of ShopStock)().Query().
                Where(Function(s) s.ShopId = only AndAlso s.ProductId = product.Id).
                Select(Function(s) s.UnitPrice).FirstOrDefault()
            Return If(own, product.UnitPrice)
        End Function

        ''' <summary>Own prices at one shop, per product. Empty for the central pool.</summary>
        Friend Function PricesAt(shopId As Integer?) As Dictionary(Of Integer, Decimal)
            If Not shopId.HasValue Then Return New Dictionary(Of Integer, Decimal)()
            Dim only = shopId.Value
            Return Uow.Repository(Of ShopStock)().Query().
                Where(Function(s) s.ShopId = only AndAlso s.UnitPrice.HasValue).
                Select(Function(s) New With {s.ProductId, s.UnitPrice}).ToList().
                ToDictionary(Function(s) s.ProductId, Function(s) s.UnitPrice.Value)
        End Function

        ''' <summary>
        ''' Posts the stock effect of a confirmed transaction at the transaction's own location.
        ''' Called by <see cref="TransactionService"/>; assumes permission was already checked.
        ''' </summary>
        Friend Function PostForTransaction(txn As Transaction) As Result
            Dim direction = DirectionFor(txn.Type)
            Dim reason = ReasonFor(txn.Type)
            Dim products = Uow.Repository(Of Product)()
            Dim balances = NewBalanceSet()

            ' Availability is validated up front so a multi-line sale is all-or-nothing, and
            ' totalled per product so two lines of the same item cannot each pass against the
            ' same balance and overdraw it together.
            If direction = StockMovementDirection.Out Then
                For Each group In txn.Lines.GroupBy(Function(l) l.ProductId)
                    Dim p = products.GetById(group.Key)
                    If p Is Nothing Then Return NotFound($"Product {group.Key}")
                    Dim needed = group.Sum(Function(l) l.Quantity)
                    Dim available = BalanceAt(balances, p, txn.ShopId)
                    If available < needed Then
                        Return Result.Fail(
                            $"Not enough stock of '{p.Name}' at {LocationName(txn.ShopId)} " &
                            $"(need {needed:0.###}, have {available:0.###}).")
                    End If
                Next
            End If

            For Each line In txn.Lines
                Dim product = products.GetById(line.ProductId)
                Dim before = BalanceAt(balances, product, txn.ShopId)
                ApplyMovement(balances, product, txn.ShopId, before, direction, line.Quantity, reason, line.Id,
                              $"{txn.Type} {txn.TransactionNumber}", affectsOrgTotal:=True)
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
            Dim balances = NewBalanceSet()

            If opposite = StockMovementDirection.Out Then
                For Each group In txn.Lines.GroupBy(Function(l) l.ProductId)
                    Dim p = products.GetById(group.Key)
                    Dim needed = group.Sum(Function(l) l.Quantity)
                    If p Is Nothing OrElse BalanceAt(balances, p, txn.ShopId) < needed Then
                        Return Result.Fail(
                            $"Cannot cancel: stock of '{If(p?.Name, "?")}' at {LocationName(txn.ShopId)} " &
                            "has already been used and would go negative.")
                    End If
                Next
            End If

            For Each line In txn.Lines
                Dim product = products.GetById(line.ProductId)
                Dim before = BalanceAt(balances, product, txn.ShopId)
                ApplyMovement(balances, product, txn.ShopId, before, opposite, line.Quantity,
                              StockMovementReason.CancellationReversal, line.Id,
                              $"Reversal of {txn.TransactionNumber}", affectsOrgTotal:=True)
                products.Update(product)
            Next
            Return Result.Ok()
        End Function

        ''' <summary>
        ''' A product's movement history, optionally narrowed to one location.
        ''' </summary>
        Public Function GetLedger(productId As Integer, options As QueryOptions,
                                  Optional shopId As Integer? = Nothing,
                                  Optional shopOnly As Boolean = False) As Result(Of PagedResult(Of StockLedgerRow))
            If Denied(PermissionCodes.Inventory.View) Then Return Forbidden(Of PagedResult(Of StockLedgerRow))()
            If Uow.Repository(Of Product)().GetById(productId) Is Nothing Then
                Return NotFound(Of PagedResult(Of StockLedgerRow))("Product")
            End If

            ' A pinned caller only ever sees their own shop's movements, whatever was asked for.
            Dim pinned = PinnedShopId
            Dim narrowTo = If(pinned.HasValue, pinned, shopId)
            Dim narrow = pinned.HasValue OrElse shopOnly

            Dim q = Uow.Repository(Of StockMovement)().Query().Where(Function(m) m.ProductId = productId)
            If narrow Then
                If narrowTo.HasValue Then
                    Dim only = narrowTo.Value
                    q = q.Where(Function(m) m.ShopId.HasValue AndAlso m.ShopId.Value = only)
                Else
                    q = q.Where(Function(m) Not m.ShopId.HasValue)
                End If
            End If

            Dim linesById = Uow.Repository(Of TransactionLine)().Query().ToDictionary(Function(l) l.Id, Function(l) l.TransactionId)
            Dim txnNumbers = Uow.Repository(Of Transaction)().Query().ToDictionary(Function(t) t.Id, Function(t) t.TransactionNumber)
            ' Not named 'shopNames': VB is case-insensitive, so the local would shadow the
            ' ShopNames() method it is being assigned from and fail to compile.
            Dim namesByShop = ShopNames()

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
                    .ShopId = m.ShopId,
                    .Location = LocationNameFrom(m.ShopId, namesByShop),
                    .TransactionNumber = ResolveTxnNumber(m.TransactionLineId, linesById, txnNumbers)
                }).ToList()

            Return Result(Of PagedResult(Of StockLedgerRow)).Ok(
                New PagedResult(Of StockLedgerRow)(rows, total, options.Page, options.PageSize))
        End Function

        ''' <summary>Organization-wide valuation, at cost.</summary>
        Public Function GetValuation() As Result(Of IReadOnlyList(Of InventoryValuationRow))
            If Denied(PermissionCodes.Inventory.View) Then Return Forbidden(Of IReadOnlyList(Of InventoryValuationRow))()

            ' A caller pinned to a shop is valuing their shop, not the company.
            Dim pinned = PinnedShopId
            If pinned.HasValue Then
                Dim held = StockAt(pinned)
                Dim scoped = Uow.Repository(Of Product)().Query().Where(Function(p) p.IsActive).
                    OrderBy(Function(p) p.Name).ToList().
                    Select(Function(p) New InventoryValuationRow With {
                        .ProductId = p.Id,
                        .Sku = p.Sku,
                        .ProductName = p.Name,
                        .QuantityOnHand = held.GetValueOrDefault(p.Id, 0D),
                        .UnitCost = p.CostPrice,
                        .ValueAtCost = Math.Round(held.GetValueOrDefault(p.Id, 0D) * p.CostPrice, 2),
                        .BelowReorderLevel = held.GetValueOrDefault(p.Id, 0D) <= p.ReorderLevel
                    }).ToList()
                Return Result(Of IReadOnlyList(Of InventoryValuationRow)).Ok(scoped)
            End If

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

        ''' <summary>
        ''' What one location is holding, product by product. <paramref name="shopId"/> Nothing
        ''' reads the central pool.
        ''' </summary>
        Public Function GetStockAt(shopId As Integer?, options As QueryOptions,
                                   Optional includeEmpty As Boolean = False) As Result(Of PagedResult(Of ShopStockRow))
            If Denied(PermissionCodes.Inventory.View) Then Return Forbidden(Of PagedResult(Of ShopStockRow))()

            Dim location = ResolveShopScope(shopId)
            Dim locationError = ValidateShop(location)
            If locationError IsNot Nothing Then Return Result(Of PagedResult(Of ShopStockRow)).Fail(locationError)

            Dim name = LocationName(location)

            Dim q = Uow.Repository(Of Product)().Query().Where(Function(p) p.IsActive)
            If Not String.IsNullOrWhiteSpace(options.Search) Then
                Dim term = options.Search.Trim().ToLower()
                q = q.Where(Function(p) p.Name.ToLower().Contains(term) OrElse p.Sku.ToLower().Contains(term))
            End If
            Dim products = q.OrderBy(Function(p) p.Name).ToList()

            ' Two round trips whichever location this is: the products above, and one read of
            ' shop stock. Central works from the products already in hand rather than reading
            ' the catalogue a second time, and a shop's prices come back with its quantities.
            Dim held As Dictionary(Of Integer, Decimal)
            Dim prices As Dictionary(Of Integer, Decimal)
            If location.HasValue Then
                Dim holdings = HoldingsAt(location.Value)
                held = holdings.Quantities
                prices = holdings.Prices
            Else
                held = CentralStock(products.ToDictionary(Function(p) p.Id, Function(p) p.QuantityOnHand))
                prices = New Dictionary(Of Integer, Decimal)()
            End If

            Dim all = products.
                Select(Function(p) BuildStockRow(p, location, name, held.GetValueOrDefault(p.Id, 0D), prices)).
                Where(Function(r) includeEmpty OrElse r.QuantityOnHand <> 0D).
                ToList()

            Dim page = all.Skip(options.Skip).Take(options.PageSize).ToList()
            Return Result(Of PagedResult(Of ShopStockRow)).Ok(
                New PagedResult(Of ShopStockRow)(page, all.Count, options.Page, options.PageSize))
        End Function

        ''' <summary>
        ''' Headline figures for one location. <paramref name="shopId"/> Nothing summarises the
        ''' central pool, which has no <c>Shop</c> row of its own to carry them.
        ''' </summary>
        ''' <remarks>
        ''' Totalled here rather than by summing a page of <see cref="GetStockAt"/>, which would
        ''' silently report only the first page's worth once the catalogue outgrows one.
        ''' </remarks>
        Public Function GetSummaryAt(shopId As Integer?) As Result(Of ShopSummaryRow)
            If Denied(PermissionCodes.Inventory.View) Then Return Forbidden(Of ShopSummaryRow)()

            Dim location = ResolveShopScope(shopId)
            Dim locationError = ValidateShop(location)
            If locationError IsNot Nothing Then Return Result(Of ShopSummaryRow).Fail(locationError)

            ' The catalogue is read once and serves both the figures below and, for central, the
            ' organization totals the remainder is worked out from; it used to be read twice.
            Dim products = Uow.Repository(Of Product)().Query().
                Select(Function(p) New With {p.Id, p.QuantityOnHand, p.CostPrice, p.ReorderLevel, p.IsActive}).ToList()
            Dim held = If(location.HasValue,
                          HoldingsAt(location.Value).Quantities,
                          CentralStock(products.ToDictionary(Function(p) p.Id, Function(p) p.QuantityOnHand)))
            Dim row As New ShopSummaryRow With {
                .ShopId = If(location, 0),
                .Name = LocationName(location),
                .IsActive = True}

            For Each product In products
                If Not product.IsActive Then Continue For
                Dim quantity = held.GetValueOrDefault(product.Id, 0D)
                If quantity = 0D Then Continue For
                row.SkuCount += 1
                row.QuantityOnHand += quantity
                row.StockValueAtCost += quantity * product.CostPrice
                If quantity <= product.ReorderLevel Then row.LowStockCount += 1
            Next
            row.StockValueAtCost = Math.Round(row.StockValueAtCost, 2)

            If location.HasValue Then
                Dim shop = Uow.Repository(Of Shop)().GetById(location.Value)
                If shop IsNot Nothing Then
                    row.Code = shop.Code
                    row.IsActive = shop.IsActive
                End If
                Dim only = location.Value
                row.StaffCount = Uow.Repository(Of User)().Query().
                    Count(Function(u) u.IsActive AndAlso u.ShopId.HasValue AndAlso u.ShopId.Value = only)
            End If

            Return Result(Of ShopSummaryRow).Ok(row)
        End Function

        ''' <summary>
        ''' Where one product's stock currently sits: the central pool first, then every shop.
        ''' This is what an allocation screen is built from.
        ''' </summary>
        Public Function GetDistribution(productId As Integer) As Result(Of IReadOnlyList(Of ShopStockRow))
            If Denied(PermissionCodes.Inventory.View) Then Return Forbidden(Of IReadOnlyList(Of ShopStockRow))()

            Dim product = Uow.Repository(Of Product)().GetById(productId)
            If product Is Nothing Then Return NotFound(Of IReadOnlyList(Of ShopStockRow))("Product")

            Dim atShop = Uow.Repository(Of ShopStock)().Query().
                Where(Function(s) s.ProductId = productId).
                Select(Function(s) New With {s.ShopId, s.QuantityOnHand, s.UnitPrice}).
                ToList()
            Dim byShop = atShop.ToDictionary(Function(s) s.ShopId, Function(s) s.QuantityOnHand)
            Dim priceByShop = atShop.Where(Function(s) s.UnitPrice.HasValue).
                ToDictionary(Function(s) s.ShopId, Function(s) s.UnitPrice.Value)

            Dim rows As New List(Of ShopStockRow)()
            Dim pinned = PinnedShopId
            If Not pinned.HasValue Then
                rows.Add(BuildStockRow(product, Nothing, CentralLocationName,
                                       product.QuantityOnHand - byShop.Values.Sum(),
                                       New Dictionary(Of Integer, Decimal)()))
            End If

            For Each shop In Uow.Repository(Of Shop)().Query().OrderBy(Function(s) s.Name).ToList()
                If pinned.HasValue AndAlso shop.Id <> pinned.Value Then Continue For
                Dim shopPrice As New Dictionary(Of Integer, Decimal)()
                If priceByShop.ContainsKey(shop.Id) Then shopPrice(productId) = priceByShop(shop.Id)
                rows.Add(BuildStockRow(product, shop.Id, shop.Name, byShop.GetValueOrDefault(shop.Id, 0D), shopPrice))
            Next

            Return Result(Of IReadOnlyList(Of ShopStockRow)).Ok(rows)
        End Function

        ''' <summary>
        ''' Recomputes a product's cached quantities from its ledger (repair tool). Rebuilds the
        ''' organization total and every shop balance, so the two can be brought back into step.
        ''' </summary>
        Public Function Reconcile(productId As Integer) As Result(Of Decimal)
            If Denied(PermissionCodes.Inventory.Adjust) Then Return Forbidden(Of Decimal)()
            Dim products = Uow.Repository(Of Product)()
            Dim product = products.GetById(productId)
            If product Is Nothing Then Return NotFound(Of Decimal)("Product")

            Dim movements = Uow.Repository(Of StockMovement)().Query().
                Where(Function(m) m.ProductId = productId).
                Select(Function(m) New With {m.ShopId, m.Direction, m.Quantity, m.Reason}).
                ToList()

            ' An allocation moves stock without changing how much there is, so it is excluded from
            ' the organization total and included in the per-shop figures. Everything else counts
            ' towards both.
            product.QuantityOnHand = movements.
                Where(Function(m) m.Reason <> StockMovementReason.Allocation).
                Sum(Function(m) If(m.Direction = StockMovementDirection.In, m.Quantity, -m.Quantity))
            products.Update(product)

            Dim shopStocks = Uow.Repository(Of ShopStock)()
            Dim existing = shopStocks.Query().Where(Function(s) s.ProductId = productId).ToList()
            Dim fromLedger = movements.Where(Function(m) m.ShopId.HasValue).
                GroupBy(Function(m) m.ShopId.Value).
                ToDictionary(Function(g) g.Key,
                             Function(g) g.Sum(Function(m) If(m.Direction = StockMovementDirection.In, m.Quantity, -m.Quantity)))

            For Each row In existing
                row.QuantityOnHand = fromLedger.GetValueOrDefault(row.ShopId, 0D)
                shopStocks.Update(row)
                fromLedger.Remove(row.ShopId)
            Next
            For Each pair In fromLedger
                shopStocks.Add(New ShopStock With {
                    .ShopId = pair.Key, .ProductId = productId, .QuantityOnHand = pair.Value})
            Next

            Uow.SaveChanges()
            Return Result(Of Decimal).Ok(product.QuantityOnHand)
        End Function

        ''' <summary>Quantity held at one location, per product.</summary>
        Private Function StockAt(shopId As Integer?) As Dictionary(Of Integer, Decimal)
            If shopId.HasValue Then Return HoldingsAt(shopId.Value).Quantities

            Return CentralStock(Uow.Repository(Of Product)().Query().
                Select(Function(p) New With {p.Id, p.QuantityOnHand}).
                ToList().
                ToDictionary(Function(p) p.Id, Function(p) p.QuantityOnHand))
        End Function

        ''' <summary>
        ''' One shop's quantities and own prices, read together in a single query.
        ''' </summary>
        ''' <remarks>
        ''' Filtered in the database. This used to download every shop's rows and pick one shop
        ''' out in memory, and then read the same shop again for its prices - two round trips
        ''' to a remote database, the larger of them growing with every branch, on each click.
        ''' </remarks>
        Private Function HoldingsAt(shopId As Integer) As ShopHoldings
            Dim rows = Uow.Repository(Of ShopStock)().Query().
                Where(Function(s) s.ShopId = shopId).
                Select(Function(s) New With {s.ProductId, s.QuantityOnHand, s.UnitPrice}).
                ToList()
            Return New ShopHoldings With {
                .Quantities = rows.GroupBy(Function(s) s.ProductId).
                    ToDictionary(Function(g) g.Key, Function(g) g.Sum(Function(s) s.QuantityOnHand)),
                .Prices = rows.Where(Function(s) s.UnitPrice.HasValue).
                    GroupBy(Function(s) s.ProductId).
                    ToDictionary(Function(g) g.Key, Function(g) g.First().UnitPrice.Value)}
        End Function

        ''' <summary>
        ''' The central pool per product, from organization totals the caller has already read.
        ''' </summary>
        ''' <remarks>
        ''' Central is the remainder, so it is the organization total less everything allocated.
        ''' Taking the totals as an argument lets a caller that already holds the products pass
        ''' them in rather than have them read a second time.
        ''' </remarks>
        Private Function CentralStock(orgTotals As Dictionary(Of Integer, Decimal)) As Dictionary(Of Integer, Decimal)
            Dim allocated = Uow.Repository(Of ShopStock)().Query().
                Select(Function(s) New With {s.ProductId, s.QuantityOnHand}).
                ToList().
                GroupBy(Function(s) s.ProductId).
                ToDictionary(Function(g) g.Key, Function(g) g.Sum(Function(s) s.QuantityOnHand))
            Return orgTotals.ToDictionary(Function(p) p.Key,
                                          Function(p) p.Value - allocated.GetValueOrDefault(p.Key, 0D))
        End Function

        Private NotInheritable Class ShopHoldings
            Public Property Quantities As Dictionary(Of Integer, Decimal)
            Public Property Prices As Dictionary(Of Integer, Decimal)
        End Class

        ''' <param name="ownPrices">
        ''' Prices this location has set for itself, per product. A product missing from it sells
        ''' at the catalogue price.
        ''' </param>
        Private Shared Function BuildStockRow(product As Product, shopId As Integer?, locationName As String,
                                              quantity As Decimal,
                                              ownPrices As Dictionary(Of Integer, Decimal)) As ShopStockRow
            Dim own = ownPrices.ContainsKey(product.Id)
            Return New ShopStockRow With {
                .ShopId = shopId,
                .Location = locationName,
                .ProductId = product.Id,
                .Sku = product.Sku,
                .ProductName = product.Name,
                .UnitOfMeasure = product.UnitOfMeasure,
                .QuantityOnHand = quantity,
                .ReorderLevel = product.ReorderLevel,
                .UnitCost = product.CostPrice,
                .ValueAtCost = Math.Round(quantity * product.CostPrice, 2),
                .BelowReorderLevel = quantity <= product.ReorderLevel,
                .UnitPrice = If(own, ownPrices(product.Id), product.UnitPrice),
                .HasOwnPrice = own}
        End Function

        ''' <summary>
        ''' Rejects a shop that does not exist, or that this caller may not act at.
        ''' Returns Nothing when the location is fine. A Nothing shop is the central pool and is
        ''' always fine.
        ''' </summary>
        Private Function ValidateShop(shopId As Integer?, Optional mustBeActive As Boolean = False) As String
            If Not shopId.HasValue Then Return Nothing
            Dim shop = Uow.Repository(Of Shop)().GetById(shopId.Value)
            If shop Is Nothing Then Return "The selected shop was not found."
            If mustBeActive AndAlso Not shop.IsActive Then Return $"'{shop.Name}' is closed and cannot take stock."
            Return Nothing
        End Function

        ''' <summary>
        ''' Applies one movement and writes its ledger row.
        ''' </summary>
        ''' <param name="balanceBefore">
        ''' The location's balance before this movement. Passed in rather than re-read: under EF a
        ''' <c>ShopStock</c> row created earlier in the same unit of work is invisible to a query
        ''' until <c>SaveChanges</c>, so a second read inside one operation - a two-sided
        ''' allocation, or a sale with two lines of the same product - would compute the balance
        ''' from a figure that no longer holds.
        ''' </param>
        ''' <param name="affectsOrgTotal">
        ''' False only for an allocation, which moves stock between locations without the
        ''' organization gaining or losing any.
        ''' </param>
        Private Function ApplyMovement(balances As BalanceSet, product As Product, shopId As Integer?,
                                       balanceBefore As Decimal, direction As StockMovementDirection,
                                       quantity As Decimal, reason As StockMovementReason,
                                       transactionLineId As Integer?, note As String,
                                       affectsOrgTotal As Boolean) As StockMovement
            Dim delta = If(direction = StockMovementDirection.In, quantity, -quantity)
            If affectsOrgTotal Then product.QuantityOnHand += delta
            If shopId.HasValue Then balances.Apply(shopId.Value, product.Id, delta)

            Dim movement As New StockMovement With {
                .ProductId = product.Id,
                .ShopId = shopId,
                .TransactionLineId = transactionLineId,
                .Direction = direction,
                .Reason = reason,
                .Quantity = quantity,
                .QuantityAfter = balanceBefore + delta,
                .Note = If(note, String.Empty),
                .MovedAtUtc = Clock.UtcNow,
                .UserId = CurrentUser.UserId
            }
            Uow.Repository(Of StockMovement)().Add(movement)
            Return movement
        End Function

        Private Function NewBalanceSet() As BalanceSet
            Return New BalanceSet(Uow.Repository(Of ShopStock)())
        End Function

        ''' <summary>Balance of one product at one location, reflecting this operation's changes.</summary>
        Private Shared Function BalanceAt(balances As BalanceSet, product As Product, shopId As Integer?) As Decimal
            If shopId.HasValue Then Return balances.Balance(shopId.Value, product.Id)
            Return product.QuantityOnHand - balances.AllocatedTotal(product.Id)
        End Function

        Private Function ShopNames() As Dictionary(Of Integer, String)
            Return Uow.Repository(Of Shop)().Query().
                Select(Function(s) New With {s.Id, s.Name}).
                ToList().
                ToDictionary(Function(s) s.Id, Function(s) s.Name)
        End Function

        Private Function LocationName(shopId As Integer?) As String
            If Not shopId.HasValue Then Return CentralLocationName
            Return If(Uow.Repository(Of Shop)().GetById(shopId.Value)?.Name, $"Shop #{shopId.Value}")
        End Function

        Private Shared Function LocationNameFrom(shopId As Integer?, names As Dictionary(Of Integer, String)) As String
            If Not shopId.HasValue Then Return CentralLocationName
            Return names.GetValueOrDefault(shopId.Value, $"Shop #{shopId.Value}")
        End Function

        Private Shared Function NullableEquals(a As Integer?, b As Integer?) As Boolean
            If Not a.HasValue AndAlso Not b.HasValue Then Return True
            If a.HasValue <> b.HasValue Then Return False
            Return a.Value = b.Value
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

        ''' <summary>
        ''' The shop balances one operation is reading and changing.
        ''' </summary>
        ''' <remarks>
        ''' Exists because a repository query is not a reliable view of work in progress. Under the
        ''' in-memory store an added row appears immediately; under EF Core it does not appear
        ''' until <c>SaveChanges</c> runs. Holding the rows this operation has touched makes both
        ''' stores agree, which matters wherever one operation writes two movements at the same
        ''' location - an allocation, or a sale listing the same product twice.
        ''' </remarks>
        Private NotInheritable Class BalanceSet

            Private ReadOnly _shopStocks As IRepository(Of ShopStock)
            Private ReadOnly _touched As New Dictionary(Of (ShopId As Integer, ProductId As Integer), ShopStock)()

            ''' <summary>
            ''' Rows this operation created. They are already pending insert, so calling Update on
            ''' one would ask EF to mark an unsaved row Modified - which throws.
            ''' </summary>
            Private ReadOnly _added As New HashSet(Of ShopStock)()

            Public Sub New(shopStocks As IRepository(Of ShopStock))
                _shopStocks = shopStocks
            End Sub

            Public Function Balance(shopId As Integer, productId As Integer) As Decimal
                Dim row = Find(shopId, productId)
                Return If(row Is Nothing, 0D, row.QuantityOnHand)
            End Function

            Public Sub Apply(shopId As Integer, productId As Integer, delta As Decimal)
                Dim row = GetOrCreate(shopId, productId)
                row.QuantityOnHand += delta
                If Not _added.Contains(row) Then _shopStocks.Update(row)
            End Sub

            ''' <summary>
            ''' Sets this shop's own price, or clears it back to the catalogue's. Reports what was
            ''' there before so the caller can record the change.
            ''' </summary>
            Public Function SetPrice(shopId As Integer, productId As Integer,
                                     unitPrice As Decimal?) As (WasInheriting As Boolean, OldPrice As Decimal)
                Dim row = GetOrCreate(shopId, productId)
                Dim before = row.UnitPrice
                row.UnitPrice = unitPrice
                If Not _added.Contains(row) Then _shopStocks.Update(row)
                Return (Not before.HasValue, If(before, 0D))
            End Function

            ''' <summary>
            ''' The row for this shop and product, creating an empty one if the product has never
            ''' been here. A row with zero quantity is meaningful: it is how a shop can be given a
            ''' price before any stock arrives.
            ''' </summary>
            Private Function GetOrCreate(shopId As Integer, productId As Integer) As ShopStock
                Dim row = Find(shopId, productId)
                If row IsNot Nothing Then Return row

                ' OrganizationId is left for the store to stamp, exactly as every other row
                ' created by a service is.
                row = New ShopStock With {.ShopId = shopId, .ProductId = productId, .QuantityOnHand = 0D}
                _touched((shopId, productId)) = row
                _added.Add(row)
                _shopStocks.Add(row)
                Return row
            End Function

            ''' <summary>How much of a product the shops hold between them, pending changes included.</summary>
            Public Function AllocatedTotal(productId As Integer) As Decimal
                Dim total = 0D
                For Each row In _shopStocks.Query().Where(Function(s) s.ProductId = productId).ToList()
                    ' A row this operation already holds is the authority; the stored copy is stale,
                    ' and under the in-memory store it is the very same row read back again.
                    If Not _touched.ContainsKey((row.ShopId, productId)) Then total += row.QuantityOnHand
                Next
                For Each pair In _touched
                    If pair.Key.ProductId = productId Then total += pair.Value.QuantityOnHand
                Next
                Return total
            End Function

            Private Function Find(shopId As Integer, productId As Integer) As ShopStock
                Dim key = (shopId, productId)
                Dim hit As ShopStock = Nothing
                If _touched.TryGetValue(key, hit) Then Return hit

                hit = _shopStocks.Query().FirstOrDefault(
                    Function(s) s.ShopId = shopId AndAlso s.ProductId = productId)
                If hit IsNot Nothing Then _touched(key) = hit
                Return hit
            End Function
        End Class
    End Class

End Namespace

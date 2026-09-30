Imports System.Reflection
Imports System.Threading
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.EntityFrameworkCore.Metadata
Imports Npgsql
Imports NpgsqlTypes
Imports GMS.Core.Abstractions
Imports GMS.Core.Data
Imports GMS.Core.Enums
Imports GMS.Core.Models

Namespace Sync

    ''' <summary>What one sync did.</summary>
    Public NotInheritable Class SyncReport
        ''' <summary>False when PostgreSQL could not be reached; nothing else was attempted.</summary>
        Public Property Reachable As Boolean
        ''' <summary>True when another sync was already running and this one did nothing.</summary>
        Public Property Skipped As Boolean
        ''' <summary>Changes made here that the server now has.</summary>
        Public Property Sent As Integer
        ''' <summary>Rows that changed on the server and were copied down.</summary>
        Public Property Received As Integer
        ''' <summary>Changes that could not be sent this time and are still waiting.</summary>
        Public Property Failed As Integer
        Public ReadOnly Property Errors As New List(Of String)()
        ''' <summary>
        ''' Stock that went below zero on the server once this computer's offline sales landed,
        ''' because someone else sold the same units in the meantime. Each one also raises a
        ''' notification in the organization.
        ''' </summary>
        Public ReadOnly Property NegativeStock As New List(Of String)()
        Public Property FinishedAtUtc As DateTime
    End Class

    ''' <summary>
    ''' Sends this computer's offline changes to PostgreSQL and copies the server's changes down.
    ''' </summary>
    ''' <remarks>
    ''' <para>
    ''' One run is: push, then pull. Pushing first means the pull immediately brings back the
    ''' server's merged view of whatever was just sent - stock totals in particular.
    ''' </para>
    ''' <para>
    ''' Ids. Rows the server created keep the server's id locally. Rows created here have negative
    ''' ids (<see cref="LocalIdGenerator"/>) that are never rewritten; <see cref="IdMapEntry"/>
    ''' remembers the server id each one was given, and every reference is translated at the
    ''' boundary, both ways.
    ''' </para>
    ''' <para>
    ''' Conflicts. An edit sends only the fields it changed, so two people who edit different
    ''' fields of one record both keep their change; on the same field, whoever syncs last wins.
    ''' Stock totals are never sent at all: the movements are, and the server adds each one's
    ''' effect to its own totals, so a sale made offline and a sale made online both count. If
    ''' that takes stock below zero it is kept and flagged, not refused - the customer has already
    ''' paid and left.
    ''' </para>
    ''' <para>
    ''' Failures. A change that cannot be sent stays queued with the reason, and is retried on the
    ''' next run; nothing is ever dropped because the network failed. A change whose record no
    ''' longer exists on the server (someone deleted it) is dropped: the deletion wins.
    ''' </para>
    ''' </remarks>
    Public NotInheritable Class SyncEngine

        ''' <summary>
        ''' How far back each pull re-reads, in sync versions. A version is taken when a row is
        ''' written but only visible once its transaction commits, so a slow transaction can
        ''' become visible after a later one has already been pulled. Re-reading a margin catches
        ''' those; the rows are applied idempotently, so re-reading costs nothing but bandwidth.
        ''' </summary>
        Public Const PullOverlap As Long = 100

        Private Const PageSize As Integer = 500
        Private Const PushBatchSize As Integer = 200

        Private Shared ReadOnly _running As New SemaphoreSlim(1, 1)

        Private ReadOnly _localConnectionString As String
        Private ReadOnly _serverConnectionString As String

        Public Sub New(localDatabasePath As String, serverConnectionString As String)
            _localConnectionString = LocalStore.ConnectionStringFor(localDatabasePath)
            _serverConnectionString = serverConnectionString
        End Sub

        ''' <summary>A context on the local file, scoped to one organization.</summary>
        Public Function NewLocalContext(organizationId As Integer) As GmsDbContext
            Dim options = New DbContextOptionsBuilder(Of GmsDbContext)().
                UseSqlite(_localConnectionString).
                UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).Options
            Return New GmsDbContext(options, New FixedTenant(organizationId))
        End Function

        ''' <summary>A context on PostgreSQL, scoped to one organization.</summary>
        Public Function NewServerContext(organizationId As Integer) As GmsDbContext
            Dim options = New DbContextOptionsBuilder(Of GmsDbContext)().
                UseNpgsql(_serverConnectionString).
                UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).Options
            Return New GmsDbContext(options, New FixedTenant(organizationId))
        End Function

        ''' <summary>Whether PostgreSQL answers within <paramref name="timeoutSeconds"/>.</summary>
        Public Function IsServerReachable(Optional timeoutSeconds As Integer = 5) As Boolean
            Try
                Dim builder As New NpgsqlConnectionStringBuilder(_serverConnectionString) With {
                    .Timeout = timeoutSeconds}
                Using connection As New NpgsqlConnection(builder.ToString())
                    connection.Open()
                End Using
                Return True
            Catch ex As Exception When TypeOf ex Is NpgsqlException OrElse
                                       TypeOf ex Is TimeoutException OrElse
                                       TypeOf ex Is Net.Sockets.SocketException OrElse
                                       TypeOf ex Is InvalidOperationException
                Return False
            End Try
        End Function

        ''' <summary>Changes made here for this organization that the server has not received yet.</summary>
        Public Function PendingCount(organizationId As Integer) As Integer
            Using local = NewLocalContext(organizationId)
                Return local.Set(Of OutboxEntry)().Where(Function(o) o.OrganizationId = organizationId).
                    Select(Function(o) New With {o.EntityName, o.LocalId}).Distinct().Count()
            End Using
        End Function

        ''' <summary>Push this organization's changes, then pull the server's. Safe to call often.</summary>
        Public Function Run(organizationId As Integer) As SyncReport
            Dim report As New SyncReport()
            If Not _running.Wait(0) Then
                report.Skipped = True
                Return report
            End If

            Try
                report.Reachable = IsServerReachable()
                If Not report.Reachable Then Return report

                Using local = NewLocalContext(organizationId), server = NewServerContext(organizationId)
                    local.SuppressOutbox = True
                    ' Held open so the foreign-key pragma below sticks to this one connection.
                    local.Database.OpenConnection()
                    Dim ids = IdMap.Load(local)

                    Push(local, server, organizationId, ids, report)

                    ' A pull applies rows in the order the server changed them, which is not always
                    ' parent-first (a category edited after its child was created, deletions
                    ' cascaded on the server). The server has already enforced its foreign keys.
                    local.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF;")
                    Try
                        Pull(local, server, organizationId, ids, report)
                    Finally
                        local.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON;")
                    End Try
                End Using
            Catch ex As Exception When IsConnectionFailure(ex)
                ' Lost the connection part-way. Everything not yet confirmed is still queued.
                report.Reachable = False
                report.Errors.Add("The connection was lost during sync: " & Unwrap(ex).Message)
            Finally
                report.FinishedAtUtc = DateTime.UtcNow
                _running.Release()
            End Try
            Return report
        End Function

#Region "Push"

        Private NotInheritable Class PendingRow
            Public Property EntityName As String
            Public Property LocalId As Integer
            Public Property Entries As List(Of OutboxEntry)

            Public ReadOnly Property IsDelete As Boolean
                Get
                    Return Entries.OrderBy(Function(e) e.Seq).Last().Operation = OutboxOperation.Delete
                End Get
            End Property

            Public ReadOnly Property Seqs As List(Of Long)
                Get
                    Return Entries.Select(Function(e) e.Seq).ToList()
                End Get
            End Property

            ''' <summary>Every property any queued update touched.</summary>
            Public ReadOnly Property ChangedProperties As HashSet(Of String)
                Get
                    Return New HashSet(Of String)(
                        Entries.Where(Function(e) e.Operation = OutboxOperation.Update).
                            SelectMany(Function(e) e.ChangedProperties.Split(","c, StringSplitOptions.RemoveEmptyEntries)),
                        StringComparer.Ordinal)
                End Get
            End Property
        End Class

        Private Sub Push(local As GmsDbContext, server As GmsDbContext, organizationId As Integer,
                         ids As IdMap, report As SyncReport)
            Dim rows = local.Set(Of OutboxEntry)().
                Where(Function(o) o.OrganizationId = organizationId).
                OrderBy(Function(o) o.Seq).ToList().
                GroupBy(Function(o) New With {Key o.EntityName, Key o.LocalId}).
                Select(Function(g) New PendingRow With {
                    .EntityName = g.Key.EntityName, .LocalId = g.Key.LocalId, .Entries = g.ToList()}).
                ToList()
            If rows.Count = 0 Then Return

            ' Inserts and updates parent-first, oldest change first within a table so a category
            ' created offline goes before the sub-category created under it.
            For Each type In SyncCatalog.Pushed
                Dim ofType = rows.Where(Function(r) r.EntityName = type.Name AndAlso Not r.IsDelete).
                    OrderBy(Function(r) r.Entries.Min(Function(e) e.Seq)).ToList()
                If ofType.Count = 0 Then Continue For
                InvokeGeneric(NameOf(PushUpserts), type, local, server, organizationId, ids, ofType, report)
            Next

            ' Deletions child-first, so a parent is never deleted while a child still points at it.
            For Each type In SyncCatalog.Pushed.Reverse()
                Dim ofType = rows.Where(Function(r) r.EntityName = type.Name AndAlso r.IsDelete).ToList()
                If ofType.Count = 0 Then Continue For
                InvokeGeneric(NameOf(PushDeletes), type, local, server, organizationId, ids, ofType, report)
            Next
        End Sub

        Private Sub PushUpserts(Of T As {EntityBase, New})(local As GmsDbContext, server As GmsDbContext,
                                                           organizationId As Integer, ids As IdMap,
                                                           rows As List(Of PendingRow), report As SyncReport)
            Dim name = GetType(T).Name
            Dim localIds = rows.Select(Function(r) r.LocalId).ToList()
            Dim current = local.Set(Of T)().IgnoreQueryFilters().
                Where(Function(e) localIds.Contains(e.Id)).ToList().
                ToDictionary(Function(e) e.Id)

            Dim inserts As New List(Of (Row As PendingRow, Entity As T))()
            Dim updates As New List(Of (Row As PendingRow, Entity As T, ServerId As Integer, Properties As IReadOnlyCollection(Of String)))()

            For Each row In rows
                Dim entity As T = Nothing
                If Not current.TryGetValue(row.LocalId, entity) Then
                    ' Gone locally without a delete being queued - nothing left to send.
                    Forget(local, row.Seqs)
                    Continue For
                End If

                Dim serverId = ids.ToServer(name, row.LocalId)
                If serverId.HasValue Then
                    Dim props = SendableProperties(local, GetType(T), row.ChangedProperties)
                    If props.Count = 0 Then
                        Forget(local, row.Seqs)
                    Else
                        updates.Add((row, entity, serverId.Value, props))
                    End If
                Else
                    inserts.Add((row, entity))
                End If
            Next

            If inserts.Count > 0 Then PushInserts(local, server, organizationId, ids, inserts, report)
            If updates.Count > 0 Then PushUpdates(local, server, ids, updates, report)
        End Sub

        Private Sub PushInserts(Of T As {EntityBase, New})(local As GmsDbContext, server As GmsDbContext,
                                                           organizationId As Integer, ids As IdMap,
                                                           inserts As List(Of (Row As PendingRow, Entity As T)),
                                                           report As SyncReport)
            Dim name = GetType(T).Name

            ' A previous run may have inserted some of these and then lost the connection before it
            ' could record that locally. Those are found by SyncId and adopted, not inserted twice.
            Dim syncIds = inserts.Select(Function(i) i.Entity.SyncId).ToList()
            Dim alreadyThere = server.Set(Of T)().IgnoreQueryFilters().
                Where(Function(e) syncIds.Contains(e.SyncId)).
                Select(Function(e) New With {e.SyncId, e.Id}).ToList().
                ToDictionary(Function(e) e.SyncId, Function(e) e.Id)

            Dim fresh As New List(Of (Row As PendingRow, Entity As T))()
            For Each item In inserts
                Dim existingId As Integer
                If alreadyThere.TryGetValue(item.Entity.SyncId, existingId) Then
                    ids.Add(local, name, item.Row.LocalId, existingId)
                    Forget(local, item.Row.Seqs)
                    report.Sent += 1
                Else
                    fresh.Add(item)
                End If
            Next

            For Each chunk In fresh.Chunk(PushBatchSize)
                If Not TryInsertBatch(local, server, organizationId, ids, chunk, report) Then
                    ' Something in the batch was refused. Send them one at a time so one bad row
                    ' holds back only itself and whatever depends on it.
                    For Each item In chunk
                        TryInsertBatch(local, server, organizationId, ids, {item}, report)
                    Next
                End If
            Next
        End Sub

        ''' <summary>Inserts rows in one server transaction. Returns False (and changes nothing) on failure.</summary>
        Private Function TryInsertBatch(Of T As {EntityBase, New})(local As GmsDbContext, server As GmsDbContext,
                                                                   organizationId As Integer, ids As IdMap,
                                                                   batch As IList(Of (Row As PendingRow, Entity As T)),
                                                                   report As SyncReport) As Boolean
            Dim name = GetType(T).Name
            Dim outgoing As New List(Of (Row As PendingRow, Copy As T))()
            Try
                For Each item In batch
                    Dim copy = CopyOf(item.Entity)
                    TranslateReferences(local, copy, ids, toServer:=True)
                    If Not TranslateShopStock(server, copy, ids, local, item.Row, report) Then Continue For
                    For Each prop In SyncCatalog.NeverSent.GetValueOrDefault(GetType(T), Array.Empty(Of String)())
                        ' The server works these out from the movements; see SyncCatalog.NeverSent.
                        GetType(T).GetProperty(prop).SetValue(copy, 0D)
                    Next
                    copy.Id = 0
                    outgoing.Add((item.Row, copy))
                Next
            Catch ex As PendingReferenceException
                If batch.Count = 1 Then
                    MarkFailed(local, batch(0).Row, ex.Message, report)
                    Return True
                End If
                Return False
            End Try
            If outgoing.Count = 0 Then Return True

            Using tx = server.Database.BeginTransaction()
                Try
                    server.Set(Of T)().AddRange(outgoing.Select(Function(o) o.Copy))
                    server.SaveChanges()
                    If GetType(T) Is GetType(StockMovement) Then
                        ApplyStockEffects(server, organizationId,
                                          outgoing.Select(Function(o) DirectCast(CObj(o.Copy), StockMovement)).ToList(),
                                          report)
                    End If
                    tx.Commit()
                Catch ex As Exception When Not IsConnectionFailure(ex)
                    tx.Rollback()
                    server.ChangeTracker.Clear()
                    If batch.Count = 1 Then
                        MarkFailed(local, batch(0).Row, Unwrap(ex).Message, report)
                        Return True
                    End If
                    Return False
                End Try
            End Using
            server.ChangeTracker.Clear()

            For Each item In outgoing
                ids.Add(local, name, item.Row.LocalId, item.Copy.Id)
                Forget(local, item.Row.Seqs)
                report.Sent += 1
            Next
            Return True
        End Function

        ''' <summary>
        ''' One balance row per product per shop is a rule on the server, so a shop-stock row made
        ''' here for a pair the server already has (someone else stocked that shop first) is
        ''' matched to the server's row instead of inserted. Returns False when it was matched,
        ''' having sent this computer's price for it as an update.
        ''' </summary>
        Private Shared Function TranslateShopStock(Of T As {EntityBase, New})(server As GmsDbContext, copy As T,
                                                                              ids As IdMap, local As GmsDbContext,
                                                                              row As PendingRow, report As SyncReport) As Boolean
            Dim stock = TryCast(copy, ShopStock)
            If stock Is Nothing Then Return True

            Dim existing = server.ShopStocks.IgnoreQueryFilters().
                Where(Function(s) s.ShopId = stock.ShopId AndAlso s.ProductId = stock.ProductId).
                Select(Function(s) New With {s.Id}).FirstOrDefault()
            If existing Is Nothing Then Return True

            ids.Add(local, NameOf(ShopStock), row.LocalId, existing.Id)
            If stock.UnitPrice.HasValue Then
                server.ShopStocks.Where(Function(s) s.Id = existing.Id).
                    ExecuteUpdate(Sub(setters) setters.SetProperty(Function(s) s.UnitPrice, stock.UnitPrice))
            End If
            Forget(local, row.Seqs)
            report.Sent += 1
            Return False
        End Function

        Private Sub PushUpdates(Of T As {EntityBase, New})(local As GmsDbContext, server As GmsDbContext, ids As IdMap,
                                                           updates As List(Of (Row As PendingRow, Entity As T, ServerId As Integer, Properties As IReadOnlyCollection(Of String))),
                                                           report As SyncReport)
            For Each chunk In updates.Chunk(PushBatchSize)
                If Not TryUpdateBatch(local, server, ids, chunk, report) Then
                    For Each item In chunk
                        TryUpdateBatch(local, server, ids, {item}, report)
                    Next
                End If
            Next
        End Sub

        Private Function TryUpdateBatch(Of T As {EntityBase, New})(local As GmsDbContext, server As GmsDbContext, ids As IdMap,
                                                                   batch As IList(Of (Row As PendingRow, Entity As T, ServerId As Integer, Properties As IReadOnlyCollection(Of String))),
                                                                   report As SyncReport) As Boolean
            Try
                For Each item In batch
                    Dim copy = CopyOf(item.Entity)
                    TranslateReferences(local, copy, ids, toServer:=True)

                    ' A stub carrying only the key and the changed values, so the UPDATE names only
                    ' those columns and leaves every other field as the server has it.
                    Dim stub As New T() With {.Id = item.ServerId}
                    For Each prop In item.Properties
                        Dim info = GetType(T).GetProperty(prop)
                        info.SetValue(stub, info.GetValue(copy))
                    Next
                    Dim tracked = server.Attach(stub)
                    For Each prop In item.Properties
                        tracked.Property(prop).IsModified = True
                    Next
                Next
            Catch ex As PendingReferenceException
                server.ChangeTracker.Clear()
                If batch.Count = 1 Then
                    MarkFailed(local, batch(0).Row, ex.Message, report)
                    Return True
                End If
                Return False
            End Try

            Try
                server.SaveChanges()
            Catch ex As DbUpdateConcurrencyException When batch.Count = 1
                ' The row is gone from the server: someone deleted it. The deletion wins, and the
                ' pull will remove the local copy.
                server.ChangeTracker.Clear()
                Forget(local, batch(0).Row.Seqs)
                Return True
            Catch ex As Exception When Not IsConnectionFailure(ex)
                server.ChangeTracker.Clear()
                If batch.Count = 1 Then
                    MarkFailed(local, batch(0).Row, Unwrap(ex).Message, report)
                    Return True
                End If
                Return False
            End Try
            server.ChangeTracker.Clear()

            For Each item In batch
                Forget(local, item.Row.Seqs)
                report.Sent += 1
            Next
            Return True
        End Function

        Private Sub PushDeletes(Of T As {EntityBase, New})(local As GmsDbContext, server As GmsDbContext,
                                                           organizationId As Integer, ids As IdMap,
                                                           rows As List(Of PendingRow), report As SyncReport)
            Dim name = GetType(T).Name
            For Each row In rows
                Dim serverId = ids.ToServer(name, row.LocalId)
                If Not serverId.HasValue Then
                    ' Created and deleted without ever being sent: the server never needs to know.
                    Forget(local, row.Seqs)
                    Continue For
                End If

                Try
                    Dim only = serverId.Value
                    server.Set(Of T)().IgnoreQueryFilters().Where(Function(e) e.Id = only).ExecuteDelete()
                    ids.Remove(local, name, row.LocalId)
                    Forget(local, row.Seqs)
                    report.Sent += 1
                Catch ex As Exception When Not IsConnectionFailure(ex)
                    MarkFailed(local, row, Unwrap(ex).Message, report)
                End Try
            Next
        End Sub

        ''' <summary>
        ''' Adds the effect of stock movements made here to the server's cached totals, in the same
        ''' transaction as the movements themselves, then flags anything that went below zero.
        ''' </summary>
        ''' <remarks>
        ''' Added to the server's figure rather than replacing it with this computer's, so that a
        ''' sale made online while this computer was offline is still counted. Mirrors
        ''' InventoryService.ApplyMovement: an allocation moves stock between locations and so
        ''' leaves the organization total alone; everything else changes both.
        ''' </remarks>
        Private Shared Sub ApplyStockEffects(server As GmsDbContext, organizationId As Integer,
                                             movements As List(Of StockMovement), report As SyncReport)
            Dim byProduct = movements.Where(Function(m) m.Reason <> StockMovementReason.Allocation).
                GroupBy(Function(m) m.ProductId).
                Select(Function(g) New With {.ProductId = g.Key, .Delta = g.Sum(Function(m) m.SignedQuantity)}).
                ToList()
            If byProduct.Count > 0 Then
                server.Database.ExecuteSqlRaw(
                    "update products p set quantity_on_hand = p.quantity_on_hand + d.delta " &
                    "from unnest(@ids, @deltas) as d(id, delta) where p.id = d.id",
                    IntArray("ids", byProduct.Select(Function(p) p.ProductId)),
                    DecimalArray("deltas", byProduct.Select(Function(p) p.Delta)))
            End If

            Dim byShop = movements.Where(Function(m) m.ShopId.HasValue).
                GroupBy(Function(m) New With {Key .ShopId = m.ShopId.Value, Key m.ProductId}).
                Select(Function(g) New With {g.Key.ShopId, g.Key.ProductId, .Delta = g.Sum(Function(m) m.SignedQuantity)}).
                ToList()
            If byShop.Count > 0 Then
                server.Database.ExecuteSqlRaw(
                    "insert into shop_stocks (organization_id, shop_id, product_id, quantity_on_hand) " &
                    "select @org, d.shop_id, d.product_id, d.delta " &
                    "from unnest(@shops, @products, @deltas) as d(shop_id, product_id, delta) " &
                    "on conflict (shop_id, product_id) do update " &
                    "set quantity_on_hand = shop_stocks.quantity_on_hand + excluded.quantity_on_hand",
                    New NpgsqlParameter("org", organizationId),
                    IntArray("shops", byShop.Select(Function(s) s.ShopId)),
                    IntArray("products", byShop.Select(Function(s) s.ProductId)),
                    DecimalArray("deltas", byShop.Select(Function(s) s.Delta)))
            End If

            FlagNegativeStock(server, organizationId, movements.Select(Function(m) m.ProductId).Distinct().ToList(), report)
        End Sub

        ''' <summary>
        ''' Raises one notification per product and location that is now below zero. Kept, not
        ''' refused: the sale happened. Someone needs to count the shelf and adjust.
        ''' </summary>
        Private Shared Sub FlagNegativeStock(server As GmsDbContext, organizationId As Integer,
                                             productIds As List(Of Integer), report As SyncReport)
            Dim products = server.Products.
                Where(Function(p) productIds.Contains(p.Id)).
                Select(Function(p) New With {p.Id, p.Name, p.Sku, p.QuantityOnHand}).ToList()
            Dim names = products.ToDictionary(Function(p) p.Id, Function(p) $"{p.Name} ({p.Sku})")
            Dim shopNames = server.Shops.Select(Function(s) New With {s.Id, s.Name}).ToList().
                ToDictionary(Function(s) s.Id, Function(s) s.Name)

            Dim found As New List(Of (Key As String, Text As String, ProductId As Integer))()
            For Each p In products.Where(Function(x) x.QuantityOnHand < 0D)
                found.Add(($"negative-stock:{p.Id}:all", $"{names(p.Id)} is at {p.QuantityOnHand:0.###} across the organization", p.Id))
            Next
            For Each s In server.ShopStocks.
                    Where(Function(x) productIds.Contains(x.ProductId) AndAlso x.QuantityOnHand < 0D).
                    Select(Function(x) New With {x.ShopId, x.ProductId, x.QuantityOnHand}).ToList()
                Dim shopName = shopNames.GetValueOrDefault(s.ShopId, $"Shop #{s.ShopId}")
                found.Add(($"negative-stock:{s.ProductId}:{s.ShopId}",
                           $"{names.GetValueOrDefault(s.ProductId, $"Product #{s.ProductId}")} is at {s.QuantityOnHand:0.###} at {shopName}",
                           s.ProductId))
            Next
            If found.Count = 0 Then Return

            Dim keys = found.Select(Function(f) f.Key).ToList()
            Dim alreadyRaised = server.Notifications.
                Where(Function(n) Not n.IsRead AndAlso keys.Contains(n.DedupeKey)).
                Select(Function(n) n.DedupeKey).ToList()

            For Each item In found
                report.NegativeStock.Add(item.Text)
                If alreadyRaised.Contains(item.Key) Then Continue For
                server.Notifications.Add(New Notification With {
                    .OrganizationId = organizationId,
                    .Type = NotificationType.LowStock,
                    .Severity = NotificationSeverity.Critical,
                    .Title = "Stock below zero after offline sales",
                    .Message = item.Text & ". Sales made while offline used stock that had already " &
                               "been sold elsewhere. Count what is really there and adjust the stock.",
                    .RelatedEntityName = NameOf(Product),
                    .RelatedEntityId = item.ProductId.ToString(Globalization.CultureInfo.InvariantCulture),
                    .DedupeKey = item.Key,
                    .CreatedAtUtc = DateTime.UtcNow})
            Next
            server.SaveChanges()
        End Sub

        Private Shared Function IntArray(name As String, values As IEnumerable(Of Integer)) As NpgsqlParameter
            Return New NpgsqlParameter(name, NpgsqlDbType.Array Or NpgsqlDbType.Integer) With {.Value = values.ToArray()}
        End Function

        Private Shared Function DecimalArray(name As String, values As IEnumerable(Of Decimal)) As NpgsqlParameter
            Return New NpgsqlParameter(name, NpgsqlDbType.Array Or NpgsqlDbType.Numeric) With {.Value = values.ToArray()}
        End Function

        ''' <summary>The changed properties that may be sent: not the key, not the SyncId, not a server-computed total.</summary>
        Private Shared Function SendableProperties(local As GmsDbContext, type As Type,
                                                   changed As HashSet(Of String)) As IReadOnlyCollection(Of String)
            Dim entityType = local.Model.FindEntityType(type)
            Return changed.Where(Function(name)
                                     Dim prop = entityType.FindProperty(name)
                                     Return prop IsNot Nothing AndAlso prop.PropertyInfo IsNot Nothing AndAlso
                                            Not prop.IsPrimaryKey() AndAlso
                                            name <> NameOf(EntityBase.SyncId) AndAlso
                                            Not SyncCatalog.IsNeverSent(type, name)
                                 End Function).ToList()
        End Function

        ''' <summary>Removes queued changes that have been dealt with.</summary>
        Private Shared Sub Forget(local As GmsDbContext, seqs As List(Of Long))
            local.Set(Of OutboxEntry)().Where(Function(o) seqs.Contains(o.Seq)).ExecuteDelete()
        End Sub

        Private Shared Sub MarkFailed(local As GmsDbContext, row As PendingRow, message As String, report As SyncReport)
            Dim seqs = row.Seqs
            local.Set(Of OutboxEntry)().Where(Function(o) seqs.Contains(o.Seq)).
                ExecuteUpdate(Sub(setters) setters.
                    SetProperty(Function(o) o.LastError, message).
                    SetProperty(Function(o) o.Attempts, Function(o) o.Attempts + 1))
            report.Failed += 1
            report.Errors.Add($"{row.EntityName} {row.LocalId}: {message}")
        End Sub

#End Region

#Region "Pull"

        Private NotInheritable Class PulledRow(Of T)
            Public Property Row As T
            Public Property Version As Long
        End Class

        Private Sub Pull(local As GmsDbContext, server As GmsDbContext, organizationId As Integer,
                         ids As IdMap, report As SyncReport)
            For Each type In SyncCatalog.Pulled
                InvokeGeneric(NameOf(PullType), type, local, server, organizationId, ids, report)
            Next
            PullRolePermissions(local, server)
            PullDeletions(local, server, organizationId, ids, report)
        End Sub

        Private Sub PullType(Of T As {EntityBase, New})(local As GmsDbContext, server As GmsDbContext,
                                                        organizationId As Integer, ids As IdMap, report As SyncReport)
            Dim name = GetType(T).Name
            Dim cursorKey = $"pull:{organizationId}:{name}"
            Dim stored = ReadCursor(local, cursorKey)
            Dim after = stored - PullOverlap
            Dim highest = stored

            Do
                Dim from = after
                Dim page = ServerRows(Of T)(server, organizationId).
                    Where(Function(e) EF.Property(Of Long)(e, GmsDbContext.SyncVersionProperty) > from).
                    OrderBy(Function(e) EF.Property(Of Long)(e, GmsDbContext.SyncVersionProperty)).
                    Select(Function(e) New PulledRow(Of T) With {
                        .Row = e, .Version = EF.Property(Of Long)(e, GmsDbContext.SyncVersionProperty)}).
                    Take(PageSize).ToList()
                If page.Count = 0 Then Exit Do

                ApplyPulled(local, page.Select(Function(p) p.Row).ToList(), ids)
                report.Received += page.Where(Function(p) p.Version > stored).Count()
                after = page.Last().Version
                highest = Math.Max(highest, after)
                If page.Count < PageSize Then Exit Do
            Loop

            If highest <> stored Then LocalSyncState.Write(local, cursorKey, highest.ToString(Globalization.CultureInfo.InvariantCulture))
        End Sub

        ''' <summary>
        ''' The rows of one table this organization may see on the server. Tenant tables are
        ''' already filtered by the context; these four have no tenant filter of their own.
        ''' </summary>
        Private Shared Function ServerRows(Of T As EntityBase)(server As GmsDbContext, organizationId As Integer) As IQueryable(Of T)
            If GetType(T) Is GetType(Organization) Then
                Return DirectCast(CObj(server.Organizations.Where(Function(o) o.Id = organizationId)), IQueryable(Of T))
            End If
            If GetType(T) Is GetType(Subscription) Then
                Return DirectCast(CObj(server.Subscriptions.Where(Function(s) s.OrganizationId = organizationId)), IQueryable(Of T))
            End If
            Return server.Set(Of T)()
        End Function

        ''' <summary>
        ''' Writes server rows into the local file: updates the local copy when there is one,
        ''' inserts otherwise. A row with changes still waiting to be sent is left alone - the
        ''' next run sends those changes, which gives the row a new version on the server, and
        ''' the pull after that brings back the merged result.
        ''' </summary>
        Private Shared Sub ApplyPulled(Of T As {EntityBase, New})(local As GmsDbContext, rows As List(Of T), ids As IdMap)
            Dim name = GetType(T).Name
            ' Immediate, so the screens cannot slip a change in between the check for waiting
            ' changes below and the write that would overwrite it.
            Using tx = local.Database.BeginTransaction()
                Dim waiting = local.Set(Of OutboxEntry)().Where(Function(o) o.EntityName = name).
                    Select(Function(o) o.LocalId).Distinct().ToList().ToHashSet()

                Dim syncIds = rows.Select(Function(r) r.SyncId).ToList()
                Dim bySyncId = local.Set(Of T)().IgnoreQueryFilters().
                    Where(Function(e) syncIds.Contains(e.SyncId)).
                    Select(Function(e) New With {e.SyncId, e.Id}).ToList().
                    ToDictionary(Function(e) e.SyncId, Function(e) e.Id)
                Dim candidates = rows.Select(Function(r) ids.ToLocal(name, r.Id)).ToList()
                Dim existing = local.Set(Of T)().IgnoreQueryFilters().
                    Where(Function(e) candidates.Contains(e.Id)).
                    Select(Function(e) e.Id).ToList().ToHashSet()

                For Each row In rows
                    Dim localId As Integer
                    If Not bySyncId.TryGetValue(row.SyncId, localId) Then localId = ids.ToLocal(name, row.Id)
                    If waiting.Contains(localId) Then Continue For

                    TranslateReferences(local, row, ids, toServer:=False)
                    row.Id = localId
                    If bySyncId.ContainsKey(row.SyncId) OrElse existing.Contains(localId) Then
                        local.Update(row)
                    Else
                        local.Add(row)
                    End If
                Next
                local.SaveChanges()
                local.ChangeTracker.Clear()
                tx.Commit()
            End Using
        End Sub

        ''' <summary>
        ''' Role-permission links have no id or version of their own; the table is small and global,
        ''' so it is simply replaced each time.
        ''' </summary>
        Private Shared Sub PullRolePermissions(local As GmsDbContext, server As GmsDbContext)
            Dim links = server.RolePermissions.Select(Function(rp) New RolePermission With {
                .RoleId = rp.RoleId, .PermissionId = rp.PermissionId}).ToList()
            Using tx = local.Database.BeginTransaction()
                local.RolePermissions.ExecuteDelete()
                local.RolePermissions.AddRange(links)
                local.SaveChanges()
                local.ChangeTracker.Clear()
                tx.Commit()
            End Using
        End Sub

        Private Sub PullDeletions(local As GmsDbContext, server As GmsDbContext, organizationId As Integer,
                                  ids As IdMap, report As SyncReport)
            Dim cursorKey = $"pull:{organizationId}:deletions"
            Dim stored = ReadCursor(local, cursorKey)
            Dim highest = stored
            Dim byTable = local.Model.GetEntityTypes().
                Where(Function(e) GetType(EntityBase).IsAssignableFrom(e.ClrType) AndAlso e.GetTableName() IsNot Nothing).
                ToDictionary(Function(e) e.GetTableName(), Function(e) e.ClrType, StringComparer.Ordinal)

            Dim connection = server.Database.GetDbConnection()
            server.Database.OpenConnection()
            Try
                Do
                    Dim page As New List(Of (Table As String, SyncId As Guid, Version As Long))()
                    Using command = connection.CreateCommand()
                        command.CommandText =
                            "select table_name, sync_id, sync_version from sync_tombstones " &
                            "where (organization_id = @org or organization_id is null) and sync_version > @after " &
                            "order by sync_version limit " & PageSize.ToString(Globalization.CultureInfo.InvariantCulture)
                        command.Parameters.Add(New NpgsqlParameter("org", organizationId))
                        command.Parameters.Add(New NpgsqlParameter("after", highest - If(highest = stored, PullOverlap, 0L)))
                        Using reader = command.ExecuteReader()
                            While reader.Read()
                                page.Add((reader.GetString(0), reader.GetGuid(1), reader.GetInt64(2)))
                            End While
                        End Using
                    End Using
                    If page.Count = 0 Then Exit Do

                    For Each group In page.GroupBy(Function(p) p.Table)
                        Dim type As Type = Nothing
                        If Not byTable.TryGetValue(group.Key, type) Then Continue For
                        InvokeGeneric(NameOf(DeleteLocal), type, local, ids,
                                      group.Select(Function(g) g.SyncId).ToList(), report)
                    Next
                    highest = Math.Max(highest, page.Last().Version)
                    If page.Count < PageSize Then Exit Do
                Loop
            Finally
                server.Database.CloseConnection()
            End Try

            If highest <> stored Then LocalSyncState.Write(local, cursorKey, highest.ToString(Globalization.CultureInfo.InvariantCulture))
        End Sub

        ''' <summary>
        ''' Removes rows the server deleted. A deletion on the server wins over an edit waiting
        ''' here, so that edit is dropped with the row.
        ''' </summary>
        Private Sub DeleteLocal(Of T As {EntityBase, New})(local As GmsDbContext, ids As IdMap,
                                                           syncIds As List(Of Guid), report As SyncReport)
            Dim name = GetType(T).Name
            Dim gone = local.Set(Of T)().IgnoreQueryFilters().
                Where(Function(e) syncIds.Contains(e.SyncId)).Select(Function(e) e.Id).ToList()
            If gone.Count = 0 Then Return

            local.Set(Of T)().IgnoreQueryFilters().Where(Function(e) gone.Contains(e.Id)).ExecuteDelete()
            local.Set(Of OutboxEntry)().Where(Function(o) o.EntityName = name AndAlso gone.Contains(o.LocalId)).ExecuteDelete()
            For Each id In gone
                ids.Remove(local, name, id)
            Next
            report.Received += gone.Count
        End Sub

        Private Shared Function ReadCursor(local As GmsDbContext, key As String) As Long
            Dim text = LocalSyncState.Read(local, key)
            Dim value As Long
            If Long.TryParse(text, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, value) Then Return value
            Return 0
        End Function

#End Region

#Region "References"

        Private Structure Reference
            Public PropertyInfo As PropertyInfo
            ''' <summary>CLR name of the referenced entity.</summary>
            Public Target As String
        End Structure

        Private Shared ReadOnly _references As New Concurrent.ConcurrentDictionary(Of Type, Reference())()

        ''' <summary>
        ''' Every integer property of <paramref name="type"/> that holds another row's id: the
        ''' foreign keys EF knows about, plus the "...UserId" columns that deliberately have none
        ''' (audit and stock rows must outlive a deleted user, so the schema has no FK for them).
        ''' </summary>
        Private Shared Function ReferencesOf(model As IModel, type As Type) As Reference()
            Return _references.GetOrAdd(type,
                Function(t)
                    Dim entityType = model.FindEntityType(t)
                    Dim found As New List(Of Reference)()
                    For Each fk In entityType.GetForeignKeys()
                        If fk.Properties.Count <> 1 Then Continue For
                        Dim prop = fk.Properties(0)
                        If prop.PropertyInfo Is Nothing Then Continue For
                        If Not GetType(EntityBase).IsAssignableFrom(fk.PrincipalEntityType.ClrType) Then Continue For
                        found.Add(New Reference With {.PropertyInfo = prop.PropertyInfo, .Target = fk.PrincipalEntityType.ClrType.Name})
                    Next
                    For Each prop In entityType.GetProperties()
                        If prop.PropertyInfo Is Nothing OrElse prop.IsForeignKey() OrElse prop.IsPrimaryKey() Then Continue For
                        If prop.ClrType IsNot GetType(Integer) AndAlso prop.ClrType IsNot GetType(Integer?) Then Continue For
                        If Not prop.Name.EndsWith("UserId", StringComparison.Ordinal) Then Continue For
                        found.Add(New Reference With {.PropertyInfo = prop.PropertyInfo, .Target = NameOf(User)})
                    Next
                    Return found.ToArray()
                End Function)
        End Function

        ''' <summary>
        ''' Rewrites the ids a row holds from local to server numbering or back. Going to the
        ''' server, a reference to a row not sent yet throws <see cref="PendingReferenceException"/>
        ''' so the row waits for its parent rather than arriving pointing at nothing.
        ''' </summary>
        Private Shared Sub TranslateReferences(local As GmsDbContext, row As Object, ids As IdMap, toServer As Boolean)
            For Each ref In ReferencesOf(local.Model, row.GetType())
                Dim value = ref.PropertyInfo.GetValue(row)
                If value Is Nothing Then Continue For
                Dim id = CInt(value)
                If toServer Then
                    Dim mapped = ids.ToServer(ref.Target, id)
                    If Not mapped.HasValue Then Throw New PendingReferenceException(ref.Target, id)
                    ref.PropertyInfo.SetValue(row, mapped.Value)
                Else
                    ref.PropertyInfo.SetValue(row, ids.ToLocal(ref.Target, id))
                End If
            Next

            ' Audit and notification rows name the row they are about as text.
            Dim audit = TryCast(row, AuditEntry)
            If audit IsNot Nothing Then audit.EntityId = TranslateText(audit.EntityName, audit.EntityId, ids, toServer)
            Dim note = TryCast(row, Notification)
            If note IsNot Nothing Then note.RelatedEntityId = TranslateText(note.RelatedEntityName, note.RelatedEntityId, ids, toServer)
        End Sub

        Private Shared Function TranslateText(entityName As String, value As String, ids As IdMap, toServer As Boolean) As String
            Dim id As Integer
            If SyncCatalog.Find(entityName) Is Nothing OrElse
               Not Integer.TryParse(value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, id) Then
                Return value
            End If
            Dim translated = If(toServer, If(ids.ToServer(entityName, id), id), ids.ToLocal(entityName, id))
            Return translated.ToString(Globalization.CultureInfo.InvariantCulture)
        End Function

        Private Shared Function CopyOf(Of T As {EntityBase, New})(source As T) As T
            Dim copy As New T()
            For Each prop In GetType(T).GetProperties(BindingFlags.Public Or BindingFlags.Instance)
                If Not prop.CanRead OrElse Not prop.CanWrite OrElse prop.GetIndexParameters().Length > 0 Then Continue For
                Dim type = If(Nullable.GetUnderlyingType(prop.PropertyType), prop.PropertyType)
                ' Scalars only. Navigations are left empty so EF inserts just this row.
                If type.IsPrimitive OrElse type.IsEnum OrElse type Is GetType(String) OrElse type Is GetType(Decimal) OrElse
                   type Is GetType(DateTime) OrElse type Is GetType(Guid) OrElse type Is GetType(DateTimeOffset) Then
                    prop.SetValue(copy, prop.GetValue(source))
                End If
            Next
            Return copy
        End Function

#End Region

        Private Sub InvokeGeneric(methodName As String, type As Type, ParamArray args As Object())
            Dim method = GetType(SyncEngine).GetMethod(methodName, BindingFlags.NonPublic Or BindingFlags.Instance Or BindingFlags.Static)
            Try
                method.MakeGenericMethod(type).Invoke(If(method.IsStatic, Nothing, Me), args)
            Catch ex As TargetInvocationException When ex.InnerException IsNot Nothing
                Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw()
            End Try
        End Sub

        Private Shared Function Unwrap(ex As Exception) As Exception
            While ex.InnerException IsNot Nothing
                ex = ex.InnerException
            End While
            Return ex
        End Function

        ''' <summary>The network or server went away, as opposed to the server refusing a row.</summary>
        Public Shared Function IsConnectionFailure(ex As Exception) As Boolean
            Dim e = ex
            While e IsNot Nothing
                If TypeOf e Is NpgsqlException AndAlso TypeOf e IsNot PostgresException Then Return True
                If TypeOf e Is TimeoutException OrElse TypeOf e Is Net.Sockets.SocketException OrElse
                   TypeOf e Is IO.IOException Then Return True
                e = e.InnerException
            End While
            Return False
        End Function

        Private NotInheritable Class FixedTenant
            Implements ITenantContext

            Private ReadOnly _organizationId As Integer

            Public Sub New(organizationId As Integer)
                _organizationId = organizationId
            End Sub

            Public ReadOnly Property OrganizationId As Integer Implements ITenantContext.OrganizationId
                Get
                    Return _organizationId
                End Get
            End Property

            Public ReadOnly Property IsSystemMode As Boolean Implements ITenantContext.IsSystemMode
                Get
                    Return False
                End Get
            End Property
        End Class
    End Class

    ''' <summary>A row refers to another row that has not been sent to the server yet.</summary>
    Friend NotInheritable Class PendingReferenceException
        Inherits Exception

        Public Sub New(target As String, localId As Integer)
            MyBase.New($"Waiting for {target} {localId} to be sent first.")
        End Sub
    End Class

    ''' <summary>Local-to-server id pairs for rows created on this computer.</summary>
    Friend NotInheritable Class IdMap

        Private ReadOnly _toServer As New Dictionary(Of (String, Integer), Integer)()
        Private ReadOnly _toLocal As New Dictionary(Of (String, Integer), Integer)()

        Public Shared Function Load(local As GmsDbContext) As IdMap
            Dim map As New IdMap()
            For Each entry In local.Set(Of IdMapEntry)().AsNoTracking().ToList()
                map._toServer((entry.EntityName, entry.LocalId)) = entry.ServerId
                map._toLocal((entry.EntityName, entry.ServerId)) = entry.LocalId
            Next
            Return map
        End Function

        ''' <summary>
        ''' The server's id for a local one: itself when positive (it came from the server),
        ''' the recorded one when this computer created the row, Nothing when not sent yet.
        ''' </summary>
        Public Function ToServer(entityName As String, localId As Integer) As Integer?
            If localId > 0 Then Return localId
            Dim serverId As Integer
            If _toServer.TryGetValue((entityName, localId), serverId) Then Return serverId
            Return Nothing
        End Function

        ''' <summary>The local id for a server one: the local row it was created from, if any.</summary>
        Public Function ToLocal(entityName As String, serverId As Integer) As Integer
            Dim localId As Integer
            If _toLocal.TryGetValue((entityName, serverId), localId) Then Return localId
            Return serverId
        End Function

        Public Sub Add(local As GmsDbContext, entityName As String, localId As Integer, serverId As Integer)
            If localId > 0 Then Return
            _toServer((entityName, localId)) = serverId
            _toLocal((entityName, serverId)) = localId
            local.Set(Of IdMapEntry)().Where(Function(m) m.EntityName = entityName AndAlso m.LocalId = localId).ExecuteDelete()
            local.Set(Of IdMapEntry)().Add(New IdMapEntry With {.EntityName = entityName, .LocalId = localId, .ServerId = serverId})
            local.SaveChanges()
            local.ChangeTracker.Clear()
        End Sub

        Public Sub Remove(local As GmsDbContext, entityName As String, localId As Integer)
            Dim serverId As Integer
            If _toServer.TryGetValue((entityName, localId), serverId) Then
                _toServer.Remove((entityName, localId))
                _toLocal.Remove((entityName, serverId))
            End If
            local.Set(Of IdMapEntry)().Where(Function(m) m.EntityName = entityName AndAlso m.LocalId = localId).ExecuteDelete()
        End Sub
    End Class

End Namespace

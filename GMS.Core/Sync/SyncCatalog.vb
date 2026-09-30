Imports GMS.Core.Models

Namespace Sync

    ''' <summary>
    ''' Which tables the desktop client sends and receives, and in what order.
    ''' </summary>
    Public NotInheritable Class SyncCatalog

        Private Sub New()
        End Sub

        ''' <summary>
        ''' Tables changes made on this computer are sent for, parents before children so a row's
        ''' references already exist on the server when it arrives.
        ''' </summary>
        Public Shared ReadOnly Pushed As IReadOnlyList(Of Type) = {
            GetType(Category), GetType(Product), GetType(Supplier), GetType(Customer),
            GetType(Shop), GetType(User), GetType(ShopStock), GetType(AppSetting),
            GetType(Transaction), GetType(TransactionLine), GetType(StockMovement),
            GetType(Notification), GetType(AuditEntry)}

        ''' <summary>
        ''' Tables copied down from the server. The pushed ones plus reference data this client
        ''' reads but never changes: its organization, its subscription, and the roles and
        ''' permissions that sign-in needs. Anything changed locally in these is overwritten by
        ''' the next pull rather than sent.
        ''' </summary>
        Public Shared ReadOnly Pulled As IReadOnlyList(Of Type) =
            New Type() {GetType(Organization), GetType(Subscription), GetType(Role), GetType(Permission)}.
                Concat(Pushed).ToArray()

        ''' <summary>
        ''' Cached totals the server works out itself from the stock ledger. Sending this
        ''' computer's copy would overwrite sales rung up elsewhere while it was offline, so
        ''' these are never sent; the movements that produced them are.
        ''' </summary>
        Public Shared ReadOnly NeverSent As IReadOnlyDictionary(Of Type, String()) =
            New Dictionary(Of Type, String()) From {
                {GetType(Product), {NameOf(Product.QuantityOnHand)}},
                {GetType(ShopStock), {NameOf(ShopStock.QuantityOnHand)}}}

        Private Shared ReadOnly _pushedSet As New HashSet(Of Type)(Pushed)
        Private Shared ReadOnly _byName As Dictionary(Of String, Type) =
            Pulled.ToDictionary(Function(t) t.Name, StringComparer.Ordinal)

        Public Shared Function IsPushed(type As Type) As Boolean
            Return _pushedSet.Contains(type)
        End Function

        ''' <summary>The synced type with this CLR name, or Nothing.</summary>
        Public Shared Function Find(name As String) As Type
            Dim found As Type = Nothing
            If name IsNot Nothing AndAlso _byName.TryGetValue(name, found) Then Return found
            Return Nothing
        End Function

        Public Shared Function IsNeverSent(type As Type, propertyName As String) As Boolean
            Dim names As String() = Nothing
            Return NeverSent.TryGetValue(type, names) AndAlso names.Contains(propertyName)
        End Function
    End Class

End Namespace

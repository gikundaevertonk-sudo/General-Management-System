Namespace Enums

    ''' <summary>Kind of stock/money document represented by a <c>Transaction</c>.</summary>
    Public Enum TransactionType
        ''' <summary>Goods leaving stock, money owed by a customer.</summary>
        Sale = 1
        ''' <summary>Goods entering stock, money owed to a supplier.</summary>
        Purchase = 2
        ''' <summary>Manual positive stock correction (found stock, opening balance).</summary>
        AdjustmentIn = 3
        ''' <summary>Manual negative stock correction (breakage, loss, shrinkage).</summary>
        AdjustmentOut = 4
    End Enum

    ''' <summary>Lifecycle of a <c>Transaction</c>. Stock only moves on <c>Confirmed</c>.</summary>
    Public Enum TransactionStatus
        Draft = 0
        Confirmed = 1
        Cancelled = 2
    End Enum

    ''' <summary>Direction of a single <c>StockMovement</c> ledger entry.</summary>
    Public Enum StockMovementDirection
        [In] = 1
        [Out] = 2
    End Enum

    ''' <summary>Why a <c>StockMovement</c> was created.</summary>
    Public Enum StockMovementReason
        Sale = 1
        Purchase = 2
        Adjustment = 3
        CancellationReversal = 4
        OpeningBalance = 5
        ''' <summary>
        ''' Stock moved between locations. Always written in pairs - one Out at the source, one In
        ''' at the destination - so the organization's total is unchanged by it.
        ''' </summary>
        Allocation = 6
    End Enum

    Public Enum NotificationType
        LowStock = 1
        PendingApproval = 2
        System = 3
    End Enum

    Public Enum NotificationSeverity
        Info = 0
        Warning = 1
        Critical = 2
    End Enum

    Public Enum AuditAction
        Create = 1
        Update = 2
        Delete = 3
        ''' <summary>
        ''' A successful sign-in. Written by <c>AuthService</c> so the audit trail can be read as a
        ''' list of sessions, and everything else a user did can be attributed to the session it
        ''' happened in.
        ''' </summary>
        Login = 4
    End Enum

End Namespace

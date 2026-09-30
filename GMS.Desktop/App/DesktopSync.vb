Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports GMS.Core.Sync

Namespace App

    Public Enum SyncState
        ''' <summary>Not signed in yet, or no sync has finished.</summary>
        Idle
        Syncing
        ''' <summary>Reached the server; nothing is waiting.</summary>
        UpToDate
        ''' <summary>Reached the server, but some changes were refused and are still waiting.</summary>
        NeedsAttention
        ''' <summary>The server could not be reached. Work carries on locally.</summary>
        Offline
    End Enum

    ''' <summary>
    ''' Runs <see cref="SyncEngine"/> in the background while the desktop client is open, and
    ''' keeps the status the sidebar shows.
    ''' </summary>
    ''' <remarks>
    ''' A sync every <see cref="IntervalSeconds"/>, plus one on demand (the sidebar's status is
    ''' clickable). Each run works on its own database contexts on a worker thread; only the
    ''' bookkeeping afterwards happens on the UI thread, where it can safely tell the screens'
    ''' context to forget what it has cached.
    ''' </remarks>
    Public NotInheritable Class DesktopSync
        Implements IDisposable

        Public Const IntervalSeconds As Integer = 30

        Private ReadOnly _session As SessionContext
        Private ReadOnly _timer As New Timer With {.Interval = IntervalSeconds * 1000}
        Private _running As Boolean

        Public ReadOnly Property Engine As SyncEngine
        Public ReadOnly Property State As SyncState = SyncState.Idle
        ''' <summary>Changes made here that have not reached the server.</summary>
        Public ReadOnly Property Pending As Integer
        Public ReadOnly Property LastReport As SyncReport
        ''' <summary>When the server was last reached, local time; Nothing if never this session.</summary>
        Public ReadOnly Property LastContact As DateTime?

        ''' <summary>Raised on the UI thread whenever <see cref="State"/> or <see cref="Pending"/> changes.</summary>
        Public Event StatusChanged As EventHandler

        ''' <summary>
        ''' Raised on the UI thread when this computer's offline sales took stock below zero on
        ''' the server. The server has already raised a notification for each one.
        ''' </summary>
        Public Event NegativeStockFound As EventHandler(Of IReadOnlyList(Of String))

        Public Sub New(engine As SyncEngine, session As SessionContext)
            Me.Engine = engine
            _session = session
            AddHandler _timer.Tick, Sub() SyncNow()
        End Sub

        ''' <summary>Begins syncing in the background. Call on the UI thread once signed in.</summary>
        Public Sub Start()
            _timer.Start()
            SyncNow()
        End Sub

        Public Sub [Stop]()
            _timer.Stop()
        End Sub

        ''' <summary>Starts a sync unless one is already running. Returns at once.</summary>
        Public Async Sub SyncNow()
            If _running OrElse Not _session.IsSignedIn Then Return
            _running = True
            _State = SyncState.Syncing
            OnStatusChanged()

            Dim organizationId = _session.TenantId
            Dim report As SyncReport = Nothing
            Dim pending = _Pending
            Dim failure As String = Nothing
            Try
                report = Await Task.Run(Function() Engine.Run(organizationId))
                pending = Await Task.Run(Function() Engine.PendingCount(organizationId))
            Catch ex As Exception
                ' Not a network failure (those come back in the report): something about the data
                ' itself. Kept, shown on the status, and tried again next time.
                failure = ex.Message
            Finally
                _running = False
            End Try

            Finish(report, pending, failure)
        End Sub

        ''' <summary>
        ''' Syncs on the calling thread. For sign-in, which needs this computer's copy of the
        ''' organization to be current before the first screen opens.
        ''' </summary>
        Public Function RunBlocking(organizationId As Integer) As SyncReport
            Dim report = Engine.Run(organizationId)
            _Pending = Engine.PendingCount(organizationId)
            _LastReport = report
            If report.Reachable Then _LastContact = DateTime.Now
            Return report
        End Function

        Private Sub Finish(report As SyncReport, pending As Integer, failure As String)
            _Pending = pending
            If report IsNot Nothing Then _LastReport = report

            If failure IsNot Nothing Then
                _LastReport = New SyncReport With {.Reachable = True, .Failed = 1}
                _LastReport.Errors.Add(failure)
                _State = SyncState.NeedsAttention
            ElseIf report Is Nothing OrElse report.Skipped Then
                _State = If(_State = SyncState.Syncing, SyncState.Idle, _State)
            ElseIf Not report.Reachable Then
                _State = SyncState.Offline
            Else
                _LastContact = DateTime.Now
                _State = If(report.Failed > 0, SyncState.NeedsAttention, SyncState.UpToDate)
            End If

            ' The sync wrote to the file through its own context; anything the screens' context
            ' still holds from before may now be out of date.
            If report IsNot Nothing AndAlso (report.Sent > 0 OrElse report.Received > 0) Then
                AppHost.Current.ResetTracking()
            End If

            OnStatusChanged()
            If report IsNot Nothing AndAlso report.NegativeStock.Count > 0 Then
                RaiseEvent NegativeStockFound(Me, report.NegativeStock)
            End If
        End Sub

        Private Sub OnStatusChanged()
            RaiseEvent StatusChanged(Me, EventArgs.Empty)
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            _timer.Dispose()
        End Sub
    End Class

End Namespace

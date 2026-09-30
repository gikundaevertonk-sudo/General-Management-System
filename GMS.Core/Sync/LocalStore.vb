Imports System.IO
Imports Microsoft.Data.Sqlite
Imports Microsoft.EntityFrameworkCore
Imports GMS.Core.Data

Namespace Sync

    ''' <summary>
    ''' The SQLite file the desktop client works against, so it keeps running when PostgreSQL
    ''' cannot be reached.
    ''' </summary>
    ''' <remarks>
    ''' The schema is generated from the EF model rather than hand-written, so it cannot drift from
    ''' the code the way the PostgreSQL scripts once did. The price is that EnsureCreated never
    ''' alters an existing file; <see cref="Open"/> therefore stamps the model's shape into the file
    ''' and rebuilds it when a new version of the application changes that shape - but only when
    ''' nothing is waiting to be sent, because a rebuild throws away whatever has not been synced.
    ''' </remarks>
    Public NotInheritable Class LocalStore

        Private Sub New()
        End Sub

        Public Shared Function ConnectionStringFor(databasePath As String) As String
            ' Two contexts share the file: the screens and the background sync. DefaultTimeout is
            ' how long a writer that finds the file busy waits, instead of failing at once.
            Return New SqliteConnectionStringBuilder With {
                .DataSource = databasePath,
                .Mode = SqliteOpenMode.ReadWriteCreate,
                .DefaultTimeout = 30
            }.ToString()
        End Function

        ''' <summary>Where the desktop client keeps its copy, per Windows user and per server.</summary>
        ''' <remarks>
        ''' One file per server because a copy belongs to the database it was made from: its ids
        ''' are that server's ids. Pointing the application at a different database must start a
        ''' fresh copy, never push the old one's rows into the new database.
        ''' </remarks>
        Public Shared Function DefaultPath(serverConnectionString As String) As String
            Dim folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GMS")
            Directory.CreateDirectory(folder)
            Return Path.Combine(folder, $"gms-local-{ServerKey(serverConnectionString)}.db")
        End Function

        ''' <summary>Host, port, database and user - what identifies the server, not the password.</summary>
        Private Shared Function ServerKey(serverConnectionString As String) As String
            Dim b As New Npgsql.NpgsqlConnectionStringBuilder(serverConnectionString)
            Dim identity = $"{b.Host}|{b.Port}|{b.Database}|{b.Username}".ToLowerInvariant()
            Using sha = System.Security.Cryptography.SHA256.Create()
                Return Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(identity))).
                    Substring(0, 12).ToLowerInvariant()
            End Using
        End Function

        ''' <summary>
        ''' Creates the file if it does not exist, or rebuilds it if this build's model differs
        ''' from the one that created it. Returns a message for the user when an out-of-date file
        ''' had to be set aside with unsent changes still in it, otherwise Nothing.
        ''' </summary>
        Public Shared Function Open(context As GmsDbContext, databasePath As String) As String
            Dim fingerprint = ModelFingerprint(context)
            Dim notice As String = Nothing

            If File.Exists(databasePath) Then
                Dim stored = ReadFingerprint(context)
                If stored IsNot Nothing AndAlso stored <> fingerprint Then
                    Dim pending = CountPending(context)
                    context.Database.CloseConnection()
                    SqliteConnection.ClearAllPools()
                    If pending > 0 Then
                        ' Never silently discard a sale. The old file is kept intact beside the
                        ' new one so the changes in it can still be recovered by hand.
                        Dim aside = databasePath & "." & DateTime.Now.ToString("yyyyMMddHHmmss") & ".bak"
                        File.Move(databasePath, aside)
                        notice = $"This update changed how data is stored on this computer. {pending} change(s) " &
                                 $"made offline had not been sent yet; they are kept in {aside} and are not " &
                                 "in the new copy. Contact support before deleting that file."
                    Else
                        File.Delete(databasePath)
                    End If
                    DeleteSidecars(databasePath)
                    LocalIdGenerator.Reset()
                End If
            End If

            context.Database.EnsureCreated()
            ' Write-ahead logging lets the screens read while the sync writes.
            context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;")
            WriteFingerprint(context, fingerprint)
            Return notice
        End Function

        ''' <summary>
        ''' This installation's short code for document numbers (see IDeviceIdentity), made up
        ''' the first time and kept in the local file from then on.
        ''' </summary>
        ''' <remarks>
        ''' Four characters from an alphabet without look-alikes (no 0/O, 1/I/L), so it can be read
        ''' off a receipt over the phone. That is about 850,000 codes; two tills of one business
        ''' drawing the same one is unlikely, and would show as two receipts sharing a number
        ''' rather than as lost data.
        ''' </remarks>
        Public Shared Function DeviceCode(context As GmsDbContext) As String
            Dim existing = LocalSyncState.Read(context, DeviceCodeKey)
            If Not String.IsNullOrEmpty(existing) Then Return existing

            Const alphabet As String = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"
            Dim chars = Enumerable.Range(0, 4).
                Select(Function(i) alphabet(System.Security.Cryptography.RandomNumberGenerator.GetInt32(alphabet.Length))).
                ToArray()
            Dim code As New String(chars)
            LocalSyncState.Write(context, DeviceCodeKey, code)
            Return code
        End Function

        Private Const DeviceCodeKey As String = "device.code"

        Private Shared Function ModelFingerprint(context As GmsDbContext) As String
            Dim script = context.Database.GenerateCreateScript()
            Using sha = System.Security.Cryptography.SHA256.Create()
                Return Convert.ToHexString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(script)))
            End Using
        End Function

        Private Shared Function ReadFingerprint(context As GmsDbContext) As String
            Try
                Return context.Set(Of LocalSyncState)().
                    Where(Function(s) s.Key = FingerprintKey).
                    Select(Function(s) s.Value).FirstOrDefault()
            Catch ex As SqliteException
                ' A file from before fingerprints existed, or one that is not ours at all.
                Return String.Empty
            End Try
        End Function

        Private Shared Function CountPending(context As GmsDbContext) As Integer
            Try
                Return context.Set(Of OutboxEntry)().Count()
            Catch ex As SqliteException
                Return 0
            End Try
        End Function

        Private Shared Sub WriteFingerprint(context As GmsDbContext, fingerprint As String)
            LocalSyncState.Write(context, FingerprintKey, fingerprint)
        End Sub

        Private Shared Sub DeleteSidecars(databasePath As String)
            For Each suffix In {"-wal", "-shm"}
                If File.Exists(databasePath & suffix) Then File.Delete(databasePath & suffix)
            Next
        End Sub

        Private Const FingerprintKey As String = "schema.fingerprint"
    End Class

End Namespace

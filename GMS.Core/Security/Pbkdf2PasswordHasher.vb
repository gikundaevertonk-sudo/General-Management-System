Imports System.Security.Cryptography
Imports GMS.Core.Abstractions

Namespace Security

    ''' <summary>
    ''' PBKDF2 (HMAC-SHA256) password hasher. Stored format is four dot-separated
    ''' fields: <c>algorithm.iterations.saltBase64.hashBase64</c>. The parameters
    ''' are read back from the stored value on verify, so they can be raised over
    ''' time without invalidating existing hashes.
    ''' </summary>
    Public NotInheritable Class Pbkdf2PasswordHasher
        Implements IPasswordHasher

        Private Const Prefix As String = "PBKDF2-SHA256"
        Private Const SaltBytes As Integer = 16
        Private Const KeyBytes As Integer = 32
        Private ReadOnly _iterations As Integer

        Public Sub New()
            Me.New(210_000)
        End Sub

        Public Sub New(iterations As Integer)
            If iterations < 10_000 Then
                Throw New ArgumentOutOfRangeException(NameOf(iterations), "Iteration count is too low.")
            End If
            _iterations = iterations
        End Sub

        Public Function Hash(password As String) As String Implements IPasswordHasher.Hash
            If String.IsNullOrEmpty(password) Then
                Throw New ArgumentException("Password cannot be empty.", NameOf(password))
            End If

            Dim salt = RandomNumberGenerator.GetBytes(SaltBytes)
            Dim key = Rfc2898DeriveBytes.Pbkdf2(password, salt, _iterations, HashAlgorithmName.SHA256, KeyBytes)
            Return String.Join("."c, Prefix, _iterations.ToString(),
                               Convert.ToBase64String(salt), Convert.ToBase64String(key))
        End Function

        Public Function Verify(password As String, hash As String) As Boolean Implements IPasswordHasher.Verify
            If String.IsNullOrEmpty(password) OrElse String.IsNullOrWhiteSpace(hash) Then Return False

            Dim parts = hash.Split("."c)
            If parts.Length <> 4 OrElse Not String.Equals(parts(0), Prefix, StringComparison.Ordinal) Then
                Return False
            End If

            Dim iterations As Integer
            If Not Integer.TryParse(parts(1), iterations) OrElse iterations <= 0 Then Return False

            Try
                Dim salt = Convert.FromBase64String(parts(2))
                Dim expected = Convert.FromBase64String(parts(3))
                Dim actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations,
                                                       HashAlgorithmName.SHA256, expected.Length)
                Return CryptographicOperations.FixedTimeEquals(actual, expected)
            Catch ex As FormatException
                Return False
            End Try
        End Function
    End Class

    ''' <summary>Default <see cref="IClock"/> bound to the machine clock (UTC).</summary>
    Public NotInheritable Class SystemClock
        Implements IClock

        Public ReadOnly Property UtcNow As DateTime Implements IClock.UtcNow
            Get
                Return DateTime.UtcNow
            End Get
        End Property
    End Class

End Namespace

Imports System.Collections.Concurrent
Imports GMS.Core.Abstractions
Imports GMS.Core.Data

Namespace Repositories.Ef

    ''' <summary>EF Core-backed <see cref="IUnitOfWork"/>. Register scoped (one per request).</summary>
    Public NotInheritable Class EfUnitOfWork
        Implements IUnitOfWork

        Private ReadOnly _context As GmsDbContext
        Private ReadOnly _repositories As New ConcurrentDictionary(Of Type, Object)()

        Public Sub New(context As GmsDbContext)
            _context = context
        End Sub

        Public Function Repository(Of T As Class)() As IRepository(Of T) Implements IUnitOfWork.Repository
            Return DirectCast(
                _repositories.GetOrAdd(GetType(T), Function(keyType) New EfRepository(Of T)(_context)),
                IRepository(Of T))
        End Function

        Public Function SaveChanges() As Integer Implements IUnitOfWork.SaveChanges
            Return _context.SaveChanges()
        End Function
    End Class

End Namespace

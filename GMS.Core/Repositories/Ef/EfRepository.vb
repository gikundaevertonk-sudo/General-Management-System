Imports Microsoft.EntityFrameworkCore
Imports GMS.Core.Abstractions
Imports GMS.Core.Data

Namespace Repositories.Ef

    ''' <summary>EF Core-backed <see cref="IRepository(Of T)"/>. One instance per <see cref="GmsDbContext"/>.</summary>
    Friend NotInheritable Class EfRepository(Of T As Class)
        Implements IRepository(Of T)

        Private ReadOnly _context As GmsDbContext
        Private ReadOnly _set As DbSet(Of T)

        Public Sub New(context As GmsDbContext)
            _context = context
            _set = context.Set(Of T)()
        End Sub

        Public Function GetById(id As Integer) As T Implements IRepository(Of T).GetById
            Return _set.Find(id)
        End Function

        Public Function Query() As IQueryable(Of T) Implements IRepository(Of T).Query
            Return _set
        End Function

        Public Function List() As IReadOnlyList(Of T) Implements IRepository(Of T).List
            Return _set.ToList()
        End Function

        Public Sub Add(entity As T) Implements IRepository(Of T).Add
            _set.Add(entity)
        End Sub

        Public Sub AddRange(entities As IEnumerable(Of T)) Implements IRepository(Of T).AddRange
            _set.AddRange(entities)
        End Sub

        Public Sub Update(entity As T) Implements IRepository(Of T).Update
            Dim entry = _context.Entry(entity)
            If entry.State = EntityState.Detached Then _set.Attach(entity)
            entry.State = EntityState.Modified
        End Sub

        Public Sub Remove(entity As T) Implements IRepository(Of T).Remove
            If _context.Entry(entity).State = EntityState.Detached Then _set.Attach(entity)
            _set.Remove(entity)
        End Sub
    End Class

End Namespace

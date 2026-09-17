Imports System.Linq

Namespace Abstractions

    ''' <summary>
    ''' Storage-agnostic access to one entity type. The current implementation is
    ''' in-memory; an Entity Framework Core implementation replaces it in the final
    ''' step without any change to the services that depend on this interface.
    ''' </summary>
    Public Interface IRepository(Of T As Class)

        ''' <summary>Single entity by primary key, or <c>Nothing</c> if not found.</summary>
        Function GetById(id As Integer) As T

        ''' <summary>Composable query for filtering, sorting, paging and projection.</summary>
        Function Query() As IQueryable(Of T)

        ''' <summary>Every row of this entity type (small reference tables only).</summary>
        Function List() As IReadOnlyList(Of T)

        Sub Add(entity As T)
        Sub AddRange(entities As IEnumerable(Of T))
        Sub Update(entity As T)
        Sub Remove(entity As T)
    End Interface

    ''' <summary>
    ''' One business transaction across many repositories. Services mutate entities
    ''' through repositories, then call <see cref="SaveChanges"/> exactly once.
    ''' </summary>
    Public Interface IUnitOfWork

        Function Repository(Of T As Class)() As IRepository(Of T)

        ''' <summary>Persists all pending changes; returns the number of rows affected.</summary>
        Function SaveChanges() As Integer
    End Interface

End Namespace

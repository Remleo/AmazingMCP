using TestProject.Core.Persistence;

namespace TestProject.App.Persistence;

public class GenericRepository<T> : IRepository<T> where T : class
{
    readonly List<T> _store = [];

    public T? GetById(int id) => _store.FirstOrDefault();
    public void Save(T entity) => _store.Add(entity);
    public int Count => _store.Count;
}

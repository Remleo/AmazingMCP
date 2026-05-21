namespace TestProject.Core.Persistence;

public abstract class RepositoryBase<T> where T : class
{
    public abstract T? GetById(int id);
    public abstract void Save(T entity);
}

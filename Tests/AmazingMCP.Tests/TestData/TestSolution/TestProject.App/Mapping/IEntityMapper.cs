namespace TestProject.App.Mapping;

/// <summary>
/// Generic entity mapper abstraction.
/// </summary>
public interface IEntityMapper<TSource, TDestination>
{
    TDestination Map(TSource source);
    TSource MapBack(TDestination destination);
}

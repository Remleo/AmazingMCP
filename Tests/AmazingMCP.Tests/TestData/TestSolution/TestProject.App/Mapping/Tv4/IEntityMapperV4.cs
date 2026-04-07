namespace TestProject.App.Mapping.Tv4;

public interface IEntityMapperV4<TSource, TDestination>
{
    TDestination Map(TSource source);
    TSource MapBack(TDestination destination);
    bool CanMap(TSource source);
    TDestination MapPartial(TSource source, IReadOnlyList<string> fields);
    Task<TDestination> MapAsync(TSource source, CancellationToken ct = default);
}

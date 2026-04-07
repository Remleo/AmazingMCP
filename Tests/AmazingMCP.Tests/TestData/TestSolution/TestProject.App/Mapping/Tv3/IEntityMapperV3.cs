namespace TestProject.App.Mapping.Tv3;

public interface IEntityMapperV3<TSource, TDestination>
{
    TDestination Map(TSource source);
    TSource MapBack(TDestination destination);
    bool CanMap(TSource source);
    TDestination MapPartial(TSource source, IReadOnlyList<string> fields);
}

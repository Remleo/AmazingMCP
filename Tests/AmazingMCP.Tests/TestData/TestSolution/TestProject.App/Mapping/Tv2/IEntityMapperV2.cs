namespace TestProject.App.Mapping.Tv2;

public interface IEntityMapperV2<TSource, TDestination>
{
    TDestination Map(TSource source);
    TSource MapBack(TDestination destination);
    bool CanMap(TSource source);
}

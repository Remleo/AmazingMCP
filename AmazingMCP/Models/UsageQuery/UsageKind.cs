namespace AmazingMCP.Models.UsageQuery;

public enum UsageKind
{
    MethodCall,
    ConstructorCall,
    PropertyRead,
    PropertyWrite,
    FieldRead,
    FieldWrite,
    TypeAsGenericArgument,
    TypeAsGenericConstraint,
    TypeAsReturnType,
    TypeAsParameter,
    TypeAsInheritance,
    NameOf,
    TypeOf,
}

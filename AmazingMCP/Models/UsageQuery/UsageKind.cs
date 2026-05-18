namespace AmazingMCP.Models.UsageQuery;

public enum UsageKind
{
    MethodCall,
    ConstructorCall,
    PropertyRead,
    PropertyWrite,
    FieldRead,
    FieldWrite,
    GenericArgument,
    GenericConstraint,
    ReturnType,
    Parameter,
    Inheritance,
    NameOf,
    TypeOf,
    IsOrAs,
}

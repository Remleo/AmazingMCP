namespace AmazingMCP.Models;

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
}

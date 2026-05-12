using AmazingMCP.Models;
using AmazingMCP.Models.Design;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.Scanning;

public class MemberAccessAnalyzer : IMemberAccessAnalyzer
{
    public (RawTypeInfo TypeInfo, string MemberName, MemberUsageKind Kind)?
        AnalyzeAccess(MemberAccessExpressionSyntax memberAccess, SemanticModel model)
    {
        // Skip if this is the target of an invocation — handled by InvocationAnalyzer
        if (memberAccess.Parent is InvocationExpressionSyntax) return null;
        // Skip if this is the left side of an assignment — handled by AnalyzeAssignment
        if (memberAccess.Parent is AssignmentExpressionSyntax assignment &&
            assignment.Left == memberAccess) return null;

        if (model.GetSymbolInfo(memberAccess).Symbol is not IPropertySymbol prop) return null;
        if (prop.ContainingType is not { } containingType) return null;

        return (RawTypeInfo.From(containingType), prop.Name, MemberUsageKind.PropertyGet);
    }

    public (RawTypeInfo TypeInfo, string MemberName)?
        AnalyzeAssignment(AssignmentExpressionSyntax assignment, SemanticModel model)
    {
        if (assignment.Left is not MemberAccessExpressionSyntax memberAccess) return null;
        if (model.GetSymbolInfo(memberAccess).Symbol is not IPropertySymbol prop) return null;
        if (prop.ContainingType is not { } containingType) return null;

        return (RawTypeInfo.From(containingType), prop.Name);
    }
}

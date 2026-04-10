using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.Scanning;

/// <summary>
/// Analyzes MemberAccessExpression and AssignmentExpression nodes to detect property get/set usages.
/// </summary>
public interface IMemberAccessAnalyzer
{
    /// <summary>
    /// Returns (containingType, memberName, kind) for a property access, or null if not applicable.
    /// </summary>
    (INamedTypeSymbol ContainingType, string MemberName, MemberUsageKind Kind)?
        AnalyzeAccess(MemberAccessExpressionSyntax memberAccess, SemanticModel model);

    /// <summary>
    /// Returns (containingType, memberName) for a property set via assignment, or null if not applicable.
    /// </summary>
    (INamedTypeSymbol ContainingType, string MemberName)?
        AnalyzeAssignment(AssignmentExpressionSyntax assignment, SemanticModel model);
}

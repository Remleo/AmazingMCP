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
    /// Returns (typeInfo, memberName, kind) for a property access, or null if not applicable.
    /// </summary>
    (RawTypeInfo TypeInfo, string MemberName, MemberUsageKind Kind)?
        AnalyzeAccess(MemberAccessExpressionSyntax memberAccess, SemanticModel model);

    /// <summary>
    /// Returns (typeInfo, memberName) for a property set via assignment, or null if not applicable.
    /// </summary>
    (RawTypeInfo TypeInfo, string MemberName)?
        AnalyzeAssignment(AssignmentExpressionSyntax assignment, SemanticModel model);
}

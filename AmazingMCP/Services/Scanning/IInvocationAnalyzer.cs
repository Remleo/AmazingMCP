using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.Scanning;

/// <summary>
/// Analyzes InvocationExpression nodes to extract the containing type and member name.
/// Handles regular method calls, extension methods, and static calls.
/// </summary>
public interface IInvocationAnalyzer
{
    /// <summary>
    /// Returns (containingType, memberName, isStatic) for a given invocation, or null if not applicable.
    /// For extension methods, returns the receiver type (not the static class declaring the extension).
    /// </summary>
    (INamedTypeSymbol ContainingType, string MemberName, bool IsStatic)?
        Analyze(InvocationExpressionSyntax invocation, SemanticModel model);
}

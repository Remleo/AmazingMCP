using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AmazingMCP.Services.CodeLens;

internal static class CollectorHelpers
{
    /// <summary>
    /// Returns the 1-based line number for a given span start position.
    /// </summary>
    internal static int GetSourceLine(SemanticModel model, int spanStart)
        => model.SyntaxTree
               .GetLineSpan(new TextSpan(spanStart, 0))
               .StartLinePosition.Line + 1;

    /// <summary>
    /// Returns the short (unqualified) name of a type — just the class name without namespace.
    /// </summary>
    internal static string GetShortName(INamedTypeSymbol type)
        => type.Name;
}

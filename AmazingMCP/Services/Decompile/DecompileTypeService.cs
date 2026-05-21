using AmazingMCP.Configuration;
using AmazingMCP.Models.FileAnalysis;
using AmazingMCP.Services.FileAnalysis;
using AmazingMCP.Services.SymbolQuery;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace AmazingMCP.Services.Decompile;

public class DecompileTypeService(
    RoslynSymbolService roslynSymbolService,
    IFilteredSourceService filteredSource,
    ISourceDigestService sourceDigest,
    IOptions<ReadCsOptions> options) : IDecompileTypeService
{
    public async Task<string> DecompileTypeAsync(
        string solutionPath,
        string fullTypeName,
        string[]? memberFilters = null,
        CancellationToken ct = default)
    {
        // Resolve the type symbol from the solution compilations
        var solution = await roslynSymbolService.GetSolutionAsync(solutionPath, ct);
        var (symbol, error) = await roslynSymbolService.FindExactTypeAsync(solution, fullTypeName, ct);

        if (symbol is null)
            return error!;

        // Source types should be read directly from files
        if (!symbol.DeclaringSyntaxReferences.IsEmpty)
            return BuildSourceTypeError(symbol);

        // Locate the assembly DLL containing this type
        var dllPath = FindDllPath(symbol, solution);
        if (dllPath is null)
            return $"Could not locate assembly for type '{fullTypeName}'";

        // Decompile the full type source
        var fullSource = DecompileSource(symbol, dllPath);

        // No filters — return full source or digest if too large
        if (memberFilters is not { Length: > 0 })
            return FormatFullOutput(fullSource);

        // Apply member filters (constructors and usings are always included)
        return FormatFilteredOutput(fullSource, memberFilters);
    }

    string FormatFullOutput(string fullSource)
    {
        if (fullSource.Length <= options.Value.ReadOutputMaxLength)
            return fullSource;

        var digest = sourceDigest.GetDigest(fullSource, includeLineNumbers: false);

        return $"// Decompiled source is too large ({fullSource.Length} chars). Use memberFilters to narrow the output.\n\n" +
               $"// --- Digest ---\n{digest}";
    }

    string FormatFilteredOutput(string fullSource, string[] memberFilters)
    {
        // .ctor and usings are always included alongside user-specified filters
        var filters = BuildFilters(memberFilters);
        var filtered = filteredSource.GetFilteredSource(fullSource, filters);

        if (filtered.Length <= options.Value.ReadOutputMaxLength)
            return filtered;

        // Truncate and append digest for navigation
        var digest = sourceDigest.GetDigest(fullSource, includeLineNumbers: false);

        return filtered[..options.Value.ReadOutputMaxLength] +
               $"\n\n// Output truncated. Use narrower memberFilters.\n\n// --- Digest ---\n{digest}";
    }

    static string[] BuildFilters(string[] memberFilters) =>
        [..memberFilters, FileStructureItem.ConstructorAlias, FileStructureItem.UsingsAlias];

    static string DecompileSource(INamedTypeSymbol symbol, string dllPath)
    {
        var decompiler = new CSharpDecompiler(dllPath, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });
        var ilspyName = new FullTypeName(symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", ""));
        return decompiler.DecompileTypeAsString(ilspyName);
    }

    static string BuildSourceTypeError(INamedTypeSymbol symbol)
    {
        var paths = SourceLocationFormatter.GetSourcePaths(symbol);
        var pathList = string.Join("\n", paths.Select(p => $"  {p}"));
        return $"Type '{symbol.ToDisplayString()}' is defined in source — read it directly:\n{pathList}";
    }

    static string? FindDllPath(INamedTypeSymbol symbol, Models.Workspace.ICachedSolution solution)
    {
        var assemblyName = symbol.ContainingAssembly?.Name;
        if (assemblyName is null)
            return null;

        foreach (var (_, compilation) in solution.Compilations)
        {
            foreach (var reference in compilation.References.OfType<PortableExecutableReference>())
            {
                if (reference.Display is not null
                    && Path.GetFileNameWithoutExtension(reference.Display)
                        .Equals(assemblyName, StringComparison.OrdinalIgnoreCase))
                    return reference.Display;
            }
        }

        return null;
    }
}

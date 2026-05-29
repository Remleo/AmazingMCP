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
        string? version = null,
        CancellationToken ct = default)
    {
        var solution = await roslynSymbolService.GetSolutionAsync(solutionPath, ct);
        var (group, error) = roslynSymbolService.FindExactType(solution, fullTypeName);

        if (group is null)
            return error!;

        var symbol = SelectVersion(group, version);

        // Source types should be read directly from files
        if (!symbol.DeclaringSyntaxReferences.IsEmpty)
            return BuildSourceTypeError(symbol);

        // Locate the assembly DLL containing this type
        var dllPath = FindDllPath(symbol, solution);
        if (dllPath is null)
            return $"Could not locate assembly for type '{fullTypeName}'";

        var banner = BuildVersionBanner(group, symbol);

        // Decompile the full type source
        var fullSource = DecompileSource(symbol, dllPath);

        // No filters — return full source or digest if too large
        if (memberFilters is not { Length: > 0 })
            return banner + FormatFullOutput(fullSource);

        // Apply member filters (constructors and usings are always included)
        return banner + FormatFilteredOutput(fullSource, memberFilters);
    }

    static INamedTypeSymbol SelectVersion(Models.TypeVersionGroup group, string? requestedVersion)
    {
        if (requestedVersion is not null && Version.TryParse(requestedVersion, out var parsed))
        {
            var match = group.Versions.FirstOrDefault(v => v.Version == parsed);
            if (match.Symbol is not null)
                return match.Symbol;
        }

        return group.Best;
    }

    static string BuildVersionBanner(Models.TypeVersionGroup group, INamedTypeSymbol displayed)
    {
        if (group.Versions.Count <= 1 && group.Versions.All(v => v.Version is null))
            return string.Empty;

        var versions = group.Versions
            .Select(v => v.Version)
            .OrderByDescending(v => v)
            .Select(v => v?.ToString() ?? "source")
            .ToList();

        var displayedVersion = group.Versions
            .FirstOrDefault(v => SymbolEqualityComparer.Default.Equals(v.Symbol, displayed))
            .Version?.ToString() ?? "source";

        if (group.Versions.Count > 1)
            return $"// ⚠ WARNING: This type exists in multiple versions: {string.Join(", ", versions)}\n" +
                   $"// Showing version: {displayedVersion}. To see another version, pass version=\"<version>\" parameter.\n\n";

        return $"// NuGet version: {displayedVersion}\n\n";
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
        return decompiler.DecompileTypeAsString(IlspyFullTypeNameBuilder.Build(symbol));
    }

    static string BuildSourceTypeError(INamedTypeSymbol symbol)
    {
        var paths = SourceLocationFormatter.GetSourcePaths(symbol);
        var pathList = string.Join("\n", paths.Select(p => $"  {p}"));
        return $"Type '{symbol.ToDisplayString()}' is defined in source — read it directly:\n{pathList}";
    }

    static string? FindDllPath(INamedTypeSymbol symbol, Models.Workspace.ICachedSolution solution)
    {
        var asm = symbol.ContainingAssembly;
        if (asm is null)
            return null;

        foreach (var (_, compilation) in solution.Compilations)
        {
            if (compilation.GetMetadataReference(asm) is PortableExecutableReference peRef
                && peRef.FilePath is not null)
                return peRef.FilePath;
        }

        return null;
    }
}

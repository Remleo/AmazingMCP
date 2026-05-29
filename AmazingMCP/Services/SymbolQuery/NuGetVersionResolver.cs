using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;

namespace AmazingMCP.Services.SymbolQuery;

/// <summary>
/// Extracts the NuGet package version from a metadata reference dll path.
/// NuGet stores dlls at: .nuget/packages/{name}/{version}/lib/...
/// Results are cached per dll path with a sliding expiration.
/// </summary>
/// <remarks>
/// Caching by file path (not assembly identity) is intentional: many NuGet packages
/// keep AssemblyVersion stable across patch releases (e.g. Microsoft.Extensions.* 10.0.X
/// all have AssemblyVersion=10.0.0.0), so identity is not unique per NuGet version.
/// The path under <c>.nuget/packages/{name}/{version}/</c> is the only reliable discriminator.
/// </remarks>
public class NuGetVersionResolver(IMemoryCache cache)
{
    record AssemblyCacheKey(string FilePath);

    static readonly Regex VersionPattern = new(
        @"[/\\]\.nuget[/\\]packages[/\\][^/\\]+[/\\]([^/\\]+)[/\\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    /// <summary>
    /// Resolves the NuGet package version for a type's containing assembly within the given compilation.
    /// Source types and types without a resolvable metadata reference return null.
    /// Result is cached by dll file path.
    /// </summary>
    public Version? GetVersion(Compilation compilation, INamedTypeSymbol type)
    {
        if (type.DeclaringSyntaxReferences.Length > 0)
            return null;

        var asm = type.ContainingAssembly;
        if (asm is null)
            return null;

        if (compilation.GetMetadataReference(asm) is not PortableExecutableReference peRef
            || peRef.FilePath is null)
            return null;

        var key = new AssemblyCacheKey(peRef.FilePath);

        return cache.GetOrCreate(key, entry =>
        {
            entry.SlidingExpiration = CacheTtl;
            return ParseVersion(peRef.FilePath);
        });
    }

    static Version? ParseVersion(string path)
    {
        var match = VersionPattern.Match(path);
        if (!match.Success)
            return null;

        return Version.TryParse(match.Groups[1].Value, out var version) ? version : null;
    }
}

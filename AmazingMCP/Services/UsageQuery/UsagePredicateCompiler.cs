using System.Reflection;
using AmazingMCP.Models;
using AmazingMCP.Models.UsageQuery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AmazingMCP.Services.UsageQuery;

/// <summary>
/// Compiles a C# predicate expression string into a <see cref="Func{QueryEntry, bool}"/>.
/// Safety validation is delegated to <see cref="PredicateSafetyValidator"/>.
/// </summary>
public static class UsagePredicateCompiler
{
    /// <summary>
    /// Validates and compiles the predicate expression into a delegate.
    /// Throws <see cref="InvalidOperationException"/> if the expression is unsafe or fails to compile.
    /// </summary>
    public static Task<Func<QueryEntry, bool>> CompileAsync(string expression)
    {
        PredicateSafetyValidator.Validate(expression);
        var del = BuildDelegate(expression);
        return Task.FromResult(del);
    }

    // ── Compilation ───────────────────────────────────────────────────────────

    static Func<QueryEntry, bool> BuildDelegate(string expression)
    {
        var fullSource = $$"""
                           using System;
                           using System.Linq;
                           using System.Collections.Generic;
                           using AmazingMCP.Models;
                           using AmazingMCP.Models.UsageQuery;

                           public static class __Predicate
                           {
                               public static bool Evaluate(QueryEntry x) => {{expression}};
                           }
                           """;

        var syntaxTree = CSharpSyntaxTree.ParseText(fullSource);
        var compilation = CSharpCompilation.Create(
            "__PredicateAssembly_" + Guid.NewGuid().ToString("N"),
            [ syntaxTree ],
            BuildReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage());
            throw new InvalidOperationException(
                $"Predicate compilation failed: {string.Join("; ", errors)}");
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());
        var type = assembly.GetType("__Predicate")!;
        var method = type.GetMethod("Evaluate", BindingFlags.Public | BindingFlags.Static)!;

        return entry => (bool) method.Invoke(null, [ entry ])!;
    }

    static MetadataReference[] BuildReferences() =>
        ((AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? "")
        .Split(';', StringSplitOptions.RemoveEmptyEntries)
        .Where(File.Exists)
        .Select(p => (MetadataReference) MetadataReference.CreateFromFile(p))
        .ToArray();
}
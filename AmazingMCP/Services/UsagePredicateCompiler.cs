using System.Reflection;
using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AmazingMCP.Services;

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

            public static class __Predicate
            {
                public static bool Evaluate(QueryEntry x) => {{expression}};
            }
            """;

        var syntaxTree = CSharpSyntaxTree.ParseText(fullSource);
        var compilation = CSharpCompilation.Create(
            "__PredicateAssembly_" + Guid.NewGuid().ToString("N"),
            [syntaxTree],
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

        return entry => (bool)method.Invoke(null, [entry])!;
    }

    static MetadataReference[] BuildReferences()
    {
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(QueryEntry).Assembly.Location),
        };

        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        foreach (var name in new[] { "System.Runtime.dll", "System.Collections.dll", "netstandard.dll" })
        {
            var path = Path.Combine(runtimeDir, name);
            if (File.Exists(path))
                refs.Add(MetadataReference.CreateFromFile(path));
        }

        return refs.ToArray();
    }
}

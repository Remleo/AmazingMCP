using AmazingMCP.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AmazingMCP.Services.Scanning;

/// <summary>
/// Scans the direct body of a class (no base class traversal) and returns RawUsages.
/// Detects: method calls, extension method calls, static calls, property get/set (interfaces only).
/// Self-calls (calls on 'this' or the class itself) are excluded.
/// </summary>
public class MemberUsageAnalyzer(
    IInvocationAnalyzer invocationAnalyzer,
    IMemberAccessAnalyzer memberAccessAnalyzer,
    ITypeFilter typeFilter) : IMemberUsageAnalyzer
{
    public async Task<IReadOnlyList<RawUsage>> AnalyzeAsync(
        INamedTypeSymbol cls,
        Compilation compilation,
        CancellationToken ct)
    {
        // Build self-type set: the class itself and all its base classes (to filter self-calls)
        var selfTypes = new HashSet<string>();
        selfTypes.Add(cls.ToDisplayString());
        var current = cls.BaseType;
        while (current is not null && current.SpecialType == SpecialType.None)
        {
            selfTypes.Add(current.ToDisplayString());
            current = current.BaseType;
        }

        // usageMap: fullName → (typeInfo, isStatic, usages)
        var usageMap = new Dictionary<string, (RawTypeInfo TypeInfo, bool IsStatic, HashSet<MemberUsage> Usages)>();

        foreach (var syntaxRef in cls.DeclaringSyntaxReferences)
        {
            if (!compilation.ContainsSyntaxTree(syntaxRef.SyntaxTree)) continue;

            var syntax = await syntaxRef.GetSyntaxAsync(ct);
            var model = compilation.GetSemanticModel(syntaxRef.SyntaxTree);

            foreach (var node in syntax.DescendantNodes())
            {
                switch (node)
                {
                    case InvocationExpressionSyntax invocation:
                        ProcessInvocation(invocation, model, usageMap, selfTypes);
                        break;
                    case MemberAccessExpressionSyntax memberAccess:
                        ProcessMemberAccess(memberAccess, model, usageMap, selfTypes);
                        break;
                    case AssignmentExpressionSyntax assignment:
                        ProcessAssignment(assignment, model, usageMap, selfTypes);
                        break;
                }
            }
        }

        return usageMap
            .Select(kv => new RawUsage(
                Usage: new AbstractionUsage(kv.Key, kv.Value.IsStatic, kv.Value.Usages.ToList()),
                TypeInfo: kv.Value.TypeInfo))
            .ToList();
    }

    void ProcessInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        Dictionary<string, (RawTypeInfo TypeInfo, bool IsStatic, HashSet<MemberUsage> Usages)> usageMap,
        HashSet<string> selfTypes)
    {
        var result = invocationAnalyzer.Analyze(invocation, model);
        if (result is null) return;

        var (typeInfo, memberName, isStatic) = result.Value;
        if (typeInfo.IsSpecialType) return;
        if (typeFilter.ShouldExcludeByName(typeInfo.FullName)) return;
        if (selfTypes.Contains(typeInfo.FullName)) return;

        EnsureEntry(usageMap, typeInfo, isStatic);
        usageMap[typeInfo.FullName].Usages.Add(new MemberUsage(memberName, MemberUsageKind.MethodCall));
    }

    void ProcessMemberAccess(
        MemberAccessExpressionSyntax memberAccess,
        SemanticModel model,
        Dictionary<string, (RawTypeInfo TypeInfo, bool IsStatic, HashSet<MemberUsage> Usages)> usageMap,
        HashSet<string> selfTypes)
    {
        var result = memberAccessAnalyzer.AnalyzeAccess(memberAccess, model);
        if (result is null) return;

        var (typeInfo, memberName, kind) = result.Value;
        if (typeInfo.IsSpecialType) return;
        if (typeFilter.ShouldExcludeByName(typeInfo.FullName)) return;

        // Property access only tracked for interfaces — avoids noise from POCO/DTO field reads
        if (!typeInfo.IsInterface) return;

        if (selfTypes.Contains(typeInfo.FullName)) return;

        EnsureEntry(usageMap, typeInfo, isStatic: false);
        usageMap[typeInfo.FullName].Usages.Add(new MemberUsage(memberName, kind));
    }

    void ProcessAssignment(
        AssignmentExpressionSyntax assignment,
        SemanticModel model,
        Dictionary<string, (RawTypeInfo TypeInfo, bool IsStatic, HashSet<MemberUsage> Usages)> usageMap,
        HashSet<string> selfTypes)
    {
        var result = memberAccessAnalyzer.AnalyzeAssignment(assignment, model);
        if (result is null) return;

        var (typeInfo, memberName) = result.Value;
        if (typeInfo.IsSpecialType) return;
        if (typeFilter.ShouldExcludeByName(typeInfo.FullName)) return;

        // Property set only tracked for interfaces
        if (!typeInfo.IsInterface) return;

        if (selfTypes.Contains(typeInfo.FullName)) return;

        EnsureEntry(usageMap, typeInfo, isStatic: false);
        usageMap[typeInfo.FullName].Usages.Add(new MemberUsage(memberName, MemberUsageKind.PropertySet));
    }

    static void EnsureEntry(
        Dictionary<string, (RawTypeInfo TypeInfo, bool IsStatic, HashSet<MemberUsage> Usages)> map,
        RawTypeInfo typeInfo,
        bool isStatic)
    {
        if (!map.ContainsKey(typeInfo.FullName))
            map[typeInfo.FullName] = (typeInfo, isStatic, []);
    }
}

using System.ComponentModel;
using AmazingMCP.Services;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public static class QueryUsagesTool
{
    [McpServerTool(Name = "query_usages"), Description(
        "Finds all usages of a given type across the solution. " +
        "Use typePattern as the primary filter to specify which type to search for. " +
        "Optionally narrow results with a predicate expression.")]
    public static async Task<string> QueryUsages(
        IUsageQueryService usageQueryService,
        SolutionResolver solutionResolver,
        [Description("Absolute path to the directory where the .sln/.slnx file is located")]
        string solutionWorkspacePath,
        [Description(
            "Wildcard pattern matched against the full name of the target type involved in each usage. " +
            "Prefer the fully qualified name including namespace to avoid false positives. " +
            "Supports '*' wildcard. " +
            "Examples: \"MyApp.Core.IRequestStream\", \"*.IRequestStream\"")]
        string typePattern,
        [Description(
            "Optional C# boolean expression to further filter results. Variable 'x' is of type QueryEntry. " +
            "QueryEntry fields: " +
            "TypeName (string), " +
            "Kind (UsageKind: MethodCall, ConstructorCall, PropertyRead, PropertyWrite, FieldRead, FieldWrite, TypeAsGenericArgument, TypeAsGenericConstraint, TypeAsReturnType, TypeAsParameter), " +
            "MethodName (string?), ArgumentTypes (IReadOnlyList<string>?), PropertyName (string?), FieldName (string?). " +
            "Supports && / || / () and instance method calls on any type including collection methods: " +
            "Any(), Contains(), FirstOrDefault(). " +
            "Examples: " +
            "\"x.Kind == UsageKind.MethodCall && x.MethodName == \\\"Save\\\"\" — only calls to Save; " +
            "\"x.Kind == UsageKind.TypeAsParameter\" — only parameter usages; " +
            "\"x.Kind == UsageKind.ConstructorCall\" — only instantiations; " +
            "\"x.Kind == UsageKind.MethodCall && x.ArgumentTypes != null && x.ArgumentTypes.Any()\" — calls with arguments.")]
        string? predicate = null,
        [Description(
            "Optional. Restricts which containing types are scanned. " +
            "Does not affect what is searched — only where. " +
            "Wildcard patterns matched against the full name of the containing type. " +
            "Only types matching at least one pattern are traversed. " +
            "Leave null to scan the entire solution. " +
            "Examples: [\"MyApp.Services.*\", \"*.Persistence.*\"]")]
        string[] scanInclude = null,
        [Description(
            "Optional. Excludes specific containing types from scanning. " +
            "Wildcard patterns matched against the full name of the containing type. " +
            "Types matching any pattern are skipped even if they match scanInclude. " +
            "Leave null to exclude nothing. " +
            "Examples: [\"*.Tests.*\", \"MyApp.Generated.*\"]")]
        string[] scanExclude = null,
        [Description("Absolute path to the .sln/.slnx file. Required only when the workspace contains multiple solution files.")]
        string? solutionPath = null,
        CancellationToken ct = default)
    {
        var (resolved, error) = solutionResolver.Resolve(solutionWorkspacePath, solutionPath);
        if (resolved is null)
            return error!;

        var (matches, queryError, truncated) = await usageQueryService.QueryAsync(
            resolved,
            typePattern,
            predicate,
            scanInclude,
            scanExclude,
            ct);

        if (queryError is not null)
            return $"Error: {queryError}";

        return UsageResultFormatter.Format(matches, truncated);
    }
}

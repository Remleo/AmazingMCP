using System.ComponentModel;
using AmazingMCP.Services;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public static class QueryUsagesTool
{
    [McpServerTool(Name = "query_usages"), Description(
        "Traverses the entire solution and finds all usages of a given type. " +
        "typePattern is the primary filter — it matches against the full name of the target type involved in each usage. " +
        "The optional predicate further narrows results using a C# boolean expression where 'x' is a QueryEntry. " +
        "QueryEntry fields: " +
        "TypeName (string — full name of the target type, always set), " +
        "Kind (UsageKind enum: MethodCall, ConstructorCall, PropertyRead, PropertyWrite, FieldRead, FieldWrite, TypeAsGenericArgument, TypeAsGenericConstraint, TypeAsReturnType, TypeAsParameter), " +
        "MethodName (string?), ArgumentTypes (IReadOnlyList<string>?), PropertyName (string?), FieldName (string?). " +
        "Example: typePattern=\"MyApp.Persistence.IAnimalRepository\" finds all usages of IAnimalRepository across the solution. " +
        "Example: typePattern=\"MyApp.Persistence.IAnimalRepository\", predicate=\"x.Kind == UsageKind.MethodCall && x.MethodName == \\\"Save\\\"\" — finds only calls to Save on IAnimalRepository.")]
    public static async Task<string> QueryUsages(
        IUsageQueryService usageQueryService,
        SolutionResolver solutionResolver,
        [Description("Absolute path to the directory where the .sln/.slnx file is located")]
        string solutionWorkspacePath,
        [Description(
            "Wildcard pattern matched against the full name of the target type involved in each usage. " +
            "This is the primary filter. Supports '*' wildcard. " +
            "Prefer the fully qualified type name including namespace to avoid false positives. " +
            "Examples: \"MyApp.Core.IRequestStream\", \"*IRequestStream\"")]
        string typePattern,
        [Description(
            "Optional C# boolean expression to further filter results. Variable 'x' is of type QueryEntry. " +
            "Use && / || / () for complex conditions. " +
            "Example: \"x.Kind == UsageKind.MethodCall && x.MethodName == \\\"Save\\\"\"")]
        string? predicate = null,
        [Description(
            "Optional. Restricts which containing types are scanned during traversal. " +
            "Wildcard patterns matched against the full name of the type whose code is being analysed. " +
            "Leave null to scan the entire solution. " +
            "Supports '*' wildcard. Example: [\"MyApp.Services.*\", \"MyApp.Core.*\"]")]
        string[]? scanFilters = null,
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
            scanFilters,
            ct);

        if (queryError is not null)
            return $"Error: {queryError}";

        return UsageResultFormatter.Format(matches, truncated);
    }
}

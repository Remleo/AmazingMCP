using System.ComponentModel;
using AmazingMCP.Services.UsageQuery;
using AmazingMCP.Services.Workspace;
using ModelContextProtocol.Server;

namespace AmazingMCP.Tools;

[McpServerToolType]
public class QueryUsagesTool(
    IQueryUsagesService queryUsagesService,
    SolutionResolver solutionResolver)
{
    [McpServerTool(Name = "query_usages"), Description(
        "Finds all usages of a given type across the solution. " +
        "Use typeName as the primary filter to specify which type to search for. " +
        "Optionally narrow results with a predicate expression.")]
    public async Task<string> QueryUsages(
        [Description("Absolute path to the directory where the .sln/.slnx file is located")]
        string solutionWorkspacePath,
        [Description(
            "The fully qualified name of the target type to search for usages of. " +
            "Must include the namespace. Example: \"MyApp.Core.IRequestStream\". " +
            "For closed generics, use full type names for all arguments: \"System.Collections.Generic.List<MyApp.Core.Animal>\". " +
            "For open generics, argument names must match the declaration: \"MyApp.Persistance.IRepository<TKey, TValue>\".")]
        string typeName,
        [Description(
            "Optional C# boolean expression to further filter results. Variable 'x' is of type QueryEntry. " +
            "QueryEntry fields: " +
            "TypeName (string), " +
            "Kind (UsageKind: MethodCall, ConstructorCall, PropertyRead, PropertyWrite, FieldRead, FieldWrite, GenericArgument, GenericConstraint, ReturnType, Parameter, Inheritance, NameOf, TypeOf, IsOrAs), " +
            "MethodName (string?), ArgumentTypes (IReadOnlyList<string>?), PropertyName (string?), FieldName (string?). " +
            "Supports && / || / () and instance method calls on any type including collection methods: " +
            "Any(), Contains(), FirstOrDefault(). " +
            "Examples: " +
            "\"x.Kind == UsageKind.MethodCall && x.MethodName == \\\"Save\\\"\" — only calls to Save; " +
            "\"x.Kind == UsageKind.Parameter\" — only parameter usages; " +
            "\"x.Kind == UsageKind.ConstructorCall\" — only instantiations; " +
            "\"x.Kind == UsageKind.Inheritance\" — only types that inherit or implement the target; " +
            "\"x.Kind == UsageKind.NameOf\" — only nameof() usages; " +
            "\"x.Kind == UsageKind.TypeOf\" — only typeof() usages; " +
            "\"x.Kind == UsageKind.MethodCall && x.ArgumentTypes != null && x.ArgumentTypes.Any()\" — calls with arguments.")]
        string? predicate = null,
        [Description(
            "Optional. Restricts which containing types are scanned. " +
            "Does not affect what is searched — only where. " +
            "Wildcard patterns matched against the full name of the containing type. " +
            "Only types matching at least one pattern are traversed. " +
            "Leave null to scan the entire solution. " +
            "Examples: [\"MyApp.Services.*\", \"*.Persistence.*\"]")]
#pragma warning disable CS8625
        string[] scanInclude = null,
        [Description(
            "Optional. Excludes specific containing types from scanning. " +
            "Wildcard patterns matched against the full name of the containing type. " +
            "Types matching any pattern are skipped even if they match scanInclude. " +
            "Leave null to exclude nothing. " +
            "Examples: [\"*.Tests.*\", \"MyApp.Generated.*\"]")]
        string[] scanExclude = null,
#pragma warning restore CS8625
        [Description("Absolute path to the .sln/.slnx file. Required only when the workspace contains multiple solution files.")]
        string? solutionPath = null,
        CancellationToken ct = default)
    {
        var (resolved, error) = solutionResolver.Resolve(solutionWorkspacePath, solutionPath);
        if (resolved is null) return error!;

        return await queryUsagesService.QueryAsync(resolved, typeName, predicate, scanInclude, scanExclude, ct);
    }
}

using ModelContextProtocol.Server;

namespace AmazingMCP.Infrastructure;

/// <summary>
/// Extension methods to remove disabled tools from the MCP server tool collection.
/// </summary>
public static class DisabledToolsExtensions
{
    /// <summary>
    /// Removes tools whose names appear in <paramref name="disabledToolNames"/> from the MCP server tool collection.
    /// </summary>
    public static IServiceCollection AddDisabledTools(this IServiceCollection services, IEnumerable<string> disabledToolNames)
    {
        var disabled = disabledToolNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (disabled.Count == 0)
            return services;

        services.PostConfigure<McpServerOptions>(options =>
        {
            var toolCollection = options.ToolCollection;
            if (toolCollection is null || toolCollection.IsEmpty)
                return;

            var toRemove = toolCollection
                .Where(t => disabled.Contains(t.ProtocolTool.Name))
                .ToArray();

            foreach (var tool in toRemove)
                toolCollection.Remove(tool);
        });

        return services;
    }
}

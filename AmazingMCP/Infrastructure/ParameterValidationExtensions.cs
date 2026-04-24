using ModelContextProtocol.Server;

namespace AmazingMCP.Infrastructure;

/// <summary>
/// Extension methods to register parameter validation wrapping for all MCP tools.
/// </summary>
public static class ParameterValidationExtensions
{
    /// <summary>
    /// Wraps all registered MCP tools with <see cref="ParameterValidatingMcpServerTool"/>
    /// to provide friendly error messages when parameter deserialization fails.
    /// </summary>
    public static IServiceCollection AddToolParameterValidation(this IServiceCollection services)
    {
        services.PostConfigure<McpServerOptions>(options =>
        {
            var toolCollection = options.ToolCollection;
            if (toolCollection is null || toolCollection.IsEmpty)
                return;

            var originalTools = toolCollection.ToArray();
            toolCollection.Clear();

            foreach (var tool in originalTools)
            {
                toolCollection.Add(new ParameterValidatingMcpServerTool(tool));
            }
        });

        return services;
    }
}

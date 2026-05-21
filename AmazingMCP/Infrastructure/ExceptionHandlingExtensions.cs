using AmazingMCP.Configuration;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace AmazingMCP.Infrastructure;

/// <summary>
/// Extension methods to register exception handling wrapping for all MCP tools.
/// </summary>
static class ExceptionHandlingExtensions
{
    /// <summary>
    /// Wraps all registered MCP tools with <see cref="ExceptionHandlingMcpServerTool"/>
    /// to catch unhandled exceptions and return them as error responses.
    /// Must be called after <see cref="ParameterValidationExtensions.AddToolParameterValidation"/>
    /// so that exception handling is the outermost layer.
    /// </summary>
    public static IServiceCollection AddExceptionHandling(this IServiceCollection services)
    {
        services.AddSingleton<IPostConfigureOptions<McpServerOptions>, ExceptionHandlingPostConfigure>();
        return services;
    }

    sealed class ExceptionHandlingPostConfigure(IOptions<DiagnosticsOptions> diagnosticsOptions)
        : IPostConfigureOptions<McpServerOptions>
    {
        public void PostConfigure(string? name, McpServerOptions options)
        {
            var toolCollection = options.ToolCollection;
            if (toolCollection is null || toolCollection.IsEmpty)
                return;

            var originalTools = toolCollection.ToArray();
            toolCollection.Clear();

            foreach (var tool in originalTools)
            {
                toolCollection.Add(new ExceptionHandlingMcpServerTool(tool, diagnosticsOptions));
            }
        }
    }
}

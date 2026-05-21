using AmazingMCP.Configuration;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AmazingMCP.Infrastructure;

/// <summary>
/// Wraps each MCP tool to catch unhandled exceptions and return them as error responses
/// with optional full exception details controlled by <see cref="DiagnosticsOptions"/>.
/// </summary>
sealed class ExceptionHandlingMcpServerTool(McpServerTool innerTool, IOptions<DiagnosticsOptions> options)
    : DelegatingMcpServerTool(innerTool)
{
    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.InvokeAsync(request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (!options.Value.IncludeExceptionDetails)
                throw;

            return new()
            {
                IsError = true,
                Content = [new TextContentBlock { Text = $"[Internal Error] {ex}" }]
            };
        }
    }
}

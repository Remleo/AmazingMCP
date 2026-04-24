using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AmazingMCP.Infrastructure;

/// <summary>
/// Wraps each MCP tool to catch <see cref="JsonException"/> during parameter
/// deserialization and return a user-friendly error that includes the parameter name.
/// </summary>
public sealed class ParameterValidatingMcpServerTool(McpServerTool innerTool) : DelegatingMcpServerTool(innerTool)
{
    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.InvokeAsync(request, cancellationToken);
        }
        catch (JsonException ex)
        {
            var (paramName, rawValue, expectedType) = TryIdentifyParameter(request);
            var message = paramName is not null
                ? $"""
                  Parameter binding failed for tool '{ProtocolTool.Name}'.
                    Parameter : {paramName}
                    Received  : {rawValue}
                    Expected  : {expectedType ?? "unknown"}
                    Error     : {ex.Message}
                  """
                : $"Parameter binding failed for tool '{ProtocolTool.Name}': {ex.Message}";

            return new()
            {
                IsError = true,
                Content = [new TextContentBlock { Text = message }]
            };
        }
    }

    /// <summary>
    /// Attempts to identify which parameter caused the deserialization error
    /// by comparing the supplied arguments against the expected JSON schema.
    /// Returns the parameter name, its raw JSON value, and expected type — or nulls if not found.
    /// </summary>
    (string? name, string? rawValue, string? expectedType) TryIdentifyParameter(RequestContext<CallToolRequestParams> request)
    {
        var arguments = request.Params?.Arguments;
        if (arguments is null || arguments.Count == 0)
            return (null, null, null);

        var schema = ProtocolTool.InputSchema;
        if (schema.ValueKind != JsonValueKind.Object)
            return (null, null, null);

        if (!schema.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object)
            return (null, null, null);

        foreach (var arg in arguments)
        {
            if (!properties.TryGetProperty(arg.Key, out var propSchema))
                continue;

            if (!IsValueCompatible(arg.Value, propSchema))
                return (arg.Key, arg.Value.GetRawText(), GetSchemaType(propSchema));
        }

        return (null, null, null);
    }

    /// <summary>
    /// Performs a basic type compatibility check between a JSON value and its schema definition.
    /// </summary>
    static bool IsValueCompatible(JsonElement value, JsonElement schema)
    {
        var expectedType = GetSchemaType(schema);
        if (expectedType is null)
            return true; // cannot determine — skip

        return expectedType switch
        {
            "string" => value.ValueKind == JsonValueKind.String,
            "number" or "integer" => value.ValueKind == JsonValueKind.Number,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "array" => value.ValueKind == JsonValueKind.Array,
            "object" => value.ValueKind == JsonValueKind.Object,
            "null" => value.ValueKind == JsonValueKind.Null,
            _ => true
        };
    }

    static string? GetSchemaType(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var typeElement))
            return null;

        // "type" can be a string or an array of strings
        return typeElement.ValueKind == JsonValueKind.String
            ? typeElement.GetString()
            : null;
    }
}

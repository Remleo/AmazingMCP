using AmazingMCP.Configuration;
using Microsoft.Extensions.Options;

namespace AmazingMCP.Services.FileAnalysis;

public class ReadLargeCsFileService(
    IFilteredSourceService filteredSource,
    IOptions<ReadCsOptions> options) : IReadLargeCsFileService
{
    readonly ReadCsOptions _options = options.Value;

    public string Read(string filePath, string[]? filters)
    {
        var result = filteredSource.GetFilteredSource(filePath, filters);

        if (result.Contains("No matches found"))
            return result + "\n\n" +
                   "> No members matched. Use `read_cs_file_digest` to see the compact outline and find correct member names/signatures.";

        var truncationMarker =
            "\n\n<< ... output truncated ... >>\n\n" +
            $"> Output exceeded {_options.ReadOutputMaxLength:N0} characters and was cut off.\n" +
            "> Use narrower filter patterns to target specific members (e.g. [\"*MethodName*\"]).\n" +
            "> To get a structural overview of the file, use `read_cs_file_digest`.";

        if (result.Length > _options.ReadOutputMaxLength)
            return result[.._options.ReadOutputMaxLength] + truncationMarker;

        return result;
    }
}

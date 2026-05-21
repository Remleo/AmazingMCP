using AmazingMCP.Configuration;
using Microsoft.Extensions.Options;

namespace AmazingMCP.Services.FileAnalysis;

public class ReadLargeCsFileService(
    IFileReader fileReader,
    IFilteredSourceService filteredSource,
    IOptions<ReadCsOptions> options) : IReadLargeCsFileService
{
    readonly ReadCsOptions _options = options.Value;

    public string Read(string filePath, string[]? filters)
    {
        filePath = Path.GetFullPath(filePath);

        if (!fileReader.Exists(filePath))
            return $"File not found: {filePath}";

        var source = fileReader.ReadAllText(filePath);

        if (filters is not { Length: > 0 })
        {
            if (source.Length > _options.ReadOutputMaxLength)
                return $"File is too large ({source.Length:N0} chars) to return without filters. " +
                       "Use wildcard `filters` to select specific members, or call `read_cs_file_digest` to see the compact outline.";

            return source;
        }

        var result = filteredSource.GetFilteredSource(source, filters);

        if (result.Contains("No matches found"))
            return result + "\n\n" +
                   "> No members matched. Use `read_cs_file_digest` to see the compact outline and find correct member names/signatures.";

        if (result.Length > _options.ReadOutputMaxLength)
        {
            var truncationMarker =
                "\n\n<< ... output truncated ... >>\n\n" +
                $"> Output exceeded {_options.ReadOutputMaxLength:N0} characters and was cut off.\n" +
                "> Use narrower filter patterns to target specific members (e.g. [\"*MethodName*\"]).\n" +
                "> To get a structural overview of the file, use `read_cs_file_digest`.";

            return result[.._options.ReadOutputMaxLength] + truncationMarker;
        }

        return result;
    }
}

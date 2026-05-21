namespace AmazingMCP.Services.FileAnalysis;

public class ReadCsFileDigestService(
    IFileReader fileReader,
    ISourceDigestService sourceDigest) : IReadCsFileDigestService
{
    public string Read(string filePath)
    {
        filePath = Path.GetFullPath(filePath);

        if (!fileReader.Exists(filePath))
            return $"File not found: {filePath}";

        var source = fileReader.ReadAllText(filePath);
        var digest = sourceDigest.GetDigest(source, includeLineNumbers: true);

        return digest + "\n\n" +
               "> PREFER `read_large_cs_file` over reading the raw file — shows real source of any member by name/signature without loading the whole file.\n" +
               "> Examples: `[\"*ProcessAsync*\"]`, `[\"usings\", \"*public*\"]`, `[\"*Async*\"]`";
    }
}

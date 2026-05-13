namespace AmazingMCP.Services.FileAnalysis;

public class ReadCsFileDigestService(IFileDigestService fileDigest) : IReadCsFileDigestService
{
    public string Read(string filePath) =>
        fileDigest.GetStructure(filePath) + "\n\n" +
        "> PREFER `read_large_cs_file` over reading the raw file — shows real source of any member by name/signature without loading the whole file.\n" +
        "> Examples: `[\"*ProcessAsync*\"]`, `[\"usings\", \"*public*\"]`, `[\"*Async*\"]`";
}

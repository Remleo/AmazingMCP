namespace AmazingMCP.Services.FileAnalysis;

public interface ISourceDigestService
{
    string GetDigest(string source, bool includeLineNumbers);
}

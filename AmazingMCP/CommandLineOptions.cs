using CommandLine;

namespace AmazingMCP;

class CommandLineOptions
{
    [Option("urls", HelpText = "Listening URL (default: http://localhost:7777)")]
    public string? Urls { get; set; }
}

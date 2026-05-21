using CommandLine;

namespace AmazingMCP;

class CommandLineOptions
{
    [Option("urls", HelpText = "Listening URL (default: http://localhost:7777)")]
    public string? Urls { get; set; }

    [Option("Symbol:QueryOutputLineLimit", Default = 100, HelpText = "Max output lines for query_symbol")]
    public int? SymbolQueryOutputLineLimit { get; set; }

    [Option("ReadCs:ReadOutputMaxLength", Default = 20000, HelpText = "Max output characters for read_large_cs_file")]
    public int? ReadCsReadOutputMaxLength { get; set; }

    [Option("ProjectDesign:DetailsOutputMaxLength", Default = 30000, HelpText = "Max output characters for get_project_design_details")]
    public int? ProjectDesignDetailsOutputMaxLength { get; set; }

    [Option("ProjectDesign:DetailsXmlDocSummaryMaxLength", Default = 2000, HelpText = "Max XML doc summary characters in get_project_design_details")]
    public int? ProjectDesignDetailsXmlDocSummaryMaxLength { get; set; }

    [Option("QueryUsages:QueryMatchLimit", Default = 200, HelpText = "Max usage matches for query_usages")]
    public int? QueryUsagesQueryMatchLimit { get; set; }

    [Option("Diagnostics:IncludeExceptionDetails", Default = false, HelpText = "Include full exception details in tool error responses")]
    public bool? DiagnosticsIncludeExceptionDetails { get; set; }
}

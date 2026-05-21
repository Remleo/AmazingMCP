using CommandLine;
using AmazingMCP;
using AmazingMCP.Configuration;
using Microsoft.Build.Locator;
using AmazingMCP.Infrastructure;
using AmazingMCP.Services;
using AmazingMCP.Services.CodeLens;
using AmazingMCP.Services.Design;
using AmazingMCP.Services.Decompile;
using AmazingMCP.Services.FileAnalysis;
using AmazingMCP.Services.Scanning;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.UsageQuery;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Services.Workspace;
using AmazingMCP.Tools;

if (Parser.Default.ParseArguments<CommandLineOptions>(args).Tag == ParserResultType.NotParsed)
    return;

// MSBuild Locator MUST be registered before any Roslyn Workspace types are loaded
MSBuildLocator.RegisterDefaults();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();

// Options
builder.Services.Configure<SymbolOptions>(builder.Configuration.GetSection("Symbol"));
builder.Services.Configure<ReadCsOptions>(builder.Configuration.GetSection("ReadCs"));
builder.Services.Configure<ProjectDesignOptions>(builder.Configuration.GetSection("ProjectDesign"));
builder.Services.Configure<QueryUsagesOptions>(builder.Configuration.GetSection("QueryUsages"));
builder.Services.Configure<DiagnosticsOptions>(builder.Configuration.GetSection("Diagnostics"));

// Infrastructure
builder.Services.AddSingleton<IWildcardPatternFactory, WildcardPatternFactory>();
builder.Services.AddSingleton<ISolutionLoader, SolutionLoader>();
builder.Services.AddSingleton<ISolutionWatcher, SolutionWatcher>();
builder.Services.AddSingleton<ISolutionCache, SolutionCache>();
builder.Services.AddSingleton<ISolutionRecompiler, SolutionRecompiler>();
builder.Services.AddSingleton<WorkspaceProvider>();
builder.Services.AddSingleton<IWorkspaceProvider>(sp => sp.GetRequiredService<WorkspaceProvider>());
builder.Services.AddSingleton<ISolutionResolver, SolutionResolver>();
builder.Services.AddSingleton<IXmlDocExtractor, XmlDocExtractor>();
builder.Services.AddSingleton<RoslynSymbolService>();
builder.Services.AddSingleton<ISymbolQueryService, SymbolQueryService>();
builder.Services.AddSingleton<SymbolInfoService>();
builder.Services.AddSingleton<IDecompileTypeService, DecompileTypeService>();
builder.Services.AddSingleton<IFileReader, FileSystemFileReader>();
builder.Services.AddSingleton<IFileStructureService, FileStructureService>();
builder.Services.AddSingleton<ISourceDigestService, SourceDigestService>();
builder.Services.AddSingleton<IFilteredSourceService, FilteredSourceService>();
builder.Services.AddSingleton<IReadLargeCsFileService, ReadLargeCsFileService>();
builder.Services.AddSingleton<IReadCsFileDigestService, ReadCsFileDigestService>();

// Scanning
builder.Services.AddSingleton<ITypeFilter, TypeFilter>();
builder.Services.AddSingleton<IInvocationAnalyzer, InvocationAnalyzer>();
builder.Services.AddSingleton<IMemberAccessAnalyzer, MemberAccessAnalyzer>();
builder.Services.AddSingleton<IMemberUsageAnalyzer, MemberUsageAnalyzer>();

// Usage query
builder.Services.AddSingleton<IInheritanceSearchSymbolResolver, InheritanceSearchSymbolResolver>();
builder.Services.AddSingleton<IDerivedTypeService, RoslynDerivedTypeService>();
builder.Services.AddSingleton<IInheritanceUsageProvider, InheritanceUsageProvider>();
builder.Services.AddSingleton<IUsageProvider, UsageProvider>();
builder.Services.AddSingleton<IUsageResultFormatter, UsageResultFormatter>();
builder.Services.AddSingleton<IQueryUsagesService, QueryUsagesService>();

// Core services
builder.Services.AddSingleton<ITypeCollector, TypeCollector>();
builder.Services.AddSingleton<IAbstractionExtractor, AbstractionExtractor>();
builder.Services.AddSingleton<IDependencyAggregator, DependencyAggregator>();
builder.Services.AddSingleton<IDependencyMapService, DependencyMapService>();
builder.Services.AddSingleton<IProjectDesignProvider, ProjectDesignProvider>();
builder.Services.AddSingleton<IProjectDesignDetailsService, ProjectDesignDetailsService>();
builder.Services.AddSingleton<IProjectDesignService, ProjectDesignService>();
builder.Services.AddSingleton<ICodeLensService, CodeLensService>();

// Tools
builder.Services.AddSingleton<QuerySymbolTool>();
builder.Services.AddSingleton<ReadLargeCsFileTool>();
builder.Services.AddSingleton<GetProjectDesignDetailsTool>();
builder.Services.AddSingleton<QueryUsagesTool>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "AmazingMCP", Version = "1.0.0" };
    })
    .WithHttpTransport()
    .WithToolsFromAssembly();

builder.Services.AddToolParameterValidation();
builder.Services.AddExceptionHandling();

var app = builder.Build();

app.MapMcp();

app.Run();

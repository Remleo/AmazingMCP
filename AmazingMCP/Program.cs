using CommandLine;
using AmazingMCP;
using AmazingMCP.Configuration;
using AmazingMCP.Models;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using AmazingMCP.Infrastructure;
using AmazingMCP.Services;
using ModelContextProtocol.AspNetCore;
using AmazingMCP.Services.CodeLens;
using AmazingMCP.Services.Design;
using AmazingMCP.Services.Decompile;
using AmazingMCP.Services.FileAnalysis;
using AmazingMCP.Services.Scanning;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.SymbolQuery.Strategies;
using AmazingMCP.Services.UsageQuery;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Services.Workspace;
using AmazingMCP.Tools;

var parseResult = Parser.Default.ParseArguments<CommandLineOptions>(args);
if (parseResult.Tag == ParserResultType.NotParsed)
    return;

var cmdOptions = parseResult.Value;

// MSBuild Locator MUST be registered before any Roslyn Workspace types are loaded
MSBuildLocator.RegisterDefaults();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();

// Options
builder.Services
    .Configure<SymbolOptions>(builder.Configuration.GetSection("Symbol"))
    .Configure<ReadCsOptions>(builder.Configuration.GetSection("ReadCs"))
    .Configure<ProjectDesignOptions>(builder.Configuration.GetSection("ProjectDesign"))
    .Configure<QueryUsagesOptions>(builder.Configuration.GetSection("QueryUsages"))
    .Configure<DiagnosticsOptions>(builder.Configuration.GetSection("Diagnostics"))
    .Configure<HttpServerTransportOptions>(builder.Configuration.GetSection("HttpServerTransport"));

// Infrastructure
builder.Services
    .AddSingleton<IWildcardPatternFactory, WildcardPatternFactory>()
    .AddSingleton<ISolutionLoader, SolutionLoader>()
    .AddSingleton<ISolutionWatcher, SolutionWatcher>()
    .AddSingleton<ISolutionCache, SolutionCache>()
    .AddSingleton<ISolutionRecompiler, SolutionRecompiler>()
    .AddSingleton<WorkspaceProvider>()
    .AddSingleton<IWorkspaceProvider>(sp => sp.GetRequiredService<WorkspaceProvider>())
    .AddSingleton<ISolutionResolver, SolutionResolver>()
    .AddSingleton<IXmlDocExtractor, XmlDocExtractor>()
    .AddSingleton<NuGetVersionResolver>()
    .AddKeyedSingleton<ITypeEnumerationStrategy<INamedTypeSymbol>>(TypeEnumerationMode.Simple, (_, _) => new SimpleTypeStrategy())
    .AddKeyedSingleton<ITypeEnumerationStrategy<INamedTypeSymbol>>(TypeEnumerationMode.AllInstances, (_, _) => new AllInstancesTypeStrategy())
    .AddKeyedSingleton<ITypeEnumerationStrategy<TypeVersionGroup>>(TypeEnumerationMode.Versioned, (_, _) => new VersionedTypeStrategy())
    .AddSingleton<RoslynTypeProvider>()
    .AddSingleton<IRoslynTypeProvider>(sp => sp.GetRequiredService<RoslynTypeProvider>())
    .AddSingleton<IRoslynSymbolService, RoslynSymbolService>()
    .AddSingleton<ISymbolQueryService, SymbolQueryService>()
    .AddSingleton<SymbolInfoService>()
    .AddSingleton<IDecompileTypeService, DecompileTypeService>()
    .AddSingleton<IFileReader, FileSystemFileReader>()
    .AddSingleton<IFileStructureService, FileStructureService>()
    .AddSingleton<ISourceDigestService, SourceDigestService>()
    .AddSingleton<IFilteredSourceService, FilteredSourceService>()
    .AddSingleton<IReadLargeCsFileService, ReadLargeCsFileService>()
    .AddSingleton<IReadCsFileDigestService, ReadCsFileDigestService>();

// Scanning
builder.Services
    .AddSingleton<ITypeFilter, TypeFilter>()
    .AddSingleton<IInvocationAnalyzer, InvocationAnalyzer>()
    .AddSingleton<IMemberAccessAnalyzer, MemberAccessAnalyzer>()
    .AddSingleton<IMemberUsageAnalyzer, MemberUsageAnalyzer>();

// Usage query
builder.Services
    .AddSingleton<IInheritanceSearchSymbolResolver, InheritanceSearchSymbolResolver>()
    .AddSingleton<IDerivedTypeService, RoslynDerivedTypeService>()
    .AddSingleton<IInheritanceUsageProvider, InheritanceUsageProvider>()
    .AddSingleton<IUsageProvider, UsageProvider>()
    .AddSingleton<IUsageResultFormatter, UsageResultFormatter>()
    .AddSingleton<IQueryUsagesService, QueryUsagesService>();

// Core builder.Services
builder.Services
    .AddSingleton<ITypeCollector, TypeCollector>()
    .AddSingleton<IAbstractionExtractor, AbstractionExtractor>()
    .AddSingleton<IDependencyAggregator, DependencyAggregator>()
    .AddSingleton<IDependencyMapService, DependencyMapService>()
    .AddSingleton<IProjectDesignProvider, ProjectDesignProvider>()
    .AddSingleton<IProjectDesignDetailsService, ProjectDesignDetailsService>()
    .AddSingleton<IProjectDesignService, ProjectDesignService>()
    .AddSingleton<ICodeLensService, CodeLensService>();

// Tools
builder.Services
    .AddSingleton<QuerySymbolTool>()
    .AddSingleton<ReadLargeCsFileTool>()
    .AddSingleton<GetProjectDesignDetailsTool>()
    .AddSingleton<QueryUsagesTool>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "AmazingMCP", Version = "1.0.0" };
    })
    .WithHttpTransport()
    .WithToolsFromAssembly();

builder.Services
    .AddToolParameterValidation()
    .AddExceptionHandling()
    .AddDisabledTools(cmdOptions.DisabledTools);

var app = builder.Build();

app.MapMcp();

app.Run();

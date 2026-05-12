using Microsoft.Build.Locator;
using AmazingMCP.Infrastructure;
using AmazingMCP.Services;
using AmazingMCP.Services.CodeLens;
using AmazingMCP.Services.Design;
using AmazingMCP.Services.FileAnalysis;
using AmazingMCP.Services.Scanning;
using AmazingMCP.Services.SymbolQuery;
using AmazingMCP.Services.UsageQuery;
using AmazingMCP.Services.Wildcard;
using AmazingMCP.Services.Workspace;

// MSBuild Locator MUST be registered before any Roslyn Workspace types are loaded
MSBuildLocator.RegisterDefaults();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();

// Infrastructure
builder.Services.AddSingleton<IWildcardPatternFactory, WildcardPatternFactory>();
builder.Services.AddSingleton<ISolutionLoader, SolutionLoader>();
builder.Services.AddSingleton<ISolutionWatcher, SolutionWatcher>();
builder.Services.AddSingleton<ISolutionCache, SolutionCache>();
builder.Services.AddSingleton<ISolutionRecompiler, SolutionRecompiler>();
builder.Services.AddSingleton<WorkspaceProvider>();
builder.Services.AddSingleton<IWorkspaceProvider>(sp => sp.GetRequiredService<WorkspaceProvider>());
builder.Services.AddSingleton<SolutionResolver>();
builder.Services.AddSingleton<IXmlDocExtractor, XmlDocExtractor>();
builder.Services.AddSingleton<RoslynSymbolService>();
builder.Services.AddSingleton<SymbolInfoService>();
builder.Services.AddSingleton<IFileReader, FileSystemFileReader>();
builder.Services.AddSingleton<IFileStructureService, FileStructureService>();
builder.Services.AddSingleton<IFileDigestService, FileDigestService>();
builder.Services.AddSingleton<IFilteredSourceService, FilteredSourceService>();

// Scanning
builder.Services.AddSingleton<ITypeFilter, TypeFilter>();
builder.Services.AddSingleton<IInvocationAnalyzer, InvocationAnalyzer>();
builder.Services.AddSingleton<IMemberAccessAnalyzer, MemberAccessAnalyzer>();
builder.Services.AddSingleton<IMemberUsageAnalyzer, MemberUsageAnalyzer>();

// Usage query
builder.Services.AddSingleton<IUsageQueryService, UsageQueryService>();

// Core services
builder.Services.AddSingleton<ITypeCollector, TypeCollector>();
builder.Services.AddSingleton<IAbstractionExtractor, AbstractionExtractor>();
builder.Services.AddSingleton<IDependencyAggregator, DependencyAggregator>();
builder.Services.AddSingleton<IDependencyMapService, DependencyMapService>();
builder.Services.AddSingleton<ProjectDesignService>();
builder.Services.AddSingleton<ICodeLensService, CodeLensService>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "AmazingMCP", Version = "1.0.0" };
    })
    .WithHttpTransport()
    .WithToolsFromAssembly();

builder.Services.AddToolParameterValidation();

var app = builder.Build();

app.MapMcp("/sse");

app.Run();

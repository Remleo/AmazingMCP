using Microsoft.Build.Locator;
using AmazingMCP.Services;
using AmazingMCP.Services.Scanning;

// MSBuild Locator MUST be registered before any Roslyn Workspace types are loaded
MSBuildLocator.RegisterDefaults();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();

// Infrastructure
builder.Services.AddSingleton<IWildcardPatternFactory, WildcardPatternFactory>();
builder.Services.AddSingleton<WorkspaceProvider>();
builder.Services.AddSingleton<IWorkspaceProvider>(sp => sp.GetRequiredService<WorkspaceProvider>());
builder.Services.AddSingleton<SolutionResolver>();
builder.Services.AddSingleton<RoslynSymbolService>();
builder.Services.AddSingleton<SymbolInfoService>();
builder.Services.AddSingleton<FileStructureService>();
builder.Services.AddSingleton<FilteredSourceService>();

// Scanning
builder.Services.AddSingleton<ITypeFilter, TypeFilter>();
builder.Services.AddSingleton<IInvocationAnalyzer, InvocationAnalyzer>();
builder.Services.AddSingleton<IMemberAccessAnalyzer, MemberAccessAnalyzer>();
builder.Services.AddSingleton<IMemberUsageAnalyzer, MemberUsageAnalyzer>();

// Core services
builder.Services.AddSingleton<ITypeCollector, TypeCollector>();
builder.Services.AddSingleton<IAbstractionExtractor, AbstractionExtractor>();
builder.Services.AddSingleton<IDependencyAggregator, DependencyAggregator>();
builder.Services.AddSingleton<DependencyMapService>();
builder.Services.AddSingleton<ProjectDesignService>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "AmazingMCP", Version = "1.0.0" };
    })
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapMcp("/sse");

app.Run();

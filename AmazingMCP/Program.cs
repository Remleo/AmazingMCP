using Microsoft.Build.Locator;
using AmazingMCP.Services;

// MSBuild Locator MUST be registered before any Roslyn Workspace types are loaded
MSBuildLocator.RegisterDefaults();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<WorkspaceProvider>();
builder.Services.AddSingleton<IWorkspaceProvider>(sp => sp.GetRequiredService<WorkspaceProvider>());
builder.Services.AddSingleton<ITypeCollector, TypeCollector>();
builder.Services.AddSingleton<IConstructorAnalyzer, ConstructorAnalyzer>();
builder.Services.AddSingleton<IMemberUsageAnalyzer, MemberUsageAnalyzer>();
builder.Services.AddSingleton<IAbstractionExtractor, AbstractionExtractor>();
builder.Services.AddSingleton<RoslynSymbolService>();
builder.Services.AddSingleton<SymbolInfoService>();
builder.Services.AddSingleton<DependencyMapService>();
builder.Services.AddSingleton<ProjectDesignService>();
builder.Services.AddSingleton<SolutionResolver>();

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

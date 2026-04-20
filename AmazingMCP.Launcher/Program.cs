using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddMcpServer(options => { options.ServerInfo = new() { Name = "AmazingMCP.Launcher", Version = "1.0.0" }; })
    .WithStdioServerTransport();

// Extract --urls value from launcher args to forward to the child process
var urlsArg = string.Empty;
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i].Equals("--urls", StringComparison.OrdinalIgnoreCase))
    {
        urlsArg = $"--urls \"{args[i + 1]}\"";
        break;
    }
}

builder.Services.AddHostedService(sp =>
    new AmazingMcpHostService(sp.GetRequiredService<IHostApplicationLifetime>(), urlsArg));

var app = builder.Build();
await app.RunAsync();

/// <summary>
/// Hosted service that starts the main AmazingMCP HTTP server as a child process.
/// </summary>
public class AmazingMcpHostService(IHostApplicationLifetime lifetime, string urlsArg) : IHostedService, IDisposable
{
    Process? _process;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        const string projectDir = @"C:\dotNet\AmazingMCP\AmazingMCP";

        var arguments = string.IsNullOrEmpty(urlsArg)
            ? $"run --project \"{projectDir}\""
            : $"run --project \"{projectDir}\" {urlsArg}";

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };

        try
        {
            if (!_process.Start())
            {
                await Console.Error.WriteLineAsync("Failed to start AmazingMCP process.");
                lifetime.StopApplication();
                return;
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Failed to start AmazingMCP process: {ex.Message}");
            lifetime.StopApplication();
            return;
        }

        // Give the process a moment to crash if it's going to
        try
        {
            await Task.Delay(2000, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_process.HasExited && _process.ExitCode != 0)
        {
            var stderr = await _process.StandardError.ReadToEndAsync(cancellationToken);
            await Console.Error.WriteLineAsync(
                $"AmazingMCP process exited immediately with code {_process.ExitCode}.{(string.IsNullOrWhiteSpace(stderr) ? "" : $"\n{stderr}")}");
            lifetime.StopApplication();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        KillProcess();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        KillProcess();
        _process?.Dispose();
    }

    void KillProcess()
    {
        if (_process is null || _process.HasExited)
            return;

        try
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(3000);
        }
        catch
        {
            // Process already exited
        }
    }
}
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "AmazingMCP.Launcher", Version = "1.0.0" };
    })
    .WithStdioServerTransport();

builder.Services.AddHostedService<AmazingMcpHostService>();

var app = builder.Build();
await app.RunAsync();

/// <summary>
/// Hosted service that starts the main AmazingMCP HTTP server as a child process.
/// </summary>
public class AmazingMcpHostService : IHostedService, IDisposable
{
    private readonly IHostApplicationLifetime _lifetime;
    private Process? _process;

    public AmazingMcpHostService(IHostApplicationLifetime lifetime)
    {
        _lifetime = lifetime;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        const string projectDir = @"C:\dotNet\AmazingMCP\AmazingMCP";

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{projectDir}\"",
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
                _lifetime.StopApplication();
                return;
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Failed to start AmazingMCP process: {ex.Message}");
            _lifetime.StopApplication();
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
            _lifetime.StopApplication();
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

    private void KillProcess()
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

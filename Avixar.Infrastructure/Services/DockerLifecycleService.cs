using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Avixar.Infrastructure.Services;

public class DockerLifecycleService : IHostedService
{
    private readonly ILogger<DockerLifecycleService> _logger;
    private readonly IHostEnvironment _environment;

    public DockerLifecycleService(ILogger<DockerLifecycleService> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🐳 Starting Docker containers...");
        RunCommand("docker-compose", "up -d");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🛑 Stopping Docker containers...");
        RunCommand("docker-compose", "stop");
        return Task.CompletedTask;
    }

    private void RunCommand(string command, string args)
    {
        try
        {
            // Get the solution root directory (go up from ContentRootPath)
            var projectRoot = Directory.GetParent(_environment.ContentRootPath)?.FullName 
                ?? _environment.ContentRootPath;

            var processInfo = new ProcessStartInfo(command, args)
            {
                WorkingDirectory = projectRoot,  // Set working directory to solution root
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(processInfo);
            process?.WaitForExit();
            
            var output = process?.StandardOutput.ReadToEnd();
            var error = process?.StandardError.ReadToEnd();
            
            if (!string.IsNullOrWhiteSpace(output))
                _logger.LogInformation("Docker output: {Output}", output);
            
            if (!string.IsNullOrWhiteSpace(error))
                _logger.LogWarning("Docker stderr: {Error}", error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Docker command failed: {Message}", ex.Message);
            _logger.LogWarning("Make sure Docker Desktop is running and docker-compose is in PATH!");
        }
    }
}

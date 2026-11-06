using LabReport.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LabReport.Tests;

public sealed class DeskFactory : WebApplicationFactory<Program>
{
    public string Storage { get; } = Path.Combine(Path.GetTempPath(), "labreport-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(FindContentRoot());
        builder.UseSetting("Storage:Root", Storage);
        builder.UseSetting("Data:Provider", "Json");
        builder.UseSetting("Worker:MaxConcurrency", "1");
        builder.UseSetting("Worker:RenderTimeoutSeconds", "90");
    }

    public static string FindContentRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "LabReport.Web");
            if (File.Exists(Path.Combine(candidate, "LabReport.Web.csproj")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate src/LabReport.Web.");
    }

    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(FindContentRoot());
        return dir.Parent?.Parent?.FullName ?? throw new InvalidOperationException("Repo root was not found.");
    }
}

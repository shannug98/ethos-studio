using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Ethos.Api.Tests;

public class TestWebHostEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "Ethos.Api";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = null!;
    public string WebRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider WebRootFileProvider { get; set; } = null!;
}

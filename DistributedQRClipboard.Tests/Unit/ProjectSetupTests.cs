namespace DistributedQRClipboard.Tests.Unit;

public class ProjectSetupTests
{
    [Fact]
    public void Solution_ShouldHaveCorrectProjectStructure()
    {
        // Arrange & Act
        var solutionDirectory = GetSolutionDirectory();
        
        // Assert - Verify all projects exist
        Assert.True(Directory.Exists(Path.Combine(solutionDirectory, "DistributedQRClipboard.Api")),
            "API project directory should exist");
        Assert.True(Directory.Exists(Path.Combine(solutionDirectory, "DistributedQRClipboard.Core")),
            "Core project directory should exist");
        Assert.True(Directory.Exists(Path.Combine(solutionDirectory, "DistributedQRClipboard.Infrastructure")),
            "Infrastructure project directory should exist");
        Assert.True(Directory.Exists(Path.Combine(solutionDirectory, "DistributedQRClipboard.Tests")),
            "Tests project directory should exist");
        
        // Verify solution file exists
        Assert.True(File.Exists(Path.Combine(solutionDirectory, "DistributedQRClipboard.sln")),
            "Solution file should exist");
    }

    [Fact]
    public void Projects_ShouldHaveNullableReferenceTypesEnabled()
    {
        // Arrange
        var solutionDirectory = GetSolutionDirectory();
        var projectFiles = new[]
        {
            Path.Combine(solutionDirectory, "DistributedQRClipboard.Api", "DistributedQRClipboard.Api.csproj"),
            Path.Combine(solutionDirectory, "DistributedQRClipboard.Core", "DistributedQRClipboard.Core.csproj"),
            Path.Combine(solutionDirectory, "DistributedQRClipboard.Infrastructure", "DistributedQRClipboard.Infrastructure.csproj"),
            Path.Combine(solutionDirectory, "DistributedQRClipboard.Tests", "DistributedQRClipboard.Tests.csproj")
        };

        // Act & Assert
        foreach (var projectFile in projectFiles)
        {
            Assert.True(File.Exists(projectFile), $"Project file should exist: {projectFile}");
            
            var content = File.ReadAllText(projectFile);
            Assert.Contains("<Nullable>enable</Nullable>", content);
            Assert.Contains("<LangVersion>13</LangVersion>", content);
        }
    }

    [Fact]
    public void Dependencies_ShouldBeCorrectlyConfigured()
    {
        // Arrange
        var solutionDirectory = GetSolutionDirectory();
        var apiProjectFile = Path.Combine(solutionDirectory, "DistributedQRClipboard.Api", "DistributedQRClipboard.Api.csproj");
        
        // Act
        var content = File.ReadAllText(apiProjectFile);
        
        // Assert - Verify key packages are referenced
        Assert.Contains("QRCoder", content);
        Assert.Contains("Polly", content);
        Assert.Contains("Serilog.AspNetCore", content);
        Assert.Contains("Microsoft.Extensions.Caching.Memory", content);
        
        // Verify project references
        Assert.Contains("DistributedQRClipboard.Core", content);
        Assert.Contains("DistributedQRClipboard.Infrastructure", content);
    }

    [Fact]
    public void Configuration_ShouldHaveRequiredSettings()
    {
        // Arrange
        var solutionDirectory = GetSolutionDirectory();
        var appsettingsFile = Path.Combine(solutionDirectory, "DistributedQRClipboard.Api", "appsettings.json");
        
        // Act
        Assert.True(File.Exists(appsettingsFile), "appsettings.json should exist");
        
        var content = File.ReadAllText(appsettingsFile);
        
        // Assert
        Assert.Contains("ClipboardSettings", content);
        Assert.Contains("SessionExpirationHours", content);
        Assert.Contains("MaxContentLength", content);
        Assert.Contains("MaxDevicesPerSession", content);
    }

    [Fact]
    public void DirectoryStructure_ShouldFollowDesignPattern()
    {
        // Arrange
        var solutionDirectory = GetSolutionDirectory();
        
        // Act & Assert - Verify Core project structure
        var coreProject = Path.Combine(solutionDirectory, "DistributedQRClipboard.Core");
        Assert.True(Directory.Exists(Path.Combine(coreProject, "Interfaces")), "Core should have Interfaces folder");
        Assert.True(Directory.Exists(Path.Combine(coreProject, "Services")), "Core should have Services folder");
        Assert.True(Directory.Exists(Path.Combine(coreProject, "Models")), "Core should have Models folder");
        Assert.True(Directory.Exists(Path.Combine(coreProject, "Exceptions")), "Core should have Exceptions folder");
        
        // Verify Infrastructure project structure
        var infraProject = Path.Combine(solutionDirectory, "DistributedQRClipboard.Infrastructure");
        Assert.True(Directory.Exists(Path.Combine(infraProject, "Caching")), "Infrastructure should have Caching folder");
        Assert.True(Directory.Exists(Path.Combine(infraProject, "QrCode")), "Infrastructure should have QrCode folder");
        Assert.True(Directory.Exists(Path.Combine(infraProject, "Configuration")), "Infrastructure should have Configuration folder");
        
        // Verify API project structure
        var apiProject = Path.Combine(solutionDirectory, "DistributedQRClipboard.Api");
        Assert.True(Directory.Exists(Path.Combine(apiProject, "Endpoints")), "API should have Endpoints folder");
        Assert.True(Directory.Exists(Path.Combine(apiProject, "Hubs")), "API should have Hubs folder");
        Assert.True(Directory.Exists(Path.Combine(apiProject, "wwwroot")), "API should have wwwroot folder");
    }

    private static string GetSolutionDirectory()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var directory = new DirectoryInfo(currentDirectory);
        
        while (directory != null && !directory.GetFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }
        
        return directory?.FullName ?? throw new InvalidOperationException("Solution directory not found");
    }
}

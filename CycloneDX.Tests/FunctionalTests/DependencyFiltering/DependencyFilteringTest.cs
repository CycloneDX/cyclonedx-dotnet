using System.IO;
using System.Threading.Tasks;
using CycloneDX.Models;
using Xunit;

namespace CycloneDX.Tests.FunctionalTests.DependencyFiltering;

public class DependencyFilteringTest
{
    /// <summary>
    /// One package (with transitive dependencies) should be excluded from the resulting BOM.
    /// </summary>
    [Fact]
    public async Task TestExcludeDependency()
    {
        var assetContents =
            await File.ReadAllTextAsync(Path.Combine("FunctionalTests", "DependencyFiltering", "netstandard.assets.json"));

        var options = new RunOptions
        {
            DependencyExcludeFilter = "NETStandard.Library@1.6.0", json = true
        };
        var bom = await FunctionalTestHelper.Test(assetContents, options);

        Assert.Equal(2, bom.Components.Count);
        Assert.Equal("Antlr3.Runtime", bom.Components[0].Name);
        Assert.Equal("3.5.1", bom.Components[0].Version);
        Assert.Equal("NLog", bom.Components[1].Name);
        Assert.Equal("5.4.0", bom.Components[1].Version);
    }

    /// <summary>
    /// It is possible to exclude multiple packages by separating package names with a comma.
    /// </summary>
    [Fact]
    public async Task TestExcludeMultipleDependencies()
    {
        var assetContents =
            await File.ReadAllTextAsync(Path.Combine("FunctionalTests", "DependencyFiltering", "netstandard.assets.json"));

        var options = new RunOptions
        {
            DependencyExcludeFilter = "NETStandard.Library@1.6.0,NLog@5.4.0", json = true
        };
        var bom = await FunctionalTestHelper.Test(assetContents, options);

        Assert.Single(bom.Components);
        Assert.Equal("Antlr3.Runtime", bom.Components[0].Name);
        Assert.Equal("3.5.1", bom.Components[0].Version);
    }

    /// <summary>
    /// An exclude filter containing a package name without a version number is not supported.
    /// </summary>
    [Fact]
    public async Task TestExceptionForMissingVersion()
    {
        var assetContents =
            await File.ReadAllTextAsync(Path.Combine("FunctionalTests", "DependencyFiltering", "netstandard.assets.json"));

        var options = new RunOptions
        {
            DependencyExcludeFilter = "NETStandard.Library", json = true
        };
        var bom = await FunctionalTestHelper.Test(assetContents, options, ExitCode.InvalidOptions);
        Assert.Null(bom);
    }

    /// <summary>
    /// In this test, there are package references to Microsoft.Extensions.Configuration.Abstractions and
    /// Microsoft.Extensions.FileProviders.Physical which both have a transitive dependency to
    /// Microsoft.Extensions.Primitives. We have to make sure, the transitive dependency
    /// Microsoft.Extensions.Primitives still exists after excluding Microsoft.Extensions.Configuration.Abstractions.
    /// </summary>
    [Fact]
    public async Task DoNotRemoveSharedDependency()
    {
        var assetContents =
            await File.ReadAllTextAsync(Path.Combine("FunctionalTests", "DependencyFiltering", "shared_dependency.assets.json"));

        var options = new RunOptions
        {
            DependencyExcludeFilter = "Microsoft.Extensions.Configuration.Abstractions@9.0.4", json = true
        };
        var bom = await FunctionalTestHelper.Test(assetContents, options);

        Assert.Equal(4, bom.Components.Count);
        Assert.Contains(bom.Components, x => x.Name == "Microsoft.Extensions.Primitives" && x.Version == "9.0.4");
    }
}

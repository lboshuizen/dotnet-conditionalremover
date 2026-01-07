namespace Net8ConditionalRemover.Tests.Models;

using Net8ConditionalRemover.Models;
using Xunit;

public class ProcessingOptionsTests
{
    [Fact]
    public void DefaultTargetSymbol_IsNet8()
    {
        var options = new ProcessingOptions();

        Assert.Equal("NET8_0_OR_GREATER", options.TargetSymbol);
    }

    [Fact]
    public void PreprocessorSymbols_IncludesTargetSymbol()
    {
        var options = new ProcessingOptions { TargetSymbol = "NET9_0_OR_GREATER" };

        Assert.Contains("NET9_0_OR_GREATER", options.PreprocessorSymbols);
    }

    [Fact]
    public void PreprocessorSymbols_IncludesAdditionalDefines()
    {
        var options = new ProcessingOptions
        {
            TargetSymbol = "NET8_0_OR_GREATER",
            AdditionalDefines = ["DEBUG", "TRACE"]
        };

        Assert.Contains("NET8_0_OR_GREATER", options.PreprocessorSymbols);
        Assert.Contains("DEBUG", options.PreprocessorSymbols);
        Assert.Contains("TRACE", options.PreprocessorSymbols);
    }

    [Fact]
    public void TargetSymbolsWithAliases_IncludesUnderscoreVariant()
    {
        var options = new ProcessingOptions { TargetSymbol = "NET8_0_OR_GREATER" };

        var aliases = options.TargetSymbolsWithAliases;

        Assert.Contains("NET8_0_OR_GREATER", aliases);
        Assert.Contains("NET_8_0_OR_GREATER", aliases);
    }

    [Fact]
    public void TargetSymbolsWithAliases_HandlesNet9()
    {
        var options = new ProcessingOptions { TargetSymbol = "NET9_0_OR_GREATER" };

        var aliases = options.TargetSymbolsWithAliases;

        Assert.Contains("NET9_0_OR_GREATER", aliases);
        Assert.Contains("NET_9_0_OR_GREATER", aliases);
    }

    [Fact]
    public void TargetSymbolsWithAliases_NoDuplicates()
    {
        var options = new ProcessingOptions { TargetSymbol = "NET8_0_OR_GREATER" };

        var aliases = options.TargetSymbolsWithAliases;

        Assert.Equal(aliases.Distinct().Count(), aliases.Length);
    }
}

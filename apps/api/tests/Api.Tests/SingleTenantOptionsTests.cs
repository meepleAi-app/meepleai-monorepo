using Api.Services;
using Xunit;

namespace Api.Tests;

public class SingleTenantOptionsTests
{
    [Fact]
    public void GetTenantId_ReturnsDefault_WhenUnset()
    {
        var options = new SingleTenantOptions();

        Assert.Equal("meepleai", options.GetTenantId());
    }

    [Fact]
    public void GetTenantId_TrimsAndUsesProvidedValue()
    {
        var options = new SingleTenantOptions
        {
            TenantId = "  custom-tenant  "
        };

        Assert.Equal("custom-tenant", options.GetTenantId());
    }

    [Fact]
    public void GetTenantName_FallsBackToTenantId()
    {
        var options = new SingleTenantOptions
        {
            TenantId = "single"
        };

        Assert.Equal("single", options.GetTenantName());
    }

    [Fact]
    public void GetTenantName_UsesConfiguredName()
    {
        var options = new SingleTenantOptions
        {
            TenantId = "single",
            TenantName = "MeepleAI"
        };

        Assert.Equal("MeepleAI", options.GetTenantName());
    }
}

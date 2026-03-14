using Api.Infrastructure.Seeders;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

public sealed class SeedContextTests
{
    [Fact]
    public void SeedContext_StoresAllProperties()
    {
        var profile = SeedProfile.Dev;
        var userId = Guid.NewGuid();
        var services = Substitute.For<IServiceProvider>();
        var logger = NullLogger.Instance;

        var ctx = new SeedContext(profile, null!, services, logger, userId);

        Assert.Equal(SeedProfile.Dev, ctx.Profile);
        Assert.Equal(userId, ctx.SystemUserId);
        Assert.Same(services, ctx.Services);
    }

    [Theory]
    [InlineData(SeedProfile.None, 0)]
    [InlineData(SeedProfile.Prod, 1)]
    [InlineData(SeedProfile.Staging, 2)]
    [InlineData(SeedProfile.Dev, 3)]
    public void SeedProfile_HasCorrectOrdinalValues(SeedProfile profile, int expected)
    {
        Assert.Equal(expected, (int)profile);
    }
}

using System.Collections.Generic;
using Api.DevTools;
using Xunit;

namespace Api.Tests.DevTools;

public interface ITestService
{
    string Greet(string name);
    int Add(int a, int b);
}

internal sealed class RealTestService : ITestService
{
    public string Greet(string name) => $"Hello, {name}! (real)";
    public int Add(int a, int b) => a + b;
}

internal sealed class MockTestService : ITestService
{
    public string Greet(string name) => $"Hello, {name}! (mock)";
    public int Add(int a, int b) => 42;
}

public class MockAwareProxyTests
{
    private static MockToggleStateProvider MakeProvider(bool mocked)
    {
        var env = new Dictionary<string, string?>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["MOCK_TEST"] = mocked ? "true" : "false"
        };
        return new MockToggleStateProvider(env, new[] { "test" });
    }

    [Fact]
    public void DispatchesToRealWhenNotMocked()
    {
        var provider = MakeProvider(false);
        var proxy = MockAwareProxy<ITestService>.Create(
            new RealTestService(), new MockTestService(), provider, "test");
        Assert.Equal("Hello, world! (real)", proxy.Greet("world"));
        Assert.Equal(5, proxy.Add(2, 3));
    }

    [Fact]
    public void DispatchesToMockWhenMocked()
    {
        var provider = MakeProvider(true);
        var proxy = MockAwareProxy<ITestService>.Create(
            new RealTestService(), new MockTestService(), provider, "test");
        Assert.Equal("Hello, world! (mock)", proxy.Greet("world"));
        Assert.Equal(42, proxy.Add(2, 3));
    }

    [Fact]
    public void RuntimeSwitchTakesEffectImmediately()
    {
        var provider = MakeProvider(false);
        var proxy = MockAwareProxy<ITestService>.Create(
            new RealTestService(), new MockTestService(), provider, "test");
        Assert.Equal("Hello, world! (real)", proxy.Greet("world"));

        provider.Set("test", true);
        Assert.Equal("Hello, world! (mock)", proxy.Greet("world"));

        provider.Set("test", false);
        Assert.Equal("Hello, world! (real)", proxy.Greet("world"));
    }
}

using System;
using System.Collections.Generic;
using Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Qdrant.Client;
using Xunit;

namespace Api.Tests;

public class QdrantClientAdapterTests
{
    [Fact]
    public void Constructor_UsesPortFromUrl_WhenProvided()
    {
        string? capturedHost = null;
        int? capturedPort = null;
        bool? capturedUseHttps = null;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QDRANT_URL"] = "http://example.com:7654"
            })
            .Build();

        _ = new QdrantClientAdapter(
            configuration,
            NullLogger<QdrantClientAdapter>.Instance,
            (host, port, useHttps) =>
            {
                capturedHost = host;
                capturedPort = port;
                capturedUseHttps = useHttps;
                return new QdrantClient(host, port, useHttps);
            });

        Assert.Equal("example.com", capturedHost);
        Assert.Equal(7654, capturedPort);
        Assert.False(capturedUseHttps);
    }

    [Fact]
    public void Constructor_UsesGrpcPortOverride_WhenProvided()
    {
        int? capturedPort = null;
        bool? capturedUseHttps = null;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QDRANT_URL"] = "https://example.com",
                ["QDRANT_GRPC_PORT"] = "7777"
            })
            .Build();

        _ = new QdrantClientAdapter(
            configuration,
            NullLogger<QdrantClientAdapter>.Instance,
            (_, port, useHttps) =>
            {
                capturedPort = port;
                capturedUseHttps = useHttps;
                return new QdrantClient("localhost", 1, false);
            });

        Assert.Equal(7777, capturedPort);
        Assert.True(capturedUseHttps);
    }

    [Fact]
    public void Constructor_DefaultsToGrpcPort_WhenRestPortUsed()
    {
        int? capturedPort = null;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["QDRANT_URL"] = "http://example.com:6333"
            })
            .Build();

        _ = new QdrantClientAdapter(
            configuration,
            NullLogger<QdrantClientAdapter>.Instance,
            (_, port, _) =>
            {
                capturedPort = port;
                return new QdrantClient("localhost", 1, false);
            });

        Assert.Equal(6334, capturedPort);
    }
}

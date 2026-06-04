using System.Net;
using System.Text;
using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Issue #1903 M7.1 — unit tests for <see cref="BggTosWatcherJob"/>. Drive the
/// internal <c>RunOnceAsync</c> hook directly so we don't need a Quartz
/// scheduler. Pattern mirrors <c>BackfillPdfCoversJobTests</c>: InMemory EF
/// for the singleton row + <see cref="HttpMessageHandler"/> mock for HTTP.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class BggTosWatcherJobTests : IDisposable
{
    private readonly MeepleAiDbContext _db;

    public BggTosWatcherJobTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"BggTosWatcherJob_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);
    }

    public void Dispose() => _db.Dispose();

    private static BggTosWatcherJob CreateJob() =>
        new(serviceProvider: new Mock<IServiceProvider>().Object,
            NullLogger<BggTosWatcherJob>.Instance);

    private static IHttpClientFactory MakeHttpFactory(
        HttpStatusCode status,
        string body,
        Action<HttpRequestMessage>? capture = null)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capture?.Invoke(req))
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/html"),
            });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
               .Returns(() => new HttpClient(handler.Object));
        return factory.Object;
    }

    private static IHttpClientFactory MakeThrowingFactory(Exception exception)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(exception);

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>()))
               .Returns(() => new HttpClient(handler.Object));
        return factory.Object;
    }

    // ---------------------------------------------------------------------
    // ComputeSha256Hex unit tests
    // ---------------------------------------------------------------------

    [Fact]
    public void ComputeSha256Hex_KnownInput_ReturnsExpectedDigest()
    {
        // "abc" SHA-256 hash (well-known RFC test vector).
        const string Expected = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";
        BggTosWatcherJob.ComputeSha256Hex("abc").Should().Be(Expected);
    }

    [Fact]
    public void ComputeSha256Hex_DifferentContent_ProducesDifferentHash()
    {
        var a = BggTosWatcherJob.ComputeSha256Hex("Version 1 of the BGG ToS");
        var b = BggTosWatcherJob.ComputeSha256Hex("Version 2 of the BGG ToS");
        a.Should().NotBe(b);
        a.Should().HaveLength(64);
        b.Should().HaveLength(64);
    }

    // ---------------------------------------------------------------------
    // RunOnceAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task RunOnceAsync_FirstRun_NoExistingRow_RecordsInitialHash()
    {
        const string TosHtml = "<html><body>Initial BGG ToS contents</body></html>";
        var factory = MakeHttpFactory(HttpStatusCode.OK, TosHtml);

        await CreateJob().RunOnceAsync(_db, factory, default);

        var row = await _db.BggTosHashes.SingleAsync();
        row.Id.Should().Be(BggTosHashEntity.SingletonId);
        row.CurrentHash.Should().Be(BggTosWatcherJob.ComputeSha256Hex(TosHtml));
        row.ChangeCount.Should().Be(0);
        row.LastChangedAt.Should().BeNull("first observation is not a change");
        row.LastCheckedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RunOnceAsync_SameHash_OnlyUpdatesLastCheckedAt()
    {
        const string TosHtml = "<html>Same content twice</html>";
        var hash = BggTosWatcherJob.ComputeSha256Hex(TosHtml);
        var initialCheck = DateTime.UtcNow.AddDays(-30);

        _db.BggTosHashes.Add(new BggTosHashEntity
        {
            Id = BggTosHashEntity.SingletonId,
            CurrentHash = hash,
            LastCheckedAt = initialCheck,
            LastChangedAt = null,
            ChangeCount = 0,
        });
        await _db.SaveChangesAsync();

        var factory = MakeHttpFactory(HttpStatusCode.OK, TosHtml);
        await CreateJob().RunOnceAsync(_db, factory, default);

        var row = await _db.BggTosHashes.SingleAsync();
        row.CurrentHash.Should().Be(hash, "the hash didn't change");
        row.ChangeCount.Should().Be(0, "no change observed");
        row.LastChangedAt.Should().BeNull();
        row.LastCheckedAt.Should().BeAfter(initialCheck, "we just checked");
    }

    [Fact]
    public async Task RunOnceAsync_HashChanged_UpdatesRow_IncrementsChangeCount_SetsLastChangedAt()
    {
        var oldHash = BggTosWatcherJob.ComputeSha256Hex("old content");
        _db.BggTosHashes.Add(new BggTosHashEntity
        {
            Id = BggTosHashEntity.SingletonId,
            CurrentHash = oldHash,
            LastCheckedAt = DateTime.UtcNow.AddDays(-30),
            ChangeCount = 0,
        });
        await _db.SaveChangesAsync();

        const string NewHtml = "<html>new content</html>";
        var factory = MakeHttpFactory(HttpStatusCode.OK, NewHtml);
        await CreateJob().RunOnceAsync(_db, factory, default);

        var row = await _db.BggTosHashes.SingleAsync();
        row.CurrentHash.Should().Be(BggTosWatcherJob.ComputeSha256Hex(NewHtml));
        row.CurrentHash.Should().NotBe(oldHash);
        row.ChangeCount.Should().Be(1, "exactly one change observed");
        row.LastChangedAt.Should().NotBeNull();
        row.LastChangedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RunOnceAsync_HashChangedMultipleTimes_AccumulatesChangeCount()
    {
        _db.BggTosHashes.Add(new BggTosHashEntity
        {
            Id = BggTosHashEntity.SingletonId,
            CurrentHash = BggTosWatcherJob.ComputeSha256Hex("v1"),
            LastCheckedAt = DateTime.UtcNow.AddDays(-60),
            ChangeCount = 0,
        });
        await _db.SaveChangesAsync();

        // First change: v1 → v2
        await CreateJob().RunOnceAsync(_db, MakeHttpFactory(HttpStatusCode.OK, "v2"), default);
        // Second change: v2 → v3
        await CreateJob().RunOnceAsync(_db, MakeHttpFactory(HttpStatusCode.OK, "v3"), default);

        var row = await _db.BggTosHashes.SingleAsync();
        row.ChangeCount.Should().Be(2, "two distinct changes observed");
        row.CurrentHash.Should().Be(BggTosWatcherJob.ComputeSha256Hex("v3"));
    }

    [Fact]
    public async Task RunOnceAsync_HttpThrows_LogsAndCompletes_NoRowWritten()
    {
        var factory = MakeThrowingFactory(new HttpRequestException("connection refused"));

        // Should NOT throw; the watcher swallows arbitrary HTTP failures.
        var act = async () => await CreateJob().RunOnceAsync(_db, factory, default);
        await act.Should().NotThrowAsync();

        _db.BggTosHashes.Count().Should().Be(0, "no row should be written when HTTP fails");
    }

    [Fact]
    public async Task RunOnceAsync_CancellationDuringHttp_PropagatesOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var factory = MakeThrowingFactory(new OperationCanceledException(cts.Token));

        var act = async () => await CreateJob().RunOnceAsync(_db, factory, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunOnceAsync_SetsUserAgentHeader_PerBggAbuseContact()
    {
        HttpRequestMessage? captured = null;
        var factory = MakeHttpFactory(HttpStatusCode.OK, "ok", req => captured = req);

        await CreateJob().RunOnceAsync(_db, factory, default);

        captured.Should().NotBeNull();
        var ua = captured!.Headers.UserAgent.ToString();
        ua.Should().Contain("MeepleAI", "BGG ToS mandates a User-Agent with abuse contact");
        ua.Should().Contain("abuse@meepleai.app");
    }
}

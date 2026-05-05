using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Net;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Unit tests for SmoldoclingPhotoPreprocessor — HTTP client adapter to /api/v1/preprocess.
/// Libro Game AI Assistant MVP Phase 1 — Task 1.4b.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public class SmoldoclingPhotoPreprocessorTests
{
    [Fact]
    public async Task PreprocessAsync_ServiceReturnsHighConfidence_ParsesCorrectly()
    {
        // Arrange
        var stubResponse = """
        {
          "processed_image_base64": "dGVzdA==",
          "extracted_text": "Hello world",
          "confidence": 0.92,
          "orientation": "portrait",
          "is_blank": false,
          "warnings": []
        }
        """;
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, stubResponse);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("smoldocling-photo-preprocessor").Returns(httpClient);

        var preprocessor = new SmoldoclingPhotoPreprocessor(factory, NullLogger<SmoldoclingPhotoPreprocessor>.Instance);

        // Act
        var result = await preprocessor.PreprocessAsync(new byte[] { 0x01, 0x02 });

        // Assert
        result.ConfidenceScore.Should().Be(0.92);
        result.ExtractedText.Should().Be("Hello world");
        result.IsBlankPage.Should().BeFalse();
        result.DetectedOrientation.Should().Be(PageOrientation.Portrait);
        result.Warnings.Should().BeEmpty();
        handler.LastRequestUri.Should().EndWith("/api/v1/preprocess");
    }

    [Fact]
    public async Task PreprocessAsync_ServiceReturns500_ThrowsHttpRequestException()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "{}");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("smoldocling-photo-preprocessor").Returns(httpClient);

        var preprocessor = new SmoldoclingPhotoPreprocessor(factory, NullLogger<SmoldoclingPhotoPreprocessor>.Instance);

        // Act
        Func<Task> act = () => preprocessor.PreprocessAsync(new byte[] { 0x01 });

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task PreprocessAsync_ServiceReturnsBlankPage_MapsCorrectly()
    {
        // Arrange
        var stubResponse = """
        {
          "processed_image_base64": "AA==",
          "extracted_text": "",
          "confidence": 0.0,
          "orientation": "unknown",
          "is_blank": true,
          "warnings": ["Page appears blank"]
        }
        """;
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, stubResponse);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("smoldocling-photo-preprocessor").Returns(httpClient);

        var preprocessor = new SmoldoclingPhotoPreprocessor(factory, NullLogger<SmoldoclingPhotoPreprocessor>.Instance);

        // Act
        var result = await preprocessor.PreprocessAsync(new byte[] { 0x00 });

        // Assert
        result.IsBlankPage.Should().BeTrue();
        result.DetectedOrientation.Should().Be(PageOrientation.Unknown);
        result.Warnings.Should().ContainSingle().Which.Should().Be("Page appears blank");
    }

    // ── Test helper ─────────────────────────────────────────────────────────────

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _content;

        public string? LastRequestUri { get; private set; }

        public StubHttpMessageHandler(HttpStatusCode status, string content)
        {
            _status = status;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_content, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}

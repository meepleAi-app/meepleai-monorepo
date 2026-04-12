using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Api.DevTools.MockImpls;
using Xunit;

namespace Api.Tests.DevTools;

public class MockN8nTemplateServiceTests
{
    private static MockN8nTemplateService MakeService() => new();

    // --- GetTemplatesAsync ---

    [Fact]
    public async Task GetTemplatesAsync_NoCategory_ReturnsEmptyList()
    {
        var svc = MakeService();
        var result = await svc.GetTemplatesAsync(category: null, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTemplatesAsync_WithCategory_ReturnsEmptyList()
    {
        var svc = MakeService();
        var result = await svc.GetTemplatesAsync(category: "automation", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // --- GetTemplateAsync ---

    [Fact]
    public async Task GetTemplateAsync_AnyId_ReturnsNull()
    {
        var svc = MakeService();
        var result = await svc.GetTemplateAsync("template-001", CancellationToken.None);
        Assert.Null(result);
    }

    // --- ImportTemplateAsync ---

    [Fact]
    public async Task ImportTemplateAsync_ReturnsSuccessResponse()
    {
        var svc = MakeService();
        var parameters = new Dictionary<string, string>
        {
            ["webhookUrl"] = "https://example.com/hook",
            ["userId"] = "user-123"
        };

        var result = await svc.ImportTemplateAsync(
            templateId: "template-001",
            parameters: parameters,
            userId: "user-123",
            ct: CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotEmpty(result.WorkflowId);
        Assert.NotEmpty(result.Message);
    }

    [Fact]
    public async Task ImportTemplateAsync_EmptyParameters_ReturnsSuccessResponse()
    {
        var svc = MakeService();
        var result = await svc.ImportTemplateAsync(
            templateId: "template-empty",
            parameters: new Dictionary<string, string>(),
            userId: "user-abc",
            ct: CancellationToken.None);

        Assert.NotNull(result);
        Assert.NotEmpty(result.WorkflowId);
    }

    // --- ValidateTemplate ---

    [Fact]
    public void ValidateTemplate_ValidJson_ReturnsValid()
    {
        var svc = MakeService();
        var result = svc.ValidateTemplate("{\"nodes\":[]}");
        Assert.True(result.IsValid);
        Assert.Null(result.Errors);
    }

    [Fact]
    public void ValidateTemplate_EmptyString_ReturnsValid()
    {
        var svc = MakeService();
        var result = svc.ValidateTemplate(string.Empty);
        Assert.True(result.IsValid);
        Assert.Null(result.Errors);
    }
}

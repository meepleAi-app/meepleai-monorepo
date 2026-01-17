using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.UserLibrary.Domain;

/// <summary>
/// Unit tests for UserLibraryEntry entity domain logic.
/// </summary>
public class UserLibraryEntryTests
{
    [Fact]
    public void ConfigureAgent_ShouldSetCustomAgentConfig()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var agentConfig = AgentConfiguration.Create(
            llmModel: "google/gemini-pro",
            temperature: 0.7,
            maxTokens: 4096,
            personality: "Amichevole",
            detailLevel: "Normale"
        );

        // Act
        entry.ConfigureAgent(agentConfig);

        // Assert
        entry.CustomAgentConfig.Should().NotBeNull();
        entry.CustomAgentConfig!.LlmModel.Should().Be("google/gemini-pro");
        entry.CustomAgentConfig.Temperature.Should().Be(0.7);
        entry.HasCustomAgent().Should().BeTrue();
    }

    [Fact]
    public void ResetAgentToDefault_ShouldClearCustomAgentConfig()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var agentConfig = AgentConfiguration.CreateDefault();
        entry.ConfigureAgent(agentConfig);

        // Act
        entry.ResetAgentToDefault();

        // Assert
        entry.CustomAgentConfig.Should().BeNull();
        entry.HasCustomAgent().Should().BeFalse();
    }

    [Fact]
    public void UploadCustomPdf_ShouldSetCustomPdfMetadata()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var pdfMetadata = CustomPdfMetadata.Create(
            url: "https://storage.example.com/pdfs/custom-rulebook.pdf",
            fileSizeBytes: 5_000_000,
            originalFileName: "custom-rulebook.pdf"
        );

        // Act
        entry.UploadCustomPdf(pdfMetadata);

        // Assert
        entry.CustomPdfMetadata.Should().NotBeNull();
        entry.CustomPdfMetadata!.Url.Should().Be("https://storage.example.com/pdfs/custom-rulebook.pdf");
        entry.CustomPdfMetadata.FileSizeBytes.Should().Be(5_000_000);
        entry.HasCustomPdf().Should().BeTrue();
    }

    [Fact]
    public void ResetPdfToShared_ShouldClearCustomPdfMetadata()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var pdfMetadata = CustomPdfMetadata.Create(
            url: "https://storage.example.com/pdfs/custom.pdf",
            fileSizeBytes: 1_000_000,
            originalFileName: "custom.pdf"
        );
        entry.UploadCustomPdf(pdfMetadata);

        // Act
        entry.ResetPdfToShared();

        // Assert
        entry.CustomPdfMetadata.Should().BeNull();
        entry.HasCustomPdf().Should().BeFalse();
    }

    [Fact]
    public void ConfigureAgent_WithNullAgentConfig_ShouldThrowArgumentNullException()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var act = () => entry.ConfigureAgent(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UploadCustomPdf_WithNullPdfMetadata_ShouldThrowArgumentNullException()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        var act = () => entry.UploadCustomPdf(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void HasCustomAgent_WhenAgentConfigured_ShouldReturnTrue()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.ConfigureAgent(AgentConfiguration.CreateDefault());

        // Act & Assert
        entry.HasCustomAgent().Should().BeTrue();
    }

    [Fact]
    public void HasCustomAgent_WhenNoAgentConfigured_ShouldReturnFalse()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        entry.HasCustomAgent().Should().BeFalse();
    }

    [Fact]
    public void HasCustomPdf_WhenPdfUploaded_ShouldReturnTrue()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var pdf = CustomPdfMetadata.Create("https://test.com/pdf", 1000, "test.pdf");
        entry.UploadCustomPdf(pdf);

        // Act & Assert
        entry.HasCustomPdf().Should().BeTrue();
    }

    [Fact]
    public void HasCustomPdf_WhenNoPdfUploaded_ShouldReturnFalse()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act & Assert
        entry.HasCustomPdf().Should().BeFalse();
    }
}

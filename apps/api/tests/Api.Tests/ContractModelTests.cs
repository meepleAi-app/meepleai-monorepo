using Api.Models;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Tests for contract/DTO model classes
/// </summary>
public class ContractModelTests
{
    #region IngestPdfResponse Tests

    [Fact]
    public void IngestPdfResponse_Constructor_SetsJobId()
    {
        // Act
        var response = new IngestPdfResponse("job-123");

        // Assert
        Assert.Equal("job-123", response.jobId);
    }

    [Fact]
    public void IngestPdfResponse_Equality_WorksForRecords()
    {
        // Arrange
        var response1 = new IngestPdfResponse("job-123");
        var response2 = new IngestPdfResponse("job-123");
        var response3 = new IngestPdfResponse("job-456");

        // Assert
        Assert.Equal(response1, response2);
        Assert.NotEqual(response1, response3);
    }

    [Fact]
    public void IngestPdfResponse_WithExpression_CreatesNewInstance()
    {
        // Arrange
        var original = new IngestPdfResponse("job-123");

        // Act
        var modified = original with { jobId = "job-456" };

        // Assert
        Assert.Equal("job-456", modified.jobId);
        Assert.Equal("job-123", original.jobId); // Original unchanged
    }

    [Fact]
    public void IngestPdfResponse_ToString_ContainsJobId()
    {
        // Arrange
        var response = new IngestPdfResponse("job-789");

        // Act
        var stringRepresentation = response.ToString();

        // Assert
        Assert.Contains("job-789", stringRepresentation);
    }

    #endregion
}

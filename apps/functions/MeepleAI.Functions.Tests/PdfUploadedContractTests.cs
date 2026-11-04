using FluentAssertions;
using MeepleAI.Functions.Contracts;
using Xunit;

namespace MeepleAI.Functions.Tests;

public sealed class PdfUploadedContractTests
{
    [Fact]
    public void TryParse_SharedFixture_IsValid()
    {
        var result = PdfUploadedContract.TryParse(BinaryData.FromString(ContractFixture.ReadV1()));

        result.IsValid.Should().BeTrue(result.Detail);
        result.Event!.ContractVersion.Should().Be("v1");
        result.Event.SharedGameId.Should().BeNull();
        result.Event.FilePath.Should().EndWith("_rulebook.pdf");
    }

    [Fact]
    public void TryParse_UnsupportedVersion_IsRejected()
    {
        var json = ContractFixture.ReadV1().Replace("\"v1\"", "\"v2\"", StringComparison.Ordinal);

        var result = PdfUploadedContract.TryParse(BinaryData.FromString(json));

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be(PdfUploadedContract.ReasonUnsupportedContract);
    }

    [Fact]
    public void TryParse_MalformedJson_IsRejected()
    {
        PdfUploadedContract.TryParse(BinaryData.FromString("{not json")).Reason.Should().Be(PdfUploadedContract.ReasonMalformed);
    }

    [Fact]
    public void TryParse_EmptyPdfId_IsRejected()
    {
        var json = ContractFixture.ReadV1().Replace("7b2d9e4a-1c3f-4a5b-9d8e-2f1a3b4c5d6e", Guid.Empty.ToString(), StringComparison.Ordinal);

        PdfUploadedContract.TryParse(BinaryData.FromString(json)).Reason.Should().Be(PdfUploadedContract.ReasonMissingPdfId);
    }
}

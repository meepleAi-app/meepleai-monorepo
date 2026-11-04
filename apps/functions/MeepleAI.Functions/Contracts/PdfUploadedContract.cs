using System.Text.Json;
using System.Text.Json.Serialization;

namespace MeepleAI.Functions.Contracts;

/// <summary>Contratto v1 di PdfUploaded, specchio di tests/fixtures/contracts/pdf-uploaded.v1.json.</summary>
public sealed record PdfUploadedEventV1(
    Guid EventId,
    DateTime OccurredAt,
    string SourceContext,
    string ContractVersion,
    Guid PdfDocumentId,
    Guid GameId,
    Guid? SharedGameId,
    string FilePath);

public sealed record ContractParseResult(PdfUploadedEventV1? Event, string? Reason, string? Detail)
{
    public bool IsValid => Event is not null;
}

public static class PdfUploadedContract
{
    public const string SupportedVersion = "v1";
    public const string ReasonMalformed = "malformed";
    public const string ReasonUnsupportedContract = "unsupported_contract";
    public const string ReasonMissingPdfId = "missing_pdf_id";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static ContractParseResult TryParse(BinaryData body)
    {
        PdfUploadedEventV1? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<PdfUploadedEventV1>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            return new ContractParseResult(null, ReasonMalformed, ex.Message);
        }

        if (parsed is null)
        {
            return new ContractParseResult(null, ReasonMalformed, "empty body");
        }

        if (!string.Equals(parsed.ContractVersion, SupportedVersion, StringComparison.Ordinal))
        {
            return new ContractParseResult(null, ReasonUnsupportedContract, $"contractVersion '{parsed.ContractVersion}' is not {SupportedVersion}");
        }

        if (parsed.PdfDocumentId == Guid.Empty)
        {
            return new ContractParseResult(null, ReasonMissingPdfId, "pdfDocumentId is empty");
        }

        return new ContractParseResult(parsed, null, null);
    }
}

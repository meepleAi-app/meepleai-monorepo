using System.Text.Json;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for EnqueueBggBatchFromJsonCommand
/// Issue #4352: Backend - Bulk Import JSON Command
/// </summary>
public class EnqueueBggBatchFromJsonCommandValidator : AbstractValidator<EnqueueBggBatchFromJsonCommand>
{
    private const int MaxJsonSizeBytes = 10 * 1024 * 1024; // 10MB

    public EnqueueBggBatchFromJsonCommandValidator()
    {
        RuleFor(x => x.JsonContent)
            .NotEmpty()
            .WithMessage("JSON content is required")
            .Must(NotExceedMaxSize)
            .WithMessage($"JSON content exceeds maximum size of {MaxJsonSizeBytes / 1024 / 1024}MB")
            .Must(BeValidJson)
            .WithMessage("Invalid JSON format")
            .Must(BeValidGameArray)
            .WithMessage("JSON must be an array of objects with 'bggId' (number) and 'name' (string) fields");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");
    }

    private static bool NotExceedMaxSize(string? json)
    {
        if (string.IsNullOrEmpty(json)) return true;
        return System.Text.Encoding.UTF8.GetByteCount(json) <= MaxJsonSizeBytes;
    }

    private static bool BeValidJson(string? json)
    {
        if (string.IsNullOrEmpty(json)) return false;

        try
        {
            JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool BeValidGameArray(string? json)
    {
        if (string.IsNullOrEmpty(json)) return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            // Must be an array
            if (root.ValueKind != JsonValueKind.Array)
                return false;

            // Check at least one element (non-empty array)
            if (root.GetArrayLength() == 0)
                return false;

            // Validate first element structure (sample check)
            var firstElement = root[0];
            return firstElement.TryGetProperty("bggId", out var bggId) &&
                   bggId.ValueKind == JsonValueKind.Number &&
                   firstElement.TryGetProperty("name", out var name) &&
                   name.ValueKind == JsonValueKind.String;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

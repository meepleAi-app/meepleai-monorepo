using System.Text.RegularExpressions;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

/// <summary>
/// Handles bulk invitation sending from CSV content.
/// Parses CSV rows and dispatches individual ProvisionAndInviteUserCommand per valid row.
/// Issue #124: User invitation system.
/// </summary>
internal sealed class BulkSendInvitationsCommandHandler
    : ICommandHandler<BulkSendInvitationsCommand, BulkInviteResponse>
{
    private const int MaxRows = 100;

    // Simple email validation regex for CSV parsing
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    private readonly ISender _sender;
    private readonly ILogger<BulkSendInvitationsCommandHandler> _logger;

    public BulkSendInvitationsCommandHandler(ISender sender, ILogger<BulkSendInvitationsCommandHandler> logger)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BulkInviteResponse> Handle(BulkSendInvitationsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var successful = new List<InvitationDto>();
        var failed = new List<BulkInviteFailure>();

        if (string.IsNullOrWhiteSpace(command.CsvContent))
            return new BulkInviteResponse(successful, failed);

        var lines = command.CsvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Skip header row if present
        var startIndex = 0;
        if (lines.Length > 0)
        {
            var firstLine = lines[0].Trim().ToLowerInvariant();
            if (firstLine.Contains("email", StringComparison.Ordinal) && firstLine.Contains("role", StringComparison.Ordinal))
                startIndex = 1;
        }

        var dataLines = lines.Skip(startIndex).ToList();

        // Enforce max limit
        if (dataLines.Count > MaxRows)
            throw new ValidationException($"CSV exceeds maximum of {MaxRows} rows. Found {dataLines.Count} rows.");

        foreach (var line in dataLines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            var parts = trimmedLine.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                failed.Add(new BulkInviteFailure(trimmedLine, "Invalid CSV format: expected email,role[,displayName,tier,expiresInDays]"));
                continue;
            }

            var email = parts[0].Trim();
            var role = parts[1].Trim();

            // Validate email format
            if (!EmailRegex.IsMatch(email))
            {
                failed.Add(new BulkInviteFailure(email, "Invalid email format"));
                continue;
            }

            // Optional CSV columns: displayName (col 2), tier (col 3), expiresInDays (col 4)
            var displayName = parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]) ? parts[2].Trim() : email;
            var tier = parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3].Trim() : "Free";
            var expiresInDays = 7;
            if (parts.Length > 4 && int.TryParse(parts[4].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var parsedDays) && parsedDays > 0)
                expiresInDays = parsedDays;

            try
            {
                var result = await _sender.Send(
                    new ProvisionAndInviteUserCommand(
                        Email: email,
                        DisplayName: displayName,
                        Role: role,
                        Tier: tier,
                        CustomMessage: null,
                        ExpiresInDays: expiresInDays,
                        GameSuggestions: new List<GameSuggestionDto>(),
                        InvitedByUserId: command.InvitedByUserId),
                    cancellationToken).ConfigureAwait(false);
                successful.Add(result);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to provision invitation for {Email}", email);
                var userFacingError = ex switch
                {
                    Api.Middleware.Exceptions.ConflictException => "A pending invitation or user already exists for this email",
                    ArgumentException ae => ae.Message,
                    FluentValidation.ValidationException => "Invalid email or role",
                    _ => "Failed to send invitation"
                };
                failed.Add(new BulkInviteFailure(email, userFacingError));
            }
#pragma warning restore CA1031
        }

        return new BulkInviteResponse(successful, failed);
    }
}

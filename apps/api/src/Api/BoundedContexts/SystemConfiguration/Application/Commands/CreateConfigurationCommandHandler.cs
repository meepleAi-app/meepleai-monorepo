using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Exceptions;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

internal class CreateConfigurationCommandHandler : ICommandHandler<CreateConfigurationCommand, ConfigurationDto>
{
    private readonly IConfigurationRepository _configurationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateConfigurationCommandHandler(
        IConfigurationRepository configurationRepository,
        IUnitOfWork unitOfWork)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ConfigurationDto> Handle(CreateConfigurationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var key = new ConfigKey(command.Key);

        // #4612: una riga di questa chiave scritta dall'API nascerebbe con il claim del primo admin aperto.
        ReservedConfigurationKeyGuard.EnsureNotReserved(key.Value);

        var config = new Domain.Entities.SystemConfiguration(
            id: Guid.NewGuid(),
            key: key,
            value: command.Value,
            valueType: command.ValueType,
            createdByUserId: command.CreatedByUserId,
            description: command.Description,
            category: command.Category,
            environment: command.Environment,
            requiresRestart: command.RequiresRestart
        );

        await _configurationRepository.AddAsync(config, cancellationToken).ConfigureAwait(false);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException pg && IsKeyEnvironmentViolation(pg.SqlState, pg.ConstraintName))
        {
            // #5016: the index decides, not a lookup before the insert. A lookup would leave the window between
            // the read and the insert, in which two concurrent creations both pass it; the index has none.
            throw new ConfigurationKeyConflictException(config.Key.Value, config.Environment, ex);
        }

        return MapToDto(config);
    }

    /// <summary>
    /// Name of the unique index on <c>(Key, Environment)</c> of <c>system_configurations</c>: the one a second
    /// creation of the same key and environment trips. It is the default name EF gives the index declared in
    /// <c>SystemConfigurationEntityConfiguration</c>; the integration tests of #4599 and #5016 run against the
    /// real schema and fail if it is ever renamed.
    /// </summary>
    internal const string KeyEnvironmentIndexName = "IX_system_configurations_Key_Environment";

    /// <summary>
    /// True when the PostgreSQL error metadata identify the unique violation on
    /// <see cref="KeyEnvironmentIndexName"/>: SQLSTATE 23505 and that constraint name. Separate and pure so the
    /// matching can be tested without a database; same split as
    /// <c>NotificationDedupePipelineBehavior.IsNotificationDedupViolation</c>.
    /// </summary>
    internal static bool IsKeyEnvironmentViolation(string? sqlState, string? constraintName) =>
        string.Equals(sqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal)
        && string.Equals(constraintName, KeyEnvironmentIndexName, StringComparison.Ordinal);

    private static ConfigurationDto MapToDto(Domain.Entities.SystemConfiguration config)
    {
        return new ConfigurationDto(
            Id: config.Id,
            Key: config.Key.Value,
            Value: config.Value,
            ValueType: config.ValueType,
            Description: config.Description,
            Category: config.Category,
            IsActive: config.IsActive,
            RequiresRestart: config.RequiresRestart,
            Environment: config.Environment,
            Version: config.Version,
            CreatedAt: config.CreatedAt,
            UpdatedAt: config.UpdatedAt
        );
    }
}

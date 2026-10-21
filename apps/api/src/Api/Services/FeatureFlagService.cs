using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.Infrastructure.Entities;
using Api.Helpers;
using Api.Middleware;
using Api.Models;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Api.Services;

public static class FeatureFlagConstants
{
    public const string RagAuxModelKey = "rag.enhancement.aux-model";

    // R1 (issue #3416, ADR-088): gate approved mechanic-card claim injection into RAG answers. Default off.
    public const string MechanicCardInjectionKey = "rag.mechanic-card-injection";

    /// <summary>Orders injected Verified Rules by priority/override chain (spec 2026-10-08 §9, default off).</summary>
    public const string MechanicClaimsV3OrderingKey = "rag.mechanic-claims.v3-ordering";

    // #3390 Slice 2: route the in-session IMAGE agent path through RAG retrieval (grounded, with
    // citations) instead of the multimodal-only path. Default OFF (rollback-safe migration).
    public const string LiveImageRetrievalKey = "rag.live-image-retrieval";

    // #3390 Slice 3: when the in-session turn has no text, derive the retrieval query (and the
    // user-visible chat message) from the vision board-state. Default OFF.
    public const string LiveVisionQueryExpansionKey = "rag.live-vision-query-expansion";

    public static readonly string[] RagEnhancements =
    [
        "rag.enhancement.adaptive-routing",
        "rag.enhancement.crag-evaluation",
        "rag.enhancement.raptor-retrieval",
        "rag.enhancement.rag-fusion-queries",
        "rag.enhancement.graph-traversal"
    ];
}

/// <summary>
/// CONFIG-05: Feature flags service implementation.
/// Provides runtime feature toggling with role-based and tier-based access control.
/// Uses CQRS commands/queries for configuration management.
/// Issue #3073: Extended to support tier-based feature flags (Free/Normal/Premium).
/// </summary>
internal class FeatureFlagService : IFeatureFlagService
{
    private readonly IConfigurationService _configService;
    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FeatureFlagService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private const string FeatureFlagCategory = "FeatureFlags";
    private const string TierPrefix = "Tier";

    public FeatureFlagService(
        IConfigurationService configService,
        IMediator mediator,
        IWebHostEnvironment environment,
        ILogger<FeatureFlagService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _configService = configService;
        _mediator = mediator;
        _environment = environment;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Check if a feature is enabled with role-based hierarchy.
    /// Hierarchy: Role-specific flag > Global flag > Default false
    /// </summary>
    public async Task<bool> IsEnabledAsync(string featureName, UserRole? role = null)
    {
        // Try role-specific flag first (highest priority)
        if (role.HasValue)
        {
            var roleFlagKey = $"{featureName}.{role.Value}";
            var roleSpecificFlag = await _configService.GetValueAsync<bool?>(roleFlagKey, null).ConfigureAwait(false);

            if (roleSpecificFlag.HasValue)
            {
                _logger.LogDebug("Feature {FeatureName} for role {Role}: {Enabled} (role-specific)",
                    LogSanitizer.Sanitize(featureName), role.Value, roleSpecificFlag.Value);
                return roleSpecificFlag.Value;
            }
        }

        // Fall back to global flag
        var globalFlag = await _configService.GetValueAsync<bool?>(featureName, null).ConfigureAwait(false);

        if (globalFlag.HasValue)
        {
            _logger.LogDebug("Feature {FeatureName}: {Enabled} (global)", LogSanitizer.Sanitize(featureName), globalFlag.Value);
            return globalFlag.Value;
        }

        // Default: feature disabled (fail-safe)
        _logger.LogDebug("Feature {FeatureName}: false (default - not found)", LogSanitizer.Sanitize(featureName));
        return false;
    }

    /// <summary>
    /// Check if a feature is enabled for a specific tier.
    /// Hierarchy: Tier-specific flag > Global flag > Default true (for backward compatibility)
    /// </summary>
    public async Task<bool> IsEnabledForTierAsync(string featureName, UserTier tier)
    {
        ArgumentNullException.ThrowIfNull(tier);

        // Try tier-specific flag first (highest priority)
        var tierFlagKey = $"{featureName}.{TierPrefix}.{tier.Value}";
        var tierSpecificFlag = await _configService.GetValueAsync<bool?>(tierFlagKey, null).ConfigureAwait(false);

        if (tierSpecificFlag.HasValue)
        {
            _logger.LogDebug("Feature {FeatureName} for tier {Tier}: {Enabled} (tier-specific)",
                LogSanitizer.Sanitize(featureName), LogSanitizer.Sanitize(tier.Value), tierSpecificFlag.Value);
            return tierSpecificFlag.Value;
        }

        // Fall back to global flag
        var globalFlag = await _configService.GetValueAsync<bool?>(featureName, null).ConfigureAwait(false);

        if (globalFlag.HasValue)
        {
            _logger.LogDebug("Feature {FeatureName}: {Enabled} (global)", LogSanitizer.Sanitize(featureName), globalFlag.Value);
            return globalFlag.Value;
        }

        // Default: feature enabled for tier (backward compatibility - all tiers allowed by default)
        _logger.LogDebug("Feature {FeatureName} for tier {Tier}: true (default - not found)", LogSanitizer.Sanitize(featureName), LogSanitizer.Sanitize(tier.Value));
        return true;
    }

    /// <summary>
    /// Check if a user can access a feature based on both role AND tier.
    /// Combined logic: roleAccess AND tierAccess must both be true.
    /// </summary>
    public async Task<bool> CanAccessFeatureAsync(User user, string featureName)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrWhiteSpace(featureName))
            throw new ArgumentException("Feature name cannot be empty", nameof(featureName));

        // Admin users bypass all tier-based restrictions
        if (user.Role.HasPermission(Role.Admin))
        {
            _logger.LogDebug("Feature {FeatureName} granted for admin user {UserId} (tier bypass)",
                LogSanitizer.Sanitize(featureName), user.Id);
            return true;
        }

        // Map User.Role (ValueObject) to UserRole enum
        var userRole = MapRoleToUserRole(user.Role);

        // Check role-based access
        var roleAccess = await IsEnabledAsync(featureName, userRole).ConfigureAwait(false);
        if (!roleAccess)
        {
            _logger.LogDebug("Feature {FeatureName} denied for user {UserId}: role-based access denied",
                LogSanitizer.Sanitize(featureName), user.Id);
            return false;
        }

        // Check tier-based access
        var tierAccess = await IsEnabledForTierAsync(featureName, user.Tier).ConfigureAwait(false);
        if (!tierAccess)
        {
            _logger.LogDebug("Feature {FeatureName} denied for user {UserId}: tier-based access denied",
                LogSanitizer.Sanitize(featureName), user.Id);
            return false;
        }

        _logger.LogDebug("Feature {FeatureName} granted for user {UserId} (role={Role}, tier={Tier})",
            LogSanitizer.Sanitize(featureName), user.Id, user.Role, user.Tier);
        return true;
    }

    /// <summary>
    /// Maps Role value object to UserRole enum for feature flag checking.
    /// </summary>
    /// <remarks>
    /// #3873 — l'ordine e' dal ruolo piu' alto al piu' basso, e i cinque rami sono tutti
    /// espliciti perche' il fallback e' <see cref="UserRole.User"/>: prima, superadmin e
    /// creator ci finivano dentro. Per superadmin era l'opposto di quanto l'enum dichiara
    /// (SuperAdmin = «can manage other admins and global feature flags», Admin = «no global
    /// feature flags»), cioe' il ruolo proprietario dei flag globali veniva valutato come
    /// utente semplice.
    ///
    /// Il valore mappato diventa un pezzo di chiave di configurazione in
    /// <c>IsEnabledAsync</c> (<c>$"{featureName}.{role}"</c>), non un livello confrontato:
    /// aggiungere due valori non rompe nessun consumatore, e in assenza di una chiave
    /// per-ruolo si continua a cadere sul flag globale.
    /// </remarks>
    private static UserRole MapRoleToUserRole(Role role)
    {
        if (role.IsSuperAdmin()) return UserRole.SuperAdmin;
        if (role.IsExactlyAdmin()) return UserRole.Admin;
        if (role.IsEditor()) return UserRole.Editor;
        if (role.IsCreator()) return UserRole.Creator;
        return UserRole.User;
    }

    /// <summary>
    /// Creates the row of a flag and, if another request created it a moment earlier, applies the wanted
    /// value on top of that row instead (#4599).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The callers look the key up and create the row when there is none. Two requests on a key without a
    /// row both see «no row» and both create it; the unique index on <c>(Key, Environment)</c> fails the
    /// second with 23505, and that was a 500. The row now exists, so the request that lost does what a
    /// request arriving after the winner would have done: it updates the row to its own value.
    /// </para>
    /// <para>
    /// 🔴 The creation runs in <b>its own DI scope</b>. A failed <c>SaveChangesAsync</c> leaves, in the
    /// context that ran it, the row it tried to insert, the <c>domain_event_outbox</c> row of
    /// <c>ConfigurationCreatedEvent</c>, and the event itself still pending in the collector (the collector
    /// is emptied only after a successful save). Detaching the row is not enough, and it was tried: the next
    /// save in the same scope tracks the outbox row a second time (<c>InvalidOperationException</c>, same
    /// key), and the pending event would be enqueued again for a configuration that never existed. In a
    /// scope of its own the failed creation is disposed with everything it left behind, and the caller's
    /// context never sees it.
    /// </para>
    /// <para>
    /// ⚠️ The price is that the creation no longer shares the caller's unit of work: it commits on its own.
    /// <b>Do not call this under a transaction of the caller.</b> In particular, under <c>[AtomicAudit]</c>
    /// <c>AuditLoggingBehavior</c> opens the transaction on the caller's <c>DbContext</c>, and a row created
    /// here would survive its rollback. None of the commands that reach this service is <c>[AtomicAudit]</c>,
    /// and <c>FeatureFlagWritersAreNotAtomicAuditArchitectureTests</c> keeps it so.
    /// </para>
    /// <para>
    /// Only a unique violation on <see cref="KeyEnvironmentIndexName"/> that the re-read explains is
    /// recovered. If the key has no active row to update (for example the colliding row is inactive, and the
    /// lookup only sees active ones), the original exception is rethrown.
    /// </para>
    /// </remarks>
    private async Task CreateRowAsync(CreateConfigurationCommand create, string environmentName, Guid userGuid)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(create).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException pg && IsKeyEnvironmentViolation(pg.SqlState, pg.ConstraintName))
        {
            // The lookup that led here was stale: forget it and read what the winner wrote.
            await _configService.InvalidateAsync(create.Key, environmentName).ConfigureAwait(false);
            var winner = await _configService.GetConfigurationByKeyAsync(create.Key, environmentName).ConfigureAwait(false);

            if (winner is null || !Guid.TryParse(winner.Id, out var winnerId))
            {
                throw;
            }

            _logger.LogInformation(
                ex,
                "Feature flag {Key} was created by a concurrent request; applying the value {Value} on top of it",
                LogSanitizer.Sanitize(create.Key), LogSanitizer.Sanitize(create.Value));

            await _mediator.Send(new UpdateConfigValueCommand(
                ConfigId: winnerId,
                NewValue: create.Value,
                UpdatedByUserId: userGuid)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Name of the unique index on <c>(Key, Environment)</c> of <c>system_configurations</c>: the one a second
    /// creation of the same flag trips. It is the default name EF gives the index declared in
    /// <c>SystemConfigurationEntityConfiguration</c>; the integration tests of #4599 run against the real
    /// schema and fail if it is ever renamed.
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

    /// <summary>
    /// Enable a feature globally or for a specific role.
    /// </summary>
    public async Task EnableFeatureAsync(string featureName, UserRole? role = null, string? userId = null)
    {
        var key = role.HasValue ? $"{featureName}.{role.Value}" : featureName;

        // Bug #2162 (same pattern as #2116): pass the current environment to the
        // lookup so it stays symmetric with the CREATE branch below. Without it
        // the lookup falls back to _environment.EnvironmentName via the
        // ConfigurationService default, while the CREATE used to hardcode
        // "Production" — the asymmetry caused 23505 collisions in non-Production.
        var environmentName = _environment.EnvironmentName;
        var existing = await _configService.GetConfigurationByKeyAsync(key, environmentName).ConfigureAwait(false);

        var userGuid = userId != null && Guid.TryParse(userId, out var parsed) ? parsed : Guid.Empty;

        if (existing != null)
        {
            if (!Guid.TryParse(existing.Id, out var configId))
            {
                throw new InvalidOperationException($"Invalid configuration ID format: {existing.Id}");
            }

            // Update existing configuration
            var command = new UpdateConfigValueCommand(
                ConfigId: configId,
                NewValue: "true",
                UpdatedByUserId: userGuid);

            await _mediator.Send(command).ConfigureAwait(false);

            // #3844 — la lettura e' in cache, compreso il "non esiste": senza questa riga la
            // prossima chiamata riprende il valore vecchio e riscrive invece di aggiornare,
            // violando il vincolo di unicita' su (Key, Environment).
            await _configService.InvalidateAsync(key, environmentName).ConfigureAwait(false);

            _logger.LogInformation("Feature {FeatureName} enabled{RoleInfo} by {UserId}",
                LogSanitizer.Sanitize(featureName), role.HasValue ? $" for {role.Value}" : "", LogSanitizer.Sanitize(userId ?? "system"));
        }
        else
        {
            // Create new configuration
            var command = new CreateConfigurationCommand(
                Key: key,
                Value: "true",
                ValueType: "Boolean",
                CreatedByUserId: userGuid,
                Description: $"Feature flag: {featureName}",
                Category: FeatureFlagCategory,
                Environment: environmentName,
                RequiresRestart: false);

            await CreateRowAsync(command, environmentName, userGuid).ConfigureAwait(false);

            // #3844 — la lettura e' in cache, compreso il "non esiste": senza questa riga la
            // prossima chiamata riprende il valore vecchio e riscrive invece di aggiornare,
            // violando il vincolo di unicita' su (Key, Environment).
            await _configService.InvalidateAsync(key, environmentName).ConfigureAwait(false);

            _logger.LogInformation("Feature {FeatureName} created and enabled{RoleInfo} by {UserId}",
                LogSanitizer.Sanitize(featureName), role.HasValue ? $" for {role.Value}" : "", LogSanitizer.Sanitize(userId ?? "system"));
        }
    }

    /// <summary>
    /// Disable a feature globally or for a specific role.
    /// </summary>
    public async Task DisableFeatureAsync(string featureName, UserRole? role = null, string? userId = null)
    {
        var key = role.HasValue ? $"{featureName}.{role.Value}" : featureName;

        // Bug #2162: symmetric lookup + CREATE per environment. See EnableFeatureAsync.
        var environmentName = _environment.EnvironmentName;
        var existing = await _configService.GetConfigurationByKeyAsync(key, environmentName).ConfigureAwait(false);

        var userGuid = userId != null && Guid.TryParse(userId, out var parsed) ? parsed : Guid.Empty;

        if (existing != null)
        {
            if (!Guid.TryParse(existing.Id, out var configId))
            {
                throw new InvalidOperationException($"Invalid configuration ID format: {existing.Id}");
            }

            // Update existing configuration
            var command = new UpdateConfigValueCommand(
                ConfigId: configId,
                NewValue: "false",
                UpdatedByUserId: userGuid);

            await _mediator.Send(command).ConfigureAwait(false);

            // #3844 — la lettura e' in cache, compreso il "non esiste": senza questa riga la
            // prossima chiamata riprende il valore vecchio e riscrive invece di aggiornare,
            // violando il vincolo di unicita' su (Key, Environment).
            await _configService.InvalidateAsync(key, environmentName).ConfigureAwait(false);

            _logger.LogInformation("Feature {FeatureName} disabled{RoleInfo} by {UserId}",
                LogSanitizer.Sanitize(featureName), role.HasValue ? $" for {role.Value}" : "", LogSanitizer.Sanitize(userId ?? "system"));
        }
        else
        {
            // Create new configuration as disabled
            var command = new CreateConfigurationCommand(
                Key: key,
                Value: "false",
                ValueType: "Boolean",
                CreatedByUserId: userGuid,
                Description: $"Feature flag: {featureName}",
                Category: FeatureFlagCategory,
                Environment: environmentName,
                RequiresRestart: false);

            await CreateRowAsync(command, environmentName, userGuid).ConfigureAwait(false);

            // #3844 — la lettura e' in cache, compreso il "non esiste": senza questa riga la
            // prossima chiamata riprende il valore vecchio e riscrive invece di aggiornare,
            // violando il vincolo di unicita' su (Key, Environment).
            await _configService.InvalidateAsync(key, environmentName).ConfigureAwait(false);

            _logger.LogInformation("Feature {FeatureName} created and disabled{RoleInfo} by {UserId}",
                LogSanitizer.Sanitize(featureName), role.HasValue ? $" for {role.Value}" : "", LogSanitizer.Sanitize(userId ?? "system"));
        }
    }

    /// <summary>
    /// Enable a feature for a specific tier.
    /// </summary>
    public async Task EnableFeatureForTierAsync(string featureName, UserTier tier, string? userId = null)
    {
        ArgumentNullException.ThrowIfNull(tier);
        var key = $"{featureName}.{TierPrefix}.{tier.Value}";

        // Bug #2162: symmetric lookup + CREATE per environment. See EnableFeatureAsync.
        var environmentName = _environment.EnvironmentName;
        var existing = await _configService.GetConfigurationByKeyAsync(key, environmentName).ConfigureAwait(false);

        var userGuid = userId != null && Guid.TryParse(userId, out var parsed) ? parsed : Guid.Empty;

        if (existing != null)
        {
            if (!Guid.TryParse(existing.Id, out var configId))
            {
                throw new InvalidOperationException($"Invalid configuration ID format: {existing.Id}");
            }

            // Update existing configuration
            var command = new UpdateConfigValueCommand(
                ConfigId: configId,
                NewValue: "true",
                UpdatedByUserId: userGuid);

            await _mediator.Send(command).ConfigureAwait(false);

            // #3844 — la lettura e' in cache, compreso il "non esiste": senza questa riga la
            // prossima chiamata riprende il valore vecchio e riscrive invece di aggiornare,
            // violando il vincolo di unicita' su (Key, Environment).
            await _configService.InvalidateAsync(key, environmentName).ConfigureAwait(false);

            _logger.LogInformation("Feature {FeatureName} enabled for tier {Tier} by {UserId}",
                LogSanitizer.Sanitize(featureName), LogSanitizer.Sanitize(tier.Value), LogSanitizer.Sanitize(userId ?? "system"));
        }
        else
        {
            // Create new configuration
            var command = new CreateConfigurationCommand(
                Key: key,
                Value: "true",
                ValueType: "Boolean",
                CreatedByUserId: userGuid,
                Description: $"Feature flag: {featureName} (tier: {tier.Value})",
                Category: FeatureFlagCategory,
                Environment: environmentName,
                RequiresRestart: false);

            await CreateRowAsync(command, environmentName, userGuid).ConfigureAwait(false);

            // #3844 — la lettura e' in cache, compreso il "non esiste": senza questa riga la
            // prossima chiamata riprende il valore vecchio e riscrive invece di aggiornare,
            // violando il vincolo di unicita' su (Key, Environment).
            await _configService.InvalidateAsync(key, environmentName).ConfigureAwait(false);

            _logger.LogInformation("Feature {FeatureName} created and enabled for tier {Tier} by {UserId}",
                LogSanitizer.Sanitize(featureName), LogSanitizer.Sanitize(tier.Value), LogSanitizer.Sanitize(userId ?? "system"));
        }
    }

    /// <summary>
    /// Disable a feature for a specific tier.
    /// </summary>
    public async Task DisableFeatureForTierAsync(string featureName, UserTier tier, string? userId = null)
    {
        ArgumentNullException.ThrowIfNull(tier);
        var key = $"{featureName}.{TierPrefix}.{tier.Value}";

        // Bug #2162: symmetric lookup + CREATE per environment. See EnableFeatureAsync.
        var environmentName = _environment.EnvironmentName;
        var existing = await _configService.GetConfigurationByKeyAsync(key, environmentName).ConfigureAwait(false);

        var userGuid = userId != null && Guid.TryParse(userId, out var parsed) ? parsed : Guid.Empty;

        if (existing != null)
        {
            if (!Guid.TryParse(existing.Id, out var configId))
            {
                throw new InvalidOperationException($"Invalid configuration ID format: {existing.Id}");
            }

            // Update existing configuration
            var command = new UpdateConfigValueCommand(
                ConfigId: configId,
                NewValue: "false",
                UpdatedByUserId: userGuid);

            await _mediator.Send(command).ConfigureAwait(false);

            // #3844 — la lettura e' in cache, compreso il "non esiste": senza questa riga la
            // prossima chiamata riprende il valore vecchio e riscrive invece di aggiornare,
            // violando il vincolo di unicita' su (Key, Environment).
            await _configService.InvalidateAsync(key, environmentName).ConfigureAwait(false);

            _logger.LogInformation("Feature {FeatureName} disabled for tier {Tier} by {UserId}",
                LogSanitizer.Sanitize(featureName), LogSanitizer.Sanitize(tier.Value), LogSanitizer.Sanitize(userId ?? "system"));
        }
        else
        {
            // Create new configuration as disabled
            var command = new CreateConfigurationCommand(
                Key: key,
                Value: "false",
                ValueType: "Boolean",
                CreatedByUserId: userGuid,
                Description: $"Feature flag: {featureName} (tier: {tier.Value})",
                Category: FeatureFlagCategory,
                Environment: environmentName,
                RequiresRestart: false);

            await CreateRowAsync(command, environmentName, userGuid).ConfigureAwait(false);

            // #3844 — la lettura e' in cache, compreso il "non esiste": senza questa riga la
            // prossima chiamata riprende il valore vecchio e riscrive invece di aggiornare,
            // violando il vincolo di unicita' su (Key, Environment).
            await _configService.InvalidateAsync(key, environmentName).ConfigureAwait(false);

            _logger.LogInformation("Feature {FeatureName} created and disabled for tier {Tier} by {UserId}",
                LogSanitizer.Sanitize(featureName), LogSanitizer.Sanitize(tier.Value), LogSanitizer.Sanitize(userId ?? "system"));
        }
    }

    /// <summary>
    /// List all feature flags with their current states.
    /// Issue #3073: Extended to include tier restrictions.
    /// </summary>
    public async Task<List<FeatureFlagDto>> GetAllFeatureFlagsAsync()
    {
        // Get all feature flag configurations (category = "FeatureFlags")
        var query = new GetAllConfigsQuery(
            Category: FeatureFlagCategory,
            Environment: null,
            ActiveOnly: true,
            Page: 1,
            PageSize: 100);
        var configs = await _mediator.Send(query).ConfigureAwait(false);

        var flags = new List<FeatureFlagDto>();

        foreach (var config in configs.Items)
        {
            // Parse restriction from key
            // Formats: "FeatureName", "FeatureName.Role", "FeatureName.Tier.TierName"
            var parts = config.Key.Split('.');
            string? roleRestriction = null;
            string? tierRestriction = null;
            string featureName;

            // Check for tier-based key format: "FeatureName.Tier.TierName"
            if (parts.Length >= 3 && string.Equals(parts[^2], TierPrefix, StringComparison.Ordinal))
            {
                tierRestriction = parts[^1];
                featureName = string.Join(".", parts[..^2]);
            }
            // Check for role-based key format: "FeatureName.Role"
            else if (parts.Length > 1)
            {
                var lastPart = parts[^1];
                // Check if last part is a valid UserRole (Admin, Editor, User)
                if (Enum.TryParse<UserRole>(lastPart, ignoreCase: true, out _))
                {
                    roleRestriction = lastPart;
                    featureName = string.Join(".", parts[..^1]);
                }
                else
                {
                    featureName = config.Key;
                }
            }
            else
            {
                featureName = config.Key;
            }

            // Parse boolean value
            var isEnabled = bool.TryParse(config.Value, out var enabled) && enabled;

            flags.Add(new FeatureFlagDto(
                FeatureName: featureName,
                IsEnabled: isEnabled,
                RoleRestriction: roleRestriction,
                TierRestriction: tierRestriction,
                Description: config.Description ?? $"Feature flag: {featureName}"));
        }

        return flags;
    }
}

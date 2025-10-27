# SEC-11: Empty Catch Block Analysis and Remediation Plan

**Date**: 2025-10-27
**Issue**: CodeQL Warning - Empty catch blocks hide errors and make debugging difficult
**Total Instances**: 10 (8 production + 2 test categories)

---

## Executive Summary

All 10 empty catch blocks fall into three patterns:
1. **Property Access Recovery** (1 instance) - Logging policy that handles reflection errors
2. **Data Parsing Fallback** (2 instances) - JSON/XML parsing with graceful degradation
3. **Transaction Rollback** (4 instances) - Database transaction error handling
4. **Test Cleanup** (3 mentioned but not found in search)

**Risk Assessment**:
- **High Priority**: Transaction rollback blocks (4) - Hide database errors, impede debugging
- **Medium Priority**: JSON parsing (1) - Silent data loss potential
- **Low Priority**: XML parsing (1), Property access (1) - Designed for graceful degradation

---

## 1. Property Access Error Handling

### Location
**File**: `apps/api/src/Api/Logging/SensitiveDataDestructuringPolicy.cs:179`

### Current Code
```csharp
foreach (var prop in props)
{
    try
    {
        var propValue = prop.GetValue(value);
        if (IsSensitiveProperty(prop.Name))
        {
            logProperties.Add(new LogEventProperty(prop.Name, new ScalarValue(RedactedValue)));
        }
        else if (propValue is string str)
        {
            var redacted = RedactSensitiveStringPatterns(str);
            logProperties.Add(new LogEventProperty(prop.Name, new ScalarValue(redacted)));
        }
        else if (propValue != null)
        {
            // Try to recursively destructure nested objects with this policy
            if (TryDestructure(propValue, propertyValueFactory, out var nestedResult))
            {
                logProperties.Add(new LogEventProperty(prop.Name, nestedResult!));
            }
            else
            {
                logProperties.Add(new LogEventProperty(prop.Name, propertyValueFactory.CreatePropertyValue(propValue, true)));
            }
        }
    }
    catch  // ❌ EMPTY CATCH
    {
        // Skip properties that throw on access
    }
}
```

### Exception Types
- `TargetException` - Property getter throws
- `TargetInvocationException` - Property getter throws wrapped exception
- `TargetParameterCountException` - Indexed property accessed incorrectly
- `MethodAccessException` - Security restrictions
- `NullReferenceException` - Unexpected null during reflection

### Risk Assessment
**Priority**: Low
**Impact**: Logging policy gracefully handles inaccessible properties
**Current Behavior**: Correct - skip problematic properties to avoid logging infrastructure failures

### Proposed Fix
```csharp
catch (Exception ex) when (
    ex is TargetException or
    TargetInvocationException or
    TargetParameterCountException or
    MethodAccessException or
    NotSupportedException)
{
    // Intentionally skip properties that throw on access during logging
    // This prevents logging infrastructure failures from propagating
    // Debug-level logging for troubleshooting if needed
    if (_logger?.IsEnabled(LogLevel.Debug) == true)
    {
        _logger.LogDebug(
            ex,
            "Skipped property {PropertyName} during destructuring due to access error",
            prop.Name);
    }
}
```

### Justification
- **Specific Exception Filter**: Only catches expected reflection errors
- **Optional Debug Logging**: Helps troubleshooting without impacting production performance
- **Preserves Intent**: Maintains "skip and continue" behavior for logging resilience
- **Security**: Doesn't expose sensitive data in debug logs (only property name)

---

## 2. JSON Parsing Error Handling

### Location
**File**: `apps/api/src/Api/Services/ApiKeyManagementService.cs:382`

### Current Code
```csharp
private static (int? MaxRequestsPerDay, int? MaxRequestsPerHour) ParseQuotaFromMetadata(string? metadata)
{
    if (string.IsNullOrWhiteSpace(metadata))
        return (null, null);

    try
    {
        using var doc = System.Text.Json.JsonDocument.Parse(metadata);
        var root = doc.RootElement;

        var maxPerDay = root.TryGetProperty("maxRequestsPerDay", out var dayProp) && dayProp.ValueKind == System.Text.Json.JsonValueKind.Number
            ? (int?)dayProp.GetInt32()
            : null;

        var maxPerHour = root.TryGetProperty("maxRequestsPerHour", out var hourProp) && hourProp.ValueKind == System.Text.Json.JsonValueKind.Number
            ? (int?)hourProp.GetInt32()
            : null;

        return (maxPerDay, maxPerHour);
    }
    catch  // ❌ EMPTY CATCH
    {
        return (null, null);
    }
}
```

### Exception Types
- `JsonException` - Malformed JSON structure
- `FormatException` - Invalid number format
- `InvalidOperationException` - Wrong JSON element type accessed
- `ObjectDisposedException` - JsonDocument disposed during access

### Risk Assessment
**Priority**: Medium
**Impact**: Silent data loss - invalid metadata returns null without logging
**Current Behavior**: Questionable - admins won't know API keys have invalid metadata

### Proposed Fix
```csharp
catch (JsonException ex)
{
    _logger.LogWarning(
        ex,
        "Failed to parse API key metadata as JSON. Metadata will be ignored: {Metadata}",
        metadata?.Length > 100 ? metadata[..100] + "..." : metadata);
    return (null, null);
}
catch (Exception ex) when (ex is FormatException or InvalidOperationException)
{
    _logger.LogWarning(
        ex,
        "Failed to extract quota values from API key metadata. Metadata: {Metadata}",
        metadata?.Length > 100 ? metadata[..100] + "..." : metadata);
    return (null, null);
}
```

### Justification
- **Diagnostic Visibility**: Admins can see when API key metadata is malformed
- **Data Loss Prevention**: Logged warnings help identify configuration issues
- **Security**: Truncates metadata to 100 chars to prevent log injection
- **Specific Exceptions**: Separates JSON parsing from data extraction errors
- **Preserves Behavior**: Still returns (null, null) for graceful degradation

---

## 3. XML Parsing Error Handling - Search Results

### Location
**File**: `apps/api/src/Api/Services/BggApiService.cs:227`

### Current Code
```csharp
private static BggSearchResultDto? ParseSearchResult(XElement item)
{
    try
    {
        var id = (int?)item.Attribute("id");
        var name = item.Element("name")?.Attribute("value")?.Value;
        var yearPublished = (int?)item.Element("yearpublished")?.Attribute("value");
        var thumbnail = item.Element("thumbnail")?.Value;
        var type = item.Attribute("type")?.Value ?? "boardgame";

        if (id == null || string.IsNullOrEmpty(name))
        {
            return null;
        }

        return new BggSearchResultDto(
            id.Value,
            name,
            yearPublished,
            thumbnail,
            type
        );
    }
    catch  // ❌ EMPTY CATCH
    {
        return null;
    }
}
```

### Exception Types
- `FormatException` - Invalid integer format in XML attributes
- `OverflowException` - Integer value too large
- `NullReferenceException` - Unexpected null in XML structure
- `XmlException` - Malformed XML element

### Risk Assessment
**Priority**: Low
**Impact**: Individual search result parsing failure - acceptable for external API
**Current Behavior**: Acceptable - BGG API may return inconsistent data

### Proposed Fix
```csharp
catch (Exception ex) when (ex is FormatException or OverflowException)
{
    // BGG API occasionally returns malformed data - skip this result
    // Logging at Debug level to avoid noise from external API issues
    _logger.LogDebug(
        ex,
        "Skipped malformed BGG search result. Item ID attribute: {ItemId}",
        item.Attribute("id")?.Value ?? "unknown");
    return null;
}
```

### Justification
- **External API Resilience**: BGG API quality is outside our control
- **Minimal Logging**: Debug-level prevents production log noise
- **Specific Exceptions**: Only catches expected parsing errors
- **Graceful Degradation**: Skipping one result doesn't break entire search
- **Diagnostic Info**: Includes item ID for troubleshooting

---

## 4. XML Parsing Error Handling - Game Details

### Location
**File**: `apps/api/src/Api/Services/BggApiService.cs:323`

### Current Code
```csharp
private static BggGameDetailsDto? ParseGameDetails(XElement item, int bggId)
{
    try
    {
        // Primary name (type="primary")
        var primaryName = item.Elements("name")
            .FirstOrDefault(n => n.Attribute("type")?.Value == "primary")
            ?.Attribute("value")?.Value;

        var name = primaryName ?? item.Element("name")?.Attribute("value")?.Value;
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        // Description (may contain HTML)
        var description = item.Element("description")?.Value;

        // Basic metadata
        var yearPublished = (int?)item.Element("yearpublished")?.Attribute("value");
        var minPlayers = (int?)item.Element("minplayers")?.Attribute("value");
        var maxPlayers = (int?)item.Element("maxplayers")?.Attribute("value");
        var playingTime = (int?)item.Element("playingtime")?.Attribute("value");
        var minPlayTime = (int?)item.Element("minplaytime")?.Attribute("value");
        var maxPlayTime = (int?)item.Element("maxplaytime")?.Attribute("value");
        var minAge = (int?)item.Element("minage")?.Attribute("value");

        // Images
        var thumbnail = item.Element("thumbnail")?.Value;
        var image = item.Element("image")?.Value;

        // Ratings and stats
        var stats = item.Element("statistics")?.Element("ratings");
        var averageRating = ParseDouble(stats?.Element("average")?.Attribute("value")?.Value);
        var bayesAverageRating = ParseDouble(stats?.Element("bayesaverage")?.Attribute("value")?.Value);
        var usersRated = (int?)stats?.Element("usersrated")?.Attribute("value");
        var averageWeight = ParseDouble(stats?.Element("averageweight")?.Attribute("value")?.Value);

        // Categories, Mechanics, Designers, Publishers
        var categories = item.Elements("link")
            .Where(l => l.Attribute("type")?.Value == "boardgamecategory")
            .Select(l => l.Attribute("value")?.Value)
            .Where(v => !string.IsNullOrEmpty(v))
            .Cast<string>()
            .ToList();

        var mechanics = item.Elements("link")
            .Where(l => l.Attribute("type")?.Value == "boardgamemechanic")
            .Select(l => l.Attribute("value")?.Value)
            .Where(v => !string.IsNullOrEmpty(v))
            .Cast<string>()
            .ToList();

        var designers = item.Elements("link")
            .Where(l => l.Attribute("type")?.Value == "boardgamedesigner")
            .Select(l => l.Attribute("value")?.Value)
            .Where(v => !string.IsNullOrEmpty(v))
            .Cast<string>()
            .ToList();

        var publishers = item.Elements("link")
            .Where(l => l.Attribute("type")?.Value == "boardgamepublisher")
            .Select(l => l.Attribute("value")?.Value)
            .Where(v => !string.IsNullOrEmpty(v))
            .Cast<string>()
            .ToList();

        return new BggGameDetailsDto(
            bggId,
            name,
            description,
            yearPublished,
            minPlayers,
            maxPlayers,
            playingTime,
            minPlayTime,
            maxPlayTime,
            minAge,
            averageRating,
            bayesAverageRating,
            usersRated,
            averageWeight,
            thumbnail,
            image,
            categories,
            mechanics,
            designers,
            publishers
        );
    }
    catch  // ❌ EMPTY CATCH
    {
        return null;
    }
}
```

### Exception Types
- `FormatException` - Invalid integer/double format
- `OverflowException` - Numeric value too large
- `ArgumentException` - LINQ query issues
- `InvalidOperationException` - Cast operations on null collections

### Risk Assessment
**Priority**: Low
**Impact**: Game details fetch failure - returns null (already logged at caller level)
**Current Behavior**: Acceptable - caller method logs errors

### Proposed Fix
```csharp
catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
{
    // BGG API occasionally returns malformed data - caller will log this
    // Only log at Debug level to avoid duplicate errors
    _logger.LogDebug(
        ex,
        "Failed to parse BGG game details for ID {BggId}. Data may be malformed in BGG API response",
        bggId);
    return null;
}
```

### Justification
- **Caller Already Logs**: `GetGameDetailsAsync` already logs at Error level
- **Avoid Duplicate Logging**: Debug-level here prevents log spam
- **External API Resilience**: BGG data quality issues are common
- **Specific Exceptions**: Only catches expected parsing errors
- **Diagnostic Context**: Includes BGG ID for correlation with caller logs

---

## 5. Transaction Rollback - Configuration Bulk Update

### Location
**File**: `apps/api/src/Api/Services/ConfigurationService.cs:387`

### Current Code
```csharp
public async Task<IReadOnlyList<SystemConfigurationDto>> BulkUpdateConfigurationsAsync(
    BulkConfigurationUpdateRequest request,
    string userId)
{
    // Use a transaction for atomicity
    using var transaction = await _dbContext.Database.BeginTransactionAsync();

    try
    {
        var updatedConfigs = new List<SystemConfigurationDto>();

        foreach (var update in request.Updates)
        {
            var entity = await _dbContext.SystemConfigurations.FindAsync(update.Id);

            if (entity == null)
            {
                throw new InvalidOperationException($"Configuration with ID {update.Id} not found");
            }

            // Validate the new value
            var validationResult = await ValidateConfigurationAsync(entity.Key, update.Value, entity.ValueType);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException(
                    $"Validation failed for {entity.Key}: {string.Join(", ", validationResult.Errors)}");
            }

            entity.PreviousValue = entity.Value;
            entity.Value = update.Value;
            entity.Version++;
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedByUserId = userId;

            updatedConfigs.Add(MapToDto(entity));
        }

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        _logger.LogInformation(
            "Bulk update completed for {Count} configurations by user {UserId}",
            request.Updates.Count, userId);

        // Invalidate all affected caches
        foreach (var config in updatedConfigs)
        {
            await InvalidateCacheAsync(config.Key);
        }

        return updatedConfigs;
    }
    catch  // ❌ EMPTY CATCH - CRITICAL ISSUE
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

### Exception Types
- `InvalidOperationException` - Business validation failure (already thrown explicitly)
- `DbUpdateException` - Database constraint violations
- `DbUpdateConcurrencyException` - Concurrent modification conflict
- `OperationCanceledException` - Task cancellation
- `ObjectDisposedException` - Transaction disposed unexpectedly

### Risk Assessment
**Priority**: ⚠️ **HIGH**
**Impact**: CRITICAL - Hides database errors that should be propagated
**Current Issue**: The catch block is NOT empty - it re-throws! But rollback errors are hidden

### Actual Problem Analysis
The catch block DOES re-throw the original exception, so this is **NOT** an empty catch block in the traditional sense. However, if `transaction.RollbackAsync()` itself throws an exception, that error is lost.

### Proposed Fix
```csharp
catch (Exception ex)
{
    _logger.LogError(
        ex,
        "Bulk configuration update failed for user {UserId}. Attempting rollback. Updates: {UpdateCount}",
        userId,
        request.Updates.Count);

    try
    {
        await transaction.RollbackAsync();
    }
    catch (Exception rollbackEx)
    {
        _logger.LogError(
            rollbackEx,
            "Failed to rollback transaction after bulk update error for user {UserId}",
            userId);
        // Don't throw rollback exception - original exception is more important
    }

    throw; // Re-throw original exception
}
```

### Justification
- **Log Original Error**: Captures the business logic or database error that triggered rollback
- **Safe Rollback**: Catches and logs rollback errors without masking original error
- **Error Propagation**: Preserves original exception for HTTP response (400/500)
- **Diagnostic Context**: Includes user ID and update count for troubleshooting
- **Transaction Safety**: Ensures rollback attempts even if already aborted

---

## 6. Transaction Rollback - Configuration Import

### Location
**File**: `apps/api/src/Api/Services/ConfigurationService.cs:580`

### Current Code
```csharp
public async Task<int> ImportConfigurationsAsync(
    ConfigurationImportRequest request,
    string userId)
{
    // Use a transaction for atomicity
    using var transaction = await _dbContext.Database.BeginTransactionAsync();

    try
    {
        var importedCount = 0;

        foreach (var configRequest in request.Configurations)
        {
            var exists = await _dbContext.SystemConfigurations
                .AnyAsync(c => c.Key == configRequest.Key && c.Environment == configRequest.Environment);

            if (exists)
            {
                if (request.OverwriteExisting)
                {
                    // Update existing
                    var entity = await _dbContext.SystemConfigurations
                        .FirstAsync(c => c.Key == configRequest.Key && c.Environment == configRequest.Environment);

                    entity.PreviousValue = entity.Value;
                    entity.Value = configRequest.Value;
                    entity.ValueType = configRequest.ValueType;
                    entity.Description = configRequest.Description;
                    entity.Category = configRequest.Category;
                    entity.IsActive = configRequest.IsActive;
                    entity.RequiresRestart = configRequest.RequiresRestart;
                    entity.Version++;
                    entity.UpdatedAt = DateTime.UtcNow;
                    entity.UpdatedByUserId = userId;

                    importedCount++;
                }
                // else skip existing
            }
            else
            {
                // Create new
                var entity = new SystemConfigurationEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    Key = configRequest.Key,
                    Value = configRequest.Value,
                    ValueType = configRequest.ValueType,
                    Description = configRequest.Description,
                    Category = configRequest.Category,
                    IsActive = configRequest.IsActive,
                    RequiresRestart = configRequest.RequiresRestart,
                    Environment = configRequest.Environment,
                    Version = 1,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedByUserId = userId
                };

                _dbContext.SystemConfigurations.Add(entity);
                importedCount++;
            }
        }

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        _logger.LogInformation(
            "Imported {Count} configurations by user {UserId}",
            importedCount, userId);

        // Invalidate all caches
        await InvalidateCacheAsync();

        return importedCount;
    }
    catch  // ❌ EMPTY CATCH - CRITICAL ISSUE
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

### Exception Types
Same as #5 - database and transaction errors

### Risk Assessment
**Priority**: ⚠️ **HIGH**
**Impact**: CRITICAL - Same issue as #5 (rollback errors hidden)

### Proposed Fix
```csharp
catch (Exception ex)
{
    _logger.LogError(
        ex,
        "Configuration import failed for user {UserId}. Attempting rollback. Total configs: {TotalCount}, Imported before failure: {ImportedCount}",
        userId,
        request.Configurations.Count,
        importedCount);

    try
    {
        await transaction.RollbackAsync();
    }
    catch (Exception rollbackEx)
    {
        _logger.LogError(
            rollbackEx,
            "Failed to rollback transaction after import error for user {UserId}",
            userId);
        // Don't throw rollback exception - original exception is more important
    }

    throw; // Re-throw original exception
}
```

### Justification
Same as #5 - Critical for diagnosing import failures and partial completion state

---

## 7. Transaction Rollback - Prompt Version Creation

### Location
**File**: `apps/api/src/Api/Services/PromptManagementService.cs:387`

### Current Code
```csharp
public async Task<PromptVersionDto> CreateVersionAsync(
    string templateId,
    CreatePromptVersionRequest request,
    string createdByUserId,
    CancellationToken ct = default)
{
    // ... validation code omitted ...

    var now = _timeProvider.GetUtcNow().UtcDateTime;

    // Start transaction for version creation
    using var transaction = await _db.Database.BeginTransactionAsync(ct);

    try
    {
        // Create new version
        var versionId = Guid.NewGuid().ToString();
        var nextVersionNumber = maxVersionNumber + 1;

        var version = new PromptVersionEntity
        {
            Id = versionId,
            TemplateId = templateId,
            VersionNumber = nextVersionNumber,
            Content = request.Content,
            ChangeNotes = request.ChangeNotes,
            IsActive = request.ActivateImmediately,
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
            Template = null!, // Will be loaded by EF
            CreatedBy = null! // Will be loaded by EF
        };

        _db.PromptVersions.Add(version);

        // If immediate activation requested, deactivate all other versions
        if (request.ActivateImmediately)
        {
            var otherVersions = await _db.PromptVersions
                .Where(v => v.TemplateId == templateId && v.Id != versionId && v.IsActive)
                .ToListAsync(ct);

            foreach (var otherVersion in otherVersions)
            {
                otherVersion.IsActive = false;

                // Create deactivation audit log
                var deactivationAuditLog = new PromptAuditLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    TemplateId = templateId,
                    VersionId = otherVersion.Id,
                    Action = "version_deactivated",
                    ChangedByUserId = createdByUserId,
                    ChangedAt = now,
                    Details = JsonSerializer.Serialize(new
                    {
                        versionNumber = otherVersion.VersionNumber,
                        reason = $"Deactivated due to activation of version {nextVersionNumber}"
                    }),
                    Template = null!, // Will be loaded by EF
                    ChangedBy = null! // Will be loaded by EF
                };

                _db.PromptAuditLogs.Add(deactivationAuditLog);
            }

            // Create activation audit log for new version
            var activationAuditLog = new PromptAuditLogEntity
            {
                Id = Guid.NewGuid().ToString(),
                TemplateId = templateId,
                VersionId = versionId,
                Action = "version_activated",
                ChangedByUserId = createdByUserId,
                ChangedAt = now,
                Details = JsonSerializer.Serialize(new
                {
                    versionNumber = nextVersionNumber,
                    reason = "Activated immediately upon creation"
                }),
                Template = null!, // Will be loaded by EF
                ChangedBy = null! // Will be loaded by EF
            };

            _db.PromptAuditLogs.Add(activationAuditLog);
        }

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation(
            "Created version {VersionNumber} for template {TemplateId} by user {UserId} (activated: {IsActive})",
            nextVersionNumber, templateId, createdByUserId, request.ActivateImmediately);

        // Load created version with navigation properties
        var createdVersion = await _db.PromptVersions
            .Include(v => v.CreatedBy)
            .FirstAsync(v => v.Id == versionId, ct);

        return MapToVersionDto(createdVersion);
    }
    catch  // ❌ EMPTY CATCH - CRITICAL ISSUE
    {
        await transaction.RollbackAsync(ct);
        throw;
    }
}
```

### Exception Types
Same as #5 - database and transaction errors

### Risk Assessment
**Priority**: ⚠️ **HIGH**
**Impact**: CRITICAL - Same issue (rollback errors hidden)

### Proposed Fix
```csharp
catch (Exception ex)
{
    _logger.LogError(
        ex,
        "Prompt version creation failed for template {TemplateId} by user {UserId}. Version number: {VersionNumber}, Activate immediately: {ActivateImmediately}",
        templateId,
        createdByUserId,
        nextVersionNumber,
        request.ActivateImmediately);

    try
    {
        await transaction.RollbackAsync(ct);
    }
    catch (Exception rollbackEx)
    {
        _logger.LogError(
            rollbackEx,
            "Failed to rollback transaction after prompt version creation error. Template: {TemplateId}, User: {UserId}",
            templateId,
            createdByUserId);
        // Don't throw rollback exception - original exception is more important
    }

    throw; // Re-throw original exception
}
```

### Justification
Same as #5 - Critical for prompt management audit trail integrity

---

## 8. Transaction Rollback - Prompt Version Activation

### Location
**File**: `apps/api/src/Api/Services/PromptManagementService.cs:545`

### Current Code
```csharp
public async Task<PromptVersionDto> ActivateVersionAsync(
    string templateId,
    string versionId,
    string activatedByUserId,
    string? reason = null,
    CancellationToken ct = default)
{
    // ... validation code omitted ...

    var now = _timeProvider.GetUtcNow().UtcDateTime;

    // Start transaction for activation
    using var transaction = await _db.Database.BeginTransactionAsync(ct);

    try
    {
        // Deactivate all other versions of the same template
        var otherVersions = await _db.PromptVersions
            .Where(v => v.TemplateId == templateId && v.Id != versionId && v.IsActive)
            .ToListAsync(ct);

        foreach (var otherVersion in otherVersions)
        {
            otherVersion.IsActive = false;

            // Create deactivation audit log
            var deactivationAuditLog = new PromptAuditLogEntity
            {
                Id = Guid.NewGuid().ToString(),
                TemplateId = templateId,
                VersionId = otherVersion.Id,
                Action = "version_deactivated",
                ChangedByUserId = activatedByUserId,
                ChangedAt = now,
                Details = JsonSerializer.Serialize(new
                {
                    versionNumber = otherVersion.VersionNumber,
                    reason = $"Deactivated due to activation of version {versionToActivate.VersionNumber}"
                }),
                Template = null!, // Will be loaded by EF
                ChangedBy = null! // Will be loaded by EF
            };

            _db.PromptAuditLogs.Add(deactivationAuditLog);
        }

        // Activate the target version
        versionToActivate.IsActive = true;

        // Create activation audit log
        var activationAuditLog = new PromptAuditLogEntity
        {
            Id = Guid.NewGuid().ToString(),
            TemplateId = templateId,
            VersionId = versionId,
            Action = "version_activated",
            ChangedByUserId = activatedByUserId,
            ChangedAt = now,
            Details = JsonSerializer.Serialize(new
            {
                versionNumber = versionToActivate.VersionNumber,
                reason = reason ?? "Manual activation"
            }),
            Template = null!, // Will be loaded by EF
            ChangedBy = null! // Will be loaded by EF
        };

        _db.PromptAuditLogs.Add(activationAuditLog);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _logger.LogInformation(
            "Activated version {VersionNumber} ({VersionId}) for template {TemplateId} by user {UserId}",
            versionToActivate.VersionNumber, versionId, templateId, activatedByUserId);

        return MapToVersionDto(versionToActivate);
    }
    catch  // ❌ EMPTY CATCH - CRITICAL ISSUE
    {
        await transaction.RollbackAsync(ct);
        throw;
    }
}
```

### Exception Types
Same as #5 - database and transaction errors

### Risk Assessment
**Priority**: ⚠️ **HIGH**
**Impact**: CRITICAL - Same issue (rollback errors hidden)

### Proposed Fix
```csharp
catch (Exception ex)
{
    _logger.LogError(
        ex,
        "Prompt version activation failed. Template: {TemplateId}, Version: {VersionId}, User: {UserId}, Reason: {Reason}",
        templateId,
        versionId,
        activatedByUserId,
        reason ?? "Manual activation");

    try
    {
        await transaction.RollbackAsync(ct);
    }
    catch (Exception rollbackEx)
    {
        _logger.LogError(
            rollbackEx,
            "Failed to rollback transaction after prompt activation error. Template: {TemplateId}, Version: {VersionId}",
            templateId,
            versionId);
        // Don't throw rollback exception - original exception is more important
    }

    throw; // Re-throw original exception
}
```

### Justification
Same as #5 - Critical for prompt activation audit trail and cache invalidation

---

## 9. Test Cleanup Scenarios

### Status
**INVESTIGATION RESULT**: No empty catch blocks found in test files using multiple search patterns.

The mentioned "8 test files" likely refers to test cleanup code that uses empty catch blocks for:
- Disposing test resources
- Cleaning up test databases
- Removing temporary files

### Standard Test Cleanup Pattern
```csharp
[TearDown]
public void Cleanup()
{
    try
    {
        _testDatabase?.Dispose();
    }
    catch
    {
        // Ignore cleanup errors - test already completed
    }
}
```

### Risk Assessment
**Priority**: Low (if they exist)
**Impact**: Test infrastructure only - doesn't affect production

### Recommendation
If found during implementation:
1. Use specific exception handling: `catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)`
2. Log at Debug level if ILogger available in test context
3. Document why cleanup failures are acceptable

---

## Summary of Recommendations

### Implementation Priority

1. **CRITICAL (Immediate)** - Transaction Rollback Blocks (4 instances)
   - ConfigurationService.cs:387 (Bulk Update)
   - ConfigurationService.cs:580 (Import)
   - PromptManagementService.cs:387 (Version Creation)
   - PromptManagementService.cs:545 (Version Activation)
   - **Impact**: Hidden database errors, debugging nightmare
   - **Effort**: 2 hours (all similar pattern)

2. **MEDIUM (Next Sprint)** - JSON Parsing (1 instance)
   - ApiKeyManagementService.cs:382
   - **Impact**: Silent configuration data loss
   - **Effort**: 30 minutes

3. **LOW (Backlog)** - XML Parsing & Property Access (3 instances)
   - BggApiService.cs:227, 323
   - SensitiveDataDestructuringPolicy.cs:179
   - **Impact**: Already designed for graceful degradation
   - **Effort**: 1 hour total

### Testing Strategy

For each fix:
1. **Unit Test**: Verify specific exception types are caught
2. **Integration Test**: Verify logging occurs
3. **Negative Test**: Verify rollback failures don't mask original errors

### Success Metrics

- ✅ Zero empty catch blocks in production code
- ✅ All transaction errors logged before rollback
- ✅ Rollback failures logged without masking original errors
- ✅ External API errors logged at appropriate levels
- ✅ CodeQL warning count reduced by 10

---

## Implementation Checklist

- [ ] 1. Fix ConfigurationService.cs:387 (transaction rollback + logging)
- [ ] 2. Fix ConfigurationService.cs:580 (transaction rollback + logging)
- [ ] 3. Fix PromptManagementService.cs:387 (transaction rollback + logging)
- [ ] 4. Fix PromptManagementService.cs:545 (transaction rollback + logging)
- [ ] 5. Fix ApiKeyManagementService.cs:382 (JSON parsing + logging)
- [ ] 6. Fix BggApiService.cs:227 (XML parsing + debug logging)
- [ ] 7. Fix BggApiService.cs:323 (XML parsing + debug logging)
- [ ] 8. Fix SensitiveDataDestructuringPolicy.cs:179 (property access + debug logging)
- [ ] 9. Add unit tests for exception handling paths
- [ ] 10. Add integration tests for logging behavior
- [ ] 11. Verify CodeQL warnings reduced to 359 (369 - 10)
- [ ] 12. Document exception handling patterns in team guidelines

---

## Risk Mitigation

### Rollback Pattern (CRITICAL)
The transaction rollback pattern is the most dangerous because:
1. Original exception IS re-thrown (good)
2. But rollback failures are SILENT (bad)
3. Database may be in inconsistent state without any log evidence

**Solution**: Nested try-catch ensures both original error AND rollback failures are logged.

### External API Pattern (LOW RISK)
BGG API parsing failures are acceptable because:
1. External data quality is unpredictable
2. Skipping malformed results doesn't break functionality
3. Caller methods already log errors at appropriate levels

**Solution**: Add debug-level logging for troubleshooting without production noise.

---

**Analysis Completed**: 2025-10-27
**Next Action**: Implement CRITICAL fixes (transaction rollbacks) first

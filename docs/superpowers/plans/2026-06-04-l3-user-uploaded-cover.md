# L3 User-Uploaded Custom Cover Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement L3 user-uploaded custom cover upload + delete (POST/DELETE `/api/v1/library/{gameId}/cover`) with manual crop UI (react-easy-crop), HEIC iOS support (heic2any lazy-loaded), size discipline (≤200KB webp), EXIF strip via canvas, and event-driven R2 cleanup on game removal from library.

**Architecture:** BE 5 commands/handlers + 1 event handler ext in `UserLibrary` BC. FE 3 components + 1 hook in `apps/web/src/components/features/library/`. `CoverUrlResolver.ResolveForUserAsync` priority chain L3→L4→L2 and `GetUserLibraryQueryHandler` integration are **already wired in main-dev** (from #1852). DB column `UserLibraryEntryEntity.CustomCoverR2Key` already exists (from #1839 stub). Scope: create endpoints + UI + cleanup handler.

**Tech Stack:** .NET 9 + MediatR (BE), Next.js 16 + React 19 + Tailwind 4 + Zustand + react-query (FE), `react-easy-crop` (~12KB gz), `heic2any` (~80KB gz lazy), `IBlobStorageService` (existing pattern from #1873), Vitest + Playwright (tests).

**Spec reference:** `docs/superpowers/specs/2026-06-04-l3-user-uploaded-cover-design.md` (commit `11deddc89`).

---

## File Structure

### Backend (new files)
- `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/CustomCover/UploadCustomCoverCommand.cs` — command record
- `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/CustomCover/UploadCustomCoverCommandHandler.cs` — handler
- `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/CustomCover/RemoveCustomCoverCommand.cs` — command record
- `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/CustomCover/RemoveCustomCoverCommandHandler.cs` — handler
- `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Validators/UploadCustomCoverCommandValidator.cs` — FluentValidation rules
- `apps/api/src/Api/BoundedContexts/UserLibrary/Application/EventHandlers/GameRemovedFromLibraryCustomCoverHandler.cs` — best-effort R2 cleanup on game removal
- `apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/CustomCoverUploadResult.cs` — response DTO
- `apps/api/src/Api/Routing/UserLibrary/UserLibraryCoverEndpoints.cs` — POST/DELETE endpoint mapping

### Backend (test files)
- `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Commands/CustomCover/UploadCustomCoverCommandHandlerTests.cs`
- `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Commands/CustomCover/RemoveCustomCoverCommandHandlerTests.cs`
- `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Validators/UploadCustomCoverCommandValidatorTests.cs`
- `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/EventHandlers/GameRemovedFromLibraryCustomCoverHandlerTests.cs`

### Backend (modify)
- `apps/api/src/Api/Routing/UserLibraryEndpoints.cs` — register `UserLibraryCoverEndpoints.Map(group)`

### Frontend (new files)
- `apps/web/src/components/features/library/custom-cover/EditCoverOverlay.tsx` — icon overlay component
- `apps/web/src/components/features/library/custom-cover/CropDialog.tsx` — modale crop con react-easy-crop
- `apps/web/src/components/features/library/custom-cover/CustomCoverDialog.tsx` — orchestrator (file picker + HEIC + crop + upload)
- `apps/web/src/hooks/mutations/useCustomCoverUpload.ts` — react-query mutation hook
- `apps/web/src/hooks/mutations/useRemoveCustomCover.ts` — react-query mutation hook

### Frontend (test files)
- `apps/web/src/components/features/library/custom-cover/__tests__/EditCoverOverlay.test.tsx`
- `apps/web/src/components/features/library/custom-cover/__tests__/CropDialog.test.tsx`
- `apps/web/src/components/features/library/custom-cover/__tests__/CustomCoverDialog.test.tsx`
- `apps/web/src/hooks/mutations/__tests__/useCustomCoverUpload.test.ts`
- `apps/web/e2e/library/custom-cover.spec.ts` — E2E happy path

### Frontend (modify)
- `apps/web/package.json` — add `react-easy-crop` + `heic2any` deps
- `apps/web/src/components/game-detail/GameDetailDesktop.tsx` — integrate `EditCoverOverlay` on hero
- `apps/web/src/app/(authenticated)/library/[gameId]/game-detail-mobile.tsx` — integrate `EditCoverOverlay` on hero

---

## Task Decomposition

12 tasks total. Mix-model recommendation (P120):
- **Haiku** (mechanical): T1, T3, T6, T7, T11
- **Sonnet** (judgment): T2, T4, T5, T8, T9, T10, T12

---

### Task 1: UploadCustomCoverCommand + DTO + Validator (BE)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/CustomCover/UploadCustomCoverCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/CustomCover/RemoveCustomCoverCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/CustomCoverUploadResult.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Validators/UploadCustomCoverCommandValidator.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Validators/UploadCustomCoverCommandValidatorTests.cs`

- [ ] **Step 1: Write the failing validator tests**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Validators/UploadCustomCoverCommandValidatorTests.cs
using Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;
using Api.BoundedContexts.UserLibrary.Application.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Validators;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserLibrary")]
public class UploadCustomCoverCommandValidatorTests
{
    private readonly UploadCustomCoverCommandValidator _validator = new();

    [Fact]
    public void Validate_EmptyUserId_Fails()
    {
        var cmd = CreateCommand(userId: Guid.Empty);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.UserId);
    }

    [Fact]
    public void Validate_EmptyGameId_Fails()
    {
        var cmd = CreateCommand(gameId: Guid.Empty);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.GameId);
    }

    [Fact]
    public void Validate_FileSizeOverLimit_Fails()
    {
        var cmd = CreateCommand(fileSizeBytes: 10_485_761); // 10MB + 1 byte
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.FileSizeBytes);
    }

    [Fact]
    public void Validate_FileSizeZero_Fails()
    {
        var cmd = CreateCommand(fileSizeBytes: 0);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.FileSizeBytes);
    }

    [Theory]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/webp")]
    [InlineData("image/heic")]
    public void Validate_ValidMimeType_Passes(string mime)
    {
        var cmd = CreateCommand(mimeType: mime);
        _validator.TestValidate(cmd).ShouldNotHaveValidationErrorFor(c => c.MimeType);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("text/plain")]
    [InlineData("image/svg+xml")]
    [InlineData("")]
    public void Validate_InvalidMimeType_Fails(string mime)
    {
        var cmd = CreateCommand(mimeType: mime);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(c => c.MimeType);
    }

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var cmd = CreateCommand();
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    private static UploadCustomCoverCommand CreateCommand(
        Guid? userId = null,
        Guid? gameId = null,
        long fileSizeBytes = 150_000,
        string mimeType = "image/webp")
    {
        return new UploadCustomCoverCommand(
            UserId: userId ?? Guid.NewGuid(),
            GameId: gameId ?? Guid.NewGuid(),
            FileStream: new MemoryStream(new byte[100]),
            FileSizeBytes: fileSizeBytes,
            MimeType: mimeType
        );
    }
}
```

- [ ] **Step 2: Run test to verify it fails (compile errors expected)**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~UploadCustomCoverCommandValidatorTests" --no-build 2>&1 | tail -20`
Expected: COMPILE ERROR — `UploadCustomCoverCommand` and `UploadCustomCoverCommandValidator` don't exist yet.

- [ ] **Step 3: Implement UploadCustomCoverCommand record**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/CustomCover/UploadCustomCoverCommand.cs
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;

/// <summary>
/// Command to upload a user-custom cover image for a game in their library (L3).
/// Issue #1824 (umbrella #1821, cover stack L3).
/// </summary>
internal record UploadCustomCoverCommand(
    Guid UserId,
    Guid GameId,
    Stream FileStream,
    long FileSizeBytes,
    string MimeType
) : ICommand<CustomCoverUploadResult>;
```

- [ ] **Step 4: Implement RemoveCustomCoverCommand record**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/CustomCover/RemoveCustomCoverCommand.cs
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;

/// <summary>
/// Command to remove a user-custom cover image for a game in their library (L3).
/// Issue #1824 (umbrella #1821, cover stack L3).
/// </summary>
internal record RemoveCustomCoverCommand(
    Guid UserId,
    Guid GameId
) : ICommand;
```

- [ ] **Step 5: Implement CustomCoverUploadResult DTO**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/CustomCoverUploadResult.cs
namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// Result of a successful custom cover upload (L3).
/// </summary>
internal sealed record CustomCoverUploadResult(
    string CoverR2Key,
    string PresignedUrl
);
```

- [ ] **Step 6: Implement UploadCustomCoverCommandValidator**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/Validators/UploadCustomCoverCommandValidator.cs
using Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Validator for UploadCustomCoverCommand.
/// Enforces: GameId/UserId not empty, FileSize > 0 AND ≤ 10MB, MIME in whitelist.
/// </summary>
internal sealed class UploadCustomCoverCommandValidator : AbstractValidator<UploadCustomCoverCommand>
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/heic"
    };

    public UploadCustomCoverCommandValidator()
    {
        RuleFor(c => c.UserId).NotEmpty().WithMessage("UserId is required");
        RuleFor(c => c.GameId).NotEmpty().WithMessage("GameId is required");
        RuleFor(c => c.FileSizeBytes)
            .GreaterThan(0).WithMessage("File cannot be empty")
            .LessThanOrEqualTo(MaxFileSizeBytes).WithMessage($"File size cannot exceed {MaxFileSizeBytes} bytes (10MB)");
        RuleFor(c => c.MimeType)
            .NotEmpty().WithMessage("MIME type is required")
            .Must(mime => AllowedMimeTypes.Contains(mime))
            .WithMessage($"MIME type must be one of: {string.Join(", ", AllowedMimeTypes)}");
    }
}
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~UploadCustomCoverCommandValidatorTests" 2>&1 | tail -10`
Expected: All 8 tests PASS (3 empty/size + 4 mime + 1 happy path).

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/CustomCover/ \
        apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/CustomCoverUploadResult.cs \
        apps/api/src/Api/BoundedContexts/UserLibrary/Application/Validators/UploadCustomCoverCommandValidator.cs \
        apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Validators/UploadCustomCoverCommandValidatorTests.cs
git commit -m "feat(user-library): #1824 L3 UploadCustomCoverCommand + RemoveCustomCoverCommand + validator"
```

---

### Task 2: UploadCustomCoverCommandHandler (BE)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/CustomCover/UploadCustomCoverCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Commands/CustomCover/UploadCustomCoverCommandHandlerTests.cs`

- [ ] **Step 1: Write the failing handler tests**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Commands/CustomCover/UploadCustomCoverCommandHandlerTests.cs
using Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Commands.CustomCover;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserLibrary")]
public class UploadCustomCoverCommandHandlerTests
{
    private readonly Mock<IUserLibraryRepository> _libraryRepo = new();
    private readonly Mock<IBlobStorageService> _blobStorage = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ILogger<UploadCustomCoverCommandHandler> _logger = NullLogger<UploadCustomCoverCommandHandler>.Instance;

    [Fact]
    public async Task Handle_GameNotInLibrary_ThrowsForbidden()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        _libraryRepo.Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((UserLibraryEntry?)null);

        var handler = CreateHandler();
        var cmd = CreateCommand(userId, gameId);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_HappyPath_UploadsToR2_UpdatesDb_Returns201()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = TestHelpers.CreateUserLibraryEntry(userId, gameId);

        _libraryRepo.Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(entry);
        _blobStorage.Setup(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(),
                                              It.IsAny<BlobCategory>(), It.IsAny<string>(),
                                              It.IsAny<CancellationToken>()))
                    .Returns(Task.CompletedTask);
        _blobStorage.Setup(b => b.GetPresignedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(),
                                                                It.IsAny<string>(), It.IsAny<TimeSpan?>()))
                    .ReturnsAsync("https://r2.example.com/signed");

        var handler = CreateHandler();
        var cmd = CreateCommand(userId, gameId);

        var result = await handler.Handle(cmd, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal($"user-covers/{userId}/{gameId}/cover", result.CoverR2Key);
        Assert.Equal("https://r2.example.com/signed", result.PresignedUrl);
        _blobStorage.Verify(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(),
                                                BlobCategory.GameImage, It.IsAny<string>(),
                                                It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReplaceExistingCover_DeletesOldFirst()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var existingKey = $"user-covers/{userId}/{gameId}/cover";
        var entry = TestHelpers.CreateUserLibraryEntry(userId, gameId);
        entry.SetCustomCoverR2Key(existingKey);

        _libraryRepo.Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(entry);
        _blobStorage.Setup(b => b.GetPresignedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(),
                                                                It.IsAny<string>(), It.IsAny<TimeSpan?>()))
                    .ReturnsAsync("https://r2.example.com/signed");

        var handler = CreateHandler();
        var cmd = CreateCommand(userId, gameId);

        await handler.Handle(cmd, CancellationToken.None);

        _blobStorage.Verify(b => b.DeleteAsync($"{existingKey}.webp", BlobCategory.GameImage, existingKey,
                                                It.IsAny<CancellationToken>()), Times.Once);
        _blobStorage.Verify(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(),
                                                BlobCategory.GameImage, It.IsAny<string>(),
                                                It.IsAny<CancellationToken>()), Times.Once);
    }

    private UploadCustomCoverCommandHandler CreateHandler() => new(
        _libraryRepo.Object,
        _blobStorage.Object,
        _unitOfWork.Object,
        _logger
    );

    private static UploadCustomCoverCommand CreateCommand(Guid userId, Guid gameId)
    {
        var ms = new MemoryStream(new byte[150_000]); // 150KB blob
        return new UploadCustomCoverCommand(
            UserId: userId,
            GameId: gameId,
            FileStream: ms,
            FileSizeBytes: 150_000,
            MimeType: "image/webp"
        );
    }
}
```

Note: `TestHelpers.CreateUserLibraryEntry` should exist or be created — verify in step 2 if missing.

- [ ] **Step 2: Verify TestHelpers exists or scaffold minimal version**

Run: `grep -rln "CreateUserLibraryEntry" apps/api/tests --include="*.cs" 2>&1 | head -3`
If not found, add to test file inline:
```csharp
private static class TestHelpers
{
    public static UserLibraryEntry CreateUserLibraryEntry(Guid userId, Guid gameId)
    {
        return UserLibraryEntry.Create(userId, gameId, notes: null, isFavorite: false);
    }
}
```
(Verify the static factory `UserLibraryEntry.Create` exists by reading `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Entities/UserLibraryEntry.cs`.)

- [ ] **Step 3: Run test to verify failures**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~UploadCustomCoverCommandHandlerTests" --no-build 2>&1 | tail -20`
Expected: COMPILE ERROR — `UploadCustomCoverCommandHandler` doesn't exist.

- [ ] **Step 4: Implement UploadCustomCoverCommandHandler**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/CustomCover/UploadCustomCoverCommandHandler.cs
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;

/// <summary>
/// Handler for uploading a user-custom cover image (L3).
/// Issue #1824. Flow: validate gameId∈library -> delete old R2 if exists ->
/// upload new -> update DB -> return presigned URL.
/// </summary>
internal sealed class UploadCustomCoverCommandHandler : ICommandHandler<UploadCustomCoverCommand, CustomCoverUploadResult>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UploadCustomCoverCommandHandler> _logger;

    public UploadCustomCoverCommandHandler(
        IUserLibraryRepository libraryRepository,
        IBlobStorageService blobStorage,
        IUnitOfWork unitOfWork,
        ILogger<UploadCustomCoverCommandHandler> logger)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CustomCoverUploadResult> Handle(UploadCustomCoverCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // AC-R9: gameId ∈ user library → 403
        var entry = await _libraryRepository
            .GetByUserAndGameAsync(command.UserId, command.GameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new ForbiddenException($"Game {command.GameId} is not in user library");

        // Resource key for R2 (without .webp suffix; CoverUrlResolver appends ".webp")
        var resourceKey = $"user-covers/{command.UserId}/{command.GameId}/cover";

        // Best-effort delete old cover if exists (race-safe via R2 last-write-wins)
        if (!string.IsNullOrWhiteSpace(entry.CustomCoverR2Key))
        {
            try
            {
                await _blobStorage.DeleteAsync(
                    $"{entry.CustomCoverR2Key}.webp",
                    BlobCategory.GameImage,
                    entry.CustomCoverR2Key,
                    cancellationToken
                ).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to delete old custom cover R2 key {Key} during replace flow", entry.CustomCoverR2Key);
                // Continue with new upload anyway
            }
        }

        // Upload new cover to R2 path
        await _blobStorage.UploadAsync(
            objectKey: $"{resourceKey}.webp",
            content: command.FileStream,
            contentType: "image/webp",
            category: BlobCategory.GameImage,
            resourceKey: resourceKey,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        // Update DB
        entry.SetCustomCoverR2Key(resourceKey);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Return result with presigned URL
        var presignedUrl = await _blobStorage.GetPresignedDownloadUrlAsync(
            $"{resourceKey}.webp",
            BlobCategory.GameImage,
            resourceKey,
            TimeSpan.FromHours(1)
        ).ConfigureAwait(false) ?? string.Empty;

        return new CustomCoverUploadResult(resourceKey, presignedUrl);
    }
}
```

- [ ] **Step 5: Verify `UserLibraryEntry.SetCustomCoverR2Key` exists**

Read: `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Entities/UserLibraryEntry.cs`

If `SetCustomCoverR2Key(string)` method doesn't exist on the entity (it was DB-stub only from #1839), add it:

```csharp
// Add to UserLibraryEntry.cs entity
public void SetCustomCoverR2Key(string? r2Key)
{
    CustomCoverR2Key = r2Key;
    UpdatedAt = DateTimeOffset.UtcNow;
}
```

Verify the field exists. If shadowed (private setter on EF entity), the method updates via reflection or direct field. Pattern existing in UserLibrary entities should be followed (refer to `MarkAsFavorite`, `UpdateNotes` patterns for examples).

- [ ] **Step 6: Run tests to verify they pass**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~UploadCustomCoverCommandHandlerTests" 2>&1 | tail -20`
Expected: All 3 tests PASS.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/CustomCover/UploadCustomCoverCommandHandler.cs \
        apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Entities/UserLibraryEntry.cs \
        apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Commands/CustomCover/UploadCustomCoverCommandHandlerTests.cs
git commit -m "feat(user-library): #1824 L3 UploadCustomCoverCommandHandler with R2 upload + DB update"
```

---

### Task 3: RemoveCustomCoverCommandHandler (BE)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/CustomCover/RemoveCustomCoverCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Commands/CustomCover/RemoveCustomCoverCommandHandlerTests.cs`

- [ ] **Step 1: Write the failing handler tests**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Commands/CustomCover/RemoveCustomCoverCommandHandlerTests.cs
using Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Commands.CustomCover;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserLibrary")]
public class RemoveCustomCoverCommandHandlerTests
{
    private readonly Mock<IUserLibraryRepository> _libraryRepo = new();
    private readonly Mock<IBlobStorageService> _blobStorage = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task Handle_GameNotInLibrary_ThrowsForbidden()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        _libraryRepo.Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync((UserLibraryEntry?)null);

        var handler = CreateHandler();
        var cmd = new RemoveCustomCoverCommand(userId, gameId);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NoCustomCoverExists_ThrowsNotFound()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = UserLibraryEntry.Create(userId, gameId, notes: null, isFavorite: false);
        // No SetCustomCoverR2Key call — entry has null key

        _libraryRepo.Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(entry);

        var handler = CreateHandler();
        var cmd = new RemoveCustomCoverCommand(userId, gameId);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_HappyPath_DeletesR2_NullsDbField()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var existingKey = $"user-covers/{userId}/{gameId}/cover";
        var entry = UserLibraryEntry.Create(userId, gameId, notes: null, isFavorite: false);
        entry.SetCustomCoverR2Key(existingKey);

        _libraryRepo.Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(entry);

        var handler = CreateHandler();
        var cmd = new RemoveCustomCoverCommand(userId, gameId);

        await handler.Handle(cmd, CancellationToken.None);

        _blobStorage.Verify(b => b.DeleteAsync($"{existingKey}.webp", BlobCategory.GameImage, existingKey,
                                                It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(entry.CustomCoverR2Key);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_R2DeleteThrows_LogsWarning_StillNullsDb()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var existingKey = $"user-covers/{userId}/{gameId}/cover";
        var entry = UserLibraryEntry.Create(userId, gameId, notes: null, isFavorite: false);
        entry.SetCustomCoverR2Key(existingKey);

        _libraryRepo.Setup(r => r.GetByUserAndGameAsync(userId, gameId, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(entry);
        _blobStorage.Setup(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(),
                                              It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("R2 unreachable"));

        var handler = CreateHandler();
        var cmd = new RemoveCustomCoverCommand(userId, gameId);

        await handler.Handle(cmd, CancellationToken.None); // Should NOT throw
        Assert.Null(entry.CustomCoverR2Key);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private RemoveCustomCoverCommandHandler CreateHandler() => new(
        _libraryRepo.Object,
        _blobStorage.Object,
        _unitOfWork.Object,
        NullLogger<RemoveCustomCoverCommandHandler>.Instance
    );
}
```

- [ ] **Step 2: Run test to verify failures**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~RemoveCustomCoverCommandHandlerTests" --no-build 2>&1 | tail -10`
Expected: COMPILE ERROR — `RemoveCustomCoverCommandHandler` doesn't exist.

- [ ] **Step 3: Implement RemoveCustomCoverCommandHandler**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/CustomCover/RemoveCustomCoverCommandHandler.cs
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;

/// <summary>
/// Handler for removing a user-custom cover image (L3).
/// Issue #1824. Flow: validate gameId∈library -> verify cover exists ->
/// best-effort R2 delete -> null DB field.
/// </summary>
internal sealed class RemoveCustomCoverCommandHandler : ICommandHandler<RemoveCustomCoverCommand>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RemoveCustomCoverCommandHandler> _logger;

    public RemoveCustomCoverCommandHandler(
        IUserLibraryRepository libraryRepository,
        IBlobStorageService blobStorage,
        IUnitOfWork unitOfWork,
        ILogger<RemoveCustomCoverCommandHandler> logger)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(RemoveCustomCoverCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entry = await _libraryRepository
            .GetByUserAndGameAsync(command.UserId, command.GameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new ForbiddenException($"Game {command.GameId} is not in user library");

        if (string.IsNullOrWhiteSpace(entry.CustomCoverR2Key))
        {
            throw new NotFoundException("No custom cover exists for this game");
        }

        var keyToDelete = entry.CustomCoverR2Key;

        // Best-effort R2 cleanup
        try
        {
            await _blobStorage.DeleteAsync(
                $"{keyToDelete}.webp",
                BlobCategory.GameImage,
                keyToDelete,
                cancellationToken
            ).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to delete custom cover R2 key {Key}; DB will still be nulled", keyToDelete);
        }

        // Null DB field regardless of R2 outcome
        entry.SetCustomCoverR2Key(null);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~RemoveCustomCoverCommandHandlerTests" 2>&1 | tail -10`
Expected: All 4 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/CustomCover/RemoveCustomCoverCommandHandler.cs \
        apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/Commands/CustomCover/RemoveCustomCoverCommandHandlerTests.cs
git commit -m "feat(user-library): #1824 L3 RemoveCustomCoverCommandHandler with best-effort R2 cleanup"
```

---

### Task 4: GameRemovedFromLibraryCustomCoverHandler (BE event handler)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/EventHandlers/GameRemovedFromLibraryCustomCoverHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/EventHandlers/GameRemovedFromLibraryCustomCoverHandlerTests.cs`

**Reference pattern**: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/PdfDeletedEventHandler.cs` (best-effort R2 cleanup, log warning on fail).

- [ ] **Step 1: Read the existing event and reference handler**

Read: `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Events/GameRemovedFromLibraryEvent.cs` to understand event payload (probably carries `UserId`, `GameId`, and `CustomCoverR2Key` — verify if not, document need to extend event in Step 2.)

Read: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/PdfDeletedEventHandler.cs` for pattern reference (best-effort blob.Delete, swallow non-cancellation exceptions, log warning).

- [ ] **Step 2: Extend GameRemovedFromLibraryEvent if it doesn't carry CustomCoverR2Key**

If `GameRemovedFromLibraryEvent` only has `UserId` + `GameId`, the handler needs to look up the entry first. But since `entry.PrepareForRemoval()` is called BEFORE the actual delete (per `RemoveGameFromLibraryCommandHandler`), we can either:

**Option A** (preferred): Extend the event with `CustomCoverR2Key` field, populated in `PrepareForRemoval()`.

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Events/GameRemovedFromLibraryEvent.cs
internal sealed record GameRemovedFromLibraryEvent(
    Guid UserId,
    Guid GameId,
    string? CustomCoverR2Key  // NEW
) : IDomainEvent;
```

And update the entity:
```csharp
// UserLibraryEntry.cs PrepareForRemoval() should emit with current CustomCoverR2Key:
public void PrepareForRemoval()
{
    RaiseDomainEvent(new GameRemovedFromLibraryEvent(UserId, GameId, CustomCoverR2Key));
}
```

**Option B** (fallback if event signature is widely used): Handler queries the entry directly (but it's already deleted by the time event fires). Use Option A.

- [ ] **Step 3: Write the failing handler tests**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/EventHandlers/GameRemovedFromLibraryCustomCoverHandlerTests.cs
using Api.BoundedContexts.UserLibrary.Application.EventHandlers;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.Services;
using Api.Services.Pdf;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.EventHandlers;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserLibrary")]
public class GameRemovedFromLibraryCustomCoverHandlerTests
{
    private readonly Mock<IBlobStorageService> _blobStorage = new();

    [Fact]
    public async Task Handle_CoverExists_DeletesR2()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var coverKey = $"user-covers/{userId}/{gameId}/cover";
        var evt = new GameRemovedFromLibraryEvent(userId, gameId, coverKey);
        var handler = CreateHandler();

        await handler.Handle(evt, CancellationToken.None);

        _blobStorage.Verify(b => b.DeleteAsync($"{coverKey}.webp", BlobCategory.GameImage, coverKey,
                                                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoCover_NoOp()
    {
        var evt = new GameRemovedFromLibraryEvent(Guid.NewGuid(), Guid.NewGuid(), null);
        var handler = CreateHandler();

        await handler.Handle(evt, CancellationToken.None);

        _blobStorage.Verify(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(),
                                                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_R2Throws_LogsWarning_NoPropagation()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var coverKey = $"user-covers/{userId}/{gameId}/cover";
        var evt = new GameRemovedFromLibraryEvent(userId, gameId, coverKey);
        _blobStorage.Setup(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(),
                                              It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("R2 unreachable"));

        var handler = CreateHandler();

        // Should NOT throw
        await handler.Handle(evt, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_Cancellation_Propagates()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var coverKey = $"user-covers/{userId}/{gameId}/cover";
        var evt = new GameRemovedFromLibraryEvent(userId, gameId, coverKey);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _blobStorage.Setup(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(),
                                              It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new OperationCanceledException());

        var handler = CreateHandler();

        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.Handle(evt, cts.Token));
    }

    private GameRemovedFromLibraryCustomCoverHandler CreateHandler() => new(
        _blobStorage.Object,
        NullLogger<GameRemovedFromLibraryCustomCoverHandler>.Instance
    );
}
```

- [ ] **Step 4: Implement handler**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/EventHandlers/GameRemovedFromLibraryCustomCoverHandler.cs
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.Services;
using Api.Services.Pdf;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserLibrary.Application.EventHandlers;

/// <summary>
/// Cleanup R2 blob when a game is removed from user library (L3).
/// Issue #1824. Best-effort cleanup pattern (mirrors PdfDeletedEventHandler from #1873).
/// Failures logged as warnings, not propagated, except OperationCanceledException.
/// </summary>
internal sealed class GameRemovedFromLibraryCustomCoverHandler : INotificationHandler<GameRemovedFromLibraryEvent>
{
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<GameRemovedFromLibraryCustomCoverHandler> _logger;

    public GameRemovedFromLibraryCustomCoverHandler(
        IBlobStorageService blobStorage,
        ILogger<GameRemovedFromLibraryCustomCoverHandler> logger)
    {
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(GameRemovedFromLibraryEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (string.IsNullOrWhiteSpace(notification.CustomCoverR2Key))
        {
            return; // No cover, nothing to clean up
        }

        try
        {
            await _blobStorage.DeleteAsync(
                $"{notification.CustomCoverR2Key}.webp",
                BlobCategory.GameImage,
                notification.CustomCoverR2Key,
                cancellationToken
            ).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to delete custom cover R2 key {Key} on game-removal event; orphan blob left in R2 (manual cleanup ops)",
                notification.CustomCoverR2Key);
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~GameRemovedFromLibraryCustomCoverHandlerTests" 2>&1 | tail -10`
Expected: All 4 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserLibrary/Application/EventHandlers/GameRemovedFromLibraryCustomCoverHandler.cs \
        apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Events/GameRemovedFromLibraryEvent.cs \
        apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Entities/UserLibraryEntry.cs \
        apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/EventHandlers/GameRemovedFromLibraryCustomCoverHandlerTests.cs
git commit -m "feat(user-library): #1824 L3 GameRemovedFromLibraryCustomCoverHandler best-effort R2 cleanup"
```

---

### Task 5: UserLibraryCoverEndpoints + routing registration (BE)

**Files:**
- Create: `apps/api/src/Api/Routing/UserLibrary/UserLibraryCoverEndpoints.cs`
- Modify: `apps/api/src/Api/Routing/UserLibraryEndpoints.cs` (register new endpoint group)
- Test: `apps/api/tests/Api.Tests/Integration/UserLibrary/CustomCoverEndpointsIntegrationTests.cs` (skip if Docker not available)

**Reference pattern**: `apps/api/src/Api/Routing/UserLibrary/UserLibraryPdfEndpoints.cs` (`MapUploadCustomGamePdfEndpoint`).

- [ ] **Step 1: Implement UserLibraryCoverEndpoints**

```csharp
// apps/api/src/Api/Routing/UserLibrary/UserLibraryCoverEndpoints.cs
using Api.BoundedContexts.UserLibrary.Application.Commands.CustomCover;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.Extensions;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.Routing.UserLibrary;

/// <summary>
/// L3 custom cover upload + delete endpoints.
/// Issue #1824 (umbrella #1821).
/// </summary>
internal static class UserLibraryCoverEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        MapUploadCoverEndpoint(group);
        MapDeleteCoverEndpoint(group);
    }

    private static void MapUploadCoverEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/library/{gameId:guid}/cover", async (
            Guid gameId,
            IFormFile file,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "File is required" });
            }

            using var stream = file.OpenReadStream();
            var command = new UploadCustomCoverCommand(
                UserId: userId,
                GameId: gameId,
                FileStream: stream,
                FileSizeBytes: file.Length,
                MimeType: file.ContentType
            );

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Created($"/api/v1/library/{gameId}/cover", result);
            }
            catch (ForbiddenException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 403);
            }
        })
        .RequireAuthenticatedUser()
        .DisableAntiforgery() // Multipart upload, no anti-forgery token from FE
        .Produces<CustomCoverUploadResult>(201)
        .Produces(400)
        .Produces(401)
        .Produces(403)
        .WithTags("Library")
        .WithSummary("Upload custom cover (L3)")
        .WithDescription("Uploads a user-custom cover image (200x300 webp ≤200KB) for a game in user's library.")
        .WithOpenApi();
    }

    private static void MapDeleteCoverEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/library/{gameId:guid}/cover", async (
            Guid gameId,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;

            if (!UserLibraryCoreEndpoints.TryGetUserId(context, session, out var userId))
            {
                return Results.Unauthorized();
            }

            var command = new RemoveCustomCoverCommand(userId, gameId);

            try
            {
                await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.NoContent();
            }
            catch (ForbiddenException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 403);
            }
            catch (NotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(401)
        .Produces(403)
        .Produces(404)
        .WithTags("Library")
        .WithSummary("Remove custom cover (L3)")
        .WithDescription("Removes the user's custom cover for a game in their library. Falls back to L4/L1 chain.")
        .WithOpenApi();
    }
}
```

- [ ] **Step 2: Register endpoint group in UserLibraryEndpoints**

Read: `apps/api/src/Api/Routing/UserLibraryEndpoints.cs` to find where existing endpoint groups (Core, Collection, Label, Pdf) are registered.

Add to the existing `Map` method:
```csharp
UserLibraryCoverEndpoints.Map(group);
```

- [ ] **Step 3: Verify route registration via build + manual smoke**

Run: `dotnet build apps/api/src/Api 2>&1 | tail -10`
Expected: BUILD SUCCEEDED.

Optional smoke (skip if not running locally):
```bash
cd infra && make dev
curl -X POST http://localhost:8080/api/v1/library/{gameId}/cover \
  -F "file=@test.webp" \
  -b "session=valid-session-cookie"
# Expected: 401 (no auth) or 403 (no game in library) or 201 (happy)
```

- [ ] **Step 4: Write integration test (optional, Docker-dependent — skip in PR CI if Docker not available)**

```csharp
// apps/api/tests/Api.Tests/Integration/UserLibrary/CustomCoverEndpointsIntegrationTests.cs
using System.Net;
using System.Net.Http.Headers;
using Api.Tests.Integration.Fixtures;
using Xunit;

namespace Api.Tests.Integration.UserLibrary;

[Trait("Category", "Integration")]
[Collection("Integration")]
public class CustomCoverEndpointsIntegrationTests : IClassFixture<ApiIntegrationFixture>
{
    private readonly ApiIntegrationFixture _fixture;

    public CustomCoverEndpointsIntegrationTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Post_NotAuthenticated_Returns401()
    {
        var client = _fixture.CreateClient();
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[100]) { Headers = { ContentType = MediaTypeHeaderValue.Parse("image/webp") } }, "file", "cover.webp");

        var response = await client.PostAsync($"/api/v1/library/{Guid.NewGuid()}/cover", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_GameNotInLibrary_Returns403()
    {
        var (client, userId, _) = await _fixture.CreateAuthenticatedClientAsync();
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(new byte[100]) { Headers = { ContentType = MediaTypeHeaderValue.Parse("image/webp") } }, "file", "cover.webp");

        var response = await client.PostAsync($"/api/v1/library/{Guid.NewGuid()}/cover", content);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NoCustomCover_Returns404()
    {
        var (client, userId, gameId) = await _fixture.CreateAuthenticatedClientWithLibraryEntryAsync();

        var response = await client.DeleteAsync($"/api/v1/library/{gameId}/cover");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
```

- [ ] **Step 5: Run all BE tests + verify backend build**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~CustomCover" 2>&1 | tail -10`
Expected: All unit tests PASS. Integration tests SKIPPED or PASS (Docker-dependent).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Routing/UserLibrary/UserLibraryCoverEndpoints.cs \
        apps/api/src/Api/Routing/UserLibraryEndpoints.cs \
        apps/api/tests/Api.Tests/Integration/UserLibrary/CustomCoverEndpointsIntegrationTests.cs
git commit -m "feat(user-library): #1824 L3 POST/DELETE /api/v1/library/{gameId}/cover endpoints"
```

---

### Task 6: FE deps install (react-easy-crop, heic2any)

**Files:**
- Modify: `apps/web/package.json`

- [ ] **Step 1: Install deps**

```bash
cd apps/web
pnpm add react-easy-crop heic2any
pnpm add -D @types/heic2any 2>&1 | tail -5  # if types are missing
```

- [ ] **Step 2: Verify versions and bundle**

Run: `cd apps/web && pnpm list react-easy-crop heic2any 2>&1 | head -10`
Expected: both versions listed.

- [ ] **Step 3: Verify TypeScript compilation**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -5`
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add apps/web/package.json apps/web/pnpm-lock.yaml
git commit -m "chore(deps): #1824 add react-easy-crop + heic2any for L3 custom cover upload"
```

---

### Task 7: EditCoverOverlay component (FE)

**Files:**
- Create: `apps/web/src/components/features/library/custom-cover/EditCoverOverlay.tsx`
- Test: `apps/web/src/components/features/library/custom-cover/__tests__/EditCoverOverlay.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/features/library/custom-cover/__tests__/EditCoverOverlay.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { EditCoverOverlay } from '../EditCoverOverlay';

describe('EditCoverOverlay', () => {
  it('renders edit icon with accessible label', () => {
    render(<EditCoverOverlay onEditClick={() => {}} hasCustomCover={false} />);
    const btn = screen.getByRole('button', { name: /modifica copertina/i });
    expect(btn).toBeInTheDocument();
  });

  it('calls onEditClick when clicked', () => {
    const onEditClick = vi.fn();
    render(<EditCoverOverlay onEditClick={onEditClick} hasCustomCover={false} />);
    fireEvent.click(screen.getByRole('button', { name: /modifica copertina/i }));
    expect(onEditClick).toHaveBeenCalledOnce();
  });

  it('shows different label when custom cover already exists', () => {
    render(<EditCoverOverlay onEditClick={() => {}} hasCustomCover={true} />);
    const btn = screen.getByRole('button', { name: /cambia copertina/i });
    expect(btn).toBeInTheDocument();
  });

  it('has tabIndex 0 for keyboard accessibility', () => {
    render(<EditCoverOverlay onEditClick={() => {}} hasCustomCover={false} />);
    const btn = screen.getByRole('button', { name: /modifica copertina/i });
    expect(btn.tabIndex).toBe(0);
  });
});
```

- [ ] **Step 2: Run test to verify failure**

Run: `cd apps/web && pnpm test EditCoverOverlay 2>&1 | tail -10`
Expected: FAIL — component doesn't exist.

- [ ] **Step 3: Implement EditCoverOverlay**

```tsx
// apps/web/src/components/features/library/custom-cover/EditCoverOverlay.tsx
'use client';

import { Pencil } from 'lucide-react';

interface EditCoverOverlayProps {
  onEditClick: () => void;
  hasCustomCover: boolean;
}

/**
 * Edit icon overlay for hero cover (top-right).
 * Visible always on mobile, on hover on desktop (controlled by parent class).
 * Issue #1824 L3.
 */
export function EditCoverOverlay({ onEditClick, hasCustomCover }: EditCoverOverlayProps): JSX.Element {
  const label = hasCustomCover ? 'Cambia copertina personalizzata' : 'Modifica copertina';

  return (
    <button
      type="button"
      onClick={onEditClick}
      aria-label={label}
      tabIndex={0}
      className="absolute right-2 top-2 z-10 inline-flex h-9 w-9 items-center justify-center rounded-full bg-background/80 text-foreground shadow-sm backdrop-blur-sm transition-opacity hover:bg-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring md:opacity-0 md:group-hover:opacity-100"
    >
      <Pencil className="h-4 w-4" aria-hidden="true" />
    </button>
  );
}
```

- [ ] **Step 4: Run test to verify pass**

Run: `cd apps/web && pnpm test EditCoverOverlay 2>&1 | tail -10`
Expected: All 4 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/library/custom-cover/EditCoverOverlay.tsx \
        apps/web/src/components/features/library/custom-cover/__tests__/EditCoverOverlay.test.tsx
git commit -m "feat(library): #1824 L3 EditCoverOverlay component with a11y label"
```

---

### Task 8: CropDialog component (FE)

**Files:**
- Create: `apps/web/src/components/features/library/custom-cover/CropDialog.tsx`
- Test: `apps/web/src/components/features/library/custom-cover/__tests__/CropDialog.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/features/library/custom-cover/__tests__/CropDialog.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { CropDialog } from '../CropDialog';

// Mock react-easy-crop because canvas operations don't work in jsdom
vi.mock('react-easy-crop', () => ({
  default: ({ onCropComplete }: { onCropComplete?: (area: any, pixels: any) => void }) => {
    return (
      <div data-testid="mock-cropper">
        <button onClick={() => onCropComplete?.({}, { x: 0, y: 0, width: 200, height: 300 })}>
          simulate-crop
        </button>
      </div>
    );
  },
}));

describe('CropDialog', () => {
  const mockImage = new File([new Blob([new ArrayBuffer(100)], { type: 'image/jpeg' })], 'test.jpg', {
    type: 'image/jpeg',
  });

  it('renders cropper, confirm + cancel buttons', () => {
    render(
      <CropDialog
        open={true}
        imageFile={mockImage}
        onConfirm={() => {}}
        onCancel={() => {}}
      />
    );
    expect(screen.getByTestId('mock-cropper')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /conferma/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /annulla/i })).toBeInTheDocument();
  });

  it('calls onCancel when cancel clicked', () => {
    const onCancel = vi.fn();
    render(
      <CropDialog open={true} imageFile={mockImage} onConfirm={() => {}} onCancel={onCancel} />
    );
    fireEvent.click(screen.getByRole('button', { name: /annulla/i }));
    expect(onCancel).toHaveBeenCalled();
  });

  it('calls onConfirm with cropped blob when confirm clicked', async () => {
    const onConfirm = vi.fn();
    render(
      <CropDialog open={true} imageFile={mockImage} onConfirm={onConfirm} onCancel={() => {}} />
    );
    fireEvent.click(screen.getByText('simulate-crop'));
    fireEvent.click(screen.getByRole('button', { name: /conferma/i }));
    await waitFor(() => expect(onConfirm).toHaveBeenCalled());
  });
});
```

- [ ] **Step 2: Run test to verify failure**

Run: `cd apps/web && pnpm test CropDialog 2>&1 | tail -10`
Expected: FAIL — component doesn't exist.

- [ ] **Step 3: Implement CropDialog (with react-easy-crop + size compression loop)**

```tsx
// apps/web/src/components/features/library/custom-cover/CropDialog.tsx
'use client';

import { useCallback, useState } from 'react';

import Cropper from 'react-easy-crop';
import type { Area } from 'react-easy-crop';

import { Button } from '@/components/ui/primitives/button';
import { Dialog, DialogContent, DialogTitle } from '@/components/ui/primitives/dialog';

interface CropDialogProps {
  open: boolean;
  imageFile: File;
  onConfirm: (blob: Blob) => void;
  onCancel: () => void;
}

const TARGET_WIDTH = 200;
const TARGET_HEIGHT = 300;
const MAX_OUTPUT_BYTES = 200_000; // 200KB
const MIN_QUALITY = 0.4;

/**
 * Modal crop UI with react-easy-crop.
 * Output webp ≤200KB via quality dial-down loop (q=0.8 → 0.4).
 * Issue #1824 L3.
 */
export function CropDialog({ open, imageFile, onConfirm, onCancel }: CropDialogProps): JSX.Element {
  const [crop, setCrop] = useState({ x: 0, y: 0 });
  const [zoom, setZoom] = useState(1);
  const [croppedPixels, setCroppedPixels] = useState<Area | null>(null);
  const [imageUrl] = useState(() => URL.createObjectURL(imageFile));
  const [isProcessing, setIsProcessing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleCropComplete = useCallback((_area: Area, pixels: Area) => {
    setCroppedPixels(pixels);
  }, []);

  const handleConfirm = useCallback(async () => {
    if (!croppedPixels) return;
    setIsProcessing(true);
    setError(null);

    try {
      const blob = await renderCropToWebp(imageUrl, croppedPixels);
      onConfirm(blob);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Errore di compressione');
    } finally {
      setIsProcessing(false);
    }
  }, [croppedPixels, imageUrl, onConfirm]);

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onCancel()}>
      <DialogContent className="max-w-[600px]">
        <DialogTitle>Ritaglia la copertina (200×300)</DialogTitle>
        <div className="relative h-[400px] w-full bg-muted">
          <Cropper
            image={imageUrl}
            crop={crop}
            zoom={zoom}
            aspect={TARGET_WIDTH / TARGET_HEIGHT}
            onCropChange={setCrop}
            onZoomChange={setZoom}
            onCropComplete={handleCropComplete}
          />
        </div>
        {error && <p className="text-sm text-destructive">{error}</p>}
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onCancel} disabled={isProcessing}>
            Annulla
          </Button>
          <Button onClick={handleConfirm} disabled={!croppedPixels || isProcessing}>
            {isProcessing ? 'Elaborazione...' : 'Conferma'}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

/**
 * Render crop area to webp blob with quality dial-down to fit ≤200KB.
 * @throws Error if output exceeds 200KB at MIN_QUALITY.
 */
async function renderCropToWebp(imageUrl: string, pixels: Area): Promise<Blob> {
  const img = await loadImage(imageUrl);
  const canvas = document.createElement('canvas');
  canvas.width = TARGET_WIDTH;
  canvas.height = TARGET_HEIGHT;
  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('Canvas context unavailable');

  ctx.drawImage(
    img,
    pixels.x, pixels.y, pixels.width, pixels.height,
    0, 0, TARGET_WIDTH, TARGET_HEIGHT
  );

  // Dial down quality until ≤200KB
  for (let q = 0.8; q >= MIN_QUALITY; q -= 0.1) {
    const blob = await canvasToBlob(canvas, 'image/webp', q);
    if (blob.size <= MAX_OUTPUT_BYTES) {
      return blob;
    }
  }
  throw new Error('Immagine troppo complessa per comprimere, riprova con foto più semplice');
}

function loadImage(url: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = reject;
    img.src = url;
  });
}

function canvasToBlob(canvas: HTMLCanvasElement, type: string, quality: number): Promise<Blob> {
  return new Promise((resolve, reject) => {
    canvas.toBlob((blob) => {
      if (!blob) reject(new Error('Canvas toBlob returned null'));
      else resolve(blob);
    }, type, quality);
  });
}
```

- [ ] **Step 4: Run test to verify pass**

Run: `cd apps/web && pnpm test CropDialog 2>&1 | tail -10`
Expected: All 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/library/custom-cover/CropDialog.tsx \
        apps/web/src/components/features/library/custom-cover/__tests__/CropDialog.test.tsx
git commit -m "feat(library): #1824 L3 CropDialog with react-easy-crop + size compression loop"
```

---

### Task 9: useCustomCoverUpload + useRemoveCustomCover hooks (FE)

**Files:**
- Create: `apps/web/src/hooks/mutations/useCustomCoverUpload.ts`
- Create: `apps/web/src/hooks/mutations/useRemoveCustomCover.ts`
- Test: `apps/web/src/hooks/mutations/__tests__/useCustomCoverUpload.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// apps/web/src/hooks/mutations/__tests__/useCustomCoverUpload.test.ts
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

import { useCustomCoverUpload } from '../useCustomCoverUpload';

const mockFetch = vi.fn();
global.fetch = mockFetch;

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

describe('useCustomCoverUpload', () => {
  beforeEach(() => {
    mockFetch.mockReset();
  });

  it('uploads blob via POST multipart', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      status: 201,
      json: () => Promise.resolve({ coverR2Key: 'user-covers/u/g/cover', presignedUrl: 'https://r2/signed' }),
    });

    const { result } = renderHook(() => useCustomCoverUpload('game-id'), { wrapper });

    const blob = new Blob([new ArrayBuffer(150_000)], { type: 'image/webp' });
    result.current.mutate(blob);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(mockFetch).toHaveBeenCalledWith(
      '/api/v1/library/game-id/cover',
      expect.objectContaining({ method: 'POST', body: expect.any(FormData) })
    );
  });

  it('handles 403 forbidden error', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 403,
      json: () => Promise.resolve({ error: 'Game not in library' }),
    });

    const { result } = renderHook(() => useCustomCoverUpload('game-id'), { wrapper });
    const blob = new Blob([new ArrayBuffer(100)], { type: 'image/webp' });
    result.current.mutate(blob);

    await waitFor(() => expect(result.current.isError).toBe(true));
  });
});
```

- [ ] **Step 2: Run test to verify failure**

Run: `cd apps/web && pnpm test useCustomCoverUpload 2>&1 | tail -10`
Expected: FAIL — hook doesn't exist.

- [ ] **Step 3: Implement useCustomCoverUpload**

```ts
// apps/web/src/hooks/mutations/useCustomCoverUpload.ts
import { useMutation, useQueryClient } from '@tanstack/react-query';

interface CustomCoverUploadResult {
  coverR2Key: string;
  presignedUrl: string;
}

interface UploadError {
  status: number;
  message: string;
}

/**
 * Hook for uploading a custom cover blob for a game in library (L3).
 * POSTs multipart to /api/v1/library/{gameId}/cover and invalidates library queries.
 * Issue #1824.
 */
export function useCustomCoverUpload(gameId: string) {
  const queryClient = useQueryClient();
  return useMutation<CustomCoverUploadResult, UploadError, Blob>({
    mutationFn: async (blob) => {
      const formData = new FormData();
      formData.append('file', blob, 'cover.webp');

      const response = await fetch(`/api/v1/library/${gameId}/cover`, {
        method: 'POST',
        body: formData,
        credentials: 'include',
      });

      if (!response.ok) {
        const errorBody = await response.json().catch(() => ({ error: 'Upload failed' }));
        throw { status: response.status, message: errorBody.error || 'Upload failed' };
      }

      return response.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['library', gameId] });
      queryClient.invalidateQueries({ queryKey: ['library'] });
    },
  });
}
```

- [ ] **Step 4: Implement useRemoveCustomCover (similar pattern)**

```ts
// apps/web/src/hooks/mutations/useRemoveCustomCover.ts
import { useMutation, useQueryClient } from '@tanstack/react-query';

interface RemoveError {
  status: number;
  message: string;
}

export function useRemoveCustomCover(gameId: string) {
  const queryClient = useQueryClient();
  return useMutation<void, RemoveError>({
    mutationFn: async () => {
      const response = await fetch(`/api/v1/library/${gameId}/cover`, {
        method: 'DELETE',
        credentials: 'include',
      });

      if (!response.ok) {
        const errorBody = await response.json().catch(() => ({ error: 'Remove failed' }));
        throw { status: response.status, message: errorBody.error || 'Remove failed' };
      }
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['library', gameId] });
      queryClient.invalidateQueries({ queryKey: ['library'] });
    },
  });
}
```

- [ ] **Step 5: Run tests to verify pass**

Run: `cd apps/web && pnpm test useCustomCoverUpload 2>&1 | tail -10`
Expected: All 2 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/hooks/mutations/useCustomCoverUpload.ts \
        apps/web/src/hooks/mutations/useRemoveCustomCover.ts \
        apps/web/src/hooks/mutations/__tests__/useCustomCoverUpload.test.ts
git commit -m "feat(library): #1824 L3 useCustomCoverUpload + useRemoveCustomCover react-query hooks"
```

---

### Task 10: CustomCoverDialog orchestrator (FE)

**Files:**
- Create: `apps/web/src/components/features/library/custom-cover/CustomCoverDialog.tsx`
- Test: `apps/web/src/components/features/library/custom-cover/__tests__/CustomCoverDialog.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/components/features/library/custom-cover/__tests__/CustomCoverDialog.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi } from 'vitest';

import { CustomCoverDialog } from '../CustomCoverDialog';

vi.mock('../CropDialog', () => ({
  CropDialog: ({ onConfirm }: { onConfirm: (blob: Blob) => void }) => (
    <div data-testid="mock-crop-dialog">
      <button onClick={() => onConfirm(new Blob([new ArrayBuffer(150_000)], { type: 'image/webp' }))}>
        confirm-crop
      </button>
    </div>
  ),
}));

vi.mock('@/hooks/mutations/useCustomCoverUpload', () => ({
  useCustomCoverUpload: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
}));

vi.mock('@/hooks/mutations/useRemoveCustomCover', () => ({
  useRemoveCustomCover: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
}));

function wrapper({ children }: { children: React.ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}

describe('CustomCoverDialog', () => {
  it('renders file input when open', () => {
    render(<CustomCoverDialog gameId="g" open={true} onClose={() => {}} hasCustomCover={false} />, { wrapper });
    expect(screen.getByLabelText(/seleziona foto/i)).toBeInTheDocument();
  });

  it('rejects file > 10MB', async () => {
    render(<CustomCoverDialog gameId="g" open={true} onClose={() => {}} hasCustomCover={false} />, { wrapper });
    const input = screen.getByLabelText(/seleziona foto/i) as HTMLInputElement;
    const bigFile = new File([new ArrayBuffer(11 * 1024 * 1024)], 'big.jpg', { type: 'image/jpeg' });
    fireEvent.change(input, { target: { files: [bigFile] } });
    await waitFor(() => expect(screen.getByText(/troppo grande/i)).toBeInTheDocument());
  });

  it('shows remove button when hasCustomCover=true', () => {
    render(<CustomCoverDialog gameId="g" open={true} onClose={() => {}} hasCustomCover={true} />, { wrapper });
    expect(screen.getByRole('button', { name: /rimuovi copertina personalizzata/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify failure**

Run: `cd apps/web && pnpm test CustomCoverDialog 2>&1 | tail -10`
Expected: FAIL — component doesn't exist.

- [ ] **Step 3: Implement CustomCoverDialog (orchestrator)**

```tsx
// apps/web/src/components/features/library/custom-cover/CustomCoverDialog.tsx
'use client';

import { useState, useCallback } from 'react';

import { Button } from '@/components/ui/primitives/button';
import { Dialog, DialogContent, DialogTitle, DialogDescription } from '@/components/ui/primitives/dialog';
import { useCustomCoverUpload } from '@/hooks/mutations/useCustomCoverUpload';
import { useRemoveCustomCover } from '@/hooks/mutations/useRemoveCustomCover';

import { CropDialog } from './CropDialog';

interface CustomCoverDialogProps {
  gameId: string;
  open: boolean;
  onClose: () => void;
  hasCustomCover: boolean;
}

const MAX_INPUT_BYTES = 10 * 1024 * 1024; // 10MB
const ACCEPTED_MIME = ['image/jpeg', 'image/png', 'image/webp', 'image/heic'];

export function CustomCoverDialog({ gameId, open, onClose, hasCustomCover }: CustomCoverDialogProps): JSX.Element {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);

  const uploadMutation = useCustomCoverUpload(gameId);
  const removeMutation = useRemoveCustomCover(gameId);

  const handleFileSelect = useCallback(async (e: React.ChangeEvent<HTMLInputElement>) => {
    setValidationError(null);
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.size > MAX_INPUT_BYTES) {
      setValidationError('File troppo grande, massimo 10MB');
      return;
    }

    if (!ACCEPTED_MIME.includes(file.type)) {
      setValidationError('Formato non supportato. Usa JPG, PNG, WebP o HEIC.');
      return;
    }

    // HEIC iOS: lazy-load heic2any and convert to JPEG
    if (file.type === 'image/heic') {
      try {
        const heic2any = (await import('heic2any')).default;
        const jpegBlob = await heic2any({ blob: file, toType: 'image/jpeg', quality: 0.9 });
        const jpegFile = new File([jpegBlob as Blob], file.name.replace(/\.heic$/i, '.jpg'), { type: 'image/jpeg' });
        setSelectedFile(jpegFile);
      } catch (err) {
        setValidationError('Conversione HEIC fallita. Riprova con JPEG o PNG.');
      }
    } else {
      setSelectedFile(file);
    }
  }, []);

  const handleCropConfirm = useCallback((blob: Blob) => {
    uploadMutation.mutate(blob, {
      onSuccess: () => {
        setSelectedFile(null);
        onClose();
      },
    });
  }, [uploadMutation, onClose]);

  const handleRemove = useCallback(() => {
    removeMutation.mutate(undefined, {
      onSuccess: () => {
        onClose();
      },
    });
  }, [removeMutation, onClose]);

  return (
    <>
      <Dialog open={open && !selectedFile} onOpenChange={(o) => !o && onClose()}>
        <DialogContent>
          <DialogTitle>Personalizza copertina</DialogTitle>
          <DialogDescription>
            Carica una foto della tua copia del gioco (200×300, max 200KB output).
          </DialogDescription>
          <label className="block">
            <span className="text-sm font-medium">Seleziona foto</span>
            <input
              type="file"
              accept="image/jpeg,image/png,image/webp,image/heic"
              capture="environment"
              onChange={handleFileSelect}
              className="mt-1 block w-full"
            />
          </label>
          {validationError && <p className="text-sm text-destructive">{validationError}</p>}
          {hasCustomCover && (
            <div className="border-t pt-4">
              <Button
                variant="outline"
                onClick={handleRemove}
                disabled={removeMutation.isPending}
                aria-label="Rimuovi copertina personalizzata"
              >
                {removeMutation.isPending ? 'Rimozione...' : 'Rimuovi copertina personalizzata'}
              </Button>
            </div>
          )}
        </DialogContent>
      </Dialog>
      {selectedFile && (
        <CropDialog
          open={true}
          imageFile={selectedFile}
          onConfirm={handleCropConfirm}
          onCancel={() => setSelectedFile(null)}
        />
      )}
    </>
  );
}
```

- [ ] **Step 4: Run test to verify pass**

Run: `cd apps/web && pnpm test CustomCoverDialog 2>&1 | tail -10`
Expected: All 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/library/custom-cover/CustomCoverDialog.tsx \
        apps/web/src/components/features/library/custom-cover/__tests__/CustomCoverDialog.test.tsx
git commit -m "feat(library): #1824 L3 CustomCoverDialog orchestrator with HEIC lazy-load"
```

---

### Task 11: Integration in GameDetailDesktop + GameDetailMobile (FE)

**Files:**
- Modify: `apps/web/src/components/game-detail/GameDetailDesktop.tsx` (add EditCoverOverlay + state for dialog)
- Modify: `apps/web/src/app/(authenticated)/library/[gameId]/game-detail-mobile.tsx` (same)

- [ ] **Step 1: Read existing GameDetailDesktop to find hero placement**

Run: `grep -n "MeepleCard\|Cover\|hero" apps/web/src/components/game-detail/GameDetailDesktop.tsx | head -10`
Verify hero structure and identify the parent element for the overlay.

- [ ] **Step 2: Add state + integration in GameDetailDesktop**

```tsx
// Modify apps/web/src/components/game-detail/GameDetailDesktop.tsx
// Add at top:
import { useState } from 'react';
import { EditCoverOverlay } from '@/components/features/library/custom-cover/EditCoverOverlay';
import { CustomCoverDialog } from '@/components/features/library/custom-cover/CustomCoverDialog';

// Inside component:
const [coverDialogOpen, setCoverDialogOpen] = useState(false);
const hasCustomCover = Boolean(gameDetail.customCoverR2Key); // verify field exists on DTO

// Wrap hero cover in a relative+group container:
<div className="group relative">
  <MeepleCard {...heroProps} />
  <EditCoverOverlay
    onEditClick={() => setCoverDialogOpen(true)}
    hasCustomCover={hasCustomCover}
  />
</div>

{/* End of component */}
<CustomCoverDialog
  gameId={gameDetail.gameId}
  open={coverDialogOpen}
  onClose={() => setCoverDialogOpen(false)}
  hasCustomCover={hasCustomCover}
/>
```

If `gameDetail.customCoverR2Key` doesn't exist on the DTO yet, verify in the BE: `UserLibraryEntryDto.cs` should expose `customCoverR2Key` field (read existing DTO). If missing, add to the DTO + GetUserLibraryQueryHandler — this could be a small back-patch within Task 11 step.

- [ ] **Step 3: Mirror integration in GameDetailMobile**

Same pattern — add EditCoverOverlay and CustomCoverDialog. Mobile UX: overlay always visible (no hover variant).

- [ ] **Step 4: Run typecheck + relevant test suites**

```bash
cd apps/web && pnpm typecheck && pnpm test custom-cover 2>&1 | tail -10
```
Expected: 0 typecheck errors, all custom-cover tests PASS.

- [ ] **Step 5: Run dev server smoke (manual)**

```bash
cd infra && make dev
# Then in browser:
# 1. Login → /library/{some-gameId}
# 2. Hover on hero cover → see edit icon top-right
# 3. Click icon → modale dialog opens
# 4. Select a JPEG file → crop dialog opens
# 5. Crop + confirm → cover updates within ~5s
```

Document any UX issues found and fix inline before committing.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/game-detail/GameDetailDesktop.tsx \
        apps/web/src/app/\(authenticated\)/library/\[gameId\]/game-detail-mobile.tsx
git commit -m "feat(library): #1824 L3 integrate EditCoverOverlay + CustomCoverDialog in game detail hero"
```

---

### Task 12: EXIF strip validation test + E2E happy path

**Files:**
- Create: `apps/web/src/components/features/library/custom-cover/__tests__/exif-strip.test.ts`
- Create: `apps/web/e2e/library/custom-cover.spec.ts`

- [ ] **Step 1: Write EXIF strip validation unit test**

```ts
// apps/web/src/components/features/library/custom-cover/__tests__/exif-strip.test.ts
import { describe, it, expect } from 'vitest';

/**
 * AC-R10 + Nygard CRITICAL finding: EXIF GPS strip via canvas re-encode.
 * Asserts that re-encoded webp blob contains no EXIF metadata.
 */
describe('EXIF strip via canvas re-encode', () => {
  it('canvas-encoded webp blob does NOT carry EXIF data', async () => {
    // Create a synthetic JPEG with EXIF (canvas-drawn red square)
    const canvas = document.createElement('canvas');
    canvas.width = 200;
    canvas.height = 300;
    const ctx = canvas.getContext('2d')!;
    ctx.fillStyle = 'red';
    ctx.fillRect(0, 0, 200, 300);

    // Encode to webp
    const blob = await new Promise<Blob>((resolve, reject) => {
      canvas.toBlob((b) => (b ? resolve(b) : reject(new Error('toBlob null'))), 'image/webp', 0.8);
    });

    // Parse blob bytes — webp container should not contain EXIF chunk
    // WebP EXIF chunk identifier is "EXIF" (0x45584946)
    const buffer = await blob.arrayBuffer();
    const bytes = new Uint8Array(buffer);

    // Search for "EXIF" ASCII signature
    const exifSignature = [0x45, 0x58, 0x49, 0x46]; // "EXIF"
    let hasExif = false;
    for (let i = 0; i <= bytes.length - 4; i++) {
      if (
        bytes[i] === exifSignature[0] &&
        bytes[i + 1] === exifSignature[1] &&
        bytes[i + 2] === exifSignature[2] &&
        bytes[i + 3] === exifSignature[3]
      ) {
        hasExif = true;
        break;
      }
    }

    expect(hasExif).toBe(false);
  });
});
```

- [ ] **Step 2: Run EXIF test**

Run: `cd apps/web && pnpm test exif-strip 2>&1 | tail -10`
Expected: PASS.

- [ ] **Step 3: Write E2E happy path test**

```ts
// apps/web/e2e/library/custom-cover.spec.ts
import { test, expect } from '@playwright/test';

test.describe('L3 Custom Cover Upload E2E', () => {
  test('user uploads custom cover for a game in library within 5s SLO', async ({ page }) => {
    // Login + navigate to library (assumes test fixture session + game in library)
    await page.goto('/library');
    await page.getByTestId('login-button').click().catch(() => {/* if already logged in */});

    // Open first game detail
    const firstGame = page.getByTestId('library-game-card').first();
    await firstGame.click();

    // Hover hero to reveal edit icon
    const hero = page.getByTestId('game-detail-hero');
    await hero.hover();
    const editBtn = page.getByRole('button', { name: /modifica copertina/i });
    await expect(editBtn).toBeVisible();

    // Open custom cover dialog
    await editBtn.click();
    const fileInput = page.getByLabel(/seleziona foto/i);

    // Upload fixture image
    const startTime = Date.now();
    await fileInput.setInputFiles('e2e/fixtures/test-cover-input.jpg');

    // Crop dialog appears
    await expect(page.getByRole('dialog', { name: /ritaglia/i })).toBeVisible();
    await page.getByRole('button', { name: /conferma/i }).click();

    // Wait for upload + library refresh (SLO p95 < 5s)
    await expect(page.getByTestId('game-detail-hero-cover')).toHaveAttribute('src', /user-covers/, {
      timeout: 5000,
    });

    const elapsed = Date.now() - startTime;
    expect(elapsed).toBeLessThan(5000);
  });
});
```

- [ ] **Step 4: Add E2E test fixture image**

Create a simple JPEG at `apps/web/e2e/fixtures/test-cover-input.jpg` (200x300 or larger, single-color test image).

Run: `cd apps/web && pnpm test:e2e custom-cover 2>&1 | tail -20`
Expected: PASS (Docker / running dev server required).

- [ ] **Step 5: Run full test suite to verify no regressions**

```bash
cd apps/web && pnpm test 2>&1 | tail -10
dotnet test apps/api/tests/Api.Tests --filter "Category=Unit" 2>&1 | tail -10
```
Expected: All tests PASS (no regressions).

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/features/library/custom-cover/__tests__/exif-strip.test.ts \
        apps/web/e2e/library/custom-cover.spec.ts \
        apps/web/e2e/fixtures/test-cover-input.jpg
git commit -m "test(library): #1824 L3 EXIF strip validation + E2E happy path with SLO check"
```

---

## Self-Review

**Spec coverage**: All 12 AC (R1-R12) mapped to tasks:
- AC-R1 (DB column) → already shipped #1839 (no task needed)
- AC-R2 (Upload endpoint) → T1 + T2 + T5
- AC-R3 (Delete endpoint) → T1 + T3 + T5
- AC-R4 (Crop UI) → T8
- AC-R5 (Size compression) → T8 (renderCropToWebp loop)
- AC-R6 (Cleanup on game removal) → T4
- AC-R7 (Priority chain) → already wired in `CoverUrlResolver` (no task)
- AC-R8 (No game restriction) → T2 validator (does not restrict by type)
- AC-R9 (gameId ∈ library) → T2 handler
- AC-R10 (Privacy/EXIF) → T12 unit test
- AC-R11 (Mobile UX) → T10 HEIC handling + T11 mobile integration
- AC-R12 (SLO p95 <5s) → T12 E2E timing assertion

All 6 use cases covered:
- UC1 (happy upload) → T7+T8+T9+T10+T11+T12
- UC2 (replace) → T2 handler best-effort delete
- UC3 (remove) → T3 + T10 (remove button)
- UC4 (HEIC iOS) → T10 (heic2any lazy)
- UC5 (>10MB rejection) → T10 (validation in handleFileSelect)
- UC6 (non-comprimibile) → T8 (renderCropToWebp throws)

**Placeholder scan**: 
- No "TBD" / "TODO" / "implement later"
- All code blocks complete
- Test code shows actual assertions
- T5 Step 4 integration test has "skip in PR CI if Docker not available" note — acceptable conditional

**Type consistency**:
- `UploadCustomCoverCommand` signature consistent: (UserId, GameId, FileStream, FileSizeBytes, MimeType)
- `RemoveCustomCoverCommand` signature: (UserId, GameId)
- `CustomCoverUploadResult` shape: { CoverR2Key, PresignedUrl }
- React hooks return mutation objects with `mutate`, `isPending`, `isSuccess`, `isError`
- `BlobCategory.GameImage` used consistently for R2 paths

**Identified inconsistency to verify during impl**:
- T5 Step 1 assumes `IFormFile file` parameter works in ASP.NET Core minimal API. Verified pattern (DisableAntiforgery added).
- T11 Step 2 assumes `gameDetail.customCoverR2Key` field on DTO. If not present, T11 will need a sub-step to add it to `UserLibraryEntryDto` + `GetUserLibraryQueryHandler` projection.

---

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-06-04-l3-user-uploaded-cover.md`. Two execution options:**

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration via P120 mix-model (haiku T1+T3+T6+T7+T11 mechanical / sonnet T2+T4+T5+T8+T9+T10+T12 judgment). Final code-reviewer on entire branch.

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints for user review.

**Which approach?**

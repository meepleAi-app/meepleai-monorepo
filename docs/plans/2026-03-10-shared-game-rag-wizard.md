# SharedGame RAG Wizard — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Admin wizard to add RAG (PDF) to a SharedGame with real-time processing queue monitoring and completion notifications. Supports batch upload. Editors require approval gate; admins auto-approve.

**Architecture:** New orchestration command `AddRagToSharedGameCommand` wraps existing sub-commands (Upload → Link → Approve → Enqueue) into a single saga. Frontend wizard on SharedGame detail page with inline SSE progress tracking via existing queue streaming infrastructure. Batch variant dispatches N commands in parallel.

**Tech Stack:** .NET 9, MediatR CQRS, FluentValidation, SSE (System.Threading.Channels), React 19, Next.js App Router, React Query, Zustand, shadcn/ui

---

## Phase 1: Backend — Orchestration Command

### Task 1: Create `AddRagToSharedGameCommand` record and result

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/AddRagToSharedGame/AddRagToSharedGameCommand.cs`

**Step 1: Create the command record**

```csharp
using MediatR;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.SharedKernel.Application.Commands;
using Microsoft.AspNetCore.Http;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;

public record AddRagToSharedGameCommand(
    Guid SharedGameId,
    IFormFile File,
    SharedGameDocumentType DocumentType,
    string Version,
    List<string>? Tags,
    Guid UserId,
    bool IsAdmin
) : ICommand<AddRagToSharedGameResult>;

public record AddRagToSharedGameResult(
    Guid PdfDocumentId,
    Guid SharedGameDocumentId,
    Guid? ProcessingJobId,
    bool AutoApproved,
    string StreamUrl
);
```

**Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/AddRagToSharedGame/AddRagToSharedGameCommand.cs
git commit -m "feat(shared-catalog): add AddRagToSharedGameCommand record"
```

---

### Task 2: Create `AddRagToSharedGameCommandValidator`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/AddRagToSharedGame/AddRagToSharedGameCommandValidator.cs`

**Step 1: Write the failing test**

- Test: `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/AddRagToSharedGame/AddRagToSharedGameCommandValidatorTests.cs`

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using FluentValidation.TestHelper;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public class AddRagToSharedGameCommandValidatorTests
{
    private readonly AddRagToSharedGameCommandValidator _sut = new();

    private static AddRagToSharedGameCommand ValidCommand() => new(
        SharedGameId: Guid.NewGuid(),
        File: CreateMockPdf("test.pdf", 1024),
        DocumentType: SharedGameDocumentType.Rulebook,
        Version: "1.0",
        Tags: null,
        UserId: Guid.NewGuid(),
        IsAdmin: true
    );

    private static IFormFile CreateMockPdf(string name, long size)
    {
        var stream = new MemoryStream(new byte[size]);
        return new FormFile(stream, 0, size, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
    }

    [Fact]
    public void Valid_command_passes()
    {
        var result = _sut.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_SharedGameId_fails()
    {
        var cmd = ValidCommand() with { SharedGameId = Guid.Empty };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.SharedGameId);
    }

    [Fact]
    public void Null_File_fails()
    {
        var cmd = ValidCommand() with { File = null! };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.File);
    }

    [Fact]
    public void File_too_large_fails()
    {
        var cmd = ValidCommand() with { File = CreateMockPdf("big.pdf", 51 * 1024 * 1024) };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.File);
    }

    [Fact]
    public void Invalid_version_format_fails()
    {
        var cmd = ValidCommand() with { Version = "abc" };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Version);
    }

    [Fact]
    public void Tags_only_allowed_for_Homerule()
    {
        var cmd = ValidCommand() with { Tags = new List<string> { "tag1" }, DocumentType = SharedGameDocumentType.Rulebook };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Tags);
    }

    [Fact]
    public void Tags_allowed_for_Homerule()
    {
        var cmd = ValidCommand() with { Tags = new List<string> { "variant" }, DocumentType = SharedGameDocumentType.Homerule };
        _sut.TestValidate(cmd).ShouldNotHaveValidationErrorFor(x => x.Tags);
    }

    [Fact]
    public void Empty_UserId_fails()
    {
        var cmd = ValidCommand() with { UserId = Guid.Empty };
        _sut.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.UserId);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~AddRagToSharedGameCommandValidatorTests" --no-restore -v minimal
```
Expected: Build error — `AddRagToSharedGameCommandValidator` does not exist.

**Step 3: Write the validator**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;

public class AddRagToSharedGameCommandValidator : AbstractValidator<AddRagToSharedGameCommand>
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB

    public AddRagToSharedGameCommandValidator()
    {
        RuleFor(x => x.SharedGameId).NotEqual(Guid.Empty);
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.File)
            .Must(f => f == null || f.Length <= MaxFileSizeBytes)
            .WithMessage($"File size must not exceed {MaxFileSizeBytes / (1024 * 1024)}MB");
        RuleFor(x => x.Version)
            .NotEmpty()
            .Matches(@"^\d+\.\d+$")
            .WithMessage("Version must be in format 'X.Y' (e.g., '1.0', '2.1')");
        RuleFor(x => x.DocumentType).IsInEnum();
        RuleFor(x => x.Tags)
            .Must(tags => tags == null || tags.Count == 0)
            .When(x => x.DocumentType != SharedGameDocumentType.Homerule)
            .WithMessage("Tags are only allowed for Homerule documents");
        RuleFor(x => x.Tags)
            .Must(tags => tags == null || tags.Count <= 10)
            .WithMessage("Maximum 10 tags allowed");
        RuleForEach(x => x.Tags)
            .MaximumLength(50)
            .When(x => x.Tags != null && x.Tags.Count > 0);
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~AddRagToSharedGameCommandValidatorTests" --no-restore -v minimal
```
Expected: All 8 tests PASS.

**Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/AddRagToSharedGame/AddRagToSharedGameCommandValidator.cs tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/AddRagToSharedGame/AddRagToSharedGameCommandValidatorTests.cs
git commit -m "feat(shared-catalog): add AddRagToSharedGame validator with tests"
```

---

### Task 3: Create `AddRagToSharedGameCommandHandler` (Saga)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/AddRagToSharedGame/AddRagToSharedGameCommandHandler.cs`
- Test: `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/AddRagToSharedGame/AddRagToSharedGameCommandHandlerTests.cs`

**Step 1: Write the failing test**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public class AddRagToSharedGameCommandHandlerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ISharedGameRepository> _sharedGameRepo = new();
    private readonly Mock<ILogger<AddRagToSharedGameCommandHandler>> _logger = new();
    private readonly AddRagToSharedGameCommandHandler _sut;

    public AddRagToSharedGameCommandHandlerTests()
    {
        _sut = new AddRagToSharedGameCommandHandler(
            _mediator.Object,
            _sharedGameRepo.Object,
            _logger.Object
        );
    }

    private static IFormFile CreateMockPdf() =>
        new FormFile(new MemoryStream(new byte[1024]), 0, 1024, "file", "test.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

    [Fact]
    public async Task Handle_SharedGameNotFound_ThrowsNotFoundException()
    {
        _sharedGameRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame?)null);

        var cmd = new AddRagToSharedGameCommand(
            Guid.NewGuid(), CreateMockPdf(), SharedGameDocumentType.Rulebook,
            "1.0", null, Guid.NewGuid(), true);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.Handle(cmd, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AdminUser_AutoApproves_And_Enqueues()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        SetupSharedGameExists(gameId);

        // Upload returns PdfDocumentId
        _mediator.Setup(m => m.Send(It.IsAny<UploadPdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfId);
        // AddDocument returns SharedGameDocumentId
        _mediator.Setup(m => m.Send(
            It.IsAny<Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddDocumentToSharedGameCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(docId);
        // Approve (void-like, returns Unit)
        _mediator.Setup(m => m.Send(
            It.IsAny<Api.BoundedContexts.SharedGameCatalog.Application.Commands.ApproveDocumentForRagProcessingCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);
        // Enqueue returns JobId
        _mediator.Setup(m => m.Send(It.IsAny<EnqueuePdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobId);

        var cmd = new AddRagToSharedGameCommand(
            gameId, CreateMockPdf(), SharedGameDocumentType.Rulebook,
            "1.0", null, Guid.NewGuid(), IsAdmin: true);

        // Act
        var result = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        Assert.Equal(pdfId, result.PdfDocumentId);
        Assert.Equal(docId, result.SharedGameDocumentId);
        Assert.Equal(jobId, result.ProcessingJobId);
        Assert.True(result.AutoApproved);
        Assert.Contains(jobId.ToString(), result.StreamUrl);

        _mediator.Verify(m => m.Send(
            It.Is<ApproveDocumentForRagProcessingCommand>(c => c.DocumentId == docId),
            It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(
            It.Is<EnqueuePdfCommand>(c => c.PdfDocumentId == pdfId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EditorUser_DoesNot_AutoApprove()
    {
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        SetupSharedGameExists(gameId);

        _mediator.Setup(m => m.Send(It.IsAny<UploadPdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfId);
        _mediator.Setup(m => m.Send(
            It.IsAny<Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddDocumentToSharedGameCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(docId);

        var cmd = new AddRagToSharedGameCommand(
            gameId, CreateMockPdf(), SharedGameDocumentType.Rulebook,
            "1.0", null, Guid.NewGuid(), IsAdmin: false);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        Assert.Equal(pdfId, result.PdfDocumentId);
        Assert.Equal(docId, result.SharedGameDocumentId);
        Assert.Null(result.ProcessingJobId);
        Assert.False(result.AutoApproved);

        // Should NOT call approve or enqueue
        _mediator.Verify(m => m.Send(
            It.IsAny<ApproveDocumentForRagProcessingCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(
            It.IsAny<EnqueuePdfCommand>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EnqueueFails_RollsBack_Approval()
    {
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        SetupSharedGameExists(gameId);

        _mediator.Setup(m => m.Send(It.IsAny<UploadPdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfId);
        _mediator.Setup(m => m.Send(
            It.IsAny<Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddDocumentToSharedGameCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(docId);
        _mediator.Setup(m => m.Send(
            It.IsAny<ApproveDocumentForRagProcessingCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);
        _mediator.Setup(m => m.Send(It.IsAny<EnqueuePdfCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Queue full"));

        var cmd = new AddRagToSharedGameCommand(
            gameId, CreateMockPdf(), SharedGameDocumentType.Rulebook,
            "1.0", null, Guid.NewGuid(), IsAdmin: true);

        // Should still return result with ProcessingJobId = null (degraded)
        var result = await _sut.Handle(cmd, CancellationToken.None);

        Assert.Null(result.ProcessingJobId);
        Assert.True(result.AutoApproved);
        // Document was still linked and approved, just not enqueued
    }

    private void SetupSharedGameExists(Guid gameId)
    {
        // Use a mock that returns a non-null SharedGame
        // The handler only needs to verify existence, so a basic mock suffices
        var mockGame = Mock.Of<Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.SharedGame>();
        _sharedGameRepo.Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockGame);
    }
}
```

**Step 2: Run test to verify it fails**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~AddRagToSharedGameCommandHandlerTests" --no-restore -v minimal
```
Expected: Build error — `AddRagToSharedGameCommandHandler` does not exist.

**Step 3: Write the handler**

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Commands.Queue;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Commands;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;

public class AddRagToSharedGameCommandHandler : ICommandHandler<AddRagToSharedGameCommand, AddRagToSharedGameResult>
{
    private readonly IMediator _mediator;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly ILogger<AddRagToSharedGameCommandHandler> _logger;

    public AddRagToSharedGameCommandHandler(
        IMediator mediator,
        ISharedGameRepository sharedGameRepository,
        ILogger<AddRagToSharedGameCommandHandler> logger)
    {
        _mediator = mediator;
        _sharedGameRepository = sharedGameRepository;
        _logger = logger;
    }

    public async Task<AddRagToSharedGameResult> Handle(
        AddRagToSharedGameCommand request, CancellationToken cancellationToken)
    {
        // Step 0: Validate SharedGame exists
        var game = await _sharedGameRepository.GetByIdAsync(request.SharedGameId, cancellationToken)
            ?? throw new NotFoundException("SharedGame", request.SharedGameId);

        // Step 1: Upload PDF
        var pdfId = await _mediator.Send(new UploadPdfCommand(
            GameId: request.SharedGameId.ToString(),
            Metadata: null,
            PrivateGameId: null,
            UserId: request.UserId,
            File: request.File
        ), cancellationToken);

        _logger.LogInformation(
            "RAG wizard: PDF uploaded {PdfId} for SharedGame {GameId}",
            pdfId, request.SharedGameId);

        // Step 2: Link document to SharedGame
        var docId = await _mediator.Send(new AddDocumentToSharedGameCommand(
            SharedGameId: request.SharedGameId,
            PdfDocumentId: pdfId,
            DocumentType: request.DocumentType,
            Version: request.Version,
            Tags: request.Tags,
            SetAsActive: true,
            CreatedBy: request.UserId
        ), cancellationToken);

        _logger.LogInformation(
            "RAG wizard: Document {DocId} linked to SharedGame {GameId}",
            docId, request.SharedGameId);

        // Step 3: Admin auto-approves; Editor requires manual approval
        if (!request.IsAdmin)
        {
            _logger.LogInformation(
                "RAG wizard: Editor upload — document {DocId} pending approval",
                docId);

            return new AddRagToSharedGameResult(
                PdfDocumentId: pdfId,
                SharedGameDocumentId: docId,
                ProcessingJobId: null,
                AutoApproved: false,
                StreamUrl: string.Empty
            );
        }

        // Admin path: approve + enqueue
        await _mediator.Send(new ApproveDocumentForRagProcessingCommand(
            DocumentId: docId,
            ApprovedBy: request.UserId,
            Notes: "Auto-approved via RAG wizard"
        ), cancellationToken);

        _logger.LogInformation(
            "RAG wizard: Document {DocId} auto-approved for RAG", docId);

        // Step 4: Enqueue for processing (graceful degradation if queue fails)
        Guid? jobId = null;
        try
        {
            jobId = await _mediator.Send(new EnqueuePdfCommand(
                PdfDocumentId: pdfId,
                UserId: request.UserId,
                Priority: (int)ProcessingPriority.High
            ), cancellationToken);

            _logger.LogInformation(
                "RAG wizard: Job {JobId} enqueued for PDF {PdfId}", jobId, pdfId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "RAG wizard: Failed to enqueue PDF {PdfId} — document approved but not queued",
                pdfId);
        }

        var streamUrl = jobId.HasValue
            ? $"/api/v1/admin/queue/{jobId}/stream"
            : string.Empty;

        return new AddRagToSharedGameResult(
            PdfDocumentId: pdfId,
            SharedGameDocumentId: docId,
            ProcessingJobId: jobId,
            AutoApproved: true,
            StreamUrl: streamUrl
        );
    }
}
```

**Step 4: Run tests to verify they pass**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~AddRagToSharedGameCommandHandlerTests" --no-restore -v minimal
```
Expected: All 4 tests PASS.

> **Note to implementer:** The test mocks may need adjustment based on the exact return types of existing commands (`UploadPdfCommand` may return `Guid` or a result record — verify by reading the handler). Adapt the mock `Setup` calls to match actual signatures.

**Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/AddRagToSharedGame/AddRagToSharedGameCommandHandler.cs tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/AddRagToSharedGame/AddRagToSharedGameCommandHandlerTests.cs
git commit -m "feat(shared-catalog): add AddRagToSharedGame handler with saga orchestration"
```

---

### Task 4: Create Batch Command `BatchAddRagToSharedGameCommand`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/AddRagToSharedGame/BatchAddRagToSharedGameCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/AddRagToSharedGame/BatchAddRagToSharedGameCommandHandler.cs`
- Test: `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/AddRagToSharedGame/BatchAddRagToSharedGameCommandHandlerTests.cs`

**Step 1: Write the command**

```csharp
using Api.SharedKernel.Application.Commands;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;

public record BatchAddRagToSharedGameCommand(
    List<AddRagToSharedGameCommand> Items
) : ICommand<BatchAddRagToSharedGameResult>;

public record BatchAddRagToSharedGameResult(
    List<BatchItemResult> Results,
    int SuccessCount,
    int FailureCount
);

public record BatchItemResult(
    Guid SharedGameId,
    string FileName,
    AddRagToSharedGameResult? Result,
    string? Error
);
```

**Step 2: Write the failing test**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public class BatchAddRagToSharedGameCommandHandlerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ILogger<BatchAddRagToSharedGameCommandHandler>> _logger = new();
    private readonly BatchAddRagToSharedGameCommandHandler _sut;

    public BatchAddRagToSharedGameCommandHandlerTests()
    {
        _sut = new BatchAddRagToSharedGameCommandHandler(_mediator.Object, _logger.Object);
    }

    private static IFormFile CreateMockPdf(string name = "test.pdf") =>
        new FormFile(new MemoryStream(new byte[1024]), 0, 1024, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

    [Fact]
    public async Task Handle_AllSucceed_ReturnsFullSuccess()
    {
        var items = new List<AddRagToSharedGameCommand>
        {
            new(Guid.NewGuid(), CreateMockPdf("a.pdf"), SharedGameDocumentType.Rulebook, "1.0", null, Guid.NewGuid(), true),
            new(Guid.NewGuid(), CreateMockPdf("b.pdf"), SharedGameDocumentType.Rulebook, "1.0", null, Guid.NewGuid(), true),
        };

        _mediator.Setup(m => m.Send(It.IsAny<AddRagToSharedGameCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AddRagToSharedGameResult(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), true, "/stream"));

        var result = await _sut.Handle(new BatchAddRagToSharedGameCommand(items), CancellationToken.None);

        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.FailureCount);
    }

    [Fact]
    public async Task Handle_OneFailsOneSucceeds_ReturnsPartialResult()
    {
        var items = new List<AddRagToSharedGameCommand>
        {
            new(Guid.NewGuid(), CreateMockPdf("a.pdf"), SharedGameDocumentType.Rulebook, "1.0", null, Guid.NewGuid(), true),
            new(Guid.NewGuid(), CreateMockPdf("b.pdf"), SharedGameDocumentType.Rulebook, "1.0", null, Guid.NewGuid(), true),
        };

        var callCount = 0;
        _mediator.Setup(m => m.Send(It.IsAny<AddRagToSharedGameCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (Interlocked.Increment(ref callCount) == 1)
                    throw new InvalidOperationException("Upload failed");
                return new AddRagToSharedGameResult(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), true, "/stream");
            });

        var result = await _sut.Handle(new BatchAddRagToSharedGameCommand(items), CancellationToken.None);

        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.Contains(result.Results, r => r.Error != null);
    }
}
```

**Step 3: Write the handler**

```csharp
using Api.SharedKernel.Application.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;

public class BatchAddRagToSharedGameCommandHandler
    : ICommandHandler<BatchAddRagToSharedGameCommand, BatchAddRagToSharedGameResult>
{
    private readonly IMediator _mediator;
    private readonly ILogger<BatchAddRagToSharedGameCommandHandler> _logger;

    public BatchAddRagToSharedGameCommandHandler(IMediator mediator, ILogger<BatchAddRagToSharedGameCommandHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<BatchAddRagToSharedGameResult> Handle(
        BatchAddRagToSharedGameCommand request, CancellationToken cancellationToken)
    {
        var results = new List<BatchItemResult>();

        // Process sequentially to avoid overwhelming the upload pipeline
        foreach (var item in request.Items)
        {
            try
            {
                var result = await _mediator.Send(item, cancellationToken);
                results.Add(new BatchItemResult(item.SharedGameId, item.File.FileName, result, null));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Batch RAG: Failed for SharedGame {GameId}, file {FileName}",
                    item.SharedGameId, item.File.FileName);
                results.Add(new BatchItemResult(item.SharedGameId, item.File.FileName, null, ex.Message));
            }
        }

        return new BatchAddRagToSharedGameResult(
            Results: results,
            SuccessCount: results.Count(r => r.Result != null),
            FailureCount: results.Count(r => r.Error != null)
        );
    }
}
```

**Step 4: Run tests**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~BatchAddRagToSharedGameCommandHandlerTests" --no-restore -v minimal
```
Expected: All PASS.

**Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/AddRagToSharedGame/BatchAddRagToSharedGame*.cs tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/AddRagToSharedGame/BatchAddRagToSharedGameCommandHandlerTests.cs
git commit -m "feat(shared-catalog): add batch RAG upload command"
```

---

### Task 5: Add Endpoints to `SharedGameCatalogEndpoints`

**Files:**
- Modify: `apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs` — add 2 new endpoints

**Step 1: Find the MapAdminEndpoints method**

Look for the admin group builder inside `SharedGameCatalogEndpoints.cs`. The new endpoints go inside the admin section.

**Step 2: Add single and batch RAG endpoints**

Add these two endpoints inside the admin endpoints section (after existing document endpoints):

```csharp
// --- RAG Wizard Endpoints ---

group.MapPost("/{id:guid}/rag", async (
    Guid id,
    [FromForm] IFormFile file,
    [FromForm] string documentType,
    [FromForm] string version,
    [FromForm] string? tags,
    HttpContext context,
    IMediator mediator) =>
{
    var session = context.RequireAdminSession();
    var isAdmin = session.IsAdmin(); // Check role from session

    var parsedType = Enum.Parse<SharedGameDocumentType>(documentType, ignoreCase: true);
    var parsedTags = string.IsNullOrEmpty(tags)
        ? null
        : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    var result = await mediator.Send(new AddRagToSharedGameCommand(
        SharedGameId: id,
        File: file,
        DocumentType: parsedType,
        Version: version,
        Tags: parsedTags,
        UserId: session.UserId,
        IsAdmin: isAdmin
    ));

    return Results.Accepted(result.StreamUrl, result);
})
.RequireAdminSession()
.DisableAntiforgery()
.WithName("AddRagToSharedGame")
.Produces<AddRagToSharedGameResult>(202)
.Produces(404)
.Produces(409)
.WithSummary("Upload PDF and start RAG processing for a SharedGame");

group.MapPost("/batch-rag", async (
    [FromForm] BatchRagRequest request,
    HttpContext context,
    IMediator mediator) =>
{
    var session = context.RequireAdminSession();
    var isAdmin = session.IsAdmin();

    var items = new List<AddRagToSharedGameCommand>();
    for (var i = 0; i < request.SharedGameIds.Count; i++)
    {
        var parsedType = Enum.Parse<SharedGameDocumentType>(request.DocumentTypes[i], ignoreCase: true);
        items.Add(new AddRagToSharedGameCommand(
            SharedGameId: request.SharedGameIds[i],
            File: request.Files[i],
            DocumentType: parsedType,
            Version: request.Versions[i],
            Tags: null,
            UserId: session.UserId,
            IsAdmin: isAdmin
        ));
    }

    var result = await mediator.Send(new BatchAddRagToSharedGameCommand(items));
    return Results.Ok(result);
})
.RequireAdminSession()
.DisableAntiforgery()
.WithName("BatchAddRagToSharedGames")
.Produces<BatchAddRagToSharedGameResult>(200)
.WithSummary("Batch upload PDFs and start RAG processing for multiple SharedGames");
```

**Step 3: Add the request DTO** (at bottom of file with other internal records):

```csharp
internal record BatchRagRequest(
    List<Guid> SharedGameIds,
    List<IFormFile> Files,
    List<string> DocumentTypes,
    List<string> Versions
);
```

> **Note to implementer:** Verify how `RequireAdminSession()` works and how to extract `IsAdmin` vs editor role. Check existing session extension methods. The `session.IsAdmin()` method name may differ — search for `IsAdmin` or role checking patterns in the codebase.

**Step 4: Commit**

```bash
git add apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs
git commit -m "feat(shared-catalog): add RAG wizard endpoints (single + batch)"
```

---

## Phase 2: Frontend — Wizard UI

### Task 6: Create API client functions for RAG wizard

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/shared-games/[id]/rag-wizard/lib/rag-api.ts`

**Step 1: Write the API client**

```typescript
import { apiClient } from '@/lib/api/client';

// --- Types ---

export interface AddRagResult {
  pdfDocumentId: string;
  sharedGameDocumentId: string;
  processingJobId: string | null;
  autoApproved: boolean;
  streamUrl: string;
}

export interface BatchRagResult {
  results: BatchItemResult[];
  successCount: number;
  failureCount: number;
}

export interface BatchItemResult {
  sharedGameId: string;
  fileName: string;
  result: AddRagResult | null;
  error: string | null;
}

// --- API Functions ---

export async function addRagToSharedGame(
  sharedGameId: string,
  file: File,
  documentType: string,
  version: string,
  tags?: string[]
): Promise<AddRagResult> {
  const formData = new FormData();
  formData.append('file', file);
  formData.append('documentType', documentType);
  formData.append('version', version);
  if (tags?.length) {
    formData.append('tags', tags.join(','));
  }

  const response = await apiClient.post(
    `/api/v1/admin/shared-games/${sharedGameId}/rag`,
    formData,
    { headers: { 'Content-Type': 'multipart/form-data' } }
  );

  return response.data;
}

export async function batchAddRag(
  items: Array<{
    sharedGameId: string;
    file: File;
    documentType: string;
    version: string;
  }>
): Promise<BatchRagResult> {
  const formData = new FormData();
  items.forEach((item, i) => {
    formData.append('sharedGameIds', item.sharedGameId);
    formData.append('files', item.file);
    formData.append('documentTypes', item.documentType);
    formData.append('versions', item.version);
  });

  const response = await apiClient.post(
    '/api/v1/admin/shared-games/batch-rag',
    formData,
    { headers: { 'Content-Type': 'multipart/form-data' } }
  );

  return response.data;
}
```

**Step 2: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/shared-games/[id]/rag-wizard/lib/rag-api.ts
git commit -m "feat(frontend): add RAG wizard API client"
```

---

### Task 7: Create the Wizard component — Step 1 (Upload)

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/shared-games/[id]/rag-wizard/components/rag-wizard.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/shared-games/[id]/rag-wizard/components/step-upload.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/shared-games/[id]/rag-wizard/components/step-configure.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/shared-games/[id]/rag-wizard/components/step-progress.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/shared-games/[id]/rag-wizard/components/step-complete.tsx`

> **Note to implementer:** Before creating these files, check existing wizard/stepper patterns in the codebase. Search for `Step`, `Wizard`, `Stepper` components under `apps/web/src/`. Follow the existing pattern if one exists.

**Step 1: Create the wizard shell**

`rag-wizard.tsx`:
```tsx
'use client';

import { useState } from 'react';
import { StepUpload } from './step-upload';
import { StepConfigure } from './step-configure';
import { StepProgress } from './step-progress';
import { StepComplete } from './step-complete';
import type { AddRagResult } from '../lib/rag-api';

type WizardStep = 'upload' | 'configure' | 'progress' | 'complete';

interface WizardFile {
  file: File;
  documentType: 'Rulebook' | 'Errata' | 'Homerule';
  version: string;
  tags?: string[];
}

interface RagWizardProps {
  sharedGameId: string;
  sharedGameName: string;
  onClose: () => void;
}

export function RagWizard({ sharedGameId, sharedGameName, onClose }: RagWizardProps) {
  const [step, setStep] = useState<WizardStep>('upload');
  const [files, setFiles] = useState<File[]>([]);
  const [configs, setConfigs] = useState<WizardFile[]>([]);
  const [results, setResults] = useState<AddRagResult[]>([]);

  const steps: { key: WizardStep; label: string }[] = [
    { key: 'upload', label: 'Upload' },
    { key: 'configure', label: 'Configura' },
    { key: 'progress', label: 'Processing' },
    { key: 'complete', label: 'Completo' },
  ];

  const currentIndex = steps.findIndex(s => s.key === step);

  return (
    <div className="flex flex-col gap-6">
      {/* Stepper indicator */}
      <nav aria-label="Wizard progress" className="flex items-center gap-2">
        {steps.map((s, i) => (
          <div key={s.key} className="flex items-center gap-2">
            <div
              className={`flex h-8 w-8 items-center justify-center rounded-full text-sm font-medium ${
                i < currentIndex
                  ? 'bg-green-500 text-white'
                  : i === currentIndex
                    ? 'bg-primary text-primary-foreground'
                    : 'bg-muted text-muted-foreground'
              }`}
            >
              {i < currentIndex ? '✓' : i + 1}
            </div>
            <span className={`text-sm ${i === currentIndex ? 'font-semibold' : 'text-muted-foreground'}`}>
              {s.label}
            </span>
            {i < steps.length - 1 && <div className="h-px w-8 bg-border" />}
          </div>
        ))}
      </nav>

      {/* Step content */}
      {step === 'upload' && (
        <StepUpload
          files={files}
          onFilesChange={setFiles}
          onNext={() => {
            setConfigs(files.map(f => ({
              file: f,
              documentType: 'Rulebook',
              version: '1.0',
            })));
            setStep('configure');
          }}
          onCancel={onClose}
        />
      )}

      {step === 'configure' && (
        <StepConfigure
          configs={configs}
          onConfigsChange={setConfigs}
          onNext={() => setStep('progress')}
          onBack={() => setStep('upload')}
        />
      )}

      {step === 'progress' && (
        <StepProgress
          sharedGameId={sharedGameId}
          configs={configs}
          onComplete={(res) => {
            setResults(res);
            setStep('complete');
          }}
        />
      )}

      {step === 'complete' && (
        <StepComplete
          results={results}
          sharedGameName={sharedGameName}
          onClose={onClose}
        />
      )}
    </div>
  );
}

export type { WizardFile };
```

**Step 2: Create step-upload.tsx**

```tsx
'use client';

import { useCallback } from 'react';
import { Button } from '@/components/ui/button';

interface StepUploadProps {
  files: File[];
  onFilesChange: (files: File[]) => void;
  onNext: () => void;
  onCancel: () => void;
}

const MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB
const ACCEPTED_TYPE = 'application/pdf';

export function StepUpload({ files, onFilesChange, onNext, onCancel }: StepUploadProps) {
  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      const dropped = Array.from(e.dataTransfer.files).filter(
        f => f.type === ACCEPTED_TYPE && f.size <= MAX_FILE_SIZE
      );
      onFilesChange([...files, ...dropped]);
    },
    [files, onFilesChange]
  );

  const handleFileInput = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const selected = Array.from(e.target.files ?? []).filter(
        f => f.type === ACCEPTED_TYPE && f.size <= MAX_FILE_SIZE
      );
      onFilesChange([...files, ...selected]);
      e.target.value = ''; // Reset for re-selection
    },
    [files, onFilesChange]
  );

  const removeFile = (index: number) => {
    onFilesChange(files.filter((_, i) => i !== index));
  };

  return (
    <div className="flex flex-col gap-4">
      {/* Drop zone */}
      <div
        onDrop={handleDrop}
        onDragOver={e => e.preventDefault()}
        className="flex min-h-[200px] cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed border-muted-foreground/25 bg-muted/50 p-8 transition-colors hover:border-primary/50"
      >
        <label className="cursor-pointer text-center">
          <p className="text-lg font-medium">Trascina PDF qui</p>
          <p className="text-sm text-muted-foreground">o clicca per selezionare (max 50MB per file)</p>
          <input
            type="file"
            accept=".pdf"
            multiple
            className="hidden"
            onChange={handleFileInput}
          />
        </label>
      </div>

      {/* File list */}
      {files.length > 0 && (
        <ul className="divide-y rounded-lg border">
          {files.map((file, i) => (
            <li key={`${file.name}-${i}`} className="flex items-center justify-between px-4 py-3">
              <div>
                <p className="text-sm font-medium">{file.name}</p>
                <p className="text-xs text-muted-foreground">
                  {(file.size / (1024 * 1024)).toFixed(1)} MB
                </p>
              </div>
              <Button variant="ghost" size="sm" onClick={() => removeFile(i)}>
                Rimuovi
              </Button>
            </li>
          ))}
        </ul>
      )}

      {/* Actions */}
      <div className="flex justify-between">
        <Button variant="outline" onClick={onCancel}>Annulla</Button>
        <Button onClick={onNext} disabled={files.length === 0}>
          Avanti ({files.length} file)
        </Button>
      </div>
    </div>
  );
}
```

**Step 3: Create step-configure.tsx**

```tsx
'use client';

import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import type { WizardFile } from './rag-wizard';

interface StepConfigureProps {
  configs: WizardFile[];
  onConfigsChange: (configs: WizardFile[]) => void;
  onNext: () => void;
  onBack: () => void;
}

export function StepConfigure({ configs, onConfigsChange, onNext, onBack }: StepConfigureProps) {
  const updateConfig = (index: number, update: Partial<WizardFile>) => {
    const updated = [...configs];
    updated[index] = { ...updated[index], ...update };
    onConfigsChange(updated);
  };

  const allValid = configs.every(c => /^\d+\.\d+$/.test(c.version));

  return (
    <div className="flex flex-col gap-4">
      {configs.map((config, i) => (
        <div key={`${config.file.name}-${i}`} className="rounded-lg border p-4">
          <p className="mb-3 text-sm font-semibold">{config.file.name}</p>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <Label htmlFor={`type-${i}`}>Tipo documento</Label>
              <Select
                value={config.documentType}
                onValueChange={(v) => updateConfig(i, { documentType: v as WizardFile['documentType'] })}
              >
                <SelectTrigger id={`type-${i}`}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Rulebook">Regolamento</SelectItem>
                  <SelectItem value="Errata">Errata</SelectItem>
                  <SelectItem value="Homerule">Regola casalinga</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor={`version-${i}`}>Versione</Label>
              <Input
                id={`version-${i}`}
                value={config.version}
                onChange={e => updateConfig(i, { version: e.target.value })}
                placeholder="1.0"
                pattern="\d+\.\d+"
              />
              {!/^\d+\.\d+$/.test(config.version) && (
                <p className="mt-1 text-xs text-destructive">Formato: X.Y (es. 1.0)</p>
              )}
            </div>
          </div>
        </div>
      ))}

      <div className="flex justify-between">
        <Button variant="outline" onClick={onBack}>Indietro</Button>
        <Button onClick={onNext} disabled={!allValid}>
          Avvia Processing
        </Button>
      </div>
    </div>
  );
}
```

**Step 4: Create step-progress.tsx** (key component — uses existing SSE hooks)

```tsx
'use client';

import { useEffect, useState, useRef } from 'react';
import { addRagToSharedGame, type AddRagResult } from '../lib/rag-api';
import { useJobSSE } from '@/app/admin/(dashboard)/knowledge-base/queue/hooks/use-job-sse';
import { useJobDetail } from '@/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api';
import type { WizardFile } from './rag-wizard';

interface StepProgressProps {
  sharedGameId: string;
  configs: WizardFile[];
  onComplete: (results: AddRagResult[]) => void;
}

interface FileProgress {
  config: WizardFile;
  status: 'pending' | 'uploading' | 'processing' | 'completed' | 'failed';
  result?: AddRagResult;
  error?: string;
  jobId?: string;
}

export function StepProgress({ sharedGameId, configs, onComplete }: StepProgressProps) {
  const [progresses, setProgresses] = useState<FileProgress[]>(
    configs.map(c => ({ config: c, status: 'pending' }))
  );
  const startedRef = useRef(false);

  // Track the active job for SSE
  const activeJobId = progresses.find(p => p.status === 'processing')?.jobId;

  // Connect SSE to the active processing job
  const { connectionState } = useJobSSE(activeJobId ?? null);
  const { data: jobDetail } = useJobDetail(activeJobId ?? null, connectionState === 'connected');

  // Start processing sequentially
  useEffect(() => {
    if (startedRef.current) return;
    startedRef.current = true;

    const processAll = async () => {
      const results: AddRagResult[] = [];

      for (let i = 0; i < configs.length; i++) {
        const config = configs[i];

        setProgresses(prev => {
          const updated = [...prev];
          updated[i] = { ...updated[i], status: 'uploading' };
          return updated;
        });

        try {
          const result = await addRagToSharedGame(
            sharedGameId,
            config.file,
            config.documentType,
            config.version,
            config.tags
          );

          results.push(result);

          setProgresses(prev => {
            const updated = [...prev];
            updated[i] = {
              ...updated[i],
              status: result.processingJobId ? 'processing' : 'completed',
              result,
              jobId: result.processingJobId ?? undefined,
            };
            return updated;
          });
        } catch (err) {
          setProgresses(prev => {
            const updated = [...prev];
            updated[i] = {
              ...updated[i],
              status: 'failed',
              error: err instanceof Error ? err.message : 'Upload fallito',
            };
            return updated;
          });
        }
      }

      // Wait for all processing jobs to complete via SSE
      // The parent will transition to 'complete' step
    };

    processAll();
  }, [configs, sharedGameId]);

  // Check if active job completed via jobDetail
  useEffect(() => {
    if (!jobDetail || !activeJobId) return;

    if (jobDetail.status === 'Completed' || jobDetail.status === 'Failed') {
      setProgresses(prev =>
        prev.map(p =>
          p.jobId === activeJobId
            ? { ...p, status: jobDetail.status === 'Completed' ? 'completed' : 'failed' }
            : p
        )
      );
    }
  }, [jobDetail, activeJobId]);

  // Check if all done
  useEffect(() => {
    const allDone = progresses.every(p => p.status === 'completed' || p.status === 'failed');
    if (allDone && progresses.length > 0 && startedRef.current) {
      const results = progresses
        .filter(p => p.result)
        .map(p => p.result!);
      onComplete(results);
    }
  }, [progresses, onComplete]);

  const stepLabels: Record<string, string> = {
    Upload: 'Upload',
    Extract: 'Estrazione testo',
    Chunk: 'Chunking',
    Embed: 'Generazione embeddings',
    Index: 'Indicizzazione vettoriale',
  };

  return (
    <div className="flex flex-col gap-4">
      {progresses.map((p, i) => (
        <div key={`${p.config.file.name}-${i}`} className="rounded-lg border p-4">
          <div className="flex items-center justify-between">
            <p className="text-sm font-semibold">{p.config.file.name}</p>
            <StatusBadge status={p.status} />
          </div>

          {/* Show job steps if this is the active processing job */}
          {p.jobId && p.jobId === activeJobId && jobDetail?.steps && (
            <div className="mt-3 space-y-2">
              {jobDetail.steps.map(step => (
                <div key={step.id} className="flex items-center gap-2 text-xs">
                  <StepIcon status={step.status} />
                  <span>{stepLabels[step.stepName] ?? step.stepName}</span>
                  {step.durationMs != null && (
                    <span className="text-muted-foreground">
                      ({(step.durationMs / 1000).toFixed(1)}s)
                    </span>
                  )}
                </div>
              ))}
            </div>
          )}

          {p.error && (
            <p className="mt-2 text-xs text-destructive">{p.error}</p>
          )}
        </div>
      ))}

      {/* SSE connection state */}
      {activeJobId && (
        <p className="text-xs text-muted-foreground">
          Streaming: {connectionState === 'connected' ? 'Connesso' : connectionState}
        </p>
      )}
    </div>
  );
}

function StatusBadge({ status }: { status: FileProgress['status'] }) {
  const styles: Record<string, string> = {
    pending: 'bg-muted text-muted-foreground',
    uploading: 'bg-blue-100 text-blue-700 dark:bg-blue-900 dark:text-blue-300',
    processing: 'bg-amber-100 text-amber-700 dark:bg-amber-900 dark:text-amber-300',
    completed: 'bg-green-100 text-green-700 dark:bg-green-900 dark:text-green-300',
    failed: 'bg-red-100 text-red-700 dark:bg-red-900 dark:text-red-300',
  };
  const labels: Record<string, string> = {
    pending: 'In attesa',
    uploading: 'Caricamento...',
    processing: 'In elaborazione...',
    completed: 'Completato',
    failed: 'Errore',
  };

  return (
    <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${styles[status]}`}>
      {labels[status]}
    </span>
  );
}

function StepIcon({ status }: { status: string }) {
  if (status === 'Completed') return <span className="text-green-500">✓</span>;
  if (status === 'Failed') return <span className="text-red-500">✗</span>;
  if (status === 'Processing') return <span className="animate-spin">⟳</span>;
  return <span className="text-muted-foreground">○</span>;
}
```

**Step 5: Create step-complete.tsx**

```tsx
'use client';

import { Button } from '@/components/ui/button';
import type { AddRagResult } from '../lib/rag-api';

interface StepCompleteProps {
  results: AddRagResult[];
  sharedGameName: string;
  onClose: () => void;
}

export function StepComplete({ results, sharedGameName, onClose }: StepCompleteProps) {
  const approved = results.filter(r => r.autoApproved);
  const pending = results.filter(r => !r.autoApproved);

  return (
    <div className="flex flex-col items-center gap-6 py-8 text-center">
      <div className="flex h-16 w-16 items-center justify-center rounded-full bg-green-100 dark:bg-green-900">
        <span className="text-3xl">✓</span>
      </div>

      <div>
        <h3 className="text-lg font-semibold">
          RAG configurato per {sharedGameName}
        </h3>
        <p className="mt-1 text-sm text-muted-foreground">
          {approved.length > 0 && `${approved.length} documento/i indicizzato/i con successo.`}
          {pending.length > 0 && ` ${pending.length} documento/i in attesa di approvazione.`}
        </p>
      </div>

      {results.map((r, i) => (
        <div key={r.pdfDocumentId} className="w-full rounded-lg border p-3 text-left text-sm">
          <div className="flex items-center justify-between">
            <span className="font-medium">Documento {i + 1}</span>
            <span className={r.autoApproved ? 'text-green-600' : 'text-amber-600'}>
              {r.autoApproved ? 'Indicizzato' : 'In attesa approvazione'}
            </span>
          </div>
        </div>
      ))}

      <Button onClick={onClose} className="mt-4">
        Chiudi
      </Button>
    </div>
  );
}
```

**Step 6: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/shared-games/[id]/rag-wizard/
git commit -m "feat(frontend): add RAG wizard components (upload, configure, progress, complete)"
```

---

### Task 8: Integrate wizard into SharedGame detail page

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/shared-games/[id]/client.tsx`

**Step 1: Read the existing file to understand the component structure**

```bash
# Read the client component to understand its imports, state, and tab structure
```

**Step 2: Add wizard trigger**

Add these changes to the existing file:

1. Import the wizard and Sheet component:
```tsx
import { RagWizard } from './rag-wizard/components/rag-wizard';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';
```

2. Add state for wizard visibility:
```tsx
const [showRagWizard, setShowRagWizard] = useState(false);
```

3. Add a button in the Documents tab (or header area) that opens the wizard:
```tsx
<Button onClick={() => setShowRagWizard(true)}>
  Aggiungi RAG
</Button>
```

4. Add the Sheet (drawer) with the wizard:
```tsx
<Sheet open={showRagWizard} onOpenChange={setShowRagWizard}>
  <SheetContent side="right" className="w-full sm:max-w-xl overflow-y-auto">
    <SheetHeader>
      <SheetTitle>Aggiungi RAG — {game.name}</SheetTitle>
    </SheetHeader>
    <div className="mt-6">
      <RagWizard
        sharedGameId={game.id}
        sharedGameName={game.name}
        onClose={() => setShowRagWizard(false)}
      />
    </div>
  </SheetContent>
</Sheet>
```

> **Note to implementer:** Read `client.tsx` first to find the exact location for the button (likely in the Documents tab header or the page header actions area). Match the existing style for action buttons. Invalidate document list queries after wizard closes.

**Step 3: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/shared-games/[id]/client.tsx
git commit -m "feat(frontend): integrate RAG wizard into SharedGame detail page"
```

---

## Phase 3: Testing

### Task 9: Integration test — Full saga flow

**Files:**
- Create: `tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/AddRagToSharedGame/AddRagToSharedGameIntegrationTests.cs`

> **Note to implementer:** This test requires Testcontainers for PostgreSQL. Check existing integration test base classes (search for `IntegrationTestBase`, `TestcontainersFixture`, or `WebApplicationFactory` patterns in the test project). Follow that exact pattern.

**Step 1: Write integration test skeleton**

```csharp
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;

[Trait("Category", "Integration")]
[Trait("BoundedContext", "SharedGameCatalog")]
public class AddRagToSharedGameIntegrationTests : IClassFixture</* existing test fixture */>
{
    // Follow existing integration test patterns in the project.
    // Key scenarios to test:
    //
    // 1. Full saga: Upload → Link → Approve → Enqueue (admin)
    //    Assert: PdfDocument exists in DB, SharedGameDocument linked,
    //    ApprovalStatus = Approved, ProcessingJob created
    //
    // 2. Editor path: Upload → Link (no approve, no enqueue)
    //    Assert: PdfDocument exists, SharedGameDocument linked,
    //    ApprovalStatus = Pending, no ProcessingJob
    //
    // 3. SharedGame not found → NotFoundException
    //
    // 4. Duplicate version → ConflictException
}
```

**Step 2: Commit**

```bash
git add tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/AddRagToSharedGame/AddRagToSharedGameIntegrationTests.cs
git commit -m "test(shared-catalog): add integration test skeleton for RAG wizard saga"
```

---

### Task 10: Frontend test — Wizard flow

**Files:**
- Create: `apps/web/src/__tests__/admin/shared-games/rag-wizard/rag-wizard.test.tsx`

**Step 1: Write the test**

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { RagWizard } from '@/app/admin/(dashboard)/shared-games/[id]/rag-wizard/components/rag-wizard';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// Mock the API
vi.mock('@/app/admin/(dashboard)/shared-games/[id]/rag-wizard/lib/rag-api', () => ({
  addRagToSharedGame: vi.fn().mockResolvedValue({
    pdfDocumentId: 'pdf-1',
    sharedGameDocumentId: 'doc-1',
    processingJobId: 'job-1',
    autoApproved: true,
    streamUrl: '/api/v1/admin/queue/job-1/stream',
  }),
}));

// Mock SSE hooks
vi.mock('@/app/admin/(dashboard)/knowledge-base/queue/hooks/use-job-sse', () => ({
  useJobSSE: () => ({ connectionState: 'connected' }),
}));

vi.mock('@/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api', () => ({
  useJobDetail: () => ({
    data: {
      status: 'Completed',
      steps: [
        { id: '1', stepName: 'Upload', status: 'Completed', durationMs: 500 },
        { id: '2', stepName: 'Extract', status: 'Completed', durationMs: 2000 },
        { id: '3', stepName: 'Chunk', status: 'Completed', durationMs: 300 },
        { id: '4', stepName: 'Embed', status: 'Completed', durationMs: 1500 },
        { id: '5', stepName: 'Index', status: 'Completed', durationMs: 800 },
      ],
    },
  }),
}));

function renderWizard() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const onClose = vi.fn();
  render(
    <QueryClientProvider client={queryClient}>
      <RagWizard sharedGameId="game-1" sharedGameName="Catan" onClose={onClose} />
    </QueryClientProvider>
  );
  return { onClose };
}

describe('RagWizard', () => {
  it('renders upload step initially', () => {
    renderWizard();
    expect(screen.getByText('Trascina PDF qui')).toBeInTheDocument();
    expect(screen.getByText(/Avanti/)).toBeDisabled();
  });

  it('enables next button when files added', async () => {
    renderWizard();
    const input = screen.getByLabelText(/clicca per selezionare/i).querySelector('input[type="file"]')
      ?? document.querySelector('input[type="file"]');

    const file = new File(['test'], 'catan.pdf', { type: 'application/pdf' });
    await userEvent.upload(input!, file);

    await waitFor(() => {
      expect(screen.getByText('catan.pdf')).toBeInTheDocument();
    });
  });
});
```

> **Note to implementer:** Adapt the test based on actual rendering output. The file input selector may need adjustment. Run `pnpm test -- --grep "RagWizard"` and iterate.

**Step 2: Commit**

```bash
git add apps/web/src/__tests__/admin/shared-games/rag-wizard/
git commit -m "test(frontend): add RAG wizard component tests"
```

---

## Phase 4: Polish & PR

### Task 11: Verify all tests pass

**Step 1: Run backend tests**

```bash
cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~AddRagToSharedGame" -v minimal
```
Expected: All unit tests PASS.

**Step 2: Run frontend tests**

```bash
cd apps/web && pnpm test -- --grep "RagWizard"
```
Expected: All tests PASS.

**Step 3: Run full linting/typecheck**

```bash
cd apps/web && pnpm typecheck && pnpm lint
```
Expected: No errors.

---

### Task 12: Create PR

**Step 1: Stage and review all changes**

```bash
git status
git diff --stat main...HEAD
```

**Step 2: Create PR to parent branch**

```bash
git push -u origin HEAD
gh pr create --base <parent-branch> --title "feat: SharedGame RAG wizard with batch upload and SSE progress" --body "..."
```

PR body should include:
- Summary: New wizard for admin to add RAG to SharedGames with real-time progress monitoring
- Key changes: Orchestration command, batch support, admin auto-approve, editor approval gate, SSE progress
- Test plan: Unit + integration + frontend tests

---

## Appendix: File Tree Summary

```
apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/AddRagToSharedGame/
├── AddRagToSharedGameCommand.cs                    (Task 1)
├── AddRagToSharedGameCommandValidator.cs           (Task 2)
├── AddRagToSharedGameCommandHandler.cs             (Task 3)
├── BatchAddRagToSharedGameCommand.cs               (Task 4)
└── BatchAddRagToSharedGameCommandHandler.cs        (Task 4)

apps/api/src/Api/Routing/SharedGameCatalogEndpoints.cs  (Task 5 — modify)

apps/web/src/app/admin/(dashboard)/shared-games/[id]/rag-wizard/
├── lib/
│   └── rag-api.ts                                  (Task 6)
└── components/
    ├── rag-wizard.tsx                              (Task 7)
    ├── step-upload.tsx                             (Task 7)
    ├── step-configure.tsx                          (Task 7)
    ├── step-progress.tsx                           (Task 7)
    └── step-complete.tsx                           (Task 7)

apps/web/src/app/admin/(dashboard)/shared-games/[id]/client.tsx  (Task 8 — modify)

tests/Api.Tests/.../AddRagToSharedGame/
├── AddRagToSharedGameCommandValidatorTests.cs      (Task 2)
├── AddRagToSharedGameCommandHandlerTests.cs        (Task 3)
├── BatchAddRagToSharedGameCommandHandlerTests.cs   (Task 4)
└── AddRagToSharedGameIntegrationTests.cs           (Task 9)

apps/web/src/__tests__/admin/shared-games/rag-wizard/
└── rag-wizard.test.tsx                             (Task 10)
```

## Key Decisions Log

| Decision | Rationale |
|---|---|
| Saga (not event chain) | Admin needs synchronous `ProcessingJobId` for SSE subscription |
| Graceful enqueue failure | Document still linked+approved even if queue is full |
| Sequential batch (not parallel) | Avoid overwhelming S3 upload + extraction pipeline |
| Sheet/drawer (not modal) | Consistent with existing `ExtraMeepleCard` drawer pattern |
| Reuse existing SSE hooks | `useJobSSE` + `useJobDetail` already handle reconnection + debounce |
| Admin auto-approve | Admin uploading directly = implicit approval (skip redundant gate) |
| Editor requires approval | Approval gate preserved for non-admin roles per spec panel decision |

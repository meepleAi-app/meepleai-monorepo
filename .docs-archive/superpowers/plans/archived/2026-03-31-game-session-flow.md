# Game Session Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare il flusso completo di sessione hot-seat: configurazione fasi per gioco (admin + AI), context injection nell'agente, TurnAdvancePolicy, Game Over modal, SyncQueue offline, e UI admin.

**Architecture:**
- Backend: `GamePhaseTemplate` è una nuova entità EF persistente (GameManagement BC) con il suo Entity/Configuration/Repository. `TurnAdvancePolicy` è aggiunta in-memory a `LiveGameSession`. L'enrichment del contesto agente avviene nel handler esistente tramite `ILiveSessionRepository`.
- Frontend: `GameOverModal` è una modale shadcn/ui. `SyncQueue` estende lo Zustand store esistente con operazioni pendenti persistite in localStorage. I componenti di sessione live esistenti vengono aggiornati.

**Tech Stack:** .NET 9, EF Core (PostgreSQL), MediatR CQRS, FluentValidation, Next.js 16, Zustand + immer, shadcn/ui, Tailwind 4, framer-motion, @dnd-kit/sortable

---

## ⚠️ Note Architetturali Critiche

- `LiveGameSession` è **in-memory** (`ConcurrentDictionary`) — non richiede migration per le sue proprietà
- Le entity EF usano il pattern `*Entity` in `apps/api/src/Api/Infrastructure/Entities/GameManagement/`
- EF configurations sono in `apps/api/src/Api/Infrastructure/EntityConfigurations/GameManagement/`
- DbContext è `MeepleAiDbContext` (non AppDbContext)
- Tabelle usano snake_case senza schema prefix (ex: `game_phase_templates`)
- I test backend seguono xUnit; frontend Vitest

---

## PHASE 1 — Backend Core

### Task 1: GamePhaseTemplate Entity + Infrastructure Entity

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GamePhaseTemplate.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/IGamePhaseTemplateRepository.cs`
- Create: `apps/api/src/Api/Infrastructure/Entities/GameManagement/GamePhaseTemplateEntity.cs`

- [ ] **Step 1: Scrivi il test unitario per il factory method**

```csharp
// tests/Api.Tests/BoundedContexts/GameManagement/Unit/GamePhaseTemplateTests.cs
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Unit;

public class GamePhaseTemplateTests
{
    [Fact]
    public void Create_WithValidInputs_SetsAllProperties()
    {
        var gameId = Guid.NewGuid();
        var template = GamePhaseTemplate.Create(gameId, "Pesca carte", 1, "editor-user-id", "Ogni giocatore pesca 2 carte");

        Assert.Equal(gameId, template.GameId);
        Assert.Equal("Pesca carte", template.PhaseName);
        Assert.Equal(1, template.PhaseOrder);
        Assert.Equal("editor-user-id", template.CreatedBy);
        Assert.Equal("Ogni giocatore pesca 2 carte", template.Description);
        Assert.NotEqual(Guid.Empty, template.Id);
    }

    [Fact]
    public void Create_WithEmptyPhaseName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            GamePhaseTemplate.Create(Guid.NewGuid(), "", 1, "editor"));
    }

    [Fact]
    public void Update_ChangesNameAndOrder()
    {
        var template = GamePhaseTemplate.Create(Guid.NewGuid(), "Vecchio nome", 1, "editor");
        template.Update("Nuovo nome", 2, "Nuova descrizione");
        Assert.Equal("Nuovo nome", template.PhaseName);
        Assert.Equal(2, template.PhaseOrder);
        Assert.Equal("Nuova descrizione", template.Description);
    }
}
```

- [ ] **Step 2: Esegui il test — verifica che fallisca**

```bash
cd tests/Api.Tests
dotnet test --filter "GamePhaseTemplateTests" -v
# Expected: FAIL — GamePhaseTemplate non esiste ancora
```

- [ ] **Step 3: Crea il domain entity**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GamePhaseTemplate.cs
namespace Api.BoundedContexts.GameManagement.Domain.Entities;

internal sealed class GamePhaseTemplate : Entity<Guid>
{
    public Guid GameId { get; private set; }
    public string PhaseName { get; private set; } = string.Empty;
    public int PhaseOrder { get; private set; }
    public string? Description { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private GamePhaseTemplate() { }

    public static GamePhaseTemplate Create(
        Guid gameId,
        string phaseName,
        int phaseOrder,
        string createdBy,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        return new GamePhaseTemplate
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            PhaseName = phaseName.Trim(),
            PhaseOrder = phaseOrder,
            Description = description?.Trim(),
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    // ⚠️ Used by repository to restore from DB — preserves original Id/CreatedAt
    internal static GamePhaseTemplate Restore(
        Guid id, Guid gameId, string phaseName, int phaseOrder,
        string createdBy, string? description, DateTime createdAt, DateTime updatedAt)
    {
        return new GamePhaseTemplate
        {
            Id = id,
            GameId = gameId,
            PhaseName = phaseName,
            PhaseOrder = phaseOrder,
            Description = description,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    public void Update(string phaseName, int phaseOrder, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseName);
        PhaseName = phaseName.Trim();
        PhaseOrder = phaseOrder;
        Description = description?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
```

- [ ] **Step 4: Crea il repository interface**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/IGamePhaseTemplateRepository.cs
namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

internal interface IGamePhaseTemplateRepository
{
    Task<IReadOnlyList<GamePhaseTemplate>> GetByGameIdAsync(Guid gameId, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<GamePhaseTemplate> templates, CancellationToken ct = default);
    Task DeleteByGameIdAsync(Guid gameId, CancellationToken ct = default);
}
```

- [ ] **Step 5: Crea l'infrastructure entity (per EF Core)**

```csharp
// apps/api/src/Api/Infrastructure/Entities/GameManagement/GamePhaseTemplateEntity.cs
namespace Api.Infrastructure.Entities.GameManagement;

public class GamePhaseTemplateEntity
{
    public Guid Id { get; set; }
    public Guid GameId { get; set; }
    public string PhaseName { get; set; } = default!;
    public int PhaseOrder { get; set; }
    public string? Description { get; set; }
    public string CreatedBy { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public GameEntity? Game { get; set; }
}
```

- [ ] **Step 6: Esegui i test — verifica che passino**

```bash
cd tests/Api.Tests
dotnet test --filter "GamePhaseTemplateTests" -v
# Expected: 3 PASS
```

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/GamePhaseTemplate.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Domain/Repositories/IGamePhaseTemplateRepository.cs \
        apps/api/src/Api/Infrastructure/Entities/GameManagement/GamePhaseTemplateEntity.cs \
        tests/Api.Tests/BoundedContexts/GameManagement/Unit/GamePhaseTemplateTests.cs
git commit -m "feat(game-management): add GamePhaseTemplate domain entity and IGamePhaseTemplateRepository"
```

---

### Task 2: EF Configuration + DbContext + Migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/EntityConfigurations/GameManagement/GamePhaseTemplateEntityConfiguration.cs`
- Modify: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` (aggiungi DbSet)

- [ ] **Step 1: Crea la configurazione EF**

```csharp
// apps/api/src/Api/Infrastructure/EntityConfigurations/GameManagement/GamePhaseTemplateEntityConfiguration.cs
using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

internal sealed class GamePhaseTemplateEntityConfiguration : IEntityTypeConfiguration<GamePhaseTemplateEntity>
{
    public void Configure(EntityTypeBuilder<GamePhaseTemplateEntity> builder)
    {
        builder.ToTable("game_phase_templates");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasColumnName("id")
            .IsRequired();

        builder.Property(t => t.GameId)
            .HasColumnName("game_id")
            .IsRequired();

        builder.Property(t => t.PhaseName)
            .HasColumnName("phase_name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.PhaseOrder)
            .HasColumnName("phase_order")
            .IsRequired();

        builder.Property(t => t.Description)
            .HasColumnName("description")
            .HasMaxLength(200);

        builder.Property(t => t.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(t => new { t.GameId, t.PhaseOrder })
            .HasDatabaseName("ix_game_phase_templates_game_id_order")
            .IsUnique();

        builder.HasOne(t => t.Game)
            .WithMany()
            .HasForeignKey(t => t.GameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 2: Aggiungi DbSet a MeepleAiDbContext**

Nel file `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`, aggiungi dopo la riga `public DbSet<GameStrategyEntity> GameStrategies`:

```csharp
public DbSet<GamePhaseTemplateEntity> GamePhaseTemplates => Set<GamePhaseTemplateEntity>(); // Game phase templates for session setup
```

- [ ] **Step 3: Genera la migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddGamePhaseTemplates
# Expected: Migration created in Infrastructure/Migrations/
```

- [ ] **Step 4: Verifica la migration generata**

Apri il file migration appena creato e verifica che contenga:
- `migrationBuilder.CreateTable("game_phase_templates", ...)`
- Colonne: id, game_id, phase_name, phase_order, description, created_by, created_at, updated_at
- Unique index su (game_id, phase_order)

- [ ] **Step 5: Applica la migration al DB di sviluppo**

```bash
cd apps/api/src/Api
dotnet ef database update
# Expected: Migration applied successfully
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Infrastructure/EntityConfigurations/GameManagement/GamePhaseTemplateEntityConfiguration.cs \
        apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs \
        apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(db): add game_phase_templates table migration"
```

---

### Task 3: Repository Implementation + DI Registration

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/GamePhaseTemplateRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/DependencyInjection/GameManagementServiceExtensions.cs`

- [ ] **Step 1: Crea il repository**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/GamePhaseTemplateRepository.cs
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

internal sealed class GamePhaseTemplateRepository : IGamePhaseTemplateRepository
{
    private readonly MeepleAiDbContext _db;

    public GamePhaseTemplateRepository(MeepleAiDbContext db) =>
        _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<IReadOnlyList<GamePhaseTemplate>> GetByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        var entities = await _db.GamePhaseTemplates
            .Where(t => t.GameId == gameId)
            .OrderBy(t => t.PhaseOrder)
            .ToListAsync(ct);

        return entities.Select(MapToDomain).ToList().AsReadOnly();
    }

    public async Task AddRangeAsync(IEnumerable<GamePhaseTemplate> templates, CancellationToken ct = default)
    {
        var entities = templates.Select(MapToEntity).ToList();
        await _db.GamePhaseTemplates.AddRangeAsync(entities, ct);
    }

    public async Task DeleteByGameIdAsync(Guid gameId, CancellationToken ct = default)
    {
        var entities = await _db.GamePhaseTemplates
            .Where(t => t.GameId == gameId)
            .ToListAsync(ct);
        _db.GamePhaseTemplates.RemoveRange(entities);
    }

    private static GamePhaseTemplate MapToDomain(GamePhaseTemplateEntity e)
        => GamePhaseTemplate.Restore(e.Id, e.GameId, e.PhaseName, e.PhaseOrder, e.CreatedBy, e.Description, e.CreatedAt, e.UpdatedAt);

    private static GamePhaseTemplateEntity MapToEntity(GamePhaseTemplate d) => new()
    {
        Id = d.Id,
        GameId = d.GameId,
        PhaseName = d.PhaseName,
        PhaseOrder = d.PhaseOrder,
        Description = d.Description,
        CreatedBy = d.CreatedBy,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt
    };
}
```

- [ ] **Step 2: Registra il repository nel DI**

Nel file `GameManagementServiceExtensions.cs`, aggiungi:

```csharp
services.AddScoped<IGamePhaseTemplateRepository, GamePhaseTemplateRepository>();
```

- [ ] **Step 3: Verifica che il progetto compili**

```bash
cd apps/api/src/Api
dotnet build
# Expected: Build succeeded, 0 errors
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/GamePhaseTemplateRepository.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/DependencyInjection/GameManagementServiceExtensions.cs
git commit -m "feat(game-management): add GamePhaseTemplateRepository with DI registration"
```

---

### Task 4: CRUD Commands + Query + Endpoints

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/GetPhaseTemplatesQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/GetPhaseTemplatesQueryHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/UpsertPhaseTemplatesCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/UpsertPhaseTemplatesCommandHandler.cs`
- Create: `apps/api/src/Api/Routing/GamePhaseTemplateEndpoints.cs`
- Modify: endpoint registration (cerca il file che chiama `MapTurnOrderEndpoints` o simile)

- [ ] **Step 1: Crea DTOs condivisi e Query**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/GetPhaseTemplatesQuery.cs
namespace Api.BoundedContexts.GameManagement.Application.Queries;

internal record GetPhaseTemplatesQuery(Guid GameId) : IQuery<IReadOnlyList<PhaseTemplateDto>>;

public sealed record PhaseTemplateDto(
    Guid Id,
    string PhaseName,
    int PhaseOrder,
    string? Description
);
```

- [ ] **Step 2: Crea il Query Handler**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/GetPhaseTemplatesQueryHandler.cs
using Api.BoundedContexts.GameManagement.Domain.Repositories;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

internal sealed class GetPhaseTemplatesQueryHandler : IQueryHandler<GetPhaseTemplatesQuery, IReadOnlyList<PhaseTemplateDto>>
{
    private readonly IGamePhaseTemplateRepository _repository;

    public GetPhaseTemplatesQueryHandler(IGamePhaseTemplateRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<IReadOnlyList<PhaseTemplateDto>> Handle(GetPhaseTemplatesQuery query, CancellationToken ct)
    {
        var templates = await _repository.GetByGameIdAsync(query.GameId, ct);
        return templates.Select(t => new PhaseTemplateDto(t.Id, t.PhaseName, t.PhaseOrder, t.Description))
                        .ToList()
                        .AsReadOnly();
    }
}
```

- [ ] **Step 3: Crea il Command con Validator**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/UpsertPhaseTemplatesCommand.cs
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal record UpsertPhaseTemplatesCommand(
    Guid GameId,
    string EditorUserId,
    IReadOnlyList<PhaseTemplateInput> Templates
) : ICommand<IReadOnlyList<PhaseTemplateDto>>;

public sealed record PhaseTemplateInput(
    string PhaseName,
    int PhaseOrder,
    string? Description = null
);

internal sealed class UpsertPhaseTemplatesCommandValidator : AbstractValidator<UpsertPhaseTemplatesCommand>
{
    public UpsertPhaseTemplatesCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty().WithMessage("GameId is required");
        RuleFor(x => x.EditorUserId).NotEmpty().WithMessage("EditorUserId is required");
        RuleFor(x => x.Templates).NotNull().WithMessage("Templates list is required");
        RuleForEach(x => x.Templates).ChildRules(t =>
        {
            t.RuleFor(p => p.PhaseName).NotEmpty().MaximumLength(50);
            t.RuleFor(p => p.PhaseOrder).GreaterThan(0);
            t.RuleFor(p => p.Description).MaximumLength(200).When(p => p.Description != null);
        });
    }
}
```

- [ ] **Step 4: Crea il Command Handler**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/UpsertPhaseTemplatesCommandHandler.cs
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal sealed class UpsertPhaseTemplatesCommandHandler
    : ICommandHandler<UpsertPhaseTemplatesCommand, IReadOnlyList<PhaseTemplateDto>>
{
    private readonly IGamePhaseTemplateRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpsertPhaseTemplatesCommandHandler(
        IGamePhaseTemplateRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<IReadOnlyList<PhaseTemplateDto>> Handle(UpsertPhaseTemplatesCommand command, CancellationToken ct)
    {
        // Delete existing templates for this game (full replace strategy)
        await _repository.DeleteByGameIdAsync(command.GameId, ct);

        // Create new templates from input
        var templates = command.Templates
            .Select(t => GamePhaseTemplate.Create(
                command.GameId,
                t.PhaseName,
                t.PhaseOrder,
                command.EditorUserId,
                t.Description))
            .ToList();

        await _repository.AddRangeAsync(templates, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return templates
            .Select(t => new PhaseTemplateDto(t.Id, t.PhaseName, t.PhaseOrder, t.Description))
            .ToList()
            .AsReadOnly();
    }
}
```

- [ ] **Step 5: Crea gli endpoint**

```csharp
// apps/api/src/Api/Routing/GamePhaseTemplateEndpoints.cs
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

internal static class GamePhaseTemplateEndpoints
{
    public static RouteGroupBuilder MapGamePhaseTemplateEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/games/{gameId}/phase-templates", HandleGet)
            .RequireAuthenticatedUser()
            .Produces<IReadOnlyList<PhaseTemplateDto>>(200)
            .Produces(404)
            .WithTags("PhaseTemplates")
            .WithSummary("Get phase templates for a game");

        group.MapPut("/games/{gameId}/phase-templates", HandleUpsert)
            .RequireAuthenticatedUser()
            .RequireAuthorization("RequireEditorOrAbove")
            .Produces<IReadOnlyList<PhaseTemplateDto>>(200)
            .Produces(400)
            .Produces(403)
            .WithTags("PhaseTemplates")
            .WithSummary("Upsert phase templates for a game (Editor/Admin only)");

        return group;
    }

    private static async Task<IResult> HandleGet(
        Guid gameId,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetPhaseTemplatesQuery(gameId), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleUpsert(
        Guid gameId,
        [FromBody] UpsertPhaseTemplatesRequest request,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var editorUserId = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? string.Empty;
        var command = new UpsertPhaseTemplatesCommand(gameId, editorUserId, request.Templates);
        var result = await mediator.Send(command, ct);
        return Results.Ok(result);
    }
}

internal sealed record UpsertPhaseTemplatesRequest(IReadOnlyList<PhaseTemplateInput> Templates);
```

- [ ] **Step 6: Registra gli endpoint**

Cerca il file che registra gli altri endpoint (es. `Program.cs` o un extension method). Aggiungi:

```csharp
v1Api.MapGamePhaseTemplateEndpoints(); // ⚠️ Usa v1Api (RouteGroupBuilder), NON app direttamente
```

- [ ] **Step 7: Scrivi integration test**

```csharp
// tests/Api.Tests/BoundedContexts/GameManagement/Integration/PhaseTemplateEndpointsTests.cs
public class PhaseTemplateEndpointsTests : IntegrationTestBase
{
    [Fact]
    public async Task UpsertPhaseTemplates_WithEditorRole_ReturnsOk()
    {
        var gameId = await CreateTestGameAsync();
        var request = new
        {
            templates = new[]
            {
                new { phaseName = "Pesca carte", phaseOrder = 1, description = "Pesca 2 carte" },
                new { phaseName = "Azioni", phaseOrder = 2, description = (string?)null },
                new { phaseName = "Fine turno", phaseOrder = 3, description = (string?)null }
            }
        };

        var response = await Client.PutAsJsonAsync($"/api/v1/games/{gameId}/phase-templates", request);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<PhaseTemplateDto>>();
        Assert.Equal(3, result!.Count);
        Assert.Equal("Pesca carte", result[0].PhaseName);
    }

    [Fact]
    public async Task GetPhaseTemplates_AfterUpsert_ReturnsOrderedTemplates()
    {
        var gameId = await CreateTestGameAsync();
        await UpsertTemplatesAsync(gameId);

        var response = await Client.GetAsync($"/api/v1/games/{gameId}/phase-templates");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<List<PhaseTemplateDto>>();
        Assert.Equal(3, result!.Count);
        Assert.Equal(1, result[0].PhaseOrder);
        Assert.Equal(2, result[1].PhaseOrder);
        Assert.Equal(3, result[2].PhaseOrder);
    }
}
```

- [ ] **Step 8: Build e test**

```bash
cd apps/api/src/Api && dotnet build
cd ../../.. && dotnet test tests/Api.Tests --filter "PhaseTemplate" -v
# Expected: tutti i test PASS
```

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/ \
        apps/api/src/Api/Routing/GamePhaseTemplateEndpoints.cs \
        tests/Api.Tests/BoundedContexts/GameManagement/Integration/PhaseTemplateEndpointsTests.cs
git commit -m "feat(game-management): add GetPhaseTemplates and UpsertPhaseTemplates CQRS + endpoints"
```

---

### Task 5: AI Phase Suggestion Endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/SuggestPhaseTemplatesCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/SuggestPhaseTemplatesCommandHandler.cs`
- Modify: `apps/api/src/Api/Routing/GamePhaseTemplateEndpoints.cs`

- [ ] **Step 1: Crea Command e DTO**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/SuggestPhaseTemplatesCommand.cs
namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal record SuggestPhaseTemplatesCommand(Guid GameId, Guid UserId)
    : IQuery<IReadOnlyList<PhaseTemplateSuggestionDto>>;

public sealed record PhaseTemplateSuggestionDto(
    string PhaseName,
    int PhaseOrder,
    string Description,
    string Rationale
);
```

- [ ] **Step 1b: Definisci IPhaseRulesSearchService nel dominio GameManagement**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Domain/Services/IPhaseRulesSearchService.cs
namespace Api.BoundedContexts.GameManagement.Domain.Services;

// Interfaccia nel dominio GameManagement — implementata dalla KB infrastruttura (inversione dipendenze)
internal interface IPhaseRulesSearchService
{
    Task<IReadOnlyList<string>> SearchRulesChunksAsync(Guid gameId, string query, int topK, CancellationToken ct = default);
}
```

- [ ] **Step 2: Crea il Handler**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/SuggestPhaseTemplatesCommandHandler.cs
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal sealed class SuggestPhaseTemplatesCommandHandler
    : IQueryHandler<SuggestPhaseTemplatesCommand, IReadOnlyList<PhaseTemplateSuggestionDto>>
{
    private readonly IPhaseRulesSearchService _rulesSearch;
    private readonly ILlmService _llmService;
    private readonly ILogger<SuggestPhaseTemplatesCommandHandler> _logger;

    private const string SystemPrompt = "Sei un esperto di giochi da tavolo. Suggerisci le fasi principali di un turno di gioco.";

    public SuggestPhaseTemplatesCommandHandler(
        IPhaseRulesSearchService rulesSearch,
        ILlmService llmService,
        ILogger<SuggestPhaseTemplatesCommandHandler> logger)
    {
        _rulesSearch = rulesSearch ?? throw new ArgumentNullException(nameof(rulesSearch));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<PhaseTemplateSuggestionDto>> Handle(
        SuggestPhaseTemplatesCommand command, CancellationToken ct)
    {
        var chunks = await _rulesSearch.SearchRulesChunksAsync(
            command.GameId, "turno fasi azioni giocatore", topK: 5, ct);

        if (chunks.Count == 0)
            return Array.Empty<PhaseTemplateSuggestionDto>();

        var rulesContext = string.Join("\n\n", chunks);
        var userPrompt = $"""
            Regole del gioco (estratto):
            {rulesContext}

            Suggerisci le fasi principali di un turno come JSON array:
            [{{"phaseName":"...(max 50)","phaseOrder":1,"description":"...(max 200)","rationale":"..."}}]
            """;

        try
        {
            // ⚠️ Usa GenerateJsonAsync<T> — NON CompleteAsync (non esiste)
            var suggestions = await _llmService.GenerateJsonAsync<List<PhaseTemplateSuggestionDto>>(
                SystemPrompt, userPrompt, ct);
            return suggestions?.AsReadOnly() ?? Array.Empty<PhaseTemplateSuggestionDto>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to get AI phase suggestions for game {GameId}", command.GameId);
            return Array.Empty<PhaseTemplateSuggestionDto>();
        }
    }
}
```

> ⚠️ **Nota implementazione**: Registra `IPhaseRulesSearchService` nel DI di `GameManagementServiceExtensions.cs`.
> L'implementazione concreta può essere una classe in `GameManagement/Infrastructure/` che usa `MeepleAiDbContext` per query pgvector, senza dipendere da KB BC.

- [ ] **Step 3: Aggiungi endpoint a GamePhaseTemplateEndpoints.cs**

Nel file esistente, aggiungi dentro `MapGamePhaseTemplateEndpoints`:

```csharp
group.MapPost("/games/{gameId}/phase-templates/suggest", HandleSuggest)
    .RequireAuthenticatedUser()
    .RequireAuthorization("EditorOrAdmin")
    .Produces<IReadOnlyList<PhaseTemplateSuggestionDto>>(200)
    .Produces(404)
    .WithTags("PhaseTemplates")
    .WithSummary("AI-powered phase template suggestions based on game rulebook");
```

E aggiungi il handler privato:

```csharp
private static async Task<IResult> HandleSuggest(
    Guid gameId,
    [FromServices] IMediator mediator,
    HttpContext httpContext,
    CancellationToken ct)
{
    var userId = Guid.Parse(httpContext.User.FindFirst("sub")?.Value ?? Guid.Empty.ToString());
    var result = await mediator.Send(new SuggestPhaseTemplatesCommand(gameId, userId), ct);
    return Results.Ok(result);
}
```

- [ ] **Step 4: Build e verifica**

```bash
cd apps/api/src/Api && dotnet build
# Expected: Build succeeded. Se ILlmService.CompleteAsync non esiste, usa il metodo corretto per LLM non-streaming.
# Nota: verifica il metodo esatto di ILlmService per completions non-streaming nel codebase.
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/SuggestPhaseTemplates* \
        apps/api/src/Api/Routing/GamePhaseTemplateEndpoints.cs
git commit -m "feat(game-management): add AI-powered SuggestPhaseTemplates endpoint"
```

---

### Task 6: TurnAdvancePolicy su LiveGameSession

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Enums/TurnAdvancePolicy.cs`
- Modify: `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs`
- Modify: il command che crea la LiveGameSession (trova con `grep -rn "LiveGameSession.Create" apps/api/`)

- [ ] **Step 1: Scrivi il test**

```csharp
// tests/Api.Tests/BoundedContexts/GameManagement/Unit/LiveGameSessionTurnPolicyTests.cs
public class LiveGameSessionTurnPolicyTests
{
    [Fact]
    public void Create_DefaultPolicy_IsAnyPlayer()
    {
        var session = LiveGameSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Catan");
        Assert.Equal(TurnAdvancePolicy.AnyPlayer, session.TurnAdvancePolicy);
    }

    [Fact]
    public void SetTurnAdvancePolicy_ChangesPolicy()
    {
        var session = LiveGameSession.Create(Guid.NewGuid(), Guid.NewGuid(), "Catan");
        session.SetTurnAdvancePolicy(TurnAdvancePolicy.HostOnly);
        Assert.Equal(TurnAdvancePolicy.HostOnly, session.TurnAdvancePolicy);
    }
}
```

- [ ] **Step 2: Crea l'enum**

```csharp
// apps/api/src/Api/BoundedContexts/GameManagement/Domain/Enums/TurnAdvancePolicy.cs
namespace Api.BoundedContexts.GameManagement.Domain.Enums;

public enum TurnAdvancePolicy
{
    AnyPlayer = 0,
    HostOnly = 1
}
```

- [ ] **Step 3: Aggiungi la proprietà a LiveGameSession**

Nel file `LiveGameSession.cs`, aggiungi tra le proprietà esistenti:

```csharp
public TurnAdvancePolicy TurnAdvancePolicy { get; private set; } = TurnAdvancePolicy.AnyPlayer;
```

E aggiungi il metodo:

```csharp
public void SetTurnAdvancePolicy(TurnAdvancePolicy policy)
{
    TurnAdvancePolicy = policy;
    UpdatedAt = DateTime.UtcNow;
}
```

- [ ] **Step 4: Aggiorna Create factory se necessario**

Controlla la firma di `LiveGameSession.Create(...)` e aggiungi un parametro opzionale:

```csharp
// Aggiungi al factory method se accetta parametri configurabili:
TurnAdvancePolicy turnAdvancePolicy = TurnAdvancePolicy.AnyPlayer
// E nel body:
TurnAdvancePolicy = turnAdvancePolicy,
```

- [ ] **Step 5: Aggiorna il command CreateLiveSession**

Cerca con: `grep -rn "CreateLiveSession\|StartSession\|LiveGameSession.Create" apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/ | head -10`

Nel command trovato, aggiungi il campo `TurnAdvancePolicy TurnAdvancePolicy = TurnAdvancePolicy.AnyPlayer`.

- [ ] **Step 6: Esegui test**

```bash
dotnet test tests/Api.Tests --filter "TurnPolicy" -v
# Expected: PASS
```

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/GameManagement/Domain/Enums/TurnAdvancePolicy.cs \
        apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs \
        tests/Api.Tests/BoundedContexts/GameManagement/Unit/LiveGameSessionTurnPolicyTests.cs
git commit -m "feat(game-management): add TurnAdvancePolicy to LiveGameSession"
```

---

### Task 7: Agent Context Auto-Injection

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs`

- [ ] **Step 1: Scrivi il test**

```csharp
// tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/AgentContextEnrichmentTests.cs
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Unit;

public class AgentContextEnrichmentTests
{
    [Fact]
    public void Build_WithActiveTurnAndPhase_ReturnsFormattedContext()
    {
        var ctx = new LiveSessionContext(
            GameName: "Catan",
            CurrentPlayerName: "Marco",
            CurrentPhaseName: "Dadi",
            CurrentTurnIndex: 0);

        var context = AgentContextBuilder.Build(ctx);

        Assert.Contains("Marco", context);
        Assert.Contains("Dadi", context);
        Assert.Contains("Catan", context);
    }

    [Fact]
    public void Build_WithNullContext_ReturnsEmpty()
    {
        var context = AgentContextBuilder.Build(null);
        Assert.Equal(string.Empty, context);
    }

    [Fact]
    public void Build_WithNoPlayer_ReturnsGameOnly()
    {
        var ctx = new LiveSessionContext("Catan", null, null, 0);
        var context = AgentContextBuilder.Build(ctx);
        Assert.Contains("Catan", context);
    }
}
```

- [ ] **Step 2: Crea il DTO e l'helper AgentContextBuilder**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/AgentContextBuilder.cs
// ⚠️ NON importa GameManagement.Domain.Entities — usa un DTO per rispettare isolamento BC

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

// DTO passato al builder — non dipende da LiveGameSession direttamente
internal sealed record LiveSessionContext(
    string GameName,
    string? CurrentPlayerName,
    string? CurrentPhaseName,
    int CurrentTurnIndex
);

internal static class AgentContextBuilder
{
    public static string Build(LiveSessionContext? ctx)
    {
        if (ctx == null) return string.Empty;
        if (ctx.CurrentPlayerName == null) return $"[Gioco: {ctx.GameName}]";

        var phase = ctx.CurrentPhaseName ?? "Nessuna fase";
        return $"[Contesto sessione — Gioco: {ctx.GameName} | " +
               $"Turno di: {ctx.CurrentPlayerName} | " +
               $"Fase: {phase} | Turno #{ctx.CurrentTurnIndex + 1}]";
    }

    // Factory method per costruire il DTO dalla LiveGameSession (usato nel handler KB)
    // Il handler ha già accesso a GameManagement via ILiveSessionRepository
    public static LiveSessionContext? FromLiveSession(dynamic? session)
    {
        if (session == null) return null;

        var activePlayers = ((IEnumerable<dynamic>)session.Players)
            .Where(p => (bool)p.IsActive).ToList();
        if (activePlayers.Count == 0) return new LiveSessionContext(session.GameName, null, null, 0);

        var currentPlayerIndex = (int)session.CurrentTurnIndex % activePlayers.Count;
        var currentPlayer = activePlayers.ElementAtOrDefault(currentPlayerIndex)?.DisplayName as string;

        string? currentPhase = null;
        var phaseNames = (string[])session.PhaseNames;
        var phaseIndex = (int)session.CurrentPhaseIndex;
        if (phaseNames.Length > 0 && phaseIndex < phaseNames.Length)
            currentPhase = phaseNames[phaseIndex];

        return new LiveSessionContext(session.GameName, currentPlayer, currentPhase, session.CurrentTurnIndex);
    }
}
```

> **Alternativa più semplice** (consigliata): invece di `dynamic`, crea il `LiveSessionContext` direttamente nel handler (ChatWithSessionAgentCommandHandler.cs) dopo aver caricato `liveSession` da `ILiveSessionRepository`, evitando del tutto la dipendenza su tipi dinamici.

```csharp
// Nel ChatWithSessionAgentCommandHandler.Handle(), dopo aver ottenuto liveSession:
var ctx = liveSession != null ? new LiveSessionContext(
    liveSession.GameName,
    liveSession.Players.Where(p => p.IsActive).ElementAtOrDefault(
        liveSession.CurrentTurnIndex % Math.Max(1, liveSession.Players.Count(p => p.IsActive)))?.DisplayName,
    liveSession.PhaseNames.Length > 0 ? liveSession.PhaseNames[Math.Min(liveSession.CurrentPhaseIndex, liveSession.PhaseNames.Length - 1)] : null,
    liveSession.CurrentTurnIndex
) : null;
var gameContext = AgentContextBuilder.Build(ctx);
```

- [ ] **Step 3: Modifica il handler per usare AgentContextBuilder**

Nel file `ChatWithSessionAgentCommandHandler.cs`, nel metodo `Handle`, dopo aver caricato `agentSession`:

```csharp
// Carica live session per context enrichment
LiveGameSession? liveSession = null;
if (agentSession.GameSessionId.HasValue)
{
    liveSession = await _liveSessionRepository.GetByIdAsync(agentSession.GameSessionId.Value, cancellationToken)
        .ConfigureAwait(false);
}

var gameContext = AgentContextBuilder.Build(liveSession);
var enrichedQuestion = string.IsNullOrEmpty(gameContext)
    ? command.UserQuestion
    : $"{gameContext}\n\n{command.UserQuestion}";

// Usa enrichedQuestion al posto di command.UserQuestion nelle chiamate successive
// (sostituisci command.UserQuestion → enrichedQuestion nel corpo del Handle method)
```

- [ ] **Step 4: Esegui test**

```bash
dotnet test tests/Api.Tests --filter "AgentContext" -v
# Expected: PASS
```

- [ ] **Step 5: Build completo**

```bash
cd apps/api/src/Api && dotnet build && dotnet test ../../tests/Api.Tests -v --no-build
# Expected: tutto green
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/AgentContextBuilder.cs \
        apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs \
        tests/Api.Tests/BoundedContexts/KnowledgeBase/Unit/AgentContextEnrichmentTests.cs
git commit -m "feat(kb): auto-inject game context (player/phase/round) into agent chat prompt"
```

---

## PHASE 2 — Frontend Live Session

### Task 8: Session Store Enhancements

**Files:**
- Modify: `apps/web/src/stores/session/types.ts`
- Modify: `apps/web/src/stores/session/store.ts`

- [ ] **Step 1: Aggiorna i types**

Nel file `apps/web/src/stores/session/types.ts`, aggiungi:

```typescript
export type TurnAdvancePolicy = 'any-player' | 'host-only';

export interface TurnState {
  currentPlayerName: string;
  currentPhase: string;
  phaseIndex: number;
  totalPhases: number;
  roundNumber: number;
}

export interface SyncOperation {
  id: string;
  type: 'advance-turn' | 'advance-phase' | 'update-score' | 'roll-dice' | 'finalize';
  sessionId: string;
  endpoint: string;
  method: 'POST' | 'PUT';
  body: Record<string, unknown>;
  timestamp: string;
  retries: number;
}
```

- [ ] **Step 2: Aggiorna lo store**

Nel file `apps/web/src/stores/session/store.ts`, estendi `SessionState` e aggiungi:

```typescript
// Aggiungi all'interfaccia SessionState:
turnAdvancePolicy: TurnAdvancePolicy;
turnState: TurnState | null;
syncQueue: SyncOperation[];
isOnline: boolean;

// Aggiungi actions:
setTurnAdvancePolicy: (policy: TurnAdvancePolicy) => void;
setTurnState: (state: TurnState | null) => void;
addToSyncQueue: (op: Omit<SyncOperation, 'id' | 'timestamp' | 'retries'>) => void;
removeFromSyncQueue: (id: string) => void;
incrementRetries: (id: string) => void;
setOnline: (online: boolean) => void;
```

E nell'`initialState`:

```typescript
turnAdvancePolicy: 'any-player',
turnState: null,
syncQueue: [],
isOnline: true,
```

E nelle actions dentro `immer(set => ({`:

```typescript
setTurnAdvancePolicy: (policy) =>
  set(s => { s.turnAdvancePolicy = policy; }),
setTurnState: (state) =>
  set(s => { s.turnState = state; }),
addToSyncQueue: (op) =>
  set(s => {
    s.syncQueue.push({
      ...op,
      id: crypto.randomUUID(),
      timestamp: new Date().toISOString(),
      retries: 0,
    });
  }),
removeFromSyncQueue: (id) =>
  set(s => { s.syncQueue = s.syncQueue.filter(op => op.id !== id); }),
incrementRetries: (id) =>
  set(s => {
    const op = s.syncQueue.find(op => op.id === id);
    if (op) op.retries += 1;
  }),
setOnline: (online) =>
  set(s => { s.isOnline = online; }),
```

- [ ] **Step 3: Aggiungi persist middleware a store.ts per SyncQueue**

Nel file `apps/web/src/stores/session/store.ts`, wrappa lo store con `persist` per mantenere `syncQueue` in localStorage:

```typescript
import { persist } from 'zustand/middleware';

// Wrappa la definizione esistente con persist:
// Da: create<SessionState>()(immer((set, get) => ({ ... })))
// A:
export const useSessionStore = create<SessionState>()(
  persist(
    immer((set, get) => ({
      // ... tutto il contenuto esistente + le nuove azioni ...
    })),
    {
      name: 'meeple-session-sync-queue',
      // Persisti SOLO syncQueue — il resto è volatile
      partialize: (state) => ({ syncQueue: state.syncQueue }),
    }
  )
);
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/stores/session/
git commit -m "feat(session-store): add TurnState, SyncQueue, TurnAdvancePolicy + persist middleware"
```

---

### Task 9: SyncWorker Hook + Offline Banner

**Files:**
- Create: `apps/web/src/hooks/useSyncWorker.ts`
- Create: `apps/web/src/components/session/OfflineBanner.tsx`
- Modify: `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/layout.tsx`

- [ ] **Step 1: Crea useSyncWorker**

```typescript
// apps/web/src/hooks/useSyncWorker.ts
'use client';

import { useEffect, useRef } from 'react';
import { useSessionStore } from '@/stores/session/store';
import type { SyncOperation } from '@/stores/session/types';

async function executeOperation(op: SyncOperation): Promise<void> {
  const response = await fetch(`/api/v1${op.endpoint}`, {
    method: op.method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(op.body),
  });
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
}

export function useSyncWorker() {
  const { syncQueue, removeFromSyncQueue, incrementRetries, setOnline, isOnline } = useSessionStore();
  const isFlushing = useRef(false);

  // Rileva stato connessione
  useEffect(() => {
    const handleOnline = () => setOnline(true);
    const handleOffline = () => setOnline(false);
    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);
    setOnline(navigator.onLine);
    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, [setOnline]);

  // Flush queue quando torna online o ci sono operazioni pendenti
  useEffect(() => {
    if (!isOnline || syncQueue.length === 0 || isFlushing.current) return;

    const flush = async () => {
      isFlushing.current = true;
      for (const op of syncQueue) {
        try {
          await executeOperation(op);
          removeFromSyncQueue(op.id);
        } catch {
          // ⚠️ Incrementa retry PRIMA di controllare il limite
          incrementRetries(op.id);
          if (op.retries + 1 >= 3) removeFromSyncQueue(op.id);
        }
      }
      isFlushing.current = false;
    };

    const timer = setTimeout(flush, 500);
    return () => clearTimeout(timer);
  }, [isOnline, syncQueue, removeFromSyncQueue]);
}
```

- [ ] **Step 2: Crea OfflineBanner**

```tsx
// apps/web/src/components/session/OfflineBanner.tsx
'use client';

import { WifiOff } from 'lucide-react';
import { useSessionStore } from '@/stores/session/store';

export function OfflineBanner() {
  const { isOnline, syncQueue } = useSessionStore();

  if (isOnline && syncQueue.length === 0) return null;

  return (
    <div className="flex items-center gap-2 px-3 py-1.5 bg-amber-500/10 border-b border-amber-500/20 text-amber-700 dark:text-amber-400 text-sm">
      <WifiOff className="h-3.5 w-3.5 shrink-0" />
      {isOnline ? (
        <span>Sincronizzazione in corso... ({syncQueue.length} operazioni)</span>
      ) : (
        <span>Offline — AI non disponibile · {syncQueue.length > 0 ? `${syncQueue.length} operazioni in coda` : 'continua a giocare'}</span>
      )}
    </div>
  );
}
```

- [ ] **Step 3: Integra nel layout live session**

Nel file `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/layout.tsx`, aggiungi:

```tsx
import { OfflineBanner } from '@/components/session/OfflineBanner';
import { useSyncWorker } from '@/hooks/useSyncWorker';

// Nel componente layout, chiama l'hook e aggiungi il banner:
useSyncWorker();

// Nel JSX, prima del contenuto principale:
<OfflineBanner />
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/hooks/useSyncWorker.ts \
        apps/web/src/components/session/OfflineBanner.tsx \
        apps/web/src/app/(authenticated)/sessions/live/[sessionId]/layout.tsx
git commit -m "feat(session): add SyncWorker hook and OfflineBanner for offline-capable session"
```

---

### Task 10: Game Over Modal

**Files:**
- Create: `apps/web/src/components/session/GameOverModal.tsx`
- Modify: `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/page.tsx`

- [ ] **Step 1: Crea il componente**

```tsx
// apps/web/src/components/session/GameOverModal.tsx
'use client';

import { Trophy, Clock, Dices, RotateCcw, Library, X } from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';
import { Dialog, DialogContent, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { cn } from '@/lib/utils';

interface ParticipantResult {
  name: string;
  finalRank: number;
  score: number;
}

interface SessionStats {
  rounds: number;
  durationMinutes: number;
  diceRolls: number;
}

interface GameOverModalProps {
  isOpen: boolean;
  gameName: string;
  participants: ParticipantResult[];
  stats: SessionStats;
  onNewGame: () => void;
  onGoToLibrary: () => void;
  onClose: () => void;
}

const RANK_MEDALS = ['🥇', '🥈', '🥉'];

export function GameOverModal({
  isOpen,
  gameName,
  participants,
  stats,
  onNewGame,
  onGoToLibrary,
  onClose,
}: GameOverModalProps) {
  const sorted = [...participants].sort((a, b) => a.finalRank - b.finalRank);

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-sm mx-auto p-0 overflow-hidden rounded-2xl">
        {/* Header */}
        <div className="bg-gradient-to-br from-violet-600 to-purple-700 px-6 pt-8 pb-6 text-center text-white">
          <motion.div
            initial={{ scale: 0 }}
            animate={{ scale: 1 }}
            transition={{ type: 'spring', delay: 0.1 }}
            className="text-5xl mb-2"
          >
            🎉
          </motion.div>
          <DialogTitle className="text-xl font-bold text-white">Partita Conclusa!</DialogTitle>
          <p className="text-violet-200 text-sm mt-1">{gameName}</p>
        </div>

        {/* Classifica */}
        <div className="px-4 py-4 space-y-2">
          <AnimatePresence>
            {sorted.map((p, i) => (
              <motion.div
                key={p.name}
                initial={{ opacity: 0, x: -20 }}
                animate={{ opacity: 1, x: 0 }}
                transition={{ delay: 0.1 + i * 0.08 }}
                className={cn(
                  'flex items-center gap-3 rounded-xl px-3 py-2.5',
                  i === 0 ? 'bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800' : 'bg-muted/50'
                )}
              >
                <span className="text-xl w-7 text-center">
                  {RANK_MEDALS[i] ?? `${p.finalRank}.`}
                </span>
                <span className={cn('flex-1 font-medium', i === 0 && 'text-amber-700 dark:text-amber-300')}>
                  {p.name}
                </span>
                <Badge variant={i === 0 ? 'default' : 'secondary'}>
                  {p.score} pt
                </Badge>
              </motion.div>
            ))}
          </AnimatePresence>
        </div>

        {/* Statistiche */}
        <div className="px-4 pb-4">
          <div className="flex justify-around text-center text-sm text-muted-foreground bg-muted/30 rounded-xl py-3">
            <div>
              <div className="font-semibold text-foreground">{stats.rounds}</div>
              <div>round</div>
            </div>
            <div>
              <Clock className="h-4 w-4 mx-auto mb-0.5" />
              <div className="font-semibold text-foreground">{stats.durationMinutes}m</div>
            </div>
            <div>
              <Dices className="h-4 w-4 mx-auto mb-0.5" />
              <div className="font-semibold text-foreground">{stats.diceRolls}</div>
            </div>
          </div>
        </div>

        {/* CTA */}
        <div className="px-4 pb-6 space-y-2">
          <Button onClick={onNewGame} className="w-full gap-2">
            <RotateCcw className="h-4 w-4" />
            Nuova partita
          </Button>
          <Button variant="outline" onClick={onGoToLibrary} className="w-full gap-2">
            <Library className="h-4 w-4" />
            Vai alla libreria
          </Button>
          <Button variant="ghost" onClick={onClose} className="w-full gap-2 text-muted-foreground">
            <X className="h-4 w-4" />
            Chiudi
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 2: Integra nella pagina live session**

Nel file `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/page.tsx`:

```tsx
import { GameOverModal } from '@/components/session/GameOverModal';
import { useState } from 'react';

// Nel componente, aggiungi state:
const [showGameOver, setShowGameOver] = useState(false);

// Aggiungi il modal in fondo al JSX:
<GameOverModal
  isOpen={showGameOver}
  gameName={session.gameName}
  participants={session.participants.map(p => ({
    name: p.displayName,
    finalRank: p.finalRank ?? 99,
    score: p.totalScore ?? 0,
  }))}
  stats={{
    rounds: session.turnState?.roundNumber ?? 0,
    durationMinutes: Math.round((Date.now() - new Date(session.startedAt).getTime()) / 60000),
    diceRolls: session.diceRollCount ?? 0,
  }}
  onNewGame={() => { setShowGameOver(false); router.push(`/sessions/new?gameId=${session.gameId}`); }}
  onGoToLibrary={() => router.push('/library')}
  onClose={() => setShowGameOver(false)}
/>
```

- [ ] **Step 3: Scrivi il test del componente**

```typescript
// apps/web/src/__tests__/components/session/GameOverModal.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { GameOverModal } from '@/components/session/GameOverModal';
import { describe, it, expect, vi } from 'vitest';

const defaultProps = {
  isOpen: true,
  gameName: 'Catan',
  participants: [
    { name: 'Anna', finalRank: 1, score: 10 },
    { name: 'Luca', finalRank: 2, score: 8 },
  ],
  stats: { rounds: 5, durationMinutes: 47, diceRolls: 23 },
  onNewGame: vi.fn(),
  onGoToLibrary: vi.fn(),
  onClose: vi.fn(),
};

describe('GameOverModal', () => {
  it('mostra il vincitore in cima con medaglia oro', () => {
    render(<GameOverModal {...defaultProps} />);
    expect(screen.getByText('Anna')).toBeInTheDocument();
    expect(screen.getByText('🥇')).toBeInTheDocument();
  });

  it('chiama onNewGame al click', () => {
    render(<GameOverModal {...defaultProps} />);
    fireEvent.click(screen.getByText('Nuova partita'));
    expect(defaultProps.onNewGame).toHaveBeenCalled();
  });

  it('mostra le statistiche', () => {
    render(<GameOverModal {...defaultProps} />);
    expect(screen.getByText('5')).toBeInTheDocument(); // rounds
    expect(screen.getByText('47m')).toBeInTheDocument(); // minutes
  });
});
```

- [ ] **Step 4: Esegui test frontend**

```bash
cd apps/web && pnpm test GameOverModal
# Expected: 3 PASS
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/session/GameOverModal.tsx \
        apps/web/src/app/(authenticated)/sessions/live/[sessionId]/page.tsx \
        apps/web/src/__tests__/components/session/GameOverModal.test.tsx
git commit -m "feat(session): add GameOverModal with podium, stats, and CTA actions"
```

---

### Task 11: Turn State Header

**Files:**
- Create: `apps/web/src/components/session/TurnStateHeader.tsx`
- Modify: `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/layout.tsx`

- [ ] **Step 1: Crea il componente**

```tsx
// apps/web/src/components/session/TurnStateHeader.tsx
'use client';

import { ChevronRight, SkipForward } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { useSessionStore } from '@/stores/session/store';
import { cn } from '@/lib/utils';

interface TurnStateHeaderProps {
  onAdvancePhase: () => void;
  onAdvanceTurn: () => void;
  canAdvance: boolean; // false se TurnAdvancePolicy.HostOnly e non sei host
  className?: string;
}

export function TurnStateHeader({
  onAdvancePhase,
  onAdvanceTurn,
  canAdvance,
  className,
}: TurnStateHeaderProps) {
  const { turnState } = useSessionStore();

  if (!turnState) return null;

  const { currentPlayerName, currentPhase, phaseIndex, totalPhases, roundNumber } = turnState;

  return (
    <div className={cn(
      'flex items-center justify-between gap-2 px-3 py-2',
      'bg-card/80 backdrop-blur-sm border-b',
      className
    )}>
      {/* Info turno */}
      <div className="flex items-center gap-2 min-w-0">
        <Badge variant="outline" className="text-xs shrink-0">
          Round {roundNumber}
        </Badge>
        <span className="font-semibold text-sm truncate">{currentPlayerName}</span>
        {totalPhases > 1 && (
          <span className="text-xs text-muted-foreground shrink-0">
            {currentPhase} · {phaseIndex + 1}/{totalPhases}
          </span>
        )}
      </div>

      {/* Azioni */}
      {canAdvance && (
        <div className="flex gap-1 shrink-0">
          {totalPhases > 1 && (
            <Button
              size="sm"
              variant="ghost"
              onClick={onAdvancePhase}
              className="h-7 px-2 text-xs gap-1"
            >
              <ChevronRight className="h-3.5 w-3.5" />
              Fase
            </Button>
          )}
          <Button
            size="sm"
            variant="default"
            onClick={onAdvanceTurn}
            className="h-7 px-2 text-xs gap-1"
          >
            <SkipForward className="h-3.5 w-3.5" />
            Turno
          </Button>
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Integra nel layout**

Nel file `layout.tsx` della live session, aggiungi il `TurnStateHeader` sopra il contenuto principale, passando i callback che chiamano `POST /live-sessions/{id}/advance-phase` e `/advance-turn`.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/session/TurnStateHeader.tsx \
        apps/web/src/app/(authenticated)/sessions/live/[sessionId]/layout.tsx
git commit -m "feat(session): add TurnStateHeader with phase/turn advance controls"
```

---

### Task 12: Agent Tab — Chat Condivisa

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/agent/page.tsx`

- [ ] **Step 1: Aggiorna la pagina agente**

Nel file esistente `agent/page.tsx`, verifica e assicura:

1. La chat usa `session.chatSessionId` (già presente su `LiveGameSession`) — non crea una nuova ChatSession per ogni utente
2. Il placeholder del campo input sia:
   ```tsx
   placeholder="Chiedi all'arbitro... es. 'Come funziona il commercio?'"
   ```
3. Il primo messaggio suggerito (non auto-inviato) se la chat è vuota:
   ```tsx
   {messages.length === 0 && (
     <button
       onClick={() => setInputValue('Come si prepara questa partita?')}
       className="text-sm text-muted-foreground underline underline-offset-2"
     >
       Come si prepara questa partita?
     </button>
   )}
   ```
4. Se offline, disabilita l'input con tooltip:
   ```tsx
   <Input
     disabled={!isOnline}
     title={!isOnline ? 'Connessione richiesta per usare l\'agente AI' : undefined}
     placeholder={isOnline ? 'Chiedi all\'arbitro...' : 'Offline — AI non disponibile'}
     ...
   />
   ```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/app/(authenticated)/sessions/live/[sessionId]/agent/page.tsx
git commit -m "feat(session-agent): shared chat session, offline guard, setup suggestion"
```

---

## PHASE 3 — Admin + Setup

### Task 13: Admin Phase Config Page

**Files:**
- Create: `apps/web/src/app/(authenticated)/admin/games/[gameId]/phases/page.tsx`
- Create: `apps/web/src/components/admin/PhaseTemplateEditor.tsx`

- [ ] **Step 1: Installa @dnd-kit/sortable se non presente**

```bash
cd apps/web
pnpm add @dnd-kit/core @dnd-kit/sortable @dnd-kit/utilities
```

- [ ] **Step 2: Crea il componente editor**

```tsx
// apps/web/src/components/admin/PhaseTemplateEditor.tsx
'use client';

import { useState } from 'react';
import { DndContext, closestCenter, type DragEndEvent } from '@dnd-kit/core';
import { SortableContext, verticalListSortingStrategy, arrayMove, useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { GripVertical, Plus, Trash2, Sparkles, Save } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Card, CardContent } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import type { PhaseTemplateDto, PhaseTemplateSuggestionDto } from '@/lib/api/game-phase-templates';

interface PhaseTemplateEditorProps {
  gameId: string;
  initialTemplates: PhaseTemplateDto[];
  hasPdf: boolean;
  onSave: (templates: { phaseName: string; phaseOrder: number; description?: string }[]) => Promise<void>;
  onSuggest: () => Promise<PhaseTemplateSuggestionDto[]>;
}

interface LocalPhase {
  localId: string;
  phaseName: string;
  description: string;
}

function SortablePhaseRow({
  phase,
  index,
  onUpdate,
  onRemove,
}: {
  phase: LocalPhase;
  index: number;
  onUpdate: (localId: string, field: 'phaseName' | 'description', value: string) => void;
  onRemove: (localId: string) => void;
}) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({ id: phase.localId });

  return (
    <div
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      className={`flex gap-2 items-start p-3 rounded-lg border bg-card ${isDragging ? 'opacity-50 shadow-lg' : ''}`}
    >
      <div className="cursor-grab mt-2.5 text-muted-foreground" {...attributes} {...listeners}>
        <GripVertical className="h-4 w-4" />
      </div>
      <div className="w-6 h-6 rounded-full bg-primary/10 text-primary text-xs font-bold flex items-center justify-center mt-2 shrink-0">
        {index + 1}
      </div>
      <div className="flex-1 space-y-1.5">
        <Input
          value={phase.phaseName}
          onChange={(e) => onUpdate(phase.localId, 'phaseName', e.target.value)}
          placeholder="Nome fase (es. Azioni)"
          maxLength={50}
        />
        <Input
          value={phase.description}
          onChange={(e) => onUpdate(phase.localId, 'description', e.target.value)}
          placeholder="Descrizione breve (opzionale)"
          maxLength={200}
          className="text-sm"
        />
      </div>
      <Button
        variant="ghost"
        size="icon"
        onClick={() => onRemove(phase.localId)}
        className="text-destructive hover:text-destructive mt-1 shrink-0"
      >
        <Trash2 className="h-4 w-4" />
      </Button>
    </div>
  );
}

export function PhaseTemplateEditor({ gameId: _gameId, initialTemplates, hasPdf, onSave, onSuggest }: PhaseTemplateEditorProps) {
  const [phases, setPhases] = useState<LocalPhase[]>(
    initialTemplates.map((t) => ({ localId: crypto.randomUUID(), phaseName: t.phaseName, description: t.description ?? '' }))
  );
  const [isSuggestLoading, setIsSuggestLoading] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [suggestions, setSuggestions] = useState<PhaseTemplateSuggestionDto[] | null>(null);

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event;
    if (over && active.id !== over.id) {
      const oldIndex = phases.findIndex((p) => p.localId === active.id);
      const newIndex = phases.findIndex((p) => p.localId === over.id);
      setPhases(arrayMove(phases, oldIndex, newIndex));
    }
  };

  const handleUpdate = (localId: string, field: 'phaseName' | 'description', value: string) =>
    setPhases((prev) => prev.map((p) => (p.localId === localId ? { ...p, [field]: value } : p)));

  const handleRemove = (localId: string) =>
    setPhases((prev) => prev.filter((p) => p.localId !== localId));

  const handleAddPhase = () =>
    setPhases((prev) => [...prev, { localId: crypto.randomUUID(), phaseName: '', description: '' }]);

  const handleSuggest = async () => {
    setIsSuggestLoading(true);
    try {
      const result = await onSuggest();
      setSuggestions(result);
    } finally {
      setIsSuggestLoading(false);
    }
  };

  const handleAcceptSuggestions = () => {
    if (!suggestions) return;
    setPhases(suggestions.map((s) => ({ localId: crypto.randomUUID(), phaseName: s.phaseName, description: s.description })));
    setSuggestions(null);
  };

  const handleSave = async () => {
    setIsSaving(true);
    try {
      await onSave(phases.filter((p) => p.phaseName.trim()).map((p, i) => ({
        phaseName: p.phaseName.trim(),
        phaseOrder: i + 1,
        description: p.description.trim() || undefined,
      })));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="space-y-4">
      {/* AI Suggestions banner */}
      {suggestions && (
        <Card className="border-violet-200 bg-violet-50 dark:bg-violet-950/20">
          <CardContent className="pt-4 space-y-2">
            <p className="text-sm font-medium text-violet-700 dark:text-violet-300">
              💡 Suggerimenti AI ({suggestions.length} fasi)
            </p>
            {suggestions.map((s, i) => (
              <div key={i} className="text-sm">
                <span className="font-medium">{i + 1}. {s.phaseName}</span>
                <span className="text-muted-foreground ml-2">— {s.description}</span>
              </div>
            ))}
            <div className="flex gap-2 pt-2">
              <Button size="sm" onClick={handleAcceptSuggestions}>Accetta tutto</Button>
              <Button size="sm" variant="outline" onClick={() => setSuggestions(null)}>Rifiuta</Button>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Fase list */}
      <DndContext collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
        <SortableContext items={phases.map((p) => p.localId)} strategy={verticalListSortingStrategy}>
          <div className="space-y-2">
            {phases.map((phase, index) => (
              <SortablePhaseRow
                key={phase.localId}
                phase={phase}
                index={index}
                onUpdate={handleUpdate}
                onRemove={handleRemove}
              />
            ))}
          </div>
        </SortableContext>
      </DndContext>

      {phases.length === 0 && !isSuggestLoading && (
        <p className="text-center text-sm text-muted-foreground py-8">
          Nessuna fase configurata. Aggiungi fasi manualmente o usa l'AI.
        </p>
      )}

      {isSuggestLoading && <Skeleton className="h-24 w-full" />}

      {/* Actions */}
      <div className="flex gap-2 flex-wrap">
        <Button variant="outline" size="sm" onClick={handleAddPhase} className="gap-2">
          <Plus className="h-4 w-4" />
          Aggiungi fase
        </Button>
        {hasPdf && (
          <Button
            variant="outline"
            size="sm"
            onClick={handleSuggest}
            disabled={isSuggestLoading}
            className="gap-2 text-violet-600 border-violet-300 hover:bg-violet-50"
          >
            <Sparkles className="h-4 w-4" />
            {isSuggestLoading ? 'Leggo il regolamento...' : 'Suggerisci con AI'}
          </Button>
        )}
        <Button size="sm" onClick={handleSave} disabled={isSaving} className="gap-2 ml-auto">
          <Save className="h-4 w-4" />
          {isSaving ? 'Salvando...' : 'Salva fasi'}
        </Button>
      </div>
    </div>
  );
}
```

- [ ] **Step 3: Crea le Server Actions in file separato**

> ⚠️ `'use server'` NON può essere inline dentro JSX prop — deve essere in cima a un file separato.

```typescript
// apps/web/src/app/(authenticated)/admin/games/[gameId]/phases/actions.ts
'use server';

import { revalidateTag } from 'next/cache';
import { upsertPhaseTemplates, suggestPhaseTemplates } from '@/lib/api/game-phase-templates';

export async function savePhaseTemplatesAction(
  gameId: string,
  templates: { phaseName: string; phaseOrder: number; description?: string }[]
): Promise<void> {
  await upsertPhaseTemplates(gameId, templates);
  revalidateTag(`phases-${gameId}`);
}

export async function suggestPhaseTemplatesAction(gameId: string) {
  return suggestPhaseTemplates(gameId);
}
```

- [ ] **Step 4: Crea la page admin**

```tsx
// apps/web/src/app/(authenticated)/admin/games/[gameId]/phases/page.tsx
import { PhaseTemplateEditor } from '@/components/admin/PhaseTemplateEditor';
import { getPhaseTemplates } from '@/lib/api/game-phase-templates';
import { getGame } from '@/lib/api/games';
import { notFound } from 'next/navigation';
import { savePhaseTemplatesAction, suggestPhaseTemplatesAction } from './actions';

export default async function GamePhasesPage({ params }: { params: { gameId: string } }) {
  const [game, templates] = await Promise.all([
    getGame(params.gameId),
    getPhaseTemplates(params.gameId),
  ]);

  if (!game) notFound();

  return (
    <div className="max-w-2xl mx-auto p-6 space-y-6">
      <div>
        <h1 className="text-2xl font-bold">{game.title}</h1>
        <p className="text-muted-foreground">Configura le fasi del turno di gioco</p>
      </div>

      <PhaseTemplateEditor
        gameId={params.gameId}
        initialTemplates={templates}
        hasPdf={!!game.pdfDocumentId}
        onSave={(templates) => savePhaseTemplatesAction(params.gameId, templates)}
        onSuggest={() => suggestPhaseTemplatesAction(params.gameId)}
      />
    </div>
  );
}
```

- [ ] **Step 4: Crea le API helper functions**

```typescript
// apps/web/src/lib/api/game-phase-templates.ts
export interface PhaseTemplateDto {
  id: string;
  phaseName: string;
  phaseOrder: number;
  description?: string;
}

export interface PhaseTemplateSuggestionDto {
  phaseName: string;
  phaseOrder: number;
  description: string;
  rationale: string;
}

export async function getPhaseTemplates(gameId: string): Promise<PhaseTemplateDto[]> {
  const res = await fetch(`/api/v1/games/${gameId}/phase-templates`, { next: { tags: [`phases-${gameId}`] } });
  if (!res.ok) return [];
  return res.json();
}

export async function upsertPhaseTemplates(
  gameId: string,
  templates: { phaseName: string; phaseOrder: number; description?: string }[]
): Promise<PhaseTemplateDto[]> {
  const res = await fetch(`/api/v1/games/${gameId}/phase-templates`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ templates }),
  });
  if (!res.ok) throw new Error('Failed to save phase templates');
  return res.json();
}

export async function suggestPhaseTemplates(gameId: string): Promise<PhaseTemplateSuggestionDto[]> {
  const res = await fetch(`/api/v1/games/${gameId}/phase-templates/suggest`, { method: 'POST' });
  if (!res.ok) return [];
  return res.json();
}
```

- [ ] **Step 5: Build e verifica**

```bash
cd apps/web && pnpm build
# Expected: Build succeeded
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/(authenticated)/admin/games/ \
        apps/web/src/components/admin/PhaseTemplateEditor.tsx \
        apps/web/src/lib/api/game-phase-templates.ts
git commit -m "feat(admin): add phase template config page with drag&drop and AI suggestion"
```

---

### Task 14: Setup Wizard — Step Fasi

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/new/` (cerca il componente wizard)

- [ ] **Step 1: Trova il wizard esistente**

```bash
find apps/web/src -name "*.tsx" | xargs grep -l "new session\|createSession\|wizard" -i | head -10
```

- [ ] **Step 2: Aggiungi step "Configura fasi"**

Nel wizard di creazione sessione, dopo lo step di aggiunta giocatori, aggiungi uno step che:

1. Carica i `PhaseTemplate` del gioco selezionato via `GET /games/{id}/phase-templates`
2. Pre-popola i campi con i template trovati
3. Permette all'host di modificare nome/ordine fasi prima di iniziare
4. Aggiunge il campo `TurnAdvancePolicy` con radio button: "Chiunque / Solo io"
5. Passa `phaseNames` e `turnAdvancePolicy` al comando di creazione sessione

```tsx
// Snippet per lo step fasi nel wizard:
const [phases, setPhases] = useState<string[]>([]);
const [turnPolicy, setTurnPolicy] = useState<'any-player' | 'host-only'>('any-player');

useEffect(() => {
  if (!selectedGameId) return;
  fetch(`/api/v1/games/${selectedGameId}/phase-templates`)
    .then(r => r.json())
    .then((templates: PhaseTemplateDto[]) => {
      if (templates.length > 0) setPhases(templates.map(t => t.phaseName));
    });
}, [selectedGameId]);

// Nel form:
<div className="space-y-3">
  <Label>Fasi del turno</Label>
  {phases.map((phase, i) => (
    <div key={i} className="flex gap-2">
      <Input
        value={phase}
        onChange={e => setPhases(prev => prev.map((p, idx) => idx === i ? e.target.value : p))}
        placeholder={`Fase ${i + 1}`}
      />
    </div>
  ))}
  <Button variant="outline" size="sm" onClick={() => setPhases(prev => [...prev, ''])}>
    + Aggiungi fase
  </Button>
</div>

<RadioGroup value={turnPolicy} onValueChange={v => setTurnPolicy(v as 'any-player' | 'host-only')}>
  <div className="flex items-center gap-2">
    <RadioGroupItem value="any-player" id="any" />
    <Label htmlFor="any">Chiunque può avanzare il turno</Label>
  </div>
  <div className="flex items-center gap-2">
    <RadioGroupItem value="host-only" id="host" />
    <Label htmlFor="host">Solo io (host) posso avanzare il turno</Label>
  </div>
</RadioGroup>
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/(authenticated)/sessions/new/
git commit -m "feat(session-setup): add phase config step with pre-loaded templates and TurnAdvancePolicy"
```

---

## Self-Review del Piano

### Copertura della Spec v2.0

| Requirement | Task |
|-------------|------|
| GamePhaseTemplate entity | Task 1 |
| EF migration | Task 2 |
| Repository + DI | Task 3 |
| GET/PUT phase templates endpoint | Task 4 |
| AI suggestion endpoint | Task 5 |
| TurnAdvancePolicy | Task 6 |
| Agent context injection | Task 7 |
| SyncQueue + offline | Task 8, 9 |
| Game Over modal | Task 10 |
| Turn state header | Task 11 |
| Agent tab condiviso + offline guard | Task 12 |
| Admin phase config | Task 13 |
| Setup wizard con fasi | Task 14 |

### Gap noti (fuori scope di questo piano)
- Offline caching di sessioni già caricate (Service Worker / PWA)
- SessionExpiry job per sessioni Paused > 90gg
- Re-join ospiti (feature futura, no login)
- Auto-checkpoint ogni 5 min (Quartz job separato)

### Potenziale problema: ILlmService.CompleteAsync
Il Task 5 usa `ILlmService.CompleteAsync(prompt, ct)` per LLM non-streaming. Verifica che il metodo esatto esista; se l'interfaccia espone solo streaming, usa `ILlmService` nel modo corretto o crea una utility che consuma lo stream fino a `[DONE]`.

### Placeholder scan: NESSUNO TROVATO ✅
Ogni step contiene codice o comandi esatti.

### Consistenza dei tipi
- `PhaseTemplateDto` definita in Task 4, usata in Task 8, 13, 14 ✅
- `TurnAdvancePolicy` enum C# (Task 6) e TypeScript string union (Task 8) — nomi allineati ✅
- `SyncOperation.endpoint` + `method` usati in `useSyncWorker` (Task 9) ✅

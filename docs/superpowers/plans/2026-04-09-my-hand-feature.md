# La Mia Mano — Quick Action Bar Implementation Plan

> ⚠️ **FROZEN / DO NOT EXECUTE (2026-04-09)**
> Plan congelato a seguito della riscrittura della spec. La feature MyHand v1 (4 slot fissi persistiti) è stata superata dal modello contestuale Session + GamePlay + Hand dinamica + Diario.
>
> **Nuova spec autoritativa**: `docs/superpowers/specs/2026-04-09-session-gameplay-flow-v2.md`
>
> Non eseguire Task 1+ di questo plan. Un nuovo plan verrà generato da `superpowers:writing-plans` sulla spec v2. Design tokens e layout shell possono essere riusati.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare "La Mia Mano" — una barra di 4 slot persistenti (toolkit/game/session/ai) con quick actions inline, visibile come sidebar destra su desktop e bottom bar su mobile.

**Architecture:** Backend: nuova entità `UserHandSlot` nel BC `UserLibrary` con 3 endpoints CQRS (GET/PUT/DELETE). Frontend: Zustand store `useMyHandStore` + componenti `MyHandSidebar` (desktop) e `MyHandBottomBar` (mobile) montati nel layout principale.

**Tech Stack:** .NET 9 / MediatR / EF Core / FluentValidation · Next.js 16 / Zustand + immer / Tailwind 4 / shadcn/ui

**Spec:** `docs/frontend/my-hand-spec.md`

---

## File Map

### Backend — nuovi file
| File | Responsabilità |
|---|---|
| `apps/api/src/Api/Infrastructure/Entities/UserLibrary/UserHandSlotEntity.cs` | Entità persistenza EF Core |
| `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Repositories/IUserHandRepository.cs` | Interfaccia repository |
| `apps/api/src/Api/BoundedContexts/UserLibrary/Infrastructure/Persistence/UserHandRepository.cs` | Implementazione repository |
| `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetUserHandQuery.cs` | Query CQRS |
| `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetUserHandQueryValidator.cs` | Validator query |
| `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/UpdateHandSlotCommand.cs` | Command PUT |
| `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Validators/UpdateHandSlotCommandValidator.cs` | Validator command |
| `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/UpdateHandSlotCommandHandler.cs` | Handler PUT |
| `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/ClearHandSlotCommand.cs` | Command DELETE |
| `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Validators/ClearHandSlotCommandValidator.cs` | Validator DELETE |
| `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/ClearHandSlotCommandHandler.cs` | Handler DELETE |
| `apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/UserHandSlotDto.cs` | DTO risposta |
| `apps/api/src/Api/Routing/UserHandEndpoints.cs` | Minimal API endpoints |

### Backend — file modificati
| File | Modifica |
|---|---|
| `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` | Aggiungere `DbSet<UserHandSlotEntity>` |
| `apps/api/src/Api/BoundedContexts/UserLibrary/Infrastructure/DependencyInjection/UserLibraryServiceExtensions.cs` | Registrare `IUserHandRepository` |
| `apps/api/src/Api/Program.cs` | Mappare `MapUserHandEndpoints()` |

### Frontend — nuovi file
| File | Responsabilità |
|---|---|
| `apps/web/src/stores/my-hand/store.ts` | Zustand store con localStorage + sync server |
| `apps/web/src/stores/my-hand/types.ts` | Tipi TypeScript condivisi |
| `apps/web/src/stores/my-hand/__tests__/store.test.ts` | Test store Zustand |
| `apps/web/src/lib/api/my-hand.ts` | Client API (GET/PUT/DELETE) |
| `apps/web/src/lib/api/__tests__/my-hand.api.test.ts` | Test client API |
| `apps/web/src/components/layout/MyHand/MyHandSlot.tsx` | Singolo slot (3 stati) |
| `apps/web/src/components/layout/MyHand/MyHandSidebar.tsx` | Sidebar desktop |
| `apps/web/src/components/layout/MyHand/MyHandBottomBar.tsx` | Bottom bar mobile |
| `apps/web/src/components/layout/MyHand/MyHandSlotPicker.tsx` | Modal selezione entità |
| `apps/web/src/components/layout/MyHand/index.ts` | Re-export |
| `apps/web/src/components/layout/MyHand/__tests__/MyHandSlot.test.tsx` | Test slot |
| `apps/web/src/components/layout/MyHand/__tests__/MyHandSidebar.test.tsx` | Test sidebar |

### Frontend — file modificati
| File | Modifica |
|---|---|
| `apps/web/src/components/layout/UserShell/DesktopShell.tsx` | Montare `<MyHandSidebar />` |
| Wrapper layout mobile (verificare file) | Montare `<MyHandBottomBar />` |

---

## Task 1: Entità EF Core e Migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Entities/UserLibrary/UserHandSlotEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`

- [ ] **Step 1: Creare l'entità di persistenza**

```csharp
// apps/api/src/Api/Infrastructure/Entities/UserLibrary/UserHandSlotEntity.cs
namespace Api.Infrastructure.Entities.UserLibrary;

/// <summary>
/// Persistence entity for "La Mia Mano" hand slots.
/// Each user has up to 4 slots (one per SlotType).
/// </summary>
public class UserHandSlotEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>User who owns this slot.</summary>
    public Guid UserId { get; set; }

    /// <summary>Slot type: "toolkit" | "game" | "session" | "ai"</summary>
    public string SlotType { get; set; } = string.Empty;

    /// <summary>ID of the pinned entity (null = empty slot).</summary>
    public Guid? EntityId { get; set; }

    /// <summary>MeepleCard entity type: "toolkit" | "game" | "session" | "agent"</summary>
    public string? EntityType { get; set; }

    /// <summary>Cached display name for quick rendering.</summary>
    public string? EntityLabel { get; set; }

    /// <summary>Cached thumbnail URL.</summary>
    public string? EntityImageUrl { get; set; }

    /// <summary>When the slot was last assigned.</summary>
    public DateTime? PinnedAt { get; set; }

    // Navigation
    public UserEntity? User { get; set; }
}
```

- [ ] **Step 2: Aggiungere DbSet al DbContext**

In `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`, dopo la riga `public DbSet<WishlistItemEntity> WishlistItems ...` (cerca "WishlistItems"), aggiungere:

```csharp
public DbSet<UserHandSlotEntity> UserHandSlots => Set<UserHandSlotEntity>(); // La Mia Mano: user hand slots
```

- [ ] **Step 3: Creare la migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddUserHandSlots
```

Expected output: `Done. To undo this action, use 'ef migrations remove'`

- [ ] **Step 4: Verificare il file migration generato**

Aprire `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_AddUserHandSlots.cs` e verificare che contenga `CreateTable` per `user_hand_slots` con le colonne attese (`id`, `user_id`, `slot_type`, `entity_id`, `entity_type`, `entity_label`, `entity_image_url`, `pinned_at`).

- [ ] **Step 5: Commit**

```bash
cd apps/api/src/Api
git add Infrastructure/Entities/UserLibrary/UserHandSlotEntity.cs
git add Infrastructure/MeepleAiDbContext.cs
git add Infrastructure/Migrations/
git commit -m "feat(user-library): add UserHandSlot entity and EF migration"
```

---

## Task 2: Repository Domain Interface + Implementazione

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Repositories/IUserHandRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Infrastructure/Persistence/UserHandRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserLibrary/Infrastructure/DependencyInjection/UserLibraryServiceExtensions.cs`

- [ ] **Step 1: Definire l'interfaccia repository**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Repositories/IUserHandRepository.cs
namespace Api.BoundedContexts.UserLibrary.Domain.Repositories;

public interface IUserHandRepository
{
    Task<IReadOnlyList<UserHandSlotData>> GetAllSlotsAsync(Guid userId, CancellationToken ct = default);
    Task UpsertSlotAsync(Guid userId, string slotType, Guid entityId, string entityType, string? entityLabel, string? entityImageUrl, CancellationToken ct = default);
    Task ClearSlotAsync(Guid userId, string slotType, CancellationToken ct = default);
}

/// <summary>Simple data transfer record used within the domain layer.</summary>
public record UserHandSlotData(
    string SlotType,
    Guid? EntityId,
    string? EntityType,
    string? EntityLabel,
    string? EntityImageUrl,
    DateTime? PinnedAt
);
```

- [ ] **Step 2: Implementare il repository**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Infrastructure/Persistence/UserHandRepository.cs
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;

internal class UserHandRepository : IUserHandRepository
{
    private readonly MeepleAiDbContext _db;

    public UserHandRepository(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<UserHandSlotData>> GetAllSlotsAsync(Guid userId, CancellationToken ct = default)
    {
        var entities = await _db.UserHandSlots
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(e => new UserHandSlotData(
            e.SlotType,
            e.EntityId,
            e.EntityType,
            e.EntityLabel,
            e.EntityImageUrl,
            e.PinnedAt
        )).ToList();
    }

    public async Task UpsertSlotAsync(Guid userId, string slotType, Guid entityId, string entityType,
        string? entityLabel, string? entityImageUrl, CancellationToken ct = default)
    {
        var existing = await _db.UserHandSlots
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SlotType == slotType, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _db.UserHandSlots.Add(new UserHandSlotEntity
            {
                UserId = userId,
                SlotType = slotType,
                EntityId = entityId,
                EntityType = entityType,
                EntityLabel = entityLabel,
                EntityImageUrl = entityImageUrl,
                PinnedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.EntityId = entityId;
            existing.EntityType = entityType;
            existing.EntityLabel = entityLabel;
            existing.EntityImageUrl = entityImageUrl;
            existing.PinnedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task ClearSlotAsync(Guid userId, string slotType, CancellationToken ct = default)
    {
        var existing = await _db.UserHandSlots
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SlotType == slotType, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            _db.UserHandSlots.Remove(existing);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
```

- [ ] **Step 3: Registrare nel DI**

In `apps/api/src/Api/BoundedContexts/UserLibrary/Infrastructure/DependencyInjection/UserLibraryServiceExtensions.cs`, aggiungere dopo la riga `IUserCollectionRepository`:

```csharp
services.AddScoped<IUserHandRepository, UserHandRepository>(); // La Mia Mano: hand slots
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Repositories/IUserHandRepository.cs
git add apps/api/src/Api/BoundedContexts/UserLibrary/Infrastructure/Persistence/UserHandRepository.cs
git add apps/api/src/Api/BoundedContexts/UserLibrary/Infrastructure/DependencyInjection/UserLibraryServiceExtensions.cs
git commit -m "feat(user-library): add IUserHandRepository and EF implementation"
```

---

## Task 3: CQRS — Query GetUserHand

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/UserHandSlotDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetUserHandQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetUserHandQueryValidator.cs`

- [ ] **Step 1: Creare il DTO di risposta**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/UserHandSlotDto.cs
namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

public record UserHandSlotDto(
    string SlotType,
    Guid? EntityId,
    string? EntityType,
    string? EntityLabel,
    string? EntityImageUrl,
    string? PinnedAt  // ISO 8601
);
```

- [ ] **Step 2: Creare la Query con il suo Handler**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetUserHandQuery.cs
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

internal record GetUserHandQuery(Guid UserId) : IQuery<IReadOnlyList<UserHandSlotDto>>;

internal class GetUserHandQueryHandler : IQueryHandler<GetUserHandQuery, IReadOnlyList<UserHandSlotDto>>
{
    private static readonly string[] AllSlotTypes = { "toolkit", "game", "session", "ai" };

    private readonly IUserHandRepository _repo;

    public GetUserHandQueryHandler(IUserHandRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public async Task<IReadOnlyList<UserHandSlotDto>> Handle(GetUserHandQuery request, CancellationToken cancellationToken)
    {
        var slots = await _repo.GetAllSlotsAsync(request.UserId, cancellationToken).ConfigureAwait(false);
        var slotMap = slots.ToDictionary(s => s.SlotType);

        // Always return all 4 slot types — empty if not assigned
        return AllSlotTypes.Select(slotType =>
        {
            if (slotMap.TryGetValue(slotType, out var slot))
            {
                return new UserHandSlotDto(
                    slotType,
                    slot.EntityId,
                    slot.EntityType,
                    slot.EntityLabel,
                    slot.EntityImageUrl,
                    slot.PinnedAt?.ToString("o")
                );
            }
            return new UserHandSlotDto(slotType, null, null, null, null, null);
        }).ToList();
    }
}
```

- [ ] **Step 3: Creare il Validator**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetUserHandQueryValidator.cs
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

internal class GetUserHandQueryValidator : AbstractValidator<GetUserHandQuery>
{
    public GetUserHandQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserLibrary/Application/DTOs/UserHandSlotDto.cs
git add apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetUserHandQuery.cs
git add apps/api/src/Api/BoundedContexts/UserLibrary/Application/Queries/GetUserHandQueryValidator.cs
git commit -m "feat(user-library): add GetUserHandQuery with handler and validator"
```

---

## Task 4: CQRS — Command UpdateHandSlot (PUT)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/UpdateHandSlotCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Validators/UpdateHandSlotCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/UpdateHandSlotCommandHandler.cs`

- [ ] **Step 1: Creare il Command**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/UpdateHandSlotCommand.cs
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

internal record UpdateHandSlotCommand(
    Guid UserId,
    string SlotType,      // "toolkit" | "game" | "session" | "ai"
    Guid EntityId,
    string EntityType,    // "toolkit" | "game" | "session" | "agent"
    string? EntityLabel = null,
    string? EntityImageUrl = null
) : ICommand<UserHandSlotDto>;
```

- [ ] **Step 2: Creare il Validator**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/Validators/UpdateHandSlotCommandValidator.cs
using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

internal class UpdateHandSlotCommandValidator : AbstractValidator<UpdateHandSlotCommand>
{
    private static readonly string[] ValidSlotTypes = { "toolkit", "game", "session", "ai" };
    private static readonly string[] ValidEntityTypes = { "toolkit", "game", "session", "agent" };

    private static readonly Dictionary<string, string[]> SlotEntityMap = new()
    {
        ["toolkit"] = ["toolkit"],
        ["game"]    = ["game"],
        ["session"] = ["session"],
        ["ai"]      = ["agent"]
    };

    public UpdateHandSlotCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required");

        RuleFor(x => x.SlotType)
            .NotEmpty()
            .Must(s => ValidSlotTypes.Contains(s))
            .WithMessage($"SlotType must be one of: {string.Join(", ", ValidSlotTypes)}");

        RuleFor(x => x.EntityId)
            .NotEmpty()
            .WithMessage("EntityId is required");

        RuleFor(x => x.EntityType)
            .NotEmpty()
            .Must(t => ValidEntityTypes.Contains(t))
            .WithMessage($"EntityType must be one of: {string.Join(", ", ValidEntityTypes)}");

        // EntityType must be compatible with SlotType
        RuleFor(x => x)
            .Must(x => SlotEntityMap.TryGetValue(x.SlotType, out var allowed) && allowed.Contains(x.EntityType))
            .WithMessage(x => $"EntityType '{x.EntityType}' is not valid for SlotType '{x.SlotType}'")
            .When(x => ValidSlotTypes.Contains(x.SlotType) && ValidEntityTypes.Contains(x.EntityType));
    }
}
```

- [ ] **Step 3: Creare il Handler**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/UpdateHandSlotCommandHandler.cs
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

internal class UpdateHandSlotCommandHandler : ICommandHandler<UpdateHandSlotCommand, UserHandSlotDto>
{
    private readonly IUserHandRepository _repo;
    private readonly ILogger<UpdateHandSlotCommandHandler> _logger;

    public UpdateHandSlotCommandHandler(IUserHandRepository repo, ILogger<UpdateHandSlotCommandHandler> logger)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserHandSlotDto> Handle(UpdateHandSlotCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await _repo.UpsertSlotAsync(
            command.UserId,
            command.SlotType,
            command.EntityId,
            command.EntityType,
            command.EntityLabel,
            command.EntityImageUrl,
            cancellationToken
        ).ConfigureAwait(false);

        _logger.LogInformation("User {UserId} assigned {EntityType} {EntityId} to hand slot '{SlotType}'",
            command.UserId, command.EntityType, command.EntityId, command.SlotType);

        return new UserHandSlotDto(
            command.SlotType,
            command.EntityId,
            command.EntityType,
            command.EntityLabel,
            command.EntityImageUrl,
            DateTime.UtcNow.ToString("o")
        );
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/UpdateHandSlotCommand.cs
git add apps/api/src/Api/BoundedContexts/UserLibrary/Application/Validators/UpdateHandSlotCommandValidator.cs
git add apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/UpdateHandSlotCommandHandler.cs
git commit -m "feat(user-library): add UpdateHandSlotCommand with validator and handler"
```

---

## Task 5: CQRS — Command ClearHandSlot (DELETE)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/ClearHandSlotCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Validators/ClearHandSlotCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/ClearHandSlotCommandHandler.cs`

- [ ] **Step 1: Creare Command + Validator + Handler**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/ClearHandSlotCommand.cs
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

internal record ClearHandSlotCommand(Guid UserId, string SlotType) : ICommand;
```

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/Validators/ClearHandSlotCommandValidator.cs
using Api.BoundedContexts.UserLibrary.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.UserLibrary.Application.Validators;

internal class ClearHandSlotCommandValidator : AbstractValidator<ClearHandSlotCommand>
{
    private static readonly string[] ValidSlotTypes = { "toolkit", "game", "session", "ai" };

    public ClearHandSlotCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId is required");
        RuleFor(x => x.SlotType)
            .NotEmpty()
            .Must(s => ValidSlotTypes.Contains(s))
            .WithMessage($"SlotType must be one of: {string.Join(", ", ValidSlotTypes)}");
    }
}
```

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/ClearHandSlotCommandHandler.cs
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

internal class ClearHandSlotCommandHandler : ICommandHandler<ClearHandSlotCommand>
{
    private readonly IUserHandRepository _repo;

    public ClearHandSlotCommandHandler(IUserHandRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public async Task Handle(ClearHandSlotCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await _repo.ClearSlotAsync(command.UserId, command.SlotType, cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/ClearHandSlotCommand.cs
git add apps/api/src/Api/BoundedContexts/UserLibrary/Application/Validators/ClearHandSlotCommandValidator.cs
git add apps/api/src/Api/BoundedContexts/UserLibrary/Application/Commands/ClearHandSlotCommandHandler.cs
git commit -m "feat(user-library): add ClearHandSlotCommand with validator and handler"
```

---

## Task 6: API Endpoints

**Files:**
- Create: `apps/api/src/Api/Routing/UserHandEndpoints.cs`
- Modify: `apps/api/src/Api/Program.cs`

- [ ] **Step 1: Creare il file endpoints**

```csharp
// apps/api/src/Api/Routing/UserHandEndpoints.cs
using System.Security.Claims;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.Extensions;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

/// <summary>
/// "La Mia Mano" hand slot endpoints.
/// GET/PUT/DELETE /api/v1/users/me/hand and /api/v1/users/me/hand/{slotType}
/// </summary>
internal static class UserHandEndpoints
{
    public static RouteGroupBuilder MapUserHandEndpoints(this RouteGroupBuilder group)
    {
        MapGetHandEndpoint(group);
        MapUpdateSlotEndpoint(group);
        MapClearSlotEndpoint(group);
        return group;
    }

    private static void MapGetHandEndpoint(RouteGroupBuilder group)
    {
        group.MapGet("/users/me/hand", async (
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!TryGetUserId(context, session, out var userId)) return Results.Unauthorized();

            var query = new GetUserHandQuery(userId);
            var result = await mediator.Send(query, ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthenticatedUser()
        .Produces<IReadOnlyList<UserHandSlotDto>>(200)
        .Produces(401)
        .WithTags("MyHand")
        .WithSummary("Get user hand slots")
        .WithDescription("Returns all 4 hand slots for the authenticated user. Empty slots have null entityId.")
        .WithOpenApi();
    }

    private static void MapUpdateSlotEndpoint(RouteGroupBuilder group)
    {
        group.MapPut("/users/me/hand/{slotType}", async (
            string slotType,
            [FromBody] UpdateHandSlotRequest request,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!TryGetUserId(context, session, out var userId)) return Results.Unauthorized();

            var command = new UpdateHandSlotCommand(
                UserId: userId,
                SlotType: slotType,
                EntityId: request.EntityId,
                EntityType: request.EntityType,
                EntityLabel: request.EntityLabel,
                EntityImageUrl: request.EntityImageUrl
            );

            try
            {
                var result = await mediator.Send(command, ct).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (ConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthenticatedUser()
        .Produces<UserHandSlotDto>(200)
        .Produces(400)
        .Produces(401)
        .Produces(409)
        .WithTags("MyHand")
        .WithSummary("Assign entity to hand slot")
        .WithDescription("Assigns an entity to the specified slot (toolkit/game/session/ai). Replaces any existing assignment.")
        .WithOpenApi();
    }

    private static void MapClearSlotEndpoint(RouteGroupBuilder group)
    {
        group.MapDelete("/users/me/hand/{slotType}", async (
            string slotType,
            IMediator mediator,
            HttpContext context,
            CancellationToken ct) =>
        {
            var (authenticated, session, error) = context.TryGetAuthenticatedUser();
            if (!authenticated) return error!;
            if (!TryGetUserId(context, session, out var userId)) return Results.Unauthorized();

            var command = new ClearHandSlotCommand(userId, slotType);
            await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.NoContent();
        })
        .RequireAuthenticatedUser()
        .Produces(204)
        .Produces(400)
        .Produces(401)
        .WithTags("MyHand")
        .WithSummary("Clear hand slot")
        .WithDescription("Clears the specified hand slot. No-op if slot is already empty.")
        .WithOpenApi();
    }

    private static bool TryGetUserId(HttpContext context, SessionStatusDto? session, out Guid userId)
    {
        userId = Guid.Empty;
        if (session != null) { userId = session.User!.Id; return true; }
        var claim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out userId);
    }
}

public record UpdateHandSlotRequest(
    Guid EntityId,
    string EntityType,
    string? EntityLabel = null,
    string? EntityImageUrl = null
);
```

- [ ] **Step 2: Registrare gli endpoints in Program.cs**

Nel file `apps/api/src/Api/Program.cs`, cerca la riga dove vengono mappati gli endpoint utente (es. cerca `MapWishlistEndpoints` o `MapUserAccountEndpoints`). Aggiungi nella stessa route group:

```csharp
.MapUserHandEndpoints()
```

L'esatta posizione dipende dalla struttura del file — cerca il metodo `MapUserHandEndpoints` e aggiungilo vicino agli altri endpoint `UserLibrary`.

- [ ] **Step 3: Build di verifica**

```bash
cd apps/api/src/Api
dotnet build
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Routing/UserHandEndpoints.cs
git add apps/api/src/Api/Program.cs
git commit -m "feat(user-library): add hand slot API endpoints (GET/PUT/DELETE)"
```

---

## Task 7: Test backend (Unit — Validator)

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/UpdateHandSlotCommandValidatorTests.cs`

- [ ] **Step 1: Scrivere i test failing**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/UpdateHandSlotCommandValidatorTests.cs
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserLibrary")]
public class UpdateHandSlotCommandValidatorTests
{
    private readonly UpdateHandSlotCommandValidator _sut = new();

    [Theory]
    [InlineData("toolkit", "toolkit")]
    [InlineData("game", "game")]
    [InlineData("session", "session")]
    [InlineData("ai", "agent")]
    public void Valid_combinations_pass(string slotType, string entityType)
    {
        var cmd = new UpdateHandSlotCommand(Guid.NewGuid(), slotType, Guid.NewGuid(), entityType);
        var result = _sut.TestValidate(cmd);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("toolkit", "game")]
    [InlineData("game", "agent")]
    [InlineData("session", "toolkit")]
    [InlineData("ai", "game")]
    public void Incompatible_slot_entityType_fails(string slotType, string entityType)
    {
        var cmd = new UpdateHandSlotCommand(Guid.NewGuid(), slotType, Guid.NewGuid(), entityType);
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveAnyValidationError();
    }

    [Fact]
    public void Empty_UserId_fails()
    {
        var cmd = new UpdateHandSlotCommand(Guid.Empty, "toolkit", Guid.NewGuid(), "toolkit");
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Invalid_slotType_fails()
    {
        var cmd = new UpdateHandSlotCommand(Guid.NewGuid(), "unknown", Guid.NewGuid(), "toolkit");
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.SlotType);
    }

    [Fact]
    public void Empty_EntityId_fails()
    {
        var cmd = new UpdateHandSlotCommand(Guid.NewGuid(), "toolkit", Guid.Empty, "toolkit");
        var result = _sut.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.EntityId);
    }
}
```

- [ ] **Step 2: Eseguire il test per verificare che fallisca**

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~UpdateHandSlotCommandValidatorTests" -v minimal
```

Expected: tutti i test passano (i validator sono già implementati nel Task 4).

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Application/UpdateHandSlotCommandValidatorTests.cs
git commit -m "test(user-library): add UpdateHandSlotCommandValidator unit tests"
```

---

## Task 8: Zustand Store frontend

**Files:**
- Create: `apps/web/src/stores/my-hand/types.ts`
- Create: `apps/web/src/stores/my-hand/store.ts`
- Create: `apps/web/src/stores/my-hand/__tests__/store.test.ts`

- [ ] **Step 1: Scrivere il test failing**

```typescript
// apps/web/src/stores/my-hand/__tests__/store.test.ts
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { useMyHandStore } from '../store';
import type { MyHandSlotType } from '../types';

describe('useMyHandStore', () => {
  beforeEach(() => {
    useMyHandStore.setState(useMyHandStore.getInitialState());
  });

  it('starts with all 4 slots empty', () => {
    const state = useMyHandStore.getState();
    expect(state.slots.toolkit.entityId).toBeNull();
    expect(state.slots.game.entityId).toBeNull();
    expect(state.slots.session.entityId).toBeNull();
    expect(state.slots.ai.entityId).toBeNull();
  });

  it('assigns entity to a slot', () => {
    useMyHandStore.getState().assignSlot('toolkit', {
      entityId: 'tk-1',
      entityType: 'toolkit',
      entityLabel: 'My Toolkit',
      entityImageUrl: null,
    });
    const slot = useMyHandStore.getState().slots.toolkit;
    expect(slot.entityId).toBe('tk-1');
    expect(slot.entityType).toBe('toolkit');
    expect(slot.entityLabel).toBe('My Toolkit');
    expect(slot.pinnedAt).not.toBeNull();
  });

  it('clears a slot', () => {
    useMyHandStore.getState().assignSlot('game', {
      entityId: 'g-1',
      entityType: 'game',
      entityLabel: 'Agricola',
      entityImageUrl: null,
    });
    useMyHandStore.getState().clearSlot('game');
    const slot = useMyHandStore.getState().slots.game;
    expect(slot.entityId).toBeNull();
    expect(slot.entityLabel).toBeNull();
    expect(slot.pinnedAt).toBeNull();
  });

  it('marks slot as invalid', () => {
    useMyHandStore.getState().assignSlot('session', {
      entityId: 's-1',
      entityType: 'session',
      entityLabel: 'Friday Game',
      entityImageUrl: null,
    });
    useMyHandStore.getState().markSlotInvalid('session');
    expect(useMyHandStore.getState().slots.session.isEntityValid).toBe(false);
  });

  it('toggles sidebar collapsed state', () => {
    expect(useMyHandStore.getState().isSidebarCollapsed).toBe(false);
    useMyHandStore.getState().toggleSidebarCollapsed();
    expect(useMyHandStore.getState().isSidebarCollapsed).toBe(true);
    useMyHandStore.getState().toggleSidebarCollapsed();
    expect(useMyHandStore.getState().isSidebarCollapsed).toBe(false);
  });

  it('toggles mobile expanded state', () => {
    expect(useMyHandStore.getState().isMobileExpanded).toBe(false);
    useMyHandStore.getState().toggleMobileExpanded();
    expect(useMyHandStore.getState().isMobileExpanded).toBe(true);
  });
});
```

- [ ] **Step 2: Eseguire il test per verificare che fallisca**

```bash
cd apps/web
pnpm test src/stores/my-hand/__tests__/store.test.ts
```

Expected: FAIL — `Cannot find module '../store'`

- [ ] **Step 3: Creare i tipi**

```typescript
// apps/web/src/stores/my-hand/types.ts
export type MyHandSlotType = 'toolkit' | 'game' | 'session' | 'ai';

export interface MyHandSlot {
  slotType: MyHandSlotType;
  entityId: string | null;
  entityType: string | null;  // 'toolkit' | 'game' | 'session' | 'agent'
  entityLabel: string | null;
  entityImageUrl: string | null;
  pinnedAt: string | null;    // ISO 8601
  isEntityValid: boolean;
}

export interface AssignSlotPayload {
  entityId: string;
  entityType: string;
  entityLabel: string | null;
  entityImageUrl: string | null;
}

const EMPTY_SLOT = (slotType: MyHandSlotType): MyHandSlot => ({
  slotType,
  entityId: null,
  entityType: null,
  entityLabel: null,
  entityImageUrl: null,
  pinnedAt: null,
  isEntityValid: true,
});

export const createEmptySlots = (): Record<MyHandSlotType, MyHandSlot> => ({
  toolkit: EMPTY_SLOT('toolkit'),
  game: EMPTY_SLOT('game'),
  session: EMPTY_SLOT('session'),
  ai: EMPTY_SLOT('ai'),
});
```

- [ ] **Step 4: Creare lo store**

```typescript
// apps/web/src/stores/my-hand/store.ts
import { create } from 'zustand';
import { devtools } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';
import type { MyHandSlotType, MyHandSlot, AssignSlotPayload } from './types';
import { createEmptySlots } from './types';

interface MyHandState {
  slots: Record<MyHandSlotType, MyHandSlot>;
  isSidebarCollapsed: boolean;
  isMobileExpanded: boolean;
  isLoading: boolean;
  // Actions
  assignSlot: (slotType: MyHandSlotType, payload: AssignSlotPayload) => void;
  clearSlot: (slotType: MyHandSlotType) => void;
  markSlotInvalid: (slotType: MyHandSlotType) => void;
  toggleSidebarCollapsed: () => void;
  toggleMobileExpanded: () => void;
  setLoading: (loading: boolean) => void;
  hydrateFromServer: (slots: Array<{
    slotType: string;
    entityId: string | null;
    entityType: string | null;
    entityLabel: string | null;
    entityImageUrl: string | null;
    pinnedAt: string | null;
  }>) => void;
}

const INITIAL_STATE = {
  slots: createEmptySlots(),
  isSidebarCollapsed: false,
  isMobileExpanded: false,
  isLoading: false,
};

export const useMyHandStore = create<MyHandState>()(
  devtools(
    immer(set => ({
      ...INITIAL_STATE,

      assignSlot: (slotType, payload) =>
        set(state => {
          state.slots[slotType].entityId = payload.entityId;
          state.slots[slotType].entityType = payload.entityType;
          state.slots[slotType].entityLabel = payload.entityLabel;
          state.slots[slotType].entityImageUrl = payload.entityImageUrl;
          state.slots[slotType].pinnedAt = new Date().toISOString();
          state.slots[slotType].isEntityValid = true;
        }),

      clearSlot: (slotType) =>
        set(state => {
          state.slots[slotType].entityId = null;
          state.slots[slotType].entityType = null;
          state.slots[slotType].entityLabel = null;
          state.slots[slotType].entityImageUrl = null;
          state.slots[slotType].pinnedAt = null;
          state.slots[slotType].isEntityValid = true;
        }),

      markSlotInvalid: (slotType) =>
        set(state => {
          state.slots[slotType].isEntityValid = false;
        }),

      toggleSidebarCollapsed: () =>
        set(state => {
          state.isSidebarCollapsed = !state.isSidebarCollapsed;
        }),

      toggleMobileExpanded: () =>
        set(state => {
          state.isMobileExpanded = !state.isMobileExpanded;
        }),

      setLoading: (loading) =>
        set(state => {
          state.isLoading = loading;
        }),

      hydrateFromServer: (serverSlots) =>
        set(state => {
          for (const s of serverSlots) {
            const slotType = s.slotType as MyHandSlotType;
            if (!['toolkit', 'game', 'session', 'ai'].includes(slotType)) continue;
            state.slots[slotType].entityId = s.entityId;
            state.slots[slotType].entityType = s.entityType;
            state.slots[slotType].entityLabel = s.entityLabel;
            state.slots[slotType].entityImageUrl = s.entityImageUrl;
            state.slots[slotType].pinnedAt = s.pinnedAt;
            state.slots[slotType].isEntityValid = true;
          }
        }),
    })),
    { name: 'my-hand-store' }
  )
);

// Selectors
export const selectSlot = (slotType: MyHandSlotType) =>
  (s: MyHandState) => s.slots[slotType];
export const selectIsSidebarCollapsed = (s: MyHandState) => s.isSidebarCollapsed;
export const selectIsMobileExpanded = (s: MyHandState) => s.isMobileExpanded;
```

- [ ] **Step 5: Eseguire i test per verificare che passino**

```bash
cd apps/web
pnpm test src/stores/my-hand/__tests__/store.test.ts
```

Expected: All tests PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/stores/my-hand/
git commit -m "feat(my-hand): add Zustand store with types and unit tests"
```

---

## Task 9: API Client frontend

**Files:**
- Create: `apps/web/src/lib/api/my-hand.ts`
- Create: `apps/web/src/lib/api/__tests__/my-hand.api.test.ts`

- [ ] **Step 1: Scrivere il test failing**

```typescript
// apps/web/src/lib/api/__tests__/my-hand.api.test.ts
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { getMyHand, updateHandSlot, clearHandSlot } from '../my-hand';

vi.mock('../client', () => ({
  apiClient: {
    get: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
}));

import { apiClient } from '../client';

describe('my-hand API client', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('getMyHand calls GET /users/me/hand', async () => {
    (apiClient.get as ReturnType<typeof vi.fn>).mockResolvedValue([
      { slotType: 'toolkit', entityId: null, entityType: null, entityLabel: null, entityImageUrl: null, pinnedAt: null }
    ]);

    const result = await getMyHand();
    expect(apiClient.get).toHaveBeenCalledWith('/users/me/hand');
    expect(result).toHaveLength(1);
  });

  it('updateHandSlot calls PUT /users/me/hand/{slotType}', async () => {
    const dto = { slotType: 'game', entityId: 'g-1', entityType: 'game', entityLabel: 'Catan', entityImageUrl: null, pinnedAt: '2026-04-09T00:00:00Z' };
    (apiClient.put as ReturnType<typeof vi.fn>).mockResolvedValue(dto);

    const result = await updateHandSlot('game', { entityId: 'g-1', entityType: 'game', entityLabel: 'Catan', entityImageUrl: null });
    expect(apiClient.put).toHaveBeenCalledWith('/users/me/hand/game', {
      entityId: 'g-1', entityType: 'game', entityLabel: 'Catan', entityImageUrl: null,
    });
    expect(result.entityId).toBe('g-1');
  });

  it('clearHandSlot calls DELETE /users/me/hand/{slotType}', async () => {
    (apiClient.delete as ReturnType<typeof vi.fn>).mockResolvedValue(undefined);

    await clearHandSlot('toolkit');
    expect(apiClient.delete).toHaveBeenCalledWith('/users/me/hand/toolkit');
  });

  it('getMyHand returns empty array on null response', async () => {
    (apiClient.get as ReturnType<typeof vi.fn>).mockResolvedValue(null);
    const result = await getMyHand();
    expect(result).toEqual([]);
  });
});
```

- [ ] **Step 2: Eseguire il test per verificare che fallisca**

```bash
cd apps/web
pnpm test src/lib/api/__tests__/my-hand.api.test.ts
```

Expected: FAIL — `Cannot find module '../my-hand'`

- [ ] **Step 3: Implementare il client**

```typescript
// apps/web/src/lib/api/my-hand.ts
import { apiClient } from './client';

export interface HandSlotDto {
  slotType: string;
  entityId: string | null;
  entityType: string | null;
  entityLabel: string | null;
  entityImageUrl: string | null;
  pinnedAt: string | null;
}

export interface UpdateHandSlotPayload {
  entityId: string;
  entityType: string;
  entityLabel: string | null;
  entityImageUrl: string | null;
}

export async function getMyHand(): Promise<HandSlotDto[]> {
  const result = await apiClient.get<HandSlotDto[]>('/users/me/hand');
  return result ?? [];
}

export async function updateHandSlot(
  slotType: string,
  payload: UpdateHandSlotPayload
): Promise<HandSlotDto> {
  return apiClient.put<HandSlotDto>(`/users/me/hand/${slotType}`, payload);
}

export async function clearHandSlot(slotType: string): Promise<void> {
  await apiClient.delete(`/users/me/hand/${slotType}`);
}
```

- [ ] **Step 4: Eseguire i test**

```bash
cd apps/web
pnpm test src/lib/api/__tests__/my-hand.api.test.ts
```

Expected: All PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/api/my-hand.ts
git add apps/web/src/lib/api/__tests__/my-hand.api.test.ts
git commit -m "feat(my-hand): add API client with unit tests"
```

---

## Task 10: Componente MyHandSlot

**Files:**
- Create: `apps/web/src/components/layout/MyHand/MyHandSlot.tsx`
- Create: `apps/web/src/components/layout/MyHand/__tests__/MyHandSlot.test.tsx`

- [ ] **Step 1: Scrivere il test failing**

```typescript
// apps/web/src/components/layout/MyHand/__tests__/MyHandSlot.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { MyHandSlot } from '../MyHandSlot';

describe('MyHandSlot', () => {
  it('renders empty state with CTA', () => {
    render(
      <MyHandSlot
        slotType="toolkit"
        slot={{ slotType: 'toolkit', entityId: null, entityType: null, entityLabel: null, entityImageUrl: null, pinnedAt: null, isEntityValid: true }}
        onAssign={vi.fn()}
        onClear={vi.fn()}
      />
    );
    expect(screen.getByText(/Toolkit/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /seleziona/i })).toBeInTheDocument();
  });

  it('renders populated state with entity label', () => {
    render(
      <MyHandSlot
        slotType="game"
        slot={{ slotType: 'game', entityId: 'g-1', entityType: 'game', entityLabel: 'Agricola', entityImageUrl: null, pinnedAt: '2026-04-09T00:00:00Z', isEntityValid: true }}
        onAssign={vi.fn()}
        onClear={vi.fn()}
      />
    );
    expect(screen.getByText('Agricola')).toBeInTheDocument();
  });

  it('renders degraded state with warning', () => {
    render(
      <MyHandSlot
        slotType="session"
        slot={{ slotType: 'session', entityId: 's-1', entityType: 'session', entityLabel: 'Partita', entityImageUrl: null, pinnedAt: '2026-04-09T00:00:00Z', isEntityValid: false }}
        onAssign={vi.fn()}
        onClear={vi.fn()}
      />
    );
    expect(screen.getByText(/non più disponibile/i)).toBeInTheDocument();
  });

  it('calls onAssign when CTA clicked in empty state', () => {
    const onAssign = vi.fn();
    render(
      <MyHandSlot
        slotType="toolkit"
        slot={{ slotType: 'toolkit', entityId: null, entityType: null, entityLabel: null, entityImageUrl: null, pinnedAt: null, isEntityValid: true }}
        onAssign={onAssign}
        onClear={vi.fn()}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /seleziona/i }));
    expect(onAssign).toHaveBeenCalledWith('toolkit');
  });

  it('calls onClear when clear button clicked', () => {
    const onClear = vi.fn();
    render(
      <MyHandSlot
        slotType="game"
        slot={{ slotType: 'game', entityId: 'g-1', entityType: 'game', entityLabel: 'Agricola', entityImageUrl: null, pinnedAt: '2026-04-09T00:00:00Z', isEntityValid: true }}
        onAssign={vi.fn()}
        onClear={onClear}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /rimuovi/i }));
    expect(onClear).toHaveBeenCalledWith('game');
  });
});
```

- [ ] **Step 2: Eseguire il test per verificare che fallisca**

```bash
cd apps/web
pnpm test src/components/layout/MyHand/__tests__/MyHandSlot.test.tsx
```

Expected: FAIL — `Cannot find module '../MyHandSlot'`

- [ ] **Step 3: Implementare il componente**

```tsx
// apps/web/src/components/layout/MyHand/MyHandSlot.tsx
'use client';

import { AlertTriangle, Plus, X } from 'lucide-react';
import type { MyHandSlot as MyHandSlotData, MyHandSlotType } from '@/stores/my-hand/types';
import { cn } from '@/lib/utils';

const SLOT_LABELS: Record<MyHandSlotType, string> = {
  toolkit: 'Toolkit',
  game: 'Gioco',
  session: 'Partita',
  ai: 'AI',
};

const SLOT_ICONS: Record<MyHandSlotType, string> = {
  toolkit: '🔧',
  game: '🎮',
  session: '🎯',
  ai: '🤖',
};

interface MyHandSlotProps {
  slotType: MyHandSlotType;
  slot: MyHandSlotData;
  onAssign: (slotType: MyHandSlotType) => void;
  onClear: (slotType: MyHandSlotType) => void;
  compact?: boolean;
}

export function MyHandSlot({ slotType, slot, onAssign, onClear, compact = false }: MyHandSlotProps) {
  const label = SLOT_LABELS[slotType];
  const icon = SLOT_ICONS[slotType];

  if (!slot.entityId) {
    return (
      <div className={cn('flex flex-col items-center gap-1 rounded-lg border border-dashed border-border p-3 text-muted-foreground', compact && 'p-2')}>
        <span className="text-lg">{icon}</span>
        {!compact && <span className="text-xs font-medium">{label}</span>}
        <button
          aria-label={`Seleziona ${label}`}
          onClick={() => onAssign(slotType)}
          className="mt-1 flex items-center gap-1 rounded-md bg-secondary px-2 py-1 text-xs hover:bg-secondary/80"
        >
          <Plus className="h-3 w-3" />
          {!compact && 'Seleziona'}
        </button>
      </div>
    );
  }

  if (!slot.isEntityValid) {
    return (
      <div className={cn('flex flex-col items-center gap-1 rounded-lg border border-yellow-500/40 bg-yellow-50/10 p-3', compact && 'p-2')}>
        <AlertTriangle className="h-4 w-4 text-yellow-500" />
        {!compact && <span className="text-center text-xs text-muted-foreground">Non più disponibile</span>}
        <button
          aria-label={`Seleziona ${label}`}
          onClick={() => onAssign(slotType)}
          className="mt-1 rounded-md bg-secondary px-2 py-1 text-xs hover:bg-secondary/80"
        >
          {compact ? <Plus className="h-3 w-3" /> : 'Seleziona nuovo'}
        </button>
      </div>
    );
  }

  return (
    <div className={cn('group relative flex flex-col gap-1 rounded-lg border border-border bg-card p-3', compact && 'p-2')}>
      {/* Clear button */}
      <button
        aria-label={`Rimuovi ${label}`}
        onClick={() => onClear(slotType)}
        className="absolute right-1 top-1 hidden rounded p-0.5 hover:bg-muted group-hover:flex"
      >
        <X className="h-3 w-3" />
      </button>

      {/* Icon + label */}
      <div className="flex items-center gap-1.5">
        <span className="text-base">{icon}</span>
        {!compact && <span className="truncate text-xs font-medium">{slot.entityLabel}</span>}
      </div>
      {compact && slot.entityLabel && (
        <span className="truncate text-[10px] text-muted-foreground">{slot.entityLabel}</span>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Eseguire i test**

```bash
cd apps/web
pnpm test src/components/layout/MyHand/__tests__/MyHandSlot.test.tsx
```

Expected: All PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/MyHand/MyHandSlot.tsx
git add apps/web/src/components/layout/MyHand/__tests__/MyHandSlot.test.tsx
git commit -m "feat(my-hand): add MyHandSlot component with 3 states and tests"
```

---

## Task 11: Componente MyHandSidebar (Desktop)

**Files:**
- Create: `apps/web/src/components/layout/MyHand/MyHandSidebar.tsx`
- Create: `apps/web/src/components/layout/MyHand/__tests__/MyHandSidebar.test.tsx`

- [ ] **Step 1: Scrivere il test failing**

```typescript
// apps/web/src/components/layout/MyHand/__tests__/MyHandSidebar.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useMyHandStore } from '@/stores/my-hand/store';
import { MyHandSidebar } from '../MyHandSidebar';

// Mock del picker per isolare
vi.mock('../MyHandSlotPicker', () => ({
  MyHandSlotPicker: ({ isOpen }: { isOpen: boolean }) =>
    isOpen ? <div data-testid="slot-picker">Picker</div> : null,
}));

describe('MyHandSidebar', () => {
  beforeEach(() => {
    useMyHandStore.setState(useMyHandStore.getInitialState());
  });

  it('renders 4 slots', () => {
    render(<MyHandSidebar />);
    expect(screen.getAllByRole('button', { name: /seleziona/i })).toHaveLength(4);
  });

  it('toggles collapsed state', () => {
    render(<MyHandSidebar />);
    const toggleBtn = screen.getByRole('button', { name: /comprimi|espandi/i });
    fireEvent.click(toggleBtn);
    expect(useMyHandStore.getState().isSidebarCollapsed).toBe(true);
  });

  it('opens picker when slot assign is clicked', () => {
    render(<MyHandSidebar />);
    fireEvent.click(screen.getAllByRole('button', { name: /seleziona/i })[0]);
    expect(screen.getByTestId('slot-picker')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Eseguire per verificare che fallisca**

```bash
cd apps/web
pnpm test src/components/layout/MyHand/__tests__/MyHandSidebar.test.tsx
```

Expected: FAIL — `Cannot find module '../MyHandSidebar'`

- [ ] **Step 3: Implementare MyHandSidebar**

```tsx
// apps/web/src/components/layout/MyHand/MyHandSidebar.tsx
'use client';

import { useState } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { useMyHandStore, selectIsSidebarCollapsed } from '@/stores/my-hand/store';
import { MyHandSlot } from './MyHandSlot';
import { MyHandSlotPicker } from './MyHandSlotPicker';
import { cn } from '@/lib/utils';
import type { MyHandSlotType } from '@/stores/my-hand/types';

const SLOT_TYPES: MyHandSlotType[] = ['toolkit', 'game', 'session', 'ai'];

export function MyHandSidebar() {
  const slots = useMyHandStore(s => s.slots);
  const isCollapsed = useMyHandStore(selectIsSidebarCollapsed);
  const toggleCollapsed = useMyHandStore(s => s.toggleSidebarCollapsed);
  const assignSlot = useMyHandStore(s => s.assignSlot);
  const clearSlot = useMyHandStore(s => s.clearSlot);

  const [pickerOpen, setPickerOpen] = useState(false);
  const [activePickerSlot, setActivePickerSlot] = useState<MyHandSlotType | null>(null);

  const handleAssign = (slotType: MyHandSlotType) => {
    setActivePickerSlot(slotType);
    setPickerOpen(true);
  };

  const handleClear = (slotType: MyHandSlotType) => {
    clearSlot(slotType);
    // TODO Task 12: also call API clearHandSlot
  };

  return (
    <aside
      className={cn(
        'sticky top-16 flex h-[calc(100vh-64px)] flex-col border-l border-border bg-background transition-all duration-300',
        isCollapsed ? 'w-14' : 'w-[280px]'
      )}
    >
      {/* Header */}
      <div className="flex items-center justify-between border-b border-border px-3 py-2">
        {!isCollapsed && (
          <span className="text-sm font-semibold text-foreground">La Mia Mano</span>
        )}
        <button
          aria-label={isCollapsed ? 'Espandi La Mia Mano' : 'Comprimi La Mia Mano'}
          onClick={toggleCollapsed}
          className="rounded p-1 hover:bg-muted"
        >
          {isCollapsed ? <ChevronLeft className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
        </button>
      </div>

      {/* Slots */}
      <div className="flex flex-col gap-2 p-2">
        {SLOT_TYPES.map(slotType => (
          <MyHandSlot
            key={slotType}
            slotType={slotType}
            slot={slots[slotType]}
            onAssign={handleAssign}
            onClear={handleClear}
            compact={isCollapsed}
          />
        ))}
      </div>

      {/* Slot Picker */}
      <MyHandSlotPicker
        isOpen={pickerOpen}
        slotType={activePickerSlot}
        onClose={() => setPickerOpen(false)}
        onConfirm={(slotType, payload) => {
          assignSlot(slotType, payload);
          setPickerOpen(false);
          // TODO Task 12: also call API updateHandSlot
        }}
      />
    </aside>
  );
}
```

- [ ] **Step 4: Creare MyHandSlotPicker (stub per ora)**

```tsx
// apps/web/src/components/layout/MyHand/MyHandSlotPicker.tsx
'use client';

import type { MyHandSlotType, AssignSlotPayload } from '@/stores/my-hand/types';

interface MyHandSlotPickerProps {
  isOpen: boolean;
  slotType: MyHandSlotType | null;
  onClose: () => void;
  onConfirm: (slotType: MyHandSlotType, payload: AssignSlotPayload) => void;
}

export function MyHandSlotPicker({ isOpen }: MyHandSlotPickerProps) {
  if (!isOpen) return null;
  // TODO Task 13: implement full picker UI
  return <div data-testid="slot-picker">Picker (TODO)</div>;
}
```

- [ ] **Step 5: Eseguire i test**

```bash
cd apps/web
pnpm test src/components/layout/MyHand/__tests__/MyHandSidebar.test.tsx
```

Expected: All PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/layout/MyHand/MyHandSidebar.tsx
git add apps/web/src/components/layout/MyHand/MyHandSlotPicker.tsx
git add apps/web/src/components/layout/MyHand/__tests__/MyHandSidebar.test.tsx
git commit -m "feat(my-hand): add MyHandSidebar with collapse and slot assignment"
```

---

## Task 12: Componente MyHandBottomBar (Mobile) + index.ts

**Files:**
- Create: `apps/web/src/components/layout/MyHand/MyHandBottomBar.tsx`
- Create: `apps/web/src/components/layout/MyHand/index.ts`

- [ ] **Step 1: Creare MyHandBottomBar**

```tsx
// apps/web/src/components/layout/MyHand/MyHandBottomBar.tsx
'use client';

import { useState } from 'react';
import { ChevronUp, ChevronDown } from 'lucide-react';
import { useMyHandStore } from '@/stores/my-hand/store';
import { MyHandSlot } from './MyHandSlot';
import { MyHandSlotPicker } from './MyHandSlotPicker';
import { cn } from '@/lib/utils';
import type { MyHandSlotType, AssignSlotPayload } from '@/stores/my-hand/types';

const SLOT_TYPES: MyHandSlotType[] = ['toolkit', 'game', 'session', 'ai'];

export function MyHandBottomBar() {
  const slots = useMyHandStore(s => s.slots);
  const isMobileExpanded = useMyHandStore(s => s.isMobileExpanded);
  const toggleMobileExpanded = useMyHandStore(s => s.toggleMobileExpanded);
  const assignSlot = useMyHandStore(s => s.assignSlot);
  const clearSlot = useMyHandStore(s => s.clearSlot);

  const [pickerOpen, setPickerOpen] = useState(false);
  const [activePickerSlot, setActivePickerSlot] = useState<MyHandSlotType | null>(null);

  const handleAssign = (slotType: MyHandSlotType) => {
    setActivePickerSlot(slotType);
    setPickerOpen(true);
  };

  return (
    <div
      className={cn(
        'fixed bottom-0 left-0 right-0 z-30 border-t border-border bg-background/95 backdrop-blur-md',
        'pb-[env(safe-area-inset-bottom)]'
      )}
    >
      {/* Expand toggle */}
      <button
        aria-label={isMobileExpanded ? 'Comprimi La Mia Mano' : 'Espandi La Mia Mano'}
        onClick={toggleMobileExpanded}
        className="absolute -top-7 right-4 flex h-6 w-12 items-center justify-center rounded-t-full border border-b-0 border-border bg-background"
      >
        {isMobileExpanded
          ? <ChevronDown className="h-3 w-3" />
          : <ChevronUp className="h-3 w-3" />
        }
      </button>

      {/* Collapsed: 4 slot icons in a row */}
      <div className="flex items-center justify-around px-4 py-2">
        {SLOT_TYPES.map(slotType => (
          <MyHandSlot
            key={slotType}
            slotType={slotType}
            slot={slots[slotType]}
            onAssign={handleAssign}
            onClear={(st) => clearSlot(st)}
            compact
          />
        ))}
      </div>

      {/* Expanded: full slots sheet */}
      {isMobileExpanded && (
        <div
          className="grid grid-cols-2 gap-2 px-4 pb-4 pt-1"
          style={{ maxHeight: '40vh', overflowY: 'auto' }}
        >
          {SLOT_TYPES.map(slotType => (
            <MyHandSlot
              key={slotType}
              slotType={slotType}
              slot={slots[slotType]}
              onAssign={handleAssign}
              onClear={(st) => clearSlot(st)}
            />
          ))}
        </div>
      )}

      <MyHandSlotPicker
        isOpen={pickerOpen}
        slotType={activePickerSlot}
        onClose={() => setPickerOpen(false)}
        onConfirm={(slotType, payload) => {
          assignSlot(slotType, payload);
          setPickerOpen(false);
        }}
      />
    </div>
  );
}
```

- [ ] **Step 2: Creare index.ts**

```typescript
// apps/web/src/components/layout/MyHand/index.ts
export { MyHandSidebar } from './MyHandSidebar';
export { MyHandBottomBar } from './MyHandBottomBar';
export { MyHandSlot } from './MyHandSlot';
export { MyHandSlotPicker } from './MyHandSlotPicker';
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/layout/MyHand/MyHandBottomBar.tsx
git add apps/web/src/components/layout/MyHand/index.ts
git commit -m "feat(my-hand): add MyHandBottomBar for mobile + index exports"
```

---

## Task 13: Integrazione nel Layout

**Files:**
- Read e Modify: `apps/web/src/components/layout/UserShell/DesktopShell.tsx`
- Identificare e modificare il layout mobile

- [ ] **Step 1: Leggere DesktopShell per capire la struttura corrente**

Aprire e leggere `apps/web/src/components/layout/UserShell/DesktopShell.tsx` per trovare dove inserire `<MyHandSidebar />`.

Il componente deve essere aggiunto come pannello destro all'interno del flex container principale, dopo il `<main>`.

Pattern atteso (cercare il `<main>` o il contenitore flex principale):
```tsx
<div className="flex flex-1 overflow-hidden">
  <DesktopHandRail />        {/* sinistra — esistente */}
  <main className="flex-1">  {/* contenuto — esistente */}
    {children}
  </main>
  <MyHandSidebar />           {/* DESTRA — aggiungere qui */}
</div>
```

- [ ] **Step 2: Aggiungere l'import in DesktopShell**

Nel file `DesktopShell.tsx`, aggiungere in cima agli import:
```tsx
import { MyHandSidebar } from '@/components/layout/MyHand';
```

- [ ] **Step 3: Aggiungere `<MyHandSidebar />` nel JSX**

Aggiungere `<MyHandSidebar />` dopo il tag `<main>` e prima della chiusura del flex wrapper. La posizione esatta dipende dalla struttura del file — leggerlo prima di modificarlo.

- [ ] **Step 4: Aggiungere MyHandBottomBar per mobile**

Cercare il componente wrapper del layout mobile (probabilmente in `apps/web/src/components/layout/UserShell/` o nel root layout). Aggiungere:
```tsx
import { MyHandBottomBar } from '@/components/layout/MyHand';
// Aggiungere nel JSX — visibile solo su mobile (md:hidden)
<div className="md:hidden">
  <MyHandBottomBar />
</div>
```

- [ ] **Step 5: Build di verifica**

```bash
cd apps/web
pnpm build 2>&1 | tail -20
```

Expected: Build completed successfully.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/layout/UserShell/DesktopShell.tsx
git commit -m "feat(my-hand): integrate MyHandSidebar into DesktopShell layout"
```

---

## Task 14: Hydration dal server al mount

**Files:**
- Create: `apps/web/src/components/layout/MyHand/MyHandProvider.tsx`
- Modify: `apps/web/src/components/layout/MyHand/MyHandSidebar.tsx`

- [ ] **Step 1: Creare MyHandProvider**

Il provider carica i dati dal server al mount e li inietta nello store.

```tsx
// apps/web/src/components/layout/MyHand/MyHandProvider.tsx
'use client';

import { useEffect } from 'react';
import { useMyHandStore } from '@/stores/my-hand/store';
import { getMyHand } from '@/lib/api/my-hand';

/**
 * Mounted una volta nel layout autenticato.
 * Carica gli slot dal server e li inietta nello store.
 */
export function MyHandProvider({ children }: { children: React.ReactNode }) {
  const hydrateFromServer = useMyHandStore(s => s.hydrateFromServer);
  const setLoading = useMyHandStore(s => s.setLoading);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      try {
        const slots = await getMyHand();
        if (!cancelled) hydrateFromServer(slots);
      } catch {
        // Silently fail — store remains empty (default state)
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    load();
    return () => { cancelled = true; };
  }, [hydrateFromServer, setLoading]);

  return <>{children}</>;
}
```

- [ ] **Step 2: Esportare da index.ts**

In `apps/web/src/components/layout/MyHand/index.ts`, aggiungere:
```typescript
export { MyHandProvider } from './MyHandProvider';
```

- [ ] **Step 3: Aggiornare updateHandSlot nel MyHandSidebar con API call**

In `MyHandSidebar.tsx`, aggiornare `handleAssign` e `handleClear` per chiamare l'API in background (optimistic update — lo store è già aggiornato):

```tsx
// In MyHandSidebar.tsx, aggiornare l'import:
import { updateHandSlot, clearHandSlot } from '@/lib/api/my-hand';

// handleClear aggiornato:
const handleClear = (slotType: MyHandSlotType) => {
  clearSlot(slotType); // optimistic
  clearHandSlot(slotType).catch(() => {
    // TODO: rollback se necessario — per MVP silently ignore
  });
};
```

E in `MyHandSlotPicker.onConfirm`:
```tsx
onConfirm={(slotType, payload) => {
  assignSlot(slotType, payload); // optimistic
  setPickerOpen(false);
  updateHandSlot(slotType, payload).catch(() => {
    // TODO: rollback — per MVP silently ignore
  });
}}
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/layout/MyHand/MyHandProvider.tsx
git add apps/web/src/components/layout/MyHand/index.ts
git add apps/web/src/components/layout/MyHand/MyHandSidebar.tsx
git commit -m "feat(my-hand): add MyHandProvider for server hydration + optimistic API calls"
```

---

## Task 15: Typecheck, lint e test completi

- [ ] **Step 1: Backend — build e test**

```bash
cd apps/api
dotnet build
dotnet test --filter "BoundedContext=UserLibrary" -v minimal
```

Expected: Build succeeded, test pass.

- [ ] **Step 2: Frontend — typecheck**

```bash
cd apps/web
pnpm typecheck
```

Expected: 0 errori.

- [ ] **Step 3: Frontend — lint**

```bash
cd apps/web
pnpm lint
```

Expected: 0 errori.

- [ ] **Step 4: Frontend — test suite completa per MyHand**

```bash
cd apps/web
pnpm test src/stores/my-hand/ src/lib/api/__tests__/my-hand.api.test.ts src/components/layout/MyHand/__tests__/
```

Expected: All PASS.

- [ ] **Step 5: Commit finale**

```bash
git add -u
git commit -m "chore(my-hand): all tests pass, lint and typecheck clean"
```

---

## Spec Coverage Self-Review

| Requisito spec | Task che lo implementa |
|---|---|
| F-01: 4 slot fissi | Task 1 (entity), Task 8 (store types) |
| F-02: Slot vuoti al primo login | Task 8 (store initial state), Task 3 (GET returns empty slots) |
| F-03: Entity type per slot | Task 4 (validator SlotEntityMap) |
| F-04: Slot vuoto con CTA | Task 10 (MyHandSlot empty state) |
| F-05: Slot popolato con MeepleCard + actions | Task 10 (populated state) — azioni da aggiungere in task futuro |
| F-06: Quick actions senza navigazione | Non incluso in questo piano — richiede Task separato per ogni SlotActions component |
| F-07: Persistenza server | Task 2-6 (backend CRUD), Task 9 (API client), Task 14 (hydration) |
| F-08: Stato degradato | Task 8 (markSlotInvalid), Task 10 (degraded state) |
| F-09: Desktop sidebar | Task 11 (MyHandSidebar), Task 13 (layout integration) |
| F-10: Mobile bottom bar | Task 12 (MyHandBottomBar) |
| F-11: Swipe-up mobile | Non implementato — P1, task futuro |
| F-12/13/14/15: Quick actions | Non implementati — P0 ma richiedono piano separato per ogni SlotActions |
| F-16: Picker entità | Task 11/12 (stub) — implementazione completa task futuro |
| F-17: Long-press su MeepleCard | P1 — task futuro |
| F-18: Offline badge | Non implementato — P1, task futuro |

**Scope di questo piano:** Backend CRUD completo + store + layout integrazione + slot componente con 3 stati. Le **quick actions specifiche per slot** (F-12..F-15) e il **picker completo** (F-16) sono esclusi e richiedono un piano separato.

---

*Piano generato con superpowers:writing-plans — 2026-04-09*

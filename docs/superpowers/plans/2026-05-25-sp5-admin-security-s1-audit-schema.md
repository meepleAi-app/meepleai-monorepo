# SP5 Admin Security S1 — Audit schema (D-4 + outbox + before/after) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sbloccare il track ⊥ sicurezza partendo dalla base: risolvere la doppia entity `AuditLog` (decisione residua D-4), estendere lo schema con `before_json`/`after_json`/`impersonated_user_id`/`step_up_token_id`, e introdurre il pattern **outbox** per garantire l'audit delle azioni distruttive anche quando la transazione di mutazione fallisce.

**Architecture:** Un `SaveChangesInterceptor` di EF Core cattura `before/after` di entità tracciate prima del commit. Una tabella `audit_outbox` riceve le scritture audit nella **stessa transazione** della mutazione (atomic); un `AuditOutboxProcessor` (BackgroundService) trasferisce i record a `audit_logs` immutabili in batch. La doppia entity `AuditLog` (`Administration` vs `SecurityAudit`) viene disambiguata in un primo spike + decisione documentata; il ramo morto viene marcato deprecated/rimosso.

**Tech Stack:** .NET 9 · ASP.NET Minimal APIs · MediatR · EF Core 9 + Postgres + pgvector · xUnit + Testcontainers · System.Text.Json. Niente nuove dipendenze.

**Spec/panel di riferimento:** `docs/superpowers/specs/2026-05-24-sp5-admin-console-consolidation-design.md` §7 (track ⊥ sicurezza) + nota di rischio "finestra di falsa sicurezza"; `admin-mockups/design_handoff_admin/ADMIN_AUDIT.md` §3 (Audit log, gap di schema). Panel `sblocca il track sicurezza` (2026-05-25): D-4 = primo blocco (Fowler+Wiegers+Hohpe convergono); outbox = Hohpe; before/after via interceptor = Fowler.

**Non-goals di S1 (rimandati a S2/S3 / track ⊥):**
- ❌ Impersonate token short-lived + claim actor — **S2** (questo plan introduce solo la colonna `impersonated_user_id` come *campo* nel record audit; come *valore* sarà popolato in S2).
- ❌ Step-up strict + `step_up_token_id` populated — **S3** (questo plan introduce solo la colonna; il valore arriva in S3).
- ❌ Sistematizzare `[AuditableAction]` su tutti i comandi admin — backlog dopo S1 (lo scope di S1 è infrastruttura).
- ❌ Read endpoints estesi (filtering per before/after, impersonate flag) — i Read attuali continuano a funzionare; nuovi filtri arrivano on-demand.

**Pre-requisito di processo (Step 6 della roadmap panel — non un Task tecnico):**
- ✋ **Three-amigos kickoff** (security + BE + FE + QE) **prima** del Task 0. 30-60min per allineare ownership operativo, contratto del dashboard, e gating dello strict cutover S3.

---

## File Structure

- **Create** `audits/2026-05-25-audit-log-d4-spike.md` — outcome del Task 0 (canonical decision, code paths analysis, deprecation plan).
- **Modify** `apps/api/src/Api/Infrastructure/Entities/Administration/AuditLogEntity.cs` — props `BeforeJson`, `AfterJson`, `ImpersonatedUserId`, `StepUpTokenId`.
- **Modify** `apps/api/src/Api/Infrastructure/EntityConfigurations/Administration/AuditLogEntityConfiguration.cs` — colonne + indici.
- **Create** `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_ExtendAuditLogSchema.cs` — `ALTER TABLE` + nuova tabella `audit_outbox`.
- **Create** `apps/api/src/Api/Infrastructure/Entities/Administration/AuditOutboxEntity.cs` — entity outbox (Pending/Sent + payload JSON + retry count).
- **Create** `apps/api/src/Api/Infrastructure/Persistence/AuditingSaveChangesInterceptor.cs` — cattura `OriginalValues`/`CurrentValues` su `SaveChangesAsync` per entità marcate `[Auditable]`.
- **Modify** `apps/api/src/Api/BoundedContexts/Administration/Application/Behaviors/AuditLoggingBehavior.cs` — usa l'outbox in-tx invece della scrittura diretta a `audit_logs`.
- **Create** `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/AuditOutboxProcessor.cs` — `BackgroundService` con polling (5s) → batch transfer → mark Sent.
- **Delete/Deprecate** (post-spike): il ramo morto `SecurityAudit/Infrastructure/Entities/AuditLogEntity.cs` + relative config (se Task 0 conferma).
- **Tests** in `tests/Api.Tests/Unit/Infrastructure/Persistence/AuditingSaveChangesInterceptorTests.cs` + `tests/Api.Tests/Integration/Administration/AuditOutboxProcessorIntegrationTests.cs` (Testcontainers Postgres).

---

## Task 0: D-4 Spike — disambiguazione doppia entity `AuditLog`

**Files:**
- Create: `audits/2026-05-25-audit-log-d4-spike.md`

> **Tipo Task:** spike read-only. Nessun codice di prodotto, ma è il **gate** per i Task 1-4: se il ramo morto è davvero morto, viene marcato per rimozione; se invece è in uso da componenti non-`Administration`, la strategia cambia (mantenere entrambi, sincronizzare, o consolidare con migrazione dati).

- [ ] **Step 1: Cataloga le scritture su ciascuna entity**

  ```bash
  cd D:/Repositories/meepleai-monorepo-dev
  rg -l 'AuditLogEntity|AuditLogRepository|class AuditLog\b' apps/api/src/Api -t cs
  rg -n 'context\.AuditLogs\.Add|AuditLogs\.AddAsync|new AuditLog\(' apps/api/src/Api -t cs
  ```

  Expected: una lista di ~10-30 file. Catalogare: per ogni hit, quale `AuditLog` (Administration vs SecurityAudit), e qual è il chiamante (handler / repo / behavior).

- [ ] **Step 2: Cataloga le letture (read endpoints + analytics + esports)**

  ```bash
  rg -n 'context\.AuditLogs\.|AuditLogs\.Where|AuditLogs\.AsNoTracking' apps/api/src/Api -t cs
  rg -n 'audit_logs|audit-logs' apps/api/src/Api -t cs
  ```

  Expected: catalogo dei consumer (es. `AdminAuditLogEndpoints`, dashboard analytics, eventuali export CSV).

- [ ] **Step 3: Verifica le migration EF storiche**

  ```bash
  ls apps/api/src/Api/Infrastructure/Migrations/ | grep -i audit
  ```

  Expected: una sola migration "InitialCreate"-style su `audit_logs` (Administration). Se ne esistono due (una per ognuna), c'è una vera doppia tabella in produzione. Se ne esiste una sola, una delle due entity non è mappata → ramo morto in C#.

- [ ] **Step 4: Scrivi il report**

  Crea `audits/2026-05-25-audit-log-d4-spike.md` con la struttura:

  ```markdown
  # D-4 Spike — AuditLog canonical entity decision

  **Date:** 2026-05-25
  **Status:** decision
  **Scope:** disambiguare `Administration/AuditLogEntity` vs `SecurityAudit/AuditLogEntity` (spec §10 D-4)

  ## Findings

  ### Administration/AuditLogEntity
  - Writes: [list — file:line]
  - Reads: [list]
  - Migration: [name]
  - Active consumers: [yes/no]

  ### SecurityAudit/AuditLogEntity
  - Writes: [list]
  - Reads: [list]
  - Migration: [name or "NOT MAPPED"]
  - Active consumers: [yes/no]

  ## Decision

  **Canonical:** `<Administration | SecurityAudit | both-merge>`

  **Rationale:** [...]

  **Deprecation plan:**
  - Delete: [files]
  - Migrate data: [yes/no — if yes, migration script in Task 1]
  - Communication: [team-channel notice if BC contract changes]
  ```

- [ ] **Step 5: Commit (spike-only, no code changes)**

  ```bash
  git add audits/2026-05-25-audit-log-d4-spike.md
  git commit -m "spike(audit): D-4 disambiguate canonical AuditLog entity"
  ```

  > Se la decision è "merge entrambe", **stop**: lo scope di S1 cambia significativamente e va riconcordato col team (è una migrazione dati, non un'estensione schema). Apri una issue + ferma S1 Task 1.

---

## Task 1: Migration — estendi `audit_logs` + crea `audit_outbox`

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/Administration/AuditLogEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/Administration/AuditLogEntityConfiguration.cs`
- Create: `apps/api/src/Api/Infrastructure/Entities/Administration/AuditOutboxEntity.cs`
- Create: `apps/api/src/Api/Infrastructure/EntityConfigurations/Administration/AuditOutboxEntityConfiguration.cs`
- Create: `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_ExtendAuditLogSchema.cs` (autogenerato da EF)

- [ ] **Step 1: Estendi `AuditLogEntity` con i 4 nuovi campi**

  Apri `apps/api/src/Api/Infrastructure/Entities/Administration/AuditLogEntity.cs` e aggiungi le 4 property (private setter, factory updated):

  ```csharp
  public class AuditLogEntity
  {
      // ... esistenti: Id, UserId, Action, Resource, ResourceId, IpAddress, UserAgent, CreatedAt, Details ...

      /// <summary>Snapshot JSON of the entity state BEFORE the mutation. Null for create operations.</summary>
      public string? BeforeJson { get; private set; }

      /// <summary>Snapshot JSON of the entity state AFTER the mutation. Null for delete operations.</summary>
      public string? AfterJson { get; private set; }

      /// <summary>When set, the actor user was impersonating this user at the time of the mutation. Populated by S2.</summary>
      public Guid? ImpersonatedUserId { get; private set; }

      /// <summary>When set, the mutation was gated by a step-up token. Populated by S3.</summary>
      public Guid? StepUpTokenId { get; private set; }

      // Update the factory / constructor to accept these (optional, default null).
  }
  ```

- [ ] **Step 2: Estendi `AuditLogEntityConfiguration` con le colonne + indici**

  Apri `AuditLogEntityConfiguration.cs` e aggiungi:

  ```csharp
  builder.Property(e => e.BeforeJson)
      .HasColumnName("before_json")
      .HasColumnType("jsonb"); // Postgres jsonb — searchable, compact

  builder.Property(e => e.AfterJson)
      .HasColumnName("after_json")
      .HasColumnType("jsonb");

  builder.Property(e => e.ImpersonatedUserId)
      .HasColumnName("impersonated_user_id");

  builder.Property(e => e.StepUpTokenId)
      .HasColumnName("step_up_token_id");

  builder.HasIndex(e => e.ImpersonatedUserId)
      .HasDatabaseName("ix_audit_logs_impersonated_user_id")
      .HasFilter(@"""impersonated_user_id"" IS NOT NULL");
  ```

- [ ] **Step 3: Crea `AuditOutboxEntity`**

  ```csharp
  // apps/api/src/Api/Infrastructure/Entities/Administration/AuditOutboxEntity.cs
  using System;

  namespace Api.Infrastructure.Entities.Administration;

  public class AuditOutboxEntity
  {
      public Guid Id { get; private set; }
      public string PayloadJson { get; private set; } = string.Empty;
      public OutboxStatus Status { get; private set; }
      public int RetryCount { get; private set; }
      public string? LastError { get; private set; }
      public DateTimeOffset CreatedAt { get; private set; }
      public DateTimeOffset? ProcessedAt { get; private set; }

      private AuditOutboxEntity() { }

      public static AuditOutboxEntity CreatePending(string payloadJson, DateTimeOffset now)
          => new()
          {
              Id = Guid.NewGuid(),
              PayloadJson = payloadJson,
              Status = OutboxStatus.Pending,
              RetryCount = 0,
              CreatedAt = now,
          };

      public void MarkSent(DateTimeOffset now)
      {
          Status = OutboxStatus.Sent;
          ProcessedAt = now;
      }

      public void MarkFailed(string error, DateTimeOffset now)
      {
          Status = OutboxStatus.Failed;
          RetryCount++;
          LastError = error;
          ProcessedAt = now;
      }
  }

  public enum OutboxStatus
  {
      Pending = 0,
      Sent = 1,
      Failed = 2,
  }
  ```

- [ ] **Step 4: Crea la configuration per `AuditOutboxEntity`**

  ```csharp
  // apps/api/src/Api/Infrastructure/EntityConfigurations/Administration/AuditOutboxEntityConfiguration.cs
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Metadata.Builders;
  using Api.Infrastructure.Entities.Administration;

  namespace Api.Infrastructure.EntityConfigurations.Administration;

  public class AuditOutboxEntityConfiguration : IEntityTypeConfiguration<AuditOutboxEntity>
  {
      public void Configure(EntityTypeBuilder<AuditOutboxEntity> builder)
      {
          builder.ToTable("audit_outbox");
          builder.HasKey(e => e.Id);
          builder.Property(e => e.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
          builder.Property(e => e.Status).HasColumnName("status").IsRequired();
          builder.Property(e => e.RetryCount).HasColumnName("retry_count").IsRequired();
          builder.Property(e => e.LastError).HasColumnName("last_error").HasMaxLength(2048);
          builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
          builder.Property(e => e.ProcessedAt).HasColumnName("processed_at");

          // Hot index: processor scans Pending oldest-first
          builder.HasIndex(e => new { e.Status, e.CreatedAt })
                 .HasDatabaseName("ix_audit_outbox_status_created_at");
      }
  }
  ```

- [ ] **Step 5: Registra `AuditOutboxEntity` nel `DbContext`**

  In `apps/api/src/Api/Infrastructure/Persistence/MeepleDbContext.cs` (o nome equivalente), aggiungi:

  ```csharp
  public DbSet<AuditOutboxEntity> AuditOutbox => Set<AuditOutboxEntity>();
  ```

  E nell'`OnModelCreating` la configuration verrà caricata via `ApplyConfigurationsFromAssembly` (verifica che lo schema sia già auto-discoverato).

- [ ] **Step 6: Genera la migration EF**

  ```bash
  cd apps/api/src/Api
  dotnet ef migrations add ExtendAuditLogSchema
  ```

  Expected: file `Migrations/<timestamp>_ExtendAuditLogSchema.cs` creato. Apri e verifica:
  - `AddColumn` di 4 colonne su `audit_logs`
  - `CreateTable` di `audit_outbox`
  - Indici creati

  Se la migration ha output strano (es. ricrea tabelle), STOP e analizza: il DbContext probabilmente ha drift con DB → rinvia.

- [ ] **Step 7: Test integrazione — la migration applica pulita**

  ```bash
  cd apps/api/src/Api
  dotnet test --filter "FullyQualifiedName~Infrastructure.Migrations" 2>&1 | tail -20
  ```

  Se non esiste un test di smoke delle migration, crea `tests/Api.Tests/Integration/Infrastructure/AuditSchemaMigrationTests.cs` con Testcontainers Postgres che applica TUTTE le migration in sequenza e asserisce che `audit_logs` ha le 4 colonne nuove + `audit_outbox` esiste.

- [ ] **Step 8: Commit**

  ```bash
  git add apps/api/src/Api/Infrastructure/Entities/Administration/AuditLogEntity.cs \
          apps/api/src/Api/Infrastructure/Entities/Administration/AuditOutboxEntity.cs \
          apps/api/src/Api/Infrastructure/EntityConfigurations/Administration/AuditLogEntityConfiguration.cs \
          apps/api/src/Api/Infrastructure/EntityConfigurations/Administration/AuditOutboxEntityConfiguration.cs \
          apps/api/src/Api/Infrastructure/Migrations/
  git commit -m "feat(audit): extend audit_logs schema + add audit_outbox table"
  ```

---

## Task 2: `AuditingSaveChangesInterceptor` — cattura before/after

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Persistence/AuditingSaveChangesInterceptor.cs`
- Create: `tests/Api.Tests/Unit/Infrastructure/Persistence/AuditingSaveChangesInterceptorTests.cs`

- [ ] **Step 1: Definisci l'attributo `[Auditable]` per le entity da catturare**

  ```csharp
  // apps/api/src/Api/Infrastructure/Persistence/AuditableAttribute.cs
  using System;
  namespace Api.Infrastructure.Persistence;
  [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
  public sealed class AuditableAttribute : Attribute { }
  ```

  Applica `[Auditable]` su `UserEntity` (admin user mutations) come primo banco di prova:

  ```csharp
  [Auditable]
  public class UserEntity { /* ... */ }
  ```

- [ ] **Step 2: Scrivi il test unitario del interceptor**

  ```csharp
  // tests/Api.Tests/Unit/Infrastructure/Persistence/AuditingSaveChangesInterceptorTests.cs
  using System;
  using System.Linq;
  using System.Threading.Tasks;
  using Api.Infrastructure.Persistence;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Diagnostics;
  using Xunit;

  public class AuditingSaveChangesInterceptorTests
  {
      [Fact]
      public async Task CapturesBeforeAndAfter_OnUpdate_OfAuditableEntity()
      {
          // Arrange: in-memory DbContext with the interceptor + a [Auditable] test entity
          var capture = new TestAuditCapture();
          using var ctx = TestContextFactory.Create(capture);
          var user = new TestAuditableUser { Id = Guid.NewGuid(), Email = "old@x" };
          ctx.Users.Add(user);
          await ctx.SaveChangesAsync();
          capture.Snapshots.Clear();

          // Act
          user.Email = "new@x";
          await ctx.SaveChangesAsync();

          // Assert: one snapshot captured, with before+after JSON
          Assert.Single(capture.Snapshots);
          var snap = capture.Snapshots[0];
          Assert.Equal(nameof(TestAuditableUser), snap.EntityType);
          Assert.Contains("old@x", snap.BeforeJson);
          Assert.Contains("new@x", snap.AfterJson);
      }

      [Fact]
      public async Task SkipsCapture_ForNonAuditableEntities()
      {
          var capture = new TestAuditCapture();
          using var ctx = TestContextFactory.Create(capture);
          ctx.PlainItems.Add(new TestPlainItem { Id = Guid.NewGuid(), Value = "x" });
          await ctx.SaveChangesAsync();
          Assert.Empty(capture.Snapshots);
      }
  }
  ```

  (`TestAuditCapture`, `TestContextFactory`, `TestAuditableUser`, `TestPlainItem` sono helper di test nello stesso file — creali come `internal` classes nel test file. `TestAuditableUser` ha `[Auditable]`, `TestPlainItem` no.)

- [ ] **Step 3: Run, confirm FAIL** (interceptor non esiste)

  ```bash
  cd apps/api/src/Api
  dotnet test --filter "FullyQualifiedName~AuditingSaveChangesInterceptorTests" 2>&1 | tail -10
  ```

- [ ] **Step 4: Implementa l'interceptor**

  ```csharp
  // apps/api/src/Api/Infrastructure/Persistence/AuditingSaveChangesInterceptor.cs
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Reflection;
  using System.Text.Json;
  using System.Threading;
  using System.Threading.Tasks;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.ChangeTracking;
  using Microsoft.EntityFrameworkCore.Diagnostics;

  namespace Api.Infrastructure.Persistence;

  public record AuditSnapshot(
      string EntityType,
      string PrimaryKey,
      string? BeforeJson,
      string? AfterJson,
      AuditOperation Operation);

  public enum AuditOperation { Insert, Update, Delete }

  public interface IAuditSnapshotSink
  {
      void Record(AuditSnapshot snapshot);
  }

  /// <summary>
  /// Captures before/after snapshots of [Auditable] entities BEFORE SaveChanges commits.
  /// Snapshots are forwarded to <see cref="IAuditSnapshotSink"/> — the AuditLoggingBehavior
  /// reads them when a [AuditableAction] command is in flight and writes to audit_outbox.
  /// </summary>
  public class AuditingSaveChangesInterceptor : SaveChangesInterceptor
  {
      private readonly IAuditSnapshotSink _sink;
      private static readonly JsonSerializerOptions JsonOpts = new()
      {
          WriteIndented = false,
          PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
      };

      public AuditingSaveChangesInterceptor(IAuditSnapshotSink sink) => _sink = sink;

      public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
          DbContextEventData eventData,
          InterceptionResult<int> result,
          CancellationToken cancellationToken = default)
      {
          var ctx = eventData.Context;
          if (ctx is null) return base.SavingChangesAsync(eventData, result, cancellationToken);

          foreach (var entry in ctx.ChangeTracker.Entries())
          {
              if (entry.State == EntityState.Unchanged || entry.State == EntityState.Detached) continue;
              var clrType = entry.Entity.GetType();
              if (clrType.GetCustomAttribute<AuditableAttribute>() is null) continue;

              var pk = entry.Metadata.FindPrimaryKey()?.Properties
                  .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "")
                  .Aggregate((a, b) => $"{a}|{b}") ?? "";

              string? before = null;
              string? after = null;
              AuditOperation op;

              switch (entry.State)
              {
                  case EntityState.Added:
                      after = SerializeProperties(entry, useOriginal: false);
                      op = AuditOperation.Insert;
                      break;
                  case EntityState.Modified:
                      before = SerializeProperties(entry, useOriginal: true);
                      after = SerializeProperties(entry, useOriginal: false);
                      op = AuditOperation.Update;
                      break;
                  case EntityState.Deleted:
                      before = SerializeProperties(entry, useOriginal: true);
                      op = AuditOperation.Delete;
                      break;
                  default:
                      continue;
              }

              _sink.Record(new AuditSnapshot(clrType.Name, pk, before, after, op));
          }

          return base.SavingChangesAsync(eventData, result, cancellationToken);
      }

      private static string SerializeProperties(EntityEntry entry, bool useOriginal)
      {
          var dict = new Dictionary<string, object?>();
          foreach (var p in entry.Properties)
          {
              dict[p.Metadata.Name] = useOriginal ? p.OriginalValue : p.CurrentValue;
          }
          return JsonSerializer.Serialize(dict, JsonOpts);
      }
  }
  ```

- [ ] **Step 5: Crea `TestAuditCapture` (in-memory sink) nello stesso test file e `TestContextFactory`**

  Sotto la classe di test, aggiungi:

  ```csharp
  internal sealed class TestAuditCapture : IAuditSnapshotSink
  {
      public List<AuditSnapshot> Snapshots { get; } = new();
      public void Record(AuditSnapshot s) => Snapshots.Add(s);
  }

  [Auditable]
  internal sealed class TestAuditableUser
  {
      public Guid Id { get; set; }
      public string Email { get; set; } = "";
  }

  internal sealed class TestPlainItem
  {
      public Guid Id { get; set; }
      public string Value { get; set; } = "";
  }

  internal sealed class TestAuditContext : DbContext
  {
      public DbSet<TestAuditableUser> Users => Set<TestAuditableUser>();
      public DbSet<TestPlainItem> PlainItems => Set<TestPlainItem>();
      public TestAuditContext(DbContextOptions<TestAuditContext> opts) : base(opts) { }
  }

  internal static class TestContextFactory
  {
      public static TestAuditContext Create(IAuditSnapshotSink sink)
      {
          var opts = new DbContextOptionsBuilder<TestAuditContext>()
              .UseInMemoryDatabase(Guid.NewGuid().ToString())
              .AddInterceptors(new AuditingSaveChangesInterceptor(sink))
              .Options;
          return new TestAuditContext(opts);
      }
  }
  ```

- [ ] **Step 6: Run, confirm PASS** (2 test)

  ```bash
  dotnet test --filter "FullyQualifiedName~AuditingSaveChangesInterceptorTests" 2>&1 | tail -8
  ```

- [ ] **Step 7: Commit**

  ```bash
  git add apps/api/src/Api/Infrastructure/Persistence/AuditingSaveChangesInterceptor.cs \
          apps/api/src/Api/Infrastructure/Persistence/AuditableAttribute.cs \
          tests/Api.Tests/Unit/Infrastructure/Persistence/AuditingSaveChangesInterceptorTests.cs
  git commit -m "feat(audit): SaveChangesInterceptor capturing before/after of [Auditable] entities"
  ```

---

## Task 3: Outbox writer — `AuditLoggingBehavior` scrive in `audit_outbox`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Application/Behaviors/AuditLoggingBehavior.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Behaviors/ScopedAuditSnapshotSink.cs` (sink scoped per request che alimenta il behavior)
- Test: integration test in `tests/Api.Tests/Integration/Administration/AuditOutboxWriteIntegrationTests.cs`

- [ ] **Step 1: Crea `ScopedAuditSnapshotSink`** (DI scoped per request)

  ```csharp
  // apps/api/src/Api/BoundedContexts/Administration/Application/Behaviors/ScopedAuditSnapshotSink.cs
  using System.Collections.Generic;
  using Api.Infrastructure.Persistence;

  namespace Api.BoundedContexts.Administration.Application.Behaviors;

  /// <summary>
  /// Per-request sink: the interceptor records snapshots here, then
  /// AuditLoggingBehavior drains them when an [AuditableAction] command completes.
  /// </summary>
  public class ScopedAuditSnapshotSink : IAuditSnapshotSink
  {
      private readonly List<AuditSnapshot> _snapshots = new();
      public IReadOnlyList<AuditSnapshot> Snapshots => _snapshots;
      public void Record(AuditSnapshot snapshot) => _snapshots.Add(snapshot);
      public void Clear() => _snapshots.Clear();
  }
  ```

  Registra in DI come `Scoped` (in `Program.cs` o `Extensions/...`):

  ```csharp
  services.AddScoped<ScopedAuditSnapshotSink>();
  services.AddScoped<IAuditSnapshotSink>(sp => sp.GetRequiredService<ScopedAuditSnapshotSink>());
  ```

  E registra l'interceptor sul `DbContext`:

  ```csharp
  services.AddDbContext<MeepleDbContext>((sp, opts) =>
  {
      opts.UseNpgsql(/* ... */);
      opts.AddInterceptors(sp.GetRequiredService<AuditingSaveChangesInterceptor>());
  });
  services.AddScoped<AuditingSaveChangesInterceptor>();
  ```

- [ ] **Step 2: Modifica `AuditLoggingBehavior` per scrivere in `audit_outbox`**

  La modifica chiave: invece di `_dbContext.AuditLogs.Add(auditEntry); await _dbContext.SaveChangesAsync();`, il behavior costruisce **un payload JSON** che include il request, lo snapshot before/after dalla sink, l'IP, l'UserAgent, e lo scrive in `audit_outbox` (Pending). La transazione del comando + l'outbox row sono nello stesso `SaveChanges` chiamato dall'handler → atomicità.

  ```csharp
  // Estratto della modifica chiave dentro Handle()
  var snapshots = _sink.Snapshots.ToList();
  _sink.Clear();

  var payload = new
  {
      action = attr.Action,
      resource = attr.Resource,
      userId = _currentUserService.UserId,
      ipAddress = _httpContext.Request?.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
      userAgent = _httpContext.Request?.Headers["User-Agent"].ToString(),
      requestType = request.GetType().Name,
      // request scrubbed: chiamare un metodo helper Scrub() che rimuove campi marcati [Sensitive]
      requestJson = JsonSerializer.Serialize(request, AuditJsonOpts),
      snapshots = snapshots, // each has BeforeJson/AfterJson/EntityType/PrimaryKey/Operation
      impersonatedUserId = _currentUserService.ImpersonatedUserId, // wired in S2
      stepUpTokenId = _currentUserService.StepUpTokenId,           // wired in S3
      timestamp = DateTimeOffset.UtcNow,
  };

  var outboxRow = AuditOutboxEntity.CreatePending(
      JsonSerializer.Serialize(payload, AuditJsonOpts),
      DateTimeOffset.UtcNow);

  _dbContext.AuditOutbox.Add(outboxRow);
  // NO explicit SaveChanges here — the handler's SaveChanges commits both the mutation
  // and the outbox row atomically.
  ```

  NOTA: il behavior tradizionalmente scrive *dopo* il handler (post-handler hook). Per garantire atomicità, sposta la scrittura outbox PRIMA del SaveChanges del handler. Pattern: `MediatR` `IPipelineBehavior` wraps the handler — il behavior chiama `next()` (esegue handler logic ma NON ancora SaveChanges), poi scrive outbox, poi è il handler stesso che fa SaveChanges. Alternativa più semplice: il behavior chiama `next()` (handler fa già SaveChanges), e l'outbox scrive in una SECONDA transazione → no atomicità. **Scegli pattern atomico**: documenta nel doc-comment del behavior.

  ⚠️ DECISIONE: l'atomicità richiede convenzione "handler NON chiama SaveChanges, il behavior lo fa dopo aver aggiunto la outbox row". Verifica i ~8 handler `[AuditableAction]` esistenti: se chiamano SaveChanges autonomamente, vanno refactorati o si rinuncia all'atomicità (best-effort).

- [ ] **Step 3: Test integrazione (Testcontainers Postgres)**

  ```csharp
  // tests/Api.Tests/Integration/Administration/AuditOutboxWriteIntegrationTests.cs
  [Fact]
  public async Task ExecutingAuditableCommand_WritesPendingRowToAuditOutbox()
  {
      // Arrange: spin up testcontainer postgres, run migrations, register services
      await using var fx = await AdministrationFixture.CreateAsync();
      var sender = fx.GetRequiredService<IMediator>();

      // Act: execute an [AuditableAction] command (e.g. SuspendUserCommand)
      await sender.Send(new SuspendUserCommand(targetUserId: fx.SeededUserId, reason: "test"));

      // Assert: one Pending row in audit_outbox with the expected action
      var ctx = fx.GetRequiredService<MeepleDbContext>();
      var rows = await ctx.AuditOutbox.AsNoTracking().ToListAsync();
      Assert.Single(rows);
      Assert.Equal(OutboxStatus.Pending, rows[0].Status);
      Assert.Contains("\"action\":\"user.suspend\"", rows[0].PayloadJson);
      Assert.Contains("\"snapshots\":", rows[0].PayloadJson); // before/after included
  }
  ```

  Run: `dotnet test --filter "FullyQualifiedName~AuditOutboxWriteIntegrationTests" 2>&1 | tail -12`
  Expected: PASS.

- [ ] **Step 4: Commit**

  ```bash
  git add apps/api/src/Api/BoundedContexts/Administration/Application/Behaviors/ \
          tests/Api.Tests/Integration/Administration/AuditOutboxWriteIntegrationTests.cs
  git commit -m "feat(audit): atomic outbox write via AuditLoggingBehavior"
  ```

---

## Task 4: `AuditOutboxProcessor` — drena outbox → `audit_logs`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/AuditOutboxProcessor.cs`
- Create: `tests/Api.Tests/Integration/Administration/AuditOutboxProcessorIntegrationTests.cs`

- [ ] **Step 1: Test integrazione (TDD-first)**

  ```csharp
  [Fact]
  public async Task Processor_DrainsPendingRows_To_AuditLogs_AndMarksSent()
  {
      // Arrange: seed 3 Pending rows in audit_outbox, no audit_logs rows
      await using var fx = await AdministrationFixture.CreateAsync();
      var ctx = fx.GetRequiredService<MeepleDbContext>();
      for (var i = 0; i < 3; i++)
      {
          ctx.AuditOutbox.Add(AuditOutboxEntity.CreatePending(
              $"{{\"action\":\"test.{i}\",\"snapshots\":[]}}",
              DateTimeOffset.UtcNow));
      }
      await ctx.SaveChangesAsync();

      // Act: invoke processor.RunOnceAsync() (extracted from BackgroundService for testability)
      var processor = fx.GetRequiredService<AuditOutboxProcessor>();
      var processed = await processor.RunOnceAsync(batchSize: 100, ct: default);

      // Assert: 3 rows in audit_logs, 3 rows in outbox marked Sent
      Assert.Equal(3, processed);
      Assert.Equal(3, await ctx.AuditLogs.CountAsync());
      Assert.Equal(3, await ctx.AuditOutbox.CountAsync(r => r.Status == OutboxStatus.Sent));
  }
  ```

- [ ] **Step 2: Run, confirm FAIL**

- [ ] **Step 3: Implementa il processor**

  ```csharp
  // apps/api/src/Api/BoundedContexts/Administration/Infrastructure/AuditOutboxProcessor.cs
  using System;
  using System.Linq;
  using System.Text.Json;
  using System.Threading;
  using System.Threading.Tasks;
  using Api.Infrastructure.Entities.Administration;
  using Api.Infrastructure.Persistence;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Hosting;
  using Microsoft.Extensions.Logging;

  namespace Api.BoundedContexts.Administration.Infrastructure;

  public class AuditOutboxProcessor : BackgroundService
  {
      private readonly IServiceScopeFactory _scopeFactory;
      private readonly ILogger<AuditOutboxProcessor> _logger;
      private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);
      private const int BatchSize = 100;

      public AuditOutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<AuditOutboxProcessor> logger)
      {
          _scopeFactory = scopeFactory;
          _logger = logger;
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
          while (!stoppingToken.IsCancellationRequested)
          {
              try
              {
                  await RunOnceAsync(BatchSize, stoppingToken);
              }
              catch (Exception ex)
              {
                  _logger.LogError(ex, "AuditOutboxProcessor batch failed; will retry");
              }
              await Task.Delay(_pollInterval, stoppingToken);
          }
      }

      /// <summary>Process a single batch. Returns count processed. Exposed for testing.</summary>
      public async Task<int> RunOnceAsync(int batchSize, CancellationToken ct)
      {
          using var scope = _scopeFactory.CreateScope();
          var ctx = scope.ServiceProvider.GetRequiredService<MeepleDbContext>();

          var pending = await ctx.AuditOutbox
              .Where(r => r.Status == OutboxStatus.Pending)
              .OrderBy(r => r.CreatedAt)
              .Take(batchSize)
              .ToListAsync(ct);

          if (pending.Count == 0) return 0;

          await using var tx = await ctx.Database.BeginTransactionAsync(ct);
          foreach (var row in pending)
          {
              try
              {
                  var auditLog = MaterializeAuditLogFromPayload(row.PayloadJson);
                  ctx.AuditLogs.Add(auditLog);
                  row.MarkSent(DateTimeOffset.UtcNow);
              }
              catch (Exception ex)
              {
                  row.MarkFailed(ex.Message, DateTimeOffset.UtcNow);
              }
          }
          await ctx.SaveChangesAsync(ct);
          await tx.CommitAsync(ct);
          return pending.Count;
      }

      private static AuditLogEntity MaterializeAuditLogFromPayload(string payloadJson)
      {
          using var doc = JsonDocument.Parse(payloadJson);
          var root = doc.RootElement;
          var snapshotsArr = root.GetProperty("snapshots");
          string? beforeJson = null, afterJson = null;
          if (snapshotsArr.GetArrayLength() > 0)
          {
              var snap = snapshotsArr[0];
              beforeJson = snap.TryGetProperty("BeforeJson", out var b) ? b.GetString() : null;
              afterJson = snap.TryGetProperty("AfterJson", out var a) ? a.GetString() : null;
          }
          return AuditLogEntity.Create(
              userId: GetGuid(root, "userId"),
              action: root.GetProperty("action").GetString() ?? "",
              resource: root.GetProperty("resource").GetString() ?? "",
              ipAddress: GetString(root, "ipAddress"),
              userAgent: GetString(root, "userAgent"),
              beforeJson: beforeJson,
              afterJson: afterJson,
              impersonatedUserId: GetGuidNullable(root, "impersonatedUserId"),
              stepUpTokenId: GetGuidNullable(root, "stepUpTokenId"),
              createdAt: root.GetProperty("timestamp").GetDateTimeOffset());
      }

      private static Guid GetGuid(JsonElement el, string name) =>
          el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String && Guid.TryParse(p.GetString(), out var g) ? g : Guid.Empty;
      private static Guid? GetGuidNullable(JsonElement el, string name) =>
          el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String && Guid.TryParse(p.GetString(), out var g) ? g : null;
      private static string? GetString(JsonElement el, string name) =>
          el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
  }
  ```

  Aggiorna `AuditLogEntity.Create(...)` per accettare i nuovi parametri (vedi Task 1 step 1) — se il factory esistente è diverso, adatta.

  Registra in DI in `Program.cs`:

  ```csharp
  services.AddHostedService<AuditOutboxProcessor>();
  // Anche come singleton per il test (RunOnceAsync esposto):
  services.AddSingleton<AuditOutboxProcessor>();
  ```

- [ ] **Step 4: Run, confirm PASS**

  ```bash
  dotnet test --filter "FullyQualifiedName~AuditOutboxProcessorIntegrationTests" 2>&1 | tail -10
  ```

- [ ] **Step 5: Commit**

  ```bash
  git add apps/api/src/Api/BoundedContexts/Administration/Infrastructure/AuditOutboxProcessor.cs \
          tests/Api.Tests/Integration/Administration/AuditOutboxProcessorIntegrationTests.cs
  git commit -m "feat(audit): AuditOutboxProcessor draining outbox to audit_logs"
  ```

---

## Self-Review

**1. Spec coverage:** S1 copre l'intero work-stream "Audit schema" (Step 0 D-4 + Step 1 outbox+before/after+schema) della roadmap panel. Three-amigos (Step 6) è pre-requisito di processo, non implementato nei task. Test harness 2FA (Step 7) appartiene a S3, non a S1.

**2. Placeholder scan:** ogni step ha codice C# concreto + comandi `dotnet`/git esatti. L'unica decisione non chiusa è "atomic vs best-effort" outbox (Task 3 step 2): è esplicitata + flaggata, NON è placeholder.

**3. Type consistency:** `AuditSnapshot` (Task 2) → consumato da `ScopedAuditSnapshotSink` (Task 3) → serializzato in `payloadJson` (Task 3) → deserializzato da `AuditOutboxProcessor` (Task 4). Campi `BeforeJson`/`AfterJson` propagati end-to-end. `OutboxStatus` enum coerente Pending/Sent/Failed. `AuditLogEntity.Create(...)` factory aggiornato per accettare i nuovi 4 parametri.

**Note di rischio:**
- **Atomicità outbox** (Task 3 step 2): se i handler `[AuditableAction]` chiamano `SaveChanges` autonomamente, l'atomicità richiede refactor — è una decisione di scope da prendere al Task 3. Senza atomicità, l'outbox è "best-effort migliore del nulla" ma non garantito per i delete distruttivi (regressione vs intent Nygard).
- **Performance del processor** (Task 4): polling 5s + batch 100 = throughput ~20 rows/s sostenuto. Per il volume admin di MeepleAI è ampiamente sufficiente; se cresce, scalare via `LISTEN/NOTIFY` Postgres (follow-up).
- **JSON column types**: `jsonb` Postgres (vs `text`) per supportare query JSON future (filter by `before_json->>'role'`). Il driver `Npgsql` mapppa `string` ↔ `jsonb` se la column è dichiarata `jsonb` nella migration.

---

## Sblocco di S2 e S3 (intent + dipendenze)

Dopo il merge di S1, i plan **S2** (Impersonate token) e **S3** (Step-up strict) possono partire. Intent compatto per quando li scriviamo:

### S2 — Impersonate token (Step 2 della roadmap)
- **Goal:** estendere `Session` con `ImpersonatedByUserId` + `ImpersonatedUntil`, lifetime override 15min durante impersonate, `SessionStatusDto` con principal duale `subject + actor`, kill-switch superadmin (`GET /admin/impersonation/active` + `POST .../revoke`).
- **Dep da S1:** la colonna `impersonated_user_id` su `audit_logs` esiste; S2 popola il valore nell'`ICurrentUserService.ImpersonatedUserId` consumato dal behavior (Task 3 step 2 di S1).
- **Risk:** race condition al `end-impersonate` (Nygard) — atomicità tx + ripristino attributi originali.
- **Effort stimato:** 2-3g.

### S3 — Step-up strict (Step 3 + 4 + 5 + 7 della roadmap)
- **Goal:** `LastTotpVerifiedAt` su `Session`; endpoint challenge `POST /auth/step-up/challenge` → emette `step_up_token` (stateful, 5min); header `X-StepUp-Token-Version: 1` + validazione; `[RequiresStepUp(Trigger.X)]` attribute + behavior shadow→strict per-trigger feature-flag; trigger coperti (`rotate-key`, `emergency`, `mass-delete>5`, `change-flag-prod`, `promote-superadmin`).
- **Dep da S1:** la colonna `step_up_token_id` su `audit_logs` esiste; S3 popola il valore + crea l'entity `StepUpToken`.
- **Dep da S2:** se durante impersonate uno step-up trigger scatta, decisione: lo step-up vale per l'admin (actor) o per l'utente (subject)? — three-amigos.
- **Gate operativo:** prima di flip strict, 1-2 settimane di shadow per misurare "would-have-blocked" rate per trigger (Crispin); poi cutover graduale per-flag (Nygard).
- **Test harness 2FA (Step 7 roadmap):** TOTP deterministico (seed fisso, time injection) per i test E2E della matrice `(trigger × token-state × user-role × maxAge)`. Da creare come fixture di test.
- **Effort stimato:** 5-8g + 1-2 settimane di shadow calendario.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-05-25-sp5-admin-security-s1-audit-schema.md`. Two execution options:

**1. Subagent-Driven (recommended)** — dispatch fresh subagent per task, two-stage review (spec+quality) between tasks. Rate limit API permitting.

**2. Inline Execution** — execute tasks in this session using `executing-plans`, batch with checkpoints.

Which approach?

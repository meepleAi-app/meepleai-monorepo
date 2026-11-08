# Mechanic Claims v3 (Kind, Priority, Overrides, Trigger) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ogni mechanic claim porta `Kind`, `Priority`, `Overrides`, `Trigger`, proposti dall'LLM e confermati dal revisore, proiettati nella card `schema_version` 3 e usati per ordinare il blocco `[Verified Rules]` dietro un feature flag.

**Architecture:** I quattro campi vivono sull'entità `MechanicClaim` (forma canonica, ADR-084) con colonne enum e JSONB; il parser li legge dal JSON del prompt v1.2.0 (riferimenti per ordinale dentro la stessa sezione); una guardia T5 li valida e marca il claim; la card li proietta in discesa con chiavi nuove; il renderer li usa solo con `rag.mechanic-claims.v3-ordering` acceso. La riestrazione di un gioco è una nuova `MechanicAnalysis` con prompt v1.2.0.

**Tech Stack:** .NET 9, EF Core + Npgsql (jsonb via `ValueConverter`), FluentValidation, MediatR, xUnit + FluentAssertions + Moq; Next.js 16, React 19, zod, TanStack Query, Vitest.

**Spec:** `docs/for-developers/specs/2026-10-08-mechanic-claims-v3-defeasible-rules-design.md` (§1–§5, §7–§9). Il validatore Python (§6) è nel piano gemello `2026-10-08-arbiter-validate-endpoint.md`.

## Global Constraints

- Branch: creare da `main-dev` dopo `git checkout main-dev && git status` (pulito) `&& git pull --ff-only`; nome `feature/<issue>-mechanic-claims-v3`; `git config branch.<nome>.parent main-dev`. PR verso `main-dev`.
- Endpoint: solo `IMediator.Send()`, nessuna iniezione di servizi (CLAUDE.md, CQRS).
- Eccezioni: `ConflictException` (409) e `NotFoundException` (404), mai `InvalidOperationException` dagli handler (#2568). Le invarianti di dominio lanciano `ArgumentException`/`InvalidOperationException` nel dominio e l'handler le traduce.
- JSON della card: chiavi esistenti congelate (ADR-084 §3); solo chiavi **nuove** in snake_case.
- Nessun indice GIN su jsonb (ADR-084 §4).
- Nessun test skippato senza prefisso `PREVISTO:`/`GUASTO:`/`DIFETTO:`/`LIMITE:`; il conteggio dei fallimenti unit resta a zero.
- Commit: `feat|fix|test|docs(scope): descrizione` ≤ 72 caratteri nella prima riga; chiudere con `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.
- Prima di `dotnet test`: `tasklist | grep testhost` e kill (#2593). Comandi test dalla cartella `apps/api/src/Api`: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~<Classe>"`.
- Web: `pnpm typecheck && pnpm lint` prima di ogni commit che tocca `apps/web` (il pre-commit esegue il typecheck completo).
- Non toccare `ValidProviders` né altro fuori dal perimetro della spec.

## Review Focus

1. Un claim `Exception` con `Overrides` che punta a un claim di **un'altra analisi** (id valido in DB ma estraneo): deve essere rifiutato dalla guardia dell'aggregato con 400, non accettato né causare 500. Test in Task 2 (`SetStructure_RejectsOverrideOutsideAnalysis`).
2. Catena di override lunga (A→B→C→D) e ciclo lungo (A→B→C→A): il renderer deve ordinare A,B,C,D e il dominio deve rifiutare il ciclo lungo, non solo quello a due nodi. Test in Task 2 e Task 7.
3. Card v2 già pubblicata letta dal lettore v3: campi assenti ⇒ default (`Rule`, `Base`, `[]`, `null`), nessuna eccezione di deserializzazione. Test in Task 6.
4. Prompt v1.2.0 con `overrides` che cita un ordinale **fuori range** o se stesso: il parser non deve lanciare; T5 registra `T5_override_missing`, il claim nasce senza quell'arco. Test in Task 4 e Task 5.
5. Flag `rag.mechanic-claims.v3-ordering` spento: l'output del renderer deve essere **byte-identico** a oggi, anche con card v3. Golden test in Task 7.

---

## File Structure

| Path (relativo ad `apps/api/src/Api/` salvo nota) | Responsabilità |
|---|---|
| `BoundedContexts/SharedGameCatalog/Domain/Enums/MechanicClaimKind.cs` (new) | enum `Rule, Exception, Clarification, Example` |
| `BoundedContexts/SharedGameCatalog/Domain/Enums/MechanicRulePriority.cs` (new) | enum ordinato `Base, Expansion, Card, Scenario` |
| `BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicTrigger.cs` (new) | VO `{Phase?, Action?, Component?}` + normalizzazione nomi |
| `BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicClaimStructure.cs` (new) | VO di trasporto `{Kind, Priority, Overrides, Trigger}` usato da comandi e parser |
| `BoundedContexts/SharedGameCatalog/Domain/Entities/MechanicClaim.cs` | proprietà nuove, `SetStructure`, `Reconstitute` esteso |
| `BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicAnalysis.cs` | `SetClaimStructure` con invarianti sul grafo (ciclo, stessa analisi, Example) |
| `Infrastructure/Entities/SharedGameCatalog/MechanicClaimEntity.cs` + `Infrastructure/Configurations/SharedGameCatalog/MechanicClaimEntityConfiguration.cs` | colonne `kind`, `priority`, `overrides` (jsonb), `trigger` (jsonb) |
| `Infrastructure/Migrations/<ts>_AddMechanicClaimStructureV3.cs` (generata) | migrazione |
| `BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/MechanicAnalysisRepository.cs` | mapping in entrambe le direzioni |
| `BoundedContexts/SharedGameCatalog/Infrastructure/Prompts/MechanicExtractor/v1/*.md` + `EmbeddedMechanicPromptProvider.cs` | prompt v1.2.0 |
| `BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicOutputParser.cs` | lettura di `kind/priority/overrides/trigger`, risoluzione ordinale→id |
| `.../Guardrails/RuleStructureGuardrail.cs` (new) | T5 |
| `.../DependencyInjection/SharedGameCatalogServiceExtensions.cs` | registrazione T5 |
| `BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicClaimDto.cs`, `PublishedMechanicCardDto.cs` | campi nuovi nei DTO |
| `.../Commands/MechanicExtractor/ApproveMechanicClaimCommand*.cs`, `UpdateMechanicClaimStructureCommand*.cs` (new) | comandi e validatori |
| `Routing/AdminMechanicAnalysesEndpoints.cs` | request estesa + route `PUT .../claims/{claimId}/structure` |
| `BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicCardContent.cs` | `CurrentSchemaVersion = 3`, snapshot esteso |
| `BoundedContexts/SharedGameCatalog/Application/Queries/MechanicExtractor/GetPublishedMechanicCardByGameQueryHandler.cs` | proiezione dei campi nel DTO pubblicato |
| `Services/FeatureFlagService.cs` | costante `MechanicClaimsV3OrderingKey` |
| `BoundedContexts/KnowledgeBase/Application/Services/MechanicClaimInjection/VerifiedRulesRenderer.cs` | ordinamento v3 (parametro `RenderOptions`) |
| `BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionQueryHandler.cs:194-204`, `StreamQaQueryHandler.cs:634-647` | lettura del flag e passaggio delle opzioni |
| `.../Commands/MechanicExtractor/RequeueMechanicAnalysisForPromptVersionCommand*.cs` (new) | riestrazione per gioco |
| `apps/web/src/lib/api/schemas/mechanic-analyses.schemas.ts`, `lib/api/clients/admin/adminContentClient.ts`, `components/admin/mechanic-extractor/claims/{ClaimsSection,ApproveClaimDialog,ClaimStructureFields}.tsx` | review admin |

Test (relativi ad `apps/api/tests/Api.Tests/`): `BoundedContexts/SharedGameCatalog/Domain/{Entities/MechanicClaimStructureTests.cs, Aggregates/MechanicAnalysisClaimStructureTests.cs, ValueObjects/MechanicTriggerTests.cs, ValueObjects/MechanicCardContentV3Tests.cs}`, `BoundedContexts/SharedGameCatalog/Application/{MechanicExtractor/Guardrails/RuleStructureGuardrailTests.cs, Services/MechanicExtractor/MechanicOutputParserV12StructureTests.cs, Commands/MechanicExtractor/ApproveMechanicClaimCommandHandlerStructureTests.cs, Commands/MechanicExtractor/UpdateMechanicClaimStructureCommandHandlerTests.cs, Queries/MechanicExtractor/GetPublishedMechanicCardByGameQueryHandlerV3Tests.cs}`, `BoundedContexts/KnowledgeBase/Application/Services/MechanicClaimInjection/VerifiedRulesRendererOrderingTests.cs`.

---

### Task 1: Enum e value object (`Kind`, `Priority`, `Trigger`, `Structure`)

**Files:**
- Create: `BoundedContexts/SharedGameCatalog/Domain/Enums/MechanicClaimKind.cs`
- Create: `BoundedContexts/SharedGameCatalog/Domain/Enums/MechanicRulePriority.cs`
- Create: `BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicTrigger.cs`
- Create: `BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicClaimStructure.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicTriggerTests.cs`

**Interfaces:**
- Produces: `enum MechanicClaimKind { Rule = 0, Exception = 1, Clarification = 2, Example = 3 }`; `enum MechanicRulePriority { Base = 0, Expansion = 1, Card = 2, Scenario = 3 }`; `sealed record MechanicTrigger(string? Phase, string? Action, string? Component)` con `static MechanicTrigger? Create(string? phase, string? action, string? component)` (ritorna `null` se tutti vuoti), `static string Normalize(string raw)`, `bool IsEmpty`; `sealed record MechanicClaimStructure(MechanicClaimKind Kind, MechanicRulePriority Priority, IReadOnlyList<Guid> Overrides, MechanicTrigger? Trigger)` con `static MechanicClaimStructure Default`.

- [ ] **Step 1: Scrivi il test del VO `MechanicTrigger`**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicTriggerTests.cs
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicTriggerTests
{
    [Theory]
    [InlineData("  Fase di Azione ", "fase di azione")]
    [InlineData("CARTE", "carta")]        // plurale italiano regolare -e → -a? no: regola = solo trim+lower+collasso spazi; vedi sotto
    public void Normalize_TrimsLowercasesAndCollapsesWhitespace(string raw, string expected)
    {
        MechanicTrigger.Normalize(raw).Should().Be(expected);
    }

    [Fact]
    public void Create_ReturnsNull_WhenEveryFieldIsBlank()
    {
        MechanicTrigger.Create(" ", null, "").Should().BeNull();
    }

    [Fact]
    public void Create_NormalizesEachField()
    {
        var t = MechanicTrigger.Create("Fase  Azione", null, "Cavaliere ");
        t.Should().NotBeNull();
        t!.Phase.Should().Be("fase azione");
        t.Action.Should().BeNull();
        t.Component.Should().Be("cavaliere");
        t.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Structure_Default_IsBaseRuleWithoutOverridesOrTrigger()
    {
        var s = MechanicClaimStructure.Default;
        s.Kind.Should().Be(Api.BoundedContexts.SharedGameCatalog.Domain.Enums.MechanicClaimKind.Rule);
        s.Priority.Should().Be(Api.BoundedContexts.SharedGameCatalog.Domain.Enums.MechanicRulePriority.Base);
        s.Overrides.Should().BeEmpty();
        s.Trigger.Should().BeNull();
    }
}
```

Correggi la seconda `InlineData` prima di eseguire: la normalizzazione è **solo** trim, minuscolo e collasso degli spazi (niente singolarizzazione: la spec la rimanda alla risoluzione delle entità). Usa `[InlineData("CARTE", "carte")]`.

- [ ] **Step 2: Esegui il test e verifica che fallisca**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~MechanicTriggerTests"`
Expected: FAIL per tipi non definiti (`MechanicTrigger`, `MechanicClaimStructure`).

- [ ] **Step 3: Implementa enum e VO**

```csharp
// BoundedContexts/SharedGameCatalog/Domain/Enums/MechanicClaimKind.cs
namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>Kind of a mechanic claim (spec 2026-10-08 §2). Persisted as int: append-only.</summary>
public enum MechanicClaimKind
{
    Rule = 0,
    Exception = 1,
    Clarification = 2,
    Example = 3
}
```

```csharp
// BoundedContexts/SharedGameCatalog/Domain/Enums/MechanicRulePriority.cs
namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>Ordered precedence level of a claim. Higher wins. House rules live in AgentMemory and beat all.</summary>
public enum MechanicRulePriority
{
    Base = 0,
    Expansion = 1,
    Card = 2,
    Scenario = 3
}
```

```csharp
// BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicTrigger.cs
using System.Text.RegularExpressions;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// When a claim applies: canonical (trimmed, lower-case, single-spaced) names from the
/// EntityExtractor vocabulary (Phase / Action / Component). Null field = wildcard.
/// </summary>
public sealed record MechanicTrigger(string? Phase, string? Action, string? Component)
{
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    public bool IsEmpty => Phase is null && Action is null && Component is null;

    public static string Normalize(string raw) =>
        Spaces.Replace(raw.Trim(), " ").ToLowerInvariant();

    private static string? NormalizeOrNull(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : Normalize(raw);

    /// <summary>Returns null when every field is blank, so callers store "no trigger" as null.</summary>
    public static MechanicTrigger? Create(string? phase, string? action, string? component)
    {
        var t = new MechanicTrigger(NormalizeOrNull(phase), NormalizeOrNull(action), NormalizeOrNull(component));
        return t.IsEmpty ? null : t;
    }
}
```

```csharp
// BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicClaimStructure.cs
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>Transport VO for the four v3 fields (parser output, review commands).</summary>
public sealed record MechanicClaimStructure(
    MechanicClaimKind Kind,
    MechanicRulePriority Priority,
    IReadOnlyList<Guid> Overrides,
    MechanicTrigger? Trigger)
{
    public static MechanicClaimStructure Default { get; } =
        new(MechanicClaimKind.Rule, MechanicRulePriority.Base, Array.Empty<Guid>(), null);
}
```

- [ ] **Step 4: Esegui il test e verifica che passi**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~MechanicTriggerTests"`
Expected: PASS (4 test).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Enums/MechanicClaimKind.cs apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Enums/MechanicRulePriority.cs apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicTrigger.cs apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicClaimStructure.cs apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicTriggerTests.cs
git commit -m "feat(mechanic): enum Kind/Priority e VO Trigger/Structure per claim v3"
```

---

### Task 2: Entità `MechanicClaim` e aggregato: campi, `SetStructure`, invarianti del grafo

**Files:**
- Modify: `BoundedContexts/SharedGameCatalog/Domain/Entities/MechanicClaim.cs` (proprietà dopo `ReviewNote` ~:53; `Reconstitute` :225-262; nuovo metodo interno `ApplyStructure`)
- Modify: `BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicAnalysis.cs` (nuovo `SetClaimStructure` accanto a `ApproveClaim` :617)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/Entities/MechanicClaimStructureTests.cs`, `.../Aggregates/MechanicAnalysisClaimStructureTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces: su `MechanicClaim`: `MechanicClaimKind Kind`, `MechanicRulePriority Priority`, `IReadOnlyList<Guid> Overrides`, `MechanicTrigger? Trigger`; `internal void ApplyStructure(MechanicClaimStructure structure)` (senza validazione cross-claim); `Reconstitute(..., MechanicClaimKind kind = MechanicClaimKind.Rule, MechanicRulePriority priority = MechanicRulePriority.Base, IEnumerable<Guid>? overrides = null, MechanicTrigger? trigger = null)`; `CreateWithId(..., MechanicClaimStructure? structure = null)`. Su `MechanicAnalysis`: `public void SetClaimStructure(Guid claimId, MechanicClaimStructure structure)` che valida: claim esiste; stato `Pending` o `Approved` (altrimenti `InvalidOperationException("… is Rejected")`); nessun self-override; ogni id in `Overrides` appartiene a `Claims`; nessun target con `Kind == Example`; se `Kind == Example` allora `Overrides` vuoto e nessun altro claim lo sovrascrive; se `Kind == Exception` allora `Overrides.Count > 0 || Trigger != null`; nessun ciclo (DFS sul grafo risultante). Errori: `ArgumentException` per invarianti sui dati, `InvalidOperationException` per stato.

- [ ] **Step 1: Scrivi i test di dominio**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicAnalysisClaimStructureTests.cs
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicAnalysisClaimStructureTests
{
    private static MechanicAnalysis Analysis(int claims)
    {
        var a = MechanicAnalysis.Create(
            sharedGameId: Guid.NewGuid(), pdfDocumentId: Guid.NewGuid(), promptVersion: "v1.2.0",
            createdBy: Guid.NewGuid(), createdAt: DateTime.UtcNow, modelUsed: "m", provider: "p", costCapUsd: 1m);
        for (var i = 0; i < claims; i++)
        {
            var cit = MechanicCitation.Create(Guid.NewGuid(), pdfPage: 1, quote: "q", chunkId: null, displayOrder: 0);
            a.AddClaim(MechanicClaim.Create(a.Id, MechanicSection.Mechanics, $"claim {i}", i, new[] { cit }));
        }
        return a;
    }

    private static MechanicClaimStructure Exc(params Guid[] overrides) =>
        new(MechanicClaimKind.Exception, MechanicRulePriority.Card, overrides, null);

    [Fact]
    public void SetClaimStructure_StoresFieldsOnClaim()
    {
        var a = Analysis(2);
        var (general, exc) = (a.Claims[0], a.Claims[1]);

        a.SetClaimStructure(exc.Id, Exc(general.Id));

        exc.Kind.Should().Be(MechanicClaimKind.Exception);
        exc.Priority.Should().Be(MechanicRulePriority.Card);
        exc.Overrides.Should().ContainSingle().Which.Should().Be(general.Id);
        exc.Trigger.Should().BeNull();
        general.Kind.Should().Be(MechanicClaimKind.Rule, "default untouched");
    }

    [Fact]
    public void SetClaimStructure_RejectsSelfOverride()
    {
        var a = Analysis(1);
        var c = a.Claims[0];
        var act = () => a.SetClaimStructure(c.Id, Exc(c.Id));
        act.Should().Throw<ArgumentException>().WithMessage("*itself*");
    }

    [Fact]
    public void SetClaimStructure_RejectsOverrideOutsideAnalysis()
    {
        var a = Analysis(1);
        var act = () => a.SetClaimStructure(a.Claims[0].Id, Exc(Guid.NewGuid()));
        act.Should().Throw<ArgumentException>().WithMessage("*same analysis*");
    }

    [Fact]
    public void SetClaimStructure_RejectsLongCycle()
    {
        var a = Analysis(3);
        var (x, y, z) = (a.Claims[0], a.Claims[1], a.Claims[2]);
        a.SetClaimStructure(x.Id, Exc(y.Id));
        a.SetClaimStructure(y.Id, Exc(z.Id));
        var act = () => a.SetClaimStructure(z.Id, Exc(x.Id));
        act.Should().Throw<ArgumentException>().WithMessage("*cycle*");
    }

    [Fact]
    public void SetClaimStructure_RejectsExampleInOverrideGraph()
    {
        var a = Analysis(2);
        var (ex, other) = (a.Claims[0], a.Claims[1]);
        a.SetClaimStructure(ex.Id, new MechanicClaimStructure(MechanicClaimKind.Example, MechanicRulePriority.Base, Array.Empty<Guid>(), null));
        var act = () => a.SetClaimStructure(other.Id, Exc(ex.Id));
        act.Should().Throw<ArgumentException>().WithMessage("*Example*");
    }

    [Fact]
    public void SetClaimStructure_RejectsUnboundException()
    {
        var a = Analysis(1);
        var act = () => a.SetClaimStructure(a.Claims[0].Id, new MechanicClaimStructure(MechanicClaimKind.Exception, MechanicRulePriority.Base, Array.Empty<Guid>(), null));
        act.Should().Throw<ArgumentException>().WithMessage("*Exception*Overrides*Trigger*");
    }

    [Fact]
    public void SetClaimStructure_AllowedOnApproved_RejectedOnRejected()
    {
        var a = Analysis(2);
        a.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);
        var reviewer = Guid.NewGuid();
        a.ApproveClaim(a.Claims[0].Id, reviewer, DateTime.UtcNow);
        a.RejectClaim(a.Claims[1].Id, reviewer, "no", DateTime.UtcNow);

        a.SetClaimStructure(a.Claims[0].Id, new MechanicClaimStructure(MechanicClaimKind.Clarification, MechanicRulePriority.Base, Array.Empty<Guid>(), null));
        a.Claims[0].Kind.Should().Be(MechanicClaimKind.Clarification);

        var act = () => a.SetClaimStructure(a.Claims[1].Id, MechanicClaimStructure.Default);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Rejected*");
    }
}
```

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/Entities/MechanicClaimStructureTests.cs
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicClaimStructureTests
{
    [Fact]
    public void Create_DefaultsToBaseRule()
    {
        var cit = MechanicCitation.Create(Guid.NewGuid(), 1, "q", null, 0);
        var c = MechanicClaim.Create(Guid.NewGuid(), MechanicSection.Mechanics, "t", 0, new[] { cit });
        c.Kind.Should().Be(MechanicClaimKind.Rule);
        c.Priority.Should().Be(MechanicRulePriority.Base);
        c.Overrides.Should().BeEmpty();
        c.Trigger.Should().BeNull();
    }

    [Fact]
    public void Reconstitute_RoundTripsStructure()
    {
        var target = Guid.NewGuid();
        var cit = MechanicCitation.Create(Guid.NewGuid(), 1, "q", null, 0);
        var c = MechanicClaim.Reconstitute(
            Guid.NewGuid(), Guid.NewGuid(), MechanicSection.Phases, "t", 0, MechanicClaimStatus.Approved,
            null, null, null, new[] { cit },
            kind: MechanicClaimKind.Exception, priority: MechanicRulePriority.Scenario,
            overrides: new[] { target }, trigger: new MechanicTrigger("fase azione", null, null));
        c.Kind.Should().Be(MechanicClaimKind.Exception);
        c.Priority.Should().Be(MechanicRulePriority.Scenario);
        c.Overrides.Should().Equal(target);
        c.Trigger!.Phase.Should().Be("fase azione");
        c.IsNew.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Esegui e verifica che fallisca**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~MechanicAnalysisClaimStructureTests|FullyQualifiedName~MechanicClaimStructureTests"`
Expected: FAIL (proprietà e metodi inesistenti).

- [ ] **Step 3: Implementa su `MechanicClaim`**

Aggiungi dopo `ReviewNote` (riga ~53):

```csharp
    /// <summary>Kind of claim (spec 2026-10-08 §2). Default Rule.</summary>
    public MechanicClaimKind Kind { get; private set; } = MechanicClaimKind.Rule;

    /// <summary>Precedence level; higher wins. Default Base.</summary>
    public MechanicRulePriority Priority { get; private set; } = MechanicRulePriority.Base;

    private readonly List<Guid> _overrides = new();

    /// <summary>Ids of claims of the SAME analysis this claim overrides (validated by the aggregate).</summary>
    public IReadOnlyList<Guid> Overrides => _overrides.AsReadOnly();

    /// <summary>When this claim applies; null = always.</summary>
    public MechanicTrigger? Trigger { get; private set; }

    /// <summary>
    /// Applies the four v3 fields WITHOUT cross-claim validation. Only the aggregate
    /// (<c>MechanicAnalysis.SetClaimStructure</c>) and the parser may call it.
    /// </summary>
    internal void ApplyStructure(MechanicClaimStructure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);
        Kind = structure.Kind;
        Priority = structure.Priority;
        _overrides.Clear();
        _overrides.AddRange(structure.Overrides.Distinct());
        Trigger = structure.Trigger;
    }
```

Estendi `CreateWithId` con parametro finale `MechanicClaimStructure? structure = null` e, prima del `return claim;`, `if (structure is not null) { claim.ApplyStructure(structure); }`.

Estendi `Reconstitute` con quattro parametri opzionali in coda:

```csharp
        MechanicClaimKind kind = MechanicClaimKind.Rule,
        MechanicRulePriority priority = MechanicRulePriority.Base,
        IEnumerable<Guid>? overrides = null,
        MechanicTrigger? trigger = null)
```

e nell'inizializzatore: `Kind = kind, Priority = priority, Trigger = trigger`, poi `if (overrides is not null) { claim._overrides.AddRange(overrides); }`.

- [ ] **Step 4: Implementa `SetClaimStructure` su `MechanicAnalysis`** (accanto a `ApproveClaim`, riga ~617)

```csharp
    /// <summary>
    /// Sets Kind/Priority/Overrides/Trigger on one claim (spec 2026-10-08 §2). Allowed while the claim
    /// is Pending or Approved (the card changes only at the next publish). Validates the override
    /// graph of the whole analysis: same-analysis targets, no self/Example edges, acyclic.
    /// </summary>
    public void SetClaimStructure(Guid claimId, MechanicClaimStructure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);
        var claim = _claims.FirstOrDefault(c => c.Id == claimId)
            ?? throw new InvalidOperationException($"Claim {claimId} does not belong to analysis {Id}.");

        if (claim.Status == MechanicClaimStatus.Rejected)
        {
            throw new InvalidOperationException($"Claim {claimId} is Rejected; structure cannot be changed.");
        }

        var overrides = structure.Overrides.Distinct().ToList();
        if (overrides.Contains(claimId))
        {
            throw new ArgumentException("A claim cannot override itself.", nameof(structure));
        }

        foreach (var target in overrides)
        {
            var t = _claims.FirstOrDefault(c => c.Id == target)
                ?? throw new ArgumentException($"Override target {target} is not a claim of the same analysis.", nameof(structure));
            if (t.Kind == MechanicClaimKind.Example)
            {
                throw new ArgumentException("An Example claim cannot be overridden.", nameof(structure));
            }
        }

        if (structure.Kind == MechanicClaimKind.Example)
        {
            if (overrides.Count > 0)
            {
                throw new ArgumentException("An Example claim cannot override other claims.", nameof(structure));
            }
            if (_claims.Any(c => c.Id != claimId && c.Overrides.Contains(claimId)))
            {
                throw new ArgumentException("An Example claim cannot be the target of an override.", nameof(structure));
            }
        }

        if (structure.Kind == MechanicClaimKind.Exception && overrides.Count == 0 && structure.Trigger is null)
        {
            throw new ArgumentException("An Exception claim needs at least one Overrides target or a Trigger.", nameof(structure));
        }

        // Acyclicity on the graph as it WOULD be after this change.
        var edges = _claims.ToDictionary(c => c.Id, c => c.Id == claimId ? (IReadOnlyList<Guid>)overrides : c.Overrides);
        if (HasCycle(edges))
        {
            throw new ArgumentException("Overrides would create a cycle.", nameof(structure));
        }

        claim.ApplyStructure(structure with { Overrides = overrides });
    }

    private static bool HasCycle(IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> edges)
    {
        var state = new Dictionary<Guid, int>(); // 0 = new, 1 = visiting, 2 = done
        bool Visit(Guid n)
        {
            if (state.TryGetValue(n, out var s))
            {
                return s == 1;
            }
            state[n] = 1;
            if (edges.TryGetValue(n, out var next))
            {
                foreach (var m in next)
                {
                    if (Visit(m)) { return true; }
                }
            }
            state[n] = 2;
            return false;
        }
        return edges.Keys.Any(Visit);
    }
```

Aggiungi gli `using` di `Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects` se mancano.

- [ ] **Step 5: Esegui e verifica che passi**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~MechanicAnalysisClaimStructureTests|FullyQualifiedName~MechanicClaimStructureTests"`
Expected: PASS (9 test). Esegui anche `--filter "BoundedContext=SharedGameCatalog&Category=Unit"` per confermare nessuna regressione.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain
git commit -m "feat(mechanic): claim v3 nel dominio con invarianti sul grafo Overrides"
```

---

### Task 3: Persistenza: entity, configurazione EF, migrazione, repository

**Files:**
- Modify: `Infrastructure/Entities/SharedGameCatalog/MechanicClaimEntity.cs`
- Modify: `Infrastructure/Configurations/SharedGameCatalog/MechanicClaimEntityConfiguration.cs`
- Modify: `BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/MechanicAnalysisRepository.cs` (`MapClaimToDomain` :325, `MapClaimToEntity` :396)
- Create (generata): `Infrastructure/Migrations/<timestamp>_AddMechanicClaimStructureV3.cs`
- Test: integrazione esistente per il repository, se presente, altrimenti il round-trip è coperto dal test di mapping in Step 1.

**Interfaces:**
- Consumes: Task 2.
- Produces: colonne `kind int NOT NULL DEFAULT 0`, `priority int NOT NULL DEFAULT 0`, `overrides jsonb NULL`, `trigger jsonb NULL`; check `ck_mechanic_claims_kind_range (kind BETWEEN 0 AND 3)`, `ck_mechanic_claims_priority_range (priority BETWEEN 0 AND 3)`; indice `ix_mechanic_claims_kind` su `(analysis_id, kind)`.

- [ ] **Step 1: Scrivi il test di mapping del repository**

Trova il test esistente del repository: `grep -rl "MapClaimToDomain\|MechanicAnalysisRepository" apps/api/tests/Api.Tests | head`. Se esiste una classe unit per il mapping, aggiungi lì; altrimenti crea `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/MechanicClaimMappingTests.cs` che usa i metodi privati via `InternalsVisibleTo` **solo se già configurato** (`grep InternalsVisibleTo apps/api/src/Api/Api.csproj`); in caso contrario rendi `MapClaimToEntity`/`MapClaimToDomain` `internal static` e testa così:

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicClaimMappingTests
{
    [Fact]
    public void Entity_RoundTrip_PreservesStructure()
    {
        var target = Guid.NewGuid();
        var cit = MechanicCitation.Create(Guid.NewGuid(), 2, "q", null, 0);
        var claim = MechanicClaim.Reconstitute(
            Guid.NewGuid(), Guid.NewGuid(), MechanicSection.Faq, "t", 1, MechanicClaimStatus.Pending,
            null, null, null, new[] { cit },
            kind: MechanicClaimKind.Exception, priority: MechanicRulePriority.Card,
            overrides: new[] { target }, trigger: new MechanicTrigger(null, "muovere", null));

        var entity = MechanicAnalysisRepository.MapClaimToEntity(claim);
        entity.Kind.Should().Be(1);
        entity.Priority.Should().Be(2);
        entity.Overrides.Should().Equal(target);
        entity.Trigger!.Action.Should().Be("muovere");

        var back = MechanicAnalysisRepository.MapClaimToDomain(entity);
        back.Kind.Should().Be(MechanicClaimKind.Exception);
        back.Priority.Should().Be(MechanicRulePriority.Card);
        back.Overrides.Should().Equal(target);
        back.Trigger.Should().Be(claim.Trigger);
    }

    [Fact]
    public void Entity_WithNullJsonColumns_MapsToDefaults()
    {
        var entity = new Api.Infrastructure.Entities.SharedGameCatalog.MechanicClaimEntity
        {
            Id = Guid.NewGuid(), AnalysisId = Guid.NewGuid(), Section = 0, Text = "t", Status = 0,
            Kind = 0, Priority = 0, Overrides = null, Trigger = null,
            Citations = new List<Api.Infrastructure.Entities.SharedGameCatalog.MechanicCitationEntity>
            {
                new() { Id = Guid.NewGuid(), PdfPage = 1, Quote = "q", DisplayOrder = 0 }
            }
        };
        var back = MechanicAnalysisRepository.MapClaimToDomain(entity);
        back.Overrides.Should().BeEmpty();
        back.Trigger.Should().BeNull();
    }
}
```

- [ ] **Step 2: Esegui e verifica che fallisca**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~MechanicClaimMappingTests"`
Expected: FAIL (proprietà entity inesistenti / metodi privati).

- [ ] **Step 3: Estendi l'entity**

In `MechanicClaimEntity.cs`, dopo `Validations`:

```csharp
    /// <summary>0=Rule, 1=Exception, 2=Clarification, 3=Example (spec 2026-10-08 §2).</summary>
    public int Kind { get; set; }

    /// <summary>0=Base, 1=Expansion, 2=Card, 3=Scenario.</summary>
    public int Priority { get; set; }

    /// <summary>Ids of overridden claims (same analysis). jsonb; null = none.</summary>
    public List<Guid>? Overrides { get; set; }

    /// <summary>Trigger VO as jsonb; null = always applies.</summary>
    public MechanicTrigger? Trigger { get; set; }
```

- [ ] **Step 4: Estendi la configurazione EF**

In `MechanicClaimEntityConfiguration.Configure`, dentro `ToTable` aggiungi:

```csharp
            t.HasCheckConstraint("ck_mechanic_claims_kind_range", "kind BETWEEN 0 AND 3");
            t.HasCheckConstraint("ck_mechanic_claims_priority_range", "priority BETWEEN 0 AND 3");
```

e dopo la proprietà `Validations`:

```csharp
        builder.Property(c => c.Kind).HasColumnName("kind").HasDefaultValue(0).IsRequired();
        builder.Property(c => c.Priority).HasColumnName("priority").HasDefaultValue(0).IsRequired();

        var overridesConverter = new ValueConverter<List<Guid>?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => v == null ? null : JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null));
        builder.Property(c => c.Overrides)
            .HasColumnName("overrides")
            .HasColumnType("jsonb")
            .HasConversion(overridesConverter);

        var triggerConverter = new ValueConverter<MechanicTrigger?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => v == null ? null : JsonSerializer.Deserialize<MechanicTrigger>(v, (JsonSerializerOptions?)null));
        builder.Property(c => c.Trigger)
            .HasColumnName("trigger")
            .HasColumnType("jsonb")
            .HasConversion(triggerConverter);

        builder.HasIndex(c => new { c.AnalysisId, c.Kind }).HasDatabaseName("ix_mechanic_claims_analysis_kind");
```

Nessun value comparer: il repository riscrive l'entity intera in `Update` (stessa ragione documentata per `Validations`).

- [ ] **Step 5: Estendi il repository**

`MapClaimToEntity` (:396): aggiungi `Kind = (int)claim.Kind, Priority = (int)claim.Priority, Overrides = claim.Overrides.Count == 0 ? null : claim.Overrides.ToList(), Trigger = claim.Trigger,`. `MapClaimToDomain` (:325): aggiungi gli argomenti `kind: (MechanicClaimKind)entity.Kind, priority: (MechanicRulePriority)entity.Priority, overrides: entity.Overrides, trigger: entity.Trigger`. Rendi entrambi `internal static` se il test lo richiede (Step 1).

- [ ] **Step 6: Genera la migrazione e controlla l'SQL**

Run: `cd apps/api/src/Api && dotnet ef migrations add AddMechanicClaimStructureV3`
Apri il file generato: deve contenere solo `AddColumn` per `kind`, `priority`, `overrides`, `trigger` su `mechanic_claims`, i due `AddCheckConstraint`, `CreateIndex ix_mechanic_claims_analysis_kind`, e il relativo `Down`. Se compaiono modifiche ad altre tabelle, fermati: lo snapshot era già fuori sincrono (vedi memoria `dotnet-ef-nuget-pitfall` se il comando fallisce per restore).

- [ ] **Step 7: Esegui test e migrazione locale**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~MechanicClaimMappingTests"` → PASS.
Run: `dotnet ef database update` contro il DB di sviluppo (`make dev-core` in `infra/` se non attivo) → applica senza errori. Verifica: `psql ... -c "\d mechanic_claims"` mostra le quattro colonne.

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/MechanicClaimEntity.cs apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/MechanicClaimEntityConfiguration.cs apps/api/src/Api/Infrastructure/Migrations apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/MechanicAnalysisRepository.cs apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Infrastructure/MechanicClaimMappingTests.cs
git commit -m "feat(mechanic): colonne kind/priority/overrides/trigger su mechanic_claims"
```

---

### Task 4: Prompt v1.2.0 e parser

**Files:**
- Modify: `BoundedContexts/SharedGameCatalog/Infrastructure/Prompts/MechanicExtractor/v1/system.md` (sezione nuova «Rule structure»), `mechanics.md`, `phases.md`, `faq.md`, `setup.md`, `components.md`, `endgame.md`, `resources.md`, `victory.md` (schema esteso), `summary.md` (nessun campo: resta `Rule`)
- Modify: `.../MechanicExtractor/EmbeddedMechanicPromptProvider.cs:20` → `"v1.2.0"`
- Modify: `.../MechanicExtractor/MechanicOutputParser.cs` (helper `ReadStructure`, risoluzione ordinali per sezione)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicOutputParserV12StructureTests.cs`

**Interfaces:**
- Consumes: Task 1–2 (`MechanicClaimStructure`, `CreateWithId(..., structure)`).
- Produces: contratto JSON per item di sezione: `"kind": "rule|exception|clarification|example"`, `"priority": "base|expansion|card|scenario"`, `"overrides": [<ordinali 0-based nella stessa sezione>]`, `"trigger": {"phase": "...", "action": "...", "component": "..."}` tutti opzionali. Parser: `internal static MechanicClaimStructure ReadStructure(JsonElement item, IReadOnlyList<Guid> sectionClaimIds, int selfOrdinal)`; ordinali fuori range o uguali a `selfOrdinal` vengono **ignorati** (T5 li segnala, Task 5).

- [ ] **Step 1: Scrivi i test del parser**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicOutputParserV12StructureTests.cs
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicOutputParserV12StructureTests
{
    private const string Phases = """
    {"phases":[
      {"name":"Azione","description":"ogni giocatore fa due azioni","order":1,"kind":"rule","priority":"base",
       "citations":[{"pdf_page":4,"quote":"due azioni"}]},
      {"name":"Carta Fretta","description":"con la carta Fretta si fanno tre azioni","order":2,"kind":"exception","priority":"card",
       "overrides":[0],"trigger":{"phase":"Azione","component":"Carta Fretta"},
       "citations":[{"pdf_page":12,"quote":"tre azioni"}]},
      {"name":"Esempio","description":"Anna gioca Fretta e fa tre azioni","order":3,"kind":"example",
       "citations":[{"pdf_page":12,"quote":"Anna"}]}
    ]}
    """;

    [Fact]
    public void Parse_ReadsKindPriorityTriggerAndResolvesOverridesByOrdinal()
    {
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Phases] = Phases });

        claims.Should().HaveCount(3);
        var general = claims[0];
        var exc = claims[1];
        general.Kind.Should().Be(MechanicClaimKind.Rule);
        exc.Kind.Should().Be(MechanicClaimKind.Exception);
        exc.Priority.Should().Be(MechanicRulePriority.Card);
        exc.Overrides.Should().Equal(general.Id);
        exc.Trigger!.Phase.Should().Be("azione");
        exc.Trigger.Component.Should().Be("carta fretta");
        claims[2].Kind.Should().Be(MechanicClaimKind.Example);
    }

    [Fact]
    public void Parse_MissingFields_DefaultToBaseRule()
    {
        const string json = """{"mechanics":[{"name":"X","description":"d","citations":[{"pdf_page":1,"quote":"q"}]}]}""";
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = json });
        claims.Single().Kind.Should().Be(MechanicClaimKind.Rule);
        claims.Single().Priority.Should().Be(MechanicRulePriority.Base);
        claims.Single().Overrides.Should().BeEmpty();
    }

    [Fact]
    public void Parse_OutOfRangeOrSelfOrdinal_IsIgnoredNotThrown()
    {
        const string json = """
        {"mechanics":[
          {"name":"A","description":"d","kind":"exception","priority":"card","overrides":[0, 7, -1],"citations":[{"pdf_page":1,"quote":"q"}]}
        ]}
        """;
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = json });
        claims.Single().Overrides.Should().BeEmpty();
        claims.Single().Kind.Should().Be(MechanicClaimKind.Exception);
    }

    [Fact]
    public void Parse_UnknownKindOrPriority_FallsBackToDefault()
    {
        const string json = """{"mechanics":[{"name":"X","description":"d","kind":"banana","priority":"max","citations":[{"pdf_page":1,"quote":"q"}]}]}""";
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = json });
        claims.Single().Kind.Should().Be(MechanicClaimKind.Rule);
        claims.Single().Priority.Should().Be(MechanicRulePriority.Base);
    }
}
```

- [ ] **Step 2: Esegui e verifica che fallisca**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~MechanicOutputParserV12StructureTests"`
Expected: FAIL (campi ignorati ⇒ `Kind` sempre `Rule`, `Overrides` vuoto).

- [ ] **Step 3: Implementa nel parser**

Il parser oggi genera claim in streaming per sezione (`yield return BuildClaim(...)`), quindi gli id dei claim della sezione non sono noti prima di aver visto tutti gli item. Modifica ogni `ParseXxx` che itera un array così: **prima** assegna un `claimId` a ogni item valido in una prima passata (lista `(JsonElement item, Guid id, int ordinal, string anchor, …)`), poi nella seconda passata costruisce i claim con `ReadStructure(item, ids, ordinal)`. Per `ParseSummary` e `ParseVictory` (oggetti singoli) passa `ids = [claimId]` e `selfOrdinal = 0`.

Helper comune in fondo al file:

```csharp
    private static readonly IReadOnlyDictionary<string, MechanicClaimKind> KindNames =
        new Dictionary<string, MechanicClaimKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["rule"] = MechanicClaimKind.Rule,
            ["exception"] = MechanicClaimKind.Exception,
            ["clarification"] = MechanicClaimKind.Clarification,
            ["example"] = MechanicClaimKind.Example
        };

    private static readonly IReadOnlyDictionary<string, MechanicRulePriority> PriorityNames =
        new Dictionary<string, MechanicRulePriority>(StringComparer.OrdinalIgnoreCase)
        {
            ["base"] = MechanicRulePriority.Base,
            ["expansion"] = MechanicRulePriority.Expansion,
            ["card"] = MechanicRulePriority.Card,
            ["scenario"] = MechanicRulePriority.Scenario
        };

    /// <summary>
    /// Reads kind/priority/overrides/trigger from one section item (prompt v1.2.0). Overrides are
    /// 0-based ordinals INTO THE SAME SECTION's claim id list; out-of-range or self ordinals are
    /// dropped here (T5 reports them). Unknown kind/priority fall back to Rule/Base.
    /// </summary>
    internal static MechanicClaimStructure ReadStructure(
        JsonElement item, IReadOnlyList<Guid> sectionClaimIds, int selfOrdinal)
    {
        var kind = ReadString(item, "kind") is { } k && KindNames.TryGetValue(k.Trim(), out var kk)
            ? kk : MechanicClaimKind.Rule;
        var priority = ReadString(item, "priority") is { } p && PriorityNames.TryGetValue(p.Trim(), out var pp)
            ? pp : MechanicRulePriority.Base;

        var overrides = new List<Guid>();
        if (item.TryGetProperty("overrides", out var ov) && ov.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in ov.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var ord)
                    && ord >= 0 && ord < sectionClaimIds.Count && ord != selfOrdinal
                    && !overrides.Contains(sectionClaimIds[ord]))
                {
                    overrides.Add(sectionClaimIds[ord]);
                }
            }
        }

        MechanicTrigger? trigger = null;
        if (item.TryGetProperty("trigger", out var tr) && tr.ValueKind == JsonValueKind.Object)
        {
            trigger = MechanicTrigger.Create(ReadString(tr, "phase"), ReadString(tr, "action"), ReadString(tr, "component"));
        }

        return new MechanicClaimStructure(kind, priority, overrides, trigger);
    }
```

`BuildClaim` riceve un parametro aggiuntivo `MechanicClaimStructure structure` e lo passa a `CreateWithId(..., structure: structure)`. Nota: gli ordinali del prompt contano gli item **validi** (quelli che producono un claim); documentalo nel prompt (Step 4) con «numera solo le voci che restituisci, nell'ordine in cui le restituisci».

- [ ] **Step 4: Aggiorna i prompt**

In `system.md` aggiungi dopo «Domain conventions»:

```markdown
## Rule structure (v1.2.0)

Every item of a list section MAY carry four optional fields:
- `kind`: one of `rule` (general rule, default), `exception` (modifies another rule), `clarification` (restates without changing), `example` (a worked example of play). Do NOT emit flavour or lore text as items at all.
- `priority`: one of `base` (core rulebook, default), `expansion`, `card` (effect printed on a card/tile), `scenario`.
- `overrides`: array of 0-based positions, within THIS section's output, of the items this one overrides. Count only the items you return, in the order you return them. An `exception` must have `overrides` or `trigger`.
- `trigger`: object `{ "phase": "...", "action": "...", "component": "..." }` with short names reused verbatim from the rulebook (any field may be omitted). Describes WHEN the item applies.
```

In ogni sezione a lista (`mechanics.md`, `phases.md`, `faq.md`, `setup.md`, `components.md`, `endgame.md`, `resources.md`) estendi lo schema JSON dell'item con `"kind": "rule|exception|clarification|example (optional)", "priority": "base|expansion|card|scenario (optional)", "overrides": [0], "trigger": {"phase": "string (optional)", "action": "string (optional)", "component": "string (optional)"}` e aggiorna l'intestazione a `(v1.2.0)`. In `victory.md` applica i campi all'oggetto `victory` (una sola voce, `overrides` non ammesso). `summary.md` invariato.

In `EmbeddedMechanicPromptProvider.cs` sostituisci il commento e la riga: `// v1.2.0 (spec 2026-10-08): kind/priority/overrides/trigger per item.` e `public string PromptVersion => "v1.2.0";`.

- [ ] **Step 5: Esegui tutti i test del parser**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~MechanicOutputParser"`
Expected: PASS, inclusi `MechanicOutputParserAnchorTests` e `V11SectionsTests` (gli anchor restano gli indici **raw** della sorgente, non gli ordinali: non toccare `anchor`).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Prompts apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/EmbeddedMechanicPromptProvider.cs apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicOutputParser.cs apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicOutputParserV12StructureTests.cs
git commit -m "feat(mechanic): prompt v1.2.0 e parser di kind/priority/overrides/trigger"
```

---

### Task 5: Guardia T5 `RuleStructureGuardrail`

**Files:**
- Create: `BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/Guardrails/RuleStructureGuardrail.cs`
- Modify: `BoundedContexts/SharedGameCatalog/Infrastructure/DependencyInjection/SharedGameCatalogServiceExtensions.cs:110` (registrazione)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/MechanicExtractor/Guardrails/RuleStructureGuardrailTests.cs`

**Interfaces:**
- Consumes: `IMechanicGuardrail`, `MechanicGuardrailContext`, `MechanicJsonWalker` (esistenti); `MechanicTrigger.Normalize` (Task 1).
- Produces: `RuleFamily = "T5"`, `Order = 25`; violazioni `T5_override_missing` (ordinale fuori range o self), `T5_override_cycle`, `T5_example_in_override`, `T5_exception_unbound`, `T5_trigger_unknown` (nome non presente in `context.SourceChunks` dopo normalizzazione: approssimazione del vocabolario finché la risoluzione delle entità non esiste). `Path` della violazione = anchor dell'item (`$.<section>[i]`), così `CorrelateValidations` la attacca al claim giusto.

- [ ] **Step 1: Scrivi i test**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/MechanicExtractor/Guardrails/RuleStructureGuardrailTests.cs
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.MechanicExtractor.Guardrails;

public sealed class RuleStructureGuardrailTests
{
    private static readonly RuleStructureGuardrail Sut = new();

    private static MechanicSourceChunk Chunk(string text) => new(0, 4, null, text);

    [Fact]
    public void Family_And_Order()
    {
        Sut.RuleFamily.Should().Be("T5");
        Sut.Order.Should().Be(25);
    }

    [Fact]
    public async Task ValidStructure_NoViolations()
    {
        const string json = """
        {"mechanics":[
          {"description":"regola","citations":[{"pdf_page":4,"quote":"q"}]},
          {"description":"eccezione","kind":"exception","priority":"card","overrides":[0],"trigger":{"phase":"fase azione"},"citations":[{"pdf_page":4,"quote":"q"}]}
        ]}
        """;
        var ctx = GuardrailTestContext.Ctx(json, new[] { Chunk("Durante la Fase Azione ogni giocatore...") });
        var v = await Sut.EvaluateAsync(ctx, CancellationToken.None);
        v.Should().BeEmpty();
    }

    [Fact]
    public async Task OverrideOutOfRangeOrSelf_ReportsMissing_WithItemPath()
    {
        const string json = """{"mechanics":[{"description":"a","kind":"exception","overrides":[0,5],"citations":[{"pdf_page":4,"quote":"q"}]}]}""";
        var v = await Sut.EvaluateAsync(GuardrailTestContext.Ctx(json), CancellationToken.None);
        v.Should().HaveCount(2).And.OnlyContain(x => x.Rule == "T5_override_missing" && x.Path == "$.mechanics[0]");
    }

    [Fact]
    public async Task Cycle_IsReportedOnce()
    {
        const string json = """
        {"mechanics":[
          {"description":"a","kind":"exception","overrides":[1],"citations":[{"pdf_page":4,"quote":"q"}]},
          {"description":"b","kind":"exception","overrides":[0],"citations":[{"pdf_page":4,"quote":"q"}]}
        ]}
        """;
        var v = await Sut.EvaluateAsync(GuardrailTestContext.Ctx(json), CancellationToken.None);
        v.Should().ContainSingle(x => x.Rule == "T5_override_cycle");
    }

    [Fact]
    public async Task ExampleInOverrideGraph_IsReported()
    {
        const string json = """
        {"mechanics":[
          {"description":"es","kind":"example","citations":[{"pdf_page":4,"quote":"q"}]},
          {"description":"b","kind":"exception","overrides":[0],"citations":[{"pdf_page":4,"quote":"q"}]}
        ]}
        """;
        var v = await Sut.EvaluateAsync(GuardrailTestContext.Ctx(json), CancellationToken.None);
        v.Should().ContainSingle(x => x.Rule == "T5_example_in_override" && x.Path == "$.mechanics[1]");
    }

    [Fact]
    public async Task ExceptionWithoutOverridesOrTrigger_IsReported()
    {
        const string json = """{"mechanics":[{"description":"a","kind":"exception","citations":[{"pdf_page":4,"quote":"q"}]}]}""";
        var v = await Sut.EvaluateAsync(GuardrailTestContext.Ctx(json), CancellationToken.None);
        v.Should().ContainSingle(x => x.Rule == "T5_exception_unbound");
    }

    [Fact]
    public async Task TriggerNameAbsentFromSources_IsReported()
    {
        const string json = """{"mechanics":[{"description":"a","trigger":{"phase":"fase lunare"},"citations":[{"pdf_page":4,"quote":"q"}]}]}""";
        var ctx = GuardrailTestContext.Ctx(json, new[] { Chunk("Durante la Fase Azione...") });
        var v = await Sut.EvaluateAsync(ctx, CancellationToken.None);
        v.Should().ContainSingle(x => x.Rule == "T5_trigger_unknown" && x.Message!.Contains("fase lunare"));
    }
}
```

- [ ] **Step 2: Esegui e verifica che fallisca**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~RuleStructureGuardrailTests"`
Expected: FAIL (classe inesistente).

- [ ] **Step 3: Implementa la guardia**

```csharp
// BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/Guardrails/RuleStructureGuardrail.cs
using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;

/// <summary>
/// T5 — structural checks on the v1.2.0 fields (spec 2026-10-08 §3): override ordinals in range and
/// not self, acyclic override graph, no Example in the graph, Exception bound by overrides or trigger,
/// trigger names present in the retrieved sources (vocabulary proxy until entity resolution lands).
/// Violations carry the item's JSONPath so CorrelateValidations attaches them to the right claim.
/// A T5 failure does not drop the claim; it flags it for the reviewer.
/// </summary>
internal sealed class RuleStructureGuardrail : IMechanicGuardrail
{
    public string RuleFamily => "T5";
    public int Order => 25;

    private static readonly string[] ListSections = { "mechanics", "phases", "faq", "setup", "components", "endgame", "resources" };

    public Task<IReadOnlyList<MechanicValidationViolation>> EvaluateAsync(
        MechanicGuardrailContext context, CancellationToken cancellationToken)
    {
        var violations = new List<MechanicValidationViolation>();
        var sourceText = string.Join('\n', context.SourceChunks.Select(c => c.Content));
        var normalizedSource = MechanicTrigger.Normalize(sourceText);

        foreach (var section in ListSections)
        {
            if (!context.Root.TryGetProperty(section, out var items) || items.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var list = items.EnumerateArray().ToList();
            var kinds = list.Select(i => ReadString(i, "kind")?.Trim().ToLowerInvariant() ?? "rule").ToList();
            var edges = new Dictionary<int, List<int>>();

            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                var path = $"$.{section}[{i}]";
                var targets = new List<int>();

                if (item.TryGetProperty("overrides", out var ov) && ov.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in ov.EnumerateArray())
                    {
                        if (el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var ord) || ord < 0 || ord >= list.Count || ord == i)
                        {
                            violations.Add(new MechanicValidationViolation("T5_override_missing",
                                $"Override ordinal '{el}' is out of range or refers to the item itself.", path));
                            continue;
                        }
                        if (kinds[ord] == "example" || kinds[i] == "example")
                        {
                            violations.Add(new MechanicValidationViolation("T5_example_in_override",
                                "An example item cannot take part in an override edge.", path));
                            continue;
                        }
                        targets.Add(ord);
                    }
                }
                edges[i] = targets;

                var hasTrigger = item.TryGetProperty("trigger", out var tr) && tr.ValueKind == JsonValueKind.Object;
                if (kinds[i] == "exception" && targets.Count == 0 && !hasTrigger)
                {
                    violations.Add(new MechanicValidationViolation("T5_exception_unbound",
                        "An exception item needs 'overrides' or 'trigger'.", path));
                }

                if (hasTrigger)
                {
                    foreach (var field in new[] { "phase", "action", "component" })
                    {
                        var name = ReadString(tr, field);
                        if (!string.IsNullOrWhiteSpace(name) && !normalizedSource.Contains(MechanicTrigger.Normalize(name), StringComparison.Ordinal))
                        {
                            violations.Add(new MechanicValidationViolation("T5_trigger_unknown",
                                $"Trigger {field} '{name}' does not appear in the retrieved sources.", path));
                        }
                    }
                }
            }

            if (HasCycle(edges))
            {
                violations.Add(new MechanicValidationViolation("T5_override_cycle",
                    "The overrides graph of this section contains a cycle.", $"$.{section}"));
            }
        }

        return Task.FromResult<IReadOnlyList<MechanicValidationViolation>>(violations);
    }

    private static bool HasCycle(Dictionary<int, List<int>> edges)
    {
        var state = new Dictionary<int, int>();
        bool Visit(int n)
        {
            if (state.TryGetValue(n, out var s)) { return s == 1; }
            state[n] = 1;
            foreach (var m in edges.TryGetValue(n, out var next) ? next : new List<int>())
            {
                if (Visit(m)) { return true; }
            }
            state[n] = 2;
            return false;
        }
        return edges.Keys.Any(Visit);
    }

    private static string? ReadString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;
}
```

Registra in `SharedGameCatalogServiceExtensions.cs` accanto alla riga 110: `services.AddScoped<IMechanicGuardrail, RuleStructureGuardrail>();`.

- [ ] **Step 4: Esegui e verifica che passi; controlla l'esito "fail non blocca"**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~RuleStructureGuardrailTests|FullyQualifiedName~MechanicGuardrail|FullyQualifiedName~MechanicAnalysisExecutor"`
Expected: PASS. Apri `MechanicOutputValidator.cs` e verifica come un `fail` di una famiglia incide sulla decisione di scarto/retry: se esiste una lista di famiglie **bloccanti**, T5 **non** deve esserci (spec §3: un fallimento T5 marca, non scarta). Se la logica è "qualunque fail ⇒ retry", aggiungi T5 all'eccezione con un test in `MechanicOutputValidatorTests` (`T5Fail_DoesNotTriggerRetry`). Documenta la scelta nel commit.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/Guardrails/RuleStructureGuardrail.cs apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/DependencyInjection/SharedGameCatalogServiceExtensions.cs apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/MechanicExtractor/Guardrails/RuleStructureGuardrailTests.cs
git commit -m "feat(mechanic): guardia T5 sulla struttura delle regole (non bloccante)"
```

---

### Task 6: DTO, comandi di review e route

**Files:**
- Modify: `BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicClaimDto.cs` (campi `Kind`, `Priority`, `Overrides`, `Trigger`)
- Modify: `.../Commands/MechanicExtractor/ApproveMechanicClaimCommand.cs` (parametro `MechanicClaimStructureDto? Structure = null`), `ApproveMechanicClaimCommandHandler.cs` (chiama `SetClaimStructure` prima di `ApproveClaim`; `ToDto` esteso), `ApproveMechanicClaimCommandValidator.cs`
- Create: `.../Commands/MechanicExtractor/UpdateMechanicClaimStructureCommand.cs`, `UpdateMechanicClaimStructureCommandHandler.cs`, `UpdateMechanicClaimStructureCommandValidator.cs`
- Create: `BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicClaimStructureDto.cs`
- Modify: `Routing/AdminMechanicAnalysesEndpoints.cs` (`ApproveClaimRequest(string? Note, MechanicClaimStructureDto? Structure)`; nuova route `PUT /{id:guid}/claims/{claimId:guid}/structure`)
- Modify: ogni altro `ToDto` di `MechanicClaimDto` (`grep -rn "new MechanicClaimDto\|MechanicClaimDto(" apps/api/src/Api --include=*.cs`): aggiungi i quattro argomenti.
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/ApproveMechanicClaimCommandHandlerStructureTests.cs`, `UpdateMechanicClaimStructureCommandHandlerTests.cs`

**Interfaces:**
- Consumes: Task 2 (`SetClaimStructure`).
- Produces: `public sealed record MechanicClaimStructureDto(MechanicClaimKind Kind, MechanicRulePriority Priority, IReadOnlyList<Guid> Overrides, MechanicTriggerDto? Trigger)`; `public sealed record MechanicTriggerDto(string? Phase, string? Action, string? Component)`; `internal record UpdateMechanicClaimStructureCommand(Guid AnalysisId, Guid ClaimId, Guid ReviewerId, MechanicClaimStructureDto Structure) : ICommand<MechanicClaimDto>`; `MechanicClaimDto` con `MechanicClaimKind Kind, MechanicRulePriority Priority, IReadOnlyList<Guid> Overrides, MechanicTriggerDto? Trigger` in coda. Mappatura errori: `ArgumentException` dal dominio ⇒ `ValidationException`/400 (usa la classe già usata dagli handler del BC: `grep -rn "class .*Exception" apps/api/src/Api/Middleware/Exceptions`), `InvalidOperationException` su stato ⇒ `ConflictException`.

- [ ] **Step 1: Scrivi i test degli handler**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/UpdateMechanicClaimStructureCommandHandlerTests.cs
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class UpdateMechanicClaimStructureCommandHandlerTests
{
    private readonly Mock<IMechanicAnalysisRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly UpdateMechanicClaimStructureCommandHandler _handler;

    public UpdateMechanicClaimStructureCommandHandlerTests()
    {
        _handler = new UpdateMechanicClaimStructureCommandHandler(
            _repo.Object, _uow.Object, Mock.Of<ILogger<UpdateMechanicClaimStructureCommandHandler>>());
    }

    private static MechanicAnalysis InReview(int claims)
    {
        var a = MechanicAnalysis.Create(Guid.NewGuid(), Guid.NewGuid(), "v1.2.0", Guid.NewGuid(), DateTime.UtcNow, "m", "p", 1m);
        for (var i = 0; i < claims; i++)
        {
            var cit = MechanicCitation.Create(Guid.NewGuid(), 1, "q", null, 0);
            a.AddClaim(MechanicClaim.Create(a.Id, MechanicSection.Mechanics, $"c{i}", i, new[] { cit }));
        }
        a.SubmitForReview(Guid.NewGuid(), DateTime.UtcNow);
        return a;
    }

    [Fact]
    public async Task Handle_SetsStructure_AndReturnsDtoWithFields()
    {
        var a = InReview(2);
        _repo.Setup(r => r.GetByIdWithClaimsIgnoringFiltersAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(a);
        var dto = new MechanicClaimStructureDto(MechanicClaimKind.Exception, MechanicRulePriority.Card, new[] { a.Claims[0].Id }, new MechanicTriggerDto("Azione", null, null));

        var result = await _handler.Handle(new UpdateMechanicClaimStructureCommand(a.Id, a.Claims[1].Id, Guid.NewGuid(), dto), CancellationToken.None);

        result.Kind.Should().Be(MechanicClaimKind.Exception);
        result.Priority.Should().Be(MechanicRulePriority.Card);
        result.Overrides.Should().Equal(a.Claims[0].Id);
        result.Trigger!.Phase.Should().Be("azione");
        _repo.Verify(r => r.Update(a), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidGraph_Maps400NotFoundOr409Appropriately()
    {
        var a = InReview(1);
        _repo.Setup(r => r.GetByIdWithClaimsIgnoringFiltersAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(a);
        var self = new MechanicClaimStructureDto(MechanicClaimKind.Exception, MechanicRulePriority.Base, new[] { a.Claims[0].Id }, null);

        var act = () => _handler.Handle(new UpdateMechanicClaimStructureCommand(a.Id, a.Claims[0].Id, Guid.NewGuid(), self), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>(); // 400: sostituisci con la classe 400 del progetto se diversa
    }

    [Fact]
    public async Task Handle_UnknownClaim_404()
    {
        var a = InReview(1);
        _repo.Setup(r => r.GetByIdWithClaimsIgnoringFiltersAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(a);
        var act = () => _handler.Handle(new UpdateMechanicClaimStructureCommand(a.Id, Guid.NewGuid(), Guid.NewGuid(), new MechanicClaimStructureDto(MechanicClaimKind.Rule, MechanicRulePriority.Base, Array.Empty<Guid>(), null)), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
```

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/ApproveMechanicClaimCommandHandlerStructureTests.cs
// stesso setup di ApproveMechanicClaimCommandHandlerTests (copia BuildInReviewAnalysis e SetupRepo)
[Fact]
public async Task Handle_WithStructure_AppliesItBeforeApproving()
{
    var analysis = BuildInReviewAnalysis(2);
    var (general, exc) = (analysis.Claims[0], analysis.Claims[1]);
    SetupRepo(analysis, analysis.Id);
    var structure = new MechanicClaimStructureDto(MechanicClaimKind.Exception, MechanicRulePriority.Card, new[] { general.Id }, null);

    var result = await _handler.Handle(new ApproveMechanicClaimCommand(analysis.Id, exc.Id, Guid.NewGuid(), null, structure), CancellationToken.None);

    result.Status.Should().Be(MechanicClaimStatus.Approved);
    result.Kind.Should().Be(MechanicClaimKind.Exception);
    result.Overrides.Should().Equal(general.Id);
}

[Fact]
public async Task Handle_WithoutStructure_LeavesProposedValues()
{
    var analysis = BuildInReviewAnalysis(1);
    analysis.SetClaimStructure(analysis.Claims[0].Id, new MechanicClaimStructure(MechanicClaimKind.Clarification, MechanicRulePriority.Base, Array.Empty<Guid>(), null));
    SetupRepo(analysis, analysis.Id);

    var result = await _handler.Handle(new ApproveMechanicClaimCommand(analysis.Id, analysis.Claims[0].Id, Guid.NewGuid()), CancellationToken.None);

    result.Kind.Should().Be(MechanicClaimKind.Clarification);
}
```

- [ ] **Step 2: Esegui e verifica che fallisca**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~UpdateMechanicClaimStructureCommandHandlerTests|FullyQualifiedName~ApproveMechanicClaimCommandHandlerStructureTests"`
Expected: FAIL (compilazione: tipi e parametri inesistenti).

- [ ] **Step 3: Implementa DTO e comandi**

```csharp
// BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicClaimStructureDto.cs
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

public sealed record MechanicTriggerDto(string? Phase, string? Action, string? Component)
{
    public MechanicTrigger? ToDomain() => MechanicTrigger.Create(Phase, Action, Component);
    public static MechanicTriggerDto? FromDomain(MechanicTrigger? t) => t is null ? null : new(t.Phase, t.Action, t.Component);
}

public sealed record MechanicClaimStructureDto(
    MechanicClaimKind Kind,
    MechanicRulePriority Priority,
    IReadOnlyList<Guid> Overrides,
    MechanicTriggerDto? Trigger)
{
    public MechanicClaimStructure ToDomain() => new(Kind, Priority, Overrides ?? Array.Empty<Guid>(), Trigger?.ToDomain());
}
```

`MechanicClaimDto`: aggiungi in coda `MechanicClaimKind Kind, MechanicRulePriority Priority, IReadOnlyList<Guid> Overrides, MechanicTriggerDto? Trigger`. Aggiorna ogni costruzione (`ToDto` nell'approve handler e gli altri trovati con grep) con `Kind: claim.Kind, Priority: claim.Priority, Overrides: claim.Overrides, Trigger: MechanicTriggerDto.FromDomain(claim.Trigger)`.

`ApproveMechanicClaimCommand`: aggiungi `MechanicClaimStructureDto? Structure = null`. Nell'handler, prima di `analysis.ApproveClaim(...)`:

```csharp
        if (request.Structure is not null)
        {
            try
            {
                analysis.SetClaimStructure(request.ClaimId, request.Structure.ToDomain());
            }
            catch (ArgumentException ex)
            {
                throw new ValidationException(ex.Message); // classe 400 del progetto
            }
            catch (InvalidOperationException ex)
            {
                throw new ConflictException(ex.Message, ex);
            }
        }
```

Validatore approve: `RuleFor(c => c.Structure!.Overrides).Must(o => o.Distinct().Count() == o.Count).When(c => c.Structure is not null).WithMessage("Overrides must not contain duplicates.");` e `RuleFor(c => c.Structure!.Kind).IsInEnum()`, `RuleFor(c => c.Structure!.Priority).IsInEnum()`.

Nuovo comando + handler (stesso scheletro dell'approve handler: carica con `GetByIdWithClaimsIgnoringFiltersAsync`, 404 su analisi/claim, `SetClaimStructure` con la stessa mappatura delle eccezioni, `Update`, `SaveChangesAsync` con la stessa gestione di `DbUpdateConcurrencyException`, ritorna `ToDto`). Il validatore replica le regole sopra con `Structure` obbligatoria.

Route in `AdminMechanicAnalysesEndpoints.cs`: `ApproveClaimRequest(string? Note, MechanicClaimStructureDto? Structure)` e passa `request?.Structure`; nuova route:

```csharp
        group.MapPut("/{id:guid}/claims/{claimId:guid}/structure", async (
            Guid id, Guid claimId, MechanicClaimStructureDto request, HttpContext httpContext, IMediator mediator, CancellationToken ct) =>
        {
            var session = (SessionStatusDto)httpContext.Items[nameof(SessionStatusDto)]!;
            var reviewerId = session!.Principal!.Subject.Id;
            var response = await mediator.Send(new UpdateMechanicClaimStructureCommand(id, claimId, reviewerId, request), ct).ConfigureAwait(false);
            return Results.Ok(response);
        })
        .WithName("AdminUpdateMechanicClaimStructure")
        .WithSummary("Set Kind/Priority/Overrides/Trigger on a claim (spec 2026-10-08)");
```

- [ ] **Step 4: Esegui i test e il build**

Run: `dotnet build` (zero warning nuovi; attenzione a SonarAnalyzer S1135: nessun `TODO`), poi `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~MechanicClaim|FullyQualifiedName~MechanicAnalyses"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application apps/api/src/Api/Routing/AdminMechanicAnalysesEndpoints.cs apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands
git commit -m "feat(mechanic): struttura v3 nei DTO, approve con override e PUT structure"
```

---

### Task 7: Card `schema_version` 3, DTO pubblicato, renderer ordinato dietro flag

**Files:**
- Modify: `BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicCardContent.cs` (`CurrentSchemaVersion = 3`; snapshot con `kind`, `priority`, `overrides`, `trigger`)
- Modify: `BoundedContexts/SharedGameCatalog/Application/DTOs/PublishedMechanicCardDto.cs` (`PublishedMechanicCardClaimDto` esteso)
- Modify: `.../Queries/MechanicExtractor/GetPublishedMechanicCardByGameQueryHandler.cs:56-66`
- Modify: `Services/FeatureFlagService.cs` (costante `MechanicClaimsV3OrderingKey = "rag.mechanic-claims.v3-ordering"`)
- Modify: `BoundedContexts/KnowledgeBase/Application/Services/MechanicClaimInjection/VerifiedRulesRenderer.cs` (overload con `VerifiedRulesRenderOptions`)
- Modify: `BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionQueryHandler.cs:194-204`, `StreamQaQueryHandler.cs:634-647` (lettura del flag, passaggio opzioni)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicCardContentV3Tests.cs`, `.../Application/Queries/MechanicExtractor/GetPublishedMechanicCardByGameQueryHandlerV3Tests.cs`, `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/MechanicClaimInjection/VerifiedRulesRendererOrderingTests.cs`

**Interfaces:**
- Consumes: Task 2 (campi del claim), Task 6 (`MechanicTriggerDto`).
- Produces: `MechanicCardClaimSnapshot` con `[JsonPropertyName("kind")] string Kind = "Rule"`, `[JsonPropertyName("priority")] string Priority = "Base"`, `[JsonPropertyName("overrides")] IReadOnlyList<Guid> Overrides = []`, `[JsonPropertyName("trigger")] MechanicCardTriggerSnapshot? Trigger = null` (record con `phase/action/component`); `PublishedMechanicCardClaimDto(Guid Id, string Claim, IReadOnlyList<PublishedMechanicCardCitationDto> Citations, MechanicClaimKind Kind = MechanicClaimKind.Rule, MechanicRulePriority Priority = MechanicRulePriority.Base, IReadOnlyList<Guid>? Overrides = null, MechanicTriggerDto? Trigger = null)` (parametri opzionali: i test esistenti del renderer continuano a compilare); `sealed record VerifiedRulesRenderOptions(bool V3Ordering, bool IncludeExamples = false)` con `static Default = new(false)`; `VerifiedRulesRenderer.Render(card, sections, maxClaimsPerSection = 8, VerifiedRulesRenderOptions? options = null)`.

- [ ] **Step 1: Scrivi i test della proiezione e della lettura v2**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicCardContentV3Tests.cs
using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicCardContentV3Tests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FromAnalysis_ProjectsStructure_AndBumpsSchemaVersionTo3()
    {
        var a = MechanicAnalysis.Create(Guid.NewGuid(), Guid.NewGuid(), "v1.2.0", Guid.NewGuid(), Now, "m", "p", 1m);
        var cit = MechanicCitation.Create(Guid.NewGuid(), 4, "q", null, 0);
        var general = MechanicClaim.Create(a.Id, MechanicSection.Phases, "regola", 0, new[] { cit });
        var exc = MechanicClaim.Create(a.Id, MechanicSection.Phases, "eccezione", 1, new[] { MechanicCitation.Create(Guid.NewGuid(), 12, "q2", null, 0) });
        a.AddClaim(general); a.AddClaim(exc);
        a.SetClaimStructure(exc.Id, new MechanicClaimStructure(MechanicClaimKind.Exception, MechanicRulePriority.Card, new[] { general.Id }, new MechanicTrigger("azione", null, "carta fretta")));
        var ctx = new MechanicCardGameContext { SharedGameId = a.SharedGameId, SharedGameName = "G" };

        var content = MechanicCardContent.FromAnalysis(a, ctx, Now);
        var json = content.ToJson();

        content.SchemaVersion.Should().Be(3);
        var snap = content.Claims.Single(c => c.Id == exc.Id);
        snap.Kind.Should().Be("Exception");
        snap.Priority.Should().Be("Card");
        snap.Overrides.Should().Equal(general.Id);
        snap.Trigger!.Phase.Should().Be("azione");
        json.Should().Contain("\"kind\":\"Exception\"").And.Contain("\"trigger\":{\"phase\":\"azione\"");
        // chiavi v2 invariate
        json.Should().Contain("\"schema_version\":3").And.Contain("\"claims\":[").And.Contain("\"citations\":[").And.Contain("\"validations\":[");
    }

    [Fact]
    public void Deserialize_V2Json_WithoutNewKeys_UsesDefaults()
    {
        const string v2 = """{"schema_version":2,"snapshot_at":"2026-07-10T12:00:00Z","source_analysis_id":"00000000-0000-0000-0000-000000000001","source_prompt_version":"v1.1.0","claims":[{"id":"00000000-0000-0000-0000-000000000002","section":"Phases","ordinal":0,"claim":"x","citations":[],"validations":[]}],"metadata":{"shared_game_id":"00000000-0000-0000-0000-000000000003","shared_game_name":"G","publisher":null,"language":"it"}}""";
        var content = JsonSerializer.Deserialize<MechanicCardContent>(v2)!;
        var c = content.Claims.Single();
        c.Kind.Should().Be("Rule");
        c.Priority.Should().Be("Base");
        c.Overrides.Should().BeEmpty();
        c.Trigger.Should().BeNull();
    }
}
```

- [ ] **Step 2: Scrivi il golden test del renderer**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/MechanicClaimInjection/VerifiedRulesRendererOrderingTests.cs
using Api.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.MechanicClaimInjection;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class VerifiedRulesRendererOrderingTests
{
    private static readonly Guid Pdf = Guid.NewGuid();
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid(), D = Guid.NewGuid(), E = Guid.NewGuid();

    private static PublishedMechanicCardClaimDto Claim(Guid id, string text, MechanicClaimKind kind, MechanicRulePriority prio, params Guid[] overrides)
        => new(id, text, new[] { new PublishedMechanicCardCitationDto(Pdf, 1, "q") }, kind, prio, overrides, null);

    private static PublishedMechanicCardDto Card(params PublishedMechanicCardClaimDto[] claims) => new(
        Guid.NewGuid(), Guid.NewGuid(), "T", 1, DateTime.UtcNow, "G", null, "it",
        new[] { new PublishedMechanicCardSectionDto(nameof(MechanicSection.Phases), claims) }, Guid.NewGuid(), null, null);

    // Ordine di arrivo: A (base) , B (card, overrides A), C (base), D (scenario, overrides B), E (example)
    private static PublishedMechanicCardDto Sample() => Card(
        Claim(A, "A generale", MechanicClaimKind.Rule, MechanicRulePriority.Base),
        Claim(B, "B eccezione", MechanicClaimKind.Exception, MechanicRulePriority.Card, A),
        Claim(C, "C generale", MechanicClaimKind.Rule, MechanicRulePriority.Base),
        Claim(D, "D scenario", MechanicClaimKind.Exception, MechanicRulePriority.Scenario, B),
        Claim(E, "E esempio", MechanicClaimKind.Example, MechanicRulePriority.Base));

    [Fact]
    public void FlagOff_OutputIsByteIdenticalToLegacy()
    {
        var legacy = VerifiedRulesRenderer.Render(Sample(), new[] { MechanicSection.Phases });
        var withOptions = VerifiedRulesRenderer.Render(Sample(), new[] { MechanicSection.Phases }, 8, VerifiedRulesRenderOptions.Default);
        withOptions.PromptText.Should().Be(legacy.PromptText);
        legacy.PromptText.Should().Be("[Verified Rules — human-approved]\n## Phases\n[V1] A generale [Page 1]\n[V2] B eccezione [Page 1]\n[V3] C generale [Page 1]\n[V4] D scenario [Page 1]\n[V5] E esempio [Page 1]");
    }

    [Fact]
    public void FlagOn_OrdersByPriorityThenTopologically_ExcludesExamples_AndLabelsExceptions()
    {
        var block = VerifiedRulesRenderer.Render(Sample(), new[] { MechanicSection.Phases }, 8, new VerifiedRulesRenderOptions(V3Ordering: true));
        block.PromptText.Should().Be(
            "[Verified Rules — human-approved]\n## Phases\n" +
            "[V1] D scenario [Page 1] (Eccezione a [V2])\n" +
            "[V2] B eccezione [Page 1] (Eccezione a [V3])\n" +
            "[V3] A generale [Page 1]\n" +
            "[V4] C generale [Page 1]");
        block.Citations.Select(c => c.Marker).Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public void FlagOn_CapAppliesAfterOrdering()
    {
        var block = VerifiedRulesRenderer.Render(Sample(), new[] { MechanicSection.Phases }, 2, new VerifiedRulesRenderOptions(V3Ordering: true));
        block.PromptText.Should().Contain("[V1] D scenario").And.Contain("[V2] B eccezione").And.NotContain("A generale");
    }

    [Fact]
    public void FlagOn_LongChain_IsOrderedOverridingFirst()
    {
        var card = Card(
            Claim(A, "A", MechanicClaimKind.Rule, MechanicRulePriority.Base),
            Claim(B, "B", MechanicClaimKind.Exception, MechanicRulePriority.Base, A),
            Claim(C, "C", MechanicClaimKind.Exception, MechanicRulePriority.Base, B),
            Claim(D, "D", MechanicClaimKind.Exception, MechanicRulePriority.Base, C));
        var block = VerifiedRulesRenderer.Render(card, new[] { MechanicSection.Phases }, 8, new VerifiedRulesRenderOptions(V3Ordering: true));
        block.PromptText.Should().Match("*[V1] D*[V2] C*[V3] B*[V4] A*");
    }

    [Fact]
    public void FlagOn_IncludeExamples_RendersThemLast()
    {
        var block = VerifiedRulesRenderer.Render(Sample(), new[] { MechanicSection.Phases }, 8, new VerifiedRulesRenderOptions(V3Ordering: true, IncludeExamples: true));
        block.PromptText.Should().EndWith("[V5] E esempio [Page 1]");
    }
}
```

- [ ] **Step 3: Esegui e verifica che fallisca**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~MechanicCardContentV3Tests|FullyQualifiedName~VerifiedRulesRendererOrderingTests"`
Expected: FAIL (compilazione).

- [ ] **Step 4: Implementa snapshot v3 e DTO**

In `MechanicCardContent.cs`: `CurrentSchemaVersion = 3; // spec 2026-10-08: kind/priority/overrides/trigger projected (additive keys only, ADR-084 §3)`. In `FromAnalysis` aggiungi al `new MechanicCardClaimSnapshot { … }`:

```csharp
                Kind = c.Kind.ToString(),
                Priority = c.Priority.ToString(),
                Overrides = c.Overrides.ToList(),
                Trigger = c.Trigger is null ? null : new MechanicCardTriggerSnapshot { Phase = c.Trigger.Phase, Action = c.Trigger.Action, Component = c.Trigger.Component }
```

In `MechanicCardClaimSnapshot` aggiungi dopo `Validations`:

```csharp
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "Rule";

    [JsonPropertyName("priority")]
    public string Priority { get; init; } = "Base";

    [JsonPropertyName("overrides")]
    public IReadOnlyList<Guid> Overrides { get; init; } = Array.Empty<Guid>();

    [JsonPropertyName("trigger")]
    public MechanicCardTriggerSnapshot? Trigger { get; init; }
```

e il record:

```csharp
/// <summary>Trigger snapshot (spec 2026-10-08 §5). Null = always applies.</summary>
public sealed record MechanicCardTriggerSnapshot
{
    [JsonPropertyName("phase")] public string? Phase { get; init; }
    [JsonPropertyName("action")] public string? Action { get; init; }
    [JsonPropertyName("component")] public string? Component { get; init; }
}
```

`PublishedMechanicCardClaimDto` con i quattro parametri opzionali in coda (vedi Interfaces). Nel query handler (`:61`): `new PublishedMechanicCardClaimDto(c.Id, c.Claim, cits, Enum.TryParse<MechanicClaimKind>(c.Kind, out var k) ? k : MechanicClaimKind.Rule, Enum.TryParse<MechanicRulePriority>(c.Priority, out var p) ? p : MechanicRulePriority.Base, c.Overrides, c.Trigger is null ? null : new MechanicTriggerDto(c.Trigger.Phase, c.Trigger.Action, c.Trigger.Component))`.

- [ ] **Step 5: Implementa il renderer**

In `VerifiedRulesRenderer.cs`:

```csharp
/// <summary>Rendering options (spec 2026-10-08 §5). Default = legacy behaviour, byte-identical.</summary>
internal sealed record VerifiedRulesRenderOptions(bool V3Ordering, bool IncludeExamples = false)
{
    public static VerifiedRulesRenderOptions Default { get; } = new(false);
}
```

Firma: `public static VerifiedRulesBlock Render(PublishedMechanicCardDto card, IReadOnlyList<MechanicSection> sections, int maxClaimsPerSection = 8, VerifiedRulesRenderOptions? options = null)`. Dentro il ciclo per sezione, sostituisci la presa diretta di `dto.Claims` con:

```csharp
            var ordered = options is { V3Ordering: true } ? OrderV3(dto.Claims, options.IncludeExamples) : dto.Claims;
            var take = Math.Min(maxClaimsPerSection, ordered.Count);
            // assegna i marker PRIMA di scrivere, così "(Eccezione a [Vk])" può riferirsi a un marker della stessa sezione
            var markerOf = new Dictionary<Guid, int>();
            for (var i = 0; i < take; i++) { markerOf[ordered[i].Id] = marker + i + 1; }
            for (var i = 0; i < take; i++)
            {
                var claim = ordered[i];
                marker++;
                sb.Append("\n[V").Append(marker).Append("] ").Append(claim.Claim);
                if (claim.Citations is { Count: > 0 })
                {
                    sb.Append(" [Page ").Append(claim.Citations[0].PdfPage).Append(']');
                    foreach (var cite in claim.Citations)
                    {
                        citations.Add(new VerifiedRuleCitation(marker, cite.PdfId, cite.PdfPage, cite.Quote));
                    }
                }
                if (options is { V3Ordering: true } && claim.Kind == MechanicClaimKind.Exception && claim.Overrides is { Count: > 0 })
                {
                    var refs = claim.Overrides.Where(markerOf.ContainsKey).Select(o => $"[V{markerOf[o]}]").ToList();
                    if (refs.Count > 0) { sb.Append(" (Eccezione a ").Append(string.Join(", ", refs)).Append(')'); }
                }
                if (options is { V3Ordering: true } && claim.Trigger is { } t && (t.Phase ?? t.Action ?? t.Component) is not null)
                {
                    var parts = new[] { t.Phase is null ? null : $"fase {t.Phase}", t.Action is null ? null : $"azione {t.Action}", t.Component is null ? null : $"componente {t.Component}" }.Where(p => p is not null);
                    sb.Append(" (quando: ").Append(string.Join(" / ", parts)).Append(')');
                }
            }
```

e la funzione:

```csharp
    /// <summary>Priority desc, then overriding-before-overridden (DFS post-order reversed), then original order. Examples last or dropped.</summary>
    private static IReadOnlyList<PublishedMechanicCardClaimDto> OrderV3(IReadOnlyList<PublishedMechanicCardClaimDto> claims, bool includeExamples)
    {
        var byId = claims.ToDictionary(c => c.Id);
        var index = claims.Select((c, i) => (c.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var examples = claims.Where(c => c.Kind == MechanicClaimKind.Example).ToList();
        var rules = claims.Where(c => c.Kind != MechanicClaimKind.Example).ToList();

        // rank = longest override chain length ending at this claim (A←B←C ⇒ C rank 2, B 1, A 0); higher renders first
        var rank = new Dictionary<Guid, int>();
        int Rank(Guid id, HashSet<Guid> visiting)
        {
            if (rank.TryGetValue(id, out var r)) { return r; }
            if (!visiting.Add(id)) { return 0; } // cycle guard: the domain forbids cycles, be defensive anyway
            var c = byId[id];
            var best = 0;
            foreach (var o in c.Overrides ?? Array.Empty<Guid>())
            {
                if (byId.ContainsKey(o)) { best = Math.Max(best, Rank(o, visiting) + 1); }
            }
            visiting.Remove(id);
            return rank[id] = best;
        }

        var ordered = rules
            .OrderByDescending(c => (int)c.Priority)
            .ThenByDescending(c => Rank(c.Id, new HashSet<Guid>()))
            .ThenBy(c => index[c.Id])
            .ToList();
        if (includeExamples) { ordered.AddRange(examples.OrderBy(c => index[c.Id])); }
        return ordered;
    }
```

Nota sul test `FlagOn_OrdersByPriorityThenTopologically…`: D (Scenario) precede B (Card) precede A, C (Base, ordine originale). Verifica che l'atteso del test corrisponda: D→`(Eccezione a [V2])` perché B ha marker 2; B→`(Eccezione a [V3])` perché A ha marker 3. Se l'atteso non torna, correggi l'atteso del test: la regola è «priorità decrescente, poi catena più lunga prima, poi ordine originale», non il contrario.

- [ ] **Step 6: Collega il flag nei due handler**

In `FeatureFlagConstants`: `public const string MechanicClaimsV3OrderingKey = "rag.mechanic-claims.v3-ordering"; // spec 2026-10-08 §9, default off`. In `AskQuestionQueryHandler.cs:204` e `StreamQaQueryHandler.cs:647`: leggi `var v3 = await _featureFlags.IsEnabledAsync(FeatureFlagConstants.MechanicClaimsV3OrderingKey, flagRole).ConfigureAwait(false);` (stessa firma dell'altra chiamata al flag nello stesso metodo) e passa `new VerifiedRulesRenderOptions(v3)` come quarto argomento (mantieni `8` come terzo o il valore già usato lì).

- [ ] **Step 7: Esegui test e verifica regressioni del renderer**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~VerifiedRulesRenderer|FullyQualifiedName~MechanicCardContent|FullyQualifiedName~GetPublishedMechanicCard|FullyQualifiedName~AskQuestionQueryHandler|FullyQualifiedName~StreamQaQueryHandler"`
Expected: PASS, inclusi i test legacy del renderer invariati.

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicCardContent.cs apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/DTOs/PublishedMechanicCardDto.cs apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/MechanicExtractor/GetPublishedMechanicCardByGameQueryHandler.cs apps/api/src/Api/Services/FeatureFlagService.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/MechanicClaimInjection/VerifiedRulesRenderer.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/AskQuestionQueryHandler.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/StreamQaQueryHandler.cs apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicCardContentV3Tests.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Services/MechanicClaimInjection/VerifiedRulesRendererOrderingTests.cs
git commit -m "feat(rag): card schema v3 e ordinamento Verified Rules dietro flag"
```

---

### Task 8: Comando di riestrazione per gioco

**Files:**
- Create: `.../Commands/MechanicExtractor/RequeueMechanicAnalysisForPromptVersionCommand.cs`, `…Handler.cs`, `…Validator.cs`
- Modify: `Routing/AdminMechanicAnalysesEndpoints.cs` (route `POST /admin/mechanic-analyses/requeue/{sharedGameId:guid}`)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/RequeueMechanicAnalysisForPromptVersionCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `GenerateMechanicAnalysisCommand` esistente (leggi la sua firma: `grep -n "record GenerateMechanicAnalysisCommand" -A8`), `IMechanicCardRepository.GetActiveByGameAsync` (usato dal query handler della card), `IMechanicPromptProvider.PromptVersion`.
- Produces: `internal record RequeueMechanicAnalysisForPromptVersionCommand(Guid SharedGameId, Guid ActorId) : ICommand<MechanicAnalysisGenerationResponseDto>` (lo stesso DTO di risposta di Generate). Comportamento: carica la card attiva del gioco; se assente ⇒ `NotFoundException("MechanicCard")`; ricava `PdfDocumentId` dal primo claim del contenuto (come il query handler); se esiste già un'analisi `(SharedGameId, Pdf, PromptVersion corrente)` non terminale ⇒ `ConflictException`; altrimenti invia `GenerateMechanicAnalysisCommand` via `IMediator` con quel PDF e ritorna la risposta.

- [ ] **Step 1: Scrivi il test**

```csharp
[Fact]
public async Task Handle_WithActiveCard_SendsGenerateForSamePdf()
{
    // Arrange: mock IMechanicCardRepository.GetActiveByGameAsync → card con Content v3 che ha un claim con citazione pdf_id=P
    // mock IMediator.Send(It.Is<GenerateMechanicAnalysisCommand>(c => c.SharedGameId == G && c.PdfDocumentId == P)) → response
    // Act: handler.Handle(new RequeueMechanicAnalysisForPromptVersionCommand(G, actor))
    // Assert: mediator.Verify(... Times.Once); result è la response
}

[Fact]
public async Task Handle_WithoutCard_Throws404() { /* GetActiveByGameAsync → null ⇒ NotFoundException */ }
```

Completa i due test con lo stesso stile Moq di `ApproveMechanicClaimCommandHandlerTests` (costruisci il JSON `Content` con `MechanicCardContent.FromAnalysis` su un'analisi pubblicata minima).

- [ ] **Step 2: Esegui e verifica che fallisca** — `dotnet test … --filter "FullyQualifiedName~RequeueMechanicAnalysis"` → FAIL.

- [ ] **Step 3: Implementa comando, handler, validatore, route** seguendo lo scheletro di `ApproveMechanicClaimCommandHandler` (DI: `IMechanicCardRepository`, `IMechanicAnalysisRepository`, `IMediator`, `IMechanicPromptProvider`, `ILogger<>`). Il job "riestrai tutti" **non** è in questo piano: l'admin lancia il comando per gioco (spec §7 lascia il job come comando iterato; aggiungilo solo se `GenerateMechanicAnalysisCommand` non ha già un limite di concorrenza, altrimenti è un ciclo lato client).

- [ ] **Step 4: Esegui e verifica che passi** → PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/RequeueMechanicAnalysisForPromptVersionCommand*.cs apps/api/src/Api/Routing/AdminMechanicAnalysesEndpoints.cs apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/RequeueMechanicAnalysisForPromptVersionCommandHandlerTests.cs
git commit -m "feat(mechanic): riestrazione per gioco con il prompt corrente"
```

---

### Task 9: Web admin: schema, client, badge e campi modificabili

**Files:**
- Modify: `apps/web/src/lib/api/schemas/mechanic-analyses.schemas.ts` (`MechanicClaimDtoSchema` + nuovi schema)
- Modify: `apps/web/src/lib/api/clients/admin/adminContentClient.ts:577` (`approveMechanicClaim(analysisId, claimId, note?, structure?)`, nuovo `updateMechanicClaimStructure`)
- Create: `apps/web/src/components/admin/mechanic-extractor/claims/ClaimStructureFields.tsx`
- Modify: `apps/web/src/components/admin/mechanic-extractor/claims/ApproveClaimDialog.tsx` (prop `claim`, `siblings`, `onConfirm(note, structure)`), `ClaimsSection.tsx` (badge in `ClaimRow`, wiring del dialogo, mutation)
- Test: `apps/web/src/components/admin/mechanic-extractor/claims/__tests__/ClaimStructureFields.test.tsx`, aggiornare `ApproveClaimDialog` test esistente se presente (`ls apps/web/src/components/admin/mechanic-extractor/claims/__tests__`)

**Interfaces:**
- Consumes: Task 6 (contratto JSON: `kind`/`priority` come **stringhe enum** se l'API serializza gli enum come stringhe, altrimenti numeri: verifica con `curl` o con il test di contratto esistente degli enum `MechanicSectionSchema`/`MechanicClaimStatusSchema` in questo stesso file e replica la convenzione).
- Produces: `MechanicClaimKindSchema`, `MechanicRulePrioritySchema` (stessa convenzione di `MechanicSectionSchema`), `MechanicTriggerDtoSchema = z.object({ phase: z.string().nullable(), action: z.string().nullable(), component: z.string().nullable() })`, `MechanicClaimStructureDto = { kind, priority, overrides: string[], trigger: MechanicTriggerDto | null }`; componente `ClaimStructureFields({ value, onChange, siblings: {id, text}[] })`.

- [ ] **Step 1: Scrivi il test del componente**

```tsx
// apps/web/src/components/admin/mechanic-extractor/claims/__tests__/ClaimStructureFields.test.tsx
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { ClaimStructureFields } from '../ClaimStructureFields';

describe('ClaimStructureFields', () => {
  const siblings = [
    { id: 'a', text: 'Regola generale' },
    { id: 'b', text: 'Altra regola' },
  ];

  it('renders proposed values and emits changes', () => {
    const onChange = vi.fn();
    render(
      <ClaimStructureFields
        value={{ kind: 'Exception', priority: 'Card', overrides: ['a'], trigger: { phase: 'azione', action: null, component: null } }}
        onChange={onChange}
        siblings={siblings}
      />
    );
    expect(screen.getByLabelText('Kind')).toHaveValue('Exception');
    expect(screen.getByLabelText('Priority')).toHaveValue('Card');
    expect(screen.getByLabelText('Overrides: Regola generale')).toBeChecked();
    fireEvent.change(screen.getByLabelText('Trigger phase'), { target: { value: 'fine turno' } });
    expect(onChange).toHaveBeenLastCalledWith(
      expect.objectContaining({ trigger: expect.objectContaining({ phase: 'fine turno' }) })
    );
  });

  it('unchecking an override removes it', () => {
    const onChange = vi.fn();
    render(
      <ClaimStructureFields
        value={{ kind: 'Exception', priority: 'Card', overrides: ['a'], trigger: null }}
        onChange={onChange}
        siblings={siblings}
      />
    );
    fireEvent.click(screen.getByLabelText('Overrides: Regola generale'));
    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ overrides: [] }));
  });
});
```

- [ ] **Step 2: Esegui e verifica che fallisca** — `cd apps/web && pnpm vitest run src/components/admin/mechanic-extractor/claims/__tests__/ClaimStructureFields.test.tsx` → FAIL (modulo assente).

- [ ] **Step 3: Implementa schema, client, componente**

Schema (accanto a `MechanicClaimValidationDtoSchema`):

```ts
export const MechanicClaimKindSchema = z.enum(['Rule', 'Exception', 'Clarification', 'Example']);
export const MechanicRulePrioritySchema = z.enum(['Base', 'Expansion', 'Card', 'Scenario']);
export const MechanicTriggerDtoSchema = z.object({
  phase: z.string().nullable(),
  action: z.string().nullable(),
  component: z.string().nullable(),
});
export const MechanicClaimStructureDtoSchema = z.object({
  kind: MechanicClaimKindSchema,
  priority: MechanicRulePrioritySchema,
  overrides: z.array(z.string().uuid()),
  trigger: MechanicTriggerDtoSchema.nullable(),
});
export type MechanicClaimStructureDto = z.infer<typeof MechanicClaimStructureDtoSchema>;
```

Se gli enum dell'API arrivano come numeri (convenzione di `MechanicSectionSchema`), usa `z.union([z.number(), z.enum([...])])` con un `transform` verso la stringa, come fa già quel file. In `MechanicClaimDtoSchema` aggiungi `kind: MechanicClaimKindSchema.default('Rule'), priority: MechanicRulePrioritySchema.default('Base'), overrides: z.array(z.string().uuid()).default([]), trigger: MechanicTriggerDtoSchema.nullable().default(null)`.

Client:

```ts
    async approveMechanicClaim(analysisId: string, claimId: string, note?: string, structure?: MechanicClaimStructureDto): Promise<MechanicClaimDto> {
      const body: Record<string, unknown> = {};
      if (note !== undefined) body.note = note;
      if (structure !== undefined) body.structure = structure;
      const result = await http.post(MECHANIC_ANALYSES_ROUTES.approveClaim(analysisId, claimId), body, MechanicClaimDtoSchema);
      if (!result) throw new Error('Failed to approve claim');
      return result;
    },
    async updateMechanicClaimStructure(analysisId: string, claimId: string, structure: MechanicClaimStructureDto): Promise<MechanicClaimDto> {
      const result = await http.put(MECHANIC_ANALYSES_ROUTES.claimStructure(analysisId, claimId), structure, MechanicClaimDtoSchema);
      if (!result) throw new Error('Failed to update claim structure');
      return result;
    },
```

Aggiungi `claimStructure: (a, c) => \`…/admin/mechanic-analyses/${a}/claims/${c}/structure\`` accanto ad `approveClaim` nelle route.

Componente `ClaimStructureFields.tsx`: due `<select>` con `<label htmlFor>` «Kind» e «Priority», una lista di checkbox `aria-label={\`Overrides: ${s.text}\`}` per ogni sibling (escluso il claim stesso: il chiamante non lo passa), tre `<input>` con label «Trigger phase/action/component» che emettono `trigger: null` quando tutti vuoti. Usa i primitivi `@/components/ui/...` già usati nel file (`Badge`) e i token semantici (`bg-card`, `border-border`, `text-muted-foreground`); nessun `bg-white`/`text-gray-*` (ESLint `local/no-hardcoded-color-utility` è error).

`ApproveClaimDialog`: nuove prop `claim?: MechanicClaimDto`, `siblings: {id,text}[]`; stato `structure` inizializzato da `claim` all'apertura; `onConfirm(note, structure)`. `ClaimsSection`: `approveMutation.mutate({ claimId, note, structure })` → `adminClient.approveMechanicClaim(analysisId, claimId, note || undefined, structure)`; passa `siblings` = claim della stessa analisi tranne il target; in `ClaimRow` aggiungi dopo il testo: `<Badge variant="outline">{claim.kind}</Badge> <Badge variant="outline">{claim.priority}</Badge>`, `{claim.trigger && <span className="text-xs text-muted-foreground">quando: …</span>}`, `{claim.overrides.length > 0 && <span className="text-xs">sovrascrive {claim.overrides.length} claim</span>}` con `data-testid={\`claim-structure-${claim.id}\`}`. Le violazioni T5 compaiono già via `ValidationBadges` (sono `validations` con rule `T5*`).

- [ ] **Step 4: Esegui test, typecheck, lint**

Run: `cd apps/web && pnpm vitest run src/components/admin/mechanic-extractor && pnpm typecheck && pnpm lint`
Expected: PASS senza errori. Se `pnpm typecheck` fallisce su `.next/types` stale: `rm -rf .next` e rilancia (memoria `precommit-typecheck-next-types-stale`).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/api/schemas/mechanic-analyses.schemas.ts apps/web/src/lib/api/clients/admin/adminContentClient.ts apps/web/src/components/admin/mechanic-extractor/claims
git commit -m "feat(admin): kind/priority/overrides/trigger nella review dei claim"
```

---

### Task 10: Verifica end-to-end locale e chiusura

**Files:**
- Modify: `docs/for-claude/architecture/adr/adr-088-mechanic-cards-as-rag-retrieval-source.md` (nota «v3 ordering» nella sezione D3), `CLAUDE.md` nessuna modifica (nessuna regola nuova).

- [ ] **Step 1: Suite completa SharedGameCatalog + KnowledgeBase unit**

Run: `tasklist | grep testhost` (kill se presente) e `dotnet test ../../tests/Api.Tests --filter "Category=Unit&(BoundedContext=SharedGameCatalog|BoundedContext=KnowledgeBase)"`
Expected: zero fallimenti; conteggio dei test ≥ baseline + i nuovi (riporta i tre conteggi Passed/Failed/Skipped nel PR).

- [ ] **Step 2: Gate di architettura**

Run: `dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~ArchitectureTests"`
Expected: PASS (in particolare `SkipReasonClassArchitectureTests`, `TestCategoryGateArchitectureTests`, `MechanicSectionRangeConstraintTests`).

- [ ] **Step 3: Prova manuale su stack locale**

`cd infra && make dev-core`; `dotnet ef database update`; da `/admin` lancia la riestrazione su un gioco con card pubblicata; verifica nella review i badge e le violazioni T5; approva con una struttura; pubblica; accendi `rag.mechanic-claims.v3-ordering` da `/admin/config`; fai una domanda al gioco e controlla nel log del prompt il blocco `[Verified Rules]` ordinato. Salva l'output del blocco prima/dopo in un commento del PR.

- [ ] **Step 4: Aggiorna ADR-088 e apri la PR**

Aggiungi a ADR-088 §D3 una riga: «Dal 2026-10 i claim portano Kind/Priority/Overrides/Trigger (spec `2026-10-08-mechanic-claims-v3-defeasible-rules-design.md`); l'ordinamento è dietro `rag.mechanic-claims.v3-ordering`». Commit `docs(adr): ADR-088 nota ordinamento v3`. Push e PR verso `main-dev` con `--body-file` (mai backtick in `--body`), DoD = Review Focus 1–5 verificati, nessun totale volatile nel corpo.

---

## Self-Review (eseguita dall'autore del piano)

1. **Copertura della spec**: §2 → Task 1–3; §3 → Task 4–5; §4 → Task 6, 9; §5 → Task 7; §6 → piano gemello; §7 → Task 8; §8 → test in ogni task; §9 → Task 7 (flag) e Task 10. Il job "riestrai tutti" della spec §7 è ridotto al comando per gioco: dichiarato in Task 8.
2. **Segnaposto**: nessun TBD; Task 8 Step 1 lascia i test in forma di scheletro commentato con istruzioni precise di completamento (stile Moq già mostrato in Task 6).
3. **Coerenza dei tipi**: `MechanicClaimStructure`, `MechanicTrigger`, `MechanicClaimStructureDto`, `MechanicTriggerDto`, `VerifiedRulesRenderOptions`, `SetClaimStructure`, `ApplyStructure`, `ReadStructure` usati con la stessa firma in tutti i task.
4. **Review Focus**: 1 → Task 2 (`SetClaimStructure_RejectsOverrideOutsideAnalysis`); 2 → Task 2 (`RejectsLongCycle`) + Task 7 (`FlagOn_LongChain…`); 3 → Task 7 (`Deserialize_V2Json…`); 4 → Task 4 (`OutOfRangeOrSelfOrdinal…`) + Task 5 (`OverrideOutOfRangeOrSelf…`); 5 → Task 7 (`FlagOff_OutputIsByteIdenticalToLegacy`).

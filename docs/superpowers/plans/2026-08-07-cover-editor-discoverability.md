# Cover admin: scopribilità e ritaglio per contesto — Piano di implementazione

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rendere l'editor cover trovabile e utilizzabile da un admin, e far sì che il punto focale scelto (o dedotto) cambi davvero l'inquadratura di Hero e Social.

**Architecture:** Il punto focale entra in `CoverUrlResolver.ResolvedCover` e viaggia fino al DTO. Card e Hero lo applicano via `object-position` (nessun file generato); il solo contesto Social produce un WebP 1200×630 su R2 al salvataggio dell'assegnazione. L'affordance smette di nascondersi e si rinforza sulle cover in placeholder; il CTA della vista cover-gap apre direttamente il dialog.

**Tech Stack:** .NET 9 (Minimal API + MediatR, xUnit + Moq + FluentAssertions), Next.js 16 / React 19 (Vitest + Testing Library), Magick.NET via `IWebpVariantGenerator`, S3/R2 via `IBlobStorageService`.

**Spec:** [`docs/superpowers/specs/2026-08-07-cover-editor-discoverability-design.md`](../specs/2026-08-07-cover-editor-discoverability-design.md)

## Global Constraints

- **Nessuna migration e nessun backfill.** Il default è calcolato a ogni risoluzione, non persistito.
- **Invariante #2123**: esattamente un evento `CoverResolution` per chiamata al resolver. Non aggiungere né spostare punti di emissione.
- **Contratto `MeepleCard`**: senza la nuova prop il rendering deve restare identico. Nessuno stile emesso quando la prop è assente.
- **A11y AA è bloccante**: l'attenuazione dell'affordance non deve usare `opacity` sull'elemento (abbassa il contrasto di testo e bordo sotto 4.5:1). Usare una coppia di colori semantici.
- **Token di colore**: solo semantici (`bg-background`, `text-foreground`, `border-border`…). ESLint `local/no-hardcoded-color-utility` è **error** su `bg-white`, `text-gray-*` e affini.
- **Niente `// TODO`** nel C#: SonarAnalyzer S1135 fa fallire la build. Usare `// Follow-up:`.
- **Commit**: `feat|fix|docs|refactor|test|chore(scope): descrizione`, subject ≤ 72 caratteri.
- **PR**: verso `main-dev` (parent già configurato sul branch).
- **Valori del default**: `Pdf → (0.5, 0.2)`, ogni altro kind → `(0.5, 0.5)`.

---

### Task 1: Il punto focale entra nel resolver

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/CoverUrlResolver.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/CoverUrlResolverTests.cs`

**Interfaces:**
- Produces: `CoverUrlResolver.ResolvedCover(string? Url, CoverKind? Kind, double FocalX, double FocalY)` — record struct. `CoverUrlResolver.DefaultFocalFor(CoverKind kind)` → `(double X, double Y)`, `internal static`.

`ResolvedCover` è oggi `(string? Url, CoverKind? Kind)`. I due campi nuovi vanno **in coda** con default, così i costruttori posizionali esistenti continuano a compilare.

- [ ] **Step 1: Scrivi il test che fallisce**

In `CoverUrlResolverTests.cs`, in fondo alla classe:

```csharp
    [Theory]
    [InlineData(CoverKind.Pdf, 0.5, 0.2)]
    [InlineData(CoverKind.Bgg, 0.5, 0.5)]
    [InlineData(CoverKind.Wikidata, 0.5, 0.5)]
    [InlineData(CoverKind.Manual, 0.5, 0.5)]
    [InlineData(CoverKind.User, 0.5, 0.5)]
    public void DefaultFocalFor_AnchorsPdfHigh_CentersEveryOtherSource(
        CoverKind kind, double expectedX, double expectedY)
    {
        var focal = CoverUrlResolver.DefaultFocalFor(kind);

        focal.X.Should().Be(expectedX);
        focal.Y.Should().Be(expectedY);
    }

    [Fact]
    public async Task ResolvePublicWithSourceAsync_PdfCover_CarriesTheHighAnchorDefault()
    {
        var sg = new SharedGameEntity { PdfCoverR2Key = "pdf-key" };
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", null))
             .ReturnsAsync("https://r2/pdf.webp");

        var cover = await CoverUrlResolver.ResolvePublicWithSourceAsync(sg, _blob.Object);

        cover.Kind.Should().Be(CoverKind.Pdf);
        cover.FocalY.Should().Be(0.2);
    }

    [Fact]
    public async Task ResolveForContextWithSourceAsync_AssignmentFocal_BeatsTheHeuristic()
    {
        var sg = new SharedGameEntity { PdfCoverR2Key = "pdf-key" };
        sg.CoverAssignments.Add(new GameCoverAssignmentEntity
        {
            Context = CoverContext.Hero,
            Source = CoverAssignmentSource.Pdf,
            FocalX = 0.4,
            FocalY = 0.75,
        });
        _blob.Setup(b => b.GetPresignedUrlForRawKeyAsync("pdf-key-preview.webp", null))
             .ReturnsAsync("https://r2/pdf.webp");

        var cover = await CoverUrlResolver
            .ResolveForContextWithSourceAsync(sg, CoverContext.Hero, _blob.Object);

        cover.FocalX.Should().Be(0.4);
        cover.FocalY.Should().Be(0.75);
    }

    [Fact]
    public async Task ResolvePublicWithSourceAsync_NoCover_EmitsPlaceholderOnceAndCentersFocal()
    {
        using var capture = new CoverMetricsCapture();
        var sg = new SharedGameEntity();

        var cover = await CoverUrlResolver.ResolvePublicWithSourceAsync(sg, _blob.Object);

        cover.Url.Should().BeNull();
        cover.FocalX.Should().Be(0.5);
        cover.FocalY.Should().Be(0.5);
        capture.LongMeasurements.Should().ContainSingle()
            .Which.Tags["source"].Should().Be("placeholder");
    }
```

Se `SharedGameEntity.CoverAssignments` non è inizializzata di default, sostituisci l'`Add` con
`sg.CoverAssignments = new List<GameCoverAssignmentEntity> { … }` — controlla l'entità prima di scrivere il test.

- [ ] **Step 2: Esegui il test e verifica che fallisca**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~CoverUrlResolverTests"`
Expected: FAIL in compilazione — `DefaultFocalFor` non esiste e `ResolvedCover` non ha `FocalX`.

- [ ] **Step 3: Estendi il record e aggiungi l'euristica**

In `CoverUrlResolver.cs`, sostituisci la dichiarazione di `ResolvedCover`:

```csharp
    internal readonly record struct ResolvedCover(
        string? Url,
        CoverKind? Kind,
        double FocalX = 0.5,
        double FocalY = 0.5);

    /// <summary>
    /// Punto focale di default quando nessuna assegnazione admin lo fissa (#3611).
    /// Le cover derivate da PDF portano titolo e illustrazione in alto e corpo del
    /// testo al centro, quindi un crop centrato produce una banda di testo; le cover
    /// d'artwork (BGG/Wikidata/Manual) hanno il soggetto al centro e vanno lasciate lì.
    /// Funzione pura: nessuna riga scritta, nessun backfill, il valore si corregge
    /// cambiando questa costante.
    /// </summary>
    internal static (double X, double Y) DefaultFocalFor(CoverKind kind) =>
        kind == CoverKind.Pdf ? (0.5, 0.2) : (0.5, 0.5);
```

- [ ] **Step 4: Popola il focal nei rami di `ResolvePublicWithSourceAsync`**

Ogni `return new ResolvedCover(url, CoverKind.X);` diventa portatore del default. Per il ramo PDF:

```csharp
            if (url is not null)
            {
                EmitResolution("r2_pdf");
                var focal = DefaultFocalFor(CoverKind.Pdf);
                return new ResolvedCover(url, CoverKind.Pdf, focal.X, focal.Y);
            }
```

Applica lo stesso schema ai rami `r2_bgg` e `r2_wikidata` con il loro kind. Il ramo terminale resta
`return new ResolvedCover(null, null);` — i default del record sono già `(0.5, 0.5)`.

- [ ] **Step 5: Fai vincere il focal dell'assegnazione**

In `ResolveForContextWithSourceAsync`, nel ramo dell'override:

```csharp
            if (overrideUrl is not null)
            {
                EmitResolution(SourceTagFor(assignment.Source));
                return new ResolvedCover(
                    overrideUrl,
                    assignment.Source.ToCoverKind(),
                    assignment.FocalX,
                    assignment.FocalY);
            }
```

- [ ] **Step 6: Esegui i test e verifica che passino**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~CoverUrlResolverTests"`
Expected: PASS, inclusi i test preesistenti (l'invariante metrica non è stata toccata).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/CoverUrlResolver.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/CoverUrlResolverTests.cs
git commit -m "feat(catalog): punto focale nel resolver cover (#3611)"
```

---

### Task 2: I DTO espongono il punto focale

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/SharedGameDto.cs` (record `SharedGameDto` ~riga 60, record `SharedGameDetailDto` ~riga 256)
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetSharedGameByIdQueryHandler.cs:406-469`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/SearchSharedGamesQueryHandler.cs:430`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetFilteredSharedGamesQueryHandler.cs:120-155`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetPendingApprovalGamesQueryHandler.cs:71-106`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetAllSharedGamesQueryHandler.cs:84-119`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Queries/GetSharedGameByIdQueryHandlerCoverFocalTests.cs` (nuovo)

**Interfaces:**
- Consumes: `ResolvedCover.FocalX` / `.FocalY` dal Task 1.
- Produces: `SharedGameDto.CoverFocalX`, `SharedGameDto.CoverFocalY`, `SharedGameDetailDto.CoverFocalX`, `SharedGameDetailDto.CoverFocalY` — tutti `double`, default `0.5`.

- [ ] **Step 1: Scrivi il test che fallisce**

Crea `GetSharedGameByIdQueryHandlerCoverFocalTests.cs`. Copia il setup (fixture, mock, costruzione della query) da `GetSharedGameByIdQueryHandlerTests.cs` che già esiste nella stessa cartella — non reinventarlo — e aggiungi il caso:

```csharp
    [Fact]
    public async Task Handle_PdfCover_ExposesTheHighAnchorFocalOnTheDetailDto()
    {
        // Arrange: gioco con la sola cover da PDF, nessuna assegnazione admin.
        // (Riusa l'helper di seed della classe di test esistente.)
        var dto = await _handler.Handle(new GetSharedGameByIdQuery(gameId), CancellationToken.None);

        dto!.CoverFocalX.Should().Be(0.5);
        dto.CoverFocalY.Should().Be(0.2);
    }
```

- [ ] **Step 2: Esegui il test e verifica che fallisca**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~GetSharedGameByIdQueryHandlerCoverFocalTests"`
Expected: FAIL in compilazione — `CoverFocalX` non esiste sul DTO.

- [ ] **Step 3: Aggiungi i campi ai due record**

In `SharedGameDto.cs`, in coda ai parametri di `SharedGameDto` (dopo `CoverSourceUrl`):

```csharp
    // Issue #3611 — punto focale del crop, in [0,1]. Deriva dall'assegnazione admin quando
    // esiste, altrimenti dall'euristica per sorgente (CoverUrlResolver.DefaultFocalFor).
    // Il FE lo traduce in object-position; assente dalle voci di cache pre-#3611, che si
    // deserializzano al centro e mantengono il comportamento precedente fino alla scadenza.
    double CoverFocalX = 0.5,
    double CoverFocalY = 0.5,
```

Ripeti identicamente in coda a `SharedGameDetailDto` (dopo `SocialCoverUrl`).

- [ ] **Step 4: Propaga nei query handler**

In `GetSharedGameByIdQueryHandler.cs` dichiara le due variabili accanto a `coverUrl`:

```csharp
        double coverFocalX = 0.5, coverFocalY = 0.5;
```

subito dopo la risoluzione Hero, assegna:

```csharp
            coverFocalX = cover.FocalX;
            coverFocalY = cover.FocalY;
```

e passale al costruttore, dopo `SocialCoverUrl: socialCoverUrl`:

```csharp
            CoverFocalX: coverFocalX,
            CoverFocalY: coverFocalY);
```

Negli altri quattro handler di lista il pattern è identico: dove oggi c'è `CoverUrl: cover.Url,`
aggiungi subito sotto `CoverFocalX: cover.FocalX, CoverFocalY: cover.FocalY,`.
`GetUserLibraryQueryHandler.cs:190` usa `ResolveForUserAsync`, che restituisce `string?`: **non
toccarlo**, la libreria personale non è nello scope.

- [ ] **Step 5: Esegui i test e verifica che passino**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~SharedGameCatalog"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog
git commit -m "feat(catalog): esponi il punto focale cover sui DTO (#3611)"
```

---

### Task 3: Schema Zod e tipi del frontend

**Files:**
- Modify: `apps/web/src/lib/api/schemas/shared-games.schemas.ts:230` (lista) e `:325` (dettaglio)
- Test: `apps/web/src/lib/api/schemas/__tests__/shared-games.schemas.test.ts` (se il file non esiste, crealo accanto agli altri test di schema)

**Interfaces:**
- Produces: campi `coverFocalX?: number`, `coverFocalY?: number` sui tipi inferiti dei due schemi.

I campi sono `.optional()` perché una risposta servita dalla cache pre-deploy non li conterrà.

- [ ] **Step 1: Scrivi il test che fallisce**

```typescript
  it('accetta il punto focale della cover quando presente (#3611)', () => {
    const parsed = SharedGameDetailDtoSchema.parse({ ...validDetailFixture, coverFocalX: 0.5, coverFocalY: 0.2 });
    expect(parsed.coverFocalY).toBe(0.2);
  });

  it('resta valido quando il punto focale è assente (risposta da cache pre-#3611)', () => {
    const parsed = SharedGameDetailDtoSchema.parse(validDetailFixture);
    expect(parsed.coverFocalY).toBeUndefined();
  });
```

Usa la fixture già presente nel file di test; se non c'è, costruiscila dal minimo richiesto dallo schema.

- [ ] **Step 2: Esegui il test e verifica che fallisca**

Run: `cd apps/web && pnpm test src/lib/api/schemas`
Expected: FAIL — `coverFocalY` viene rimosso dal parse.

- [ ] **Step 3: Aggiungi i campi allo schema**

Accanto a `coverUrl` in **entrambi** gli schemi:

```typescript
  // #3611 — punto focale del crop in [0,1]; il FE lo traduce in object-position.
  // Optional: le risposte servite dalla cache anteriore al deploy non lo contengono.
  coverFocalX: z.number().min(0).max(1).optional(),
  coverFocalY: z.number().min(0).max(1).optional(),
```

- [ ] **Step 4: Esegui il test e verifica che passi**

Run: `cd apps/web && pnpm test src/lib/api/schemas && pnpm typecheck`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/api/schemas
git commit -m "feat(catalog): schema zod per il punto focale cover (#3611)"
```

---

### Task 4: `object-position` nella catena MeepleCard

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/Cover.tsx:11-28` (props) e `:70-77` (img)
- Modify: `apps/web/src/components/ui/data-display/meeple-card/types.ts` (accanto a `coverEditSlot`, ~riga 165)
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx` (passaggio a `Cover`)
- Modify: `apps/web/src/components/ui/shared-games/meeple-card-game.tsx:63,81,124`
- Test: `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/Cover.focal.test.tsx` (nuovo)

**Interfaces:**
- Produces: prop `coverFocal?: { x: number; y: number }` su `MeepleCardProps`, `MeepleCardGameProps` e `CoverProps`.

**Vincolo non negoziabile**: senza la prop, `<img>` non deve avere l'attributo `style`.

- [ ] **Step 1: Scrivi il test che fallisce**

```tsx
/**
 * @vitest-environment jsdom
 */
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { Cover } from '../Cover';

describe('Cover — punto focale (#3611)', () => {
  it('non emette alcuno stile inline quando coverFocal è assente (contratto invariato)', () => {
    render(<Cover entity="game" variant="grid" imageUrl="https://r2.example/c.webp" alt="c" />);
    expect(screen.getByRole('img')).not.toHaveAttribute('style');
  });

  it('traduce il punto focale in object-position', () => {
    render(
      <Cover
        entity="game"
        variant="hero"
        imageUrl="https://r2.example/c.webp"
        alt="c"
        coverFocal={{ x: 0.5, y: 0.2 }}
      />
    );
    expect(screen.getByRole('img')).toHaveStyle({ objectPosition: '50% 20%' });
  });
});
```

- [ ] **Step 2: Esegui il test e verifica che fallisca**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card/parts`
Expected: FAIL — la prop non esiste, il secondo test non trova `object-position`.

- [ ] **Step 3: Implementa in `Cover.tsx`**

Aggiungi alla `CoverProps`:

```tsx
  /**
   * #3611 — punto focale del crop, componenti in [0,1]. Quando è assente NON viene
   * emesso alcuno stile: il rendering resta byte-identico al comportamento precedente.
   */
  coverFocal?: { x: number; y: number };
```

estendi la destrutturazione a `{ entity, variant, imageUrl, alt, coverEmoji, coverFocal }` e sull'`<img>` (riga 70-77) aggiungi:

```tsx
          style={
            coverFocal
              ? { objectPosition: `${coverFocal.x * 100}% ${coverFocal.y * 100}%` }
              : undefined
          }
```

- [ ] **Step 4: Propaga lungo la catena**

In `types.ts`, accanto a `coverEditSlot?: ReactNode;`:

```tsx
  /** #3611 — punto focale del crop, inoltrato a `Cover`. Assente = comportamento invariato. */
  coverFocal?: { x: number; y: number };
```

In `GridCard.tsx` destruttura `coverFocal` dalle props e passalo a `<Cover … coverFocal={coverFocal} />`.
In `meeple-card-game.tsx` aggiungi `readonly coverFocal?: { x: number; y: number };` alle props (accanto alla riga 63), destrutturala (riga 81) e inoltrala (riga 124).

- [ ] **Step 5: Esegui i test e verifica che passino**

Run: `cd apps/web && pnpm test src/components/ui/data-display/meeple-card && pnpm typecheck`
Expected: PASS, incluso `GridCard.coverEditSlot.test.tsx` che protegge il contratto.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card apps/web/src/components/ui/shared-games/meeple-card-game.tsx
git commit -m "feat(catalog): object-position sulle cover MeepleCard (#3611)"
```

---

### Task 5: `object-position` sull'hero del dettaglio

**Files:**
- Modify: `apps/web/src/components/ui/detail-layout/hero.tsx:213-215` (+ props)
- Modify: `apps/web/src/app/(public)/shared-games/[id]/page-client.tsx:342-344`
- Test: `apps/web/src/components/ui/detail-layout/__tests__/hero.focal.test.tsx` (nuovo)

**Interfaces:**
- Consumes: `coverFocalX` / `coverFocalY` dal DTO (Task 3).
- Produces: prop `coverFocal?: { x: number; y: number }` su `Hero`.

- [ ] **Step 1: Scrivi il test che fallisce**

```tsx
/**
 * @vitest-environment jsdom
 */
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { Hero } from '../hero';

describe('Hero — punto focale (#3611)', () => {
  it('inquadra la parte alta quando il focal lo richiede', () => {
    render(<Hero title="Catan" coverUrl="https://r2.example/c.webp" coverFocal={{ x: 0.5, y: 0.2 }} />);
    expect(screen.getByRole('img', { hidden: true })).toHaveStyle({ objectPosition: '50% 20%' });
  });

  it('non emette stile inline senza focal', () => {
    render(<Hero title="Catan" coverUrl="https://r2.example/c.webp" />);
    expect(screen.getByRole('img', { hidden: true })).not.toHaveAttribute('style');
  });
});
```

L'`<img>` dell'hero ha `alt=""`, quindi è accessibile solo con `hidden: true`. Se `Hero` richiede altre
props obbligatorie, aggiungile dal file dei test esistenti in `detail-layout/__tests__`.

- [ ] **Step 2: Esegui il test e verifica che fallisca**

Run: `cd apps/web && pnpm test src/components/ui/detail-layout`
Expected: FAIL — la prop non esiste.

- [ ] **Step 3: Implementa**

Aggiungi `coverFocal?: { x: number; y: number };` alle props di `Hero`, destrutturala, e alla riga 215:

```tsx
          <img
            src={coverUrl}
            alt=""
            className="absolute inset-0 h-full w-full object-cover"
            style={
              coverFocal
                ? { objectPosition: `${coverFocal.x * 100}% ${coverFocal.y * 100}%` }
                : undefined
            }
          />
```

- [ ] **Step 4: Collega la pagina di dettaglio**

In `page-client.tsx`, subito sotto `coverUrl={game.coverUrl ?? null}`:

```tsx
          coverFocal={
            game.coverFocalX != null && game.coverFocalY != null
              ? { x: game.coverFocalX, y: game.coverFocalY }
              : undefined
          }
```

- [ ] **Step 5: Esegui i test e verifica che passino**

Run: `cd apps/web && pnpm test src/components/ui/detail-layout && pnpm typecheck`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/detail-layout apps/web/src/app/\(public\)/shared-games/\[id\]/page-client.tsx
git commit -m "feat(catalog): punto focale sull'hero del dettaglio gioco (#3611)"
```

---

### Task 6: L'affordance smette di nascondersi

**Files:**
- Modify: `apps/web/src/components/features/cover-editor/AdminCoverEditAffordance.tsx:42-51`
- Test: `apps/web/src/components/features/cover-editor/__tests__/AdminCoverEditAffordance.test.tsx`

**Interfaces:**
- Produces: prop `needsAttention?: boolean` su `AdminCoverEditAffordanceProps`.

Il difetto è `md:opacity-0 md:group-hover:opacity-100` alla riga 46. L'attenuazione a riposo passa
da `opacity` a colori semantici, perché il gate AA è bloccante.

- [ ] **Step 1: Scrivi il test che fallisce**

Aggiungi al `describe` esistente:

```tsx
  it('è visibile a riposo su desktop: niente opacity-0 (#3611)', () => {
    setRole(true);
    render(<AdminCoverEditAffordance gameId={GID} title="Catan" />);
    expect(screen.getByRole('button', { name: /copertina/i }).className).not.toMatch(/opacity-0/);
  });

  it('marca la cover da sistemare quando needsAttention', () => {
    setRole(true);
    render(<AdminCoverEditAffordance gameId={GID} title="Catan" needsAttention />);
    expect(screen.getByTestId('cover-needs-attention')).toBeInTheDocument();
  });

  it('non marca nulla senza needsAttention', () => {
    setRole(true);
    render(<AdminCoverEditAffordance gameId={GID} title="Catan" />);
    expect(screen.queryByTestId('cover-needs-attention')).not.toBeInTheDocument();
  });
```

- [ ] **Step 2: Esegui il test e verifica che fallisca**

Run: `cd apps/web && pnpm test src/components/features/cover-editor`
Expected: FAIL — la classe `md:opacity-0` è presente e il marcatore non esiste.

- [ ] **Step 3: Implementa**

Sostituisci il corpo del componente (props e markup del bottone):

```tsx
export interface AdminCoverEditAffordanceProps {
  gameId: string;
  title?: string;
  className?: string;
  /**
   * #3611 — la cover sottostante è un placeholder: l'azione qui serve davvero.
   * Rende la matita piena da subito e disegna un contorno tratteggiato sul riquadro,
   * senza richiedere modifiche ai contenitori (GridCard / Cover / hero).
   */
  needsAttention?: boolean;
}

export function AdminCoverEditAffordance({
  gameId,
  title,
  className,
  needsAttention = false,
}: AdminCoverEditAffordanceProps): React.JSX.Element | null {
  const { isEditorOrAbove } = useAdminRole();
  const [open, setOpen] = useState(false);

  if (!isEditorOrAbove) return null;

  // L'attenuazione a riposo NON usa `opacity`: abbasserebbe il contrasto di testo e bordo
  // sotto la soglia AA, e il gate di accessibilità è bloccante. Si attenua con i token.
  const restingTone = needsAttention
    ? 'border-border-strong bg-background text-foreground'
    : 'border-border/60 bg-background/80 text-muted-foreground hover:text-foreground hover:bg-background';

  return (
    <>
      {needsAttention && (
        <span
          data-testid="cover-needs-attention"
          aria-hidden="true"
          className="pointer-events-none absolute inset-0 z-10 rounded-md border-2 border-dashed border-border-strong"
        />
      )}
      <button
        type="button"
        aria-label="Modifica sorgente copertina"
        onClick={() => setOpen(true)}
        className={`absolute right-2 top-2 z-20 flex h-8 w-8 items-center justify-center rounded-md border shadow-sm backdrop-blur-md transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring ${restingTone} ${
          className ?? ''
        }`}
      >
        <Pencil className="h-4 w-4" aria-hidden="true" />
      </button>
      <AdminCoverSourceDialog
        gameId={gameId}
        title={title}
        open={open}
        onClose={() => setOpen(false)}
      />
    </>
  );
}
```

- [ ] **Step 4: Esegui test e gate di accessibilità**

Run: `cd apps/web && pnpm test src/components/features/cover-editor && pnpm lint:tokens`
Expected: PASS, incluso `cover-editor.axe.test.tsx`. Se axe segnala contrasto insufficiente sul tono a
riposo, sostituisci `text-muted-foreground` con `text-foreground` — non reintrodurre `opacity`.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/cover-editor
git commit -m "fix(catalog): affordance cover visibile senza hover (#3611)"
```

---

### Task 7: Marcare le cover che hanno bisogno di intervento

**Files:**
- Modify: `apps/web/src/components/ui/shared-games/shared-games-grid.tsx:103-108`
- Modify: `apps/web/src/app/(public)/shared-games/[id]/page-client.tsx:359`
- Test: `apps/web/src/components/ui/shared-games/shared-games-grid.test.tsx`

**Interfaces:**
- Consumes: `needsAttention` dal Task 6, `shouldUsePlaceholder` da `@/lib/games/cover-utils`.

- [ ] **Step 1: Scrivi il test che fallisce**

Il file mocka già `AdminCoverEditAffordance` (riga 30). Estendi il mock perché esponga la prop e aggiungi:

```tsx
vi.mock('@/components/features/cover-editor', () => ({
  AdminCoverEditAffordance: ({ gameId, needsAttention }: { gameId: string; needsAttention?: boolean }) => (
    <button type="button" data-testid={`cover-edit-${gameId}`} data-needs-attention={needsAttention ? 'true' : 'false'}>
      edit
    </button>
  ),
}));
```

```tsx
  it('segnala come da sistemare le card senza cover (#3611)', () => {
    renderGrid([
      { ...baseGame, id: 'g1', coverUrl: 'https://r2.example/c.webp' },
      { ...baseGame, id: 'g2', coverUrl: null },
    ]);

    expect(screen.getByTestId('cover-edit-g1')).toHaveAttribute('data-needs-attention', 'false');
    expect(screen.getByTestId('cover-edit-g2')).toHaveAttribute('data-needs-attention', 'true');
  });
```

Adatta `renderGrid` / `baseGame` agli helper già presenti nel file.

- [ ] **Step 2: Esegui il test e verifica che fallisca**

Run: `cd apps/web && pnpm test src/components/ui/shared-games/shared-games-grid`
Expected: FAIL — entrambe le card riportano `false`.

- [ ] **Step 3: Implementa nella griglia**

Importa `shouldUsePlaceholder` da `@/lib/games/cover-utils` e passa la prop, inoltrando anche il focal:

```tsx
          coverFocal={
            game.coverFocalX != null && game.coverFocalY != null
              ? { x: game.coverFocalX, y: game.coverFocalY }
              : undefined
          }
          coverEditSlot={
            isEditorOrAbove ? (
              <AdminCoverEditAffordance
                gameId={game.id}
                needsAttention={shouldUsePlaceholder(game.coverUrl)}
              />
            ) : undefined
          }
```

- [ ] **Step 4: Implementa sull'hero**

In `page-client.tsx` riga 359:

```tsx
          coverOverlay={
            <AdminCoverEditAffordance
              gameId={game.id}
              title={resolvedTitle}
              needsAttention={shouldUsePlaceholder(game.coverUrl)}
            />
          }
```

- [ ] **Step 5: Esegui i test e verifica che passino**

Run: `cd apps/web && pnpm test src/components/ui/shared-games && pnpm typecheck`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/shared-games apps/web/src/app/\(public\)/shared-games/\[id\]/page-client.tsx
git commit -m "feat(catalog): marca le cover mancanti nella griglia (#3611)"
```

---

### Task 8: Dalla vista cover-gap all'editor aperto

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/shared-games/cover-gap/page.tsx:141-148`
- Modify: `apps/web/src/components/features/cover-editor/AdminCoverEditAffordance.tsx`
- Modify: `apps/web/src/app/(public)/shared-games/[id]/page-client.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/shared-games/cover-gap/__tests__/page.test.tsx` (se assente, crealo)
- Test: `apps/web/src/components/features/cover-editor/__tests__/AdminCoverEditAffordance.test.tsx`

**Interfaces:**
- Produces: prop `defaultOpen?: boolean` su `AdminCoverEditAffordanceProps`.

Il parametro `highlight` non è letto da nessuno: viene sostituito, non implementato.

- [ ] **Step 1: Scrivi i test che falliscono**

Per l'affordance:

```tsx
  it('apre il dialog al mount con defaultOpen (deep-link da cover-gap)', () => {
    setRole(true);
    render(<AdminCoverEditAffordance gameId={GID} title="Catan" defaultOpen />);
    expect(screen.getByTestId('cover-dialog')).toBeInTheDocument();
  });

  it('ignora defaultOpen per un non-admin', () => {
    setRole(false);
    const { container } = render(<AdminCoverEditAffordance gameId={GID} defaultOpen />);
    expect(container).toBeEmptyDOMElement();
  });
```

Per la pagina cover-gap:

```tsx
  it('il CTA porta al gioco con l\'editor già aperto (#3611)', async () => {
    renderCoverGapPage();
    const link = await screen.findByRole('link', { name: /assegna cover/i });
    expect(link).toHaveAttribute('href', `/shared-games/${GAME_ID}?cover=edit`);
  });
```

- [ ] **Step 2: Esegui i test e verifica che falliscano**

Run: `cd apps/web && pnpm test src/components/features/cover-editor src/app/admin`
Expected: FAIL — `defaultOpen` non esiste; il link punta ancora a `/shared-games?highlight=…`.

- [ ] **Step 3: Implementa `defaultOpen`**

In `AdminCoverEditAffordance.tsx`, aggiungi la prop e usala come stato iniziale:

```tsx
  /** #3611 — apre il dialog al mount (deep-link `?cover=edit` dalla vista cover-gap). */
  defaultOpen?: boolean;
```

```tsx
  const [open, setOpen] = useState(defaultOpen);
```

Il gate resta l'`if (!isEditorOrAbove) return null` già presente: un non-admin col link non monta
nulla, quindi non serve alcun nuovo controllo di autorizzazione.

- [ ] **Step 4: Cambia il CTA della vista cover-gap**

Sostituisci l'`<a>` alle righe 142-147:

```tsx
                    <a
                      href={`/shared-games/${game.gameId}?cover=edit`}
                      className="text-primary underline underline-offset-2"
                    >
                      Assegna cover
                    </a>
```

Aggiorna anche la frase dell'header (riga 63-66) da «Per risolvere, apri il gioco e usa l'editor
cover.» a «Per risolvere, usa il collegamento in fondo a ogni riga: apre il gioco con l'editor
cover già pronto.», e il commento di intestazione del file che descrive il vecchio percorso.

- [ ] **Step 5: Collega il parametro nella pagina di dettaglio**

In `page-client.tsx`, leggi il parametro e passalo, ripulendo l'URL alla chiusura:

```tsx
  const searchParams = useSearchParams();
  const router = useRouter();
  const coverEditRequested = searchParams.get('cover') === 'edit';
```

```tsx
          coverOverlay={
            <AdminCoverEditAffordance
              gameId={game.id}
              title={resolvedTitle}
              needsAttention={shouldUsePlaceholder(game.coverUrl)}
              defaultOpen={coverEditRequested}
              onDialogClose={() => {
                if (coverEditRequested) router.replace(`/shared-games/${game.id}`, { scroll: false });
              }}
            />
          }
```

Questo richiede una terza prop opzionale sull'affordance, `onDialogClose?: () => void`, invocata
dentro l'`onClose` già esistente:

```tsx
        onClose={() => {
          setOpen(false);
          onDialogClose?.();
        }}
```

Se `useSearchParams` / `useRouter` non sono già importati in `page-client.tsx`, importali da
`next/navigation`.

- [ ] **Step 6: Esegui i test e verifica che passino**

Run: `cd apps/web && pnpm test src/components/features/cover-editor src/app/admin && pnpm typecheck && pnpm lint`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/app/admin apps/web/src/components/features/cover-editor apps/web/src/app/\(public\)/shared-games/\[id\]/page-client.tsx
git commit -m "fix(catalog): il CTA cover-gap apre l'editor sul gioco (#3611)"
```

---

### Task 9: Lettura dei byte da una chiave R2 grezza

**Files:**
- Modify: `apps/api/src/Api/Services/Pdf/IBlobStorageService.cs`
- Modify: `apps/api/src/Api/Services/Pdf/S3BlobStorageService.cs`
- Modify: `apps/api/src/Api/Services/Pdf/BlobStorageService.cs`
- Modify: `apps/api/src/Api/DevTools/MockImpls/MockBlobStorageService.cs`
- Test: `apps/api/tests/Api.Tests/Services/Pdf/S3BlobStorageServiceRawKeyTests.cs` (se esiste una classe di test per il servizio S3, aggiungi lì il caso invece di crearne una nuova)

**Interfaces:**
- Produces: `Task<Stream?> RetrieveRawKeyAsync(string rawKey, CancellationToken ct = default)` su `IBlobStorageService`.

Serve al Task 10 per rileggere la cover sorgente prima di ritagliarla. `RetrieveAsync` non è
utilizzabile: valida gli argomenti con `PathSecurity.ValidateIdentifier`, che rifiuta `/` e `.`
— la stessa trappola documentata nell'intestazione di `CoverUrlResolver`.

- [ ] **Step 1: Scrivi il test che fallisce**

```csharp
    [Fact]
    public async Task RetrieveRawKeyAsync_MissingObject_ReturnsNull()
    {
        var svc = CreateService(); // helper della classe di test esistente
        var stream = await svc.RetrieveRawKeyAsync("covers/pdf/does-not-exist/cover-preview.webp");
        stream.Should().BeNull();
    }
```

- [ ] **Step 2: Esegui il test e verifica che fallisca**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~BlobStorage"`
Expected: FAIL in compilazione — il metodo non esiste.

- [ ] **Step 3: Dichiara il metodo sull'interfaccia**

```csharp
    /// <summary>
    /// Legge un oggetto dalla sua chiave fisica ESATTA, senza la validazione categorizzata
    /// (che rifiuta `/` e `.`). Speculare a <see cref="StoreRawKeyAsync"/> (#3611).
    /// </summary>
    /// <remarks>IMPORTANT: il chiamante DEVE disporre lo stream restituito.</remarks>
    /// <returns>Lo stream dell'oggetto, o null se assente o se il backend non ospita chiavi grezze.</returns>
    Task<Stream?> RetrieveRawKeyAsync(string rawKey, CancellationToken ct = default);
```

- [ ] **Step 4: Implementa nelle tre classi**

In `S3BlobStorageService`, sul modello del `GetObjectAsync` già usato da `RetrieveAsync`:

```csharp
    public async Task<Stream?> RetrieveRawKeyAsync(string rawKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawKey);

        try
        {
            var response = await _s3Client
                .GetObjectAsync(new GetObjectRequest { BucketName = _bucketName, Key = rawKey }, ct)
                .ConfigureAwait(false);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
```

Verifica il nome esatto del campo del bucket leggendo `RetrieveAsync` nello stesso file e riusalo.

In `BlobStorageService` (locale) e in `MockBlobStorageService` restituisci `Task.FromResult<Stream?>(null)`,
coerentemente con `StoreRawKeyAsync` che documenta il `false` sui backend che non ospitano chiavi grezze.

Aggiungi lo stesso stub alle tre `FakeBlobStorageService` nei test
(`PlayRecordPhotoUploadTests`, `GameNightPhotoUploadTests`, `GamebookPhotoStorageServiceTests`),
altrimenti la suite non compila.

- [ ] **Step 5: Esegui la suite e verifica che passi**

Run: `dotnet test apps/api/tests/Api.Tests --filter "Category=Unit"`
Expected: PASS, nessun errore di compilazione dalle implementazioni fake.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Services/Pdf apps/api/src/Api/DevTools/MockImpls apps/api/tests/Api.Tests
git commit -m "feat(storage): lettura per chiave R2 grezza (#3611)"
```

---

### Task 10: Il crop Social viene generato al salvataggio

**Files:**
- Modify: `apps/api/src/Api/SharedKernel/Domain/Covers/CoverKeyBuilder.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/AssignCoverCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/AssignCoverSocialCropTests.cs` (nuovo)

**Interfaces:**
- Consumes: `RetrieveRawKeyAsync` (Task 9), `IWebpVariantGenerator.GenerateWebpAsync(byte[], int, int, double, double, CancellationToken)`, `GameCoverAssignment.SetGeneratedKey(string)`.
- Produces: `CoverKeyBuilder.ContextCropPhysicalKey(Guid gameId, CoverContext context)` → `string`.

`SetGeneratedKey` non ha oggi alcun chiamante di produzione: questo task glielo dà, per il solo
contesto Social. Card e Hero restano gestiti dal CSS.

- [ ] **Step 1: Scrivi il test che fallisce**

```csharp
    [Fact]
    public async Task Handle_SocialContext_RendersTheCropAndStampsTheGeneratedKey()
    {
        // Arrange: gioco con cover da PDF, storage che restituisce byte leggibili.
        _blob.Setup(b => b.RetrieveRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(() => new MemoryStream(new byte[] { 1, 2, 3 }));
        _webp.Setup(w => w.GenerateWebpAsync(
                It.IsAny<byte[]>(), 1200, 630, 0.5, 0.2, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new byte[] { 9 });
        _blob.Setup(b => b.StoreRawKeyAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), "image/webp", It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);

        await _handler.Handle(
            new AssignCoverCommand(GameId, CoverContext.Social, CoverAssignmentSource.Pdf, AdminId, 0.5, 0.2),
            CancellationToken.None);

        _blob.Verify(b => b.StoreRawKeyAsync(
            $"covers/crops/{GameId:D}/social.webp", It.IsAny<Stream>(), "image/webp", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CardContext_DoesNotRenderAnything()
    {
        await _handler.Handle(
            new AssignCoverCommand(GameId, CoverContext.Card, CoverAssignmentSource.Pdf, AdminId, 0.5, 0.5),
            CancellationToken.None);

        _blob.Verify(b => b.StoreRawKeyAsync(
            It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_RenderFails_StillPersistsTheAssignment()
    {
        _blob.Setup(b => b.RetrieveRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Stream?)null);

        var dto = await _handler.Handle(
            new AssignCoverCommand(GameId, CoverContext.Social, CoverAssignmentSource.Pdf, AdminId, 0.5, 0.2),
            CancellationToken.None);

        dto.Context.Should().Be(CoverContext.Social);
    }
```

Allinea la firma di `AssignCoverCommand` a quella reale leggendo `AssignCoverCommand.cs`, e copia il
setup del handler (repository, unit of work, cache, retry policy, logger) dai test già presenti per
`AssignCoverCommandHandler`.

- [ ] **Step 2: Esegui i test e verifica che falliscano**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~AssignCoverSocialCropTests"`
Expected: FAIL — nessuna chiamata a `StoreRawKeyAsync`, e il handler non accetta le due dipendenze nuove.

- [ ] **Step 3: Aggiungi la chiave del crop**

In `CoverKeyBuilder.cs`:

```csharp
    /// <summary>
    /// Crop per contesto renderizzato dal punto focale (#3611): <c>covers/crops/{gameId:D}/{context}.webp</c>.
    /// È già una chiave FISICA — il resolver la usa verbatim da <c>GeneratedR2Key</c>, senza suffissi.
    /// </summary>
    public static string ContextCropPhysicalKey(Guid gameId, CoverContext context) =>
        $"covers/crops/{gameId:D}/{context.ToString().ToLowerInvariant()}.webp";
```

- [ ] **Step 4: Renderizza nel handler**

In `AssignCoverCommandHandler`, inietta `IWebpVariantGenerator` e `IBlobStorageService` (registrandoli
nel costruttore con lo stesso schema `?? throw new ArgumentNullException` degli altri), e inserisci il
render fra `game.AssignCover(...)` e `ReconcileCoverAssignmentsAsync`:

```csharp
        // #3611 — il solo contesto Social ha bisogno di un FILE: un crawler OpenGraph non esegue
        // CSS, mentre Card e Hero sono inquadrate dal browser via object-position. Il fallimento
        // è tollerato: GeneratedR2Key resta null e il resolver ricade sull'immagine base.
        if (command.Context == CoverContext.Social)
        {
            var generatedKey = await TryRenderSocialCropAsync(game, assignment, cancellationToken)
                .ConfigureAwait(false);
            if (generatedKey is not null)
            {
                assignment.SetGeneratedKey(generatedKey);
            }
        }
```

e il metodo privato:

```csharp
    private const int SocialWidth = 1200;
    private const int SocialHeight = 630;
    private const string WebpContentType = "image/webp";

    private async Task<string?> TryRenderSocialCropAsync(
        Domain.Aggregates.SharedGame game,
        Domain.Entities.GameCoverAssignment assignment,
        CancellationToken ct)
    {
        try
        {
            var kind = assignment.Source.ToCoverKind();
            var baseDbKey = SourceDbKeyFor(game, kind);
            if (string.IsNullOrWhiteSpace(baseDbKey))
            {
                return null;
            }

            await using var source = await _blobStorage
                .RetrieveRawKeyAsync(CoverKeyBuilder.PhysicalKeyFor(kind, baseDbKey), ct)
                .ConfigureAwait(false);
            if (source is null)
            {
                return null;
            }

            using var buffer = new MemoryStream();
            await source.CopyToAsync(buffer, ct).ConfigureAwait(false);

            var cropped = await _webpGenerator
                .GenerateWebpAsync(
                    buffer.ToArray(), SocialWidth, SocialHeight,
                    assignment.FocalX, assignment.FocalY, ct)
                .ConfigureAwait(false);

            var key = CoverKeyBuilder.ContextCropPhysicalKey(game.Id, CoverContext.Social);
            using var upload = new MemoryStream(cropped);
            var stored = await _blobStorage
                .StoreRawKeyAsync(key, upload, WebpContentType, ct)
                .ConfigureAwait(false);

            return stored ? key : null;
        }
        catch (Exception ex) when (ex is ImageProcessingException or IOException or HttpRequestException)
        {
            _logger.LogWarning(ex,
                "Render del crop Social fallito per {GameId}: la cover base resta servita", game.Id);
            return null;
        }
    }
```

`SourceDbKeyFor` seleziona la colonna della sorgente sull'aggregato, con lo stesso `switch` di
`CoverUrlResolver.SourceDbKey`. Se l'aggregato `SharedGame` non espone quelle chiavi come proprietà
pubbliche, leggi il valore dall'entità già caricata dal repository invece di aggiungere accessori
al dominio.

- [ ] **Step 5: Esegui i test e verifica che passino**

Run: `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~AssignCover"`
Expected: PASS, inclusi i test preesistenti del handler.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/SharedKernel/Domain/Covers/CoverKeyBuilder.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/AssignCoverCommandHandler.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands
git commit -m "feat(catalog): genera il crop Social dal punto focale (#3611)"
```

---

### Task 11: Verifica finale e apertura PR

- [ ] **Step 1: Suite completa del backend**

Run: `dotnet test apps/api/tests/Api.Tests --filter "Category=Unit"`
Expected: PASS. La baseline dei fallimenti unitari è **zero**: qualunque rosso è una regressione introdotta qui.

- [ ] **Step 2: Suite del frontend toccata dalla modifica**

Run: `cd apps/web && pnpm test src/components src/lib/api/schemas src/app/admin`
Expected: PASS. Non lanciare la suite completa in locale: produce fallimenti da mock-pollution che la CI, che gira a shard, non vede.

- [ ] **Step 3: Gate di qualità**

Run: `cd apps/web && pnpm typecheck && pnpm lint && pnpm lint:tokens`
Expected: PASS.

- [ ] **Step 4: Formattazione backend**

```bash
dotnet format apps/api/src/Api/Api.csproj --include \
  apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/CoverUrlResolver.cs \
  apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/SharedGameDto.cs \
  apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/AssignCoverCommandHandler.cs \
  apps/api/src/Api/SharedKernel/Domain/Covers/CoverKeyBuilder.cs \
  apps/api/src/Api/Services/Pdf/IBlobStorageService.cs \
  apps/api/src/Api/Services/Pdf/S3BlobStorageService.cs \
  apps/api/src/Api/Services/Pdf/BlobStorageService.cs \
  apps/api/src/Api/DevTools/MockImpls/MockBlobStorageService.cs
```

Expected: nessuna modifica residua. **Usa sempre `--include`**: senza, `dotnet format` applica anche le
correzioni degli analyzer e ha già rimosso costruttori usati solo via reflection.

- [ ] **Step 5: Push e PR**

```bash
git push -u origin feature/issue-3611-cover-editor-discoverability
gh pr create --base main-dev \
  --title "fix(catalog): editor cover trovabile e crop per contesto (#3611)" \
  --body "$(cat <<'EOF'
Chiude #3611.

L'editor cover esisteva da #3470 ma non era raggiungibile e non produceva effetti:
- l'affordance era invisibile su desktop (`md:opacity-0`);
- il CTA della vista cover-gap puntava a un parametro (`?highlight=`) che nessuno legge;
- il crop per contesto non è mai stato implementato — `SetGeneratedKey` non aveva chiamanti di produzione, quindi il punto focale impostato dall'admin non aveva alcun effetto visivo.

Il punto focale ora viaggia dal resolver fino al DTO, con un default euristico per sorgente (le cover da PDF si ancorano in alto, quelle d'artwork restano centrate) che non richiede né migration né backfill. Card e Hero lo applicano via `object-position`; il contesto Social, che un crawler consuma senza eseguire CSS, ottiene un WebP 1200×630 generato al salvataggio.

Fuori scope, da tracciare: il crop Social per i giochi che nessuno ha ancora assegnato.

Design: `docs/superpowers/specs/2026-08-07-cover-editor-discoverability-design.md`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 6: Apri la issue di follow-up**

```bash
gh issue create --title "feat(catalog): crop Social per i giochi senza assegnazione (#3611 follow-up)" \
  --body "Il default euristico di #3611 corregge Card e Hero via object-position, ma il contesto Social ha bisogno di un file: un crawler OpenGraph non esegue CSS. Oggi il crop 1200x630 nasce solo quando un admin assegna esplicitamente la cover Social, quindi l'anteprima social resta un ritratto 2:3 per tutti gli altri giochi.

Due strade valutate durante il design:
1. generazione in fase di materializzazione della cover (event handler già esistente) più un comando admin di backfill per le cover in archivio;
2. generazione on-demand alla prima richiesta con cache su R2, senza backfill ma con una scrittura su un percorso di sola lettura e la concorrenza della prima richiesta da gestire.

Design di riferimento: docs/superpowers/specs/2026-08-07-cover-editor-discoverability-design.md"
```

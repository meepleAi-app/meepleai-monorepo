# KB Hub Implementation — `/library/[gameId]/kb`

**Issue**: [#1481](https://github.com/meepleAi-app/meepleai-monorepo/issues/1481) — `feat(kb-hub): implement /knowledge-base hub — 8 components Tier M`
**Epic**: [#1475](https://github.com/meepleAi-app/meepleai-monorepo/issues/1475) — User-facing UI completion
**Date**: 2026-05-25
**Tier**: M (~6-8h)
**Mockup**: `admin-mockups/design_files/sp4-kb-hub.{html,jsx}` (1072 LOC reference impl)

## Decisione di scope

### Routing — Opzione A1 (standalone game-scoped)

L'issue title menziona "knowledge-base hub" ma il mockup `sp4-kb-hub.jsx` è esplicitamente game-scoped (`/library/private/[gameId]/kb | /games/[id]/kb` nel comment header). La rotta `/knowledge-base` attuale è solo un `redirect('/library')`.

**Decisione**: creiamo una NUOVA rotta `/library/[gameId]/kb` (standalone), NON modifichiamo `/knowledge-base`. Pattern coerente con sibling `/library/[gameId]/toolbox`.

- `/library/[gameId]/kb` (NUOVA) — orchestrator + 8 componenti del mockup
- `/knowledge-base/page.tsx` (immutato — resta redirect)
- `/library/[gameId]?tab=*` (immutato — i 5 tab esistenti restano)

`GameDetailKbDocList.tsx` (esistente, lightweight) **coexiste** (decisione F2): resta per il Info tab card del game detail. `HubDefault` (nuovo) è il rendering full-page del nuovo route.

### Scope BE — Opzione X1 (Component-only shelf-ready + BE follow-up)

**Pattern P83 spec-panel-scope-reduction-via-BE-reality** (precedente: #1484): il mockup mostra 8 stati con 13+ campi di dati; il BE user-side espone solo 3 campi via `UserGameKbStatusSchema` + `GamePdfDto`.

**Approccio**: 8 componenti pure presentational con **props complete del mockup** (future-proof), orchestrator wires SOLO dati BE disponibili oggi, componenti **hide gracefully** UI per props undefined. Aprirò follow-up BE issue per arricchire lo schema.

| Dato mockup | BE user-side | Strategia |
|---|---|---|
| `documentCount` | `UserGameKbStatus.documentCount` ✅ | wired |
| `coverageLevel/Score` | `UserGameKbStatus.coverageLevel/Score` ✅ | wired |
| PDF list | `libraryClient.getGamePdfs(gameId)` ✅ | wired |
| Reindex action | `POST /api/v1/games/{gameId}/kb/reindex` ✅ | wired |
| RAPTOR rebuild | `POST /api/v1/games/{gameId}/kb/raptor/rebuild` ✅ | wired (button disabled in free MVP) |
| Delete PDF | `DELETE /api/v1/pdf/{pdfId}` ✅ | wired |
| `chunks` | – | deferred, prop optional, hide |
| `embeddings` | – | deferred, prop optional, hide |
| `lastReindex` | – | deferred, prop optional, hide |
| `raptorLastRebuild` | – | deferred, prop optional, hide |
| `lifetimeCost` | – | deferred, prop optional, hide |
| `costHistory[]` | – | deferred, prop optional, hide sparkline |
| PDF `status` (ready/indexing/stale/failed) | – | deferred, prop optional, hide badge |
| PDF `chunks` count | – | deferred, prop optional, hide |
| User `tier` (free/pro) | – | default `'free'` MVP, follow-up issue |
| Cleanup metadata (chunks/raptor/edges count) | – | deferred, prop optional, hide list |

## Architettura

```
apps/web/src/app/(authenticated)/library/[gameId]/kb/
├── page.tsx        # Server route, awaits params, renders <KbHubPage gameId={...} />
└── _content.tsx    # 'use client', KbHubPage orchestrator

apps/web/src/components/features/kb-hub/
├── index.ts                  # barrel export
├── KbStatsCard.tsx
├── PdfRow.tsx
├── HubDefault.tsx
├── EmptyState.tsx
├── ActionsMenu.tsx
├── ReindexModal.tsx
├── RaptorPanel.tsx
├── DeleteDialog.tsx
└── __tests__/                # vitest + RTL + jest-axe
    ├── KbStatsCard.test.tsx
    ├── PdfRow.test.tsx
    ├── HubDefault.test.tsx
    ├── EmptyState.test.tsx
    ├── ActionsMenu.test.tsx
    ├── ReindexModal.test.tsx
    ├── RaptorPanel.test.tsx
    └── DeleteDialog.test.tsx

apps/web/src/hooks/queries/
└── useKbHub.ts              # query keys + 5 hooks consolidated

apps/web/messages/{it,en}/messages.json
└── pages.library.gameDetail.kb.*   # NEW namespace
```

## Component contracts

Tutti i componenti sono **pure presentational** (props-only, no `useQuery`/`useMutation` interni). Labels passate caller-side (i18n risolta nell'orchestrator). Pattern coerente con `GameDetailLeaderboard` / `GameDetailHouseRulesList` / `GameDetailChatTab`.

### KbStatsCard

```ts
interface KbStatsCardProps {
  readonly documentCount: number;
  readonly coverageLevel: 'None' | 'Basic' | 'Standard' | 'Complete';
  readonly coverageScore: number;          // 0-100
  readonly labels: KbStatsCardLabels;
  // Deferred (hide if undefined):
  readonly chunks?: number;
  readonly embeddings?: number;
  readonly lastReindex?: string;           // human-relative "3 gg fa"
  readonly raptorLastRebuild?: string;
  readonly lifetimeCost?: string;          // "$2.84"
  readonly costHistory?: ReadonlyArray<number>;
  readonly compact?: boolean;
  readonly className?: string;
}
```

### PdfRow

```ts
type PdfStatus = 'ready' | 'indexing' | 'stale' | 'failed';

interface PdfRowProps {
  readonly pdf: {
    readonly id: string;
    readonly name: string;
    readonly sizeFormatted: string;
    readonly uploadedAtRelative: string;
    readonly status?: PdfStatus;       // hide badge if undefined
    readonly chunks?: number;          // hide "N chunks" suffix if undefined
  };
  readonly labels: PdfRowLabels;
  readonly onActionClick: (pdfId: string) => void;
}
```

### HubDefault

```ts
interface HubDefaultProps {
  readonly game: { readonly title: string; readonly cover?: string; readonly emoji?: string };
  readonly documentCount: number;
  readonly coverageLevel: 'None' | 'Basic' | 'Standard' | 'Complete';
  readonly pdfs: ReadonlyArray<PdfRowProps['pdf']>;
  readonly labels: HubDefaultLabels;
  readonly onUpload: () => void;
  readonly onReindexAll: () => void;
  readonly onPdfAction: (pdfId: string) => void;
  // Deferred:
  readonly chunks?: number;
  readonly embeddings?: number;
  readonly lastReindexRelative?: string;
}
```

### EmptyState

```ts
interface EmptyStateProps {
  readonly gameTitle: string;
  readonly labels: EmptyStateLabels;
  readonly onUpload: () => void;
}
```

### ActionsMenu

```ts
type PdfAction = 'open' | 'reindex' | 'cost' | 'move' | 'delete';

interface ActionsMenuProps {
  readonly pdf: PdfRowProps['pdf'];
  readonly labels: ActionsMenuLabels;       // 5 actions × { label, description }
  readonly onSelect: (action: PdfAction) => void;
  readonly children: ReactNode;             // trigger element (DropdownMenuTrigger)
}
```

Implementazione: wrap di `DropdownMenu` Radix esistente (`components/ui/navigation/dropdown-menu.tsx`).

### ReindexModal

```ts
type ReindexPhase = 'confirm' | 'running' | 'done';

interface ReindexModalProps {
  readonly open: boolean;
  readonly phase: ReindexPhase;
  readonly labels: ReindexModalLabels;
  readonly costRows: ReadonlyArray<{ readonly key: string; readonly label: string; readonly value: string; readonly bold?: boolean }>;
  readonly onConfirm: () => void;
  readonly onClose: () => void;
  // Running phase optional:
  readonly progress?: { readonly current: number; readonly total: number; readonly jobId?: string };
  // Done phase optional:
  readonly summary?: { readonly chunks: number; readonly embeddings: number; readonly actualCost: string };
}
```

FSM contract (Gherkin):

```gherkin
Given user opens ReindexModal phase="confirm"
Then dialog shows cost breakdown table + Re-index / Annulla CTAs

When user clicks Re-index
Then orchestrator triggers useReindexKb.mutateAsync() AND sets phase="running"
And modal shows spinner + progress bar + job ID (if returned)

When mutation resolves successfully
Then orchestrator sets phase="done"
And modal shows success badge + summary + Chiudi CTA

When user clicks Chiudi
Then orchestrator sets open=false AND phase="confirm" (reset for next invocation)
```

### RaptorPanel

```ts
type RaptorTier = 'free' | 'pro';

interface RaptorPanelProps {
  readonly tier: RaptorTier;
  readonly labels: RaptorPanelLabels;
  readonly onRebuild?: () => void;          // only invoked if tier='pro'
  // Deferred metrics:
  readonly lastRebuildRelative?: string;
  readonly summariesCount?: number;
  readonly estimatedCost?: string;
  readonly estimatedDuration?: string;
}
```

Variant routing (Gherkin):

```gherkin
Given tier='free'
Then panel shows locked state with "🔒 PRO" badge + lockedNote + disabled CTA + upgrade link

Given tier='pro'
Then panel shows active state with "PRO ✓" badge + cost/duration row + enabled CTA
And clicking CTA invokes onRebuild
```

### DeleteDialog

```ts
interface DeleteDialogProps {
  readonly open: boolean;
  readonly pdfName: string;
  readonly labels: DeleteDialogLabels;
  readonly onConfirm: () => void;
  readonly onCancel: () => void;
  // Deferred cleanup metadata (hide list if undefined):
  readonly cleanupItems?: ReadonlyArray<{ readonly key: string; readonly icon: string; readonly label: string }>;
}
```

Implementazione: estende `ConfirmModal` con slot `<DialogDescription>` custom contenente cleanup list + warning. Variant=`destructive`.

## Hook contracts — `useKbHub.ts`

```ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { knowledgeBaseClient } from '@/lib/api/clients/knowledgeBaseClient';
import { libraryClient } from '@/lib/api/clients/libraryClient';
import { pdfClient } from '@/lib/api/clients/pdfClient';

export const kbHubKeys = {
  all: ['kbHub'] as const,
  status: (gameId: string) => [...kbHubKeys.all, 'status', gameId] as const,
  pdfs: (gameId: string) => [...kbHubKeys.all, 'pdfs', gameId] as const,
};

// READ
export function useUserGameKbStatus(gameId: string | undefined) {
  return useQuery({
    queryKey: gameId ? kbHubKeys.status(gameId) : ['kbHub', 'status', '__skip'],
    queryFn: () => knowledgeBaseClient.getUserGameKbStatus(gameId!),
    enabled: !!gameId,
  });
}

export function useGamePdfs(gameId: string | undefined) {
  return useQuery({
    queryKey: gameId ? kbHubKeys.pdfs(gameId) : ['kbHub', 'pdfs', '__skip'],
    queryFn: () => libraryClient.getGamePdfs(gameId!),
    enabled: !!gameId,
  });
}

// MUTATIONS — cache invalidation strategy
export function useReindexKb(gameId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => knowledgeBaseClient.reindexKb(gameId), // NEW client method (existing endpoint)
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: kbHubKeys.status(gameId) });
      qc.invalidateQueries({ queryKey: kbHubKeys.pdfs(gameId) });
    },
  });
}

export function useRebuildRaptor(gameId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => knowledgeBaseClient.rebuildRaptor(gameId), // NEW client method (existing endpoint)
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: kbHubKeys.status(gameId) });
    },
  });
}

export function useDeletePdf(gameId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (pdfId: string) => pdfClient.deletePdf(pdfId), // existing
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: kbHubKeys.pdfs(gameId) });
      qc.invalidateQueries({ queryKey: kbHubKeys.status(gameId) });
    },
  });
}
```

**NB**: `knowledgeBaseClient.reindexKb()` e `.rebuildRaptor()` non esistono ancora — vanno aggiunti (thin wrappers `POST /api/v1/games/{gameId}/kb/reindex` e `POST /api/v1/games/{gameId}/kb/raptor/rebuild`).

## Orchestrator — `_content.tsx`

```tsx
'use client';

export function KbHubPage({ gameId }: { gameId: string }) {
  const { t } = useTranslation();
  const { data: status, isLoading: statusLoading } = useUserGameKbStatus(gameId);
  const { data: pdfs = [], isLoading: pdfsLoading } = useGamePdfs(gameId);
  const { data: game } = useGameDetail(gameId);
  const reindexMutation = useReindexKb(gameId);
  const raptorMutation = useRebuildRaptor(gameId);
  const deleteMutation = useDeletePdf(gameId);

  const [reindexOpen, setReindexOpen] = useState(false);
  const [reindexPhase, setReindexPhase] = useState<ReindexPhase>('confirm');
  const [actionsMenuPdfId, setActionsMenuPdfId] = useState<string | null>(null);
  const [deletePdfTarget, setDeletePdfTarget] = useState<GamePdfDto | null>(null);

  if (statusLoading || pdfsLoading) return <KbHubSkeleton />;
  
  const isEmpty = pdfs.length === 0;
  const gameInfo = { title: game?.title ?? '', emoji: '📚' };

  return (
    <div className="container mx-auto py-8 px-4 max-w-6xl space-y-6">
      {isEmpty ? (
        <EmptyState gameTitle={gameInfo.title} labels={...} onUpload={handleUpload} />
      ) : (
        <HubDefault 
          game={gameInfo}
          documentCount={status?.documentCount ?? 0}
          coverageLevel={status?.coverageLevel ?? 'None'}
          pdfs={mapPdfs(pdfs)}
          labels={...}
          onUpload={handleUpload}
          onReindexAll={() => { setReindexOpen(true); setReindexPhase('confirm'); }}
          onPdfAction={setActionsMenuPdfId}
        />
      )}

      {!isEmpty && (
        <KbStatsCard
          documentCount={status?.documentCount ?? 0}
          coverageLevel={status?.coverageLevel ?? 'None'}
          coverageScore={status?.coverageScore ?? 0}
          labels={...}
          // chunks/embeddings/lastReindex/etc. undefined → hidden gracefully
        />
      )}

      {!isEmpty && (
        <RaptorPanel
          tier="free"  // MVP: no tier data from BE
          labels={...}
          // metrics undefined → hidden
        />
      )}

      <ReindexModal
        open={reindexOpen}
        phase={reindexPhase}
        labels={...}
        costRows={STATIC_REINDEX_COST_ROWS}  // hardcoded MVP (BE doesn't expose per-game estimate)
        onConfirm={async () => {
          setReindexPhase('running');
          try {
            await reindexMutation.mutateAsync();
            setReindexPhase('done');
          } catch {
            setReindexOpen(false);
            setReindexPhase('confirm');
          }
        }}
        onClose={() => { setReindexOpen(false); setReindexPhase('confirm'); }}
      />

      <DeleteDialog
        open={!!deletePdfTarget}
        pdfName={deletePdfTarget?.name ?? ''}
        labels={...}
        onConfirm={async () => {
          if (!deletePdfTarget) return;
          await deleteMutation.mutateAsync(deletePdfTarget.id);
          setDeletePdfTarget(null);
        }}
        onCancel={() => setDeletePdfTarget(null)}
      />

      {/* ActionsMenu è popover bound a PdfRow trigger — wireup interno HubDefault */}
    </div>
  );
}
```

## Testing strategy

### Unit tests (vitest + RTL)

Target: **~40 test** (mirror conteggio component breakdown).

Per ogni component:
- Default render (props richiesti)
- Optional props rendering (deferred fields → hide UI when undefined)
- Event callbacks (onAction, onClick, onSelect, ecc.)
- Edge cases (empty list, error variant, locked tier)

### A11y tests (jest-axe)

- ReindexModal: 3 phases × dialog + focus trap + ESC = 6+ checks
- DeleteDialog: dialog + focus trap + destructive variant = 4+ checks
- ActionsMenu: popover focus return + ESC + arrow nav = 3+ checks

### Integration tests

- Orchestrator: loading state → empty/default routing
- Orchestrator: mutation success → cache invalidation
- Orchestrator: mutation error → modal reset

### i18n MESSAGES test extension (P71)

Aggiornare `apps/web/src/lib/i18n/__tests__/messages.test.tsx` per coprire le nuove key `pages.library.gameDetail.kb.*`.

## i18n keys (preview)

Namespace: `pages.library.gameDetail.kb`

```json
"kb": {
  "title": "{gameTitle} · Knowledge Base",
  "entityLabel": "Knowledge Base",
  "uploadCta": "+ Carica PDF",
  "reindexAllCta": "⟳ Re-index all",
  "stats": {
    "docs": "{count, plural, one {# documento} other {# documenti}}",
    "coverage": {
      "None": "Nessuna",
      "Basic": "Base",
      "Standard": "Standard",
      "Complete": "Completa"
    }
  },
  "empty": {
    "title": "Nessun PDF indicizzato",
    "description": "Carica il primo documento PDF per indicizzare le regole di {gameTitle}.",
    "ctaLabel": "Carica primo documento",
    "supportedFormats": "Formati supportati: PDF · Max 200 MB per file"
  },
  "pdfRow": {
    "openCta": "Apri dettaglio",
    "openAria": "Apri dettaglio {pdfName}",
    "chunksLabel": "{count} chunks",
    "status": {
      "ready": "Ready",
      "indexing": "Indexing",
      "stale": "Stale",
      "failed": "Failed"
    }
  },
  "actionsMenu": {
    "open": { "label": "Apri dettaglio", "description": "Visualizza chunks e preview" },
    "reindex": { "label": "Re-index", "description": "Rielabora embedding PDF" },
    "cost": { "label": "Statistiche costo", "description": "Token consumati e costo" },
    "move": { "label": "Sposta in altro gioco", "description": "Sposta in altra scheda KB" },
    "delete": { "label": "Elimina", "description": "Rimuovi PDF e cleanup" }
  },
  "reindexModal": {
    "title": "Re-index full KB",
    "subtitle": "{gameTitle} · {docCount} documenti",
    "costHeader": "Stima costo operazione",
    "description": "Questa operazione rielabora tutti i chunk e rigenera gli embedding. Le chat esistenti continueranno a funzionare durante il processo.",
    "reindexCta": "⟳ Re-index",
    "cancelCta": "Annulla",
    "runningTitle": "Re-index in corso…",
    "jobIdLabel": "Job ID",
    "doneTitle": "Re-index completato",
    "closeCta": "Chiudi"
  },
  "raptor": {
    "title": "RAPTOR Rebuild",
    "description": "Recursive Abstractive Processing Tree Of Retrieval",
    "lockedBadge": "🔒 PRO",
    "activeBadge": "PRO ✓",
    "lockedNote": "RAPTOR richiede piano Pro. Abilita clustering gerarchico per retrieval Q&A avanzato.",
    "upgradeCta": "Rebuild RAPTOR — Upgrade to Pro",
    "upgradeLink": "Scopri piano Pro",
    "rebuildCta": "Rebuild RAPTOR",
    "metrics": {
      "lastRebuild": "Ultimo rebuild",
      "summaries": "Summaries generate"
    }
  },
  "delete": {
    "title": "Confermi eliminazione PDF?",
    "subtitle": "{pdfName}",
    "listHeader": "Verrà eliminato definitivamente:",
    "warning": "Operazione irreversibile — nessun backup disponibile",
    "deleteCta": "Elimina definitivamente",
    "cancelCta": "Annulla"
  }
}
```

EN mirror.

## DS-15 tokens

- KB entity: `bg-entity-kb`, `text-entity-kb`, `border-entity-kb`, `text-entity-kb/[opacity]` (canonical `--c-kb` teal 174 60% 40% light / 174 60% 55% dark)
- Destructive (delete): `bg-destructive`, `text-destructive`, `border-destructive`
- RAPTOR gold: usa `text-warning`, `bg-warning`, `border-warning` (AA-safe semantic, sostituisce mockup hardcoded `hsl(43,92%,52%)`)
- Cards: `bg-card`, `border-border`, `text-foreground`, `text-muted-foreground`
- NO hardcoded color utilities (ESLint rule `local/no-hardcoded-color-utility` enforced)

## Out of scope (deferred follow-up issues)

1. **BE schema enrichment** — apro 1 follow-up issue P2/P3:
   - Extend `UserGameKbStatusSchema` con: `chunks, embeddings, lastReindexAt, raptorLastRebuildAt, lifetimeCostUsd, costHistoryLast7Days[]`
   - Extend `GamePdfDtoSchema` con: `processingStatus, chunkCount`
   - New endpoint per cleanup metadata: `GET /api/v1/pdf/{pdfId}/cleanup-preview`
2. **User tier detection** (free/pro) — apro 1 follow-up issue se non esiste già
3. **Reindex job polling** — server emette job ID, FE polling endpoint disponibile (`/kb/reindex/{jobId}/status`) ma non integrato in MVP; running phase usa optimistic mock progress
4. **PDF upload flow** — già esistente in altro flow, CTA Upload link out (out of scope qui)
5. **Mockup state #6 KB Coverage Stats Card "compact" variant** — `compact` prop esposto nel mockup ma orchestrator render solo full size

## File list

### NEW (FE)
- `apps/web/src/app/(authenticated)/library/[gameId]/kb/page.tsx`
- `apps/web/src/app/(authenticated)/library/[gameId]/kb/_content.tsx`
- `apps/web/src/components/features/kb-hub/index.ts`
- `apps/web/src/components/features/kb-hub/KbStatsCard.tsx`
- `apps/web/src/components/features/kb-hub/PdfRow.tsx`
- `apps/web/src/components/features/kb-hub/HubDefault.tsx`
- `apps/web/src/components/features/kb-hub/EmptyState.tsx`
- `apps/web/src/components/features/kb-hub/ActionsMenu.tsx`
- `apps/web/src/components/features/kb-hub/ReindexModal.tsx`
- `apps/web/src/components/features/kb-hub/RaptorPanel.tsx`
- `apps/web/src/components/features/kb-hub/DeleteDialog.tsx`
- `apps/web/src/components/features/kb-hub/__tests__/KbStatsCard.test.tsx`
- `apps/web/src/components/features/kb-hub/__tests__/PdfRow.test.tsx`
- `apps/web/src/components/features/kb-hub/__tests__/HubDefault.test.tsx`
- `apps/web/src/components/features/kb-hub/__tests__/EmptyState.test.tsx`
- `apps/web/src/components/features/kb-hub/__tests__/ActionsMenu.test.tsx`
- `apps/web/src/components/features/kb-hub/__tests__/ReindexModal.test.tsx`
- `apps/web/src/components/features/kb-hub/__tests__/RaptorPanel.test.tsx`
- `apps/web/src/components/features/kb-hub/__tests__/DeleteDialog.test.tsx`
- `apps/web/src/hooks/queries/useKbHub.ts`
- `apps/web/src/hooks/queries/__tests__/useKbHub.test.tsx`

### MODIFIED (FE)
- `apps/web/messages/it/messages.json` — add `pages.library.gameDetail.kb.*`
- `apps/web/messages/en/messages.json` — add `pages.library.gameDetail.kb.*` (EN mirror)
- `apps/web/src/lib/i18n/__tests__/messages.test.tsx` — extend MESSAGES coverage (P71)
- `apps/web/src/lib/api/clients/knowledgeBaseClient.ts` — add `reindexKb(gameId)` + `rebuildRaptor(gameId)` thin wrappers
- `apps/web/src/hooks/queries/index.ts` — export from useKbHub

### MODIFIED (matrix)
- `docs/for-developers/frontend/v2-migration-matrix.md` — `pending` → `done` for `/library/[gameId]/kb` row (or new row if absent)

## Effort estimate

| Phase | Time |
|---|---|
| Spec doc (this file) + commit | ~30m ✅ |
| Branch + barrel scaffolding | ~15m |
| 8 components TDD (red → green per component) | ~3-4h |
| useKbHub hook + tests | ~30m |
| _content orchestrator + integration tests | ~1h |
| i18n keys + MESSAGES test extension | ~30m |
| Matrix update + spec link | ~10m |
| Code review subagent + apply IMPORTANT | ~30-45m |
| PR + CI + merge | ~30m-1h |
| **Total** | **~6-8h** (Tier M aligned) |

## References

- Mockup: `admin-mockups/design_files/sp4-kb-hub.{html,jsx}`
- Issue body: [#1481](https://github.com/meepleAi-app/meepleai-monorepo/issues/1481)
- Epic: [#1475](https://github.com/meepleAi-app/meepleai-monorepo/issues/1475)
- Pattern reference (game-detail siblings): `apps/web/src/components/features/game-detail/`
- ConfirmModal precedent: PR #1516 (Issue #1464 HouseRulesList CRUD)
- DS-15 token canonical: `docs/for-developers/specs/2026-05-12-token-canonicalization.md`
- v2 migration spec: `docs/for-developers/specs/2026-04-26-v2-design-migration.md`
- Pattern P83 (scope reduction): MEMORY.md 2026-05-25 #1484 EncounterCheatsheetView entry

# Design — Issue #2860 (C3): consolidate `build*Connections` + status-mapping

**Data:** 2026-07-13
**Issue:** [#2860](https://github.com/meepleAi-app/meepleai-monorepo/issues/2860) (ST6) — umbrella [#2863](https://github.com/meepleAi-app/meepleai-monorepo/issues/2863) "MeepleCard family debt teardown"
**Audit:** `docs/for-developers/audits/2026-07-12-meeplecard-css-drift-audit.md` (§4.4 friction-functions, §3 barriera MAJOR "2 build*Connections ordine invertito" + "3 status-mapping enum incompleto")
**Branch:** `feature/issue-2860-consolidate-connections-status` da `main-dev`
**Dipendenze:** nessuna.

---

## 1. Problema

L'audit ha rilevato due cluster di funzioni-doppione nella family MeepleCard:

1. **Due famiglie `build*Connections` parallele**, type-incompatibili, con builder omonimi.
2. **Tre catene di status-mapping** PDF non riconciliate, una con enum incompleto (`use-kb-detail` omette Chunking/Embedding/Uploading/Pending).

La verifica sul codice mostra che i due cluster hanno natura diversa (come nel caso C1, la premessa "consolidare in uno" dell'audit va calibrata sulla realtà).

### 1.1 build*Connections — due famiglie NON fondibili

| | `connection-bar/build-connections.ts` | `meeple-card/nav-items/build*Connections.ts` |
|---|---|---|
| Firma | `build*Connections(counts)` | `build*Connections(counts, handlers)` |
| Output | `ConnectionPip[]` (icon nel payload, `count` required, `isEmpty`) | `ConnectionChipProps[]` (icon derivata, `count` opzionale, `items`/`onCreate`/`onClick`/`href`) |
| Reso da | `ConnectionBar` (stateless, cascade-nav) | `ConnectionChipStrip`→`ConnectionChip` (stateful) |
| Superficie | **detail page** (`GameDetailDesktop`, `PlayerConnectionBar`, `AgentCharacterSheet`) — ~5 consumer | **footer card** (`MeepleAgentCard`, `MeepleKbCard`, …) — ~21 consumer |
| Semantica slot | "a cosa è connesso X" (game/kb/chat) | nav + **azioni** (Reindex, Download, Config, Chunks) |

Solo 3 campi in comune (`entityType`, `count`, `label`); modelli icon/count/interaction incompatibili → **non fondibili in un tipo/builder unico** (comprometterebbe interaction model, state, variant; ~40 consumer). Il vero difetto non è "due famiglie" ma: (a) i **nomi collisi** (`buildAgentConnections` esiste in entrambe, type-incompatibile → confonde chi porta un mockup); (b) lo **slot-order senza gate** (può driftare in silenzio).

### 1.2 status-mapping — 3 catene, una col bug

Enum canonico `ProcessingState` (`lib/api/schemas/kb-docs.schemas.ts:18-29`, esportato come union): `Pending | Uploading | Extracting | Chunking | Embedding | Indexing | Ready | Failed`. Tutti e 3 i mapper → `'processing' | 'indexed' | 'failed' | 'none'`:

| Input | Mapper 1 (`drawer-helpers.ts`) | Mapper 2 (`kb-utils.ts`) | Mapper 3 (`use-kb-detail.ts`) |
|---|---|---|---|
| Pending | none | processing | **GAP → none** |
| Uploading | processing | processing | **GAP** (si aspetta `uploaded`) |
| Chunking | processing | processing | **GAP → none** |
| Embedding | processing | processing | **GAP → none** |
| Extracting/Indexing/Ready/Failed | ✓ | ✓ | ✓ |

Mapper 3 lowercasa l'input, si aspetta `uploaded` (non `Uploading`), e **manca** Chunking/Embedding/Pending/Uploading → un PDF in quegli stati mostra `none` invece di `processing` (**bug reale**). In più, disaccordo semantico su Pending: `none` (M1) vs `processing` (M2).

## 2. Decisioni (brainstorm 2026-07-13)

1. **Parte A → disambigua + lock (non merge).** Rinominare la famiglia piccola `connection-bar` → `build*ConnectionPips` (elimina la collisione di nome, ~5 consumer); snapshot-lock dello slot-order di ENTRAMBE le famiglie; nota nella decision-table.
2. **Parte B → modulo condiviso.** Unificare i 3 mapper in `lib/kb/processing-status.ts`, case-insensitive + alias-aware, con enum-coverage a compile-time (`satisfies`) + runtime.
3. **Pending → `processing`** (canonico, coerente col Mapper 2 completo).

## 3. Parte A — disambigua + lock `build*Connections`

**Rename (solo famiglia piccola).** I 9 builder in `connection-bar/build-connections.ts` da `build<Entity>Connections` → **`build<Entity>ConnectionPips`** (output `ConnectionPip[]` esplicito nel nome). Aggiornare:
- il barrel `connection-bar/index.ts`;
- tutti i consumer (grep-verificati: `GameDetailDesktop`, `AgentCharacterSheet`, `PlayerConnectionBar`, `useConnectionBarNav`, ed eventuali detail-strip — ~5-8 file);
- il test `connection-bar/__tests__/build-connections.test.ts`.

I 9 builder in `nav-items/` **restano** `build*Connections` (famiglia primaria card, ~21 consumer, zero rename). Risolta la collisione: non esistono più due `buildAgentConnections` type-incompatibili.

**Snapshot / lock slot-order (entrambe le famiglie).** Ogni builder deve avere un test che asserisce la **sequenza esatta di `entityType` (e label)** emessa:
- `nav-items/__tests__/build*Connections.test.ts` (8 file esistenti) — verificare che ognuno asserisca l'ordine-slot; completare dove manca.
- `connection-bar/__tests__/build-connections.test.ts` — estendere a coprire tutti e 9 i builder con asserzione d'ordine.

Così l'"inverted slot order" non può più driftare in silenzio (documenta anche che le due famiglie sono intenzionalmente diverse).

**Doc.** Nota in `card-decision-table.md` (§ Connection builders): `ConnectionPip`/`build*ConnectionPips` → `ConnectionBar` (detail-page cascade-nav); `ConnectionChipProps`/`build*Connections` → `ConnectionChipStrip` (footer card, nav+azioni). Quale usare per quale superficie.

## 4. Parte B — modulo status-mapping unificato

**Nuovo modulo** `apps/web/src/lib/kb/processing-status.ts` (funzione pura, no React):

```ts
import type { ProcessingState } from '@/lib/api/schemas/kb-docs.schemas';

export type KbDisplayStatus = 'processing' | 'indexed' | 'failed' | 'none';

// Esaustivo sull'enum canonico: un nuovo ProcessingState → errore TS al build.
const CANONICAL = {
  pending: 'processing',   // DEC-Q2: Pending → processing
  uploading: 'processing',
  extracting: 'processing',
  chunking: 'processing',
  embedding: 'processing',
  indexing: 'processing',
  ready: 'indexed',
  failed: 'failed',
} satisfies Record<Lowercase<ProcessingState>, KbDisplayStatus>;

// Alias/varianti legacy (endpoint /pdfs/{id}/text: payload lowercase diverso).
const ALIASES: Record<string, KbDisplayStatus> = {
  completed: 'indexed',    // alias di Ready
  uploaded: 'processing',  // alias di Uploading
  processing: 'processing',
};

export function mapProcessingStateToDisplayStatus(
  state: string | null | undefined
): KbDisplayStatus {
  const k = String(state ?? '').trim().toLowerCase();
  return (CANONICAL as Record<string, KbDisplayStatus>)[k] ?? ALIASES[k] ?? 'none';
}
```

- **Case-insensitive + alias-aware** → assorbe i 3 formati di input (PascalCase canonico, lowercase, `uploaded`/`completed`). Corregge il bug del Mapper 3 (Chunking/Embedding/Pending/Uploading → `processing`).
- `satisfies Record<Lowercase<ProcessingState>, …>` = **enum-coverage a compile-time**.

**Sostituzione dei 3 mapper** (delegano al modulo; firme pubbliche invariate dove hanno consumer, per minimizzare churn):
- `extra-meeple-card/drawer-helpers.ts` `mapProcessingStateToStatus(state)` → `return mapProcessingStateToDisplayStatus(state)`.
- `library/kb-utils.ts` `mapToIndexingStatus(input)` → `return mapProcessingStateToDisplayStatus(input.processingState ?? input.processingStatus)`.
- `use-kb-detail.ts` `statusMap` → rimpiazzato dalla chiamata al modulo (bug risolto).

## 5. Testing

- **`lib/kb/__tests__/processing-status.test.ts`** (enum-coverage):
  - Per ciascuno degli 8 stati canonici (PascalCase) + le loro varianti lowercase + i 3 alias (`completed`/`uploaded`/`processing`) → asserisce il `KbDisplayStatus` atteso.
  - Asserisce esplicitamente `Pending → 'processing'` e `Chunking/Embedding/Uploading → 'processing'` (i gap del Mapper 3).
  - `null`/`undefined`/`''`/stringa ignota → `'none'`.
  - L'esaustività sugli 8 canonici è garantita a compile-time dal `satisfies` (un nuovo enum value rompe il build).
- **Snapshot slot-order** (Parte A): ogni builder delle due famiglie ha un test d'ordine (vedi §3).
- **Verifica finale:** `pnpm test` mirato (kb/status + connection builders), `pnpm typecheck`, `pnpm lint`, `pnpm build`.

## 6. Strategia TDD (ordine)

1. **Parte B**: `processing-status.ts` + `processing-status.test.ts` (rosso→verde); poi sostituisco i 3 mapper e aggiorno/riduco i loro test esistenti (i test dei mapper diventano thin, o si spostano nel test del modulo).
2. **Parte A**: rename `connection-bar` builders + barrel + consumer + test (typecheck-guidato); poi snapshot slot-order su entrambe le famiglie; poi doc.

Le due parti sono indipendenti; stesso PR.

## 7. Scope

**In scope:** rename `connection-bar` builders + snapshot slot-order (entrambe le famiglie) + doc; modulo `processing-status` + sostituzione dei 3 mapper + enum-coverage test.

**Fuori scope (deferiti):**
- Merge dei tipi `ConnectionPip`/`ConnectionChipProps` → rifiutato (non fondibili, §1.1).
- Rename dei builder `nav-items` → non necessario (collisione già risolta rinominando `connection-bar`).
- Verificare col BE il payload `uploaded` dell'endpoint `/pdfs/{id}/text` → il modulo lo gestisce via alias; un allineamento BE è follow-up separato.

## 8. Rischi

| Rischio | Mitigazione |
|---|---|
| Rename `connection-bar` builders rompe consumer non trovati | Rename typecheck-guidato (`pnpm typecheck` fallisce su ogni consumer non aggiornato); grep pre-rename |
| Cambio semantica Pending (`none`→`processing`) regredisce il game-detail drawer | Deciso in Q2; delta visibile atteso e più corretto (badge "in elaborazione" per Pending) |
| Il modulo unificato cambia output per stati che un mapper trattava diversamente | Enum-coverage test esplicita la matrice attesa; i 3 mapper convergono sul canonico (è l'obiettivo) |
| Alias `uploaded`/`completed` non esaustivi vs payload BE reale | Default `none` sicuro; test copre i casi noti; disallineamento BE = follow-up |

## 9. Acceptance criteria

- [ ] `connection-bar` builders rinominati `build*ConnectionPips`; barrel + consumer + test aggiornati; `pnpm typecheck` verde.
- [ ] Ogni builder di entrambe le famiglie ha un test che asserisce la sequenza-slot esatta.
- [ ] `card-decision-table.md` documenta la split delle 2 famiglie connection.
- [ ] `lib/kb/processing-status.ts` presente; `mapProcessingStateToDisplayStatus` case-insensitive + alias-aware; Pending→processing; enum-coverage a compile-time (`satisfies`) + runtime.
- [ ] I 3 mapper (`drawer-helpers`, `kb-utils`, `use-kb-detail`) delegano al modulo; il gap Chunking/Embedding/Pending/Uploading del Mapper 3 è corretto.
- [ ] `processing-status.test.ts` verde (8 canonici + lowercase + 3 alias + null/ignoto→none).
- [ ] `pnpm test` mirato, `pnpm typecheck`, `pnpm lint`, `pnpm build` verdi.

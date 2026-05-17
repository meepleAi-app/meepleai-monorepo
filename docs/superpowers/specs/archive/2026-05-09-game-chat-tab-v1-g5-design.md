# Game Chat Tab V2 — G1 Citation + G5 Confidence — Design Spec

**Date**: 2026-05-09
**Author**: Brainstorming session (post-G3 G1+G5 chat-in-game)
**Issue**: [#915](https://github.com/meepleAi-app/meepleai-monorepo/issues/915)
**Mockup**: [`admin-mockups/design_files/sp4-game-chat-tab.html`](../../../admin-mockups/design_files/sp4-game-chat-tab.html)
**Parent spec**: [`2026-05-09-game-night-user-flow-design.md`](./2026-05-09-game-night-user-flow-design.md) §G1, §G5
**Status**: ✅ COMPLETED 2026-05-10 (issue #915 CLOSED, PR #918 — Game Chat Tab V2 G1+G5)

---

## 1. Contesto

Implementa **G1** (lookup rapido con citazione autoritativa) e **G5** (disclosure calibrata dell'incertezza) del flusso "serata di gioco". Sostituisce il rendering V1 `chat-unified` per la sola route `/library/games/[gameId]?tab=aiChat` con 12 nuovi componenti V2 puri, riusa i design token v2 (`--c-chat`, `--c-agent`, `--c-kb`, `--c-success/warning/danger`) e il pattern Wave B.1 (componente puro + orchestrator + hook).

### 1.1 Scope-frame

**In scope:**
- 12 nuovi componenti V2 sotto `apps/web/src/components/v2/game-chat/`
- 1 nuovo hook `useGameChat(gameId, agent)`
- Modifica `apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx` per montare `GameChatTabV2` nel branch `tab === 'aiChat'`
- Audit + rimozione componenti V1 `chat-unified` usati esclusivamente da quella route

**Out of scope:**
- `/chat/[threadId]` route (resta V1)
- `/kb/[id]` page implementation (G4, separato)
- Backend changes (`QaResponse` già espone tutto)
- E2E Playwright (rinviato a plan G2 fast-resume)
- Streaming token-by-token (Q6 brainstorm scelse risposta atomica con spinner)

### 1.2 Decisioni di design (validate via brainstorming)

| # | Decisione | Scelta |
|---|---|---|
| 1 | Mockup approach | A1 — `sp4-game-chat-tab.html` dedicato (creato) |
| 2 | V1/V2 strategy | A — sostituzione totale + rimozione legacy V1 |
| 3 | Citation behavior | C — hybrid modal preview + footer "Apri KB" gated by G4 |
| 4 | Backend changes | Zero — `QaResponse` già completo |
| 5 | Confidence logic | C — 3-tier color + `isLowQuality` triggers disclaimer |

### 1.3 Vincoli SMART (da spec G1+G5)

| Goal | Vincolo | Misurabile |
|---|---|---|
| **G1.1** | ≥90% query con almeno 1 citation chip | Test integrazione su 10 fixture |
| **G1.2** | Citation chip cliccabile apre modal in <300ms | Performance test, non bloccante |
| **G5.1** | 100% risposte con `isLowQuality === true` mostrano `LowConfidenceDisclaimer` | Unit test FSM |
| **G5.2** | 3-tier confidence color (≥0.80 verde · 0.50-0.80 arancione · <0.50 rosso) | Snapshot test |
| **Q6** | Latency P95 ≤ 10s con spinner accettabile | Backend SLA, non oggetto di questo PR |

---

## 2. Architecture

```
apps/web/src/
├── app/(authenticated)/library/games/[gameId]/page.tsx
│   └── modifica: branch tab === 'aiChat' rende <GameChatTabV2 />
├── components/v2/game-chat/                            ← NUOVO
│   ├── GameChatTabV2.tsx        (orchestrator)
│   ├── ChatBubble.tsx           (puro)
│   ├── CitationChip.tsx         (puro)
│   ├── CitationModal.tsx        (controllato — restyle V2 di V1 PdfPageModal)
│   ├── ConfidenceBadge.tsx      (puro — 3 fasce)
│   ├── LowConfidenceDisclaimer.tsx (puro)
│   ├── OutOfContextActions.tsx  (puro)
│   ├── ChatInputBar.tsx         (controllato)
│   ├── SuggestedPrompts.tsx     (puro)
│   ├── TypingIndicator.tsx      (puro)
│   ├── GameChatHeader.tsx       (puro)
│   ├── GameChatSidebar.tsx      (controllato — desktop only)
│   └── index.ts                 (barrel)
└── hooks/queries/
    └── useGameChat.ts            ← NUOVO
```

**Rendering tree** (per `?tab=aiChat`):

```
GameChatTabV2 (orchestrator)
├── GameChatHeader (sticky top)
├── desktop: GameChatSidebar (left, 260px) — agent switch + history rail
├── ChatMessageList (children)
│   └── per messaggio:
│       ├── ChatBubble user
│       └── ChatBubble agent
│           ├── CitationChip[] (riga citazioni)
│           ├── ConfidenceBadge (riga confidenza, sempre visibile per agent)
│           ├── LowConfidenceDisclaimer (condizionale: isLowQuality===true)
│           └── OutOfContextActions (condizionale: out-of-context state)
├── TypingIndicator (durante 'submitting')
├── SuggestedPrompts (sopra l'input bar)
└── ChatInputBar (sticky bottom)

CitationModal (portal, controllato dal CitationChip)
```

---

## 3. Component Contracts

### 3.1 Orchestrator: `GameChatTabV2`

```ts
interface GameChatTabV2Props {
  readonly gameId: string;
  readonly initialAgent?: 'tutor' | 'arbitro';  // default: 'tutor'
}

export function GameChatTabV2({ gameId, initialAgent = 'tutor' }: GameChatTabV2Props): ReactElement;
```

Compone tutti i 12 sotto-componenti. Wraps `useGameChat`. Gestisce FSM (vedi §4) e routing tra stati.

### 3.2 Pure components

| Component | Props essenziali |
|---|---|
| `ChatBubble` | `role: 'user' \| 'agent', content: ReactNode, agentName?: string, avatar?: string, children?: ReactNode` |
| `CitationChip` | `pageNumber: number, sectionTitle: string, snippet?: string, onClick: () => void` |
| `ConfidenceBadge` | `score: number, kind?: 'inline' \| 'compact'` (calcola tier internamente — vedi §4.2) |
| `LowConfidenceDisclaimer` | `summary: string, alternatives: ReadonlyArray<{ label: string, kind: 'kb' \| 'external', url?: string }>` |
| `OutOfContextActions` | `actions: ReadonlyArray<{ kind: 'switch-game' \| 'find-agent' \| 'stay', label: string, onClick: () => void }>` |
| `SuggestedPrompts` | `prompts: ReadonlyArray<{ category: 'A' \| 'B' \| 'C' \| 'E' \| 'F', text: string, onClick: () => void }>, groupLabel?: string` |
| `TypingIndicator` | `elapsedMs?: number, budgetMs?: number, hint?: string` |
| `GameChatHeader` | `gameTitle: string, gameIcon: string, agent: 'tutor' \| 'arbitro', subtitle?: string` |

### 3.3 Controlled components

| Component | Props essenziali |
|---|---|
| `CitationModal` | `citation: Citation \| null, open: boolean, onClose: () => void, onOpenInKb?: () => void \| undefined` (footer button hidden se `onOpenInKb === undefined`) |
| `ChatInputBar` | `value: string, onChange: (next: string) => void, onSubmit: (question: string) => void, disabled?: boolean, placeholder?: string` |
| `GameChatSidebar` | `gameTitle: string, gameIcon: string, currentAgent: 'tutor' \| 'arbitro', history: ReadonlyArray<ChatHistoryItem>, onAgentChange: (next: 'tutor' \| 'arbitro') => void, onHistorySelect: (id: string) => void` |

### 3.4 Hook contract

```ts
interface ChatMessage {
  readonly id: string;
  readonly role: 'user' | 'agent';
  readonly content: string;
  readonly citations?: ReadonlyArray<Citation>;
  readonly overallConfidence?: number;
  readonly isLowQuality?: boolean;
  readonly outOfContext?: boolean;
  readonly createdAt: string;
}

interface UseGameChatResult {
  readonly messages: readonly ChatMessage[];
  readonly isLoading: boolean;
  readonly isError: boolean;
  readonly currentAgent: 'tutor' | 'arbitro';
  readonly ask: (question: string) => Promise<void>;
  readonly switchAgent: (next: 'tutor' | 'arbitro') => void;
}

export function useGameChat(gameId: string, initialAgent?: 'tutor' | 'arbitro'): UseGameChatResult;
```

L'hook usa l'API esistente del backend (stesso endpoint usato da chat-unified V1, da identificare nel plan).

---

## 4. Logic / Decisions

### 4.1 FSM `GameChatTabV2`

```
[idle] ──user submits──> [submitting]
[submitting] ──response received──> classifyState(response)
  classifyState:
    if (response.outOfContext) → [responding-out-of-context]
    elif (response.isLowQuality) → [responding-low-conf]
    else → [responding-normal]
[responding-*] ──auto──> [idle]   (subito; gli stati responding sono solo "tag" del rendering)
[submitting] ──API error──> [error]
[error] ──user retries OR ignores──> [idle]
```

Non c'è "streaming" come in V1 (Q6 brainstorm scelse atomica con spinner). `submitting` mostra `TypingIndicator`; `responding-*` rendono il messaggio agent completo.

### 4.2 Confidence tier logic

```ts
function getConfidenceTier(overallConfidence: number): 'alta' | 'media' | 'bassa' {
  if (overallConfidence >= 0.80) return 'alta';
  if (overallConfidence >= 0.50) return 'media';
  return 'bassa';
}
```

- **Color badge**: tutti e 3 i tier sono visibili sempre per ogni messaggio agent (mappano a `--c-success/warning/danger`)
- **Disclaimer banner** (`LowConfidenceDisclaimer`): appare SOLO se `response.isLowQuality === true` (decisione server-side, indipendente dal tier colore)
- **Out-of-context state**: detectato da `response.outOfContext === true` (oppure `citations.length === 0 && answer matches "no information"` come fallback se backend non espone il flag — da verificare nel plan)

### 4.3 Citation behavior (hybrid C)

```ts
// CitationChip onClick
const onChipClick = () => {
  setSelectedCitation(citation);  // open modal
};

// CitationModal footer
{onOpenInKb && (
  <button onClick={onOpenInKb}>📖 Apri nella KB</button>
)}
```

**G4 gate**: in questo PR, `onOpenInKb` è passato come `undefined` ovunque (footer button nascosto). Quando `/kb/[id]` (G4) atterrerà, basta cambiare il props pass:

```ts
onOpenInKb={() => router.push(`/kb/${gameId}#chunk-${citation.chunkId}`)}
```

Niente refactor del modal stesso.

### 4.4 Agent switch

`Tutor` (default) e `Arbitro` sono i due agenti del flusso "serata di gioco" (categorie domanda Q4: A/B/C/E/F, niente strategia D). `GameChatSidebar` espone toggle desktop. Su mobile: il toggle vive come secondary button nel `GameChatHeader` (decisione UX delegata al plan).

Ogni messaggio agent eredita il `currentAgent` al momento della risposta — se l'utente switcha mid-thread, i messaggi precedenti mostrano l'agent originale (no rewrite della cronologia).

---

## 5. Removal plan V1 (audit-first)

### 5.1 Audit script

```bash
grep -rn "from '@/components/chat-unified/" apps/web/src --include="*.tsx" --include="*.ts"
```

Output → categorizzare ogni import in:
1. **Solo da `?tab=aiChat`** → DELETE
2. **Anche da `/chat/[threadId]`** → KEEP
3. **Anche da admin (`/agents/debug-chat`)** → KEEP

### 5.2 Piano di rimozione

Il PR aprirà con DUE commit separati:
1. `feat(web): GameChatTabV2 wired on /library/games/[id]?tab=aiChat`
   - Tutti i 12 nuovi componenti V2
   - Modifica `library/games/[gameId]/page.tsx`
   - Hook `useGameChat`
   - Tests
2. `chore(web): remove unused chat-unified V1 components after V2 migration`
   - Solo i file della categoria 1 sopra
   - Cancellazione test relativi
   - `pnpm typecheck` e `pnpm lint` devono passare

Lista esatta dei file da cancellare sarà nel **plan**, dopo aver eseguito l'audit script.

### 5.3 Rollback strategy

Se la sostituzione introduce regressioni:
- Revert del commit `feat(web): GameChatTabV2...` ripristina V1 (i file rimossi nel commit `chore` sono ancora nei file v1 fino a quel commit)
- In emergenza: cherry-pick del solo `chore` revert per ripristinare i file V1

---

## 6. Testing strategy

### 6.1 Unit (Vitest)

Per ogni pure component (boundary testing pattern Wave B.1):

| Component | Scenari |
|---|---|
| `ChatBubble` | user · agent · agent-with-children · long content (overflow) |
| `CitationChip` | render base · click → onClick called · accessible name |
| `CitationModal` | open=true · open=false · footer KB hidden when `onOpenInKb===undefined` · click footer → onOpenInKb called |
| `ConfidenceBadge` | tier alta (0.92) · media (0.65) · bassa (0.42) · edge 0.0 · edge 1.0 · boundary 0.50 e 0.80 |
| `LowConfidenceDisclaimer` | empty alternatives · 1 alt · N alt · alt esterna (link target=_blank) |
| `OutOfContextActions` | 3 action default · click ognuno → callback |
| `ChatInputBar` | controlled · disabled · submit on Enter · empty value blocca submit |
| `SuggestedPrompts` | 0 prompts (non rende sezione) · 5 prompts categorizzati · click → callback |
| `TypingIndicator` | base · con elapsed/budget · con hint |
| `GameChatHeader` | tutor · arbitro · senza subtitle |
| `GameChatSidebar` | switch agent · history click · empty history |
| `useGameChat` | mocked API · FSM transitions (idle→submitting→responding-*) · error handling · agent switch preserva history |

### 6.2 Integration (Vitest + MSW)

| Scenario | Setup | Asserzione |
|---|---|---|
| Happy path | API ritorna `overallConfidence=0.92`, citations, isLowQuality=false | Tier badge alta · 2 chip cliccabili · no disclaimer |
| Low confidence | `isLowQuality=true`, `overallConfidence=0.42` | Disclaimer rendered · badge bassa rossa |
| Out of context | `outOfContext=true`, citations vuote | OutOfContextActions rendered · 3 action pill |
| Citation click | Click chip | Modal open · snippet visible · footer KB hidden (onOpenInKb=undefined in questo PR) |
| Agent switch mid-thread | switchAgent('arbitro') | currentAgent aggiornato · cronologia preserva agent originale per messaggi precedenti |

### 6.3 E2E (out of scope questo PR)

Plan G2 fast-resume potrà aggiungere test Playwright per:
- Round-trip reale message ≤10s
- Background → foreground preserva scroll
- Tap citation modal → "Apri KB" (quando G4 atterra)

### 6.4 Acceptance criteria misurabili

- [ ] **G1.1**: 10/10 fixture di test integrazione hanno almeno 1 `<CitationChip>` rendered
- [ ] **G5.1**: 100% test che mockano `isLowQuality=true` mostrano `<LowConfidenceDisclaimer>`
- [ ] **G5.2**: snapshot test conferma classi `.alta` (≥0.80), `.media` (≥0.50), `.bassa` (<0.50)
- [ ] **Lint+Typecheck**: 0 errori, no nuovi warning oltre baseline
- [ ] **No regressione su `/chat/[threadId]`**: i suoi test esistenti passano

---

## 7. Open questions (da risolvere in implementation plan)

- **OQ1**: API endpoint esatto del backend per `useGameChat.ask()` — `POST /api/v1/qa`? `POST /api/v1/agents/{id}/ask`? Audit V1 da fare nel plan.
- **OQ2**: Backend espone `outOfContext: boolean` o va inferito da `citations.length === 0 && match("no information")`? Da verificare in `QaResponse` schema completo.
- **OQ3**: Mobile UX per agent switch — toggle nel header, drawer dedicato, o tab interna? Decisione UX delegata al plan.
- **OQ4**: `CitationModal` riusa il `PdfPageModal` V1 esistente con restyle, o è un componente nuovo? Se riuso, il `PdfPageModal` V1 va categorizzato KEEP nell'audit §5.
- **OQ5**: Persistenza chat history — uso lo stesso storage di chat-unified V1 (probabilmente Zustand + sessionStorage), o nuovo? Audit V1 nel plan.
- **OQ6**: Suggested prompts — statici (hardcoded per agente) o dinamici (fetched da `/api/v1/agents/{id}/suggested-prompts`)? Per Alpha, statici sono sufficienti — decisione delegata al plan.

---

## 8. Riferimenti

- Mockup: [`admin-mockups/design_files/sp4-game-chat-tab.html`](../../../admin-mockups/design_files/sp4-game-chat-tab.html) (creato in questa sessione)
- Mockup precursore (Nanolith): [`admin-mockups/design_files/nanolith-runthrough-setup-chat.html`](../../../admin-mockups/design_files/nanolith-runthrough-setup-chat.html)
- Spec parent: [`2026-05-09-game-night-user-flow-design.md`](./2026-05-09-game-night-user-flow-design.md) §G1, §G5
- Plan G3 reference (pattern subagent-driven): [`docs/superpowers/plans/2026-05-09-game-night-g3-hub-recents.md`](../plans/2026-05-09-game-night-g3-hub-recents.md)
- V2 migration matrix: [`docs/for-developers/frontend/v2-migration-matrix.md`](../../for-developers/frontend/v2-migration-matrix.md)
- V1 chat-unified components: `apps/web/src/components/chat-unified/` (41 file)
- Schema `QaResponse`: `apps/web/src/types/domain.ts:179-194`
- BC backend KnowledgeBase: `apps/api/src/Api/BoundedContexts/KnowledgeBase/`
- Issue: [#915](https://github.com/meepleAi-app/meepleai-monorepo/issues/915)

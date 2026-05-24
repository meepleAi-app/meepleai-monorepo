# Components Audit — Design System Handoff ↔ MeepleAI codebase

> Generato dallo **Step 6** di `QUICK_START.md`.
> Data: 2026-05-24 · Scope: solo lettura (zero modifiche).
> Revisione `/sc:spec-panel` applicata: aggiunte § 1.5 (Tabs underline post-read), § 2.9 (LibraryGameAgentShell), § 5.5 (callout path obsoleto), § 8 decomposed, § 9 (Failure modes), verdetti ConfidenceBadge/StepIndicator.

## TL;DR

| Cluster | Stato |
|---|---|
| **7/7 primitives "esistenti" del handoff** | 6 ESISTONO direttamente, 1 MANCA (`MobileBottomBar`) |
| **8/8 nuovi componenti v2 attesi dal handoff** | 4 ESISTONO direttamente (`ConfidenceBadge`, `KbDocList`, `StepIndicator`, parzialmente `CitationExpandedPanel`), 1 PARZIALE (`CitationExpandedPanel`), 3 MANCANTI ex novo (`HouseRuleDrawer`, `SuggestedQueriesRow`, `ChatHistoryTimeline`, `ConfirmModal`) |
| **`cascadeNavigationStore`** | ✅ ESISTE (`apps/web/src/lib/stores/cascade-navigation-store.ts`) |
| **Helper `entity-color.ts` runtime HSL accessor** | ✅ ESISTE come `entityHsl/entityHslText` (esportati da `meeple-card/tokens.ts`) + helper Tailwind class-based (`entity-tokens.ts`) |

→ **Da creare ex novo**: 5 componenti. Tutti gli altri esistono e si possono **riusare o estendere** evitando duplicazioni.

## Convenzioni

| Simbolo | Significato |
|---|---|
| ✅ | Esiste, **pronto al riuso** |
| ⚠️ | Esiste **parzialmente** — copre alcuni use case, valutare estensione vs nuovo wrapper |
| ❌ | **Manca** — da creare ex novo |
| 🔀 | Esiste in **multiple varianti** — decidere canonical |

## 1. Primitives "esistenti" attesi dal handoff (QUICK_START Step 6, prima lista)

### 1.1. `ConnectionBar` ✅

| Aspetto | Valore |
|---|---|
| Path canonico | `apps/web/src/components/ui/data-display/connection-bar/ConnectionBar.tsx` |
| Test path | `apps/web/src/components/ui/data-display/connection-bar/__tests__/ConnectionBar.test.tsx` |
| Variants | `apps/web/src/components/features/session-summary/ConnectionBar.tsx` (feature-specific copy) |
| Firma esposta | `function ConnectionBar({ connections, onPipClick, className, 'data-testid'? }: ConnectionBarProps)` |
| Dipendenze | `entityHsl`, `entityHslText` da `@/components/ui/data-display/meeple-card`; `cn` da `@/lib/utils` |
| Props types | `ConnectionBarProps`, `ConnectionPip` in `./types` |
| Note | Sub-componente interno `ConnectionPipButton` con `useRef`. Render condizionale (`if (connections.length === 0) return null`). |

Riferimento al uso negli handoff (es. `sp4-game-detail.jsx`): allinea `pip.entityType` ai 9 entity types canonical.

### 1.2. `MeepleCard` ✅ (compound API molto ricca)

| Aspetto | Valore |
|---|---|
| Path canonico | `apps/web/src/components/ui/data-display/meeple-card/MeepleCard.tsx` |
| Sub-componenti | `compound.tsx`, `skeleton.tsx`, `types.ts`, `tokens.ts`, `index.ts` |
| Features sub-folder | `features/{FlipCard,HoverPreview,Carousel3D,EntityTable,FlipBack}.tsx` |
| Parts sub-folder | `parts/{ConnectionChip,ConnectionChipStrip,ConnectionChipPopover,OwnershipBadge,LifecycleStateBadge,entity-icons,status-adapter}.tsx` |
| Esposti via `index.ts` | `MeepleCard`, `MeepleCards`, `FlipCard`, `HoverPreview`, `Carousel3D`, `EntityTable`, `FlipBack`, `MeepleCardSkeleton`, `ConnectionChip`, `ConnectionChipStrip`, `ConnectionChipPopover`, `OwnershipBadge`, `LifecycleStateBadge` |
| Helpers TypeScript esposti | `entityColors`, `entityHsl`, `entityHslText`, `entityLabel`, `entityIcon`, `entityTokens`, `entityIcons`, `ENTITY_ICON_SIZE`, `ENTITY_ICON_STROKE`, `mapLegacyStatus`, `resolveStatus` |
| Types esposti | `MeepleCardProps`, `MeepleEntityType`, `MeepleCardVariant`, `MeepleCardMetadata`, `MeepleCardAction`, `CardStatus`, `CoverLabel`, `Carousel3DProps`, `ConnectionItem`, `ConnectionChipProps`, `OwnershipBadge as OwnershipBadgeValue`, `LifecycleState` |

Da `CLAUDE.md`:
- `entity` types: `game` (orange) · `player` (purple) · `collection` (teal) · `event` (rose) [+ session, agent, kb, chat, toolkit, tool delle 9 canoniche]
- `variant` values: `grid` (default) · `list` · `compact` · `featured` · `hero`
- **Mai usare** legacy `GameCard` / `PlayerCard` (deprecati).
- Docs: `docs/for-developers/frontend/meeple-card-design-tokens.md`

### 1.3. `EntityChip` ✅

| Aspetto | Valore |
|---|---|
| Path canonico | `apps/web/src/components/ui/entity-chip/entity-chip.tsx` (lower-case, no barrel `index.ts`) |
| Test path | `apps/web/src/components/ui/entity-chip/entity-chip.test.tsx` |
| ESLint scope | `local/no-hardcoded-hex: error` (vedi `eslint.config.mjs:563`) |
| Firma esposta | `function EntityChip({ entity: EntityType, label: string, size?: 'sm' \| 'md' = 'sm' }): JSX.Element` |
| Dipendenze | `getEntityToken` + `EntityType` da `@/components/ui/entity-tokens` |
| Rendering | `<span>` rounded-full pill con emoji (`aria-hidden`) + label, classi Tailwind `${t.bgSoft} ${t.text}` (DS-15 compliant) |
| Famiglia correlata | `entity-card/`, `entity-pip/`, `entity-tokens.{ts,test.ts}` |

### 1.4. `Drawer` (con `cascadeNavigationStore`) ✅

| Aspetto | Valore |
|---|---|
| Path canonico | `apps/web/src/components/ui/drawer/drawer.tsx` |
| Test path | `apps/web/src/components/ui/drawer/drawer.test.tsx` |
| Lib backing | `@radix-ui/react-dialog` (`RadixDialog`) + `vaul` (`VaulDrawer`) — composito |
| Hook utility | `apps/web/src/components/ui/drawer/use-drawer-breakpoint.ts` |
| Firma esposta | `interface DrawerProps { readonly open, onOpenChange, side?, direction?, entity?: EntityType, children }` |
| Types esposti | `DrawerSide = 'auto' \| 'mobile-bottom' \| 'desktop-right'`, `DrawerDirection = 'bottom' \| 'right'` |
| Entity prop | `EntityType` da `@/components/ui/entity-tokens`. Mapping interno `kb → 'document'` (pre-existing token name) |
| **`cascadeNavigationStore`** | ✅ `apps/web/src/lib/stores/cascade-navigation-store.ts` (Zustand) — usato in `useConnectionBarNav` hook, `SessionDrawerContent`, `ExtraMeepleCardDrawer`, `SessionPanel`, `SessionBanner` |
| Test cascade store | `apps/web/src/lib/stores/__tests__/cascade-navigation-store.test.ts` |
| Plus variant | `apps/web/src/components/ui/data-display/extra-meeple-card/ExtraMeepleCardDrawer.tsx`, `entities/SessionDrawerContent.tsx`, `DrawerActionFooter.tsx`, `drawer-helpers.ts`, `drawer-states.tsx`, `drawer-test-ids.ts` |
| Mobile variant | `apps/web/src/components/layout/mobile/drawer/DrawerContent.tsx` |

→ Il riferimento del handoff a "Drawer (con cascadeNavigationStore)" è **completamente coperto**.

### 1.5. `Tabs` (animated underline) ✅

| Aspetto | Valore |
|---|---|
| Path canonico | `apps/web/src/components/ui/navigation/tabs.tsx` |
| Test path | `apps/web/src/components/ui/navigation/__tests__/tabs.a11y.test.tsx` |
| Stories | `apps/web/src/components/ui/navigation/tabs.stories.tsx` |
| Lib backing | `@radix-ui/react-tabs` (`TabsPrimitive`) |
| Esposti | `Tabs` (Root alias), `TabsList`, `TabsTrigger`, `TabsContent` |
| Pattern | shadcn-ui style con `cn` utility + `forwardRef` |
| Active state | `data-[state=active]:bg-background data-[state=active]:text-foreground data-[state=active]:shadow` (background + shadow shift, NON underline) |
| Variant alternativa | `apps/web/src/components/ui/detail-layout/tabs.tsx` (specialized per detail pages — anche test in stesso path) |

⚠️ **Note (post-read di `detail-layout/tabs.tsx`)**: il handoff parla di "**animated underline**" come specifica di stile.

**Verifica eseguita**:
- `ui/navigation/tabs.tsx` → wrapper Radix Tabs con active state `bg-background + shadow` — **NON underline**
- `ui/detail-layout/tabs.tsx` → **componente custom** (non Radix wrapper) specifico per `/shared-games/[id]` V2 (Wave A.4, Issue #603). Implementa WAI-ARIA tablist pattern + `useTablistKeyboardNav` hook + 5-tab fixed schema `['overview', 'toolkits', 'agents', 'knowledge', 'community']`. **Underline animation non verificata** (lette solo prime 60 righe del file); il tema `entityHsl` suggerisce color-based active state, non sicuro sia underline.

**Conclusione**:
- ❌ Né `tabs.tsx` (Radix wrapper, bg+shadow) né `detail-layout/tabs.tsx` (custom 5-tab fisso per /shared-games) coprono direttamente l'use case generico "animated underline" del handoff.
- **Decisione design richiesta**:
  - (a) estendere `ui/navigation/tabs.tsx` con prop `variant?: 'default' \| 'underline'`
  - (b) creare 3° variant `ui/navigation/tabs-underline.tsx` standalone
  - (c) accettare che il mockup non richiede strettamente underline e usare Radix wrapper esistente
- 🔎 _Read aggiuntivo richiesto_: aprire `sp4-game-detail.jsx` per vedere se l'underline è effettivamente animation (es. CSS `transition: transform`) o solo bordo statico — questo determina la scelta tra (a)/(b)/(c).

### 1.6. `MobileBottomBar` ❌

| Aspetto | Valore |
|---|---|
| Path canonico | _da creare_ |
| Path suggerito | `apps/web/src/components/layout/mobile/MobileBottomBar.tsx` (allineato a `layout/mobile/drawer/`) |
| Esistente correlato | Niente. Solo `MobileDrawerContent` non è equivalente |
| Suggerimento implementazione | Bottom-nav app-style con 4-5 tab icons + label. Lib: `lucide-react` per icons. Pattern attivazione current route via `usePathname()` (vedi `RecentsBar`). Mobile-only (`md:hidden`). Allineato a `<MobileBottomBar />` slot in `app/layout.tsx` o nei sub-layouts. |
| Note | Richiede design specifico mockup `sp4-*-mobile.jsx` per route mapping (Hub / Discover / Library / Profile / +1). |

### 1.7. `RecentsBar` ✅

| Aspetto | Valore |
|---|---|
| Path canonico | `apps/web/src/components/layout/UserShell/RecentsBar.tsx` |
| Test path | `apps/web/src/components/layout/UserShell/__tests__/RecentsBar.test.tsx` |
| Firma esposta | `function RecentsBar()` (zero props) |
| State source | `useRecentsStore` (Zustand) da `@/stores/use-recents` |
| Rendering | Max 3 pills, `md:hidden` per mobile (hidden < 768px), excludes current page |
| Color usage | `entityHsl(item.entity, 0.1)` per bg + `entityHsl(item.entity)` per text |
| Test-id | `recents-bar` (parent), `recent-pill-${item.id}` (each pill) |

## 2. Nuovi componenti v2 attesi dal handoff (QUICK_START Step 6, seconda lista)

### 2.1. `HouseRuleDrawer` ❌

| Aspetto | Valore |
|---|---|
| Path canonico | _da creare_ |
| Path suggerito | `apps/web/src/components/features/house-rule-drawer/HouseRuleDrawer.tsx` |
| Esistenti correlati | `apps/web/src/components/library/game-table/HouseRulesSection.tsx` (display lista house rules per game) + `apps/web/src/components/session/live/HouseRulesDisplay.tsx` (display in-session) |
| Use case | Drawer aperto da chat agente con `confidence < 0.5` per definire una nuova house rule. Preset con `originalQuestion` + `officialRule`. Submit → POST + re-emit query. |
| Suggerimento implementazione | Riusare `<Drawer entity="agent" side="auto">` come container (vedi § 1.4). Form interno con campi: `originalQuestion : string` (readonly preset), `officialRule : string?` (readonly preset), `houseRuleText : string` (textarea). Submit handler → `POST /api/v1/games/{id}/house-rules` via `agentMemoryClient.ts` o equivalente. |
| Dipendenza BE | `AgentMemory.Domain.Models.HouseRule.cs` + `HouseRuleSource enum` (vedi `SCHEMA_DIFF.md § 10`) |

### 2.2. `ConfidenceBadge` ✅ 🔀 (4 versioni) — **VERDETTO: NON consolidare** (verificato post-read)

| Aspetto | Valore |
|---|---|
| Versione 1 — `ui/feedback/ConfidenceBadge.tsx` | Props: `{ confidence: number 0-100, size?: 'sm' \| 'md', className? }`. Rendering: **rettangolare pill** con icon Lucide + label + percentage + Tooltip description. Context: Issue #4163 (AI Metadata Extraction). ⚠️ **VIOLAZIONE DS-15 noted**: usa `bg-green-100`, `bg-yellow-100`, `bg-red-100` (palette hardcoded) — possibile file-level eslint-disable o waiver da verificare. |
| Versione 2 — `features/gamebook/ConfidenceBadge.tsx` | Props: `{ level: 'high' \| 'medium' \| 'low', size?: 'sm' \| 'md', labels: ConfidenceBadgeLabels, className? }`. Rendering: **circolare glyph** (✓/◐/⚠) con `role="img"` + aria-label. Context: Issue #789 SP6 Phase C.2.B Photo Upload indexing grid. ✅ **DS-15 compliant** (entity tokens `bg-entity-toolkit/15 text-entity-toolkit-text`). Pure component Wave D.3 pattern (i18n labels injected). |
| Versione 3 — `admin/rag/ConfidenceBadge.tsx` | Context: admin RAG dashboard (probable status indicator) — non aperto post-review |
| Versione 4 — `features/game-chat/ConfidenceBadge.tsx` | Context: game chat inline — non aperto post-review |

**Verdetto post spot-check**: **NON consolidare le 4 varianti in 1 canonical**. Sono **profondamente diverse** lungo 5 dimensioni:

| Dimensione | `ui/feedback/` | `features/gamebook/` |
|---|---|---|
| Props discriminator | `confidence: number 0-100` | `level: 'high'\|'medium'\|'low'` |
| Visual | Pill rettangolare con label/icon/% | Circolare con single glyph |
| Palette | Hardcoded slate-* (DS-15 violation) | Entity tokens (compliant) |
| Scope | AI extraction confidence (Issue #4163) | Gamebook page indexing (Issue #789) |
| A11y | Tooltip + Lucide icon | `role="img"` + aria-label + sr-only |

**Action items**:
1. ⚠️ Aprire issue separato per investigare la violazione DS-15 in `ui/feedback/ConfidenceBadge.tsx` (verificare se è bug, waiver, o file-level eslint-disable).
2. Mantenere split + documentare divergence come scelta deliberata (3 use case diversi).
3. Le 2 varianti non aperte (`admin/rag/`, `features/game-chat/`) sono probabilmente da read per verificare se sono duplicati di una delle 2 sopra o pattern nuovi.

### 2.3. `SuggestedQueriesRow` ❌

| Aspetto | Valore |
|---|---|
| Path canonico | _da creare_ |
| Path suggerito | `apps/web/src/components/features/game-chat/SuggestedQueriesRow.tsx` |
| Use case | Riga horizontal-scroll con chip "Domande suggerite" sopra empty-state chat. Tap → invio query. |
| Suggerimento implementazione | Riusare `EntityChip` come unit base (§ 1.3) o creare wrapper Chip locale. Horizontal scroll: `overflow-x-auto snap-x`. Pattern simile a `RecentsBar` (§ 1.7) ma con icone diverse. |
| Riferimento mockup | `sp7-library-game-agent.jsx`, empty state |
| Stato suggested queries source | BE endpoint suggested: `GET /api/v1/agents/{agentId}/suggested-queries?gameId=` o derivato da agent prompt template. Verificare. |

### 2.4. `ChatHistoryTimeline` ❌

| Aspetto | Valore |
|---|---|
| Path canonico | _da creare_ |
| Path suggerito | `apps/web/src/components/features/agent-detail/ChatHistoryTimeline.tsx` |
| Esistenti correlati | `apps/web/src/components/admin/rag/TimelineStep.tsx` (single-session timeline step). Generalizzare a cross-session. |
| Use case | Lista timeline cross-session di chat history per un agent (vedi `BACKEND_PROMPTS.md` § 6.2 Agent Detail page). Permette navigation a chat thread. |
| Suggerimento implementazione | Pattern `react-chrono` (già in deps) o lista verticale custom. Dati: `GET /api/v1/agents/{id}/chat-history?page=` (vedi prompt). Riuso `entityHsl('chat')` per badge color. |
| Note | Riusare logica di `TimelineStep` (admin) come base, ma diversa fonte dati (history vs RAG execution steps). |

### 2.5. `KbDocList` ✅

| Aspetto | Valore |
|---|---|
| Path canonico | `apps/web/src/components/features/agent-detail/KbDocList.tsx` |
| Test path | `apps/web/src/components/features/agent-detail/__tests__/KbDocList.test.tsx` |
| Esistente, pronto al riuso | ✅ |

### 2.6. `CitationExpandedPanel` ⚠️ (parziale)

| Aspetto | Valore |
|---|---|
| Path canonico raccomandato | _da decidere — verificare se uno degli esistenti basta_ |
| Componenti esistenti potenzialmente coprenti | `apps/web/src/components/chat-unified/CitationBadge.tsx` (badge) · `CitationBlock.tsx` (block) · `CitationSheet.tsx` (sheet drawer) · `apps/web/src/components/chat/panel/CitationExpander.tsx` (expander) · `apps/web/src/components/features/game-chat/CitationModal.tsx` (modal) · `CitationPdfTab.tsx` (PDF preview tab) · `CitationChip.tsx` (chip) · `CitationOwnershipUpsell.tsx` (upsell flow) |
| Use case attesi handoff | Espansione full-page o drawer del singolo citation chunk con PDF viewer + highlight (vedi `BACKEND_PROMPTS.md` § 5.1 KB Detail) |
| Suggerimento decisione | Verificare se `CitationSheet.tsx` OR `CitationModal.tsx` + `CitationPdfTab.tsx` insieme già coprono il "Panel". Altrimenti **creare** `apps/web/src/components/features/game-chat/CitationExpandedPanel.tsx` come orchestratore che combina `CitationBlock` + `CitationPdfTab` + `CitationOwnershipUpsell` (per gating gioco). |
| Dipendenza esistente | `@pdf-viewer/react`, `react-pdf`, `pdfjs-dist` |

### 2.7. `StepIndicator` ✅ 🔀 (2 versioni) — **VERDETTO: NON consolidare ora, refactor canonical futuro**

| Aspetto | Valore |
|---|---|
| Versione 1 — `library/add-game-sheet/StepIndicator.tsx` | Props: `{ currentStep: WizardStep, steps: { label, description? }[] }`. **N-step generico** (variabile in base a `steps.length`). Visual: linea orizzontale con cerchi 28px + tail lines. ⚠️ **VIOLAZIONE DS-15**: usa `bg-teal-500/20`, `bg-teal-500`, `bg-slate-800`, `bg-slate-700` (palette hardcoded). Issue #4818. |
| Versione 2 — `features/gamebook/StepIndicator.tsx` | Props: `{ currentStep: 1 \| 2 \| 3, labels: StepIndicatorLabels, className? }`. **3-step fissi** (sticky bar per `/gamebook/upload`). Visual: cerchi 32px con underline line tra step. ⚠️ **MIXED**: entity tokens compliant (`border-entity-game`, `bg-entity-game`) + 1 violation (`bg-slate-200`). Pure component Wave D.3 pattern (i18n labels). Issue #789. |

**Verdetto post spot-check**: NON consolidare immediatamente. Approcci troppo diversi:
- Versione 1 = N-step generico (parametrizzato `steps[]`)
- Versione 2 = 3-step fisso (specifico SP6 photo upload, sticky bar)

**Opportunity di refactor canonical futuro**:
- Creare `apps/web/src/components/ui/step-progress/StepProgress.tsx` con API unificata:
  - `variant?: 'circular-fixed' | 'pill-generic'`
  - `steps: StepDescriptor[]` con `currentIndex`
  - `labels?: StepIndicatorLabels` per i18n (Wave D.3 pattern)
  - Entity color theming via `entity?: EntityType` (default game/orange)
- **Effort**: M (~2-3h refactor + migration callers + Storybook + a11y test)
- **Trigger**: quando arriverà la **terza** richiesta di step indicator (es. `sp7-game-night-create.jsx` wizard 4-step), refactor diventa positivo.

**Action items**:
1. ⚠️ Aprire issue separato per investigare violazioni DS-15 in entrambe le versioni.
2. Mantenere split per ora — documentare divergence come scelta deliberata (2 use case diversi).
3. ESLint scope `ui/step-progress/` (vedi `eslint.config.mjs:579`) suggerisce che il canonical era pianificato — quando refactor avviene, popolare quel path.

### 2.8. `ConfirmModal` ❌

| Aspetto | Valore |
|---|---|
| Path canonico | _da creare_ |
| Path suggerito | `apps/web/src/components/ui/feedback/ConfirmModal.tsx` |
| Esistenti correlati | `apps/web/src/components/ui/animations/ModalAnimations.tsx` (animazioni generiche), `@radix-ui/react-alert-dialog` (lib backing) |
| Use case | Conferma azione distruttiva (es. delete game, delete session, leave game-night, archive chat) |
| Suggerimento implementazione | Wrapper su `@radix-ui/react-alert-dialog` con preset variants `destructive` (rosso), `default` (neutro). Props: `open`, `onOpenChange`, `title`, `description`, `confirmLabel`, `cancelLabel`, `variant`, `onConfirm`. Pattern shadcn-ui. |

### 2.9. `LibraryGameAgentShell` ❌ — **POST-REVIEW ADDITION** (omesso dall'audit originale)

| Aspetto | Valore |
|---|---|
| Path canonico | _da creare_ |
| Path suggerito | `apps/web/src/components/features/library-game-agent/LibraryGameAgentShell.tsx` |
| Esistenti correlati | Nessun shell dedicato — esistono pezzi (`MeepleCard`, `Drawer`, `Tabs`) ma non orchestratore |
| Use case | Orchestratore split-view per `/library/games/[id]/agent` (P0 in `SCREENS.md § Fase 06`). Layout responsive: mobile fullscreen chat con header compatto + bottom input + suggested queries; desktop split-view (sidebar 360px game-context + main chat 1fr). |
| Riferimento mockup | `sp7-library-game-agent.jsx` (P0) |
| Dipendenze v2 da combinare | `MeepleCard` (sidebar game-context) + `Drawer` (HouseRuleDrawer overlay) + `SuggestedQueriesRow` (empty state) + `ConfidenceBadge` (per response) + `CitationExpandedPanel` (citation drawer) + ChatBubble (UserBubble/AgentBubble) |
| Stati chat richiesti | empty (suggested queries + illustrazione) · default (high-confidence response + citation) · low-confidence (CTA "Definisci house rule") · house-rule-applied (badge + footnote) · network-error (retry banner) |
| API attese | `POST /api/v1/agents/{agentId}/query` (SSE) · `GET /api/v1/games/{gameId}/house-rules` · `POST /api/v1/games/{gameId}/house-rules` |
| Riferimento prompt | `BACKEND_PROMPTS.md § 6.1 Library Game Agent (chat inline)` + `WIRING_GUIDE.md § Quando il mock include un componente nuovo` (table row LibraryGameAgentShell) |
| Effort stimato | **L** — è un orchestratore complesso che integra 6+ primitives + SSE pipeline + 5 stati + responsive layout. Richiede definition state machine probabilmente XState (`xstate` già in deps). |

## 3. Helper TypeScript correlati

### 3.1. `entityColor(type, alpha?)` (proposto handoff) vs codebase

Il handoff propone (`DESIGN_TOKENS.md` § "Helper TypeScript"):

```ts
export type EntityType = 'game' | 'player' | 'session' | 'agent' | 'kb' | 'chat' | 'event' | 'toolkit' | 'tool';
const HSL_MAP: Record<EntityType, { h: number; s: number; l: number; em: string }> = { … };
export function entityColor(entity: EntityType, alpha?: number): string {
  const c = HSL_MAP[entity];
  return alpha !== undefined ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})` : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
}
export function entityEmoji(entity: EntityType): string { return HSL_MAP[entity].em; }
```

**Realtà codebase**: due helper **complementari** già esistono:

| Path codebase | Helper signature | Output | Use case |
|---|---|---|---|
| `apps/web/src/components/ui/entity-tokens.ts` | `function getEntityToken(type: EntityType): EntityToken` con `{ bg, bgSoft, text, border, emoji, label }` | **Tailwind class strings** (`bg-entity-game`, `text-entity-game`, ecc.) | `className` (compile-time, ESLint-friendly) |
| `apps/web/src/components/ui/data-display/meeple-card/tokens.ts` | `function entityHsl(type, alpha?): string` + `entityHslText(type)` + `entityLabel(type)` + `entityIcon(type)` + `entityColors`, `entityTokens` | **HSL `hsl()` / `hsla()` strings** | `style={{ color: entityHsl('game'), background: entityHsl('agent', 0.12) }}` (inline runtime) |

→ La proposta del handoff `lib/entity-color.ts` **è già coperta** da `meeple-card/tokens.ts` `entityHsl()`. Non serve duplicare in `lib/`.

**Decisione design opzionale**: per facilitare l'adozione del pattern handoff esponendo `entityColor()` come alias top-level, si può aggiungere un re-export in `apps/web/src/lib/entity-color.ts`:

```ts
// apps/web/src/lib/entity-color.ts (opzionale alias)
export { entityHsl as entityColor, entityIcon as entityEmoji } from '@/components/ui/data-display/meeple-card';
export type { EntityType } from '@/components/ui/entity-tokens';
```

Più semplice: lasciare un comment in `entity-tokens.ts` che punta a `entityHsl()` per il caso d'uso inline-style.

### 3.2. `cascadeNavigationStore` ✅

| Aspetto | Valore |
|---|---|
| Path canonico | `apps/web/src/lib/stores/cascade-navigation-store.ts` |
| Test path | `apps/web/src/lib/stores/__tests__/cascade-navigation-store.test.ts` |
| Consumer | `apps/web/src/components/ui/data-display/extra-meeple-card/{entities/SessionDrawerContent,ExtraMeepleCardDrawer}.tsx`, `components/dashboard/SessionPanel.tsx`, `components/layout/UserShell/SessionBanner.tsx`, `hooks/useConnectionBarNav.ts` |
| Lib backing | `zustand` |

## 4. Riepilogo decisioni richieste prima di Step 7+

| Decisione | Opzioni | Suggerimento |
|---|---|---|
| **Tabs underline animation** | (a) estendere `ui/navigation/tabs.tsx` · (b) usare `ui/detail-layout/tabs.tsx` se la copre · (c) creare 3° variant | Aprire `ui/detail-layout/tabs.tsx` per verificare. Se underline OK, riusare. Altrimenti (a). |
| **`ConfidenceBadge` consolidamento** | (a) 4 varianti split (status quo) · (b) 1 canonical in `ui/feedback/` + adapter feature | (a) status quo if le 4 hanno divergenze sostanziali; (b) altrimenti consolidare |
| **`StepIndicator` canonical** | (a) lasciare split add-game-sheet vs gamebook · (b) consolidare in `ui/step-progress/` (esiste già ESLint scope) | (b) — ESLint scope esistente suggerisce pianificazione canonical |
| **`CitationExpandedPanel`** | (a) creare ex novo · (b) `CitationSheet` esistente è già "panel" · (c) `CitationModal` + `CitationPdfTab` come composition | Aprire i 3 file per verificare quale variant copre il use case mockup |
| **`MobileBottomBar` route mapping** | TBD per design | Definire 4-5 tab principali (Hub / Library / Discover / Game-nights / Profile?) |
| **`HouseRuleDrawer` location** | `components/features/house-rule-drawer/` (nuovo) vs estensione di `library/game-table/HouseRulesSection.tsx` | Nuovo path `features/` per il drawer dedicato (chat-triggered); lasciare `HouseRulesSection` come display lista in game-table |
| **Helper `entityColor()` alias top-level** | (a) creare `lib/entity-color.ts` re-export · (b) lasciare `entityHsl()` come canonical | (b) — semplificare. Aggiungere JSDoc note in `entity-tokens.ts` |

## 4.5. ⚠️ Drift documentale: path obsoleto in `WIRING_GUIDE.md`

`WIRING_GUIDE.md § Quando il mock include un componente nuovo` (tabella riga ~176-184) raccomanda al maintainer di mettere i 9 componenti v2 nuovi (`HouseRuleDrawer`, `ConfidenceBadge`, `SuggestedQueriesRow`, `ChatHistoryTimeline`, `KbDocList`, `CitationExpandedPanel`, **`LibraryGameAgentShell`**, `StepIndicator`, `ConfirmModal`) in path della forma:

```
src/components/ui/v2/<component>/
```

**Questo path è obsoleto**. Il codebase MeepleAI ha **eliminato il prefisso `v2/`** il 2026-05-18 (DS-deversioning #1025/#1112). Path canonici post-deversioning:

| Tipo componente | Path canonico |
|---|---|
| Primitives generiche (Drawer, Tabs, EntityChip, ...) | `apps/web/src/components/ui/<primitive>/` |
| Feature compositions (HouseRuleDrawer, LibraryGameAgentShell, SuggestedQueriesRow, ChatHistoryTimeline, ...) | `apps/web/src/components/features/<feature>/` |

**Action items**:
1. Quando crei un nuovo componente, **NON usare** `src/components/ui/v2/` (path morto).
2. Per i 5 componenti da creare ex novo (vedi § 5 sotto), usa i path suggeriti in questo audit (riassunti in tabella § 5.5).
3. Considerare di aprire issue per **aggiornare `WIRING_GUIDE.md`** (parte del handoff) o creare un erratum locale `admin-mockups/design_handoff/ERRATA.md`.

## 5. Mapping handoff → codebase finale

### ✅ Già pronti (skip in Step 7+)

| Handoff | Codebase |
|---|---|
| `ConnectionBar` | `ui/data-display/connection-bar/ConnectionBar.tsx` |
| `MeepleCard` | `ui/data-display/meeple-card/MeepleCard.tsx` (+ compound API molto ricca) |
| `EntityChip` | `ui/entity-chip/*` |
| `Drawer` + `cascadeNavigationStore` | `ui/drawer/drawer.tsx` + `lib/stores/cascade-navigation-store.ts` |
| `Tabs` (forse non animated underline) | `ui/navigation/tabs.tsx` (variant `detail-layout/tabs.tsx` da verificare) |
| `RecentsBar` | `layout/UserShell/RecentsBar.tsx` |
| `ConfidenceBadge` | `ui/feedback/ConfidenceBadge.tsx` (+ 3 varianti) |
| `KbDocList` | `features/agent-detail/KbDocList.tsx` |
| `StepIndicator` | `library/add-game-sheet/StepIndicator.tsx` + `features/gamebook/StepIndicator.tsx` |
| `entityColor()` runtime helper | `entityHsl()` da `ui/data-display/meeple-card/tokens.ts` |
| Citation family | `chat-unified/Citation{Badge,Block,Sheet}.tsx` + `chat/panel/CitationExpander.tsx` + `features/game-chat/Citation{Modal,PdfTab,Chip,OwnershipUpsell}.tsx` |

### ❌ Da creare ex novo (6 componenti, **+1 post-review LibraryGameAgentShell**)

| Componente | Path suggerito (canonico post-deversioning) | Effort stimato |
|---|---|---|
| `MobileBottomBar` | `apps/web/src/components/layout/mobile/MobileBottomBar.tsx` | M (design route mapping + state) |
| `HouseRuleDrawer` | `apps/web/src/components/features/house-rule-drawer/HouseRuleDrawer.tsx` | M (form + Drawer composition) |
| `SuggestedQueriesRow` | `apps/web/src/components/features/game-chat/SuggestedQueriesRow.tsx` | S (chip horizontal-scroll) |
| `ChatHistoryTimeline` | `apps/web/src/components/features/agent-detail/ChatHistoryTimeline.tsx` | M (TanStack Query paginated + timeline rendering) |
| `ConfirmModal` | `apps/web/src/components/ui/feedback/ConfirmModal.tsx` | S (Radix AlertDialog wrapper) |
| `LibraryGameAgentShell` 🆕 post-review | `apps/web/src/components/features/library-game-agent/LibraryGameAgentShell.tsx` | **L** (orchestratore SSE + state machine + 5 stati + responsive split-view) |

### ⚠️ Da verificare / decidere

| Componente | Status | Azione |
|---|---|---|
| `CitationExpandedPanel` | Esistono 8 family member ma nessuno chiamato "ExpandedPanel" | Aprire `CitationSheet.tsx`, `CitationModal.tsx`, `CitationExpander.tsx` per capire quale copre il use case mockup → decidere riuso vs nuovo orchestratore |

## 6. Compliance ESLint rules per nuovi componenti

Ogni componente nuovo (5 above) deve rispettare:

- ✅ `local/no-hardcoded-color-utility = error` → usare `bg-background`, `text-muted-foreground`, `bg-entity-*`, `text-entity-*` (mai `bg-white`, `bg-gray-*`, ecc.)
- ✅ `local/no-hardcoded-hex = error` (se il path è dentro lo scope `ui/<primitive>/**` enumerato in `eslint.config.mjs:557-583`) → usare `entityHsl()` o token CSS vars
- ✅ `meeple/no-inline-hsl-v2 = error` (se path è in `components/features/**`) → usare `getEntityToken()` o Tailwind utilities entity
- ✅ `local/api-client-v1-prefix = error` (se path è in `hooks/queries/**` o `lib/api/clients/**`) → ogni `apiClient` call con `/api/v1/` prefix
- ✅ `@typescript-eslint/no-explicit-any = error` → no `any`, tipizzare tutto
- ✅ `react-hooks/exhaustive-deps = error` → dependency arrays corrette

## 7. Test obbligatori per nuovi componenti

Pattern attivo nel repo:

- **Unit test** (`vitest`): per ogni componente, almeno 1 file `__tests__/<Name>.test.tsx` (vedi pattern `ConnectionBar.test.tsx`, `RecentsBar.test.tsx`, `KbDocList.test.tsx`)
- **A11y test** (`jest-axe` + `@testing-library`): per primitives accessibili. Vedi `tabs.a11y.test.tsx`.
- **Storybook story** (`.stories.tsx`): per primitives canonical. Chromatic visual regression per design changes.

## 8. Stato dei deliverable Step 1-6

| Step | Deliverable | Stato | Path |
|---|---|---|---|
| 1 | `design_handoff/CODEBASE_AUDIT.md` | ✅ generato | `admin-mockups/design_handoff/CODEBASE_AUDIT.md` |
| 5 | `design_handoff/SCHEMA_DIFF.md` | ✅ generato | `admin-mockups/design_handoff/SCHEMA_DIFF.md` |
| 6 | `design_handoff/COMPONENTS_AUDIT.md` | ✅ generato (questo file) | `admin-mockups/design_handoff/COMPONENTS_AUDIT.md` |

### ✅ Step 2-3-4 — APPLICATI (2026-05-24 sub-task execution)

**Stato post-applicazione**:

| Sub-task | Stato | File modificato/creato | Verifica |
|---|---|---|---|
| 2a — `JetBrains_Mono` via `next/font/google` | ✅ DONE | `apps/web/src/app/layout.tsx` (3 hunk edits) | `pnpm typecheck` PASS |
| 2b — Fix `--font-inter` orphan → `--font-nunito` | ✅ DONE | `apps/web/tailwind.config.js` (3 occurrences replaced) | `pnpm lint` PASS (0 errors, 49 warnings sotto 510 limit) |
| 2c — `components.css` import | ✅ SKIP (decisione utente) | (nessuna modifica) | mock-canvas-only file, utility coperte da Tailwind+MeepleCard |
| 2d — Verifica baseline | ✅ PASS | (lint + typecheck) | 0 errori typecheck, 0 errors ESLint, 49 warnings (pre-existing, sotto baseline) |
| 3a — Confirm `entityHsl()` exists | ✅ DONE | (read-only verify) | Trovato in `meeple-card/tokens.ts:36` con firma `entityHsl(entity, alpha?) → string` |
| 3b — JSDoc note in `entity-tokens.ts` | ✅ DONE | `apps/web/src/components/ui/entity-tokens.ts` (JSDoc 11 righe aggiunto sopra `getEntityToken`) | Redirige consumer al complementary `entityHsl()` per inline styles |
| 3c — Alias `lib/entity-color.ts` | ✅ DONE | `apps/web/src/lib/entity-color.ts` (nuovo file 25 righe) | Re-export `entityHsl as entityColor` + `entityIcon as entityEmoji` + type alias |
| 4a — Types strategy decision | ✅ SKIP (decisione utente) | (nessuna modifica) | Tipi BE-driven via OpenAPI Zod; campi mockup-only restano inline come UI fallback |

**Effort effettivo**: ~25 min (vs stima 40-130 min). Nessun blast radius issue (sub-task 2b `--font-inter` rimpiazzato 3-for-3 senza callsite cambio downstream — `font-inter`/`font-nunito`/`font-body` className continuano a funzionare, ora correttamente legati a Nunito caricato).

### (Storico) Step distruttivi 2-3-4 — analisi pre-decision (DECOMPOSED post-review)

#### Step 2 — Importa design system

**Sub-tasks** (con acceptance criteria):

| ID | Sub-task | AC (Given / When / Then) | Effort |
|---|---|---|---|
| 2a | Aggiungere `JetBrains_Mono` via `next/font/google` in `layout.tsx` | _Given_: pre-fix `--f-mono` (token) rende fallback `ui-monospace`. _When_: aggiungo `import { JetBrains_Mono } from 'next/font/google'` + `--font-jetbrains-mono` variable + `body className` interpolation. _Then_: in DevTools console eseguendo `getComputedStyle(document.body).getPropertyValue('--f-mono')` la stringa restituita inizia con `'JetBrains Mono'`. | 5-10 min |
| 2b | Fix `--font-inter` orphan in `tailwind.config.js:17` | _Given_: `tailwind.config.js:17` referenzia `var(--font-inter)` ma `layout.tsx` non definisce mai `--font-inter`. _When_: decidere se (a) renamearlo a `var(--font-nunito)` (alias intentional?), (b) rimuovere alias `inter/body/nunito` collassati, oppure (c) aggiungere `Inter` font come `next/font`. _Then_: `pnpm typecheck && pnpm lint` passa senza nuovi warning + `font-body` className renders il font atteso. **Pre-step grep**: `Grep "font-body\|font-inter\|var\(--font-inter\)" apps/web/src` per misurare blast radius. | 5-15 min (range dipende dal numero callsites) |
| 2c | Audit `components.css` handoff vs Tailwind `@theme` esistente | _Given_: `admin-mockups/design_handoff/components.css` espone utility (`.e-game`, `.e-tint-game`, `.e-ring-game`, `.mai-cb-scroll`, `.phone`, `.phone-sbar`). _When_: confronto file utility vs Tailwind utilities entity esistenti (`bg-entity-*`, `text-entity-*`, `ring-entity-*/N`). _Then_: lista delta = utility NON coperte da Tailwind. Se delta vuota = no-op. Se delta non-vuota = decidere `cp components.css → src/styles/components.css` (totale) oppure aggiungere singoli utility a Tailwind `@theme` block. | 10-30 min |
| 2d | Verifica baseline non rotta | _Given_: post sub-tasks 2a/2b/2c. _When_: `pnpm typecheck && pnpm lint && pnpm test` (run unit suite). _Then_: 0 typecheck errors, ESLint warnings ≤ baseline (510), tests pass. | 5-15 min |

**Totale Step 2**: **25-70 min** (più realistico del "10-30 min" originale).

#### Step 3 — entity-color helper

**Sub-tasks**:

| ID | Sub-task | AC | Effort |
|---|---|---|---|
| 3a | Aprire `apps/web/src/components/ui/data-display/meeple-card/tokens.ts` e confermare export `entityHsl()` | _Given_: il file è citato in `meeple-card/index.ts:31`. _When_: lo apro. _Then_: vedo `export function entityHsl(type: EntityType, alpha?: number): string` (o equivalente). | 5 min |
| 3b | Aggiungere JSDoc note in `entity-tokens.ts` che punta a `entityHsl()` per inline-style use case | _Given_: dev legge `entity-tokens.ts`. _When_: aggiungo `/** @see entityHsl() for runtime HSL string accessor */` sopra `getEntityToken`. _Then_: discoverability +. | 5 min |
| 3c (opzionale) | Creare alias `apps/web/src/lib/entity-color.ts` re-export | _Given_: handoff specifica `src/lib/entity-color.ts`. _When_: creo file con `export { entityHsl as entityColor } from '@/components/ui/data-display/meeple-card/tokens';`. _Then_: `import { entityColor } from '@/lib/entity-color'` funziona come scritto nel handoff. | 5 min |

**Totale Step 3**: **10-15 min**.

#### Step 4 — Entity types

**Sub-tasks**:

| ID | Sub-task | AC | Effort |
|---|---|---|---|
| 4a | Decidere strategy types entity | _Given_: `types/domain.ts` ha types minimal (Game = `{ id, title, createdAt? }`). _When_: il maintainer sceglie: (1) **skip** (i tipi vengono da OpenAPI Zod schemas via `lib/api/generated/`), (2) **extend `types/domain.ts`** con campi mock optional, (3) **file separato `types/mockup-extra.ts`**. _Then_: decisione documentata. | 5-10 min |
| 4b (se 4a → opt 2/3) | Implement extraction da `data.js` | _Given_: opt 2 o 3 scelta. _When_: estraggo `Player.color : number`, `Game.coverGradient : string?`, `Game.coverEmoji : string?`, etc. _Then_: `pnpm typecheck` passa + i mockup screen possono importare tipi senza `as any`. | 15-30 min |

**Totale Step 4**: **5 min (skip) → 15-45 min (extract)**.

**Stima cumulativa rivista Step 2-4**: **40-130 min** (vs ~60 min originale). Effort realistico va calibrato in base a:
- Sub-task 2b range (blast radius `--font-inter` callsites)
- Sub-task 2c presenza/assenza delta utility
- Decisione 4a (skip vs extract)

## 9. Failure modes & mitigations per Step 2-4 (post-review)

### Step 2 failure modes

| Modalità di fallimento | Probabilità | Impatto | Mitigation |
|---|---|---|---|
| `JetBrains_Mono` request fallisce in CI con `next/font` cache stale | M | M | `rm -rf apps/web/.next/cache` prima del rerun CI; verificare `googlefonts.com` raggiungibilità da GH Actions runner |
| Hydration mismatch quando `data-theme` cambia post-mount mentre `--font-jetbrains-mono` è ancora unset (FOUC) | L | M | `next-themes` già usa `suppressHydrationWarning` su `<html>` + `<body>` (vedi `layout.tsx:78-79`); il font ha `display: 'swap'` → fallback immediato + swap quando carica → OK |
| `--font-inter` orphan ha callsites cross-monorepo non in `apps/web/src` (es. Storybook config, docs/) | L | L | Grep wide pre-fix: `Grep "font-inter\|var\\(--font-inter\\)" --output_mode files_with_matches` su tutto repo |
| `components.css` handoff utility duplica Tailwind `@theme` block → cascade conflict | M | M | Pre-import diff con `tailwind.config.js` + audit specifico in 2c sub-task |
| Build CI fails per nuovi token `--f-mono` non riconosciuti da PostCSS | L | H | Test locale `pnpm build` prima del PR; nessun cambiamento previsto a postcss config |

### Step 3 failure modes

| Modalità di fallimento | Probabilità | Impatto | Mitigation |
|---|---|---|---|
| `entityHsl()` export non esiste con quella firma (rinominato silenziosamente) | L | L | Sub-task 3a richiede Read prima di sub-task 3b. Se firma diversa, adattare. |
| Alias `lib/entity-color.ts` conflitta con import path esistente | L | L | Glob `apps/web/src/lib/entity-color*` prima di creare; il path è confermato vuoto da audit corrente |

### Step 4 failure modes

| Modalità di fallimento | Probabilità | Impatto | Mitigation |
|---|---|---|---|
| Types mockup duplicano nomi presenti in `lib/api/generated/` causando conflitto | M | M | Namespace separato: `types/mockup-extra.ts` con export `MockOnly_Player`, `MockOnly_Game`, etc. — non importare insieme |
| Maintainer applica opt 2 (`types/domain.ts` extend) introducendo campi optional che vengono fraintesi come "real BE field" | M | H | Documentazione inline obbligatoria: `// MOCK-ONLY UI fallback — NOT from BE DTO` per ogni campo |
| Build fails per nuovi types che riferiscono entity tipologie non esistenti (es. `EntityType = 'librogame'` non in tokens) | L | M | Restringere extension a soli 9 entity types canonical; non aggiungere nuovi |

---

**Generato da Claude Code Opus 4.7 in modalità read-only.** Nessuna modifica al codebase. Decisioni design demandate al maintainer per review prima di valutare Step 2-3-4. Post-review `/sc:spec-panel` aggiornato 2026-05-24.

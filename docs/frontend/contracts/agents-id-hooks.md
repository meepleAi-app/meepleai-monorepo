# `/agents/[id]` Hook Contract — Wave C.2 Phase 0.5

> **Phase 0.5 contract** per V2 migration spec sezione 3.4.
> **Tier**: L (≥3 hook indipendenti, cartesian FSM con dependency chain agent.gameId).
> **Scope**: orchestrator `AgentDetailViewV2` per route `/agents/[id]`.
> **Issue**: #581 (Wave C umbrella). **Mockup**: `admin-mockups/design_files/sp4-agent-detail.jsx`.
>
> Pattern blueprint da Wave C.1 ([games-id-hooks.md](games-id-hooks.md)) adattato per la 2-step
> dependency chain `agentId → agent.gameId → KB docs`.

## 1. Route surface

| Aspect | Value |
|--------|-------|
| Route path | `/agents/[id]` |
| Page component | `apps/web/src/app/(authenticated)/agents/[id]/page.tsx` |
| Orchestrator | `apps/web/src/app/(authenticated)/agents/[id]/_components/AgentDetailViewV2.tsx` |
| Param source | `useParams<{ id: string }>()` |
| Param validation | `params?.id` può essere `undefined` (Next.js 16 app router pre-hydration) |
| **Critical**: never pass `undefined` literal o stringa `'undefined'` ai sub-hook | |
| **Migration note** | Page legacy è server component. Wave C.2 converte a client component (mirror Wave C.1 page.tsx pattern) |

## 2. Hook dependency graph

```
useParams<{id}> ──validate──→ agentId: string | null
                                  │
                                  │ (null se params?.id è undefined o "")
                                  │
                                  ├─→ useAgent(agentId) [PARENT]
                                  │       │
                                  │       └─→ STATE: idle | loading | error | success(agent)
                                  │              │
                                  │              ├─ agent.gameId — needed for Knowledge tab
                                  │              │
                                  │              ├─→ useAgentKbDocs({ gameId: agent?.gameId, enabled: tab='knowledge' && agent.isSuccess })
                                  │              │       └─→ STATE: disabled | loading | error | success([])
                                  │              │
                                  │              ├─→ useAgentConfig({ agentId, enabled: tab='settings' && agent.isSuccess })
                                  │              │       └─→ STATE: disabled | loading | error | success(config)
                                  │              │
                                  │              ├─→ useAgentThreads({ agentId, enabled: tab='history' && agent.isSuccess })
                                  │              │       └─→ STATE: disabled | loading | error | success([])
                                  │              │
                                  │              └─→ usePerformanceStats({ agentId, enabled: tab='performance' && agent.isSuccess })
                                  │                      └─→ STATE: disabled | loading | error | success(stats)
                                  │
                                  └─→ shells: loading / error / not-found
```

**Key constraints**:
1. **`agentId` è `string | null`, MAI `undefined` o `'undefined'`** (mirror Wave C.1 sez. 2.1)
2. **Sub-hook gating cumulativo**: `enabled: !!agentId && agentQuery.isSuccess && agentQuery.data != null && tab === 'X'`
3. **`useAgent` è il parent** — sub-hooks NON montano finché parent !== success E data !== null
4. **Lazy tab fetch**: ogni sub-hook solo per il tab attivo
5. **2-step chain Knowledge tab**: `useAgentKbDocs` richiede `agent.gameId` (potrebbe essere null per agenti senza game associato)

### 2.1 AgentId resolution

Identico a Wave C.1 sez. 2.1:

```ts
// CORRETTO
const params = useParams<{ id: string }>();
const rawId = params?.id;
const agentId = typeof rawId === 'string' && rawId.length > 0 ? rawId : null;

// SBAGLIATO (anti-pattern Wave C.1 PR #697)
const id = params?.id ?? '';
```

### 2.2 Sub-hook gating + Cell 4 guard (mirror Wave C.1 hotfix)

```ts
// useAgent (parent) — accept string|null, hook internal gate via enabled
const agentQuery = useAgent(agentId);

// useAgentKbDocs (Knowledge tab) — 2-step chain gating
const kbDocsQuery = useAgentKbDocs({
  gameId: agentQuery.data?.gameId ?? null,  // null se agente standalone
  enabled:
    !!agentId &&
    agentQuery.isSuccess &&
    agentQuery.data != null &&  // Cell 4 guard
    agentQuery.data.gameId != null &&  // 2-step chain gate
    tab === 'knowledge',
});

// useAgentThreads (History tab)
const threadsQuery = useAgentThreads({
  agentId,
  enabled:
    !!agentId &&
    agentQuery.isSuccess &&
    agentQuery.data != null &&
    tab === 'history',
});

// useAgentConfig (Settings tab)
const configQuery = useAgentConfig({
  agentId,
  enabled:
    !!agentId &&
    agentQuery.isSuccess &&
    agentQuery.data != null &&
    tab === 'settings',
});

// usePerformanceStats (Performance tab) — TBD endpoint, può essere derived da agent.invocationCount
```

**Anti-pattern dal PR #697 (Wave C.1) e da evitare**:
- ❌ `enabled: !!id && (fixture == null ? agentQuery.data != null : true)` (fixture bypass)
- ❌ Skipping `agentQuery.data != null` check (Cell 4 race con success(null))
- ❌ Skipping `agent.gameId != null` check per Knowledge tab (cascade `kb-docs/undefined`)

## 3. FSM cell matrix

Cartesian rilevante (subset di celle critical, +1 vs Wave C.1 per il 2-step chain Knowledge tab):

| # | agentId | useAgent | sub-hook (active tab) | UI Behavior |
|---|---------|----------|----------------------|-------------|
| 1 | `null` | `disabled` | `disabled` | **Shell `not-found`**: hero illustrato + CTA back to /agents. Nessuna fetch. |
| 2 | valid | `loading` | `disabled` (gated) | **Shell `loading`**: skeleton hero + skeleton tabs. Sub-hook NON montato. |
| 3 | valid | `error` | `disabled` (gated) | **Shell `error`**: error card + CTA retry. Sub-hook NON montato. |
| 4 | valid | `success(null)` | `disabled` (gated) | **Shell `not-found`**: agent non trovato. |
| 5 | valid | `success(data)` | `disabled` (tab='identity') | **Default render**: hero + tabs, identity tab content (no sub-fetch). |
| 6 | valid | `success(data)` | `loading` (tab=knowledge/history/etc.) | **Default render** + tab inline skeleton. |
| 7 | valid | `success(data)` | `error` (tab=...) | **Default render** + tab inline error banner (NO shell error). |
| 8 | valid | `success(data)` | `success([])` (tab=...) | **Default render** + tab empty state CTA. |
| 9 | valid | `success(data)` | `success([...])` (tab=...) | **Default render** + tab populated. |
| **10** | valid | `success(data + gameId=null)` | `disabled` (Knowledge tab, gated) | **Default render** + Knowledge tab "agente standalone, nessuna KB associata" empty state. ⚠️ Cell unica Wave C.2 |

### 3.1 Critical assertion contracts

- ⚠️ **Cell 10 Wave C.2-specific**: agent senza gameId associato → Knowledge tab mostra empty state DEDICATO (non lo stesso di Cell 8). Test deve coprire entrambi separatamente.
- ⚠️ **Cell 4 vs Cell 1**: stesso UI shell `not-found` ma origini diverse (mirror Wave C.1).
- ⚠️ **Variant matrix**: agent può essere `active`, `draft`, `archived`. Draft mostra setup banner; archived mostra read-only banner. Variant è un campo `agent.status` o derivato.

### 3.2 State derivation function

```ts
// apps/web/src/lib/agents/agent-detail-state.ts
export type AgentDetailUiState = 'loading' | 'error' | 'not-found' | 'default';

export function deriveAgentDetailUiState(input: {
  agentId: string | null;
  isLoading: boolean;
  isError: boolean;
  hasData: boolean;
}): AgentDetailUiState {
  if (input.agentId == null) return 'not-found';  // Cell 1
  if (input.isLoading) return 'loading';            // Cell 2
  if (input.isError) return 'error';                // Cell 3
  if (!input.hasData) return 'not-found';           // Cell 4
  return 'default';                                  // Cells 5-10
}
```

### 3.3 Variant resolver (Wave C.2 specific)

```ts
export type AgentVariant = 'active' | 'draft' | 'archived';

export function deriveAgentVariant(agent: AgentDto): AgentVariant {
  // Per Phase 0.5: derivazione TBD una volta noto lo schema AgentDto.
  // Possibili sorgenti:
  //   - agent.status === 'draft' | 'active' | 'archived' (se backend espone)
  //   - agent.archivedAt != null → 'archived'
  //   - agent.systemPrompt == null && agent.kbDocsCount === 0 → 'draft'
  //   - default → 'active'
  // Decision rinviata a implementation Task 1: leggere AgentDto.
  if (agent.archivedAt != null) return 'archived';
  if (agent.systemPrompt == null) return 'draft';
  return 'active';
}
```

## 4. Component contracts

### 4.1 Tab shell (riuso GameDetailTabsAnimated)

5 tabs: `'identity' | 'knowledge' | 'performance' | 'history' | 'settings'`. Riuso esatto pattern Wave C.1 (a11y `role=tablist`, `useTablistKeyboardNav` hook).

### 4.2 AgentHero (variant draft/active/archived)

```ts
type AgentHeroVariant = 'active' | 'draft' | 'archived';

interface AgentHeroProps {
  variant: AgentHeroVariant;
  name: string;
  avatar: string;  // emoji per mockup, o imageUrl
  persona: string | null;
  meta: AgentHeroMeta;  // type/model/createdAt/lastUsed/invocations
  ctaPlay?: () => void;       // 'Avvia chat' (active variant)
  ctaSetup?: () => void;       // 'Continua setup' (draft variant)
  ctaUnarchive?: () => void;   // 'Riattiva' (archived variant)
  ctaShare?: () => void;
  labels: AgentHeroLabels;
}
```

**Render rules**:
- `variant === 'active'` → CTA Play, hide setup banner
- `variant === 'draft'` → CTA Setup + setup banner; Performance/History tabs LOCKED (read-only)
- `variant === 'archived'` → CTA Unarchive + archived banner; tutti tabs READ-ONLY (settings danger zone hidden)

### 4.3 PersonaCard / SystemPromptViewer / KbDocList / ChatHistoryTimeline / AgentSettingsForm / AgentDangerZone

**Comune contract** (mirror Wave C.1 sez. 4):
- Pure components (no hooks, accept resolved data)
- `data-slot="agent-detail-<name>"` per E2E
- `labels: <ComponentLabels>` prop (i18n upfront)

**Discriminated unions per stato**:

```ts
// KbDocList (Knowledge tab content)
type KbDocsState =
  | { kind: 'loading' }
  | { kind: 'error'; retry: () => void }
  | { kind: 'empty' }                                  // gameId valid, no docs uploaded
  | { kind: 'standalone' }                              // agent.gameId === null (Cell 10)
  | { kind: 'success'; docs: KbDocEntry[] };

// ChatHistoryTimeline (History tab)
type ChatHistoryState =
  | { kind: 'loading' }
  | { kind: 'error'; retry: () => void }
  | { kind: 'empty' }                                  // no chat threads yet
  | { kind: 'success'; threads: ChatThreadEntry[] };

// AgentSettingsForm (Settings tab) — variant-aware
type SettingsState =
  | { kind: 'loading' }
  | { kind: 'error' }
  | { kind: 'editable'; config: AgentConfig }            // active variant
  | { kind: 'read-only'; config: AgentConfig };          // archived variant
```

### 4.4 A11y critical

- Tabs: `role="tablist"`, `role="tab" aria-selected aria-controls`, `role="tabpanel" aria-labelledby id`
- Setup banner (draft): `role="status"` + descriptive text
- Archived banner: `role="alert"` + read-only signaling
- Danger zone: `role="region" aria-labelledby` + confirmation dialog `role="dialog" aria-modal`

### 4.5 ⚠️ A11y CTA contrast pre-emption (Wave C.1 lesson)

Wave C.1 hotfix scoperto che `bg-emerald-600` + `bg-amber-600` con `text-white` falliscono WCAG AA 4.5:1. Per Wave C.2 PRE-EMPTIVE FIX:
- Usare `bg-emerald-700`, `bg-amber-700`, `bg-violet-700` (entity agent purple) per CTA con white text
- Audit pre-implementation: `grep -E "bg-(emerald|amber|violet|rose|cyan)-600" components/v2/agent-detail/`

## 5. Test coverage plan

Per Tier L spec sez. 4.1 ratio: **50% unit / 35% integration / 15% e2e**.

### 5.1 Unit tests (50% — ~30 tests)

| Target | Tests |
|--------|-------|
| `deriveAgentDetailUiState` | Property tests per Cells 1-5 + Cell 10 (gameId null) — 12 tests |
| `deriveAgentVariant` | active/draft/archived matrix — 4 tests |
| `parseStateOverride` | Valid/invalid/disabled — 3 tests |
| 7 v2 components | render shape per state, discriminated unions, variant matrix — ~45 tests across 7 files |

### 5.2 Integration tests (35% — ~20 tests)

Orchestrator-level via `renderHook` + MSW mocks. Cell matrix coverage estesa per Cell 10:

| Target | Cell |
|--------|------|
| `gameId === null` | Cell 1 |
| `useAgent.loading` | Cell 2 |
| `useAgent.error` | Cell 3 |
| `useAgent.success(null)` | Cell 4 |
| `tab='identity'` (no sub-fetch) | Cell 5 |
| Tab change identity→knowledge fires fetch (gameId set) | Cell 5→6 |
| Tab change identity→history fires threads fetch | Cell 5→6 alternate |
| `useAgent.success(data, gameId=null) + tab=knowledge` | **Cell 10** — standalone empty state |
| `useAgentKbDocs.error` (tab=knowledge) | Cell 7 |
| Variant=archived → settings tab read-only | (variant test) |
| Variant=draft → performance/history tabs locked | (variant test) |

**Critical assertion in tutti i test integration**:
```ts
const kbDocsHandler = vi.fn((req) => {
  const url = new URL(req.url);
  const gameId = url.searchParams.get('gameId');
  expect(gameId).not.toBe('undefined');
  expect(gameId).not.toBe('null');
  expect(gameId).not.toBe('');
  expect(gameId).toMatch(/^[a-f0-9-]{36}$/);  // valid UUID
  return HttpResponse.json([]);
});
```

### 5.3 E2E tests (15% — ~5 specs)

- `e2e/v2-states/agent-detail.spec.ts` — 5 stati × 2 viewports = 10 PNG (4 base FSM + 1 Cell 10 standalone)
- `e2e/visual-migrated/sp4-agent-detail.spec.ts` — visual baseline 1280×720 + 375×812 (4 PNG: active variant + draft variant)
- `e2e/a11y/agent-detail.spec.ts` — axe-core WCAG 2.1 AA + reduced-motion contract
- `e2e/smoke-real-backend/agent-detail.smoke.spec.ts` — deterministic UUID

## 6. Bundle budget

Tier L per spec sez. 1.3: max +120 KB. Estimate:
- 7 v2 components: ~45 KB
- Orchestrator: ~28 KB (più complesso vs Wave C.1 — variant matrix + 2-step chain)
- i18n keys (it+en): ~12 KB
- Visual fixture: ~5 KB

**Target totale**: ~90 KB (margin 30 KB).

## 7. Coexistence flag

**Decisione**: NO flag (mirror Wave C.1 — app pre-prod, big-bang accettabile post-Phase 0.5).

## 8. Pre-implementation audit checklist (Wave C.1 lessons)

⚠️ **Verificare PRIMA del dispatch implementation subagent**:

- [ ] **Redirect cleanup**: `grep -n "/agents/:id\|/agents/.*destination" apps/web/next.config.js` — verificare nessun redirect attivo
- [ ] **Proxy.ts rewrite**: `grep -n "agents" apps/web/src/proxy.ts` — verificare nessun rewrite intercetta path
- [ ] **A11y CTA contrast pre-emption**: usare 700-shade Tailwind per white text (vedi sez. 4.5)
- [ ] **Page boundary normalization**: page.tsx convertita da server a client component (CRITICAL — current legacy è server)
- [ ] **Triple auth helper** in E2E specs: `seedAuthSession` + `seedCookieConsent` + `mockAuthEndpoints`

## 9. References

- Phase 0.5 contract Wave C.1 (template): `docs/frontend/contracts/games-id-hooks.md`
- Spec V2 migration sez. 3.4: `docs/superpowers/specs/2026-04-26-v2-design-migration.md`
- Migration matrix Tier classification: `docs/frontend/v2-migration-matrix.md`
- Issue #581 (Wave C umbrella)
- Memory feedback files:
  - `feedback_v2-tier-dispatch-strategy.md` (Phase 0.5 pattern validated)
  - `feedback_brownfield-route-redirect-audit.md` (redirect cleanup gotcha)
- Mockup source: `admin-mockups/design_files/sp4-agent-detail.jsx`

---

**Status**: DRAFT — pending review before implementation dispatch.
**Next step post-approval**: dispatch implementation subagent con prompt che referenzia esplicitamente questo contract + checklist FSM cells (10 celle).

# Design System De-versioning & Mockup-Faithful Convergence

| Field | Value |
|---|---|
| **Status** | draft |
| **Date** | 2026-05-11 |
| **Author** | spec-panel (Wiegers/Cockburn/Fowler/Adzic/Crispin/Nygard) |
| **Supersedes (partial)** | [`2026-04-26-v2-design-migration.md`](./2026-04-26-v2-design-migration.md) — Phase 0 matrix logic obsoleted post-migration |
| **Related** | [`v2-migration-matrix.md`](../frontend/v2-migration-matrix.md), [`sp3-shared-game-detail.jsx`](../../../admin-mockups/design_files/sp3-shared-game-detail.jsx) |
| **Branch convention** | `feature/issue-{N}-deversioning-{stage}` per PR stage |

## 1. Problem statement

Il codebase frontend ospita due alberi paralleli:
- `apps/web/src/components/ui/v2/` — ~24 primitives
- `apps/web/src/components/v2/<feature>/` — ~80 feature compositions (Wave 1-4)

Coesistono con componenti legacy non-versionati (es. `components/games/MeepleGameCard.tsx`). Il versioning `v2/` è artefatto di una migrazione transitoria che ora ha esaurito la sua utilità: i mockup `admin-mockups/design_files/` sono la **single source of truth** visiva, e ogni component conforme al mockup deve esistere in **una sola posizione canonica senza prefisso di versione**.

### 1.1 Decisioni utente (2026-05-11)

1. Audit automatizzato via agent autonomo (Playwright + visual diff vs mockup HTML).
2. Dashboard = sezioni per entità *autenticate/own*; Hub = catalogo *pubblico/community* per entità principali.
3. Detail pages già done vengono **ispezionati per sicurezza**, NON re-implementati di default.
4. Versione canonica = quella conforme al mockup. Legacy diverging viene **rimosso**, non mantenuto in coexistence.
5. **Nessuna backward-compat richiesta**: nessun consumer esterno importa `@meepleai/.../v2/...`. Si può rimuovere il legacy senza barrel re-export.

## 2. Goals & Non-goals

### Goals

- G1: Eliminare il prefisso `v2/` dai path component (sia `ui/v2/` sia `v2/<feature>/`).
- G2: Per ogni component esistente, mantenere **una sola versione**, quella conforme al mockup.
- G3: Estrarre `DetailPageLayout` primitive (Hero + ConnectionBar + Tabs + Footer slots) come pattern condiviso tra detail authenticated e public.
- G4: Definire routing-map canonica `MeepleCard.entity → href` in `lib/routes.ts`.
- G5: Click su `MeepleCard` usa `<a>` via `next/link` (deep-link, prefetch, middle-click, a11y).
- G6: Dashboard authenticated e Hub pubblico vivono in route groups separati `(authenticated)/dashboard` e `(public)/hub`.

### Non-goals

- NG1: Refactor logico delle Server Action / data hooks (resta `useGame`, `useAgent`, …).
- NG2: Modifica delle Phase 0.5 sub-hook contracts già firmate per route Tier L.
- NG3: Estensione del catalogo entità coperto da `MeepleCard` (resta agli 8 esistenti).
- NG4: Migrazione admin-app o pacchetti esterni (nessuno esiste).

## 3. Stage plan (3+ PR sequenziali)

> **Freeze**: per la durata del piano (stimato 2 sprint), ogni nuova PR che aggiunge file sotto `components/v2/` o `components/ui/v2/` è **bloccata**. Documentato qui + nota in `CLAUDE.md` → sezione "🔒 Active Freezes".

### Stage 1 — Audit automatizzato (read-only)

**PR title**: `chore(audit): mockup conformity audit for v2 components (Stage 1)`

**Branch**: `feature/issue-{N}-deversioning-audit`

**Deliverable**: report `docs/for-developers/audits/2026-05-11-mockup-conformity.md` con tabella per ciascun component:

| Component path | Mockup ref | Status | Diff summary | Action |
|---|---|---|---|---|
| `components/v2/game-detail/GameDetailHero.tsx` | `sp4-game-detail.jsx` | ✅ match | — | rename path only |
| `components/v2/agents/AgentsHero.tsx` | `sp4-agents-index.jsx` | ⚠️ partial | hero padding diverge | minor refactor |
| `components/games/MeepleGameCard.tsx` (legacy) | `sp4-games-index.jsx` | ❌ diverge | DOM structure differs | DELETE — keep MeepleCard canonico |

**Automation**:
- Agent autonomo (subagent `general-purpose` o `frontend-architect`) lancia Playwright su ogni mockup HTML in `admin-mockups/design_files/`
- Per ogni component implementato, mount in test page → screenshot 375 + 1280
- Visual diff: pixelmatch o `@playwright/test --update-snapshots` baseline
- Strutturale: DOM tree comparison (depth + role + key attrs) via custom AST walker
- Output: JSON → Markdown report

**Acceptance criteria**:
- AC1.1: Tutti i component sotto `components/v2/` e `components/ui/v2/` mappati con outcome `match|partial|diverge|missing|manual-review` (count effettivo emerso dall'audit: ~167 entries — vedi report)
- AC1.2: Tutti i ~30 component legacy (non-prefissati) classificati `keep|delete|merge`
- AC1.3: Report machine-readable JSON committato accanto al Markdown per consumo in Stage 2/3
- AC1.4: Zero modifica codice — solo audit + freeze annotation in `CLAUDE.md`

**Rollback**: revert PR — zero impact su build.

### Stage 2 — Path migration (rename atomico + import fix)

**PR title**: `refactor(components): drop v2/ prefix — atomic rename (Stage 2)`

**Branch**: `feature/issue-{N}-deversioning-paths`

**Scope**: SOLO i component flaggati `match` nello Stage 1. I `partial` e `diverge` restano sotto `v2/` fino a Stage 3.

**Codemod**:
- Script `scripts/codemod/drop-v2-prefix.ts` basato su `ts-morph`
- Input: lista path `match` dallo Stage 1 JSON
- Per ogni path:
  - Rename file: `components/v2/<feature>/X.tsx` → `components/features/<feature>/X.tsx`
  - Rename file: `components/ui/v2/<primitive>/Y.tsx` → `components/ui/<primitive>/Y.tsx`
  - Aggiorna tutti gli import in `apps/web/src/**/*.{ts,tsx}` via AST transform
  - Aggiorna `tsconfig.json` paths se necessario
- Output: dry-run report committato; run produce diff atomico

**Path canonico finale** (decisione architetturale):

```
apps/web/src/components/
├── ui/                    # primitives (era ui/v2/)
│   ├── detail-layout/     # NEW: DetailHero + ConnectionBar + DetailTabs + DetailFooter
│   ├── meeple-card/
│   ├── auth-card/
│   └── ...
└── features/              # feature compositions (era v2/<feature>/)
    ├── game-detail/
    ├── agent-detail/
    ├── player-detail/
    └── ...
```

`features/` discrimina compositions feature-specific dai primitives in `ui/`. Path stable per il futuro.

**Acceptance criteria**:
- AC2.1: Zero modifica behavior — solo path rename + import fix
- AC2.2: `pnpm typecheck && pnpm lint && pnpm test && pnpm build` verdi
- AC2.3: Bundle size delta < 1% (misurato via `pnpm build` size report)
- AC2.4: Playwright E2E suite pre-existing passa identica
- AC2.5: Screenshot baseline invariati (visual regression check)
- AC2.6: `v2-migration-matrix.md` aggiornato: colonna `Path` riflette nuovi path; sezione "How to use" rewritten

**Rollback**: codemod ha modalità inverse — re-applica con `--reverse` flag. Branch può essere abbandonato senza side-effect su `main-dev`.

### Stage 3 — Conformity fixes (cluster-by-cluster)

**PR titles**: `feat(<feature>): mockup-faithful re-implementation (Stage 3 — <cluster>)`

**Branch pattern**: `feature/issue-{N}-deversioning-fix-<cluster>`

**Scope**: i component flaggati `partial` o `diverge` dallo Stage 1, raggruppati per cluster (es: tutti `agents/*`, tutti `player-detail/*`).

**Procedura per cluster**:
1. Per ogni component non-conforme: implementa mockup-faithful version sotto path canonico
2. E2E test (Playwright) confronta screenshot con mockup HTML renderizzato
3. A11y audit AA-aligned via `@axe-core/playwright`
4. Token compliance check via lint rule custom (no hard-coded colors/spacing)
5. Legacy file eliminato nello stesso PR
6. PR mergea indipendentemente — cluster paralleli OK

**Cluster priority** (proposta — riflette spinte business):

| Cluster | Componenti | Rationale |
|---|---|---|
| `player-detail` (Wave 3) | 5 | Pending da Wave 3, mai implementato |
| `toolkit-detail` (Wave 3) | 6 | Pending da Wave 3 |
| `dashboard` (REFACTOR-FORWARD)¹ | mockup canonico pronto² | 5 entity sections (Games/Players/Agents/Sessions/Events) — diverge da `DashboardClient.tsx` PR #309, re-implementation completa |
| `hub/<entity>` (NEW public) | 3 mockup pronti³ | mockup canonici merged via #1148 (sp4-hub-{games,agents,toolkits}) |

¹ `dashboard` re-etichettato da `NEW` a `REFACTOR-FORWARD` dopo spec-panel 2026-05-14 (D2): `DashboardClient.tsx` PR #309 (chat/session-centric) verrà sostituito dal target mockup (entity-overview-centric). Features kept/dropped/added documentate nel mockup JSDoc top. Sub-issue Pre-Stage-3: #1149.
² Mockup canonico: [`sp4-dashboard.{html,jsx}`](../../../admin-mockups/design_files/sp4-dashboard.jsx). Hero greeting + 4 KPI grid + 5 entity sections (4 in 2×2 grid + Events full-width).
³ Mockup canonici: [`sp4-hub-games.{html,jsx}`](../../../admin-mockups/design_files/sp4-hub-games.jsx), [`sp4-hub-agents.{html,jsx}`](../../../admin-mockups/design_files/sp4-hub-agents.jsx), [`sp4-hub-toolkits.{html,jsx}`](../../../admin-mockups/design_files/sp4-hub-toolkits.jsx). Auth model: `/hub/games` pubblico, `/hub/agents` + `/hub/toolkits` authenticated (decisione D1 spec-panel 2026-05-14).
| `game-nights` (Wave 3) | 8 | Tier L — richiede Phase 0.5 contract |
| `discover` (Wave 3) | 6 | Tier L — richiede Phase 0.5 contract |

**Acceptance criteria per ciascun cluster PR**:
- AC3.1: Pixel-faithful al mockup HTML (visual diff ≤ 2% threshold)
- AC3.2: Pass T (tokens), A (a11y AA), M (motion), V (viewport) — vedi matrice
- AC3.3: Legacy versions eliminate (no coexistence)
- AC3.4: Test coverage ≥ 85% per il cluster
- AC3.5: Routing-map `lib/routes.ts` aggiornata se il cluster introduce nuove route

**Rollback**: per-cluster — revert del singolo PR non impatta altri cluster.

## 4. `DetailPageLayout` primitive (cross-cutting)

Estratto durante Stage 3 come primitive condiviso (NON è un cluster a sé, ma una dipendenza dei cluster detail).

**Path**: `apps/web/src/components/ui/detail-layout/`

**API**:

```tsx
interface DetailPageLayoutProps {
  hero: ReactNode;                    // <DetailHero> o composito feature-specific
  connections?: ConnectionItem[];     // se omesso, ConnectionBar non renderizzato
  tabs?: DetailTabConfig[];           // se omesso, layout flat-scroll (es: player-detail)
  children: ReactNode;                // tab content o flat sections
  footer?: ReactNode;                 // <ContributorsStrip> | <StickyCTA> | actions row
  variant?: 'authenticated' | 'public'; // determina default footer behavior
}
```

**Riusi**:
- `ConnectionBar` esistente da PR #549/#552 (riprodotta 1:1 in SP3)
- `MeepleCard` per liste in tab content
- Token system `--c-*`, `--e-*` AA-compliant (post #807 closure)

**Componibilità (esempi)**:

| Pagina | Variant | Hero | Connections | Tabs | Footer |
|---|---|---|---|---|---|
| `/games/[id]` | authenticated | `GameDetailHero` | toolkit/agent/kb/session | Overview/KPI/FAQ/Rules/Sessions/Agents/KB | actions row |
| `/agents/[id]` | authenticated | `AgentHero` | game/kb | Persona/SystemPrompt/Chat/Settings | DangerZone |
| `/players/[id]` | authenticated | `PlayerHero` + `PlayerOverviewRegion` | game/session/event/agent/toolkit/chat | Sessions/Games/Toolkits/Achievements | — |
| `/shared-games/[slug]` | public | `GameHero` | toolkit/agent/kb/player | Toolkit/Agent/KB | `ContributorsStrip` + `StickyCTA` |
| `/hub/games/[id]` | public | `GameHero` | toolkit/agent/kb | Toolkit/Agent/KB | `StickyCTA` |

## 5. Routing-map canonica

**Path**: `apps/web/src/lib/routes.ts`

```ts
import type { Entity } from '@/types/entity';

export const entityRoute = {
  // authenticated routes
  game:     (id: string) => `/games/${id}`,
  agent:    (id: string) => `/agents/${id}`,
  player:   (id: string) => `/players/${id}`,
  toolkit:  (id: string) => `/toolkits/${id}`,
  kb:       (id: string) => `/kb/${id}`,
  session:  (id: string) => `/sessions/${id}`,
  event:    (id: string) => `/game-nights/${id}`,
  collection: (id: string) => `/library/collections/${id}`,
  // public routes
  'shared-game': (slug: string) => `/shared-games/${slug}`,
} as const satisfies Record<string, (id: string) => string>;
```

**MeepleCard integration**: prop `href?: string` opzionale; se omesso ma `entity + id` presenti, deriva da `entityRoute[entity](id)`. Implementato con `<Link>` di `next/link`.

## 6. Dashboard vs Hub — informational architecture

### Dashboard (`/dashboard` — authenticated)

Route group: `apps/web/src/app/(authenticated)/dashboard/`

Mockup canonico (PR #1149): [`admin-mockups/design_files/sp4-dashboard.{html,jsx}`](../../../admin-mockups/design_files/sp4-dashboard.jsx)

Layout: **Hero greeting + 4 KPI grid** sopra una griglia 2×2 di sezioni per entità + Events full-width:

```
┌─────────────────────────────────────────────────────────┐
│ HERO: "Bentornato, {name}" + 4 KPI (games/sessions/h/wr)│
├──────────────────────────────┬──────────────────────────┤
│ GamesSection (carousel 3)    │ PlayersSection (avatars) │ → /library · /players
│ AgentsSection (grid 2×2)     │ SessionsSection (timeline)│ → /agents · /sessions
├──────────────────────────────┴──────────────────────────┤
│ EventsSection (3 inline, full-width)                    │ → /game-nights
└─────────────────────────────────────────────────────────┘
```

Section variants:
- **Games** — carousel 3 card inline (cover + title + plays count) → `/library`
- **Players** — avatar list orizzontale (5 inline + count badge) → `/players`
- **Agents** — grid 2×2 compact (logo + title + model + status dot) → `/agents`
- **Sessions** — timeline list 3 inline (con live indicator pulse-animated se attive) → `/sessions`
- **Events** — list 3 inline con date display (DD MMM) + participant ratio → `/game-nights`

Decisione D2 spec-panel 2026-05-14: **forward-design**, diverge da `DashboardClient.tsx` PR #309. Stage 3 cluster `dashboard` sarà re-implementation completa:
- **Kept**: Greeting personalizzato (→ hero), Live session indicator (→ Sessions section header)
- **Dropped**: Chat recent cards (→ /chat), Friends row standalone (→ merged in Players)
- **Added**: Games section (primary entity), Agents overview, Events upcoming

### Hub (`/hub/<entity>` — public)

Route group: `apps/web/src/app/(public)/hub/`

Catalogo pubblico **browse globale** per entità principali:

```
/hub/games       → catalogo PUBLIC games          (MeepleCard click → /shared-games/[slug])      → mockup sp4-hub-games
/hub/agents      → catalogo authenticated agents  (MeepleCard click → /hub/agents/[id])          → mockup sp4-hub-agents
/hub/toolkits    → catalogo authenticated toolkits (MeepleCard click → /hub/toolkits/[id])       → mockup sp4-hub-toolkits
```

**Auth model** (decisione D1 spec-panel 2026-05-14, issue #1097):
- `/hub/games`: **pubblico** — visitatori senza login sfogliano. `StickyAccessCta` "Accedi per installare" sempre visibile in basso.
- `/hub/agents` + `/hub/toolkits`: **authenticated** — install inline (hover-revealed); no StickyCTA.

Mockup canonici (PR #1148):
- [`admin-mockups/design_files/sp4-hub-games.{html,jsx}`](../../../admin-mockups/design_files/sp4-hub-games.jsx)
- [`admin-mockups/design_files/sp4-hub-agents.{html,jsx}`](../../../admin-mockups/design_files/sp4-hub-agents.jsx)
- [`admin-mockups/design_files/sp4-hub-toolkits.{html,jsx}`](../../../admin-mockups/design_files/sp4-hub-toolkits.jsx)

Detail "hub" (`/hub/<entity>/[id]`) usa `DetailPageLayout variant="public"` con `StickyCTA` "Accedi per installare" (pattern SP3, fuori scope #1148).

## 7. Failure matrix

| Stage | Failure mode | Detection | Mitigation |
|---|---|---|---|
| 1 | Audit screenshot timeout | Playwright timeout 30s | Retry 3×, fallback `manual_review` flag |
| 1 | Mockup HTML non-renderable | Babel runtime error | Logged, component skipped, flagged in report |
| 2 | Import path collision | `pnpm typecheck` fail | Codemod aborts, branch discarded |
| 2 | Test regression | `pnpm test` fail | Branch discarded, retry codemod con fix |
| 2 | Bundle size +>1% | `pnpm build` size diff | Investigation richiesta prima del merge |
| 3 | Visual diff >2% | Playwright pixelmatch | Block merge, designer review |
| 3 | A11y AA fail | `@axe-core/playwright` | Block merge, refactor required |
| 3 | Legacy deletion rompe import esterno | grep audit pre-delete | `git revert`, investigate consumer |

## 8. Out-of-scope / future work

- **Mobile native app** (React Native): non toccato in questo spec.
- **Storybook**: ricostruzione catalog post-migration — separato spec.
- **Design tokens audit**: il rework `--c-*`/`--e-*` post #807 è considerato stabile; non rivisitato qui.
- **Backend domain renames**: nessun riflesso lato API (path frontend-only).

## 9. Sequencing & estimated effort

| Stage | PR count | Estimated effort | Parallel-safe? |
|---|---|---|---|
| 1 | 1 | 2-3 giorni (agent run + report review) | N/A |
| 2 | 1 | 1-2 giorni (codemod + CI fix) | NO — atomic |
| 3 | ~6 cluster PR | 1-2 settimane (parallelizzabile per cluster) | YES post-Stage 2 |

**Total**: ~2 sprint con 1 dev FTE; ~1 sprint con 2 dev paralleli su Stage 3.

## 10. Open questions for implementation

- Q1: Quale subagent per Stage 1 audit? Candidato `frontend-architect` o `general-purpose` con Playwright browser tools? → da decidere al kickoff PR 1.
- Q2: Naming definitivo `components/features/` vs `components/feature/` vs `components/compositions/`? Proposta corrente: `features/`. Da confermare in PR 2 review.
- Q3: Tracking issue: aprire una umbrella issue su GitHub con sotto-issue per stage? Convenzione `feature/issue-{N}` lo richiede. Decidere al kickoff.

## 11. References

- Mockup source of truth: [`admin-mockups/design_files/`](../../../admin-mockups/design_files/)
- Existing migration matrix (sarà superseded dallo Stage 1 report): [`v2-migration-matrix.md`](../frontend/v2-migration-matrix.md)
- Token system: [`v2-token-system.md`](../frontend/v2-token-system.md), audit [`v2-a11y-token-audit.md`](../frontend/v2-a11y-token-audit.md)
- ConnectionBar reference: PR #549/#552
- SP3 reference implementation: [`sp3-shared-game-detail.jsx`](../../../admin-mockups/design_files/sp3-shared-game-detail.jsx)

---

**Sign-off required from**:
- [x] Project owner — decisioni di principio confermate 2026-05-11 (vedi umbrella #1023)
- [ ] Frontend architecture review — naming `features/` finale
- [ ] PM — apertura umbrella issue + scheduling sprint

# Design System De-versioning & Mockup-Faithful Convergence

| Field | Value |
|---|---|
| **Status** | accepted (Stage 1+2 delivered, Stage 3 in progress) |
| **Date** | 2026-05-11 |
| **Last sync** | 2026-05-13 (umbrella #1023 spec-panel review) |
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
| `dashboard` (NEW) | TBD post audit | Decisione utente: sezioni per entità |
| `hub/<entity>` (NEW public) | TBD post audit | Decisione utente: catalogo pubblico |
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
| `/players/[id]` | authenticated | `PlayerHero` | game/session | — (flat) | — |
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

Layout: griglia di **sezioni per entità di proprietà** dell'utente:

```
┌─────────────────────────────────────────┐
│ GamesSection         (le tue librerie)  │ → MeepleCard[entity=game]   → /games/[id]
│ PlayersSection       (i tuoi avversari) │ → MeepleCard[entity=player] → /players/[id]
│ AgentsSection        (i tuoi agenti)    │ → MeepleCard[entity=agent]  → /agents/[id]
│ SessionsSection      (sessioni recenti) │ → MeepleCard[entity=session]→ /sessions/[id]
│ EventsSection        (game-nights)      │ → MeepleCard[entity=event]  → /game-nights/[id]
└─────────────────────────────────────────┘
```

### Hub (`/hub/<entity>` — public)

Route group: `apps/web/src/app/(public)/hub/`

Catalogo pubblico **browse globale** per entità principali:

```
/hub/games       → catalogo public games          (MeepleCard click → /shared-games/[slug])
/hub/agents      → catalogo public agents         (MeepleCard click → /hub/agents/[id])
/hub/toolkits    → catalogo public toolkits       (MeepleCard click → /hub/toolkits/[id])
```

Detail "hub" usa `DetailPageLayout variant="public"` con `StickyCTA` "Accedi per installare" (pattern SP3).

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

## 10. Resolved questions

Originariamente sezione "Open questions"; tutte chiuse durante Stage 1+2 (vedi §12 change log).

- ~~Q1: Quale subagent per Stage 1 audit?~~ **Risolto** in #1024 / PR #1028 — `frontend-architect` + Playwright visual diff. Report finale: [`docs/for-developers/audits/2026-05-11-mockup-conformity.md`](../audits/2026-05-11-mockup-conformity.md).
- ~~Q2: Naming definitivo `components/features/` vs `components/feature/` vs `components/compositions/`?~~ **Risolto** in #1025 / PR #1032 — adottato `components/features/` (compositions) e `components/ui/` (primitives).
- ~~Q3: Aprire umbrella issue per tracking?~~ **Risolto** — umbrella aperta come #1023, sotto-issue #1024 / #1025 / #1026 per stage.

## 11. References

- Mockup source of truth: [`admin-mockups/design_files/`](../../../admin-mockups/design_files/)
- Existing migration matrix (sarà superseded dallo Stage 1 report): [`v2-migration-matrix.md`](../frontend/v2-migration-matrix.md)
- Token system: [`v2-token-system.md`](../frontend/v2-token-system.md), audit [`v2-a11y-token-audit.md`](../frontend/v2-a11y-token-audit.md)
- ConnectionBar reference: PR #549/#552
- SP3 reference implementation: [`sp3-shared-game-detail.jsx`](../../../admin-mockups/design_files/sp3-shared-game-detail.jsx)

## 12. Change log

| Date | Event | Reference |
|---|---|---|
| 2026-05-11 | Spec drafted, decisioni di principio firmate | umbrella #1023 |
| 2026-05-11 | Stage 1 — Audit automatizzato delivered | issue #1024, PR #1028, report `audits/2026-05-11-mockup-conformity.md` |
| 2026-05-11 | Stage 2 — Path migration codemod delivered, naming `features/` confermato | issue #1025, PR #1032 |
| 2026-05-11 | Freeze "Design System De-versioning" issued in `CLAUDE.md` | umbrella #1023 |
| 2026-05-13 | Spec sync: status `draft` → `accepted`, resolved questions migrate da §10, change log added | this PR |

**Outstanding Stage 3 prerequisite** (raised by spec-panel review 2026-05-13): i cluster `dashboard` e `hub/<entity>` non hanno mockup canonico in `admin-mockups/design_files/`. AC3.1 ("pixel-faithful al mockup HTML") non misurabile fino a creazione mockup. Vedi follow-up issue da aprire pre Stage-3 kickoff su questi cluster.

---

**Sign-off**:
- [x] Project owner — decisioni di principio confermate 2026-05-11 (umbrella #1023)
- [x] Frontend architecture review — naming `features/` confermato in Stage 2 (PR #1032)
- [x] PM — umbrella #1023 + sotto-issue #1024 / #1025 / #1026 aperte, Stage 1+2 delivered

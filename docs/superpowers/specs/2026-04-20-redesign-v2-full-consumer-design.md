# Redesign v2 — Full Consumer App Design Spec

**Date**: 2026-04-20
**Status**: Draft — awaiting user review
**Scope**: Full consumer app (no admin) — sostituire tutta la UI non-admin con i mockup di Claude Design in `admin-mockups/design_files/`
**Target branch**: `redesign/v2` (long-lived)
**Related**: `admin-mockups/README.md` (design handoff hi-fi), `admin-mockups/design_files/`, CLAUDE.md (repo guide), MEMORY.md sessione 2026-04-11 (card-drawer nav already in place)

---

## 1. Goal

Sostituire l'attuale interfaccia utente (non-admin) con il sistema di design hi-fi consegnato in `admin-mockups/design_files/` — 9 entity types, drawer-driven navigation, entity palette warm-neutral, mobile-first con adattamenti desktop.

**Non goal**:
- Admin screens (users/content/analytics/audit/ops) — fuori scope, restano sul look attuale finché non sarà decisa una seconda fase.
- Cambiare il backend, i bounded context, i modelli dominio, i service layer.
- Introdurre feature nuove non presenti oggi.

## 2. Success criteria

1. Tutti gli screen mockup in `01-screens.html` riprodotti pixel-accurate (treat as contract) nell'app Next.js.
2. 13 flussi non coperti dai mockup ricevono nuovi mockup da Claude Design (ref brief `2026-04-20-redesign-v2-claude-design-brief.md`) e implementazione conforme.
3. Parità funzionale completa con l'app attuale non-admin (nessuna feature persa).
4. Coverage backend 90%+ invariato. Frontend target 85%+ mantenuto.
5. A11y AA: contrast ≥ 4.5:1 su tutti i bg entity-colored con testo bianco (già verificato nei mockup).
6. Dark mode funzionante day-one.
7. Bundle size budget rispettato (baseline aggiornata in M0).

## 3. Decisioni architetturali

| Decisione | Scelta | Alternativa scartata | Razionale |
|---|---|---|---|
| Scope | Full consumer (alpha + power, no admin) | Alpha-only, full incl. admin | Copre l'intero target utente finale mantenendo scope finito |
| Strategia transizione | Big bang su `redesign/v2` | Strangler fig, dual-track, per-BC | Codebase pulito, nessuna doppia manutenzione runtime, accettiamo rischio merge compensato da merge settimanali di `main-dev` in v2 |
| Pattern entity detail | Hybrid responsive (drawer mobile / page desktop) | Drawer-puro, route-dedicate, intercepting routes | Mantiene SEO + deep-link desktop, UX nativa su mobile, sfrutta infrastruttura drawer esistente |
| Riuso codice | Riuso comportamentale + rewrite visuale | Riuso totale, rewrite from scratch | `useCascadeNavigationStore`/hooks/MSW/zustand restano, visual layer pulito con nuovi primitivi fedeli ai mockup |
| Sequenza | Vertical slice su screen pilota (Library) | Bottom-up per livelli, Alpha-critical-path, parallelo 2-track | Validazione precoce filiera token-to-test, feedback stakeholder in 1-2 settimane |
| Breakpoint | `md:` 768px switch drawer↔page | Tablet-specific | Allineato Tailwind default + mobile/desktop patterns in `02-desktop-patterns.html` |

## 4. Gap analysis

### 4.1 Mockup vs app attuale (mapping)

| Mockup | Route app attuale | Azione |
|---|---|---|
| Home Feed | `(authenticated)/dashboard/` | Redesign in place |
| Library | `(authenticated)/library/` | **Pilota M2** |
| Search | — | Nuovo screen dedicato |
| Chat list + detail | `(chat)/` | Redesign |
| Profile | `(authenticated)/profile/` | Redesign |
| Session Mode | `(authenticated)/sessions/[id]` | Takeover full-screen |
| Entity Drawer Game | `(authenticated)/games/[id]` | Page (desktop) + drawer (mobile) |
| Entity Drawer Player | `(authenticated)/players/*` | Idem |
| Entity Drawer Session | `(authenticated)/sessions/[id]` | Idem |
| Entity Drawer Agent | `(authenticated)/agents/*` | Idem |
| Entity Drawer KB | `(authenticated)/knowledge-base/` | Idem |
| Entity Drawer Chat | `(chat)/` | Idem |
| Entity Drawer Event | `(authenticated)/game-nights/*` | Idem |
| Entity Drawer Toolkit | `(authenticated)/toolkit/` | Idem |
| Entity Drawer Tool | — | Nuovo |

### 4.2 Pagine/flussi NON coperti dai mockup Claude Design (da creare — vedi brief separato)

13 richieste di design sessions:

1. Auth flow (login / register / forgot / verify-email / magic-link)
2. Onboarding wizard (profilo + pick-games + import BGG)
3. Settings (account, preferenze, privacy, integrazioni)
4. Notifications center (full page)
5. Upload PDF flow (drop → OCR → mapping → publish)
6. Rule Editor + Versions (split editor/preview + diff)
7. Game Night hub (serata multi-sessione)
8. Play Records / Diary (timeline cross-gioco)
9. Discover / Shared catalog (community DB browse)
10. Private Games (variante library)
11. Pipeline Builder / n8n integrations
12. Error / 404 / offline / join-landing
13. Empty states + Loading skeletons (pattern reusable)

Dettaglio in `2026-04-20-redesign-v2-claude-design-brief.md`.

## 5. Architettura

### 5.1 Layer

```
Layer 0 — Foundation
  tokens.css (verbatim da design_files) → app/globals.css
  tailwind.config.ts (entity palette, radius, shadow, motion, font)
  Google Fonts: Quicksand / Nunito / JetBrains Mono
  Theme toggle: data-theme="dark" su <html>, persist in localStorage 'mai-theme'

Layer 1 — Primitivi visivi (NEW)
  <EntityChip size=sm|md>          pill testuale (emoji + label, entity-colored)
  <EntityPip>                      avatar tondo 32px (overlap 8px, max 4 visibili + "+N")
  <EntityCard variant=...>         ex-MeepleCard rewritten (grid|list|compact|featured|hero)
  <ConnectionBar>                  strip orizzontale di pip (riusa logica esistente)
  <Drawer>                         vaul su mobile, Radix Dialog + custom side-panel desktop
  <BottomBar>                      5-tab nav + variante Session (pulse dot, session color)
  <MiniNav>                        breadcrumb + tabs con count badges
  <RecentsBar>                     re-skin del componente esistente

Layer 2 — Shell & layout responsive
  AppShell
    <768  → Drawer-mode (bottom sheet vaul)
    ≥768  → Page-mode (route dedicate con split-view)
  Topbar (logo + search icon + notif icon + avatar)
  Session mode layout (takeover)

Layer 3 — Screens (riusa query hook + mock + zustand)
  Home / Library / Search / Chat / Profile / Session Mode
  Entity drawers/pages per 9 tipi

Layer 4 — Flussi non-mockup (13 pagine, post-design session)
  Auth / Onboarding / Settings / Notifications / Upload / Editor / Versions / GameNight / PlayRecords / Discover / PrivateGames / Pipeline / Errors + Empty
```

### 5.2 Riuso confermato (no touch comportamentale)

- `useCascadeNavigationStore` (stack drawer max 3, recentEntities)
- `DrawerEntityRouter`, `ExtraMeepleCardDrawer`, `SessionDrawerContent` con `initialTabId`
- React Query hooks
- MSW + `scenarioBridge` + DevPanel
- Zustand stores
- Zod schemas
- i18n (se presente)

### 5.3 Rewrite

- `MeepleCard` → `EntityCard` (nuovo path `components/ui/data-display/entity-card/`)
- `BottomBar` completa
- Shell layout responsive
- Tutti i card/surface style con nuovi tokens
- Form primitives: Button/Input/Tabs/Dialog (re-skin shadcn)
- Topbar, MiniNav, RecentsBar (re-skin, logica invariata)

### 5.4 Dipendenze nuove

- `vaul` — bottom sheet drawer physics mobile (non reinventare)
- `framer-motion` — motion system (se non già presente, verificare in M0)
- Google Fonts subset Latin con `display=swap`

## 6. Milestones

| M | Nome | Deliverable | Definition of Done |
|---|---|---|---|
| M0 | Foundation | tokens + tailwind + fonts + theme toggle + audit deps | `04-design-system.html` side-by-side parity, dark/light toggle funzionante, vaul installato, nessuna regression test esistenti |
| M1 | Primitivi | EntityChip/Pip/Card/ConnectionBar/Drawer/BottomBar/MiniNav/Topbar | Test componenti + visual snapshot + a11y (jest-axe), coverage ≥ 85% su nuovi componenti |
| M2 | **Library pilota** | Library mobile+desktop, Game drawer+page, EntityCard migration su Library, filter tabs, grid/list toggle | Parità funzionale con `(authenticated)/library/` attuale, pixel-accurate vs `01-screens.html` Library, stakeholder walk-through OK |
| M3 | Screens mockup rimanenti | Home / Search / Chat list+detail / Profile / Session Mode / drawer+page per Player/Agent/KB/Event/Toolkit/Tool | Parità con `01-screens.html` completa (24 screens), MeepleCard eliminato dal consumer path, Storybook-free (test + snapshot come evidenza) |
| M4 | Claude Design sessions | 13 nuovi mockup HTML in `admin-mockups/design_files/v2/` | Tutte le 13 pagine consegnate con stesso grado di fedeltà dei mockup originali (tokens, fonts, motion) |
| M5 | Implementazione flussi non-mockup | Codice per le 13 pagine M4 | Test + typecheck + lint green, coverage ≥ 85%, Playwright E2E happy path per ogni flusso critico (auth, upload, editor) |
| M6 | Polish + cut-over | Empty/error/skeleton states sistematici, a11y audit, performance budget, E2E full suite, merge `redesign/v2` → `main-dev` | CI green, bundle size entro budget, axe-core zero violations critici, PR merge approvato |

**Parallelismo**: M4 può iniziare durante M1/M2 (non blocca filiera tecnica). M3 e M5 possono avere subagent paralleli una volta che M2 ha stabilito il pattern.

## 7. Testing strategy

- **Unit (Vitest)**: ogni primitivo M1 con test varianti + a11y (jest-axe). Target 85%+.
- **Integration (RTL)**: ogni screen M2/M3 con MSW scenarios esistenti + nuovi scenarios per flussi M5.
- **Visual regression**: Playwright screenshot su Library pilota, poi estensione progressiva; baseline in `redesign/v2`.
- **E2E Playwright**: suite critical path (auth → library → open game → chat → session) in M6. Happy path per ogni flusso M5.
- **Accessibility gate**: M6 audit con axe-core, contrast AA su tutti entity backgrounds.
- **CI gate**: merge su `redesign/v2` bloccato se coverage <85%, typecheck/lint fail, o a11y violations critiche.

## 8. Rischi e mitigazioni

| Rischio | Impatto | Mitigazione |
|---|---|---|
| Branch `redesign/v2` drift da `main-dev` | Alto | Merge settimanale schedulato (lunedì mattina). PR interne frequenti sul branch v2 per review incrementale |
| 13 design sessions bloccano M5 | Medio | M4 parte in parallelo con M1/M2. M5 priorizza auth+onboarding+upload (critical path), il resto segue |
| Hybrid responsive complesso | Medio | Libreria pilota M2 risolve il pattern una volta → replicabile. Nessun pattern desktop prima di M2 done |
| MeepleCard migration diffuso | Medio | No codemod automatico: sostituzione screen-by-screen in M3. Co-esistenza temporanea in v2 tollerata. Nuovo `EntityCard` in path nuovo, vecchio resta fino a cut-over |
| Test coverage cala durante rewrite | Alto | CI bloccante: no merge su v2 se coverage <85%. Test primitivi (M1) prima dei test screen (M2) |
| vaul/Framer Motion/shadcn deps non in repo | Basso | Audit dipendenze in M0. PR dedicato con lockfile review |
| Performance bundle size | Medio | Budget baseline aggiornata in M0, check per milestone. Font subset Latin + display-swap. Code-split per screen |
| Team conoscenza Next.js intercepting routes | Nullo | Non usati (pattern hybrid è route dedicate + drawer component, no intercepting) |
| Mock scenarios da adattare | Basso | `scenarioBridge` pattern già in place, handler esistenti restano validi (stesso backend) |

## 9. Deliverable

- **Questo spec**: `docs/superpowers/specs/2026-04-20-redesign-v2-full-consumer-design.md`
- **Brief Claude Design**: `docs/superpowers/specs/2026-04-20-redesign-v2-claude-design-brief.md`
- **Follow-up plan**: `docs/superpowers/plans/2026-04-20-redesign-v2-library-pilot-plan.md` (primo plan esecutivo, in fase writing-plans)
- **Branch**: `redesign/v2`
- **13 nuovi mockup** in `admin-mockups/design_files/v2/`
- **Issue tracker**: epic GitHub "Redesign v2" con 6 child issue (una per milestone M0-M6, M4 split per pagina)

## 10. Open questions (da risolvere in fase plan)

- Ordine preciso M3 (Home first o Session first dopo Library?)
- Se Private Games (brief #10) sia davvero un design session separato o copribile da filtro Library
- Bundle-size budget numerico preciso (definire in M0 basato su `bundle-size-baseline.json` attuale)
- Strategia feature flag per alpha tester che vogliono provare v2 su staging prima del cut-over (optional, no-feature-flag di default)
- Migration path per utenti con drawer history aperta durante cut-over (probabilmente: clear su version bump)

## 11. Riferimenti

- `admin-mockups/README.md` — handoff design hi-fi completo (647 righe)
- `admin-mockups/design_files/` — mockup sources (tokens, components, mobile-app.jsx, 5 HTML hub)
- `CLAUDE.md` — repo guide (18 BC, CQRS, testing, workflow)
- `MEMORY.md` — sessione 2026-04-11 card-drawer nav (infrastruttura drawer esistente)
- Next.js 16 App Router docs — route groups, parallel routes
- vaul — https://vaul.emilkowal.ski/

---

**Status**: spec scritta, in attesa di review utente prima di procedere a `writing-plans` per il primo plan esecutivo (M0 + M2 Library pilota).

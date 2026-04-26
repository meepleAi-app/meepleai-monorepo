# Redesign v2 — Program Overview

**Date**: 2026-04-21
**Owner**: redesign-v2 program lead
**Branch base**: `refactor/redesign-v2-m6`
**Related specs**:
- `2026-04-21-auth-flow-v2-design.md` (SP1 — in progress)
- `2026-04-21-full-redesign-gap-analysis.md` (source analysis)
- `2026-04-20-redesign-v2-full-consumer-design.md` (consumer vision)

## Goal

Rimpiazzare completamente l'interfaccia consumer + admin + power-user di MeepleAI con il design system v2 (entity-driven, mobile-first, dark/light, 9 entity palette, warm neutrals) partendo dai mockup di Claude Design in `admin-mockups/design_files/` ed estendendoli con i brief SP3/SP4/SP5.

Questo documento è la **mappa programma**: sequencing, dipendenze, ownership, milestone. Non è un plan eseguibile (vedi plan sotto ogni spec individuale).

## Stato attuale (snapshot 2026-04-21)

### Completato ai fini v2

- **M0-M5** (pre-branch fase): token stack (`tokens.css`), componenti base v2 (`apps/web/src/components/ui/v2/`), MeepleCard unificato, connection-bar pattern, drawer stack, RecentsBar, BottomBar mobile.
- **Mockup Claude Design già consegnati**: `auth-flow`, `mobile-app`, `public`, `settings`, `onboarding`, `notifications`, + utility `01-screens`, `02-desktop-patterns`, `03-drawer-variants`, `04-design-system`, `05-dark-mode`.
- **Gap analysis**: `2026-04-21-full-redesign-gap-analysis.md` — documento sorgente per la decomposizione SP.

### In progress

- **SP1 Auth flow v2** (`2026-04-21-auth-flow-v2-design.md`) — task #33 Fase 2.6: 5 primitive nuove (`InputField`, `PwdInput`, `StrengthMeter`, `Divider`, `SuccessCard`) + refactor `LoginForm`/`RegisterForm`/`AuthModal` + migrazione `/login` `/register` `/verification-pending` `/reset-password`. Task 1 InputField: done. Rimangono M1-M4 (primitive residue → forms → AuthModal → pages).

### Bloccato / Parcheggiato

- 2FA setup screen (deferred SP1.5, pending backend audit enrollment endpoints).
- E2E visual regression (infra decision aperta).

## Decomposizione in sub-project (SP)

Il programma è spezzato in 5 sub-project con audience/scope distinti. Ogni SP ha un brief Claude Design (dove serve produrre nuovi mockup) e una spec implementativa (dopo che i mockup sono pronti).

| SP | Nome | Audience | Mockup status | Spec impl | Branch |
|----|------|----------|---------------|-----------|--------|
| **SP1** | Auth flow v2 | Tutti (pre-login) | ✅ `auth-flow.{html,jsx}` | ✅ `2026-04-21-auth-flow-v2-design.md` | `refactor/redesign-v2-m6` (task #33) |
| **SP2** | Mobile core | Utenti loggati (mobile-first) | ✅ `mobile-app.jsx` + `01-screens.html` | ⏳ da scrivere | da pianificare (next) |
| **SP3** | Public secondary | Visitatori non loggati | ⏳ brief pronto `briefs/SP3-*` | ⏳ da scrivere dopo mockup | da pianificare |
| **SP4** | Entity detail desktop | Utenti loggati (desktop) | ⏳ brief pronto `briefs/SP4-*` | ⏳ da scrivere dopo mockup | da pianificare |
| **SP5** | Admin + power-user tools | Admin + power-user | ⏳ brief pronto `briefs/SP5-*` | ⏳ da scrivere dopo mockup | da pianificare |

### Dettaglio per SP

#### SP1 — Auth flow v2 (in corso)

**Scope**: 5 pagine auth + AuthModal + 5 primitive v2.
**Mockup**: `admin-mockups/design_files/auth-flow.{html,jsx,css}` — già consegnato.
**Spec**: `docs/superpowers/specs/2026-04-21-auth-flow-v2-design.md`.
**Milestone interne**:
- M1: Primitive (`InputField` ✅, `PwdInput`, `StrengthMeter`, `Divider`, `SuccessCard`)
- M2: Refactor `LoginForm` + `RegisterForm` per usare primitive
- M3: Refactor `AuthModal`
- M4: Migrazione pagine (`/login`, `/register`, `/verification-pending`, `/reset-password`)
**Success criteria**: vedi spec SP1 `Success criteria`.
**PR target**: `refactor/redesign-v2-m6` (continuazione stessa PR oppure split PR incrementali).

#### SP2 — Mobile core (next)

**Scope**: Bottom nav 5-tab (Home · Cerca · Libreria · Chat · Profilo), home authenticated, libreria mobile, chat mobile, profile mobile, drawer bottom-sheet tabbed. Tutto quello che è "entry point quotidiano mobile" dopo login.
**Mockup**: `admin-mockups/design_files/mobile-app.jsx` + riferimenti a `01-screens.html` — **già disponibili**, spec implementativa da scrivere.
**Componenti v2 attesi**: `MobileBottomBar` (già in M5 ma da allineare), `MobileHomeCard`, `MobileLibraryList`, `MobileChatThread`, `MobileProfileSheet`, `TabbedBottomSheet`.
**Dipendenze**: SP1 completo (auth flow landing consistente), drawer stack (già in M5).
**Milestone interne** (stimabili):
- M1: spec implementativa da `mobile-app.jsx`
- M2: refactor BottomBar + Home authenticated
- M3: Library + Chat mobile
- M4: Profile + drawer integration
- M5: Migrazione route `(authenticated)/*` mobile viewport
**Success criteria**: tutte le route principali rendono pixel-match dal mockup mobile, nav bottom sempre visibile, drawer bottom-sheet per ogni EntityChip tap.

#### SP3 — Public secondary (brief pronto)

**Scope**: 9 pagine secondarie non-auth (FAQ, How it works, Legal, Accept invite, Join/waitlist, Shared games public index + detail, Library public, Contact enhanced).
**Brief Claude Design**: `admin-mockups/briefs/SP3-public-secondary.md` — **pronto, da eseguire**.
**Mockup**: da produrre (9 file `sp3-*.{html,jsx}`).
**Spec implementativa**: da scrivere dopo che i mockup sono consegnati.
**Dipendenze**: `public.jsx` (estendere, non riscrivere), drawer/EntityChip patterns consolidati.
**Priorità implementativa** (consumer funnel impact):
1. Accept invite (#4) + Join (#5) — onboarding nuovo utente
2. FAQ (#1) + How it works (#2) — churn reduction pre-reg
3. Legal template (#3) — compliance
4. Shared games (#6, #7) — feature visibility
5. Library public (#8) — community marketing
6. Contact enhanced (#9) — se gap vs `public.jsx`

#### SP4 — Entity detail desktop (brief pronto)

**Scope**: 16 schermate desktop per 9 entity group — detail pages che oggi mancano o sono incoerenti col v2.
- A. Game index + detail
- B. Agent index + character sheet
- C. Session index + live desktop + summary
- D. Player index + detail
- E. Toolkit index + detail
- F. KB index + document preview
- G. Game Nights calendar + detail
- H. Library desktop
- I. Discover (Netflix-style)

**Brief Claude Design**: `admin-mockups/briefs/SP4-entity-desktop.md` — **pronto, da eseguire**.
**Mockup**: da produrre (~16 file `sp4-*.{html,jsx}`).
**Spec implementativa**: da scrivere dopo mockup.
**Dipendenze**: SP2 completo (drawer stack + EntityChip consolidati).
**Componenti già stabili in produzione (NON ridisegnare)**:
- `ConnectionBar` (`apps/web/src/components/ui/data-display/connection-bar/`) — PR #549 (Step 1.6) + PR #552 (Step 2 call-site migration). API contract: `ConnectionPip[]` con `{ entityType, count, label, icon, isEmpty }`.
- `ConnectionChip` family (`apps/web/src/components/ui/data-display/meeple-card/parts/`) — PR #542, #545, #549, #552.
- `MeepleCard` unificato — già in produzione su 17 call-site.
- `RecentsBar`, `MobileBottomBar`, `MiniNavSlot`, drawer stack — M5.
SP4 deve **istanziare** ConnectionBar passando `connections` builder-driven (vedi `build-connections.ts`), non riprogettarla.
**Priorità implementativa**:
1. Game detail + Agent character sheet — già top-traffic
2. Session live desktop — core live play
3. Library desktop + Discover — engagement
4. KB + Toolkit detail
5. Player + Game Nights
6. Session index (meno frequente)

#### SP5 — Admin + power-user tools (brief pronto)

**Scope**: 14 schermate admin (9) + power-user (5).
- **Admin**: Overview, Users, Content, AI/RAG Quality, KB, Catalog Ingestion, Config, Monitor, Notifications Templates.
- **Power-user**: Editor, Pipeline Builder, n8n, Upload avanzato, Play Records, Versions, Private Games, Dev Tools, UI Library (facoltativo).

**Brief Claude Design**: `admin-mockups/briefs/SP5-admin-tools.md` — **pronto, da eseguire**.
**Mockup**: da produrre (~14 file `sp5-*.{html,jsx}`, desktop-first).
**Spec implementativa**: da scrivere dopo mockup.
**Dipendenze**: `AdminDataTable` componente unificato (emerge da SP5) — da progettare in M1 SP5.
**Priorità implementativa**:
1. Admin Overview + Users — ops quotidiane
2. Admin AI/RAG Quality + KB — core debugging
3. Dev Tools — dev velocity (MeepleDev Phase 2 attivo)
4. Editor + Upload — content creation
5. Admin Monitor/Catalog/Config — piattaforma
6. Pipeline/n8n + Play Records/Versions/Private Games — power-user avanzati

## Sequencing

### Parallelizzabilità

I 5 SP possono essere **parallelizzati parzialmente** perché coprono audience + route distinte:

```
Week  │ SP1 Auth │ SP2 Mobile │ SP3 Public │ SP4 Entity │ SP5 Admin
──────┼──────────┼────────────┼────────────┼────────────┼──────────
  1   │  M1-M2   │            │  mockup    │  mockup    │  mockup
  2   │  M3-M4   │  spec/M1   │  mockup    │  mockup    │  mockup
  3   │  ✅ done │  M2-M3     │  spec      │  spec      │  spec
  4   │          │  M4-M5     │  M1-M2     │  M1        │  M1
  5   │          │  ✅ done   │  M3        │  M2-M3     │  M2-M3
 ...
```

Lane indipendenti:
- **Claude Design lane** (SP3/SP4/SP5 mockup) — esegue i 3 brief in parallelo nel `admin-mockups/design_files/` senza toccare codice.
- **Implementation lane** (SP1 → SP2 → SP4/SP5 impl) — procede lineare per evitare conflitti su shared primitives.
- **SP3 implementation** può partire prima di SP2 fine perché impatta solo `(public)/` routes.

### Ordine consigliato

1. **Now**: SP1 auth M2-M4 (completamento task #33).
2. **Parallel**: lancia 3 sessioni Claude Design con brief SP3/SP4/SP5 — produce mockup.
3. **After SP1**: spec + impl SP2 mobile core.
4. **After SP2**: spec + impl SP4 entity desktop (ha più dipendenze su drawer/connection-bar consolidati in SP2).
5. **Parallel a SP4**: spec + impl SP3 public (impatto isolato su `(public)/`).
6. **Last**: spec + impl SP5 admin (più tardi perché audience limitata e alcuni componenti come `AdminDataTable` emergono durante).

### Gate criteria tra SP

Prima di chiudere un SP per passare al successivo:

1. **Token hygiene**: nessun hardcoded color/spacing fuori da `tokens.css`.
2. **Dark mode**: ogni nuova pagina funziona light + dark.
3. **Responsive**: se desktop SP, fallback mobile ≥ "usa desktop per questa funzione"; se mobile SP, funziona su 375px.
4. **Test coverage**: primitive v2 con dedicated `__tests__/*.test.tsx` (pattern SP1). Pagine con smoke test route-level.
5. **Accessibility**: WCAG AA contrasto, focus visibile, ARIA essenziale. Lighthouse a11y ≥ 95 su landing pagine pubbliche.
6. **Bundle delta**: aggiornare `bundle-size-baseline.json` con motivazione nel PR.
7. **No regressioni**: test Vitest + Playwright esistenti rimangono green.

## Dipendenze e rischi

### Dipendenze tecniche

- **`tokens.css`**: invariato durante SP1-SP5. Ogni modifica richiede review cross-SP.
- **Tailwind config**: mappa HSL entity tokens. Cambi additivi safe, rename breaking.
- **`MeepleCard`**: già unificato, NON creare varianti parallele. Estendere con nuovi entity types solo se servono (ma constraint è: 9 entity type, no nuovi).
- **Drawer stack**: `useCascadeNavigationStore` è contract. Modifiche richiedono migration di tutti i consumer.
- **Connection-bar**: ✅ stabile in produzione (PR #549 Step 1.6 + PR #552 Step 2). Componente: `apps/web/src/components/ui/data-display/connection-bar/ConnectionBar.tsx`. Tipi: `types.ts` (`ConnectionPip`, `ConnectionBarProps`). Builder puri: `build-connections.ts` (`buildGameConnections`, `buildAgentConnections`, `buildSessionConnections`). SP4 deve **istanziare** passando `connections` props, NON ridisegnare. SP3 mockup pubblici che mostrano relazioni tra entity (es. shared-game-detail) usano la stessa primitive.

### Rischi

| Rischio | Impatto | Mitigazione |
|---------|---------|-------------|
| Mockup Claude Design non allineati ai token esistenti | SP3/SP4/SP5 impl blocked | `_common.md` preamble obbligatorio + review mockup prima di spec impl |
| Drift tra spec SP e mockup (mockup aggiornato post-spec) | Impl diverge | Commit mockup PRIMA di scrivere spec impl; spec referenzia hash commit mockup |
| `AdminDataTable` progettato in fretta durante SP5 | 9+ tabelle inconsistenti | Primo task SP5 impl = progettare `AdminDataTable` completo con varianti |
| Bundle bloat da primitive parallele | Perf budget sfondato | Bundle baseline aggiornato per PR, alert su delta >50KB |
| Regressioni a11y su density admin | Non-compliance WCAG | A11y check esplicito in DoD SP5, target hit area 44x44 mantenuto |
| SP1 `OAuthButtons.tsx` delete prima che tutti consumer migrino | Build broken | Spec SP1 già lista tutti consumer; cleanup solo dopo migrazione completa |

### Non-goal (esplicito)

Questo programma **NON** include:
- 2FA setup (SP1.5, pending backend)
- Nuovi entity type (restano 9)
- Nuovi colori base (solo tint esistenti)
- E2E visual regression infrastructure
- i18n multilingua (app resta italiano)
- Redesign dell'infra (API, DB, Redis restano invariati)
- Migrazione a framework diverso (resta Next.js 16 + React 19)
- Backend changes (ogni SP è front-only)

## Deliverable programma

### File pronti in questa spec run

| Artefatto | Path | Stato |
|-----------|------|-------|
| Common brief | `admin-mockups/briefs/_common.md` | ✅ |
| Brief SP3 | `admin-mockups/briefs/SP3-public-secondary.md` | ✅ |
| Brief SP4 | `admin-mockups/briefs/SP4-entity-desktop.md` | ✅ |
| Brief SP5 | `admin-mockups/briefs/SP5-admin-tools.md` | ✅ |
| Program overview | `docs/superpowers/specs/2026-04-21-redesign-v2-program.md` | ✅ (this file) |
| Spec SP1 | `docs/superpowers/specs/2026-04-21-auth-flow-v2-design.md` | ✅ (in impl) |

### Da produrre nelle fasi successive

| Artefatto | Owner | Trigger |
|-----------|-------|---------|
| Mockup SP3 (9 file `sp3-*`) | Claude Design session | Brief SP3 ready (now) |
| Mockup SP4 (16 file `sp4-*`) | Claude Design session | Brief SP4 ready (now) |
| Mockup SP5 (14 file `sp5-*`) | Claude Design session | Brief SP5 ready (now) |
| Spec impl SP2 | Eng | After SP1 done |
| Spec impl SP3 | Eng | After mockup SP3 consegnati |
| Spec impl SP4 | Eng | After mockup SP4 consegnati + SP2 done |
| Spec impl SP5 | Eng | After mockup SP5 consegnati |
| Plan impl per ogni SP | writing-plans skill | After spec impl |

## Success criteria programma

Il programma v2 è **done** quando:

1. **Tutte le route consumer** (pre-login, authenticated mobile/desktop) rendono pixel-match dai mockup v2.
2. **Tutte le route admin + power-user** migrate a v2 con `AdminDataTable` unificato.
3. **Zero regressioni test** rispetto a baseline pre-v2 (~13K test backend + suite frontend).
4. **A11y Lighthouse** ≥ 95 su route pubbliche + ≥ 90 su route autenticate/admin.
5. **Bundle size** entro +10% baseline pre-v2 (primitive condivise riducono overhead).
6. **Deprecations cleanup**: `shadcn/ui` wrapper legacy rimossi, `AccessibleFormInput`/`LoadingButton`/`OAuthButtons` eliminati (inizio SP1).
7. **Documentation**: ogni SP ha spec impl + plan + PR description con before/after screenshot.

## Escalation path

Se un SP incontra blocker:
- **Mockup gap** (Claude Design non copre un caso): apri issue con label `redesign-v2 mockup-gap`, aggiungi al brief SP corrispondente, non fare guess in impl.
- **Token gap** (serve nuovo token): PR dedicata a `tokens.css` con review cross-SP, NON aggiungere in-line.
- **Primitive gap** (componente v2 mancante): progetta prima in `apps/web/src/components/ui/v2/<name>/` con test, poi consumer migration.
- **Performance blocker** (bundle size sfondato): escalation a perf engineer con spec SP corrente in allegato.

## Riferimenti

- Brief preamble: `admin-mockups/briefs/_common.md`
- Gap analysis: `docs/superpowers/specs/2026-04-21-full-redesign-gap-analysis.md`
- Claude Design output dir: `admin-mockups/design_files/`
- Design tokens: `admin-mockups/design_files/tokens.css`
- v2 components: `apps/web/src/components/ui/v2/`
- CLAUDE.md redesign rules: sezione "Card Components" e "Code Standards"

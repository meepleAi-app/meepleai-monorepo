# V2 Migration — Phase 1 Execution Plan

**Data**: 2026-04-27
**Companion of**: `docs/superpowers/specs/2026-04-26-v2-design-migration.md`
**Status**: APPROVED (utente sign-off 2026-04-27)
**Scope**: Sequenza esecutiva Phase 1 + inclusione SP3 secondary public routes + FAQ pilot.

---

## 1. Why this companion doc

Lo spec di programma del 2026-04-26 (`2026-04-26-v2-design-migration.md`) copre wave 1+2 SP4 (8 route authenticated). I 5 mockup SP3 secondary public (FAQ, join, shared-games, accept-invite, shared-game-detail) sono **inclusi nella visual regression baseline Phase 0** (PR #575) ma **non listati nelle Phase 1/2** del programma.

Questa lacuna viene chiusa qui aggiungendo **Wave A** (SP3 public) come wave low-risk d'apertura, utile per:

1. Validare il workflow Big-Bang in-place su pagine pubbliche a basso blast-radius
2. Sbloccare metrica visibile (FAQ search migliorata) che dimostra valore della migrazione
3. Calibrare velocity stimata wave 1+2 (~6-8 settimane) con dati empirici

Il companion **non rivede** decisioni del programma (Big-Bang, no feature flag, Playwright visual regression nativo) — ne formalizza solo la sequenza esecutiva.

---

## 2. Wave reorganization — A/B/C/D

| Wave | Scope | Routes | Risk | Effort |
|------|-------|--------|------|--------|
| **A — SP3 public** | Pagine pubbliche secondarie | `/faq`, `/join`, `/shared-games`, `/shared-games/[id]`, `/invites/[token]` | **Low** | 2-3 settimane |
| **B — Auth indexes** | Hub authenticated entity-list | `/games`, `/agents`, `/library` | Medium | 3-4 settimane |
| **C — Auth detail** | Pagine detail entity | `/games/[id]`, `/agents/[id]` | Medium-High | 2-3 settimane |
| **D — Sessions triade** | Sessions live + summary | `/sessions`, `/sessions/[id]/live`, `/sessions/[id]` | **High** | 4-5 settimane |

**Strategia**: A → B → C → D (low-risk first, learning compounds). Ordine intra-wave: vedi sezione 3.

---

## 3. Wave A — SP3 public routes (PILOTA)

| # | Route target | Mockup source | Backend deps | Effort |
|---|-------------|---------------|--------------|--------|
| A.1 | `/faq` | `sp3-faq-enhanced.jsx` | nessuno (static markdown) | **3-4 gg (PILOTA)** |
| A.2 | `/join` | `sp3-join.jsx` | endpoint waitlist (esistente) | 2-3 gg |
| A.3 | `/shared-games` | `sp3-shared-games.jsx` | `GET /api/v1/shared-games` | 3-4 gg |
| A.4 | `/shared-games/[id]` | `sp3-shared-game-detail.jsx` | `GET /api/v1/shared-games/{id}` + ConnectionBar 1:1 prod | 4-5 gg |
| A.5 | `/invites/[token]` | `sp3-accept-invite.jsx` | endpoint accept invite + 7 stati flow + `.ics` deferred | 4-5 gg |

### 3.1 A.1 FAQ pilot — perché questa route

- ✅ **Zero backend deps** (static markdown frontmatter, popularRank curated)
- ✅ **Zero auth flow** — pubblica, no session/RLS edge cases
- ✅ **Search via URL hash** (`#q=...`) — pattern stateless testabile
- ✅ **Componenti riusabili nuovi** (FAQSearchBar, QuickAnswerCard, CategoryTabs, AccordionItem v2) → ROI alto downstream
- ✅ **Bundle delta minimo previsto** (~+15-25 KB vs budget +50 KB per PR)
- ✅ **Mockup già visual-regression baseline** (sp3-faq-enhanced.png, PR #575)

### 3.2 Pattern PR Wave A (template)

Branch: `feature/migration-wave-a-{n}-{slug}` da `main-dev`

**Test infra introdotta da A.1** (riusata da A.2-A.5 + tutta Phase 1):

1. `apps/web/e2e/visual-migrated/`: nuovo project Playwright che renderizza la **route prod** (non il mockup) con dati stub equivalenti a `data.js` del mockup. Snapshot confrontati con `e2e/visual-mockups/__snapshots__/{slug}.png` (la baseline mockup committata in PR #575).
2. `apps/web/e2e/v2-states/`: nuovo project per stati `default`/`empty`/`loading`/`error` per ogni route migrata.
3. **Hybrid masking**: zone dinamiche (timestamp relativi, contatori live, foto utente) marcate con `data-dynamic` HTML attribute → Playwright `mask: [page.locator('[data-dynamic]')]`.

**CI gates** (per ogni PR Wave A):
- ✅ Typecheck, Lint (incl. `local/no-hardcoded-hex`)
- ✅ Vitest unit (target 85%+ component nuovi)
- ✅ E2E happy path (nav → page render → search/click → no console errors)
- ✅ `e2e/visual-migrated/{slug}.spec.ts` matcha baseline mockup entro `maxDiffPixelRatio 0.001`
- ✅ `e2e/v2-states/{slug}.spec.ts` 4 stati (default/empty/loading/error)
- ✅ Bundle delta < +50 KB
- ✅ GitGuardian (no fake-UUID, no secret pattern)

**Body PR template** (sezione 4.1 spec parent + estensioni Wave A):
- Mockup source link
- Component nuovi creati (lista path)
- Component legacy rimossi (`git rm` esplicito)
- Side-by-side mockup vs prod artifact (screenshot diff allegato)
- Bundle delta output `pnpm size`
- Rollback comando: `git revert <sha>`

---

## 4. Wave B — Authenticated indexes (post-Wave A)

| # | Route | Mockup | Issue | Note |
|---|------|--------|-------|------|
| B.1 | `/games` | `sp4-games-index.jsx` | TBD | Introduce `AdvancedFiltersDrawer` (riusato in B.2) |
| B.2 | `/agents` | `sp4-agents-index.jsx` | TBD | Sidebar + EmptyAgents |
| B.3 | `/library` desktop | `sp4-library-desktop.jsx` | **#574** | Già OPEN, parte Wave B |

Effort: 3-4 settimane. Pattern uguale Wave A.

## 5. Wave C — Authenticated detail (post-Wave B)

| # | Route | Mockup | Note |
|---|------|--------|------|
| C.1 | `/games/[id]` | `sp4-game-detail.jsx` | Tabs animated + ConnectionBar (già prod) |
| C.2 | `/agents/[id]` | `sp4-agent-detail.jsx` | AgentCharacterSheet primitives (~7 nuovi) |

## 6. Wave D — Sessions triade (post-Wave C)

| # | Route | Mockup | Note |
|---|------|--------|------|
| D.1 | `/sessions` | `sp4-sessions-index.jsx` | ConnectionChipStripFooter |
| D.2 | `/sessions/[id]/live` | `sp4-session-live.jsx` + parts | **Split in 2 sub-PR** raccomandato (foundation + interactions). WCAG critical: dialog+aria-modal+focus trap |
| D.3 | `/sessions/[id]` | `sp4-session-summary.jsx` + parts | Confetti + ShareCard preview-only |

---

## 7. Sequencing rules

1. **No parallel waves** — termina Wave N prima di iniziare Wave N+1. Eccezione: dentro lo stesso wave, PR sequenziali ma branch in parallelo OK se 0 conflitti.
2. **Wave A.1 (FAQ) gate**: se workflow visual-migrated + v2-states non funziona → STOP, revise spec, NON procedere con A.2-A.5.
3. **Phase 3 review gate** (sezione 6 spec parent) DOPO Wave D, PRIMA di Wave 3+SP5.

---

## 8. Open Questions deferred

1. **Wave 3 mockup mancanti** (sp4-player-detail D, sp4-toolkit-detail E, sp4-kb-detail F, sp4-game-nights-index G, sp4-discover I): in arrivo da Claude Design (~6 giorni). Ridiscutere dopo Phase 3 review.
2. **SP3 backend `.ics` endpoint** (A.5 accept-invite): deferred. Bottone disabled con tooltip "Coming soon" finché backend non pronto.
3. **axe-core CI gate**: spec parent open question #2 ancora pending. Raccomandazione: introdurre in Wave A.1 PR come gate report-only, escalate a blocking in Wave B.

---

## 9. Issues mapping

| Issue | Wave | Status |
|-------|------|--------|
| Umbrella Phase 1 (TBD) | All waves | TBD |
| Wave A child (TBD) | A | TBD |
| Wave B child (TBD) | B | TBD (#574 = sub-task B.3) |
| Wave C child (TBD) | C | TBD |
| Wave D child (TBD) | D | TBD |
| #574 | B.3 (library) | OPEN |
| #573 | Phase 0 contract matrix | OPEN |

---

**Next action**: aprire issue umbrella + 4 wave issue + iniziare branch Wave A.1 FAQ.

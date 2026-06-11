# EPIC #2096 — M6 + M4 final closure (Info card + Toolbox coming-soon)

**Status**: design approved 2026-06-11 sess.46p brainstorming
**Owner**: badsworm@gmail.com
**Sub-issue**: TBD (to create as combined `M6 + M4 EPIC #2096 final closure`)
**Parent EPIC**: [#2096 — /library/[gameId] sp3 mockup rebuild — 6 milestone](https://github.com/meepleAi-app/meepleai-monorepo/issues/2096)
**Blocks**: DS-17-10 sp3 sub-issue reactivation (deferred per memory note `ds-17-10-sp3-deferred-decisions.md`)

## 1. Context

Closure milestone EPIC #2096 (5/7 shipped, 71% pre-sessione). I 2 milestone rimasti (M6 Info card style ~2h + M4 Toolbox cards community style ~4h) sono refactor visuali pure-FE che chiudono il rebuild `/library/[gameId]` allineato al mockup `admin-mockups/design_files/sp3-shared-game-detail.jsx`. Closure di #2096 unlocks DS-17-10 sp3 sub-issue (deferred sess.46o per `sp3-shared-game-detail` rebuild blocker).

Stato cluster post sess.46o:

| Milestone | Sub-issue | Status |
|---|---|---|
| M1 GameHero v2 | #2100 | ✅ CLOSED (PR #2101) |
| M2 Tabs animated underline | #2102 | ✅ CLOSED (PR #2103) |
| M3 ConnectionBar pip community | _(no sub)_ | ✅ shipped inline |
| **M4 Toolbox cards community style** | _(no sub)_ | ❌ this spec |
| M5 ContributorsStrip | #2036 alt track | ✅ shipped |
| **M6 Info card style** | _(no sub)_ | ❌ this spec |
| M7 Layout restructure | #2105 | ✅ CLOSED (PR #2108) |

main-dev tip: `e320b2de0` post DS-17-11 sp6-7-nano cluster.

## 2. Decisioni user-locked (8 DEC sessione 46p)

| # | Decisione | Rationale |
|---|---|---|
| DEC-1 | M6 House rules → CTA Card link to House Rules tab (sempre visibile) | Mantiene separation of concerns (House Rules è tab separato, ID `houseRules` vietato rinominare per #2010). Card always visible = engagement positivo anche con 0 house rules. |
| DEC-2 | M6 Description "rich" = plain text in card boundary + heading polish (no Markdown, no expand/collapse) | Conservative + safe. No XSS risk, no bundle bloat. "Rich" interpretato come visual hierarchy (heading + card boundary) non content formatting. |
| DEC-3 | 1 sub-issue combined "M6 + M4 final closure" → 1 spec + 1 plan + 1 PR | 1 admin-squash merge (P145 37a), 1 spec doc, 1 plan doc, 1 closure cycle. PR ~250 LOC + ~10 test ancora reviewabile. Risk: revert atomic se M4 fail (commit isolati). |
| DEC-4 | M4 = Coming-soon card mockup-style + CTA disabled (no BE work, no listing impl) | Mantiene scope ~4h cap. No data layer impl, no listing toolkit-per-game. Visual upgrade only consistent con M6 card boundary pattern. |
| DEC-5 | Manual + smoke test only (no new unit tests, no snapshots) | GameInfoTab esistenti 9 test text-based assertions resilienti a layout refactor. GameToolboxTab 0 test esistenti, 0 nuovi. Verifica via lint + typecheck + manual screenshot. Snapshot suite retirata sess.46m (#1066). |
| DEC-6 | Pre-merge designer screenshot verification | Capture screenshot Playwright/Chrome MCP + embed PR body + sync gate fino a designer 👍. Pattern non-default (Phase B DS-17 post-merge waiver) ma user-locked per closure milestone visivo. |
| DEC-7 | Inline card via shadcn/ui `Card` primitive (`@/components/ui/card`) | Riusa `Card`/`CardHeader`/`CardTitle`/`CardDescription`/`CardContent`/`CardFooter` consolidati. Pattern shadcn canonical, design tokens semantic (`bg-card`/`border-border/50`/`text-card-foreground`). |
| DEC-8 | Mantieni `dl/dt/dd grid-cols-[auto_1fr]` wrapped in `<CardContent>` (max test stability) | 9 test esistenti `getByText('Designer')`, `getByText('Klaus Teuber, ...')` rimangono 100% green. Semantic dl preserved per accessibility (key-value pairs ARIA). |

### Defaults applicati (open questions post-design)

- **Card hover override**: Card 1 (Descrizione) e Card 2 (Informazioni) sono static content → override `hover:translate-y-0 hover:shadow-sm` per disabilitare lift di default shadcn (`hover:-translate-y-0.5 hover:shadow-md`). Card 3 (House Rules CTA) preserva default lift (è clickable).
- **CTA wiring**: Opt B URL navigation via `useRouter().replace('?tab=houseRules')`. Canonical App Router pattern, survives URL bookmarking, no callback prop drilling. `GameTabsPanel` ha già `useEffect` sync su `initialTab` change (line 86-91).

## 3. Architecture

### 3.1 M6 — `GameInfoTab.tsx` post-refactor

3 Card sequence (top-down): Description → Specs → House Rules CTA.

```tsx
'use client';

import { useRouter, useSearchParams } from 'next/navigation';

import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';
import { cn } from '@/lib/utils';

import type { GameTabProps } from './types';

export function GameInfoTab({ gameId, variant, isNotInLibrary }: GameTabProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { data: game, isLoading, isError } = useLibraryGameDetail(gameId, !isNotInLibrary);

  const containerClass = cn('flex flex-col', variant === 'desktop' ? 'gap-4 p-6' : 'gap-3 p-4');

  // ... isNotInLibrary / isLoading / isError early returns (invariati) ...

  const staticCardClass = 'hover:translate-y-0 hover:shadow-sm';
  const playersLabel = /* ... esistente ... */;

  const handleOpenHouseRules = () => {
    const params = new URLSearchParams(searchParams?.toString() ?? '');
    params.set('tab', 'houseRules');
    router.replace(`?${params.toString()}`, { scroll: false });
  };

  return (
    <div role="tabpanel" aria-labelledby="game-tab-info" className={containerClass}>
      {/* Card 1: Descrizione */}
      {game.description && (
        <Card className={staticCardClass} data-testid="game-info-description">
          <CardHeader>
            <CardTitle className={variant === 'desktop' ? 'text-base' : 'text-sm'}>
              Descrizione
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="whitespace-pre-wrap text-sm leading-relaxed text-muted-foreground">
              {game.description}
            </p>
          </CardContent>
        </Card>
      )}

      {/* Card 2: Informazioni (specs grid dl preservato) */}
      <Card className={staticCardClass}>
        <CardHeader>
          <CardTitle className={variant === 'desktop' ? 'text-base' : 'text-sm'}>
            Informazioni
          </CardTitle>
        </CardHeader>
        <CardContent>
          <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-2 text-sm">
            {/* 9 esistenti conditional rows invariati */}
          </dl>
        </CardContent>
      </Card>

      {/* Card 3: House Rules CTA (sempre visibile, hover lift default) */}
      <Card>
        <CardContent className="flex items-center justify-between gap-4 p-6">
          <div className="flex-1">
            <h4 className="font-heading font-semibold text-foreground">
              House Rules personalizzate
            </h4>
            <p className="text-sm text-muted-foreground">
              Aggiungi varianti e regole della casa per questo gioco.
            </p>
          </div>
          <Button variant="outline" onClick={handleOpenHouseRules}>
            Apri House Rules →
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
```

**Notes**:
- `data-testid="game-info-description"` preservato sul Card 1 wrapper (test resilience).
- `dl` invariato dentro `CardContent` → 9 test esistenti pass 100%.
- `useRouter` + `useSearchParams` da `next/navigation` (App Router idiom).
- `router.replace` con `scroll: false` per evitare scroll-to-top spurio.

### 3.2 M4 — `GameToolboxTab.tsx` post-refactor

1 Card placeholder con icon + heading + description + CTA disabled.

```tsx
'use client';

import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

import type { GameTabProps } from './types';

export function GameToolboxTab({ variant, isNotInLibrary }: GameTabProps) {
  const containerClass = cn('flex flex-col', variant === 'desktop' ? 'gap-4 p-6' : 'gap-3 p-4');

  if (isNotInLibrary) {
    return (
      <div role="tabpanel" aria-labelledby="game-tab-toolbox" className={containerClass}>
        <p className="text-sm text-muted-foreground">
          Aggiungi il gioco alla libreria per usare il toolbox.
        </p>
      </div>
    );
  }

  return (
    <div role="tabpanel" aria-labelledby="game-tab-toolbox" className={containerClass}>
      <Card className="hover:translate-y-0 hover:shadow-sm">
        <CardHeader className="flex flex-row items-start gap-3 space-y-0">
          <div
            aria-hidden="true"
            className="flex h-11 w-11 shrink-0 items-center justify-center rounded-md bg-entity-toolkit/12 text-2xl text-entity-toolkit"
          >
            🧰
          </div>
          <div className="flex-1 space-y-1.5">
            <CardTitle className={variant === 'desktop' ? 'text-lg' : 'text-base'}>
              Toolbox
            </CardTitle>
            <CardDescription>
              Strumenti rapidi per il gioco: dadi, timer, punteggi, note e altro ancora.
            </CardDescription>
          </div>
        </CardHeader>
        <CardContent>
          <p className="text-xs italic text-muted-foreground">
            Integrazione completa del toolbox in arrivo.
          </p>
        </CardContent>
        <CardFooter>
          <Button variant="outline" disabled className="cursor-not-allowed">
            In arrivo
          </Button>
        </CardFooter>
      </Card>
    </div>
  );
}
```

**Mockup parity**: 44x44 icon riusa pattern `ToolkitPublicListItem` (mockup line 411-417, `width: 44, height: 44, borderRadius: var(--r-md), background: hsl(toolkit / .12), color: hsl(toolkit), fontSize: 22`). `bg-entity-toolkit/12 + text-entity-toolkit` = entity utility consolidata (token canonicalization Tier 4 shipped 2026-05-12).

## 4. Data flow

- **M6**: invariato. `useLibraryGameDetail(gameId, !isNotInLibrary)` hook esistente. `LibraryGameDetail` shape preservato (designers, gamePublisher, gameYearPublished, minPlayers/maxPlayers, playingTimeMinutes, complexityRating, categories, mechanics, addedAt, description).
- **M4**: invariato (zero data fetch — placeholder card statico).
- **House Rules CTA**: zero fetch. URL nav `?tab=houseRules` → `GameTabsPanel` sync via `useEffect` su `initialTab` (line 86-91) → activeTab change → render `<GameHouseRulesTab>`.

## 5. Error / loading / empty states

### M6
- `isNotInLibrary === true`: short copy gate `"Aggiungi questo gioco alla tua libreria per vedere tutti i dettagli."` (invariato).
- `isLoading`: `"Caricamento in corso…"` (invariato).
- `isError || !game`: `"Impossibile caricare i dettagli del gioco."` (invariato).
- `game.description == null`: Card 1 (Descrizione) **non rendered** (esistente conditional).
- `game.description != null + nessun spec field`: Card 1 + Card 2 con dl vuoto + Card 3 CTA.

### M4
- `isNotInLibrary === true`: short copy gate `"Aggiungi il gioco alla libreria per usare il toolbox."` (invariato).
- Otherwise: Card placeholder sempre rendered.

## 6. Testing

### DEC-5 — Smoke + manual

| Layer | Action | Acceptance |
|---|---|---|
| Unit (M6) | `pnpm test GameInfoTab` — esistenti 9 test invariati | 9/9 pass, no regression |
| Unit (M4) | nessun nuovo test | n/a |
| Lint | `pnpm lint` + `pnpm lint:tokens` (DS-15 error mode) + `pnpm lint:bgg` (#1903 ToS) | 0 violations |
| Typecheck | `pnpm typecheck` | 0 errors |
| Manual | Boot dev server, navigate `/library/[seeded-gameId]?tab=info` + `?tab=toolbox` | Visual conformance vs sp3 mockup |
| Screenshot | Playwright/Chrome MCP capture desktop 1440x900 viewport | 2 PNG embedded in PR body |

### Edge cases verificare manualmente

- M6 Card 3 CTA click → URL `?tab=houseRules` → House Rules tab attivo + animated underline slide.
- M6 mobile variant (variant="mobile") → padding `gap-3 p-4` + title size `text-sm`.
- M6 game con 0 descrizione → solo Card 2 + Card 3 (Card 1 skipped).
- M6 game con dl vuoto (no designers/publisher/year/...) → Card 2 con dl vuoto (degraded ma renderable).
- M4 isNotInLibrary → fallback copy (no Card).

## 7. Designer review gate (DEC-6)

### Process

1. Post local impl finita: boot dev server `pnpm dev` (port 8080 via Docker o standalone Next).
2. Seed game (riusa fixtures `seedAuth*.ts`, `seedLibrary` fixture). Esempio: `gameId = 00000000-0000-4000-8000-000000000001` con `gameTitle = 'Catan'` + descrizione + designers + categorie + meccaniche + addedAt.
3. Capture 2 screenshot via Playwright headless OR Chrome MCP:
   - `/library/{gameId}?tab=info` desktop 1440x900 → `pr-screenshot-m6-info-tab.png`
   - `/library/{gameId}?tab=toolbox` desktop 1440x900 → `pr-screenshot-m4-toolbox-tab.png`
4. Embed in PR body sotto `## Designer review` con caption mockup comparison.
5. PR body legend top: `**DESIGNER REVIEW PENDING** — admin-squash merge bloccato fino a designer 👍`.
6. Wait for designer review comment.
7. On 👍 → admin-squash merge P145 37a volta.
8. On 👎 → iterate refactor + re-capture + re-request.

### Fallback (forward-refactor mockup obsolete check)

Verifica `admin-mockups/design_files/sp3-shared-game-detail.fidelity.json` post-DS-17 Phase B audit. Se `design_intent === 'forward-refactor-obsolete'` → **skip designer review gate**, ship as-is con annotation in PR body.

### Tool choice

- **Preferito**: Chrome MCP `mcp__claude-in-chrome__*` via dev server local (basta load tools via ToolSearch). Pro: integrated session, fast iteration.
- **Alternative**: Playwright standalone script `apps/web/e2e/m6-m4-screenshot-capture.spec.ts` one-off (no commit, discard post-PR). Pro: replayable.
- Writing-plans skill decide tool finale.

## 8. Effort recap

| Task | Effort |
|---|---|
| 1. Branch + sub-issue #TBD creation | 15 min |
| 2. M6 Card 1 Descrizione refactor | 30 min |
| 3. M6 Card 2 Informazioni (dl wrap) | 20 min |
| 4. M6 Card 3 House Rules CTA + URL nav wiring | 40 min |
| 5. M6 Cypress integration check (URL sync con animated underline) | 20 min |
| 6. M4 Toolbox Card placeholder | 1h |
| 7. M4 manual check `isNotInLibrary` fallback | 10 min |
| 8. Lint + typecheck + test run | 15 min |
| 9. Screenshot capture + PR body | 30 min |
| 10. Designer review wait (variable) | sync block (out of estimate) |
| 11. PR admin-squash merge + EPIC #2096 closure | 15 min |
| **Total active work** | **~3h35** |

Sotto cap originale EPIC #2096 (M6 ~2h + M4 ~4h = ~6h). Riduzione ~40% via DEC-3 combined + DEC-4 scope reduced + DEC-5 no new tests.

## 9. Risk register

| # | Risk | Probability | Impact | Mitigation |
|---|---|---|---|---|
| R1 | Existing 9 test rompono per DOM-structure change | Low | High | DEC-8 preserve `dl/dt/dd` semantic. Test assertions text-based (`getByText`) resilient. Run early in TDD cycle. |
| R2 | Card hover lift visivamente eccessivo per Card 1+2 (static) | Low | Low | Default applicato `hover:translate-y-0 hover:shadow-sm`. Designer screenshot review verifica visual fit. |
| R3 | URL nav `?tab=houseRules` non sincronizza activeTab | Low | Medium | `GameTabsPanel` ha già `useEffect` sync su `initialTab` change (line 86-91, #2105 M7 review follow-up). Test manual cycle Info → houseRules → Info. |
| R4 | `next/navigation` `useSearchParams` returns null in test render | Low | Medium | Test setup mock necessario (current tests usano `mockState` direct). Se test estendono in futuro: mock `next/navigation` o sostituire wiring con callback. |
| R5 | Designer review delay (async) | Medium | Low | Sync gate documentato in PR body. Work non bloccato (altri task possono procedere). |
| R6 | Forward-refactor mockup obsolescence (sp3-shared-game-detail design intent) | Low | Low | Check `fidelity.json` pre-screenshot capture. Se `obsolete` → skip designer review per DEC-6 fallback. |
| R7 | DS-15 token lint mode error blocca per nuove classi inline | Low | Medium | Tutte le classi nel design usano semantic tokens (`bg-card`, `border-border/50`, `text-muted-foreground`, `bg-entity-toolkit/12`). `pnpm lint:tokens` 0 violations expected. |
| R8 | BGG ToS guard fail | None | High | Zero BGG references in design. `pnpm lint:bgg` pass. |

## 10. Sequencing (mandatory pre-flight P124)

```
1. Pre-flight check:
   git checkout main-dev && git pull --ff-only
   git branch --show-current  # MUST print main-dev
   git status  # MUST show clean tree
   gh issue list --search "M6 Info card OR M4 Toolbox EPIC 2096" --state all
   git branch -r | grep -E "issue-2096|m6-m4"
   gh pr list --search "M6 Info OR M4 Toolbox"

2. Sub-issue creation:
   gh issue create --title "M6 + M4 EPIC #2096 final closure (Info card + Toolbox coming-soon)" \
                   --body "<from this spec>" \
                   --label "enhancement,area/frontend,user-facing,P3,mockup-drift" \
                   --milestone "<existing or none>"

3. Branch:
   git checkout -b feature/issue-<NUM>-m6-m4-closure

4. Commit spec + plan (this doc + writing-plans output):
   - docs/superpowers/specs/2026-06-11-epic-2096-m6-m4-final-closure-design.md (this file)
   - docs/superpowers/plans/2026-06-11-epic-2096-m6-m4-final-closure-plan.md (via writing-plans skill next)

5. TDD-style impl:
   - M6 Card 1 (Descrizione) → test run early
   - M6 Card 2 (Informazioni dl wrap) → test run
   - M6 Card 3 (House Rules CTA + URL nav) → test run
   - M4 Toolbox Card placeholder → smoke test manual
   - Lint + typecheck

6. Quality gates:
   pnpm test GameInfoTab        # 9/9 pass
   pnpm lint
   pnpm lint:tokens             # 0 violations
   pnpm lint:bgg                # 0 violations
   pnpm typecheck

7. Screenshot capture + PR body composition.

8. gh pr create --base main-dev --title "feat(library): EPIC #2096 M6+M4 final closure" \
                --body "<from PR template + DESIGNER REVIEW PENDING flag + 2 screenshots>"

9. Designer review gate (async wait).

10. On 👍: gh pr merge <NUM> --admin --squash --delete-branch  # P145 37a volta

11. Issue + EPIC closure:
    gh issue close <sub-issue-NUM> --comment "<PR ref + AC evidence>"
    # Edit EPIC #2096 body: mark M6 ✓ + M4 ✓ rows
    gh issue edit 2096 --body-file <updated-body>
    # If 7/7 milestone shipped:
    gh issue close 2096 --comment "<all milestones closed, link to PRs>"

12. Memory entry:
    Write epic-2096-closure-shipped.md + update MEMORY.md index

13. DS-17-10 sp3 reactivation trigger (verify):
    gh issue view 2096 --json state  # → CLOSED
    # Notify user that DS-17-10 sp3 ready for brainstorming re-open
```

## 11. References

| Type | Path / Link |
|---|---|
| Parent EPIC | [#2096](https://github.com/meepleAi-app/meepleai-monorepo/issues/2096) `/library/[gameId] sp3 mockup rebuild — 6 milestone` |
| Mockup canonical | `admin-mockups/design_files/sp3-shared-game-detail.jsx` (1066+ righe) |
| M6 target | `apps/web/src/components/game-detail/tabs/GameInfoTab.tsx` |
| M4 target | `apps/web/src/components/game-detail/tabs/GameToolboxTab.tsx` |
| Integration | `apps/web/src/components/game-detail/GameDetailDesktop.tsx` |
| Tabs panel | `apps/web/src/components/game-detail/GameTabsPanel.tsx` |
| Tabs config | `apps/web/src/components/game-detail/tabs/types.ts` |
| Existing tests M6 | `apps/web/src/components/game-detail/tabs/__tests__/GameInfoTab.test.tsx` (9 test) |
| shadcn Card primitive | `apps/web/src/components/ui/data-display/card.tsx` (re-export `apps/web/src/components/ui/card.tsx`) |
| Memory: DS-17-10 deferred | `~/.claude/projects/.../memory/ds-17-10-sp3-deferred-decisions.md` |
| Memory: sess.46o shipped | `~/.claude/projects/.../memory/ds-17-phase-c-1-sp6-7-nano-shipped.md` |
| Sibling milestone M1 | PR #2101 `e4c6d100d` |
| Sibling milestone M2 | PR #2103 `af5145562` |
| Sibling milestone M7 | PR #2108 `d691b8ceb` |
| Constraint | #2010 tab IDs vietato rinominare |
| DS-17 spec parent | `docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md` |
| Token canonicalization | `docs/for-developers/specs/2026-05-12-token-canonicalization.md` |
| BGG ToS umbrella | `#1903` / `#2123` |

## 12. Acceptance criteria (sub-issue ready)

### M6 Info card style

- [ ] `GameInfoTab.tsx` usa `Card` primitive shadcn da `@/components/ui/card`
- [ ] 3 Card sequence: Descrizione (conditional su `game.description`) + Informazioni (sempre) + House Rules CTA (sempre)
- [ ] Card 1+2 `className="hover:translate-y-0 hover:shadow-sm"` (override lift)
- [ ] Card 3 default lift + CTA Button onClick = URL nav `?tab=houseRules` via `useRouter().replace`
- [ ] `dl/dt/dd grid-cols-[auto_1fr]` preservato dentro Card 2 `<CardContent>`
- [ ] `data-testid="game-info-description"` preservato su Card 1 (test resilience)
- [ ] 3 stati invariati (isNotInLibrary / isLoading / isError) con copy esistente
- [ ] Variant desktop/mobile padding rispettato
- [ ] Existing 9 unit test pass 100%

### M4 Toolbox coming-soon

- [ ] `GameToolboxTab.tsx` usa `Card` primitive shadcn
- [ ] 1 Card con: icon 44x44 `bg-entity-toolkit/12 text-entity-toolkit` + Title "Toolbox" + Description "Strumenti rapidi…" + CardContent "Integrazione completa…" + CardFooter Button "In arrivo" disabled
- [ ] `isNotInLibrary` fallback copy invariato
- [ ] `hover:translate-y-0 hover:shadow-sm` override (static placeholder, no lift)
- [ ] `disabled` Button con `cursor-not-allowed`
- [ ] Mockup parity verified via designer review

### Quality gates

- [ ] `pnpm test GameInfoTab` → 9/9 pass
- [ ] `pnpm lint` → 0 violations
- [ ] `pnpm lint:tokens` → 0 violations
- [ ] `pnpm lint:bgg` → 0 violations
- [ ] `pnpm typecheck` → 0 errors
- [ ] 2 screenshot embedded in PR body (Info tab + Toolbox tab)
- [ ] Designer 👍 thumbs-up nel PR (oppure mockup `forward-refactor-obsolete` confermato → skip)
- [ ] Admin-squash merge + branch auto-deleted
- [ ] Sub-issue closed + EPIC #2096 body row M6 ✓ + M4 ✓
- [ ] DS-17-10 sp3 reactivation trigger verified

## 13. Out of scope (explicit)

- ❌ Toolkit listing data layer (BE endpoint + FE hook + `ToolkitPublicListItem` cards rendering) — out of M4 P3 cap, deferred a future EPIC
- ❌ House Rules inline preview/listing dentro Card 3 — DEC-1 only CTA link
- ❌ Description Markdown rendering — DEC-2 only plain text
- ❌ New unit tests — DEC-5 manual + smoke only
- ❌ Snapshot tests — visual gate retirato sess.46m (#1066)
- ❌ Mobile screenshot capture — desktop only per designer review (DEC-6 implicit)
- ❌ ConnectionBar/Hero/ContributorsStrip/Tabs tweaks — M1+M2+M3+M5+M7 già shipped
- ❌ Tab IDs rename — vietato per #2010

---

**End of design spec.**

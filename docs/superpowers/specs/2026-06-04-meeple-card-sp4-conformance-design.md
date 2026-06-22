# #1856 — SP4 MeepleCardGrid mockup conformance reskin

**Issue**: [#1856](https://github.com/meepleAi-app/meepleai-monorepo/issues/1856)
**Author**: brainstorming session 2026-06-04
**Branch**: `feat/issue-1856-meeple-card-sp4-conformance` → PR target `main-dev`
**Status**: Design (pre-implementation)

---

## 1. Context & Problem

Discovered during Phase 0 investigation of `/library` SP4 mockup conformance (umbrella #1585-followup). The `MeepleCard` primitive at `apps/web/src/components/ui/data-display/meeple-card/` has **5 structural drifts** from the canonical SP4 mockup at `admin-mockups/design_files/sp4-library-desktop.jsx:657-749` (`MeepleCardGrid` variant).

Primitive consumers: **72 non-test files** across library/agents/players/games/sessions/dashboard/admin/catalog/chat/kb/game-night/collection.

### Structural drift matrix

| Element | Mockup spec | Current primitive | Source line |
|---|---|---|---|
| **AccentBorder** | Horizontal top, `top:0 left:0 right:0 h:3px` | Vertical left, `bottom:0 left:0 top:0 w:3px` | `parts/AccentBorder.tsx:11-15` |
| **Cover** | `height:100` squat band, emoji 38px centered, `entity.coverEmoji \|\| DS.EC[ent].em` | `aspect-[7/10]`, GameCoverPlaceholder initials clamp 1.5-4rem for game, entityIcon 5xl for others | `parts/Cover.tsx:51` + `GameCoverPlaceholder.tsx:64-72` |
| **EntityBadge** | Glass `rgba(255,255,255,.85) backdrop-blur(6px)`, entity color **text**, entity emoji prefix | Solid entity bg + white text, no emoji prefix | `parts/EntityBadge.tsx:22-23` |
| **3-dot menu** | Top-right glass button hover-visible | **Absent** | n/a |
| **Footer** | `border-top` + StatusDot + uppercase mono badge 9.5px | **Absent** (StatusBadge stacked top-left under EntityBadge) | `variants/GridCard.tsx:64` |

---

## 2. Decisions

Locked in brainstorming session 2026-06-04:

| ID | Decision | Rationale |
|---|---|---|
| **DEC-1** | Approach A — Surgical primitive restructure (1 PR) | Matcha esattamente i 5 numbered items dell'issue body. Alternativi (feature flag dual variant, opt-in prop) introducono tech debt o overhead PR multipli. |
| **DEC-2** | Cover emoji-band per ALL entities (game incluso) | Mockup-faithful totale. Sacrifica `GameCoverPlaceholder` rich-initials default per coerenza visiva con altre entity. `GameCoverPlaceholder` resta nel codebase per future reuse standalone. |
| **DEC-3** | Cover dual-mode: `imageUrl` presente → `aspect-[7/10]`; `imageUrl` assente → squat `h-[100px]` + emoji 38px | Preserva foto cover board-game canonical ratio quando disponibili (future #1831 L4 PDF cover, #1824 L3 user upload). Mockup squat-mode si applica solo a placeholder. |
| **DEC-4** | 3-dot menu placeholder visuale (no handler) | Issue body item 2 dice "placeholder". `onClick={e => e.stopPropagation()}` no-op. Future functional wiring tracked separatamente. |
| **DEC-5** | Footer StatusDot+badge sostituisce StatusBadge top-left stack | Mockup-aligned. EntityBadge resta da solo nel top-left stack (glass style). Riduce visual clutter top-left. |
| **DEC-6** | DOM structure assertions only — NO visual regression infra | Memoria CLAUDE.md: "Visual Gate REMOVED 2026-05-20" (false-positive rate alto). Vitest + RTL coprono DOM structure; designer review post-preview-deploy come gate manuale. |

---

## 3. Architecture

Modifichiamo il **primitive MeepleCard** (`apps/web/src/components/ui/data-display/meeple-card/`) seguendo principio di **modifica composizionale**: ogni gap del mockup mappa su 1 file `parts/*.tsx` o `variants/GridCard.tsx`.

- `Cover` introduce branch dual-mode (image-mode vs emoji-band-mode)
- `AccentBorder` cambia orientation (vertical-left → horizontal-top)
- `EntityBadge` cambia stile da solid→glass
- `GridCard` orchestrator: aggiunge `MenuPlaceholder` (nuovo part) + `CardFooter` (nuovo part); rimuove `StatusBadge` dal top-left stack
- Prop `coverEmoji?: string` aggiunto additivamente a `MeepleCardProps`

Le decisioni di rendering (image vs emoji) sono **pure functions of props** — nessun side-effect, nessun async, SSR-safe.

**Backward compat**: tutti i 72 consumer continuano a montare senza errore. Il rendering visivo cambia intenzionalmente (gap risolti = goal).

---

## 4. Components & file map

| File | Change type | What |
|---|---|---|
| `parts/AccentBorder.tsx` | **edit** | Geometry vertical-left → horizontal-top. From `bottom-0 left-0 top-0 w-[3px]` to `top-0 left-0 right-0 h-[3px]`. Hover `group-hover:w-[5px]` → `group-hover:h-[5px]`. |
| `parts/Cover.tsx` | **edit** | Dual-mode: if `imageUrl` present → render `<img>` `aspect-[7/10] object-cover` (current behavior); else → render emoji-band `h-[100px]` with `coverEmoji ?? entityIcon[entity]` 38px centered + `drop-shadow(0 2px 6px rgba(0,0,0,.3))`. Per-entity gradient background preserved. **`GameCoverPlaceholder` no longer invoked** (replaced by emoji-band per DEC-2). |
| `parts/EntityBadge.tsx` | **edit** | Glass style: `bg-white/85 backdrop-blur-md` + text color `entityHslText(entity)` (entity color text, AA-safe via existing helper) + entity emoji prefix `{entityIcon[entity]} {entityLabel[entity]}`. Font sizes: `text-[9px] font-extrabold uppercase tracking-wide` preserved. ESLint disable comment updated to reflect glass pattern instead of solid bg. |
| `parts/MenuPlaceholder.tsx` | **new** | Hover-visible button `absolute right-2 top-2`, glass `bg-white/85 backdrop-blur-md`, `⋯` 14px, `aria-label="Azioni"`, `onClick={e => e.stopPropagation()}` (no functional handler). Initially `opacity-0 group-hover:opacity-100 transition-opacity`. |
| `parts/CardFooter.tsx` | **new** | `border-t border-[var(--mc-border-light)] flex items-center gap-1.5 px-3.5 py-1.5`. Renders StatusDot (reuse existing pattern) + uppercase mono badge `font-mono text-[9.5px] font-bold uppercase tracking-wide text-[var(--mc-text-secondary)]`. Renderless when both `status` and `badge` are undefined. |
| `variants/GridCard.tsx` | **edit** | (a) Remove `StatusBadge` from top-left stack (current line 64: `{status && <StatusBadge status={status} stacked />}`); (b) add `<MenuPlaceholder />` after Cover; (c) add `<CardFooter status={status} badge={badge} />` as last child; (d) remove inline `badge` rendering in header (lines 83-90) since now moved to footer. |
| `types.ts` | **edit** | Add `coverEmoji?: string` field to `MeepleCardProps` (inserted after `imageUrl?: string` for grouping with cover-related props) with JSDoc: "UTF-8 emoji shown in the squat-band cover mode (when `imageUrl` is absent). Falls back to `entityIcon[entity]` when omitted. Example: 🎲 for game, 🎯 for session. Naming endorses existing FE convention: `Toolkit.coverEmoji` and play-records `coverEmoji` (`StatsHero.tsx:137`)." |
| `parts/index.ts` | **edit** | Export `MenuPlaceholder`, `CardFooter`. |

**Other variants** (`CompactCard`, `FeaturedCard`, `FocusCard`, `HeroCard`, `ListCard`): **out of scope** for this PR. Issue #1856 focuses on `MeepleCardGrid` specifically. If parts they reuse (AccentBorder, EntityBadge) change, they inherit the visual changes — verification via smoke render-test only.

---

## 5. Data flow

```
Consumer → <MeepleCard {...props} coverEmoji="🎲" />
                ↓
         MeepleCardImpl (variant default 'grid')
                ↓
            GridCard
            ├── AccentBorder entity={entity}           [horizontal-top h-[3px], group-hover:h-[5px]]
            ├── Cover entity imageUrl alt gameId coverEmoji variant="grid"
            │      ├── if (imageUrl && !onError) → <img aspect-[7/10] object-cover>
            │      └── else → <div h-[100px] emoji-band gradient>
            │                    └── <span text-[38px]>{coverEmoji ?? entityIcon[entity]}</span>
            ├── BadgeStack absolute top-left
            │      └── EntityBadge stacked   [glass white/85 + entity color text + emoji prefix]
            ├── MenuPlaceholder              [opacity-0 group-hover:opacity-100, glass, ⋯, no-op]
            ├── CardBody (h3 title + subtitle, line-clamp 2 ciascuno)
            └── CardFooter status badge      [border-top, StatusDot + uppercase mono badge]
```

### `coverEmoji` wiring sources

| Surface category | Source | Fallback |
|---|---|---|
| Library + games | `SharedGameDto.coverEmoji` field does **not** exist in current BE schema (verified 2026-06-04 — grep `coverEmoji` in `apps/api/` returns 0 matches). In this PR, library/games consumers pass `coverEmoji={undefined}` → automatic fallback to `entityIcon['game']='🎲'`. BE field addition is **out of scope** (deferred to future feature). | `entityIcon['game']='🎲'` |
| Toolkits | Existing convention: `Toolkit.coverEmoji?: string` ([`HubToolkitCardGrid.tsx:31`](../../../apps/web/src/components/features/toolkits-index/HubToolkitCardGrid.tsx)) currently rendered inline; in this PR can be passed through `<MeepleCard coverEmoji={toolkit.coverEmoji}>` if consumer migrates. Migration is **opt-in** per surface, not blocking. | `entityIcon['toolkit']='🧰'` |
| Play-records | Existing convention: `(favoriteGame as { coverEmoji?: string }).coverEmoji` ([`StatsHero.tsx:137`](../../../apps/web/src/components/play-records/stats/StatsHero.tsx)) — same pattern reuse. | `entityIcon[entity]` |
| Sessions / players / agents / kb / chat / event / toolkit / tool | No explicit pass | `entityIcon[entity]` (`🎯`, `👤`, `🤖`, `📚`, `💬`, `📅`, `🧰`, `🔧`) |

---

## 6. Error handling

| Scenario | Behavior |
|---|---|
| `imageUrl` fails to load (`onError` fires) | State flip → render emoji-band fallback (preserves current pattern, but target changes from `GameCoverPlaceholder` to emoji-band per DEC-2). |
| `imageUrl` is BGG-hosted (blocked by `shouldUsePlaceholder`) | Same as above: emoji-band rendered. |
| Missing/invalid `entity` value | Compile-time blocked: TypeScript `MeepleEntityType` literal union (9 valori validi). `entityIcon` lookup safe. |
| `coverEmoji` malformed (empty string, multi-char) | Rendered as-is — è user-data text, no XSS risk. Empty string falls through to entityIcon via nullish coalescing. |
| Footer renderless | If `status` AND `badge` entrambi `undefined`, `<CardFooter>` returns `null` (no empty border-top). |

---

## 7. Testing strategy

### TDD test files

| Test file | Coverage |
|---|---|
| `parts/__tests__/AccentBorder.test.tsx` | (a) Renders with classes `top-0 left-0 right-0 h-[3px]` (not `bottom-0 left-0 top-0 w-[3px]`); (b) bg inline style uses `entityHsl(entity)`; (c) hover class `group-hover:h-[5px]` present. |
| `parts/__tests__/Cover.test.tsx` | (a) `imageUrl` present → renders `<img>` with `aspect-[7/10]` and `object-cover`; (b) `imageUrl` absent → renders emoji-band container with `h-[100px]`; (c) emoji-band absent imageUrl → shows `coverEmoji` if provided; (d) emoji-band absent imageUrl + no `coverEmoji` → fallback to `entityIcon[entity]`; (e) emoji-band absent imageUrl + `entity='game'` → emoji-band (NOT GameCoverPlaceholder, per DEC-2); (f) `onError` on `<img>` → state flips, switches to emoji-band. |
| `parts/__tests__/EntityBadge.test.tsx` | (a) Glass classes present: `bg-white/85`, `backdrop-blur-md`; (b) text color uses `entityHslText` (verify computed style or class); (c) emoji prefix rendered before label text (e.g. `🎲 Game` for entity='game'). |
| `parts/__tests__/MenuPlaceholder.test.tsx` | (a) Renders button with `aria-label="Azioni"`; (b) `⋯` glyph present; (c) initial `opacity-0`, hover `opacity-100` classes; (d) `onClick` calls `e.stopPropagation()` (mock event); (e) no functional handler invoked (no consumer callback). |
| `parts/__tests__/CardFooter.test.tsx` | (a) `status` provided → renders StatusDot + uppercase mono badge with status text; (b) `badge` provided → renders badge text in uppercase mono; (c) both absent → returns `null` (no DOM output); (d) border-top class `border-t` present when rendered. |
| `variants/__tests__/GridCard.test.tsx` | Integration: renders `AccentBorder` + `Cover` + `EntityBadge` + `MenuPlaceholder` + `CardFooter` (when status/badge); `StatusBadge` NOT in top-left badge stack; `badge` NOT rendered inline in header (moved to footer). |
| `__tests__/MeepleCard.contract.test.tsx` | (a) `coverEmoji` prop accepted as `string` type (TypeScript check); (b) backwards compat: rendering omitting `coverEmoji` does not throw; (c) all 9 entity types render correctly. |

### Smoke test for consumer categories (post-implementation, NON-gate)

`__tests__/consumer-categories.smoke.test.tsx`: 1 render-smoke test per surface category (library/games/sessions/players/agents/dashboard/admin/catalog/chat/kb/game-night/collection). Verifica solo `expect(screen.getByTestId('meeple-card')).toBeInTheDocument()`. NO snapshot, NO visual diff.

### A11y deferred to #1842

NO `jest-axe` in this PR. EntityBadge text color cambia + MenuPlaceholder è nuovo trigger → impatti axe in scope **#1842** (a11y MeepleCard headingLevel follow-up). Documenta esplicitamente in PR description.

### Designer review gate

PR description include:
- Preview deploy URL (Vercel/staging) post-CI
- Checklist visiva: `[ ] Designer verified library card | [ ] games card | [ ] sessions card | [ ] players card | [ ] agents card`
- Designer review è **gate manuale** (no CI block).

---

## 8. Out of scope (esplicito)

- ❌ `coverEmoji` server-side wiring (DB column + migration). Solo client-side prop wiring per ora; BE metadata fa parte di future feature.
- ❌ `GameCoverPlaceholder` rimozione (resta nel codebase per altre potential reuse + uso esistente in altri primitive). Solo `MeepleCard.Cover` smette di chiamarlo.
- ❌ A11y `heading-order` axe rule re-enable (tracked in **#1842**).
- ❌ Visual regression test reintroduction (Visual Gate REMOVED 2026-05-20 sta).
- ❌ Migrazione consumer specifici per `coverEmoji` wiring oltre library/games — sessions/players/etc restano con fallback automatico `entityIcon[entity]`.
- ❌ Variant non-grid (`compact`, `featured`, `focus`, `hero`, `list`). Ereditano cambi composizionali ai `parts` ma non vengono ristrutturati a livello variant. Future cross-cutting issue se necessario.
- ❌ `MenuPlaceholder` functional handler (consumer-defined menu actions). Solo placeholder visuale. Future issue se richiesto.

---

## 9. Rollout & risk

### Risk matrix

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Visual regression on 72 consumers (intentional) | High | Low-Medium | Smoke render-test catches structural breakage; designer review post-preview gate; CI passes if DOM tests pass. |
| Cover emoji-band looks worse than GameCoverPlaceholder initials for game entity | Medium | Medium | DEC-2 lock; user accepted trade-off in brainstorming. Reversible: re-add `useInitials?: boolean` prop in follow-up if feedback negative. |
| `EntityBadge` glass + entity color text contrast fail (AA) | Low | High | `entityHslText` helper already exists and is AA-safe per `tokens.ts:77-89`. Verify in tests via computed style assertion. |
| `MenuPlaceholder` discoverability (hover-only on touch devices) | Low | Low | Issue body explicitly says "placeholder"; functional wiring deferred. Touch device users see no menu, no degradation vs current state (current = no menu at all). |
| Other variants (list/compact/etc) inherit changes silently | Low | Low | Variants reuse `parts/AccentBorder` and `parts/EntityBadge`. Inheritance is intentional (visual consistency). Smoke test covers render. |

### Rollback plan

- Revert PR → primitive restored to current state, 72 consumers continue rendering as before
- Branch protected (auto-delete on merge per repo config)
- Database: zero changes, no migration to revert

---

## 10. References

- Issue: [#1856](https://github.com/meepleAi-app/meepleai-monorepo/issues/1856)
- Parent epic: #1585 (library SP4 hybrid-hub, CLOSED 2026-06-02)
- Related deferred: #1842 (a11y headingLevel follow-up)
- Mockup canonical: `admin-mockups/design_files/sp4-library-desktop.jsx:657-749`
- Token system: `apps/web/src/components/ui/data-display/meeple-card/tokens.ts`
- Brainstorming session: 2026-06-04 (Italiano, panel sintesi multi-esperto Wiegers/Cockburn/Adzic/Nygard/Fowler/Crispin → consensus opzione A)
- Memory linked: P164 axe-rule-suppression-with-tracked-followup (sessione 29 #1841)

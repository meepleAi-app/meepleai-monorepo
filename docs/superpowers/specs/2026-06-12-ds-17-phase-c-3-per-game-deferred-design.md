# DS-17 Phase C-3 per-game implementation — META spec (DEFERRED)

**Status**: planned-not-yet-shipped — brainstorm META completed 2026-06-12 sess.46p, implementation deferred future sessions
**Owner**: badsworm@gmail.com
**Umbrella tracking**: [#2234](https://github.com/meepleAi-app/meepleai-monorepo/issues/2234)
**Parent umbrella**: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063) DS-17 Mockup-to-App Fidelity
**Phase C-2 closure**: 4/4 COMPLETE sess.46p (PRs #2218 + #2225 + #2230 + #2232)

## 1. Context

Phase C-2 sp4-sessions skeleton-first (DS-17-15 PR #2232) shipped 3 base + 7 per-game stub stories. **Phase C-3** = full per-game implementation (MSW handlers + flavor components + integration).

Brainstorm sess.46p revealed Wingspan canonical pattern is **2052 LOC** (7 JSX files: live + parts + tabs + summary variants), significantly larger than initial ~3-5h estimate. Real Phase C-3 cumulative effort: **~104-125h** across 7 sub-issue.

Sessione 46p has already shipped 7 PRs (~40-45h cumulative). Adding ~104-125h Phase C-3 work exceeds reasonable single-session scope.

**Decision** (DEC-2 sess.46p): Document scope + tracking, defer execution to future sessions when business priority signal emerges.

## 2. DEC

| # | Decisione | Source |
|---|---|---|
| DEC-1 | Phase C-3 retains full 7-game scope (no scope reduction) | sess.46p |
| DEC-2 | Spec doc + umbrella tracking sub-issue OPEN; implementation deferred future sessions | sess.46p |
| DEC-3 | Wingspan first (canonical ADR pattern → reusable primitives) | sess.46p sequencing |
| DEC-inherited | sp4-session-* stubs (DS-17-15 PR #2232) serve as Storybook placeholders until reactivation | DS-17-15 |

## 3. Per-game complexity assessment

| # | Game | Mockup LOC | Flavor components | Est. Effort | Sequencing |
|---|---|---|---|---|---|
| 1 | Wingspan | 2052 (7 files) | Canonical ADR pattern (live/parts/tabs + summary/parts/sections/tabs) | ~25-30h | **HIGHEST priority — canonical first** |
| 2 | Codenames | 168 | WordGrid + WordCard (5 states) + SpymasterKeyCardOverlay + TeamPanel + CurrentCluePanel + ClueHistoryTimeline + RoleAvatar | ~10-12h | Smallest non-trivial (fast iteration) |
| 3 | Catan | 345 | HexBoard + RobberOverlay + DiceDisplay + TradePanel + DevCardsPanel + ResourceHandBar | ~15-18h | Medium |
| 4 | Paleo | 232 | (custom per mockup analysis) | ~12-14h | Medium |
| 5 | Power Grid | 275 | (custom per mockup analysis) | ~12-15h | Medium |
| 6 | Puerto Rico | 255 | (custom per mockup analysis) | ~12-14h | Medium |
| 7 | Zombicide | 452 | (custom per mockup analysis, largest non-Wingspan) | ~18-22h | Final (highest complexity) |

**Total cumulative**: ~104-125h (multi-session committed).

## 4. Sub-issue split (7 sequential future)

| Sub-issue | Game | Effort | Note |
|---|---|---|---|
| DS-17-16 | Wingspan | ~25-30h | Canonical first establishes primitives reusable for other 6 |
| DS-17-17 | Codenames | ~10-12h | Smallest, validates stub→full pattern |
| DS-17-18 | Catan | ~15-18h | Medium |
| DS-17-19 | Paleo | ~12-14h | Medium |
| DS-17-20 | Power Grid | ~12-15h | Medium |
| DS-17-21 | Puerto Rico | ~12-14h | Medium |
| DS-17-22 | Zombicide | ~18-22h | Final |

## 5. Recommended sequencing

1. **Wingspan first** (~25-30h) — canonical pattern + reusable abstraction primitives extracted (Storybook component-mocks + MSW factories + flavor types)
2. **Codenames** (~10-12h) — validates stub→full pattern transformation with smallest complexity
3. **Medium batch** (Catan + Paleo + Puerto Rico, ~40-46h) — applies established primitives
4. **High-complexity final** (Power Grid + Zombicide, ~30-37h)

Sequential per skill rule (no parallel implementation sub-agents). Brainstorming can be parallel future sessions.

## 6. Reactivation trigger

**Business priority signal**: when per-game session implementation becomes critical for product roadmap (likely tied to live session feature launch / beta testing / customer demos).

Until trigger: sp4-session-* stubs from DS-17-15 (PR #2232 `c4576d5e9`) serve as Storybook placeholders. Storybook navigation displays:
- Authenticated / sp4-session-skeleton-live (full)
- Authenticated / sp4-session-summary-skeleton (full)
- Authenticated / sp4-session-play (full)
- Authenticated / sp4-session-{catan,codenames,paleo,power-grid,puerto-rico,wingspan,zombicide} (stub, 2 Stories each)

## 7. Out of scope (this session)

- ❌ All implementation work (deferred)
- ❌ Plan TDD detail per sub-issue (future brainstorm each)
- ❌ MSW handlers
- ❌ Flavor components implementation
- ❌ Per-game brainstorm (future, 1 per sub-issue)

## 8. References

- Umbrella tracking: #2234
- Phase C-2 closure: PR #2218 + #2225 + #2230 + #2232 (4/4 COMPLETE sess.46p)
- Memory: ds-17-15-sp4-sessions-shipped.md (P261 + P262 patterns)
- Sessions consolidation ADR: claudedocs/2026-05-31-sessions-consolidation-adr.md (per memory comments — Wingspan canonical pattern source)
- Mockups: admin-mockups/design_files/sp4-session-{wingspan,codenames,catan,paleo,power-grid,puerto-rico,zombicide}-{live,summary}-*.jsx

## 9. Status timeline

- 2026-06-12 sess.46p: brainstorm META + umbrella tracking #2234 + spec doc commit
- (Future session): Wingspan DS-17-16 brainstorm + spec + plan + execution
- (Future sessions): Codenames + Catan + Paleo + Power Grid + Puerto Rico + Zombicide

## 10. Acceptance criteria (umbrella-level)

- [ ] DS-17-16 Wingspan shipped (canonical pattern established)
- [ ] DS-17-17 Codenames shipped
- [ ] DS-17-18 Catan shipped
- [ ] DS-17-19 Paleo shipped
- [ ] DS-17-20 Power Grid shipped
- [ ] DS-17-21 Puerto Rico shipped
- [ ] DS-17-22 Zombicide shipped
- [ ] Phase C-3 7/7 COMPLETE → DS-17 umbrella Phase D ready

---

**End of META spec. Implementation deferred future sessions.**

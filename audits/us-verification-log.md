# US Manual Verification Log — DS-17 Phase B

**Started**: 2026-06-11
**Method**: visual verification (mockup ↔ app side-by-side)
**Order**: most common user-side flows first (high traffic → low traffic)
**Sub-issue**: #2127 | **PR**: #2128 | **Umbrella**: #2063

## Verdict taxonomy

| Symbol | Meaning | Action |
|---|---|---|
| ✅ PASS | Mockup + app match, US functional | Move to next US |
| ⚠️ VISUAL_DRIFT | Functional but UI differs from mockup | Note + decide accept/fix |
| 🔧 FUNCTIONAL_BUG | UI matches but flow breaks | File bug |
| 🚫 NOT_IMPLEMENTED | US sequence not reachable | Confirm scope |
| 📐 MOCKUP_OBSOLETE | Mockup outdated; app correct | Reclassify mockup `forward-refactor-obsolete` |

## Verification queue (priority: most common user-side first)

| # | US | Persona | Title | Mockup | Status |
|---|---|---|---|---|---|
| 1 | US-2 | Marco | Log in + resume session | `auth-flow.html` | ⏳ pending |
| 2 | US-6 | Marco | Dashboard priority-driven | `sp4-dashboard.html` (📐 obsolete — verifico vs Asse C live) | ⏳ pending |
| 3 | US-25 | Sara | Notifications inbox | `notifications.html` | ⏳ pending |
| 4 | US-10 | Sara | Library hybrid hub | `sp4-library-desktop.html` | ⏳ pending |
| 5 | US-8 | Marco | Games hub multi-tab (Discover default) | `sp4-discover.html` | ⏳ pending |
| 6 | US-9 | Giulia | Game detail tabs | `sp4-game-detail.html` (+ 5 tab mockups missing) | ⏳ pending |
| 7 | US-27 | Sara | AI agent chat | `chat-fullscreen.html` + `sp4-agents-index.html` | ⏳ pending |
| 8 | US-26 | Giulia | Profile + achievements | `settings.html` / `sp5-profile-settings.html` | ⏳ pending |
| 9 | US-13 | Marco | GameNight create wizard | `sp7-game-night-create.html` | ⏳ pending |
| 10 | US-15 | Marco | GameNight detail | `sp7-game-night-detail-rsvp.html` | ⏳ pending |

(More US below queue, added on demand.)

## Verification log entries

(Each US gets a `### US-N — verdict — date` heading appended below.)

---

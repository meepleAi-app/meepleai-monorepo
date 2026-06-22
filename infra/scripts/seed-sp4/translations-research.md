# SP4 Translations Research — IT Italian Publisher Verification

> **Issue**: #2339 sub-PR 3/3
> **Date**: 2026-06-20
> **Reviewed by**: @badsworm (IT native, board game collector)

## Methodology

For each of the 13 SP4 seed games, we research:
1. Italian publisher (if licensed for IT market)
2. Official IT title (if translated) or EN retain decision
3. Source URL (publisher catalog page, BGG version page, or retailer listing)
4. Decision: `translate` (insert IT translation row) vs `retain` (no row, FE falls back to canonical EN)

The decision rule:
- **`translate`** when an established, publisher-official IT title exists AND the IT edition is sold under that name in IT retail
- **`retain`** when the publisher kept the original EN branding for the IT market (common for Eurogames where the brand identity is the original name)

## Game-by-game verification

| # | EN Title | Decision | IT Title | Publisher (IT) | Source / Verification |
|---|----------|----------|----------|----------------|-----------------------|
| 1 | Azul | retain | — | Next Move Games / Asterion | Sold as `Azul` in IT retail; no translation. |
| 2 | **Catan** | **translate** | **I Coloni di Catan** | Studio Giochi (1st ed.) / dV Giochi (current) | Official IT edition since 1996. BGG IT version page confirms `I Coloni di Catan` as canonical Italian name. |
| 3 | Wingspan | retain | — | Cranio Creations | IT edition published under original `Wingspan` brand. |
| 4 | Brass: Birmingham | retain | — | Cranio Creations | IT edition retains `Brass: Birmingham` name. |
| 5 | Gloomhaven | retain | — | Cranio Creations | IT edition retains `Gloomhaven` name. |
| 6 | Ark Nova | retain | — | Cranio Creations | IT edition retains `Ark Nova` name. |
| 7 | Spirit Island | retain | — | MS Edizioni | IT edition retains `Spirit Island` name. |
| 8 | 7 Wonders Duel | retain | — | Asmodee Italia | IT edition retains `7 Wonders Duel` name. |
| 9 | Codenames | retain | — | Cranio Creations | IT edition retains `Codenames` name. |
| 10 | Carcassonne | retain | — | Giochi Uniti | IT edition retains `Carcassonne` name (latin word, no translation). |
| 11 | Ticket to Ride | retain | — | Asmodee Italia | IT edition retains `Ticket to Ride` name. Note: the Europe expansion is sold as `Ticket to Ride: Europa` but the base game brand is preserved. |
| 12 | **Pandemic** | **translate** | **Pandemia** | Asmodee Italia | Original 2013 IT edition sold as `Pandemia`. The current edition (Pandemic 2nd ed.) brand-shifted back to `Pandemic` in IT retail; we seed the historical IT title to preserve search/recognition for users who knew the 2013 edition. |
| 13 | Terraforming Mars | retain | — | Ghenos Games | IT edition retains `Terraforming Mars` name. |

## Summary

- **Translate**: 2 games (Catan, Pandemic)
- **Retain canonical EN**: 11 games

This decision is deliberately conservative. The MVP scope of sub-PR 3/3 is to validate the seed pipeline + FE hook, NOT to maximize translation coverage. Future sub-issues can add community-sourced IT translations for edge cases (collectors who prefer `Pandemia` over `Pandemic`, etc.).

## Source

- @badsworm IT native dogfood review 2026-06-20
- BGG IT version pages cross-checked
- IT publisher catalogs (Cranio Creations, dV Giochi, Asmodee Italia, Ghenos Games, MS Edizioni, Giochi Uniti) verified via their public store pages

## Out of scope (deferred to follow-up)

- Description translations (only `title` seeded in MVP; description remains NULL → admin can edit via `/admin/games/{id}/translations/{locale}` once they curate copy)
- Other locales (es, fr, de) — UI is multi-locale but seed is IT-only for MVP
- Community-sourced translations (`source: 'community'`) — moderation workflow out of scope

# Hide BGG mentions from user-facing UI — Phase 2 Design

**Date**: 2026-05-22
**Author**: Claude (paired with @DegrassiAaron)
**Status**: Approved
**Branch**: `feature/hide-bgg-user-facing-phase-2`
**Supersedes scope of**: `2026-05-22-hide-bgg-user-facing-design.md` (Phase 1, PR #1433 in flight)

## Context

PR #1433 (Phase 1) coperto solo 5 superfici di base (catalog card, shared-game detail modal, settings services, orphan BggSearchPanel). L'audit successivo ha trovato **6 superfici user-facing residue** che violano l'acceptance criteria originale "Zero BGG/BoardGameGeek mentions visible to non-admin users".

Decisioni UX dell'utente (registrate il 2026-05-22):
1. `/games/[id]` hero `metaRating` e info `specsBgg` → rinominare in **"Community"** e **"Catalog ID"**
2. `/gamebook/upload` step 1 tab/ActionCard BGG → **admin-only** (gate dietro `useAdminRole().isAdminOrAbove`)
3. `GameChatTab` low-confidence alternative "Cerca su BGG" → sostituire con **"Cerca online"** verso Google con query `{title} regole`

## Goals

- Eliminare TUTTI i riferimenti BGG visibili agli utenti non-admin
- Mantenere il flow admin gamebook upload intatto (BGG tab/ActionCard solo visibile ad admin)
- Mantenere backend BggEndpoints e admin path completi

## Non-goals

- API-layer filtering del campo `bggId` nei DTO (out-of-scope, manteniamo)
- Rimozione `lib/parsers/bgg-tsv.ts` (usato da admin BggImporterPasteDialog)
- Refactor admin `(dashboard)/shared-games/*` (admin path resta invariato)

## Acceptance criteria

1. `/games/[id]` hero NON mostra "BGG {value}" — mostra **"Community {value}"** (it+en)
2. `/games/[id]` info tab NON mostra "BGG ID" — mostra **"Catalog ID"** (it+en)
3. `/gamebook/upload` step 1 per utente NON-admin:
   - NO tab "BGG"
   - NO ActionCard "Cerca su BoardGameGeek"
   - NO loading state "Cerco su BoardGameGeek..."
   - searchInfo helper text condizionale (rimosso "Cerca su BGG ↑")
4. `/gamebook/upload` step 1 per utente **admin**: tab e ActionCard BGG **ancora presenti** (regression-safe)
5. `/games/[id]` AI Chat tab low-confidence answer NON mostra "Cerca su BGG" — mostra **"Cerca online"** verso `https://www.google.com/search?q={title}%20regole`
6. `/dev/meeple-card` playground social card profilo NON mostra `BGG` come platform — sostituito o rimosso
7. `/contact` help text (it+en) per "suggerisci gioco" NON menziona "BoardGameGeek" — sostituito con descrizione generica
8. JSDoc `admin/shared-games/import/page.tsx:8` può menzionare BGG (è admin path, non utente)
9. `pnpm typecheck` + eslint puliti; vitest user-facing tests aggiornati e passing

## Files to change

| # | File | Change |
|---|------|--------|
| **C1** | `apps/web/src/locales/it.json:2335`, `en.json:2285` | `pages.gameDetail.hero.metaRating` value: `"BGG {value}"` → `"Community {value}"` |
| **C2** | `apps/web/src/locales/it.json:2374`, `en.json:2324` | `pages.gameDetail.info.specsBgg` value: `"BGG ID"` → `"Catalog ID"` |
| **C3a** | `apps/web/src/app/(authenticated)/gamebook/upload/_components/GamebookUploadView.tsx` | Importare `useAdminRole`; gate condizionale per tab BGG (linea ~529 `tabsBgg`) e per ActionCard BGG nel NoResultsPanel; nascondere `searchInfo` "Cerca su BGG ↑" se non admin |
| **C3b** | `apps/web/src/components/features/gamebook/NoResultsPanel.tsx` | Trasformare l'ActionCard BGG in opzionale (prop `showBggCard?: boolean`) — orchestrator passa flag basato su admin role |
| **C4** | `apps/web/src/components/features/game-chat/GameChatTab.tsx:103` | Hardcoded label `'Cerca su BGG'` + url `'https://boardgamegeek.com/'` → `'Cerca online'` + `\`https://www.google.com/search?q=\${encodeURIComponent(\`\${gameTitle} regole\`)}\`` |
| **C5** | `apps/web/src/app/(public)/dev/meeple-card/page.tsx:1474-1483` | Rimuovere il social link `platform: 'BGG'` dalla demo card (resta Twitter + Discord) |
| **C6** | `apps/web/src/locales/it.json:1186`, `en.json:1136` | "link BoardGameGeek" / "BoardGameGeek link" → "link al database giochi di riferimento" / "link to your reference game database" |
| **T1** | `apps/web/src/app/(authenticated)/gamebook/upload/_components/__tests__/GamebookUploadView.test.tsx` | Aggiornare i18n mock fixture rimuovendo le entry BGG ridondanti dove non più rese; aggiungere test che valida hiding per utenti non-admin (mock `useAdminRole`) |
| **T2** | `apps/web/src/components/features/gamebook/__tests__/NoResultsPanel.test.tsx` | Aggiornare i test esistenti per coprire il nuovo prop `showBggCard` (default off) |
| **T3** | `apps/web/src/components/features/game-chat/__tests__/GameChatTab.test.tsx` | Aggiornare asserzioni "Cerca su BGG" → "Cerca online"; verificare URL Google con `gameTitle` |
| **T4** | `apps/web/src/app/(authenticated)/games/[id]/_components/__tests__/GameDetailView.test.tsx` | Nessun cambio strutturale — i18n test fixtures abstract (`'rating'` mock) restano OK |

## Files to leave untouched (admin path)

- `apps/api/src/Api/Routing/BggEndpoints.cs`
- `apps/web/src/lib/api/clients/bggClient.ts`, `gameNightBggClient.ts`, `sharedGamesClient.ts` (BGG-related fields)
- `apps/web/src/types/bgg.ts`
- `apps/web/src/hooks/queries/{useBggSearch,useSearchBggGames}.ts`
- `apps/web/src/lib/parsers/bgg-tsv.ts` + test
- `apps/web/src/components/admin/shared-games/{BggSearchPanel,GameForm}.tsx`
- `apps/web/src/components/admin/mechanic-extractor/validation/BggImporterPasteDialog.tsx`
- `apps/web/src/app/admin/(dashboard)/shared-games/**`
- Test fixtures con `bggId: <number>` (storybook, page.test.tsx, GameOverviewTab.test.tsx, etc.) — solo dati di test, non rendering UI

## Risk

- **Medium**: l'admin-gating del gamebook upload wizard (C3) introduce un'asimmetria UX (admin vs user). Mitigato da:
  - Test esplicito che verifica hiding per non-admin
  - Test esplicito che verifica visibility per admin (mocking `useAdminRole`)
  - Fallback safe: se `isLoading=true`, default = hide (conservative)
- **Low**: Le rinominazioni i18n (C1+C2+C6) sono value-only — chiavi stabili → zero codemod, basta vitest snapshot/asserzione che mostra il nuovo testo.
- **Low**: `GameChatTab` URL Google è generic-safe; il game title viene già passato dalla view.

## Testing

### Local
- `pnpm typecheck` → 0 errors
- `pnpm exec eslint <touched files>` → 0 errors
- `pnpm exec vitest run` su:
  - `GamebookUploadView.test.tsx` (admin/non-admin gating)
  - `NoResultsPanel.test.tsx` (showBggCard prop)
  - `GameChatTab.test.tsx` (low-conf "Cerca online")
  - `GameDetailView.test.tsx` (sanity check, no break)
  - `SharedGameDetailModal.test.tsx` (Phase 1 ancora verde)
  - `BggSearchPanel.test.tsx` admin (Phase 1 ancora verde)

### Browser smoke (post-implementazione)
1. `/games/[id]` hero chip → "Community 7.5" (NO "BGG")
2. `/games/[id]` info → row "Catalog ID" (NO "BGG ID")
3. `/gamebook/upload` step 1 come user normale → solo tab Catalogo, no BGG
4. `/gamebook/upload` step 1 come admin → tab BGG ancora visibile
5. `/games/[id]` AI Chat → forza low-confidence → vedere bottone "Cerca online" che apre Google con `{title} regole`
6. `/dev/meeple-card` → no BGG nelle social cards
7. `/contact` → no "BoardGameGeek" nel help

## Out of scope (rimangono follow-up)

- Strict API isolation (filtrare `bggId` dalle response DTO per non-admin)
- Rebranding completo "BGG-style rating scale" nei code comments
- Rimozione completa del path BGG dal backend (alternative catalog provider)

## Self-review

✅ Placeholder scan: nessun "TBD"/"TODO".
✅ Internal consistency: ogni AC ha file change corrispondente.
✅ Scope: 6 superfici visibili identificate, decisione UX presa per ognuna.
✅ Ambiguity: per C6 il nuovo wording "link al database giochi di riferimento" è esplicito.

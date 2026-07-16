# Issue #3010 — Follow-up cleanup #3006/#3007 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`).

**Goal:** Chiudere i Minor follow-up di #3006/#3007: CSV hardening, footer no-op, dialog prefill/Escape, ARIA nits, dedup helper, 2 feature i18n (openGameAria + dialog error-state), e sostituzione delle classi Tailwind dead `success`/`warning` in 25 file.

**Tech Stack:** Next.js 16, React 19, TypeScript, Tailwind 4, react-intl v7, Vitest.

## Global Constraints
- **Token semantici / no dead-class**: le classi `bg/text/border-success` e `bg/text-warning` (+ `-foreground`/`-ink`) sono NO-OP (i token non sono in `@theme inline`). Sostituire con arbitrary value della famiglia canonica `--c-*` (in `design-tokens-canonical.css`): `--c-success` (142 70% 45%), `--c-warning` (38 92% 50%), `--c-warning-ink` (38 92% 32%). Es. `bg-success/10` → `bg-[hsl(var(--c-success)/0.1)]`; `text-warning` → `text-[hsl(var(--c-warning))]`; `text-warning-ink` → `text-[hsl(var(--c-warning-ink))]`; `text-success-foreground`/`text-warning-foreground` (testo bianco su fondo colorato) → `text-white`. ESLint `local/no-hardcoded-color-utility` permette gli arbitrary `bg-[…]` (vieta solo neutri white/black/slate/gray/zinc/neutral/stone). NON introdurre nuovi token in `@theme inline`.
- **i18n**: ZERO stringhe hardcoded nelle parti toccate; it.json + en.json insieme, parità chiavi/placeholder.
- **Test culture-independent**; commit atomici `fix|feat|refactor|test|chore(scope):`; commit SENZA `--no-verify` (typecheck deve restare verde); branch `feature/issue-3010-followup-cleanup` → PR `main-dev`.

---

## Task 1: CSV hardening
**Files:** Modify `apps/web/src/lib/utils/csv.ts`; test `src/lib/utils/__tests__/csv.test.ts`.
- `escapeCSVField` (riga 26): la condizione di quoting copre `, " \n` ma NON un bare `\r`. Aggiungere `|| value.includes('\r')`.
- Formula-injection guard: una cella il cui valore inizia con `= + - @` (o tab/CR) va prefissata con un apice singolo `'` (o `\t`) prima del quoting, per evitare esecuzione di formule in Excel/Sheets. Implementare un guard prima del quoting.
- [ ] Test RED→GREEN: `escapeCSVField('a\rb')` → quotato; `escapeCSVField('=1+1')` → prefissato (es. `"'=1+1"` o `'=1+1` secondo la scelta, documentare); valori normali invariati. `cd apps/web && pnpm vitest run src/lib/utils/__tests__/csv.test.ts`
- [ ] Commit `fix(utils): CSV escape bare CR + formula-injection guard`.

## Task 2: HistoryDetailModal footer — rimuovere bottoni no-op
**Files:** Modify `apps/web/src/app/(authenticated)/toolkit/history/_components/HistoryDetailModal.tsx`; update its test.
- Footer (righe 202-220): 4 bottoni senza `onClick` (gameStats/playAgain/editNotes/**delete**) — affordance morte. Rimuoverli (nessun backend). Il footer resta con la sola chiusura (`DrawerClose`, riga 96) o footer rimosso se vuoto.
- Rimuovere dal catalogo i18n le chiavi ora inutilizzate `pages.toolkitHistory.modal.{gameStats,playAgain,editNotes,delete}` in it.json + en.json (verificare che non siano usate altrove con grep prima di rimuovere).
- [ ] Test: il modal non rende più i bottoni no-op; test esistenti verdi. Commit `fix(toolkit-history): remove dead footer buttons from detail modal`.

## Task 3: AddToWishlistDialog — prefill priority high + Escape stopPropagation
**Files:** Modify `apps/web/src/components/wishlist/AddToWishlistDialog.tsx`; update test.
- Prefill priority (righe 144-146 + reset 161): quando `mode==='add'` E `prefillGameId` è presente, la priority iniziale deve essere `'high'` (mockup); altrimenti `'medium'`; in edit resta `normalizePriority(item.priority)`.
- Escape (righe 219-224): nel ramo `comboOpen` aggiungere `e.stopPropagation()` dopo `preventDefault()`, così Escape chiude SOLO la combobox senza propagare al Dialog. Il ramo `!comboOpen` (return) resta invariato (Escape propaga → chiude il dialog, corretto).
- [ ] Test: apertura add con `prefillGameId` → priority chip `high` selezionato; Escape con combo aperta non chiude il dialog (solo la lista). Commit `fix(wishlist): prefill high priority + scope combobox Escape`.

## Task 4: ARIA nits
**Files:** Modify `apps/web/src/app/(authenticated)/toolkit/history/client.tsx` (breadcrumb, riga 211), `apps/web/src/app/(authenticated)/library/wishlist/page.tsx` (breadcrumb, riga 115), `apps/web/src/app/(authenticated)/toolkit/history/_components/HistoryToolbar.tsx` (listbox, righe 138-168); it.json/en.json.
- Breadcrumb aria-label: aggiungere chiave i18n dedicata `pages.toolkitHistory.hero.breadcrumbAria` = "Percorso" (it) / "Breadcrumb" (en) e `pages.library.wishlist.hero.breadcrumbAria` idem; usarle come `aria-label` dei due `<nav>` (invece di riusare l'etichetta della pagina).
- MultiSelectPopover (HistoryToolbar righe 138-168): `role="listbox"` con figli misti option/checkbox è ARIA-invalido. Cambiare il `PopoverContent` a `role="group"` (un gruppo di checkbox multi-select non è una listbox); il bottone "Tutti" (riga 141) da `role="option"` a un `<button>` semplice o `role="checkbox"` con `aria-checked`. (DateRangeFilterPopover riga 258 `role="listbox"` su RadioGroup: cambiare a `role="group"` o rimuovere il role, coerente.)
- [ ] Test: i due nav hanno aria-label "Percorso"/"Breadcrumb"; nessun `role="listbox"` con figli non-option. Commit `fix(a11y): breadcrumb landmark labels + valid group role on filter popovers`.

## Task 5: dedup history-format helpers
**Files:** Create `apps/web/src/app/(authenticated)/toolkit/history/_lib/history-format.ts` + test; modify HistoryTable.tsx, HistoryCards.tsx, HistoryDetailModal.tsx, client.tsx.
- Estrarre `formatDuration(minutes)` (identica ×4) e `getInitials(name)` (identica ×3) + `MAX_AVATARS = 3` (×2) nel nuovo modulo. Importarli nei 4 file, rimuovere le copie locali.
- [ ] Test unit per formatDuration/getInitials (boundary: 0, single-word, multi-word, empty). Re-run history folder verde. Commit `refactor(toolkit-history): extract shared history-format helpers`.

## Task 6: dedup normalizePriority
**Files:** Modify `apps/web/src/app/(authenticated)/library/wishlist/_lib/wishlist-filters.ts` (export a string-based normalizer), `components/wishlist/MeepleWishlistCard.tsx`, `components/wishlist/AddToWishlistDialog.tsx`; tests.
- In `wishlist-filters.ts`: esportare `matchPriority(value: string): Priority | undefined` (già esiste, renderla export) e un `normalizePriorityString(raw: string | undefined): Priority` (= `matchPriority(raw ?? '') ?? 'medium'`). Tenere `computeStats` che usa `matchPriority` (no-fallback) invariato.
- In MeepleWishlistCard + AddToWishlistDialog: rimuovere la `normalizePriority` locale + `KNOWN_PRIORITIES`/`PRIORITY_ORDER` locali (se solo per questo), importare da wishlist-filters.
- [ ] Test: normalizePriorityString('HIGH')→'high', 'urgent'→'medium', undefined→'medium'. Component tests verdi. Commit `refactor(wishlist): single normalizePriority source in wishlist-filters`.

## Task 7: feature — openGameAria (nome gioco → dettaglio)
**Files:** Modify `apps/web/src/components/wishlist/MeepleWishlistCard.tsx`, `apps/web/src/app/(authenticated)/library/wishlist/page.tsx`; possibly `ui/data-display/meeple-card/variants/ListCard.tsx` for aria-label; tests.
- Aggiungere prop `onOpenGame?: (gameId: string) => void` a MeepleWishlistCard. Passarla a `<MeepleCard onClick={() => onOpenGame?.(item.gameId)}>` (ListCard onora `onClick`, non `href`). Aria-label dal `card.openGameAria` ({name}): poiché ListCard non accetta `aria-label` diretto, aggiungere il supporto `aria-label` al root di ListCard (prop opzionale passthrough) OPPURE wrappare — scegliere l'approccio meno invasivo e documentarlo.
- Le azioni interne (Edit riga ~171, Remove riga ~181, badge priorità righe ~137-153) devono chiamare `e.stopPropagation()` per non triggerare la navigazione del root.
- In `page.tsx`: importare `useRouter`, passare `onOpenGame={(id) => router.push('/library/' + id)}` nel `.map` (righe ~255-264).
- [ ] Test: click sul corpo card → onOpenGame(gameId); click su Edit/Remove/badge NON chiama onOpenGame (stopPropagation). Commit `feat(wishlist): navigate to game detail from wishlist card`.

## Task 8: feature — dialog error-state
**Files:** Modify `apps/web/src/components/wishlist/AddToWishlistDialog.tsx`; test.
- Destrutturare anche `isError`/`error`/`reset` dalla mutation attiva (add o edit, come già per `isPending` riga 154). Rendere un banner `role="alert"` con `dialog.error` + bottone `dialog.retry` (che ri-lancia il submit) quando `isError`. Chiamare `reset()` su nuovo submit e su `handleOpenChange`. Pattern di riferimento: `toolkit/history/client.tsx:279-295`.
- [ ] Test: mutation che rigetta → banner errore visibile con retry; retry ri-invoca mutate; reset pulisce l'errore. Commit `feat(wishlist): inline error-state + retry in add/edit dialog`.

## Task 9: cluster 4 — sostituire classi dead success/warning (25 file)
**Files:** i 25 file di produzione elencati nel dossier (settings/2FA, kb-hub, admin/ai, game-night, game-chat, toolkit, gamebook, layout/EmailVerificationBanner, ...); + `components/features/game-night-detail/__tests__/GameNightRsvpActionBar.test.tsx` (asserzione `toContain('bg-success')`).
- Sostituire OGNI occorrenza di `{bg|text|border|ring|hover:*|focus-visible:*|dark:*}-{success|warning}[/opacity]` e `-foreground`/`-ink` con l'equivalente arbitrary value `--c-*` (vedi Global Constraints). Preservare opacity, hover, dark, ring. `text-success-foreground`/`text-warning-foreground` → `text-white` (fondo arbitrary colorato).
- Aggiornare il test `GameNightRsvpActionBar.test.tsx` che asserisce la stringa `'bg-success'` → nuova stringa (o asserire il colore in altro modo).
- ⚠️ Questo è un CAMBIO VISIVO: elementi oggi senza colore mostreranno success(verde)/warning(giallo). Ripristina l'intento del design. Verificare che nessun `text-white` finisca su fondo chiaro (contrasto) — usare `--c-warning-ink` per testo warning su fondo chiaro dove serve AA.
- [ ] Grep di verifica: 0 occorrenze residue di `-success`/`-warning` come utility Tailwind (solo arbitrary `--c-*` o commenti). typecheck + lint + lint:tokens verdi. Test suite dei file toccati verde. Commit `fix(design-tokens): replace dead success/warning utilities with canonical --c-* arbitrary values`.

## Task 10: final verify + PR
- [ ] Test suite dei path toccati + typecheck + lint + lint:tokens. Verifica visiva (dev fresco :3100) di un campione dei componenti cluster-4 + wishlist card (openGameAria) + dialog error-state.
- [ ] PR → main-dev, Closes #3010. Documentare il cambio visivo cluster-4.

## Self-Review
Copertura: CSV(T1) footer(T2) dialog-prefill/Escape(T3) ARIA(T4) dedup(T5,T6) feature openGameAria(T7) error-state(T8) dead-class(T9) verify(T10). Tutti i 12 finding + cluster 4 mappati. Nessun placeholder.

# Verifica visiva UI lato-user vs mockup

**Data**: 2026-05-25 · **Metodo**: server dev live (`pnpm dev` :3000) + screenshot Playwright, backend Docker già attivo (:8080) · **Locale runtime**: `it`
**Ambito**: pagine "UI lato user recenti" (Epic #1475) — game detail + tab e gamebook encounter
**Account**: `badsworm@gmail.com` (superadmin)

## Verdetto sintetico

La migrazione grafica è **strutturalmente riuscita** (layout, hero, tab bar, KPI, gating community/own, stato entry encounter combaciano coi mockup), **ma sono presenti regressioni a runtime** sulle due feature game-detail più recenti (#1464 house rules, #1466 stats tab): **chiavi i18n non tradotte visibili a schermo**. I test CI passano perché usano un oggetto `MESSAGES` in-memory esteso, mentre il **catalogo runtime `it.json`/`en.json` non è stato aggiornato** (gap del pattern "P71").

| Pagina / variante | Layout vs mockup | Esito | Screenshot |
|---|---|---|---|
| Game detail — own (Info) | ✅ combacia | ⚠️ chiavi i18n grezze (house rules) | `game-detail-own-info-tab.png` |
| Game detail — own (Stats) | ✅ combacia | ❌ chiavi i18n grezze + errore dati | `game-detail-own-stats-tab.png` |
| Game detail — community | ✅ combacia, gating OK | ⚠️ chiave i18n `stats` grezza | `game-detail-community-locked-tabs.png` |
| Gamebook encounter (entry/A) | ✅ combacia | ✅ tradotto correttamente | `gamebook-encounter-entry-state.png` |
| Mockup game detail (rif.) | — | — | `MOCKUP-game-detail-sp4.png` |
| Mockup encounter (rif.) | — | — | `MOCKUP-encounter-cheatsheet.png` |

## Cosa funziona (successo migrazione)

- **Layout game detail** = mockup `sp4-game-detail`: hero con colore entità (game = arancione), tab bar, KPI card (Valutazione/Complessità/Giocatori/Durata), griglia Specifiche, sezione FAQ.
- **Gating variante community** (#1465/#1466): badge "🌐 Community", tab **Partite** e **Stats** correttamente bloccati (`🔒`, `aria-disabled`).
- **Variante own**: badge "✓ In libreria", tutti i tab sbloccati, sezione house rules e tab stats presenti.
- **Encounter entry-state (state A)** (#1484): card story-context ("§5 Storybook · ultimo paragrafo letto"), CTA "📷 Fotografa Encounter §10 · nessun salvataggio long-term" — fedele al mockup e **i18n corretta**.

## Difetti rilevati (con evidenza)

### 1. ❌ Chiavi i18n mancanti nel catalogo runtime (IT **e** EN) — game detail
Rese come id grezzi a schermo su **ogni** game detail:
- `pages.gameDetail.tabs.stats` (label tab) — #1466
- `pages.gameDetail.stats.winRate` · `.timesPlayed` · `.lastPlayed` · `.lastPlayedNever` (KPI stats) — #1466
- `pages.gameDetail.houseRules.title` · `.addCta` · `.empty` — #1464

**Evidenza**: `apps/web/src/locales/it.json` (blocco `gameDetail.tabs` righe 2344-2352) e `en.json` (righe 2294-2302) contengono solo `info/rules/faqs/sessions/agents/documents` — nessun `stats`; nessuna sezione `houseRules` in entrambi i file.
**Causa**: i `MESSAGES` di test estesi (pattern P71) ma catalogo runtime non aggiornato → test verdi, runtime rotto.
**Fix**: aggiungere le chiavi a `it.json` + `en.json` sotto `pages.gameDetail` (`tabs.stats`, sezione `stats.*`, sezione `houseRules.*`).

### 2. ⚠️ Intl FORMAT_ERROR (×2) — placeholder non interpolato
Console: `[IntlProvider] Intl error [@formatjs/intl Error FORMAT_ERROR]`. Messaggio con placeholder ICU (probabile `faqs.subtitle` = `"Le domande più frequenti su {title}."`) reso senza passare `values={{ title }}`.
**Fix**: passare il valore `title` al `formatMessage`/`<FormattedMessage>` corrispondente.

### 3. ❌/⚠️ Errori API a runtime
- `GET /api/v1/gamebooks` → **500** (EF Core `ApplyUnion`/`ApplySetOperation`: query UNION non traducibile). Bug backend reale sul listing gamebook.
- `GET /api/v1/private-games/{id}` → 404 (rilevamento variante own; loggato come errore).
- `GET /api/v1/agent-memory/games/{id}/memory` → 404 (×2; fetch house rules/memory).
- Tab Stats: stato d'errore **"Impossibile caricare il gioco"** (play-stats/leaderboard non caricati per questo gioco).

### 4. 🎨 Cosmetico (non-migrazione)
- Immagine hero BGG (`cf.geekdo-images.com`) → 503 dalla CDN esterna → hero mostra il gradiente fallback.
- `aria-label` su `<div>` senza `role` valido (segnalazione axe in dev).

## Note encounter
Gli stati B (segmenting), C (cheatsheet con stats nemici/opzioni/condizioni) e D (resolved) del mockup richiedono una **foto reale + parse LLM**; lo state D è **deferito by design** nell'MVP #1484. Render fedele non verificabile live senza fixtures foto e con `/api/v1/gamebooks` in 500.

## Follow-up consigliati
1. **P0** — aggiungere le chiavi i18n mancanti (`tabs.stats`, `stats.*`, `houseRules.*`) a `it.json`/`en.json` (difetto #1, user-visible su tutte le game detail).
2. **P1** — fix FORMAT_ERROR FAQ subtitle (passare `title`).
3. **P1** — indagare il 500 di `/api/v1/gamebooks` (query EF UNION).
4. **P2** — verificare 404 `private-games`/`agent-memory` (attesi vs gestione errore) + stato errore tab Stats.
5. **Guardrail** — aggiungere un check che le chiavi usate nei componenti esistano nel catalogo runtime (non solo nei `MESSAGES` di test), per chiudere il gap P71.

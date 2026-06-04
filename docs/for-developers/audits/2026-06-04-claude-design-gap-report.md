# Claude Design — Gap Report Demo MeepleAI

**Data**: 2026-06-04
**Tool**: claude.ai/design (Opus 4.7)
**Design ID**: 038677b3-6790-4c20-92e8-f770043129e0
**Bundle source**: `claude-design-bundle/` (19 file, snapshot 2026-06-04, gitignored — rebuild via `cp` script)
**Mockup analizzati**: 12 (sp4-* + sp7-game-night-create + auth-flow + onboarding)
**Route prototipate**: 13 funzionanti + 5 stub
**Iterazioni**: 5 turni funzionali + 1 turno report + 4 fix correttivi mid-flight
**Modello dominio consolidato via**: socratic loop (9 domande) + 5 invarianti introdotte iterando
**Spec correlata**: [domain model](../specs/2026-06-04-gamenight-session-domain-model.md)

---

## Sezione 1 — Tabella gap completa

| # | categoria | route/schermo | descrizione | severity | proposta fix |
|---|---|---|---|---|---|
| 1 | CTA | Agent Detail /agents/[id] | "Inizia chat" è la CTA primaria ma non apre nulla: la route /chat/[threadId] non esiste | high | costruire /chat/[threadId] o aprire un drawer chat reale |
| 2 | CTA | Game Detail /games/[id] | "Avvia Game Night" (CTA primaria) non innesca nessun flow di creazione | high | collegare al wizard /game-nights/new pre-popolando il game |
| 3 | ENTITY | multiple (Dashboard, /game-nights) | Auto-promotion planned → in-progress: trigger non definito | high | definire trigger esplicito vs temporale (vedi Top 10 #1) |
| 4 | ENTITY | multiple (Dashboard, /game-nights/[id], Wizard) | Tagging vs RSVP: se Marco tagga Anna non-confermata, side-effect su dashboard di Anna indefinito | high | regola esplicita tagging→RSVP |
| 5 | ENTITY | Session editor (drawer draft) | Scoring polimorfico assente: solo Points, mancano BinaryWin / Objectives / Ranking | high | renderer scoring per ScoreType |
| 6 | ROUTE | global | I .jsx twin dei 12 mockup sono assenti dal bundle: ogni schermo è ricostruito da spec, non pixel-match all'originale | high | recuperare i .jsx o validare la ricostruzione col design |
| 7 | ROUTE | Sidebar (/games vs /library) | Semantica sovrapposta: entrambe mostrano giochi posseduti | high | definire Games=catalog globale, Library=collezione personale |
| 8 | CTA | /agents | "+ Crea agent" è placeholder | med | agent creation flow |
| 9 | CTA | Auth /login | "Password dimenticata?" placeholder | med | flow reset password |
| 10 | CTA | Auth /login+/register | OAuth Google/Discord placeholder | med | integrazione OAuth |
| 11 | CTA | Auth /register | Link "Terms & Privacy" placeholder | med | legal pages |
| 12 | CTA | Game Detail /games/[id] | tab Knowledge base → "Apri Knowledge Base" punta a /knowledge-base (assente) | med | costruire hub KB (dep #19) |
| 13 | CTA | Game Nights /game-nights/[id] | "Vai al riepilogo" → /game-nights/[id]/summary non costruita | med | costruire summary (dep #20) |
| 14 | CTA | multiple (Library, Onboarding) | "Aggiungi gioco" / "Importa BGG": flow BGG vs manuale non definito | med | definire import pipeline |
| 15 | ENTITY | global | Multi-live / parallel play fuori scope MVP (1 sola live per GameNight) | med | tracciato come feature futura |
| 16 | ENTITY | /game-nights/[id]/live | "Pausa live" → transizione live → draft non definita | med | state machine pausa |
| 17 | ENTITY | multiple (Wizard, Discover) | Algoritmo "suggested for tonight / for you" opaco (fixture) | med | definire input (player count, library, storia) e ranking |
| 18 | ROUTE | /discover | /toolkit/[id] detail non costruita | med | costruire toolkit detail |
| 19 | ROUTE | /sessions, /game-nights/[id] | /sessions/[id] (session detail live+summary) non costruita: il drawer è placeholder | med | costruire session detail |
| 20 | ROUTE | /sessions | Overlap semantico /sessions (cross-GameNight) vs /games/[id]/sessions (per-game) | med | unificare con filtro o chiarire scope |
| 21 | STATE | multiple (tutti gli schermi SP4) | I 5 stati sono ricostruiti da state-matrix.html (no JSX twin) → non validati vs originale | med | validare con design |
| 22 | STATE | /onboarding | loading/error/offline esistono solo allo step 2 (BGG); step 1/3 senza stati async | med | confermare che è intenzionale |
| 23 | CTA | Agent Detail /agents/[id] | tab KB "+ Aggiungi documento" placeholder | low | upload documento |
| 24 | CTA | Agent Detail /agents/[id] | tab Settings: salvataggio config placeholder | low | persistenza config |
| 25 | CTA | multiple (Wizard, New-session modal) | "+ Aggiungi un altro game" → search library placeholder | low | search library |
| 26 | CTA | global | Email verification post-registrazione fuori scope MVP | low | flow verifica inbox |
| 27 | ENTITY | /game-nights/[id]/live | Badge "live" = attributo della session attiva, non stato della GameNight (chiarito) | low | nessuna azione — documentato |
| 28 | ENTITY | /game-nights/[id] | RSVP per-player sintetizzato (data.js ha solo contatori aggregati) | low | modello RSVP per-player |
| 29 | ENTITY | /library | Dataset fixture limitato (8 giochi) | low | dataset reale |
| 30 | ENTITY | Game Detail /games/[id] | "Tempo giocato" stimato; descrizione/meccaniche/categoria assenti in data.js | low | campi reali |
| 31 | ENTITY | /game-nights/[id]/live | Risposte chat agent fixture (nessun backend) | low | backend agent |
| 32 | ENTITY | /game-nights/[id] | Drag-to-reorder games non implementato (funziona via ↑↓) | low | gesture drag |
| 33 | ENTITY | multiple (GN detail, Session editor) | Upload foto galleria placeholder | low | upload |
| 34 | ENTITY | Session (retrospettiva) | Edit manuale di completedAt per draft retrospettive fuori scope | low | edit manuale orario |
| 35 | STATE | Auth /login+/register | empty non applicabile (form) — omesso | low | nessuna azione |
| 36 | STATE | Game Detail /games/[id] | empty non applicabile a livello pagina (404 oppure esiste) | low | nessuna azione |
| 37 | TOKEN | multiple (gap-badge, ses-chip draft, agent-cat, auth-banner off) | Shade scuro warning hsl(38 92% 32%) improvvisato per contrasto in light-mode (non in tokens.css) | low | aggiungere token --c-warning-ink |
| 38 | TOKEN | multiple (disc-hero, cover, scrim, toast) | Overlay rgba(...) e shade gradient/hover (hsl(25 95% 38%), #1a1206) hardcoded fuori da tokens.css | low | tokenizzare overlay e on-color |

---

## Sezione 2 — Top 10 priorità

1. **JSX twin dei mockup assenti** · ROUTE · global
   La fedeltà visiva non è verificabile: ogni schermo è una ricostruzione spec-driven, non un match all'originale. Rischio di divergenza su layout/spacing/micro-interazioni.
   Effort: **L** · blocca la validazione di tutto il resto.

2. **Auto-promotion planned → in-progress** · ENTITY · Dashboard + /game-nights
   Senza trigger definito, una serata non passa mai a "In corso" in modo prevedibile; la dashboard "Prossimi" e il badge IN CORSO diventano inaffidabili.
   Effort: **S** · blocca anche #4 (logica live).

3. **Tagging vs RSVP** · ENTITY · multiple
   Se taggare un player lo iscrive senza conferma, la dashboard di un invitato si popola di serate non accettate (spam/privacy).
   Effort: **S**.

4. **Scoring polimorfico assente** · ENTITY · Session editor
   Solo giochi a punti sono registrabili; cooperativi (BinaryWin) e a obiettivi non hanno UI di scoring → flow "salva session" rotto per ~metà dei giochi.
   Effort: **M** · dipende dal modello ScoreType.

5. **Games vs Library** · ROUTE · Sidebar
   Due voci di navigazione che mostrano lo stesso contenuto: l'utente non sa dove andare; rischio di duplicare azioni (aggiungi gioco) in due posti.
   Effort: **S** · decisione product, poi piccola implementazione.

6. **"Inizia chat" agent senza destinazione** · CTA · /agents/[id]
   La CTA primaria della pagina (e core value prop "chiedi all'agente") non fa nulla.
   Effort: **M** · dipende da /chat/[threadId].

7. **"Avvia Game Night" da Game Detail morto** · CTA · /games/[id]
   CTA primaria che non avvia il wizard; il percorso "dal gioco alla serata" è interrotto.
   Effort: **XS** · basta deep-link al wizard esistente.

8. **/sessions/[id] session detail assente** · ROUTE · /sessions, /game-nights/[id]
   Cliccando una session si apre solo un placeholder: niente dettaglio partita (live/summary).
   Effort: **L** · blocca anche #13 (summary serata).

9. **Pausa live → draft** · ENTITY · /game-nights/[id]/live
   Il pulsante "Pausa live" non ha transizione definita: stato indeterminato se l'utente mette in pausa.
   Effort: **S** · dipende da #2/#4 (state machine session).

10. **/knowledge-base hub assente** · ROUTE/CTA · /games/[id]
    Il link KB porta a una route non costruita; la foundation dati (documenti che alimentano gli agent) non è navigabile.
    Effort: **M** · dipende dalla feature KB.

---

## Sezione 3 — Domain model emerso dal socratic loop

1. **Cardinalità**: 1 GameNight → N Session.
2. **Timing registrazione**: live opzionale; default = inserimento risultati post-partita.
3. **Player identity**: mix di User-linked + guest free (senza account).
4. **Priorità dashboard**: Prossimi > Recenti > Suggested ("Potresti giocare") > Friends.
5. **Drawer Player**: 3 sezioni — Relational (vs me) + Profile + Quick Actions (contestuali alla GameNight).
6. **Ownership session**: condivisa in read-only ai Player User-linked della serata.
7. **Granularità "Recenti"**: 1 card = 1 GameNight wrapper (non 1 partita).
8. **Stati GameNight**: planned / in-progress / completed.
9. **Naming user-facing**: GameNight = wrapper sociale; Session = partita dentro la serata (termine visibile solo dentro il dettaglio).
10. **Invariante** — max 1 session live per GameNight in qualsiasi istante.
11. **Invariante** — 3 timestamp distinti per Session: createdAt (sempre), startedAt (nullable), completedAt (nullable).
12. **Invariante** — sorting session: createdAt ascending, deterministico (ordine di registrazione).
13. **Invariante** — salvataggio draft con live attiva: permesso, con warning non bloccante (Opzione C).
14. **Invariante** — "ora di inizio": derivata da startedAt, non input utente.

---

## Sezione 4 — Tensioni aperte

1. **Auto-promotion "In corso"** — trigger temporale (data/ora raggiunta) vs trigger esplicito (apertura Live mode).
   Opzioni: (a) promozione automatica all'ora di inizio; (b) promozione manuale all'apertura live.
   **Raccomandazione**: trigger esplicito (b) — coerente con il pattern "navigate-to-live richiede scelta esplicita" già adottato; l'orario resta solo informativo.

2. **Tagging vs RSVP** — un tag iscrive o solo notifica?
   Opzioni: (a) tag = invito con RSVP pending (default non visibile in dashboard finché non accetta); (b) tag = aggiunta diretta.
   **Raccomandazione**: (a) — il taggato vede l'invito in Notifiche, la serata appare in dashboard solo dopo conferma.

3. **/sessions vs /games/[id]/sessions** — cross-GameNight vs filtrata per gioco.
   Opzioni: (a) due viste distinte; (b) una vista /sessions con filtro per game via query param.
   **Raccomandazione**: (b) — evita duplicazione di componenti; il game detail linka a `/sessions?game=`.

4. **Multi-live / parallel play** — più tavoli simultanei nella stessa serata.
   Opzioni: (a) resta 1 live (MVP); (b) N live con tab per tavolo.
   **Raccomandazione**: (a) per l'MVP, riaprire post-lancio se emerge dai dati d'uso.

5. **Games vs Library** — catalog globale (discovery) vs collezione personale.
   Opzioni: (a) Games = catalogo pubblico/discovery, Library = posseduti+wishlist; (b) fondere in un'unica voce con tab.
   **Raccomandazione**: (a) — ma valutare se "Games" non sia ridondante con "Discover" (rischio 3 superfici di scoperta).

---

## Sezione 5 — Statistiche demo

- **Gap totali**: 38
- **Per categoria**: CTA 13 · ENTITY 14 · ROUTE 5 · STATE 4 · TOKEN 2
- **Per severity**: high 7 · med 15 · low 16
- **Route prototipate**: 13 — `/dashboard`, `/library`, `/games/[id]`, `/game-nights`, `/game-nights/new`, `/game-nights/[id]`, `/game-nights/[id]/live`, `/sessions`, `/discover`, `/agents`, `/agents/[id]`, `/login`+`/register`, `/onboarding`.
- **Route stub** (referenced, not built): `/games`, `/knowledge-base`, `/toolkit/[id]`, `/sessions/[id]`, `/game-nights/[id]/summary`.
- **Badge [GAP-X] visibili nel prototipo**: ~33 in stato default + ~10 GAP-STATE che compaiono al cambio stato + 5 marker non-runtime in commento HTML (multi-live, pausa live, manual edit completedAt, manual sort, email verification).
- **Invarianti modello dominio consolidate**: 14 (vedi Sezione 3).

---

## Note / limitazioni della demo

- Non testato responsive sotto 768px (layout ottimizzato desktop; sidebar collassa <920px ma le griglie fitte non sono validate su mobile).
- Drawer stack validato fino a 2 livelli (GameNight → Player); profondità maggiori non testate.
- "Simulate first visit" è un dev-tool del prototipo, non parte del prodotto.
- I 6 marker introdotti come categorie estese in-prototipo (`GAP-DATA`, `GAP-FEATURE`) sono qui riconciliati nelle 5 categorie canoniche: `GAP-FEATURE`→CTA o ENTITY, `GAP-DATA`→ENTITY.
- Mutazioni in-memory (nuove GameNight, draft, terminazione live) si resettano al reload — comportamento atteso per un prototipo fixture.

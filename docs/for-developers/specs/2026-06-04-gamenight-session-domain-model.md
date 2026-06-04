# Domain Model — GameNight / Session

**Data**: 2026-06-04
**Origine**: socratic loop nella demo Claude Design (`docs/for-developers/audits/2026-06-04-claude-design-gap-report.md`)
**Stato**: consolidato 9 fatti + 10 invarianti (2026-06-04 socratic loop, 5 tensioni risolte 2026-06-04 sessione successiva)
**Scope**: bounded context `SessionTracking` + `GameManagement` (sub-aggregate GameNight)

> Riferimento product: questo doc cattura **decisioni semantiche** prese durante la spec-panel socratic mode del 2026-06-04. Va letto prima di toccare codice nei bounded context interessati.

---

## Concept map

```
GameNight  (evento sociale, status: planned/in-progress/completed)
├── owner: User (chi l'ha creata)
├── date, location, host (può essere ≠ owner)
├── players[]      ← lista di Player references
│   └── Player  (entry anagrafica, opzionalmente .userId)
└── sessions[]     ← 1..N partite giocate quella sera
    ├── game (ref Game catalog)
    ├── playerScores[] (player_id → score, position)
    ├── notes, photos
    ├── createdAt (always)
    ├── startedAt (nullable — when live mode opens)
    └── completedAt (nullable — when saved or live terminated)
```

---

## 9 fatti consolidati (dal socratic loop)

### 1. Cardinalità

**1 GameNight → N Session**.

Una GameNight è un wrapper sociale (la "serata"). Le N session sono le partite giocate dentro quella serata. Sabato a casa di Marco (Wingspan ×2 + Codenames) = **1 GameNight, 3 Session**.

### 2. Timing di registrazione

**Live opzionale; default = post-partita**.

L'utente apre l'app principalmente *a fine partita* per registrare risultati. La live mode è una scelta esplicita (toggle "Avvia in Live mode" o CTA "Apri Live mode"). MAI default per stato in-progress.

### 3. Player identity

**Mix User-linked + guest free**.

Quando crei una GameNight, aggiungi player. Ogni Player è un'anagrafica interna (nome + avatar). Opzionalmente puoi "linkarlo" a un User MeepleAI esistente (UI shows "✓ User" badge). I guest restano Player senza User reference (badge "Guest").

### 4. Priorità dashboard

**Prossimi > Recenti > Suggested > Friends**.

Ordine verticale fisso. La sezione "Prossimi" mostra GameNight `planned` ordinate per data ascendente (la più imminente prima). "Recenti" mostra `completed` per data discendente. "Suggested" è discover algoritmico. "Friends" è feed social minimal.

### 5. Drawer Player — 3 sezioni

Click su un Player (es: da drawer GameNight) apre drawer Player con queste sezioni:

- **Relational**: stats vs me (partite insieme, win rate, giochi comuni). Solo se Player linked a User ≠ me. Per guest: messaggio "Guest player — no account associato".
- **Profile**: avatar, bio, library pubblica del Player (se linked).
- **Actions**: quick actions contestuali alla GameNight da cui arrivo (modifica score, rimuovi, aggiungi nota).

### 6. Ownership Session

**Auto-shared read-only ai Player User-linked**.

Marco crea GameNight e tagga Anna (User-linked). Anna **vede la GameNight nella propria dashboard** in "Recenti" automaticamente, **read-only**. Non può modificare ma può consultare.

> ⚠️ Tensione aperta: vedi #2 sotto. La regola attuale potrebbe diventare "vede solo dopo aver confermato RSVP" se andiamo con opt (a).

### 7. Granularità "Recenti"

**1 card = 1 GameNight wrapper**.

Sabato a casa di Marco (3 partite) compare come **1 sola card** nella dashboard "Recenti". Per vedere le 3 session, espandi (drawer GameNight).

NON 3 card separate (Wingspan run 1, Wingspan run 2, Codenames).

### 8. Stati GameNight

**`planned` / `in-progress` / `completed`**.

3 stati esclusivi nel ciclo di vita. Transitions:
- `planned → in-progress`: auto-promotion (TBD, tensione #1) o manuale all'apertura Live mode
- `in-progress → completed`: manuale (CTA "Termina serata") o all'ultima session salvata
- Nessuna transition backward in MVP (no "annulla completamento")

### 9. Naming user-facing

**GameNight = termine wrapper, Session = termine partita interna**.

- Sidebar voce: "Game Nights"
- Card dashboard: "Game Night di Sabato a casa di Marco · 3 partite"
- Dentro il drawer GameNight: "Session 1: Wingspan", "Session 2: Wingspan", "Session 3: Codenames"
- **Mai "Session" come termine standalone nella dashboard top-level**.
- Eccezione: `/sessions` (voce sidebar = vista cross-GameNight) usa "Session" come label perché lì è il contesto archivistico.

---

## 5 invarianti (introdotte durante l'iterazione)

### Invariante 1 — Max 1 live per GameNight

In qualsiasi istante una GameNight può avere **al massimo 1 session in stato `live`**. Mai 2+ contemporanee.

Implicazioni:
- Il modale "+ Nuova session" con live attiva: toggle "Avvia in Live mode" è **disabled** (visibile + spiegazione "Una session live è già attiva")
- L'utente può sempre creare draft anche con live attiva (per registrazioni retroattive)
- Per avere una seconda live: prima terminare la corrente

**Out of scope MVP**: multi-live / parallel play (es: 2 tavoli simultanei). Tensione #4.

### Invariante 2 — 3 timestamp Session distinti

```
Session.createdAt:   DateTimeOffset (NOT NULL, auto on insert)
Session.startedAt:   DateTimeOffset? (NULL = mai entrata in live)
Session.completedAt: DateTimeOffset? (NULL = draft o live in corso)
```

Semantica:
- `createdAt` = quando la row appare nell'app (click "Crea session" nel modale o creazione implicita di una live)
- `startedAt` = quando l'utente avvia "Apri Live mode" (parte il timer real-time)
- `completedAt` = quando l'utente clicca "Salva session" (drawer editor) o "Termina session" (in /live)

### Invariante 3 — Sorting Session

**`createdAt` ascending** è il sort default e deterministic per la lista session sia in `/game-nights/[id]` che in `/sessions`.

Motivazioni:
- `createdAt` mai null → deterministic
- Corrisponde a "ordine di registrazione" / intent narrativo dell'utente
- Non assume conoscenza dell'ora di gioco reale (che potrebbe essere null per draft retroattive)

NO toggle sort manuale per MVP.

### Invariante 4 — Salvataggio draft con live attiva

**Permesso + warning non bloccante** (opzione C scelta nel panel).

Quando Marco salva la draft Azul mentre Wingspan è ancora live:
1. La save procede normalmente: `completedAt = now`, status → `completed`
2. **Nessun blocco**, nessun confirm dialog
3. Toast non-bloccante 6s appare bottom-right (warning ambra): "Session salvata mentre una live è in corso. Verifica che le partite siano state inserite nell'ordine corretto."
4. Toast contiene link micro "Vai alla session live"

### Invariante 5 — "Ora di inizio" derived

**`Session.startedAt` NON è user input**. È derived dal timestamp di "Apri Live mode".

Implicazioni UI nel drawer editor session draft:
- ❌ Field "Ora di inizio" (time picker) → **rimosso**
- ❌ Field "Durata" (input) → **rimosso**
- ✅ Display read-only nell'header drawer: "Iniziata alle HH:MM" / "Terminata alle HH:MM" / "Durata N min" solo se valorizzati
- ✅ Textarea "Note" rimane come unico input opzionale

Out of scope MVP: edit manuale di `completedAt` per correggere a posteriori draft retrospettive.

---

## Tensioni risolte (decisioni product 2026-06-04)

Le 5 tensioni aperte dal gap report sono state risolte in sessione socratic dedicata, stessa data. Per ciascuna: opzione scelta, motivazione, invariante derivata.

### Tensione 1 — Auto-promotion `planned → in-progress`

**Decisione**: trigger esplicito. La GameNight passa a `in-progress` **alla creazione della prima Session** della serata, **di qualsiasi tipo** (draft O live, non importa).

**Esempio**: Sabato a casa di Marco, GameNight `planned`. Marco apre app, "+ Nuova session" → seleziona Azul → toggle Live OFF → "Crea session". La row draft viene creata **E** la GameNight passa a `in-progress`. Nessun timer, nessuna live: la sola intent dell'utente di registrare una partita è il trigger.

**Motivazione**: coerente con pattern "navigate-to-live è scelta esplicita" già adottato. La data/ora pianificata resta solo info, mai trigger automatico. Evita falsi positivi (es: GameNight programmata che diventa in-progress da sola alle 20:00 anche se la serata è stata annullata).

→ **Invariante #15**: GameNight transition `planned → in-progress` triggered by first Session creation (draft or live).

### Tensione 2 — Tagging vs RSVP

**Decisione**: flow a 5 fasi con tagging silente + invio inviti esplicito + card pending in dashboard invitato.

**Flow canonico**:
1. **Creazione GameNight** (Marco): wizard `/game-nights/new`, tagga player. Submit → GameNight `planned`, player taggati, **nessuna notifica inviata**.
2. **Invio inviti** (Marco, azione separata): apre GameNight detail. Sezione "Player" mostra tagged. CTA esplicita "Invia inviti" (entity color GameNight rosa). Click → batch send notifiche a User-linked.
3. **Notifica ricevuta** (Anna): notifica in `/notifications` ("Marco ti ha invitato a Sabato").
4. **Visibilità dashboard** (Anna): GameNight appare in dashboard "Prossimi" come **card pending** (badge giallo "Da confermare", card semitrasparente). RSVP **inline sulla card** con button Conferma/Declina.
5. **Post-RSVP** (Anna): tap Conferma → card normale. Marco vede counter RSVP aggiornato.

**Edge case — modifiche post-invio**:
- Edit GameNight (data/location/games) → notifica di update **silente**. Anna vede badge "modificata" sulla card, **NO re-RSVP forzato**.
- Aggiunta nuovo player post-invio → notifica **auto-send** solo al nuovo player. Marco non deve riaprire "Invia inviti".

**Motivazione**: separa intent ("voglio fare la serata con loro") da action ("manda gli inviti ora"). Evita spam dashboard di invitati che ricevono auto-share senza aver confermato. Riduce friction sul caso "modifica al volo" preservando trasparenza.

→ **Invariante #16**: GameNight ha 2 stati distinti di "popolazione player": **tagged** (in lista wizard, no notifica) e **invited** (post "Invia inviti", notifica spedita).
→ **Invariante #17**: Invited player vede GameNight in dashboard solo in stato **pending** finché non RSVP. Solo dopo "Conferma" la card diventa normale auto-shared read-only.

### Tensione 3 — `/sessions` vs `/games/[id]/sessions`

**Decisione**: nessuna sub-route per-game. Tab "Partite recenti" di Game Detail **resta dentro** Game Detail con **espansione inline + paginazione**.

**Comportamento concreto**: tab rinominato "Partite". Mostra prime 10 ordinate per createdAt desc. Sotto: button "Carica altre" (paginazione fetch-on-click) + filter chip per anno o GameNight padre. Zero overlay, zero drawer, zero navigate.

**Implicazione laterale**: la voce sidebar `/sessions` cross-GameNight resta valida ma cambia scope semantico. Non più "vedi tutte le partite di un game" (Game Detail copre quel caso), bensì "timeline mista cross-game della mia attività di gioco". È la voce sidebar di **archive personale**.

**Motivazione**: l'utente che è dentro Game Detail vuole confrontare le sue partite di quel game senza perdere contesto (rating, stats, KB associato). Forzare un navigate a /sessions con filter perde il "dove sono" mentale. Inline expansion è l'affordance più naturale.

→ **Invariante #18**: Game Detail tab "Partite" è **self-contained** — paginazione + filter inline, nessun navigate verso /sessions per il caso per-game.

### Tensione 4 — Multi-live / parallel play

**Decisione**: out of scope MVP. **Periodo**. Parallel play si gestisce con draft retrospettive post-game.

**Esempio**: 6 player a casa di Marco. Alle 22:00 si dividono — 4 fanno Codenames (15 min), 2 fanno Hive in attesa. Comportamento MVP: l'utente registra Codenames e Hive come **2 session draft** a fine serata (compila score per ognuna). Nessuna live concorrente.

**Motivazione**: state machine multi-live introduce complessità sproporzionata per un caso d'uso non frequente. La draft retrospettiva copre già il pattern. Riapertura post-lancio possibile se signal d'uso (es: telemetry mostra creazione frequente di draft "subito dopo" una live in corso = segnale di parallel play frustrato).

→ **Invariante #19**: parallel-live tracking **non supportato MVP**. UI non offre alternative: l'utente che fa parallel play registra come draft retrospettive.

### Tensione 5 — `Games` vs `Library` sidebar

**Decisione**: sidebar a 2 voci giochi (rimosso `Discover` come voce sidebar).

- **`Library`** → collezione personale (owned + wishlist + played)
- **`Games`** → catalogo globale ricercabile. **Landing tab = Discover di default**.

`Discover` come funzionalità rimane (trending, suggested, community toolkits, friend activity), ma come **tab interno** di `/games`, non come voce sidebar. Quando l'utente apre `Games`, atterra su Discover tab. Tab alternativi: "Catalogo" (search + filter avanzati BGG-like), "Trending", "Community".

**Motivazione**: 3 superfici di esplorazione (Discover + Games + Library) creavano cognitive load. Fondere Discover dentro Games rispetta il job-to-be-done "scopro nuovi giochi" come navigation atomica, non come scelta tra 2-3 voci. Library resta separata perché copre un job diverso ("gestisco i miei").

→ **Invariante #20**: sidebar ha **2 voci game-related**: `Library` (personale) + `Games` (esplorazione, default tab Discover).

---

## Riepilogo invarianti aggiornato

Il modello consolidato 2026-06-04 ha ora **20 invarianti** (5 originali + 15 derivate):

- Invarianti #1-#9: 9 fatti consolidati dal socratic loop primario (vedi sezione sopra)
- Invariante #10: max 1 live per GameNight
- Invariante #11: 3 timestamp Session distinti (createdAt, startedAt, completedAt)
- Invariante #12: sorting Session = createdAt ascending
- Invariante #13: salvataggio draft con live attiva permesso + warning non bloccante
- Invariante #14: "ora di inizio" derived da startedAt
- **Invariante #15**: GameNight `planned → in-progress` triggered by first Session creation (draft or live)
- **Invariante #16**: GameNight player ha 2 stati: tagged (no notifica) vs invited (notifica spedita)
- **Invariante #17**: Invited player vede GameNight in dashboard solo come pending fino a RSVP confermato
- **Invariante #18**: Game Detail tab "Partite" è self-contained con paginazione inline (no navigate a /sessions)
- **Invariante #19**: parallel-live tracking non supportato MVP, fallback = draft retrospettive
- **Invariante #20**: sidebar 2 voci game-related — Library (personale) + Games (Discover come default tab)

---

## Tensioni laterali emerse (non bloccanti)

Tensioni meno gravi emerse durante la risoluzione delle 5 principali. Non bloccanti per l'MVP, valutabili in iterazioni successive.

1. **Voce sidebar `/sessions` ha scope ridotto post-decisione 3.** Resta come timeline cross-game generica ("archivio personale"). Potrebbe sembrare ridondante per utenti che usano poco la vista cross-game. Tracking telemetry post-MVP: se < 5% di nav events va su /sessions → considerare rimozione/spostamento sotto profile.

2. **Edit GameNight "sostanziale" vs "non sostanziale".** Decisione 2 E1: edit silente sempre, niente re-RSVP forzato. Però se Marco sposta la GameNight da Sabato a Domenica, alcuni invitati potrebbero non vedere la modifica e presentarsi nel giorno sbagliato. Mitigazione futura: notifica push più aggressiva su cambio data (non solo badge "modificata" passivo).

3. **Discover come tab interno di Games può confondere utenti BGG-experienced.** Su BGG "Browse" e "Hot" sono nav primaria. Mitigazione: micro-onboarding sulla prima visita di /games che spiega i tab.

---

## Riferimenti

- **Gap report demo**: [`2026-06-04-claude-design-gap-report.md`](../audits/2026-06-04-claude-design-gap-report.md)
- **Mockup canonici**: `admin-mockups/design_files/sp4-dashboard.html`, `sp4-game-detail.html`, `sp4-sessions-index.html`, `sp4-session-skeleton-live.html`, `sp7-game-night-create.html`, `sp7-game-night-detail-rsvp.html`, `sp7-game-night-live.html`
- **Bounded context backend**: `apps/api/src/Api/BoundedContexts/SessionTracking/` + `BoundedContexts/GameManagement/Domain/GameNight*`
- **Mockup index**: [`admin-mockups/MOCKUPS_INDEX.md`](../../../admin-mockups/MOCKUPS_INDEX.md)

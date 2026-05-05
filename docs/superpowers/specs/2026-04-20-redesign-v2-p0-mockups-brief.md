# Claude Design — Brief P0 Mockup (Redesign V2 Consumer App)

> **Scopo**: commissionare a Claude Design le 7 schermate **P0** mancanti per completare la sostituzione dell'interfaccia consumer con il nuovo design system. Le schermate già esistenti in `admin-mockups/*.html` coprono dashboard, entity detail, drawer, ConnectionBar, play-records e toolkit hub. Questo brief copre i **gap bloccanti** identificati nella gap analysis del 2026-04-20.

**Target**: Claude Design (mockup HTML + Tailwind statici, no React)

**Output atteso per ogni schermata**:
- 1 file HTML self-contained in `admin-mockups/design_files/<slug>-mobile.html` (mobile-first, 390×844)
- 1 file HTML self-contained in `admin-mockups/design_files/<slug>-desktop.html` (desktop, 1440×900) dove richiesto
- CSS inline con token HSL (vedi sezione "Design Tokens")
- Screenshot PNG opzionale in stessa cartella

**Ordine di consegna consigliato**: 1 → 2 → 3 → 4 → 5 → 6 → 7 (funnel di acquisizione prima, esperienze in-game dopo).

---

## Design Tokens (invariante per tutti i mockup)

Usare esclusivamente questi token. Background globale dark, foreground chiaro.

```css
:root {
  /* Base */
  --color-bg: 222 18% 11%;
  --color-surface: 222 16% 14%;
  --color-surface-elevated: 222 14% 18%;
  --color-border: 222 12% 22%;
  --color-text: 210 20% 96%;
  --color-text-muted: 215 12% 65%;

  /* Entity palette (9 canonici) */
  --color-entity-game:     25 92% 58%;   /* 🎲 arancio */
  --color-entity-player:   265 85% 65%;  /* 👤 viola */
  --color-entity-session:  215 90% 62%;  /* 📋 blu */
  --color-entity-agent:    185 80% 50%;  /* 🤖 ciano */
  --color-entity-kb:       145 70% 48%;  /* 📚 verde */
  --color-entity-chat:     45 95% 55%;   /* 💬 giallo */
  --color-entity-event:    340 80% 60%;  /* 🎉 rosa */
  --color-entity-toolkit:  160 70% 45%;  /* 🧰 teal */
  --color-entity-tool:     195 75% 52%;  /* 🔧 azzurro */
}
```

**Regole colore**:
- Background schermata: `hsl(var(--color-bg))`
- Superfici card/drawer: `hsl(var(--color-surface))` o `--color-surface-elevated`
- Accento entity (pill, bordi focus, pip): `hsl(var(--color-entity-<type>))`
- Soft entity (background card hero): `hsl(var(--color-entity-<type>) / 0.12)`
- **Mai usare** colori marca arbitrari. Ogni accento deve appartenere alla palette entity.

**Tipografia**: Inter 400/500/600 (body), Quicksand 600/700 (heading). Import da Google Fonts nei mockup.

**Spacing**: 4px grid (0.25rem). Touch target minimo 44×44px.

**Radius**: `12px` card, `16px` drawer, `24px` bottom-sheet handle, `9999px` pill/chip.

---

## Schermata 1 — Login + Register

**Route**: `/login`, `/register`
**Slug file**: `auth-login`, `auth-register`

### Obiettivo
Primo contatto utente. Deve comunicare in 3 secondi: "questa è un'app AI-first per boardgamer", costruire fiducia, ridurre frizione all'ingresso.

### Layout Mobile (390×844)
- Hero verticale top ~40% viewport: logo MeepleAI + tagline "Il tuo assistente AI per i giochi da tavolo"
- Card superficie elevata centrata con radius 16px
- Form: email + password, bottone primario full-width
- Link secondari: "Password dimenticata?", "Non hai un account? Registrati"
- Divider `— oppure —` + bottoni OAuth (Google, Apple) con icone
- Footer micro-copy: link Privacy + Terms

### Layout Desktop (1440×900)
- Split-view 50/50: sinistra hero narrativa animata (3 mockup screens scroll verticale fake), destra form
- Form max-width 400px centrato verticalmente

### Uso colore entity
- **Nessuna entity primaria** — questa è pre-auth. Usare solo `--color-text` + `--color-text-muted`.
- CTA primaria: gradient `linear-gradient(135deg, hsl(var(--color-entity-game)), hsl(var(--color-entity-agent)))` per segnalare dualità "games + AI".

### Interazioni
- Focus ring 2px `hsl(var(--color-entity-agent))` su input
- Validazione inline sotto campo (testo rosso `340 80% 60%` per errori)
- Loading state bottone: spinner inline + testo "Accesso in corso…"

### Stati di sistema
| Stato | Comportamento |
|-------|--------------|
| Loading | Skeleton form non necessario (form è semplice), solo spinner su CTA |
| Empty | n/a |
| Error (credenziali) | Banner rosso soft sopra form: "Email o password errati" |
| Error (rete) | Toast bottom: "Impossibile contattare il server, riprova" |

### Register — variante
- Stessa struttura + campo `displayName`
- Checkbox accetta Privacy/Terms (obbligatorio)
- Dopo submit: redirect a `/setup-account`

### Criteri di accettazione
- [ ] Contrast ratio testo body ≥ 4.5:1
- [ ] Tap target ≥ 44px su mobile
- [ ] Form funzionale con soli elementi HTML (no JS richiesto per mockup)
- [ ] Gradient CTA visibile sia su dark che light (prevedere entrambi)

---

## Schermata 2 — Setup Account / Onboarding

**Route**: `/setup-account`, `/onboarding`
**Slug file**: `onboarding-wizard`

### Obiettivo
Portare l'utente da "appena registrato" a "ha una libreria popolata + conosce la chat AI" in < 2 minuti. Ridurre abbandono cold-start.

### Layout (mobile-first, desktop = mobile centrato max-width 480px)
Wizard 4 step con progress bar top:

**Step 1 — Display name + avatar**
- Campo display name (pre-popolato da OAuth se disponibile)
- Upload avatar (circle 96px) + preset 6 meeple emoji selezionabili
- Bottone "Continua" disabilitato finché nome valido

**Step 2 — Interessi (preferenze gioco)**
- Titolo: "Che giochi ti piacciono?"
- Grid 3 colonne di chip categoria: Strategia 🧠, Party 🎉, Famiglia 👨‍👩‍👧, Eurogame 🏛️, Ameritrash ⚔️, Cooperativi 🤝, Astratti ♟️, Narrativi 📖, Dadi 🎲
- Selezione multipla (min 1, max 5)
- Chip selezionato: background `hsl(var(--color-entity-game) / 0.2)` + border game

**Step 3 — Prima libreria (seed)**
- Titolo: "Aggiungi i tuoi primi giochi"
- Input ricerca BGG inline + lista 5 risultati con EntityCard `game` variant compact
- Bottone "+" aggiunge a owned
- Contatore "X giochi aggiunti" sticky bottom
- Skip link: "Aggiungerò dopo"

**Step 4 — Tour AI (edu)**
- Illustrazione MeepleBot + bubble chat finta
- 3 bullet: "Chiedi regole", "Analizza partite", "Scopri nuovi giochi"
- CTA finale: "Inizia" → redirect `/dashboard`

### Uso colore entity
- Step 1 (profilo): `--color-entity-player` accenti (border avatar, chip preset)
- Step 2 (giochi): `--color-entity-game`
- Step 3 (libreria): `--color-entity-game` (è libreria di giochi)
- Step 4 (AI): `--color-entity-agent`

Progress bar segue colore dello step corrente.

### Interazioni
- Swipe orizzontale tra step su mobile (mostrare gesture hint primo step)
- Bottone back top-left (escluso step 1)
- Tap skip salta tutti gli step rimanenti

### Stati di sistema
| Stato | Comportamento |
|-------|--------------|
| Loading (ricerca BGG) | Skeleton 3 righe sotto input |
| Empty (no match) | "Nessun gioco trovato. Prova un altro nome o aggiungi manualmente." |
| Error (upload avatar) | Toast "Immagine troppo grande (max 2MB)" |

### Criteri di accettazione
- [ ] Progress bar riflette step corrente con colore entity dinamico
- [ ] Skip sempre disponibile da step 2 in poi
- [ ] Mockup mostra almeno 2 step (step 1 e step 3) in file separati
- [ ] Allineamento con primitive `EntityChip` del piano M1 (usa stessi padding/radius)

---

## Schermata 3 — Library Hub Mobile

**Route**: `/library` (tabs: Owned / Wishlist / Playlists)
**Slug file**: `library-hub-mobile`

### Obiettivo
Entry point principale consumer mobile. Sostituisce la Library pilot del M2 con la versione completa a 3 tab + ricerca + filtri avanzati.

### Layout Mobile (390×844)
- Topbar sticky: titolo "Libreria" + icona search + icona filter
- Sotto topbar: 3 tab orizzontali con underline animato
  - 🎲 Giochi (owned count)
  - ⭐ Wishlist (count)
  - 📚 Playlist (count)
- Sotto tab: riga segmented filter pills (All / Recenti / Preferiti / Non giocati)
- Contenuto:
  - Tab Giochi: grid 2 colonne di EntityCard variant grid (thumbnail 1:1, title, players/duration badge)
  - Tab Wishlist: lista verticale EntityCard variant list (thumbnail 64×64, title, prezzo BGG, CTA "Cerca offerte")
  - Tab Playlist: grid 2 colonne playlist cover (gradient game-color + 4 thumb overlay)

### FAB
Bottone floating bottom-right, 56×56, background `hsl(var(--color-entity-game))`, icona `+`. Tap apre bottom-sheet "Aggiungi gioco" (ricerca BGG).

### Uso colore entity
- Tab attivo: underline + label in `--color-entity-game` (tab 1), accento neutral sui tab 2-3 (tutti relativi a giochi → colore game)
- CTA wishlist "Cerca offerte": pill `--color-entity-kb` (è info/shopping knowledge)
- Playlist cover: gradient `linear-gradient(135deg, hsl(var(--color-entity-game)), hsl(var(--color-entity-event)))`

### Interazioni
- Swipe orizzontale cambia tab
- Long-press card → context menu (Rimuovi, Sposta a wishlist, Crea playlist)
- Pull-to-refresh lista

### Stati di sistema
| Stato | Comportamento |
|-------|--------------|
| Loading | Skeleton grid 6 card (shimmer) |
| Empty Owned | Illustrazione + CTA "Aggiungi il tuo primo gioco" → apre search sheet |
| Empty Wishlist | "Aggiungi giochi alla wishlist tappando ⭐ su una scheda gioco" |
| Empty Playlist | "Crea la tua prima playlist per raggruppare giochi" + CTA |
| Error | Banner retry sticky top |

### Criteri di accettazione
- [ ] Mockup mostra tutti e 3 i tab (3 screen o 1 screen con zone separate)
- [ ] Screen empty-state per ogni tab
- [ ] FAB sempre visibile
- [ ] Filter pills scrollabili orizzontalmente se overflow
- [ ] Search icon apre full-screen search overlay (mostrare anche questo screen)

---

## Schermata 4 — Game Detail Desktop

**Route**: `/games/[id]` desktop (1024px+)
**Slug file**: `game-detail-desktop`

### Obiettivo
Vista ricca desktop che mostra contestualmente: metadata gioco, sessioni recenti, house rules, KB agent, chat preview. Sostituisce `GameDetailDesktop.tsx` con split-view enterprise-grade.

### Layout Desktop (1440×900)
Griglia 12-col:

**Header (col-span 12, 320px height)**
- Hero cover art fullwidth con overlay gradient dark
- Overlay: titolo game 48px bold + metadata chip row (👥 2-4 players, ⏱️ 60-90min, ⭐ 8.2/10)
- CTA bar bottom-right hero: "Inizia partita" (primary, game color) + "Chiedi all'agente" (secondary, agent color)

**Sidebar sinistra (col-span 3)**
- ConnectionBar verticale (già esistente nel codebase) con 6 pip: sessions, agents, kb, players, tools, events
- Sezione "House Rules" accordion con 3 card
- Sezione "Collezione" mini chip: owned/wishlist/wanted-to-play

**Main center (col-span 6)**
- Tab bar: Overview / Sessioni / Regole / Chat / Strategia
- Tab Overview: descrizione + designer + publisher + expansion list
- Tab Sessioni: lista ultime 10 sessioni con score + data + vincitore
- Tab Regole: EntityCard kb per ogni documento caricato + upload zone
- Tab Chat: embed mini-chat con agent del gioco
- Tab Strategia: placeholder analytics (tasso vittoria, giocatori frequenti)

**Sidebar destra (col-span 3)**
- Widget "Prossima partita" (event color) se esiste prenotata
- Widget "Statistiche personali" (player color): partite giocate, win rate, ultimo gioco
- Widget "Giochi simili" (game color): 3 EntityCard compact

### Uso colore entity
Questo schermo è il caso d'uso paradigmatico della palette 9-entity. Ogni sezione ha il suo accento:
- Header + CTA primaria + sezioni gioco → `--color-entity-game`
- Sezione sessioni → `--color-entity-session`
- Sezione KB/documenti → `--color-entity-kb`
- Chat embed → `--color-entity-chat`
- Agent CTA → `--color-entity-agent`
- Widget prossima partita → `--color-entity-event`
- Widget statistiche → `--color-entity-player`
- Tools (dadi, timer) → `--color-entity-tool`

### Interazioni
- Sticky header dopo scroll 200px (compresso, title + CTA)
- Tab change senza reload pagina (mockup: mostrare 2 varianti tab state)
- Hover card simili → lift + shadow game-color

### Stati di sistema
| Stato | Comportamento |
|-------|--------------|
| Loading | Skeleton hero + skeleton tabs + skeleton widgets |
| Empty Sessioni | "Nessuna partita registrata. Avvia la prima da qui" + CTA |
| Empty KB | "Carica il PDF del regolamento per attivare l'agent" + upload dropzone |
| Error | Toast non-blocking top-right |

### Criteri di accettazione
- [ ] Mockup mostra tab Overview + tab Sessioni (2 varianti)
- [ ] Tutti e 9 i colori entity presenti almeno una volta nello schermo
- [ ] ConnectionBar verticale allineata con pattern esistente (pip 8px colored con count badge)
- [ ] Responsive: mostrare anche breakpoint 1024px (sidebar destra collassata a bottom)

---

## Schermata 5 — Session LIVE Mode

**Route**: `/sessions/live/[id]/**`
**Slug file**: `session-live-mobile`, `session-live-desktop`

### Obiettivo
Modalità "in-game": utente è al tavolo, gioco in corso. UI deve essere **distraction-free**, leggibile in condizioni di luce variabile, one-hand operated. Sostituisce il flusso Game Night con esperienza live-first.

### Layout Mobile (390×844)
**Topbar compatto (48px)**
- Nome gioco + turno corrente (es. "Wingspan · Turno 4 di 7")
- Icona pausa centrale grande (48px, agent color)
- Icona fine sessione top-right

**Zone principali (tabs orizzontali fixed bottom, 4 tab)**
1. 🎯 **Punteggi** — tabella punteggi live, tap su giocatore → bottom-sheet edit
2. 🧰 **Toolkit** — grid 2×3 tool icons (dado, moneta, timer, contatore, random player, note)
3. 💬 **Chiedi** — chat con agent del gioco (vedi schermata 7)
4. 📝 **Note** — note libere + eventi notevoli

**Zona centrale (dinamica per tab)**
- Tab Punteggi: lista giocatori con score big numeric + pulsanti ± inline + avatar 40px
- Tab Toolkit: grid tool, tap apre tool in bottom-sheet parziale (50% height)
- Tab Chiedi: chat stream (stesso componente schermata 7)
- Tab Note: textarea + lista eventi con timestamp

**FAB centrale (emerge solo in tab Punteggi)**
- Bottone 64×64 sopra tabbar: "Avanza turno" (session color)

### Layout Desktop (1440×900)
Split 3-col:
- Left 25%: lista giocatori + score (sempre visibile)
- Center 50%: zona attiva (punteggi editor / toolkit / note)
- Right 25%: chat agent sempre aperta

### Uso colore entity
- Topbar background `hsl(var(--color-entity-session) / 0.15)` + border bottom session
- FAB "Avanza turno" → `--color-entity-session`
- Tab attivo underline → colore dell'entity associata (toolkit=toolkit-color, chiedi=chat-color, ecc.)
- Tool icons: ciascuno con entity tool color
- Alert "Ultimo turno!" → `--color-entity-event` background soft

### Interazioni
- Blocco rotazione schermo (prefer portrait mobile)
- Wake lock (JS opzionale, menzionare in brief ma non in mockup)
- Gesture swipe tab
- Long-press su giocatore → quick actions (reset score, segna vincitore, nota)

### Stati di sistema
| Stato | Comportamento |
|-------|--------------|
| Loading iniziale | Splash con nome gioco + spinner |
| Paused | Overlay semi-trasparente + big "IN PAUSA" + CTA Riprendi |
| Error sync | Banner top "Offline — score salvati localmente" (session color soft) |
| Fine sessione | Transizione a schermata riassunto con podio vincitori |

### Criteri di accettazione
- [ ] Mobile: 4 mockup, uno per ogni tab
- [ ] Desktop: 1 mockup split-view
- [ ] Stato "paused overlay" come screen separato
- [ ] Stato "fine sessione riassunto" come screen separato (mostra podio top-3 con player color accenti)
- [ ] Tabbar bottom sempre ≥ 56px high per pollice-friendly

---

## Schermata 6 — Game Night Hub + Detail

**Route**: `/game-nights`, `/game-nights/[id]`
**Slug file**: `game-night-hub`, `game-night-detail`

### Obiettivo
Serata multi-game è il pattern di uso più evoluto dell'app (5 giochi, 4 ore, N giocatori). Hub list + detail con diario cronologico.

### Hub — Layout Mobile (390×844)
- Topbar: "Serate" + icona calendario
- Segmented: Prossime / In corso / Passate
- Lista card "game night" (EntityCard event variant custom):
  - Thumbnail 4×1 strip con cover dei giochi pianificati
  - Titolo serata + data + ora
  - Chip players (avatar stack 5 max + "+N")
  - Status pill: Pianificata / LIVE (pulse verde) / Conclusa (grey)
  - Swipe left → Archivia / Elimina

**FAB**: "+ Nuova serata" → wizard modal (giorno, ora, luogo, giochi, invita)

### Detail — Layout Mobile (390×844)
**Header (220px)**
- Gradient event color + testo bianco
- Titolo serata, data, luogo
- Avatar stack giocatori (tap → lista completa)
- Status badge top-right
- Se LIVE: bottone "Entra in modalità LIVE" (FAB grande session color)

**Body a 3 sezioni**

1. **Giochi pianificati** — lista 1-5 EntityCard game variant horizontal con:
   - Ordine playlist (1. / 2. / 3.)
   - Status: non giocato / in corso / completato
   - Durata stimata vs effettiva
   - Vincitore (se concluso)

2. **Diario eventi** — timeline verticale con:
   - Timestamp badge
   - Evento (es. "🎲 Wingspan iniziato", "🏆 Marco vince Wingspan con 72pt", "🎉 Game Night conclusa")
   - Colore pip entity per tipo evento (game/session/event)

3. **Riepilogo finale** (solo se conclusa)
   - Card "Giocatore della serata"
   - Statistiche: giochi giocati, durata totale, vincitori per gioco

### Uso colore entity
- Header hub + CTA nuova serata → `--color-entity-event`
- Card game night: border left 4px event color
- LIVE pulse indicator: `--color-entity-session` con animazione 2s pulse
- Diario entry game-related: pip `--color-entity-game`
- Diario entry session-related: pip `--color-entity-session`
- Card "Giocatore della serata": gradient player color

### Interazioni
- Tap card hub → navigazione detail
- Nel detail, tap su gioco non-ancora-giocato → avvia sessione (vai a schermata 5)
- Tap su gioco completato → mostra riepilogo sessione bottom-sheet
- Pull-to-refresh diario (solo se LIVE)

### Stati di sistema
| Stato | Comportamento |
|-------|--------------|
| Loading hub | Skeleton 3 card |
| Empty hub | Illustrazione + "Organizza la tua prima serata" + CTA |
| Empty detail diario | "Il diario si popolerà durante la serata" |
| Error | Retry banner |

### Criteri di accettazione
- [ ] Hub mostra 3 stati (prossime/in-corso/passate) come segmented
- [ ] Detail mostra 2 varianti: serata pianificata (no diario) + serata conclusa (diario completo + riepilogo)
- [ ] Timeline diario alternata sx/dx opzionale, altrimenti verticale singola
- [ ] Status LIVE con pulse animato CSS `@keyframes`

---

## Schermata 7 — Chat Mobile In-Session

**Route**: `/chat/[threadId]` (mobile + embedded in session LIVE tab "Chiedi")
**Slug file**: `chat-mobile`

### Obiettivo
Chat AI deve sentirsi "conversation with a knowledgeable friend", non "data entry form". Sostituisce il `chatDesktop.png` statico con versione mobile responsive e interattiva.

### Layout Mobile (390×844)
**Topbar (56px)**
- Back button
- Avatar agent (40px) + nome agent (es. "Wingspan Agent")
- Status indicator: online dot + "Pronto" / thinking animated dots / "Errore"
- Menu top-right (kebab): Nuova chat, Esporta, Impostazioni

**Zona messaggi (scroll)**
- Messaggi utente: bubble destra, background `hsl(var(--color-entity-player) / 0.15)`, border-radius 16px (smussato solo top-right)
- Messaggi agent: bubble sinistra, background `hsl(var(--color-surface-elevated))`, border-left 3px `--color-entity-agent`
- Timestamp sotto bubble (solo se gap > 5min)
- Citazioni KB inline: chip piccoli `hsl(var(--color-entity-kb) / 0.2)` + icona 📚 + testo "Regolamento p.12"
- Suggested replies (3 chip sotto ultimo messaggio agent): tap invia direttamente

**Streaming SSE visual**
- Cursor lampeggiante blink durante stream
- Auto-scroll bottom se utente già in bottom
- Stop button compare durante streaming (pill rossa floating)

**Input bar (sticky bottom, 64px)**
- TextArea auto-grow max 4 righe
- Icona attach (KB reference picker)
- Icona voice (nice-to-have, mostrare disabled)
- Send button: icona paper-plane, background `--color-entity-chat` quando text non vuoto

**Quick actions bar (sopra input, scrollabile horizontal)**
- Chip pre-composti contestuali: "Regole base", "Setup", "Turno mio", "Conteggio punti", "Caso specifico"

### Layout Desktop (embedded)
- Stesso layout in colonna 400px max-width
- Quando full-page: split 30% sidebar threads + 70% chat

### Uso colore entity
- Topbar agent avatar ring → `--color-entity-agent`
- Bubble agent border-left → `--color-entity-agent`
- Bubble user → `--color-entity-player`
- Send button attivo → `--color-entity-chat`
- Citazioni KB → `--color-entity-kb`
- Suggested replies → `--color-entity-chat` soft
- Thinking dots → `--color-entity-agent`

### Interazioni
- Long-press su messaggio → copy / reply / feedback 👍👎
- Tap su citazione KB → apre drawer con pagina PDF relativa (kb color)
- Swipe down su topbar → minimizza chat (modalità PIP)
- Pull-to-refresh carica messaggi precedenti

### Stati di sistema
| Stato | Comportamento |
|-------|--------------|
| Loading chat history | Skeleton 3 bubble alternate |
| Empty thread | Welcome message agent: "Ciao! Chiedimi qualsiasi cosa su Wingspan 🦅" + suggested replies |
| Thinking (streaming) | Thinking dots 3 animati in bubble agent empty |
| Error invio | Retry button inline sotto messaggio utente rosso |
| Offline | Banner top "Offline — i messaggi verranno inviati alla riconnessione" |
| Rate limit | Bubble system "Stai andando troppo veloce, riprova tra 30s" |

### Criteri di accettazione
- [ ] Mockup mostra almeno 4 messaggi scambiati (2 user + 2 agent)
- [ ] Almeno 1 messaggio agent con citazione KB inline
- [ ] Stato "thinking" come variante separata
- [ ] Stato "empty thread" come variante separata
- [ ] Input bar con quick actions sopra
- [ ] Suggested replies post-risposta agent
- [ ] Accessibile: bubble con role="log" aria-live="polite"

---

## Specifiche tecniche trasversali

### Responsive breakpoints
- Mobile: 360-767px (design su 390)
- Tablet: 768-1023px (non richiesto per P0, eventualmente adattamento)
- Desktop: 1024px+ (design su 1440)

### Accessibilità
- WCAG AA baseline: contrast 4.5:1 body, 3:1 large text (18px+)
- Focus visibile con ring 2px entity-color
- Tap targets 44×44px minimo su mobile
- Role semantici (nav, main, article, dialog) nei mockup HTML
- No informazione veicolata solo da colore (sempre testo + icona)

### Motion
- Durata default: 200ms ease-out
- Durata drawer: 300ms cubic-bezier(0.32, 0.72, 0, 1) (spring-like)
- Rispettare `prefers-reduced-motion: reduce` → disabilitare animazioni decorative

### Dark mode
- Design principale è **dark**. Light mode non richiesto per P0.
- Prevedere token light solo se banale invertire (schermata 1 beneficia da light alt).

### Placeholder contenuto
- Usare dati realistici italiani: nomi giochi BGG veri (Wingspan, Everdell, Scythe, Root, Azul)
- Nomi giocatori: Marco, Giulia, Luca, Sofia, Alessandro
- Date formato italiano: "sab 26 apr 2026 · 20:30"
- Valuta: EUR

### Asset
- Icone: Lucide React (usare SVG inline nei mockup)
- Illustrazioni vuote: stile friendly/playful, meeple protagonisti (richiesto a Claude Design)
- Thumbnail giochi: fake URL `https://cf.geekdo-images.com/...` (BGG mock)

---

## Deliverables finali

Per ogni schermata:
```
admin-mockups/design_files/
├── 01-auth-login-mobile.html
├── 01-auth-login-desktop.html
├── 01-auth-register-mobile.html
├── 02-onboarding-step1.html
├── 02-onboarding-step3.html
├── 03-library-hub-mobile.html
├── 03-library-hub-mobile-empty.html
├── 03-library-hub-mobile-search-overlay.html
├── 04-game-detail-desktop-overview.html
├── 04-game-detail-desktop-sessions.html
├── 05-session-live-mobile-scores.html
├── 05-session-live-mobile-toolkit.html
├── 05-session-live-mobile-ask.html
├── 05-session-live-mobile-notes.html
├── 05-session-live-desktop.html
├── 05-session-live-paused.html
├── 05-session-live-summary.html
├── 06-game-night-hub.html
├── 06-game-night-detail-planned.html
├── 06-game-night-detail-completed.html
├── 07-chat-mobile-active.html
├── 07-chat-mobile-thinking.html
└── 07-chat-mobile-empty.html
```

**Formato**: HTML5 self-contained (CSS inline o `<style>` in head, no build step), Tailwind CDN accettato per velocità. Fonts da Google Fonts CDN.

**Consegna**: commit su branch `design/p0-mockups` da `main-dev`, PR verso `main-dev` con screenshot inline per ogni mockup.

---

## Allineamento con implementation plan

I primitive del piano M1 (`EntityChip`, `EntityPip`, `EntityCard`, `Drawer`) devono essere estraibili da questi mockup. In fase di implementazione React, ogni mockup viene decomposto in:

- **Componenti condivisi** → `apps/web/src/components/ui/v2/`
- **Componenti feature-specific** → `apps/web/src/components/<feature>/v2/`
- **Pagine** → `apps/web/src/app/(authenticated)/<route>/page.tsx`

**Mapping atteso** (post-consegna):

| Mockup | Primitives usati | Feature components | Route |
|--------|-----------------|-------------------|-------|
| 1 Login | — (pre-auth) | `LoginForm`, `OAuthButtons` | `/login`, `/register` |
| 2 Onboarding | `EntityChip`, `EntityCard` | `OnboardingWizard` | `/setup-account` |
| 3 Library | `EntityCard`, `EntityChip`, `Drawer` | `LibraryFilterTabs`, `LibrarySearchOverlay` | `/library` |
| 4 Game detail | Tutti + `ConnectionBar` esistente | `GameDetailTabs`, `GameSidebarWidgets` | `/games/[id]` |
| 5 Session live | `EntityChip`, `Drawer`, `EntityPip` | `LiveScoreEditor`, `LiveToolkit`, `LiveDiary` | `/sessions/live/[id]` |
| 6 Game night | `EntityCard`, `EntityPip` | `GameNightTimeline`, `GameNightWizard` | `/game-nights` |
| 7 Chat | `EntityChip`, `EntityPip` (per KB citations) | `ChatBubble`, `ChatInput`, `SuggestedReplies` | `/chat/[threadId]` |

---

## Prossimi step

1. **Brief approval**: user review questo documento
2. **Delega a Claude Design**: commissione 7 schermate in 2-3 batch
3. **Review mockup**: per ogni consegna, validare coerenza token + UX
4. **Implementation planning**: ogni mockup approvato genera un piano TDD successore al `2026-04-20-redesign-v2-library-pilot-plan.md`
5. **M3-M8 roadmap**: definire sequenza rilascio (ipotesi: Login → Onboarding → Library full → Game detail → Chat → Session → Game Night)

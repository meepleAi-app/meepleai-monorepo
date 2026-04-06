# Design Spec: Navbar, FAB, Dashboard Bento & Empty States

**Data**: 2026-04-06
**Scope**: `apps/web/src/`
**Branch base**: `main-dev`
**Stato**: Approvato — pronto per implementazione

---

## Contesto e Motivazione

Il frontend MeepleAI usa `AppNavbar` come barra di navigazione principale. Tuttavia:
- Il pattern **FloatingActionPill** presente nel prototipo `v0app` non è stato portato nell'app reale
- La **dashboard** definisce 6 widget bento ma non ne renderizza nessuno — usa colori hardcoded e font di sistema invece dei token CSS del sito
- Il **nuovo utente** vede una dashboard completamente vuota senza orientamento
- Alcune pagine (Library, Chat) mancano di azioni primarie flottatanti

---

## Decisioni di Design

### 1. Navbar — Solo `AppNavbar`

**Decisione**: Mantenere solo `AppNavbar`. `HybridSidebar` risulta non referenziato in `UserShellClient.tsx` — confermarne l'assenza e non aggiungerne uno nuovo.

**Miglioramenti richiesti ad `AppNavbar`**:
- Aggiungere **dropdown avatar** con: Profilo, Impostazioni, Logout
- Il dropdown si apre al click sull'avatar (già presente ma senza `onClick`)
- Stile dropdown: `bg-card border border-border rounded-xl shadow-xl`, voci con icone Lucide

**Nav links**: mantenere i 4 esistenti (Dashboard, Libreria, Sessioni, AI Chat). Non aggiungere altri al momento per non sovraffollare.

---

### 2. FloatingActionPill — Pattern A (v0app completo)

**Decisione**: Ripristinare il pattern FAB dal prototipo v0app, variante **A** (pill desktop + action bar mobile).

**Specifiche componente `FloatingActionPill`** (nuovo file `components/layout/FloatingActionPill.tsx`):

```
Desktop (lg+):
  position: fixed bottom-6 left-1/2 -translate-x-1/2
  style: bg-[rgba(30,41,59,0.85)] backdrop-blur-md border border-white/10
         rounded-[40px] shadow-[0_8px_32px_rgba(0,0,0,0.4)]
  contenuto: [label contestuale] | [CTA primaria] | [icone secondarie]

Mobile (< lg):
  Bottom action bar: fixed bottom-0 h-16 w-full
    bg-[rgba(15,23,42,0.95)] backdrop-blur-md border-t border-white/8
    contenuto: icone azioni distribuite orizzontalmente
  FAB circle: fixed bottom-20 right-4
    w-14 h-14 rounded-full bg-[hsl(var(--e-game))]
    shadow-[0_4px_16px_hsl(var(--e-game)/0.4)]
    azione primaria della pagina
```

**Varianti per pagina** (`props.page`):

| Pagina | Label | CTA primaria | Azioni secondarie |
|--------|-------|-------------|-------------------|
| `dashboard` | 🏠 Dashboard | + Aggiungi gioco | 🔍 Cerca |
| `library` | 📚 La mia libreria | + Aggiungi | 🔍 Cerca · ↕ Ordina · ⊞ Vista |
| `sessions` | 🎯 Sessioni | ▶ Nuova sessione | 🔍 Cerca · ↕ Ordina |
| `chat` | 💬 Chat AI | + Nuova chat | — |

Il componente riceve `page` come prop e seleziona le azioni dal mapping interno. Le azioni secondarie eseguono callback passate come props (`onSearch`, `onSort`, `onToggleView`).

---

### 3. Dashboard Bento — Render completo con token CSS

**Decisione**: Attivare tutti e 6 i widget bento. Eliminare il codice morto (widget definiti ma non renderizzati). Usare esclusivamente token CSS e font Tailwind del sito.

**Layout bento** (12 colonne, row = 48px):

```
[LiveSession      8×2] [KPI Stats      4×2]
[Library          6×4] [Chat           6×4]
[Leaderboard      6×3] [Trending       6×3]
```

**Token da usare** (mai colori hardcoded):

| Widget | Accent color |
|--------|-------------|
| LiveSession | `hsl(var(--e-session))` indigo |
| KPI Stats | `hsl(var(--e-game))` orange |
| Library | `hsl(var(--e-game))` orange |
| Chat | `hsl(var(--e-chat))` blue |
| Leaderboard | `hsl(var(--e-player))` purple |
| Trending | `hsl(var(--e-game))` orange |

**Font**:
- Titoli widget, valori numerici: `font-quicksand font-bold` / `font-extrabold`
- Label, testo corpo: `font-nunito`

**Rimozione dead code**: Le funzioni widget attualmente definite ma non usate in `dashboard-client.tsx` (LiveSessionWidget, KpiWidget, etc.) vengono sostituite dai componenti inline dentro il render. Non serve estrarle in file separati a questo punto.

---

### 4. Dashboard Empty State — Welcome Hero (Opzione A)

**Condizione di attivazione**: `userStats.totalGames === 0 && userStats.totalSessions === 0`

Quando vera, la dashboard mostra la **"Welcome Mode"**:

**Layout Welcome Mode**:
```
[Welcome Hero — full width 12×3]
[Library empty CTA  6×4] [Trending (dati reali)  6×4]
[Chat empty CTA     6×3] [Sessions empty CTA     6×3]
```

**Welcome Hero** (sostituisce LiveSession + KPI nella prima riga):
- Avatar utente iniziale + nome (`Benvenuto in MeepleAI, {firstName}! 👋`)
- Sottotitolo: "Il tuo assistente AI per giochi da tavolo. Inizia aggiungendo i giochi che possiedi."
- 3 action chip: `➕ Aggiungi un gioco` (primary, `bg-[hsl(var(--e-game))]`), `🔍 Sfoglia catalogo` (outline), `🤖 Prova l'AI Chat` (outline)
- Background: `linear-gradient(135deg, hsl(var(--e-game)/0.15), hsl(var(--e-session)/0.08))`
- Border: `border-[hsl(var(--e-game)/0.3)]`

**Widget Trending**: sempre popolato con dati del catalogo community — non dipende da dati utente. È l'unico widget "reale" visibile al primo accesso.

**Widget Library, Chat, Sessions in empty mode**: empty state con icona + testo + CTA colorata coerente col colore entità.

**Transizione automatica**: quando `totalGames > 0` il Welcome Hero scompare e appare la dashboard piena. Nessun dismiss manuale richiesto.

**StatsRow con dati zero**: mostrare `—` al posto di `0` per i campi vuoti (Library, Sessions, Chat). Il campo Piano rimane sempre visibile con il tier attivo.

---

## Architettura Componenti

### File nuovi

```
apps/web/src/components/layout/FloatingActionPill.tsx
  — FAB contestuale con varianti per pagina
  — Esportato come default, usato in ogni page.tsx

apps/web/src/components/dashboard/WelcomeHero.tsx
  — Banner primo accesso, riceve firstName e callbacks
  — Renderizzato condizionalmente in DashboardClient

apps/web/src/components/dashboard/widgets/
  LiveSessionWidget.tsx
  KpiStatsWidget.tsx
  LibraryWidget.tsx
  ChatWidget.tsx
  LeaderboardWidget.tsx
  TrendingWidget.tsx
  — Ogni widget: componente autonomo, props tipizzate
```

### File modificati

```
apps/web/src/components/layout/AppNavbar.tsx
  — Aggiunto Avatar dropdown (Profilo, Impostazioni, Logout)

apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx
  — Rimozione dead code widget non renderizzati
  — Aggiunta logica welcome mode (totalGames===0 && totalSessions===0)
  — Render bento con 6 widget componenti
  — Aggiunto FloatingActionPill page="dashboard"

apps/web/src/app/(authenticated)/library/_content.tsx
  — Aggiunto FloatingActionPill page="library"

apps/web/src/app/(authenticated)/sessions/_content.tsx
  — FAB esistente rimosso, sostituito con FloatingActionPill page="sessions"

apps/web/src/app/(chat)/chat/page.tsx
  — Aggiunto FloatingActionPill page="chat"
```

---

## Comportamento Mobile

Il `FloatingActionPill` su mobile mostra:
- **Bottom action bar** (64px, fissa in basso): icone azioni secondarie
- **FAB circle** (`bottom-20 right-4`): azione primaria della pagina

Questo si sovrappone correttamente alla `bottom-nav` del mobile (che non esiste — `AppNavbar` è solo top bar).

Il `z-index` del FAB deve essere `z-50` per stare sopra il contenuto ma sotto i modal/drawer.

---

## Dati Richiesti dai Widget

| Widget | API call necessaria | Note |
|--------|-------------------|------|
| LiveSession | `GET /api/v1/sessions?status=active&limit=1` | Mostra empty se null |
| KpiStats | `GET /api/v1/users/me/stats` | Aggregato: giochi, sessioni, ore |
| Library | `GET /api/v1/library?limit=4` | Preview 4 giochi recenti |
| Chat | `GET /api/v1/chat-sessions?limit=3` | Preview 3 chat recenti |
| Leaderboard | `GET /api/v1/sessions/leaderboard?limit=5` | Top 5 giocatori |
| Trending | `GET /api/v1/games/trending?limit=6` | Catalogo community, sempre disponibile |

Le query esistenti (`useRecentChatSessions`, `useChatSessionLimit`) sono già disponibili. Per gli altri widget si usano i hook React Query già presenti nel codebase o se ne creano di nuovi seguendo il pattern `hooks/queries/`.

---

## Non in Scope

- Cambio routing o struttura URL
- Sidebar laterale di navigazione
- Refactoring componenti non toccati
- Nuove pagine
- Backend — tutti gli endpoint necessari esistono già

---

## Criteri di Accettazione

- [ ] `AppNavbar` mostra dropdown avatar funzionante con logout
- [ ] `FloatingActionPill` visibile su Dashboard, Library, Sessions, Chat
- [ ] Desktop: pill centrata con backdrop-blur; Mobile: action bar + FAB circle
- [ ] Dashboard mostra tutti e 6 i widget con colori da token CSS e font Quicksand/Nunito
- [ ] Nuovo utente (0 giochi, 0 sessioni) vede Welcome Hero + Trending
- [ ] Utente con dati vede dashboard piena senza Welcome Hero
- [ ] Nessun colore hardcoded (`#111827`, `hsl(25,95%,45%)` ecc.) nei file modificati
- [ ] `pnpm typecheck` e `pnpm lint` passano senza errori

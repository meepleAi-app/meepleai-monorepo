# Design Spec: Migrazione UI apps/web → v0app

**Data**: 2026-04-06
**Autore**: brainstorming session
**Stato**: Approvato dall'utente

---

## Obiettivo

Sostituire il layout e lo stile grafico di `apps/web` con quello del prototipo `v0app`, mantenendo intatta tutta la logica applicativa (autenticazione, API, stores Zustand, React Query, routing).

Il risultato finale deve essere visivamente identico a `v0app`, con tutte le pagine che usano `MeepleCard`.

---

## Architettura — Shell di navigazione

### Prima (UserShellClient)
```
UserShellClient
├─ UserTopNav (52px)
├─ HybridSidebar (52px→220px collassabile)
├─ main (lg:ml-[52px])
│  └─ DashboardEngineProvider → {children}
├─ UserTabBar (mobile)
└─ BackToSessionFAB
```

### Dopo (AppShell)
```
AppShell
├─ Navbar (52px, sticky, glassmorphism)
│  ├─ Logo esagonale + "MeepleAI"
│  ├─ nav: Dashboard · Libreria · Sessioni · AI Chat
│  └─ Search + Avatar + menu mobile
├─ main (no ml offset)
│  └─ {children}
└─ BackToSessionFAB (mantenuto)
```

**Componenti rimossi dalla navigazione**: `HybridSidebar`, `UserTopNav`, `UserTabBar`
**Componenti nascosti dal nav** (route accessibili via URL diretto): Players, Agents, Records, Settings
**Componenti mantenuti**: `BackToSessionFAB`, `FloatingActionPill` (già esistente)

---

## Mapping pagine — 5 step

| Step | File apps/web | Modello v0app | Cambiamento chiave |
|------|---------------|---------------|-------------------|
| 1 | `layout/UserShellClient.tsx` | `navbar.tsx` | Sidebar → Navbar orizzontale |
| 2 | `dashboard/dashboard-client.tsx` | `libreria/page.tsx` | Bento grid → stats row + MeepleCard grid |
| 3 | `library/page.tsx` + `[id]/page.tsx` | `libreria/` + `page.tsx` | Grid MeepleCard + GameHeader + tabs |
| 4 | `sessions/[id]/page.tsx` | `sessioni/[id]/page.tsx` | ScoreTracker + header sessione |
| 5 | `chat/page.tsx` | `ai/page.tsx` | ChatSidebar + ChatMainArea |

---

## Design delle pagine

### Navbar (Step 1)
- Height: 52px, sticky top-0, z-50
- Background: `hsl(var(--card))` con `backdrop-blur-md` e `border-b`
- Contenuto (left→right):
  - Logo hex (28px, gradient arancione→viola) + "MeepleAI" (font-bold)
  - NavLinks: Dashboard | Libreria | Sessioni | AI Chat (active state: `bg-secondary rounded-md`)
  - Spacer flex-1
  - SearchButton (icon) + AvatarButton (28px circle)
  - Mobile: hamburger → drawer con gli stessi link
- Route attive nel nav: `/dashboard`, `/library`, `/sessions`, `/chat`
- Route nascoste (non nel nav): `/players`, `/agents`, `/records`, `/settings`, `/admin`

### Dashboard (Step 2) — `/dashboard`
- **Stats row**: 4 card KPI con `border-left: 3px solid var(--e-*)` (entity-colored)
  - Giochi in libreria (`--e-game` orange)
  - Sessioni totali (`--e-session` verde)
  - Ore di gioco (`--e-agent` viola)
  - Agenti AI (`--e-chat` blue)
- **Grid MeepleCard**: 4 colonne desktop, 2 tablet, 1 mobile — ultimi giochi aggiunti/giocati
  - Componente: `MeepleCard entity="game" variant="grid"`
- **FloatingActionPill**: posizionato bottom-center

### Libreria (Step 3a) — `/library`
- **Header**: titolo "La mia libreria" + badge count + toggle grid/list
- **FilterBar**: chip pills orizzontali
  - Filtri stato: Tutti | Giocati | Wishlist | Non giocati
  - Sort: A-Z | Più giocati | Recenti
- **Grid MeepleCard**: 5 col desktop, 3 tablet, 2 mobile
  - `MeepleCard entity="game" variant="grid"`
  - border-left con `--e-game` (orange)

### Dettaglio Gioco (Step 3b) — `/library/[id]`
- **GameHeader**: cover 120px + titolo/publisher/anno + rating badge + BGG link
- **MiniNav tab**: Info | Documenti | Sessioni | Note
- **Tab Info**: descrizione, meccaniche, specs (giocatori, durata, età)
- **Tab Documenti**: lista PDF caricati, upload button
- **Tab Sessioni**: lista sessioni per quel gioco (MeepleCard entity="session")
- **FloatingActionPill**: "Nuova sessione" + "AI assist"

### Sessione (Step 4) — `/sessions/[id]`
- **Header**: nome gioco + data + stato sessione
- **ScoreTracker**: grid giocatori
  - Per ogni giocatore: avatar + nome + punteggio corrente
  - Controlli: pulsante `+` e `-` punteggio (con step configurabile)
  - Round counter
- **Action bar** (mobile/fixed bottom): Round | Timer | Undo | Fine sessione
- **FloatingActionPill**: AI chat contestuale (apre panel overlay)

### AI Chat (Step 5) — `/chat`
- **Layout split**: sidebar 232px | area principale flex-1
- **Sidebar**: lista conversazioni con titolo, preview, timestamp; button "Nuova chat"
- **Area principale**:
  - Header: titolo conversazione corrente
  - Messaggi: scroll, bubbles user/assistant differenziate
  - Input: textarea + attachment icon + send button
- **Empty state**: icona + "Inizia una conversazione con l'AI"

---

## Design system — token

I token CSS esistenti in `globals.css` sono già stati aggiornati nella sessione precedente per corrispondere a v0app (palette slate-blue, dark-by-default). Non sono necessarie ulteriori modifiche ai token.

```css
/* Già impostato */
--background: 222 47% 12%;   /* #0f172a */
--card: 215 28% 18%;         /* #1e293b */
--secondary: 215 19% 27%;    /* #334155 */
--primary: 25 95% 38%;       /* orange */
/* Entity colors già presenti: --e-game, --e-player, --e-session, etc. */
```

**ThemeProvider**: già configurato `defaultTheme="dark"` + `enableSystem={false}`.

---

## Componenti

### Nuovi componenti da creare
| Componente | Path | Descrizione |
|------------|------|-------------|
| `Navbar` | `components/layout/Navbar.tsx` | Navbar orizzontale 52px |
| `AppShell` | `components/layout/AppShell.tsx` | Shell wrapper che sostituisce UserShellClient |
| `StatsRow` | `components/dashboard/StatsRow.tsx` | Row 4 KPI card con entity colors |
| `FilterBar` | `components/library/FilterBar.tsx` | Chip pills filter + sort |
| `GameHeader` | `components/game/GameHeader.tsx` | Header pagina dettaglio gioco |
| `ScoreTracker` | `components/sessions/ScoreTracker.tsx` | Grid punteggi sessione |
| `ChatSidebar` | `components/chat/ChatSidebar.tsx` | Sidebar lista conversazioni |

### Componenti esistenti riutilizzati (invariati)
- `MeepleCard` — usato in tutte le pagine
- `FloatingActionPill` — mantenuto as-is
- `BackToSessionFAB` — mantenuto as-is
- Tutti gli store Zustand, React Query hooks, auth context

### Componenti rimossi (non più usati dopo migrazione)
- `HybridSidebar`
- `UserTopNav`
- `UserTabBar`

---

## Approccio di migrazione

**Approccio A — Layer-by-layer** (scelto dall'utente):
1. Ogni step è una PR separata e testabile
2. L'API reale rimane sempre funzionante
3. Rollback granulare per step

**Ordine di implementazione**:
1. AppShell + Navbar (sblocca tutti gli altri step)
2. Dashboard
3. Library + Game Detail
4. Sessions
5. Chat

---

## Invariato

- Autenticazione e middleware auth
- Tutte le route esistenti (accessibili via URL, solo nascoste dal nav)
- API calls, React Query hooks
- Zustand stores
- ThemeProvider (già aggiornato)
- CSS token palette (già aggiornata)
- ErrorBoundary, SessionWarningModal
- Componente MeepleCard (riutilizzato, non modificato)

---

## Criteri di completamento

- [ ] Navbar sostituisce HybridSidebar + UserTopNav + UserTabBar
- [ ] Dashboard mostra stats row + MeepleCard grid (no bento widget)
- [ ] Library mostra FilterBar + MeepleCard grid 5 col
- [ ] Game detail mostra GameHeader + MiniNav tabs
- [ ] Sessions mostra ScoreTracker
- [ ] Chat mostra layout split sidebar/main
- [ ] Tutte le pagine usano `MeepleCard` per le entity card
- [ ] Nessuna regressione su auth flow
- [ ] Build TypeScript senza errori

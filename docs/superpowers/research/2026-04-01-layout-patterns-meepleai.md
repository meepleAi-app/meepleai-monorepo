# Layout Patterns di Successo per MeepleAI

> **Data**: 2026-04-01
> **Scopo**: Raccomandazioni UX/UI basate su ricerca competitiva per l'app MeepleAI
> **Confidenza**: 🟡 Media-Alta (ricerca su fonti pubbliche, nessun accesso diretto ai codebase)

---

## Executive Summary

MeepleAI combina tre macro-domini UI con esigenze specifiche:

1. **Catalogo & Libreria** — scoperta, gestione collezione, wishlist
2. **AI Chat / RAG** — conversazione contestuale con documenti PDF (rulebook)
3. **Session Tracking & Dashboard** — statistiche di gioco, sessioni, score

L'analisi di BGG, Steam, Letterboxd, BG Stats, Perplexity, e trend SaaS 2025 rivela pattern consistenti che si traducono in **12 raccomandazioni concrete** per MeepleAI.

---

## 1. Insight per Dominio

### 1.1 Catalogo & Scoperta Giochi (BGG → Steam → Letterboxd)

**Cosa funziona nei competitor:**

| App | Pattern Vincente |
|-----|-----------------|
| **BGG** | Community-driven content, filtri profondi per meccaniche/categorie, rating prominente |
| **Steam** | Shelf dinamici personalizzabili, copertine grandi, filtri per tag, "recently played" sempre visibile |
| **Letterboxd** | Grid di copertine come navigazione principale, social feed integrato, liste curate, "For You" / "Friends" / "News" tabs |
| **BG Stats** | Deep analytics, BGG sync, chart non opprimenti, 300k+ utenti |

**Pattern da adottare:**
- **Grid di copertine** come modalità principale di browsing (non lista)
- **Card con metadati sintetici**: titolo, publisher, rating, player count, durata
- **Filtri contestuali** (meccanica, categoria, BGG rank, n. giocatori)
- **Shelf personalizzabili** (in stile Steam): "La mia libreria", "Wishlist", "Giocati di recente"
- **Tab social**: "Per te" | "Amici" | "Community" (pattern Letterboxd)

---

### 1.2 AI Chat con RAG e Knowledge Base (Perplexity → ChatGPT → Open WebUI)

**Cosa funziona nei competitor:**

| App | Pattern Vincente |
|-----|-----------------|
| **Perplexity** | Fonti visibili subito, risposta + citazioni inline, interfaccia focalizzata sull'informazione |
| **ChatGPT** | Sidebar con storico conversazioni, input in basso, AI response come "card" non bubble |
| **Open WebUI** | Sidebar documenti con filename + tipo + chunk count, empty state esplicito per upload |
| **NotebookLM** | Panel dedicato ai documenti sorgente, citazioni cliccabili |

**Pattern da adottare:**
- **Layout a 3 zone**: sidebar sinistra (storico/documenti) | area chat centrale | panel destra opzionale (fonti/preview)
- **Input centrato** in basso, grande, con drag-and-drop PDF integrato
- **Citazioni inline** nelle risposte (con numero di pagina del rulebook)
- **Sidebar collassabile** con conversazioni precedenti, raggruppate per gioco
- **AI response come card** anziché bubble (meglio per risposte lunghe con struttura)
- **Persistent context header**: mostra sempre il gioco/documento attivo in cima alla chat

---

### 1.3 Session Tracking & Dashboard (NemeStats → BG Stats → Steam)

**Cosa funziona nei competitor:**

| App | Pattern Vincente |
|-----|-----------------|
| **BG Stats** | Analytics profondi non opprimenti, custom score sheet, BGG sync |
| **NemeStats** | Badge/achievements, classifiche tra gruppo fisso di giocatori, "Nemesis" personalizzato |
| **Steam** | Achievement visibili, ore di gioco prominenti, "last played" |

**Pattern da adottare:**
- **Dashboard widget-based**: ogni metrica in un widget/card ridimensionabile
- **Giochi recenti** come entry point principale della dashboard
- **Leaderboard di gruppo**: classifica tra i giocatori della propria cerchia
- **Achievement/badge** per engagement (Nemesis del gruppo, gioco preferito, ecc.)
- **Chart semplici** (bar, line) — non sovraccaricare con grafici complessi
- **Quick-add sessione**: azione prominente (FAB o hero button) per registrare una partita

---

## 2. Pattern UX Universali da Adottare nel 2025

### 2.1 Navigazione Globale

**Raccomandazione: Sidebar collassabile (desktop) + Bottom tab bar (mobile)**

```
Desktop Layout:
┌─────────────────────────────────────────────────────┐
│  [Logo]  │           Area Principale                 │
│          │                                           │
│  🏠 Home  │   ┌─────────────────────────────────┐    │
│  🎲 Giochi│   │  Contenuto contestuale          │    │
│  📚 Libr. │   │                                 │    │
│  💬 Chat  │   │                                 │    │
│  📊 Stats │   └─────────────────────────────────┘    │
│  ⚙️ Admin  │                                           │
│  [Avatar] │                                           │
└───────────┴─────────────────────────────────────────┘

Mobile Layout:
┌──────────────────────┐
│      Top Bar         │
│                      │
│   Area Principale    │
│                      │
│  🏠  🎲  💬  📊  👤  │
└──────────────────────┘
```

**Sidebar best practices (SaaS 2025):**
- Larghezza espansa: 240px | Collapsed: 64px (solo icone)
- Sezione attiva evidenziata con accent color
- Profilo utente in basso nella sidebar
- Tooltip sulle icone in modalità collapsed

---

### 2.2 Homepage / Landing Autenticato

**Pattern: "Shelf Layout" ispirato a Steam/Netflix**

```
┌─────────────────────────────────────────────────────┐
│  Ciao, Mario 👋  |  [Quick Add Sessione ▶]          │
├─────────────────────────────────────────────────────┤
│  🕐 Giocati di Recente                    [Vedi tutti]│
│  [Card] [Card] [Card] [Card] [Card]                 │
├─────────────────────────────────────────────────────┤
│  ✨ Consigliati per Te                    [Vedi tutti]│
│  [Card] [Card] [Card] [Card]                        │
├─────────────────────────────────────────────────────┤
│  📊 Le Tue Stats                                     │
│  [Widget: partite] [Widget: ore] [Widget: giochi]   │
├─────────────────────────────────────────────────────┤
│  👥 Attività Amici                       [Vedi tutti]│
│  [Feed item] [Feed item] [Feed item]                │
└─────────────────────────────────────────────────────┘
```

---

### 2.3 Pagina Catalogo Giochi

**Pattern: Grid con filtri laterali (Stile Steam/BGG)**

```
┌──────────────┬──────────────────────────────────────┐
│  FILTRI      │  [Cerca...]  [Ordina ▼]  [Vista ■ ≡] │
│              ├──────────────────────────────────────┤
│  Categoria   │  [Card] [Card] [Card] [Card]         │
│  Meccaniche  │  [Card] [Card] [Card] [Card]         │
│  N. Giocatori│  [Card] [Card] [Card] [Card]         │
│  Durata      │  [Card] [Card] [Card] [Card]         │
│  BGG Rating  │                                      │
│  Anno        │  [Paginazione / Infinite scroll]     │
└──────────────┴──────────────────────────────────────┘
```

**Card gioco:**
- Copertina grande (aspect ratio 1:1 o 3:4)
- Titolo + Publisher
- Rating (stelle o numero)
- Badge: n. giocatori, durata, anno
- Azione rapida: ❤️ Wishlist | ➕ Aggiungi a libreria

---

### 2.4 Pagina Chat AI / Rulebook

**Pattern: Split layout (Perplexity + ChatGPT)**

```
┌────────────────┬─────────────────────┬──────────────┐
│  CONVERSAZIONI │   CHAT              │  FONTI       │
│                │                     │  (opzionale) │
│  🎲 Catan      │  [Contesto attivo:  │              │
│  🎲 Pandemic   │   Catan Rulebook]   │  📄 p.12     │
│  🎲 Wingspan   │                     │  📄 p.34     │
│                │  AI: Per piazzare   │              │
│  [+ Nuova chat]│  un insediamento    │              │
│                │  devi...¹²          │              │
│                │                     │              │
│                │  [Scrivi domanda...] │              │
│                │  [📎 PDF]  [↑ Send] │              │
└────────────────┴─────────────────────┴──────────────┘
```

**Best practices chat:**
- Citazioni come superscript cliccabili (¹ → pagina rulebook)
- Storico organizzato per gioco nella sidebar sinistra
- Upload PDF con drag-and-drop + stato "Elaborazione..." visibile
- Risposte in card strutturate (non bubble), con Markdown rendering
- Persistent header: "[🎲 Catan] — Rulebook caricato" sopra la chat

---

### 2.5 Dashboard Statistiche

**Pattern: Widget modulari (BG Stats + NemeStats)**

```
┌─────────────────────────────────────────────────────┐
│  LE TUE STATISTICHE          [Periodo: Ultimi 30gg ▼]│
├──────────────┬──────────────┬──────────────┬────────┤
│ 🎲 47        │ ⏱️ 32h       │ 🏆 12 vittorie│ 📈 +5% │
│ Partite      │ Tempo gioco  │             │ vs mese│
├──────────────┴──────────────┴─────────────┴────────┤
│  Giochi più giocati           │  Ultime sessioni    │
│  [Bar chart semplice]         │  [Lista compatta]   │
├───────────────────────────────┴────────────────────┤
│  Classifica Gruppo                                  │
│  🥇 Mario  🥈 Luigi  🥉 Peach  ...                  │
└─────────────────────────────────────────────────────┘
```

---

## 3. Design System Recommendations

### Colori
- **Background principale**: `#0F0F12` (dark) o `#F8F8FA` (light)
- **Surface cards**: `#1A1A22` (dark) / `#FFFFFF` (light)
- **Accent primario**: Arancione caldo `#F5821F` (allineato al brand board game)
- **Accent secondario**: Viola `#8B5CF6` (gaming/AI feel)
- **Success/Stats**: Verde `#10B981`

### Tipografia
- **Heading**: Inter o DM Sans (bold, clean)
- **Body**: Inter 14-16px
- **Metadati/label**: 12px, weight 500

### Componenti Chiave
- **MeepleCard** (già esistente) — usare per tutti gli entity
- **Bento Grid** per dashboard widgets
- **Command Palette** (⌘K) per search globale (pattern moderno)
- **Toast notifications** per azioni rapide (sessione aggiunta, ecc.)

---

## 4. Priorità di Implementazione

| Priorità | Area | Pattern | Impatto |
|----------|------|---------|---------|
| 🔴 Alta | Navigazione globale | Sidebar collassabile + bottom nav mobile | Tutti gli utenti |
| 🔴 Alta | Homepage autenticato | Shelf layout "Giocati di recente" + stats widgets | Retention |
| 🟡 Media | Catalogo giochi | Grid + filtri laterali + card copertine | Discovery |
| 🟡 Media | Chat AI | Split layout con fonti, citazioni inline | Core feature |
| 🟢 Bassa | Dashboard stats | Widget modulari, bento grid | Power users |
| 🟢 Bassa | Command palette | ⌘K global search | UX avanzata |

---

## 5. Referenze Competitive

| App | URL | Cosa Analizzare |
|-----|-----|----------------|
| BGG App | play.google.com (BoardGameGeek) | Filtri, community feed |
| BG Stats | bgstatsapp.com | Dashboard, analytics, card design |
| NemeStats | nemestats.com | Gamification, group play |
| Letterboxd | letterboxd.com | Grid copertine, social feed, tabs |
| Perplexity | perplexity.ai | Chat layout, fonti inline |
| Game UI DB | gameuidatabase.com | 55k+ UI screenshots di giochi |
| SaaSUI | saasui.design | Pattern SaaS moderni |
| SaaSFrame | saasframe.io | Dashboard examples |

---

## 6. Gap Analysis — MeepleAI Attuale vs Best Practice

| Feature | Best Practice | MeepleAI Oggi | Gap |
|---------|--------------|---------------|-----|
| Homepage | Shelf dinamici personalizzabili | Da verificare | Potenziale |
| Catalogo | Grid copertine + filtri sidebar | MeepleCard grid | Filtri sidebar? |
| Chat AI | Layout 3 zone + citazioni | Implementato | Citazioni pagina? |
| Stats | Widget bento + group ranking | In sviluppo | Group leaderboard |
| Search | Command palette ⌘K | Non verificato | Da implementare |
| Mobile | Bottom tab bar | Da verificare | Responsive? |

---

*Ricerca condotta il 2026-04-01. Fonti: BGG, Steam, Letterboxd, BG Stats, Perplexity, NemeStats, NN/g, Chop Dawg, Eleken, SaaSUI.*

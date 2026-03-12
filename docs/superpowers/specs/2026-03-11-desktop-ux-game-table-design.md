# Desktop UX Redesign: "The Game Table"

**Date**: 2026-03-11
**Status**: Approved
**Epic**: Desktop UX Improvement — Card-Centric Design

## Overview

Redesign dell'esperienza desktop MeepleAI centrata sulla metafora del tavolo da gioco. L'intera UX simula una serata di gioco: sidebar come scaffale, contenuto come tavolo, pannelli contestuali come carte girate.

## Architecture

### Layout Desktop (≥1024px)

```
┌──────────────────────────────────────────────────────────────┐
│  Top Bar (48px): Breadcrumb + ⌘K Search + Notifications     │
├────┬─────────────────────────────────────────────┬───────────┤
│    │                                             │           │
│ C  │                                             │  Quick    │
│ a  │              The Table                      │  View     │
│ r  │           (Main Content)                    │  Panel    │
│ d  │                                             │  300px    │
│    │   Grid / Master-Detail / Game Night /       │           │
│ R  │   Live Session                              │  Regole   │
│ a  │                                             │  FAQ      │
│ c  │   [Inline Picker on-demand]                 │  AI Chat  │
│ k  │                                             │           │
│    │                                             │ Collassa  │
│ 64 │                                             │ → 44px    │
│ px │                                             │           │
├────┴─────────────────────────────────────────────┴───────────┘
```

### Layout Mobile (<768px)

```
┌─────────────────────────────┐
│ Status Bar (36px)           │
│ LIVE · Catan · T.8 · 1:23  │
├─────────────────────────────┤
│ Scorebar (52px, scroll-x)   │
│ [M:7] [S:6] [L:5] [A:3]   │
├─────────────────────────────┤
│                             │
│   Feed / Timeline           │
│   (full-width, scroll-y)    │
│                             │
├─────────────────────────────┤
│ Input + Actions             │
│ [🎲 Tira] [📸] [🎥] [🎙️] [🤖] │
│ (safe area padding)         │
└─────────────────────────────┘

Bottom Sheets: Dadi, AI Chat, Score Detail
```

## Components

### 1. Card Rack (Sidebar Slim)

- **Width**: 64px collapsed, 240px expanded on hover
- **Content**: Icone navigazione per tutte le sezioni (Dashboard, Collezione, Scopri, Agenti, Chat, Game Nights, KB, Badges)
- **Behavior**: Hover → espande con label. Badge notifiche. Collassa completamente su ≤1024px.
- **Active state**: Background accent + border per sezione corrente

### 2. Top Bar

- **Height**: 48px
- **Content**: Breadcrumb auto-generato dal path + Command Palette trigger (⌘K) + Notifications bell
- **Breadcrumb**: Icona home + separatori "›" + label corrente. Ogni segmento cliccabile.
- **Search**: Fuzzy search su giochi, agenti, chat, sessioni. Azioni rapide (New Chat, Add Game). Recenti e pinned.

### 3. Quick View (Side Panel)

- **Width**: 300px expanded, 44px collapsed (icone)
- **Toggle**: Indipendente, click su ◀/▶
- **Tabs**: Regole / FAQ / AI Chat / Stats
- **Content**: Contestuale alla carta/gioco selezionato
- **Collapsed**: Strip verticale con icone (🤖 📖 ❓ 📊) cliccabili per espandere su quella tab

### 4. Inline Picker

- **Trigger**: Click su "+" drop zone o "Aggiungi dalla collezione"
- **Layout**: Carosello orizzontale con mini-card giochi
- **Filtri automatici**: Numero giocatori, durata, complessità
- **Behavior**: Click su gioco → appare come carta sul tavolo. Scompare dopo selezione.

### 5. Collapsible Panels (Live Session)

- **Left panel**: Scoreboard + Toolkit
  - Expanded (280px): Classifica con breakdown, toolkit completo
  - Collapsed (44px): Avatar + score mini verticali + dado rapido
- **Right panel**: AI + Regole
  - Expanded (280px): Chat AI, tab Regole/FAQ/Stats
  - Collapsed (44px): Icone AI/Regole/FAQ
- **Independence**: I due pannelli si collassano indipendentemente

## Flows

### Game Night Planning

1. **Lista** (`/game-nights`): Card events con stati (Prossima/Bozza/Completata), avatar stack, mini-carte giochi
2. **Planning** (`/game-nights/[id]`):
   - Left: Info serata + Player cards + AI suggestion
   - Center: Carte giocate sul "tavolo" (rotazione leggera) + drop zone "+"
   - Below table: Inline Picker dalla collezione (filtrato per n. giocatori)
   - Timeline serata con orari stimati
   - Right: Quick View con regole del gioco selezionato
3. **Avvia sessione**: Click "▶ Avvia Sessione" → transizione a Live Session

### Live Session

1. **Layout 3 colonne** (desktop):
   - Left: Scoreboard live + Game Toolkit (dadi, carte, random)
   - Center: Cronologia live (timeline verticale con tutti gli eventi)
   - Right: AI Chat + Regole + FAQ
2. **Scoreboard**: Classifica live con PV, breakdown per giocatore, barra progresso verso obiettivo
3. **Game Toolkit**:
   - Dadi: 2d6, 1d6, 1d20, custom. Risultato con animazione.
   - Pesca carte: Per tipo di mazzo (Sviluppo, Risorsa, etc.)
   - Random: Timer, selezione giocatore, colore
   - Risultati salvati automaticamente in cronologia
4. **Cronologia** (Activity Feed):
   - Timeline verticale con icone colorate per tipo
   - Tipi: 🎲 dadi, 🏆 score, 📸 foto, 🎥 video, 🎙️ audio, 📝 note, 🤖 AI tips, 🔄 turni, ⏸ pause
   - Input bar: testo + media buttons (📸🎥🎙️) + AI (🤖)
   - Tabs: Cronologia / Media / Note / Statistiche
5. **AI Assistant**:
   - Chat contestuale al gioco in corso
   - Quick prompts: "Chi vince?", "Strategia per X", "Regola Y"
   - Tips automatici per principianti
   - Tab switch: AI / Regole / FAQ / Stats
6. **Pausa & Salvataggio**:
   - Overlay centrato con stato completo (punteggi, turno, tempo)
   - Azioni: Riprendi, Foto Tavolo, Aggiungi Nota
   - Timer pausa separato
   - Riprendibile dopo chiusura browser

### Mobile Live Session

1. **Status bar** (36px): Live pulse, gioco, turno, timer, ⏸ Pausa
2. **Scorebar** (52px): Orizzontale scrollabile, mini-card per giocatore, tap per aggiornare
3. **Feed** (flex): Cronologia full-width, scroll verticale
4. **Action bar** (bottom): Input testo + 🎲 Tira Dadi (CTA primaria) + 📸🎥🎙️🤖
5. **Bottom Sheets**:
   - 🎲 Dadi: Sheet con dadi grandi (72px), selettore tipo, storico
   - 🤖 AI: Sheet tall (70%) con chat + tab Regole/FAQ
   - Tap giocatore: Sheet con score breakdown
   - ⏸ Pausa: Overlay fullscreen

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Sidebar width | 64px slim (not 220px) | Massimo spazio contenuto desktop |
| No bottom hand tray | Inline Picker on-demand | Zero spazio persistente occupato |
| Collapsible panels | Indipendenti sinistra/destra | L'utente sceglie il suo layout |
| Mobile toolkit | Bottom sheets | Pattern nativo iOS/Android, familiar |
| Activity feed | Timeline verticale cronologica | Tutti i media types in un flusso unificato |
| AI integration | Side panel (desktop) / Sheet (mobile) | Sempre accessibile, mai intrusivo |

## Mockups

Mockups HTML interattivi disponibili in `.superpowers/brainstorm/785115-1773216855/`:
- `game-card-desktop-concept.html` — Concept A vs B iniziali
- `concept-c-hybrid-innovative.html` — Concept C "The Game Table" approvato
- `hand-alternatives.html` — Alternative per la "mano" (scelta: Picker Inline)
- `final-game-night-flow.html` — Flusso Game Night completo (lista + planning)
- `live-session-gameplay.html` — Sessione live con scoreboard, toolkit, media, AI
- `collapsible-panels-mobile.html` — Pannelli riducibili + design mobile

## Sprint Plan (Preliminary)

### Sprint 1: Desktop Navigation Foundation (P0)
- Card Rack sidebar slim 64px con hover-expand
- Breadcrumb desktop auto-generato
- Azioni inline nella toolbar (non FAB)
- Command Palette (⌘K) con fuzzy search

### Sprint 2: Game Night Experience (P1)
- Game Night list con card events
- Game Night planning con tavolo carte + picker inline
- Timeline serata
- AI suggestions basate su giocatori

### Sprint 3: Live Session (P1)
- Scoreboard live con breakdown
- Game Toolkit (dadi, carte, random)
- Activity Feed cronologico
- AI Chat side panel
- Media capture (foto, video, audio)
- Pausa & salvataggio stato

### Sprint 4: Collapsible Panels + Quick View (P2)
- Pannelli riducibili indipendenti
- Quick View side panel (regole/FAQ/AI)
- Keyboard shortcuts
- State persistence (tab, filtri, scroll)

### Sprint 5: Mobile Live Session (P2)
- Layout mobile touch-first
- Scorebar orizzontale
- Bottom sheets (dadi, AI, score)
- Safe area iOS
- Reduced motion support

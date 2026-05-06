# Dashboard Overview Hub - Layout Riassuntivo

**Obiettivo**: Hub centrale post-login con overview multi-sezione e collegamenti rapidi a pagine specializzate.

**Design Philosophy**: Informativa ma non densa, con chiare call-to-action per approfondire.

---

## 📐 Layout Skeleton (Full Dashboard)

```markdown
┌─────────────────────────────────────────────────────────────────┐
│ HEADER (Sticky)                                                 │
│ [Logo] [Search Global] [Profile + Notifications]               │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ HERO SECTION                                                    │
│ ┌─────────────────────────────────────────────────────────────┐│
│ │ 👋 Ciao, Marco!                                             ││
│ │ Bentornato su MeepleAI - Ultimo accesso: Oggi alle 14:30   ││
│ └─────────────────────────────────────────────────────────────┘│
│                                                                 │
│ STATS OVERVIEW (4-col grid, responsive 2-col mobile)           │
│ ┌──────────┬──────────┬──────────┬──────────┐                 │
│ │📚 127    │🎲 23     │💬 12     │⭐ 15     │                 │
│ │Collezione│Giocati   │Chat AI   │Wishlist  │                 │
│ │+3 mese   │30gg 🔥7d │7gg       │+2 mese   │                 │
│ └──────────┴──────────┴──────────┴──────────┘                 │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ ACTIVE SESSIONS (Sessioni in corso)                            │
│ ┌─────────────────────────────────────────────────────────────┐│
│ │ 🎯 2 Sessioni Attive                       [Vedi Tutte →] ││
│ ├─────────────────────────────────────────────────────────────┤│
│ │ 🎲 Catan - Partita del 20/01               [Continua ▶]   ││
│ │    3/4 giocatori • Turno 12 • 45min                        ││
│ ├─────────────────────────────────────────────────────────────┤│
│ │ 🎲 Ticket to Ride - Partita del 19/01      [Continua ▶]   ││
│ │    2/5 giocatori • Turno 8 • 30min                         ││
│ └─────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘

┌────────────────────────────┬────────────────────────────────────┐
│ LIBRARY SNAPSHOT           │ RECENT ACTIVITY FEED               │
│ (Collezione Overview)      │ (Timeline Attività)                │
│                            │                                    │
│ 📊 Quota: 127/200 (64%)   │ 🕒 Ultime Attività                │
│ [Progress Bar ████░░░]     │                                    │
│                            │ • Oggi 15:00                       │
│ Top 3 Giochi Posseduti:    │   📚 Aggiunto "Wingspan"          │
│ ┌────────────────────────┐ │                                    │
│ │ [Cover] Catan          │ │ • Oggi 14:30                       │
│ │ ★★★★★ • 45 partite    │ │   🎲 Giocato "Catan"              │
│ └────────────────────────┘ │                                    │
│ ┌────────────────────────┐ │ • Ieri 20:15                       │
│ │ [Cover] Ticket to Ride │ │   💬 Chat "Regole Wingspan"       │
│ │ ★★★★☆ • 32 partite    │ │                                    │
│ └────────────────────────┘ │ • 19/01 18:00                      │
│ ┌────────────────────────┐ │   ⭐ Aggiunto a Wishlist          │
│ │ [Cover] Azul           │ │      "Terraforming Mars"          │
│ │ ★★★★☆ • 28 partite    │ │                                    │
│ └────────────────────────┘ │ • 18/01 21:00                      │
│                            │   🎲 Completato "Azul"            │
│ [Vedi Collezione Completa] │                                    │
│                            │ [Vedi Tutta la Timeline →]        │
└────────────────────────────┴────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ AI INSIGHTS & SUGGESTIONS (Powered by RAG)                     │
│ ┌─────────────────────────────────────────────────────────────┐│
│ │ 💡 Suggerimenti Personalizzati                              ││
│ ├─────────────────────────────────────────────────────────────┤│
│ │ • 🎯 5 giochi non giocati da 30+ giorni     [Scopri →]    ││
│ │ • 📖 Regole di "Wingspan" salvate           [Rivedi →]    ││
│ │ • 🆕 3 giochi simili a "Catan" nel catalogo [Esplora →]   ││
│ │ • 🔥 Streak: 7 giorni - Mantienilo!         [Stats →]     ││
│ └─────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ CHAT HISTORY (Conversazioni AI Recenti)                        │
│ ┌─────────────────────────────────────────────────────────────┐│
│ │ 💬 Ultime Conversazioni                    [Nuova Chat +] ││
│ ├─────────────────────────────────────────────────────────────┤│
│ │ • "Regole Wingspan - Setup iniziale"       Oggi 14:30     ││
│ │ • "Strategie Catan - Espansione Marinai"   Ieri 20:00     ││
│ │ • "FAQ Ticket to Ride - Carte duplicate"   18/01 19:30    ││
│ │ • "Setup Azul - Modalità 2 giocatori"      17/01 21:00    ││
│ └─────────────────────────────────────────────────────────────┘│
│ [Vedi Tutte le Chat →]                                         │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ QUICK ACTIONS GRID (Collegamenti Rapidi)                       │
│ ┌──────────┬──────────┬──────────┬──────────┬──────────┐      │
│ │ 📚       │ 🎲       │ 💬       │ 🔍       │ ⚙️       │      │
│ │ Vai alla │ Nuova    │ Chat AI  │ Esplora  │ Imposta- │      │
│ │ Collezione│ Sessione│ Regole   │ Catalogo │ zioni    │      │
│ └──────────┴──────────┴──────────┴──────────┴──────────┘      │
└─────────────────────────────────────────────────────────────────┘

┌────────────────────────────┬────────────────────────────────────┐
│ WISHLIST HIGHLIGHTS        │ CATALOG TRENDING                   │
│                            │ (Giochi Popolari Community)        │
│ ⭐ Top 5 Wishlist:         │                                    │
│ 1. Terraforming Mars       │ 🔥 Trending questa settimana:     │
│ 2. Gloomhaven              │ 1. Ark Nova (+15% ricerche)       │
│ 3. Brass Birmingham        │ 2. Wingspan (+12%)                │
│ 4. Spirit Island           │ 3. Dune: Imperium (+10%)          │
│ 5. Root                    │                                    │
│                            │ [Vedi Catalogo Completo →]        │
│ [Gestisci Wishlist →]     │                                    │
└────────────────────────────┴────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ ACHIEVEMENTS & BADGES (Gamification - Optional)                │
│ ┌─────────────────────────────────────────────────────────────┐│
│ │ 🏆 Progressi Recenti                                        ││
│ ├─────────────────────────────────────────────────────────────┤│
│ │ 🎖️ "Giocatore Costante" - 7 giorni di streak              ││
│ │ 🎖️ "Collezionista" - 100+ giochi nella libreria           ││
│ │ 🎖️ "Esperto AI" - 50+ chat completate                      ││
│ │                                       [Vedi Tutti i Badge →]││
│ └─────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ BOTTOM NAV (Mobile)                                             │
│ [🏠 Dashboard] [📚 Libreria] [🎲 Sessioni] [💬 Chat] [👤 Profilo]│
└─────────────────────────────────────────────────────────────────┘
```

---

## 🧩 Sezioni Dettagliate

### 1. Hero Section + Stats Overview
**Dati mostrati**:
- Greeting personalizzato con nome utente
- Ultimo accesso
- 4 metriche chiave:
  - **Collezione**: Totale giochi + trend mese (+3)
  - **Giocati**: Partite 30gg + streak 🔥
  - **Chat AI**: Conversazioni 7gg
  - **Wishlist**: Giochi desiderati + trend

**Interazione**: Cliccando su ogni card → naviga a pagina dedicata

---

### 2. Active Sessions Widget (Issue #2617)
**Dati mostrati**:
- Numero sessioni attive
- Lista ultime 2 sessioni in corso:
  - Nome gioco
  - Giocatori (es. 3/4)
  - Progresso (es. Turno 12)
  - Durata parziale

**CTA**:
- **[Continua ▶]** → Riprendi sessione (`/sessions/{id}`)
- **[Vedi Tutte →]** → Elenco completo (`/sessions`)

**Se nessuna sessione attiva**: Mostra "Nessuna sessione attiva" + CTA "Inizia Nuova Partita"

---

### 3. Library Snapshot (Collezione Overview)
**Dati mostrati**:
- **Quota**: Giochi posseduti / Limite (es. 127/200 = 64%) con progress bar
- **Top 3 Giochi**:
  - Cover thumbnail
  - Titolo
  - Rating ★★★★★
  - Numero partite giocate

**CTA**: **[Vedi Collezione Completa]** → `/library`

**Design**: Card compatta con grid 3 giochi (1-col mobile, 3-col desktop)

---

### 4. Recent Activity Feed (Timeline)
**Dati mostrati** (Issue #2612 + estensioni):
- Timeline cronologica ultimi 5 eventi:
  - 📚 Giochi aggiunti alla libreria
  - 🎲 Partite completate
  - 💬 Chat salvate
  - ⭐ Wishlist updates
  - 🏆 Achievement unlocked

**Formato**:
```
• Oggi 15:00
  📚 Aggiunto "Wingspan"

• Oggi 14:30
  🎲 Giocato "Catan"
```

**CTA**: **[Vedi Tutta la Timeline →]** → `/activity` (pagina dedicata)

---

### 5. AI Insights & Suggestions
**Dati mostrati** (AI-Powered):
- 💡 Suggerimenti personalizzati basati su:
  - **Giochi non giocati da 30+ giorni** (backlog alert)
  - **Regole salvate da rivedere** (chat history analysis)
  - **Giochi simili nel catalogo** (RAG recommendations)
  - **Streak maintenance** (gamification nudge)

**Interazione**: Ogni insight è cliccabile → azione diretta
- "5 giochi non giocati" → Filtro collezione con `lastPlayed < 30d`
- "Regole salvate" → Chat history filtrata per quel gioco
- "Giochi simili" → Catalogo con filtro similarità

**Tecnologia**: Backend usa RAG embeddings per suggerimenti contestuali

---

### 6. Chat History (Conversazioni AI)
**Dati mostrati**:
- Ultime 4 conversazioni con AI agent
- Formato:
  - Titolo conversazione (auto-generated o custom)
  - Data/ora

**CTA**:
- **[Nuova Chat +]** → Apri chat AI (`/chat`)
- **[Vedi Tutte le Chat →]** → History completa (`/chat/history`)

**Design**: Lista compatta con hover highlight

---

### 7. Quick Actions Grid
**Collegamenti rapidi** (5 azioni primarie):
1. 📚 **Vai alla Collezione** → `/library`
2. 🎲 **Nuova Sessione** → `/sessions/new`
3. 💬 **Chat AI Regole** → `/chat`
4. 🔍 **Esplora Catalogo** → `/games/catalog`
5. ⚙️ **Impostazioni** → `/settings`

**Design**: Card grid 5-col (2-col mobile) con icon + label

---

### 8. Wishlist Highlights
**Dati mostrati**:
- Top 5 giochi nella wishlist (ordinati per priorità o data aggiunta)
- Solo titolo (no cover per compattezza)

**CTA**: **[Gestisci Wishlist →]** → `/wishlist`

**Future**: Alert su disponibilità/prezzi (integrazione esterna)

---

### 9. Catalog Trending (Community Insights)
**Dati mostrati**:
- Top 3 giochi trending nel catalogo condiviso
- Metrica: Incremento ricerche/aggiunte settimanale (es. +15%)

**CTA**: **[Vedi Catalogo Completo →]** → `/games/catalog`

**Design**: Lista numerata compatta

---

### 10. Achievements & Badges (Gamification - Optional)
**Dati mostrati**:
- Ultimi 3 achievement sbloccati
- Esempi:
  - 🎖️ "Giocatore Costante" - 7 giorni streak
  - 🎖️ "Collezionista" - 100+ giochi
  - 🎖️ "Esperto AI" - 50+ chat

**CTA**: **[Vedi Tutti i Badge →]** → `/achievements`

**Design**: Card con emoji badge + titolo + descrizione breve

---

## 🎨 Design Principles

### Visual Hierarchy
1. **Hero Stats**: Grandi, colorati, immediate attention
2. **Active Sessions**: Urgency (sessioni in corso)
3. **Library + Activity**: Info-dense ma scannable
4. **AI Insights**: Highlight con colori distintivi (amber/yellow)
5. **Chat + Quick Actions**: Accessibili ma non dominanti
6. **Wishlist + Trending**: Scoperta e ispirazione
7. **Achievements**: Delight e engagement

### Information Density
- **Mobile**: Una colonna, sezioni collassabili
- **Tablet**: 2 colonne, sidebar/main layout
- **Desktop**: 3 colonne asimmetriche (sidebar + main + aside)

### Color Coding
- 📚 **Collezione**: Amber/Orange (warmth, ownership)
- 🎲 **Sessioni**: Emerald/Green (active, progress)
- 💬 **Chat AI**: Blue (knowledge, assistance)
- ⭐ **Wishlist**: Purple (aspiration, future)
- 💡 **Insights**: Yellow (attention, discovery)
- 🏆 **Achievements**: Gold (reward, gamification)

---

## 🔗 Navigation Flow

### Dashboard come Hub
```
Dashboard (/)
├── Library (/library)
│   └── Collection Dashboard (la tua Opzione A)
├── Sessions (/sessions)
│   ├── Active (/sessions/{id})
│   ├── History (/sessions/history)
│   └── New (/sessions/new)
├── Chat AI (/chat)
│   ├── New Chat (/chat)
│   └── History (/chat/history)
├── Catalog (/games/catalog)
├── Wishlist (/wishlist)
├── Activity (/activity)
├── Achievements (/achievements)
└── Settings (/settings)
```

### Deep Links da Dashboard
- **Stat card "Collezione"** → `/library`
- **Stat card "Giocati"** → `/sessions/history`
- **Stat card "Chat AI"** → `/chat`
- **Stat card "Wishlist"** → `/wishlist`
- **"Continua Sessione"** → `/sessions/{id}`
- **"Vedi Collezione Completa"** → `/library` (con la tua UI flip cards)
- **AI Insight "Giochi non giocati"** → `/library?filter=unplayed`

---

## 📱 Responsive Layout

### Mobile (< 640px)
```
[Hero + Stats 2-col]
[Active Sessions full-width]
[Library Snapshot full-width]
[Activity Feed full-width]
[AI Insights full-width]
[Chat History full-width]
[Quick Actions 2-col grid]
[Wishlist + Trending stacked]
[Achievements (collapsed)]
```

### Desktop (> 1024px)
```
┌────────────────────────────────────┐
│ Hero + Stats 4-col                 │
├──────────────┬─────────────────────┤
│ Sidebar (L)  │ Main Content (C)    │
│              │                     │
│ Library      │ Active Sessions     │
│ Snapshot     │ Activity Feed       │
│              │ AI Insights         │
│ Wishlist     │ Chat History        │
│ Highlights   │                     │
│              │ Quick Actions       │
├──────────────┴─────────────────────┤
│ Trending + Achievements full-width │
└────────────────────────────────────┘
```

---

## 🚀 Implementation Priority

### Phase 1 (MVP - Current Sprint)
1. ✅ Hero + Stats Overview (già presente: GreetingSection)
2. ✅ Active Sessions Widget (già presente: Issue #2617)
3. ✅ Library Snapshot (già presente: LibraryQuotaSection)
4. ✅ Recent Activity Feed (parziale: RecentlyAddedSection Issue #2612)
5. ✅ Chat History (già presente: ChatHistorySection)
6. ✅ Quick Actions (già presente: QuickActions)

### Phase 2 (Post-MVP)
7. 🔄 AI Insights & Suggestions (richiede RAG backend)
8. 🔄 Wishlist Highlights (richiede wishlist management)
9. 🔄 Catalog Trending (richiede analytics backend)

### Phase 3 (Enhancement)
10. 🔄 Achievements & Badges (gamification system)
11. 🔄 Advanced Activity Timeline (filtri, search)
12. 🔄 Personalized Recommendations (ML model)

---

## 🎯 Key Differences vs Opzione A

| Aspetto | Opzione A (Collection Focus) | Dashboard Hub (Questa) |
|---------|------------------------------|------------------------|
| **Scope** | Solo collezione giochi | Multi-sezione (library, sessions, chat, wishlist) |
| **Profondità** | Deep (flip cards, filtri avanzati) | Shallow (snapshot + CTA) |
| **Navigation** | Standalone page | Hub con collegamenti a pagine dedicate |
| **Use Case** | Gestire collezione dettagliatamente | Overview generale + quick access |
| **Ideal Path** | `/library` (pagina dedicata) | `/` o `/dashboard` (landing post-login) |

**Conclusione**: L'Opzione A (flip cards collection) dovrebbe diventare la **pagina `/library`**, mentre questa dashboard hub diventa il **landing post-login `/dashboard`**.

---

## 📊 Data Sources (API Endpoints)

```typescript
// Dashboard Data Aggregation
interface DashboardData {
  user: User;
  stats: {
    libraryCount: number;
    playedLast30Days: number;
    chatCount: number;
    wishlistCount: number;
    streak: number;
  };
  activeSessions: GameSession[];
  librarySnapshot: {
    quota: { used: number; total: number };
    topGames: Game[];
  };
  recentActivity: Activity[];
  aiInsights: Insight[];
  chatHistory: ChatThread[];
  wishlistHighlights: Game[];
  catalogTrending: Game[];
  achievements: Achievement[];
}

// Single API call for dashboard
GET /api/v1/dashboard
→ Returns aggregated DashboardData
```

**Performance**: Singola chiamata aggregata vs. multiple chiamate per sezione (cache server-side 5 min)

---

Vuoi che creo il **componente React completo** per questa dashboard hub? Oppure preferisci prima discutere quali sezioni prioritizzare?
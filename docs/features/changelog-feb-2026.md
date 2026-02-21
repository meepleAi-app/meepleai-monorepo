# MeepleAI — Changelog Funzionalità (21 Gen – 21 Feb 2026)

> Riepilogo di pagine, endpoint e funzionalità aggiunte o modificate nell'ultimo mese.
> Data generazione: 2026-02-21

---

## Indice

1. [Funzionalità per Area](#1-funzionalità-per-area)
2. [Pagine Frontend — Inventario Completo](#2-pagine-frontend--inventario-completo)
3. [Endpoint Backend Aggiunti](#3-endpoint-backend-aggiunti)

---

## 1. Funzionalità per Area

### Chat & AI Agents

| # | Funzionalità | Issue |
|---|---|---|
| 1 | **Custom Agents**: wizard di creazione agente, AgentCreationSheet, GameSelector/ModelSelector collegati a API reale | #4804 |
| 2 | **Chat grouping**: sessioni raggruppate per agente con limiti tier-based | #4913 |
| 3 | **Tab Debug chat**: visibilità pipeline RAG direttamente in chat | #4916 |
| 4 | **PDF Citations in chat**: citazioni PDF nelle risposte AI | #4912 |
| 5 | **Agent quota**: enforce creazione agenti per tier utente | #4944 |
| 6 | **Chat welcome message** + proxy SSE OpenRouter | #4780 |
| 7 | **Auto-link PDF**: collegamento automatico PDF indicizzati al game agent | #4941 |

### Admin Dashboard

| # | Funzionalità | Issue |
|---|---|---|
| 1 | **Navigation redesign**: Universal Navbar + Context-Sensitive Sidebar | #4936 |
| 2 | **Debug Chat admin**: real-time RAG pipeline tracing | — |
| 3 | **Knowledge Base overview**: pagina panoramica + coda di elaborazione | #4892 |
| 4 | **KB settings**: dashboard read-only + cache clear | #4888 |
| 5 | **Processing metrics**: dashboard con P99 bug fix | #4880 |
| 6 | **RAG pipeline health**: overview live | #4879 |
| 7 | **Embedding Service**: dashboard + monitoring | #4878 |
| 8 | **Qdrant operations**: operazioni avanzate + delete | #4877 |
| 9 | **Upload & Process**: pagina wired con dati reali | #4876 |
| 10 | **Documents Library**: gestione PDF admin | #4795 |
| 11 | **Vector Collections**: dati reali Qdrant | #4793 |
| 12 | **Chat history tier limits**: config admin | #4918 |
| 13 | **Shared Games migration**: integrazione nel nuovo dashboard | — |
| 14 | **Card Stack panel** + table view con dati ruolo reali | #4814 |

### Frontend Pages (nuove)

| Pagina | Descrizione | Issue |
|--------|-------------|-------|
| `/profile` | Landing con tabs (achievements, stats, history) | #4893 |
| `/games/[id]/strategies` | Strategie per il gioco | #4889 |
| `/games/[id]/reviews` | Recensioni community | #4889 |
| `/players/[id]/*` | Sub-route: achievements, games, sessions, stats | #4890 |
| `/sessions/[id]/players` | Gestione giocatori sessione | #4891 |
| `/sessions/[id]/notes` | Note di sessione | #4891 |
| `/games/[id]/add` → AddGameSheet | Drawer wizard 3-step per aggiungere giochi | #4818–#4821 |
| `/dashboard` | Redesign dashboard user-facing | #4936 |

### Session Live Game (Player Journey)

| # | Funzionalità | Issue |
|---|---|---|
| 1 | **LiveGameSession aggregate** + domain model | #4747 |
| 2 | **SSE streaming infrastructure** + state broadcasting | #4764 |
| 3 | **Session Join** via codice + ruoli attivi | #4766 |
| 4 | **Player actions** + host validation + conflict resolution | #4765 |
| 5 | **SSE Client** + Player/Spectator mode UI | #4767 |
| 6 | **ExtraMeepleCard** (4 tab: Info, Media, AI, Actions) | #4757, #4762, #4763 |
| 7 | **SessionSnapshot** history slider + time travel mode | #4755, #4758 |
| 8 | **Turn Phases** + event-triggered snapshots | #4761 |
| 9 | **SessionMedia** entity + RAG agent integration | #4760 |
| 10 | **GameToolkit** bounded context + ToolState | #4753, #4754 |

### Knowledge Base / RAG

| # | Funzionalità | Issue |
|---|---|---|
| 1 | **KB Processing Status API** per game | #4943 |
| 2 | **Notifica utente** quando PDF indicizzato | #4942 |
| 3 | **SharedGameId** su VectorDocument per contenuti admin | #4921 |
| 4 | **Admin PDF upload** (bypass user library, 500MB) + AgentDefinition.KbCardIds | #4922–#4925 |
| 5 | **Orchestrated agent creation** con auto-setup flow | #4800 |
| 6 | **Agent slots endpoint** con quota tier-based | #4799 |

### MeepleCard Design System

| # | Funzionalità | Issue |
|---|---|---|
| 1 | Migration: PrivateGameCard, SharedLibrary, RecentLibrary, BggCard, ShareRequestCard, VectorCollectionCard | #4857–#4869 |
| 2 | **MeepleCard Session Front/Back** | #4751, #4752 |
| 3 | **CardAgentAction footer** | #4777 |
| 4 | **Responsive flip trigger**: touch button / desktop click | #4841 |
| 5 | **3-state button visibility** system | #4899 |
| 6 | **Uniform** su dashboard, /games e admin | #4909 |

---

## 2. Pagine Frontend — Inventario Completo

### Pubbliche (nessuna autenticazione)

| URL | Descrizione |
|-----|-------------|
| `/` | Landing page marketing |
| `/about` | Informazioni sul prodotto |
| `/blog` | Blog post |
| `/contact` | Modulo contatto |
| `/contributions` | Contributi community |
| `/cookies` | Cookie policy |
| `/privacy` | Privacy policy |
| `/terms` | Termini di servizio |
| `/faq` | Domande frequenti |
| `/how-it-works` | Tutorial / funzionamento |
| `/discover` | Scoperta giochi consigliati |
| `/gallery` | Galleria visuale |
| `/offline` | Pagina offline fallback |
| `/games` | Catalogo giochi |
| `/games/catalog` | Vista alternativa catalogo |
| `/games/[id]` | Dettaglio gioco |
| `/games/add` | Aggiungi gioco al catalogo |
| `/shared-games/[id]` | Dettaglio gioco condiviso |
| `/giochi/[id]` | Dettaglio gioco (IT) |
| `/board-game-ai` | Info su AI per board game |
| `/board-game-ai/games` | Giochi con feature AI |
| `/sessions` | Lista sessioni pubbliche |
| `/sessions/[id]` | Dettaglio sessione |
| `/sessions/[id]/state` | Stato sessione (viewer) |
| `/sessions/join/[token]` | Join sessione via token |
| `/sessions/history` | Storico sessioni pubbliche |
| `/settings` | Impostazioni guest |
| `/settings/usage` | Quota e utilizzo |
| `/library/shared/[token]` | Libreria condivisa via token |
| `/preview/chat-card` | Preview componente chat card |
| `/preview/collection-dashboard` | Preview dashboard collection |
| `/design/cards` | Demo MeepleCard |
| `/demo/entity-list-view` | Demo EntityListView |
| `/demo/entity-list-complete` | Demo lista entità completa |

### Autenticazione (solo utenti non loggati)

| URL | Descrizione |
|-----|-------------|
| `/login` | Login |
| `/register` | Registrazione |
| `/welcome` | Benvenuto post-registrazione |
| `/reset-password` | Reset password |
| `/verify-email` | Verifica email |
| `/verification-pending` | In attesa verifica |
| `/verification-success` | Email verificata |
| `/oauth-callback` | Callback OAuth |
| `/auth/callback` | Callback auth generico |

### Utente Autenticato

#### Dashboard & Hub

| URL | Descrizione |
|-----|-------------|
| `/dashboard` | Gaming hub principale |
| `/dashboard/budget` | Overview budget/spesa |

#### Chat

| URL | Descrizione |
|-----|-------------|
| `/chat` | Lista chat (raggruppate per agente) |
| `/chat/new` | Nuova conversazione |
| `/chat/[threadId]` | Thread chat attivo |
| `/chat/agents/create` | Crea agente chat |

#### Agenti

| URL | Descrizione |
|-----|-------------|
| `/agents` | Lista agenti disponibili |
| `/agents/[id]` | Dettaglio agente |
| `/agent/slots` | Slot agenti disponibili |

#### Libreria & Giochi

| URL | Descrizione |
|-----|-------------|
| `/library` | Libreria giochi |
| `/library/games/[gameId]` | Gioco in libreria |
| `/library/games/[gameId]/agent` | Agente AI per gioco |
| `/library/games/[gameId]/toolkit` | Toolkit per gioco |
| `/library/games/[gameId]/toolkit/[sessionId]` | Sessione toolkit attiva |
| `/library/private` | Collezione privata |
| `/library/private/add` | Aggiungi gioco privato |
| `/library/wishlist` | Lista desideri |
| `/library/proposals` | Proposte giochi |
| `/library/propose` | Proponi gioco |

#### Giochi (Autenticato)

| URL | Descrizione |
|-----|-------------|
| `/games/[id]/faqs` | FAQ del gioco |
| `/games/[id]/reviews` | Recensioni community *(nuovo)* |
| `/games/[id]/rules` | Regole dettagliate |
| `/games/[id]/sessions` | Sessioni per questo gioco |
| `/games/[id]/strategies` | Strategie *(nuovo)* |

#### Sessioni

| URL | Descrizione |
|-----|-------------|
| `/sessions/[id]/notes` | Note sessione *(nuovo)* |
| `/sessions/[id]/players` | Giocatori sessione *(nuovo)* |

#### Play Records

| URL | Descrizione |
|-----|-------------|
| `/play-records` | Partite registrate |
| `/play-records/new` | Nuova partita |
| `/play-records/[id]` | Dettaglio partita |
| `/play-records/[id]/edit` | Modifica partita |
| `/play-records/stats` | Statistiche partite |

#### Giocatori

| URL | Descrizione |
|-----|-------------|
| `/players` | Lista giocatori |
| `/players/[id]` | Profilo giocatore |
| `/players/[id]/achievements` | Achievements giocatore *(nuovo)* |
| `/players/[id]/games` | Giochi del giocatore *(nuovo)* |
| `/players/[id]/sessions` | Sessioni del giocatore *(nuovo)* |
| `/players/[id]/stats` | Statistiche giocatore *(nuovo)* |

#### Profilo & Impostazioni

| URL | Descrizione |
|-----|-------------|
| `/profile` | Profilo utente con tabs *(nuovo)* |
| `/profile/achievements` | Achievements utente |
| `/settings/notifications` | Notifiche |
| `/settings/security` | Sicurezza e 2FA |
| `/setup` | Setup iniziale |

#### Toolkit

| URL | Descrizione |
|-----|-------------|
| `/toolkit` | Toolkit principale |
| `/toolkit/demo` | Demo toolkit |
| `/toolkit/history` | Storico toolkit |
| `/toolkit/[sessionId]` | Sessione toolkit attiva |

#### Knowledge Base & Notifiche

| URL | Descrizione |
|-----|-------------|
| `/knowledge-base/[id]` | Voce KB specifica |
| `/notifications` | Centro notifiche |
| `/badges` | Badge e achievement system |
| `/upload` | Upload file/documenti |
| `/n8n` | n8n workflow integration |

#### Editor (ruolo Editor)

| URL | Descrizione |
|-----|-------------|
| `/editor` | Dashboard editor |
| `/editor/agent-proposals` | Proposte agenti |
| `/editor/agent-proposals/create` | Crea proposta agente |
| `/editor/agent-proposals/[id]/edit` | Modifica proposta |
| `/editor/agent-proposals/[id]/test` | Test proposta |

### Admin — Nuovo Dashboard Unificato

#### Overview

| URL | Descrizione |
|-----|-------------|
| `/admin/overview` | Dashboard principale admin |
| `/admin/overview/activity` | Activity log |
| `/admin/overview/system` | Stato sistema |

#### Agenti AI

| URL | Descrizione |
|-----|-------------|
| `/admin/agents` | Hub gestione agenti |
| `/admin/agents/builder` | Creazione/modifica agenti *(nuovo)* |
| `/admin/agents/analytics` | Analytics agenti |
| `/admin/agents/models` | Configurazione modelli AI |
| `/admin/agents/pipeline` | Visualizzazione pipeline RAG *(nuovo #4624)* |
| `/admin/agents/debug` | Debug pipeline RAG *(nuovo #4624)* |
| `/admin/agents/debug-chat` | Chat con debug RAG *(nuovo)* |
| `/admin/agents/strategy` | Configurazione strategie RAG *(nuovo #4624)* |
| `/admin/agents/chat-history` | Storico chat agenti *(nuovo)* |
| `/admin/agents/chat-limits` | Limiti sessioni chat *(nuovo #4918)* |

#### Knowledge Base

| URL | Descrizione |
|-----|-------------|
| `/admin/knowledge-base` | Hub gestione KB |
| `/admin/knowledge-base/documents` | Gestione PDF indicizzati *(nuovo #4787)* |
| `/admin/knowledge-base/vectors` | Collezioni vettoriali Qdrant *(nuovo #4786)* |
| `/admin/knowledge-base/pipeline` | Pipeline elaborazione |
| `/admin/knowledge-base/queue` | Coda documenti *(nuovo #4892)* |
| `/admin/knowledge-base/processing` | Stato elaborazione attuale |
| `/admin/knowledge-base/embedding` | Configurazione embedding |
| `/admin/knowledge-base/upload` | Upload PDF admin *(nuovo #4925)* |
| `/admin/knowledge-base/settings` | Impostazioni KB *(nuovo #4888)* |

#### Shared Games

| URL | Descrizione |
|-----|-------------|
| `/admin/shared-games` | Hub gestione shared games |
| `/admin/shared-games/new` | Crea shared game |
| `/admin/shared-games/all` | Tutti i shared games |
| `/admin/shared-games/categories` | Gestione categorie |
| `/admin/shared-games/[id]` | Dettaglio shared game |

#### Utenti

| URL | Descrizione |
|-----|-------------|
| `/admin/users` | Dashboard gestione utenti |
| `/admin/users/activity` | Attività utenti |
| `/admin/users/roles` | Gestione ruoli |
| `/admin/users/[id]` | Dettaglio utente |

### Admin — Struttura Legacy (ancora attiva)

| URL | Descrizione |
|-----|-------------|
| `/admin/agent-definitions` | Definizioni agenti |
| `/admin/agent-definitions/create` | Crea definizione agente |
| `/admin/agent-definitions/[id]/edit` | Modifica definizione |
| `/admin/agent-definitions/playground` | Playground agenti |
| `/admin/agent-typologies` | Tipologie agenti |
| `/admin/agents/catalog` | Catalogo agenti |
| `/admin/agents/metrics` | Metriche agenti |
| `/admin/ai-lab/agents/builder` | Builder avanzato AI Lab |
| `/admin/ai-lab/multi-agent` | Sistema multi-agente |
| `/admin/ai-models` | Modelli AI |
| `/admin/ai-usage` | Utilizzo API AI |
| `/admin/alerts` | Alert attivi |
| `/admin/alert-rules` | Regole alert |
| `/admin/analytics` | Analytics sistema |
| `/admin/rag-executions` | Log esecuzioni RAG |
| `/admin/rag/execution-replay` | Replay operazioni RAG |
| `/admin/rag/strategy-builder` | Builder strategie RAG |
| `/admin/rag/tier-strategy-config` | Config strategie per tier |
| `/admin/games` | Gestione database giochi |
| `/admin/games/import/bulk` | Import bulk giochi |
| `/admin/games/import/wizard` | Wizard import giochi |
| `/admin/game-sessions` | Gestione sessioni |
| `/admin/pdfs` | Gestione PDF |
| `/admin/faqs` | Gestione FAQ |
| `/admin/sessions` | Sessioni admin |
| `/admin/configuration` | Configurazione sistema |
| `/admin/configuration/game-library-limits` | Limiti libreria giochi |
| `/admin/configuration/pdf-tier-limits` | Limiti PDF per tier |
| `/admin/feature-flags` | Feature flags |
| `/admin/tier-limits` | Limitazioni per tier |
| `/admin/prompts` | Gestione prompt di sistema |
| `/admin/strategies` | Strategie |
| `/admin/audit-log` | Audit trail azioni |
| `/admin/infrastructure` | Monitoraggio infrastruttura |
| `/admin/cache` | Gestione cache |
| `/admin/services` | Stato microservizi |
| `/admin/api-keys` | Gestione API key |
| `/admin/reports` | Report |
| `/admin/bulk-export` | Export dati |
| `/admin/share-requests` | Richieste di condivisione |
| `/admin/n8n-templates` | Template n8n |

---

## 3. Endpoint Backend Aggiunti

### Game Management

| Metodo | Path | Descrizione | Issue |
|--------|------|-------------|-------|
| `GET` | `/api/v1/games/{id}/reviews` | Recensioni del gioco | #4904 |
| `GET` | `/api/v1/games/{id}/strategies` | Strategie per il gioco | #4903 |
| `GET` | `/api/v1/games/{id}/preview` | Preview gioco per wizard | #4776 |

### Knowledge Base / RAG

| Metodo | Path | Descrizione | Issue |
|--------|------|-------------|-------|
| `GET` | `/api/v1/knowledge-base/{gameId}/status` | Stato indicizzazione PDF per game | #4943 |
| `GET` | `/api/v1/knowledge-base/agent-slots` | Slot agenti disponibili (quota tier) | #4799 |
| `POST` | `/api/v1/knowledge-base/agents` | Orchestrated agent creation con auto-setup | #4800 |

### Admin — PDF & Documents

| Metodo | Path | Descrizione | Issue |
|--------|------|-------------|-------|
| `POST` | `/api/v1/admin/shared-games/{id}/pdfs` | Upload PDF per shared game (500MB, bypass quota) | #4922 |
| `GET` | `/api/v1/admin/shared-games/{id}/agent` | Agente collegato allo shared game | #4923 |
| `GET` | `/api/v1/admin/shared-games/{id}/kb-cards` | KB cards dello shared game | #4924 |

### Admin — Agent Definitions

| Metodo | Path | Descrizione | Issue |
|--------|------|-------------|-------|
| `PUT` | `/api/v1/admin/agent-definitions/{id}/kb-card-ids` | Aggiorna KB cards dell'agente (JSONB) | #4925 |

### Live Game Session

| Metodo | Path | Descrizione | Issue |
|--------|------|-------------|-------|
| `POST` | `/api/v1/game-sessions` | Crea sessione live | #4749 |
| `GET` | `/api/v1/game-sessions/{id}` | Dettaglio sessione | #4749 |
| `PUT` | `/api/v1/game-sessions/{id}/join` | Unisciti sessione via codice | #4766 |
| `POST` | `/api/v1/game-sessions/{id}/actions` | Azione giocatore | #4765 |
| `GET` | `/api/v1/game-sessions/{id}/state` | Stato corrente sessione | #4749 |
| `GET` | `/api/v1/game-sessions/{id}/snapshots` | Storico snapshot (time travel) | #4755 |
| `GET` | `/api/v1/game-sessions/active` | Sessioni attive | #4749 |
| `GET` | `/api/v1/game-sessions/{id}/sse` | SSE stream stato sessione | #4764 |

### Notifications

| Metodo | Path | Descrizione | Issue |
|--------|------|-------------|-------|
| `POST` | `/api/v1/notifications/processing-jobs` | Notifica multi-channel per eventi elaborazione | #4744 |

### Chat / Agent Proxy

| Metodo | Path | Descrizione | Issue |
|--------|------|-------------|-------|
| `POST` | `/api/chat/stream` | Proxy SSE OpenRouter per streaming chat | #4806 |

---

## Riepilogo Numerico

| Area | Nuovi/Modificati |
|------|-----------------|
| Pagine frontend nuove | ~25 |
| Pagine admin nuove | ~15 |
| Endpoint backend nuovi | ~20 |
| Componenti UI (MeepleCard system) | ~10 |
| Epiche completate | 4 |

**Epiche**: #4789 (RAG Admin), #4820 (Admin SharedGame), #4912 (Custom Agents/Chat), #4757 (Player Journey parziale)

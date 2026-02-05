# MeepleAI Frontend-Backend Gap Analysis

**Data analisi**: 2026-02-05
**Scope**: Pagine UI raggiungibili, endpoint backend, componenti inutilizzati

---

## 📊 Executive Summary

| Metrica | Valore |
|---------|--------|
| **Pagine totali frontend** | 120+ |
| **Pagine raggiungibili via UI** | 26 |
| **Pagine orfane** | 94+ (78%) |
| **Endpoint backend totali** | 468+ |
| **Componenti totali** | 503 |
| **Componenti inutilizzati** | 264 (52.5%) |

---

## 🧭 Pagine Raggiungibili via Navigazione UI

### Header Navigation (Utenti Autenticati)
| Route | Label | Icona |
|-------|-------|-------|
| `/dashboard` | Dashboard | LayoutDashboard |
| `/library` | I Miei Giochi | BookOpen |
| `/chat` | Chat | MessageSquare |
| `/toolkit` | Toolkit | Dice6 |
| `/games` | Catalogo | Gamepad2 |
| `/profile` | Profilo | User |
| `/admin` | Admin | Shield (solo admin) |

### Header Navigation (Utenti Anonimi)
| Route | Label |
|-------|-------|
| `/` | Home |
| `/games` | Catalogo |

### Footer Links
| Sezione | Routes |
|---------|--------|
| About | `/about`, `/how-it-works`, `/blog`, `/contact` |
| Quick Links | `/games`, `/chat`, `/dashboard`, `/faq` |
| Legal | `/privacy`, `/terms`, `/cookies` |

### Auth Routes
| Route | Descrizione |
|-------|-------------|
| `/login` | Login |
| `/register` | Registrazione |
| `/verify-email` | Verifica email |
| `/reset-password` | Reset password |

### Settings & Admin (via Dropdown)
| Route | Accesso |
|-------|---------|
| `/settings` | Utenti autenticati |
| `/admin` | Solo admin |

---

## 🚨 Pagine Orfane (Non Raggiungibili via UI)

### 🔴 Priorità Alta - Admin Panel (50+ pagine)

L'intero admin panel oltre `/admin` è inaccessibile:

```
/admin/agents/test
/admin/agents/test-history
/admin/agent-typologies
/admin/agent-typologies/create
/admin/agent-typologies/[id]/edit
/admin/agent-typologies/pending
/admin/ai-models
/admin/ai-usage
/admin/alert-rules
/admin/alerts
/admin/alerts/config
/admin/analytics
/admin/api-keys
/admin/bulk-export
/admin/cache
/admin/command-center
/admin/config/rate-limits
/admin/configuration
/admin/configuration/game-library-limits
/admin/configuration/pdf-tier-limits
/admin/configuration/pdf-upload-limits
/admin/faqs
/admin/games
/admin/infrastructure
/admin/management
/admin/n8n-templates
/admin/prompts
/admin/prompts/[id]
/admin/prompts/[id]/audit
/admin/prompts/[id]/compare
/admin/prompts/[id]/versions/[versionId]
/admin/prompts/[id]/versions/new
/admin/rag/tier-strategy-config
/admin/redesign
/admin/reports
/admin/services
/admin/sessions
/admin/share-requests
/admin/share-requests/[id]
/admin/shared-games
/admin/shared-games/[id]
/admin/shared-games/add-from-bgg
/admin/shared-games/approval-queue
/admin/shared-games/import
/admin/shared-games/new
/admin/shared-games/pending-approvals
/admin/shared-games/pending-deletes
/admin/testing
/admin/users
/admin/users/[id]
/admin/wizard
```

**Raccomandazione**: Creare sidebar di navigazione interna per admin

### 🟡 Priorità Media - Editor Pages (6 pagine)

```
/editor
/editor/dashboard
/editor/agent-proposals
/editor/agent-proposals/create
/editor/agent-proposals/[id]/edit
/editor/agent-proposals/[id]/test
```

### 🟡 Priorità Media - Feature Pages Non Linkate

```
/board-game-ai
/board-game-ai/ask
/board-game-ai/games
/chess
/contributions
/dashboard/collection
/dashboard/collection/add-game
/design/cards
/poc/agent-chat
/rag
/scraper
/shadcn-demo
```

### 🟢 Priorità Bassa - Pagine Funzionali Non Navigate

```
/giochi/[id]              # Variante italiana (locale)
/library/shared/[token]   # Link condivisione
/oauth-callback           # Callback OAuth
/sessions                 # Vista pubblica sessioni
/sessions/[id]
/sessions/[id]/state
/sessions/history
/shared-games/[id]
/verification-pending
/verification-success
/welcome
/auth/callback
```

---

## 🔌 Gap Analysis: Frontend vs Backend

### ✅ Endpoint Completamente Integrati

| Bounded Context | Copertura | Note |
|-----------------|-----------|------|
| Authentication | 95% | Login, register, OAuth, 2FA funzionanti |
| User Library | 90% | CRUD completo, etichette, condivisione |
| Game Catalog | 85% | Ricerca, dettagli, categorie |
| Chat Sessions | 80% | Thread, messaggi, export |
| User Profile | 85% | Preferenze, badge, attività |

### ⚠️ Endpoint Parzialmente Integrati

| Bounded Context | Copertura | Gap |
|-----------------|-----------|-----|
| Document Processing | 60% | Upload funziona, ma preview/viewer non linkati |
| Session Tracking | 50% | API complete, UI sessions orfana |
| Knowledge Base | 40% | `/chat` usa RAG, ma dashboard RAG orfana |
| Game Sessions | 30% | API complete, toolkit parziale |

### ❌ Endpoint Non Integrati (Backend Disponibile, Frontend Mancante)

| Bounded Context | Endpoint Disponibili | UI Mancante |
|-----------------|---------------------|-------------|
| **Administration** | 100+ endpoint | Admin sidebar non esiste |
| **Workflow Integration** | 15 endpoint n8n | `/n8n` orfana |
| **API Keys** | 10 endpoint CRUD | Gestione API key non accessibile |
| **Analytics** | 20+ endpoint | Dashboard analytics non raggiungibile |
| **Prompt Management** | 15 endpoint | Editor prompts non accessibile |
| **Tier Strategy** | 8 endpoint | Config RAG non accessibile |
| **Share Requests** | 20 endpoint | Workflow approval non navigabile |
| **Agent Typologies** | 12 endpoint | Config agenti non accessibile |

---

## 🗑️ Componenti Inutilizzati (264 totali)

### 🔴 Cartelle Interamente Inutilizzate (Eliminazione Immediata)

| Cartella | Componenti | Motivazione |
|----------|------------|-------------|
| `ai/` | 3 | AILoadingIndicator, QuickQuestion* mai importati |
| `metrics/` | 1 | MetricsChart non usato |
| `question/` | 1 | QuestionInputForm non usato |
| `response/` | 1 | ResponseCard non usato |
| `rate-limit/` | 4 | Rate limit UI non implementata |
| `sessions/` | 1 | SessionQuotaBar non usato |
| `testing/` | 1 | TestingTrendChart non usato |
| `user/` | 1 | PersonalUsageCard non usato |

### 🟡 Sottosistemi Admin Inutilizzati (37 componenti)

```
admin/agents/              # 7 componenti
admin/agent-typologies/    # 5 componenti
admin/alert-rules/         # 3 componenti
admin/charts/              # 3 componenti
admin/shared-games/        # 8 componenti
admin/config/              # 11 componenti
```

### 🟡 RAG Dashboard Completo (47 componenti)

Intero sottosistema non integrato nella navigazione:
```
components/rag-dashboard/config/
components/rag-dashboard/retrieval-strategies/
components/rag-dashboard/metrics/
+ 32 altri componenti dashboard
```

### 🟡 Sistema Chat (26 componenti non usati)

```
ChatContent, ChatHeader, ChatHistory, ChatHistoryItem
ChatSessionCard, ChatSidebar, CommentBox, ContextChip
DocumentSourceSelector, FollowUpQuestions, GameSelector
MentionInput, Message, MessageActions, MessageEditForm
MessageInput, MessageList, VirtualizedMessageList
MobileSidebar, ShareChatModal, ThreadListItem, AgentSelector
```

### 🟢 Altri Componenti Inutilizzati

| Categoria | Count | Esempi |
|-----------|-------|--------|
| PDF System | 11 | PdfPreview, PdfViewer, PdfUploadForm |
| Games Detail | 9 | GameCommunityTab, GameRulesTab |
| Agent Config | 13 | AgentChatPanel, ConfigPanel |
| Collection | 5 | AddGameWizard, SearchSelectGame |
| Landing | 6 | HeroSection, FeaturesSection |
| Layout/Navbar | 12 | GlobalSearch, vecchia navigation |
| UI Primitives | 39 | animations, feedback, forms |

---

## 📋 Raccomandazioni

### Azioni Immediate

1. **Creare Admin Sidebar** - 50+ pagine admin inaccessibili
   - Aggiungere navigazione laterale in `/admin`
   - Organizzare per sezione: Users, Games, Config, Analytics

2. **Cleanup Dead Code** - 264 componenti (52.5%)
   - Eliminare cartelle completamente inutilizzate
   - Valutare sottosistemi RAG/Chat per futuro uso

3. **Collegare Pagine Funzionali**
   - `/editor` → Aggiungere link per utenti editor
   - `/contributions` → Link in profilo utente

### Azioni a Medio Termine

4. **Integrare Dashboard Orfane**
   - RAG Dashboard → Link in admin
   - Analytics → Link in admin
   - n8n Templates → Link in admin

5. **Consolidare Sistema Chat**
   - 26 componenti inutilizzati → Valutare refactoring
   - Unificare con chat attivo in `/chat`

6. **Completare Game Sessions**
   - `/toolkit` funziona ma sessions orfane
   - Collegare `/sessions` a toolkit

### Metriche Target

| Metrica | Attuale | Target |
|---------|---------|--------|
| Pagine raggiungibili | 26 (22%) | 80+ (67%) |
| Componenti usati | 239 (47.5%) | 350+ (70%) |
| Gap frontend-backend | 40% | <15% |

---

## 📁 File Generato

Questo documento è stato generato automaticamente dall'analisi del codebase.
Per aggiornamenti, rieseguire l'analisi con `/sc:brainstorm`.

# UI Flow Coverage Analysis - MeepleAI

## Executive Summary

**Analisi completata**: 2026-01-21
**Frontend Routes**: ~65 pagine
**Backend Endpoints**: ~343 API
**User Types**: Guest, User, Admin

---

## 1. Matrice Routes per Tipo Utenza

### 🔓 Guest (Non Autenticato)

| Route | Descrizione | Navigazione |
|-------|-------------|-------------|
| `/` | Landing page | Direct URL |
| `/login` | Form login | Header "Accedi" button |
| `/register` | Registrazione | Link da /login |
| `/reset-password` | Reset password | Link da /login |
| `/oauth-callback` | OAuth callback | Auto-redirect da provider |
| `/board-game-ai` | Landing AI assistant | Footer/Marketing link |
| `/board-game-ai/ask` | Chat pubblica AI | CTA da /board-game-ai |
| `/board-game-ai/games` | Giochi supportati | Nav da /board-game-ai |
| `/games/catalog` | Catalogo pubblico | Header nav (se visibile) |
| `/library/shared/[token]` | Libreria condivisa | Link esterno |
| `/shared` | Chat condivisa | Link esterno con token |
| `/shadcn-demo` | Component showcase | Dev only |
| `/versions` | Version info | Footer |

### 🔐 User (Autenticato Standard)

| Route | Descrizione | Navigazione |
|-------|-------------|-------------|
| `/dashboard` | Dashboard principale | Post-login default, Header nav |
| `/games` | Libreria giochi personale | Header "Giochi" |
| `/games/[id]` | Dettagli gioco | Click da /games list |
| `/games/add` | Aggiungi gioco | Button da /games |
| `/giochi/[id]` | Dettagli gioco (IT alias) | SEO routes |
| `/library` | Libreria personale | Header "Libreria" |
| `/chat` | Chat AI | Header "Chat" |
| `/sessions` | Sessioni gioco | Dashboard widget / Nav |
| `/sessions/[id]` | Dettagli sessione | Click da /sessions list |
| `/sessions/[id]/state` | Stato gioco | Button da sessione |
| `/sessions/history` | Cronologia sessioni | Tab da /sessions |
| `/settings` | Impostazioni account | User dropdown menu |
| `/upload` | Upload PDF | Button da /games/[id] |
| `/editor` | Editor interfaccia | Feature specifica |

### 🛡️ Admin (Amministratore)

| Route | Descrizione | Navigazione |
|-------|-------------|-------------|
| `/admin` | Dashboard admin | Sidebar link |
| `/admin/infrastructure` | Monitoring infra | Sidebar |
| `/admin/services` | Stato servizi | Sidebar |
| `/admin/cache` | Cache management | Sidebar |
| `/admin/testing` | Testing utilities | Sidebar |
| `/admin/users` | Gestione utenti | Sidebar |
| `/admin/management` | User management | Sidebar |
| `/admin/configuration` | System config | Sidebar |
| `/admin/configuration/game-library-limits` | Limiti libreria | Sub-nav config |
| `/admin/alerts` | Alert attivi | Sidebar (badge count) |
| `/admin/alert-rules` | Regole alert | Sub-nav alerts |
| `/admin/alerts/config` | Config alert system | Sub-nav alerts |
| `/admin/analytics` | Analytics | Sidebar |
| `/admin/reports` | Report | Sidebar |
| `/admin/prompts` | Prompt templates | Sidebar |
| `/admin/prompts/[id]` | Dettagli prompt | Click da list |
| `/admin/prompts/[id]/versions/new` | Nuova versione | Button da prompt |
| `/admin/prompts/[id]/versions/[versionId]` | Versione specifica | Click da versions |
| `/admin/prompts/[id]/compare` | Confronto versioni | Button da prompt |
| `/admin/prompts/[id]/audit` | Audit prompt | Tab da prompt |
| `/admin/ai-models` | Gestione AI models | Sidebar |
| `/admin/api-keys` | API keys | Sidebar |
| `/admin/n8n-templates` | N8N templates | Sidebar |
| `/admin/games` | Gestione giochi | Sidebar |
| `/admin/shared-games` | Catalogo condiviso | Sidebar |
| `/admin/shared-games/new` | Nuovo gioco | Button da list |
| `/admin/shared-games/add-from-bgg` | Import BGG | Button da list |
| `/admin/shared-games/import` | Bulk import | Button da list |
| `/admin/shared-games/[id]` | Dettagli gioco | Click da list |
| `/admin/shared-games/pending-deletes` | Soft-delete review | Tab/Badge |
| `/admin/faqs` | Gestione FAQ | Sidebar |
| `/admin/bulk-export` | Export dati | Sidebar |
| `/admin/wizard` | Setup wizard | Initial setup |

---

## 2. Gap Analysis: API senza UI

### 🔴 API Critiche senza Pagina UI Dedicata

| Endpoint | Metodo | Descrizione | Azione Suggerita |
|----------|--------|-------------|------------------|
| `GET /admin/shared-games/pending-approvals` | GET | Lista approvazioni in sospeso | ⚠️ **Creare tab/vista** in /admin/shared-games |
| `POST /admin/shared-games/{id}/approve-publication` | POST | Approva pubblicazione | Integrato in pending-approvals |
| `POST /admin/shared-games/{id}/reject-publication` | POST | Rifiuta pubblicazione | Integrato in pending-approvals |
| `POST /admin/shared-games/{id}/archive` | POST | Archivia gioco | ⚠️ **Aggiungere button** in dettagli gioco |
| `GET /admin/users/{userId}/activity` | GET | Timeline attività utente | ⚠️ **Creare vista** in /admin/users/[id] |
| `POST /admin/users/bulk/password-reset` | POST | Reset password multipli | ⚠️ **Creare bulk actions** in /admin/users |
| `POST /admin/users/bulk/role-change` | POST | Cambio ruolo multiplo | Integrato in bulk actions |
| `POST /admin/users/bulk/import` | POST | Import utenti CSV | ⚠️ **Creare import wizard** |
| `GET /admin/users/bulk/export` | GET | Export utenti CSV | ⚠️ **Aggiungere button** in /admin/users |
| `GET /admin/api-keys/stats` | GET | Stats globali API keys | ⚠️ **Creare dashboard stats** in /admin/api-keys |
| `GET /admin/api-keys/bulk/export` | GET | Export API keys | Aggiungere button |
| `POST /admin/api-keys/bulk/import` | POST | Import API keys | Aggiungere wizard |
| `GET /admin/sessions` | GET | Lista sessioni globali | ⚠️ **Creare pagina** /admin/sessions |
| `GET /admin/sessions/{sessionId}` | GET | Dettagli sessione | Sub-page |
| `GET /admin/users/{userId}/sessions` | GET | Sessioni utente | Integrare in user detail |
| `GET /admin/workflows/errors` | GET | Errori workflow | ⚠️ **Creare vista** in /admin/n8n-templates |
| `POST /chess/index`, `GET /chess/search` | POST/GET | Chess integration | ⚠️ **Completare pagina** /chess |

### 🟡 API con UI Parziale

| Endpoint | Stato UI | Note |
|----------|----------|------|
| `POST /admin/shared-games/{id}/quick-questions/generate` | ⚠️ Parziale | Button presente ma senza feedback adeguato |
| `POST /admin/shared-games/{id}/state-template/generate` | ⚠️ Parziale | Workflow incompleto |
| `GET /admin/configurations/history` | ⚠️ Mancante | History delle config non visualizzata |
| `POST /admin/configurations/{id}/rollback/{version}` | ⚠️ Mancante | Rollback non accessibile da UI |
| `GET /games/{id}/rulespec/diff` | ⚠️ Mancante | Diff viewer non implementato |

### 🟢 API Correttamente Mappate

- ✅ Autenticazione (login, register, OAuth, 2FA)
- ✅ Chat threads e messaggi
- ✅ Shared games CRUD base
- ✅ User library management
- ✅ Notifications
- ✅ Feature flags toggle
- ✅ Prompts management (versioni, compare, audit)

---

## 3. Gap Analysis: UI senza API Backend

| Route UI | Stato Backend | Note |
|----------|---------------|------|
| `/scraper` | ❓ Non trovato | Pagina utility senza endpoint chiaro |
| `/n8n` | ⚠️ Parziale | Endpoint workflow esistono ma routing confuso |
| `/admin/reports` | ⚠️ Parziale | `/generate`, `/schedule` esistono ma non `/admin/reports` list |

---

## 4. Flussi di Navigazione Critici

### Flow 1: Guest → User Registration
```
/ (Landing)
  └─ Click "Accedi" → /login
      ├─ Click "Registrati" → /register
      │    └─ Submit form → POST /auth/register → /dashboard
      ├─ Click "Google" → OAuth flow → /oauth-callback → /dashboard
      └─ Submit login → POST /auth/login → /dashboard
```

### Flow 2: User → Game Session
```
/dashboard
  └─ Click "Giochi" → /games
      └─ Click game card → /games/[id]
          └─ Click "Nuova Sessione" → POST /sessions → /sessions/[id]
              ├─ Click "Stato" → /sessions/[id]/state
              ├─ Click "Pausa" → POST /sessions/[id]/pause
              └─ Click "Termina" → POST /sessions/[id]/complete → /sessions
```

### Flow 3: User → Chat AI
```
/dashboard
  └─ Click "Chat" → /chat
      ├─ Click "+" → POST /chat-threads → New thread
      ├─ Select thread → GET /chat-threads/[id]
      ├─ Type message → POST /chat-threads/[id]/messages (SSE)
      └─ Click "Condividi" → POST /share-links → Copy link
```

### Flow 4: Admin → Game Catalog Management
```
/admin
  └─ Click "Shared Games" → /admin/shared-games
      ├─ Click "+" → /admin/shared-games/new
      │    └─ Submit → POST /admin/shared-games → /admin/shared-games/[id]
      ├─ Click "Import BGG" → /admin/shared-games/add-from-bgg
      │    └─ Search → GET /admin/shared-games/bgg/search
      │    └─ Import → POST /admin/shared-games/import-bgg
      ├─ Click game → /admin/shared-games/[id]
      │    ├─ Tab "FAQ" → GET /admin/shared-games/[id]/faq
      │    ├─ Tab "Documenti" → GET /admin/shared-games/[id]/documents
      │    └─ Click "Submit for Approval" → POST .../submit-for-approval
      └─ Tab "Pending Deletes" → /admin/shared-games/pending-deletes
           └─ Click "Approve" → POST .../approve-delete/[requestId]
```

### Flow 5: Admin → User Management
```
/admin
  └─ Click "Users" → /admin/users
      ├─ Click user → [MISSING: /admin/users/[id]]
      │    └─ Tab "Activity" → GET /admin/users/[userId]/activity
      ├─ Click "Export" → GET /admin/users/bulk/export
      └─ Click "Bulk Actions" → [MISSING: bulk action modal]
```

---

## 5. Elementi UI Cliccabili per Navigazione

### Header (PublicHeader)
| Elemento | Tipo | Target | Visibile per |
|----------|------|--------|--------------|
| Logo MeepleAI | Link | `/` | All |
| Home | NavLink | `/` | All |
| Giochi | NavLink | `/games` | User+ |
| Libreria | NavLink | `/library` | User+ |
| Chat | NavLink | `/chat` | User+ |
| Dashboard | NavLink | `/dashboard` | User+ |
| Accedi | Button | `/login` | Guest |
| User Avatar | Dropdown trigger | - | User+ |
| → Profilo | DropdownItem | `/settings` | User+ |
| → Dashboard | DropdownItem | `/dashboard` | User+ |
| → Logout | DropdownItem | API call | User+ |
| Theme Toggle | Button | Local state | All |

### Admin Sidebar (AdminSidebar)
| Elemento | Tipo | Target | Badge |
|----------|------|--------|-------|
| Dashboard | SidebarLink | `/admin` | - |
| Infrastructure | SidebarLink | `/admin/infrastructure` | - |
| Users | SidebarLink | `/admin/users` | Count |
| API Keys | SidebarLink | `/admin/api-keys` | - |
| Alerts | SidebarLink | `/admin/alerts` | Destructive |
| Analytics | SidebarLink | `/admin/analytics` | - |
| Testing | SidebarLink | `/admin/testing` | - |
| Configuration | SidebarLink | `/admin/configuration` | - |
| Cache | SidebarLink | `/admin/cache` | - |
| Prompts | SidebarLink | `/admin/prompts` | - |
| N8N Templates | SidebarLink | `/admin/n8n-templates` | - |
| Shared Games | SidebarLink | `/admin/shared-games` | - |
| Bulk Export | SidebarLink | `/admin/bulk-export` | - |

---

## 6. Test Coverage Matrix

### Priorità 1: Flussi Critici (Must Test)
| ID | Flow | User Type | Steps |
|----|------|-----------|-------|
| TC-001 | Registration complete | Guest→User | /register → form → /dashboard |
| TC-002 | Login email/password | Guest→User | /login → form → /dashboard |
| TC-003 | Login OAuth Google | Guest→User | /login → OAuth → /dashboard |
| TC-004 | Session expiry warning | User | Active → 5min warning → extend/logout |
| TC-005 | Start game session | User | /games/[id] → new session → /sessions/[id] |
| TC-006 | Chat AI conversation | User | /chat → new thread → send message |
| TC-007 | Admin game approval | Admin | /admin/shared-games → approve → published |

### Priorità 2: Flussi Importanti (Should Test)
| ID | Flow | User Type | Steps |
|----|------|-----------|-------|
| TC-010 | Add game to library | User | /games/catalog → add → /library |
| TC-011 | Upload game PDF | User | /games/[id] → upload → processed |
| TC-012 | Share chat thread | User | /chat → share → copy link |
| TC-013 | Admin user management | Admin | /admin/users → view → activity |
| TC-014 | Admin config change | Admin | /admin/configuration → edit → save |
| TC-015 | Password reset flow | Guest | /reset-password → email → new password |

### Priorità 3: Flussi Complementari (Nice to Test)
| ID | Flow | User Type | Steps |
|----|------|-----------|-------|
| TC-020 | 2FA setup | User | /settings → 2FA → QR → verify |
| TC-021 | API key management | User | /settings → API keys → create → rotate |
| TC-022 | Notification read | User | Bell → dropdown → mark read |
| TC-023 | Theme toggle | All | Header → toggle → persisted |
| TC-024 | Mobile navigation | All | Hamburger → drawer → navigate |

---

## 7. Raccomandazioni

### 🔴 Azioni Immediate (High Priority)

1. **Creare `/admin/shared-games/pending-approvals`**
   - Vista dedicata per workflow approvazione
   - Integra approve/reject buttons
   - Badge count in sidebar

2. **Creare `/admin/users/[id]` con activity timeline**
   - Dettaglio utente con tabs
   - Activity timeline integrata
   - Bulk actions accessible

3. **Creare `/admin/sessions` per monitoring**
   - Lista sessioni globali
   - Filtri per stato, utente, gioco
   - Link a dettaglio sessione

### 🟡 Azioni Medio Termine (Medium Priority)

4. **Completare bulk actions in /admin/users**
   - Modal per selezione multipla
   - Actions: password reset, role change
   - Import/Export wizard

5. **Aggiungere diff viewer per RuleSpec**
   - Compare versioni side-by-side
   - Highlight changes
   - Rollback capability

6. **Completare /chess page**
   - UI per indexing
   - Search interface
   - Results visualization

### 🟢 Azioni Future (Low Priority)

7. **Migliorare feedback generation endpoints**
   - Progress indicators
   - Status polling
   - Result preview

8. **Documentare /scraper e /n8n flows**
   - Chiarire use cases
   - Implementare UI mancante se necessario

---

## 8. Test Playwright Proposti

```typescript
// tests/e2e/flows/
├── auth/
│   ├── registration.spec.ts     // TC-001
│   ├── login-email.spec.ts      // TC-002
│   ├── login-oauth.spec.ts      // TC-003
│   └── session-expiry.spec.ts   // TC-004
├── games/
│   ├── start-session.spec.ts    // TC-005
│   ├── add-to-library.spec.ts   // TC-010
│   └── upload-pdf.spec.ts       // TC-011
├── chat/
│   ├── conversation.spec.ts     // TC-006
│   └── share-thread.spec.ts     // TC-012
├── admin/
│   ├── game-approval.spec.ts    // TC-007
│   ├── user-management.spec.ts  // TC-013
│   └── config-change.spec.ts    // TC-014
└── navigation/
    ├── guest-flows.spec.ts
    ├── user-flows.spec.ts
    └── admin-flows.spec.ts
```

---

## Appendix: Complete Route → API Mapping

See attached spreadsheet or run:
```bash
pnpm test:coverage-matrix
```

# Flussi Utente MeepleAI - Documentazione Completa

**Versione**: 2.0
**Data**: 2025-12-14
**Autore**: Frontend Architecture Team
**Stack**: Next.js 16, React 19, ASP.NET 9, PostgreSQL, Qdrant, Redis

---

## Indice

1. [Panoramica Sistema](#1-panoramica-sistema)
2. [Personas Utente](#2-personas-utente)
3. [Flussi Pubblici (Guest)](#3-flussi-pubblici-guest)
4. [Flussi Autenticazione](#4-flussi-autenticazione)
5. [Flussi Utente Registrato](#5-flussi-utente-registrato)
6. [Flussi Admin](#6-flussi-admin)
7. [Flussi Document Processing](#7-flussi-document-processing)
8. [Edge Cases & Error States](#8-edge-cases--error-states)
9. [Performance & Metriche](#9-performance--metriche)

---

## 1. Panoramica Sistema

MeepleAI è un'applicazione AI per assistenza alle regole dei giochi da tavolo con architettura DDD basata su 7 Bounded Contexts:

```
┌─────────────────────────────────────────────────────────────────┐
│                        MeepleAI Frontend                         │
│                   (Next.js 16 + React 19)                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Landing Page → Auth → Dashboard → Games → Chat → Admin         │
│       ↓           ↓         ↓         ↓       ↓        ↓        │
│    Public     Session   Overview  Catalog   RAG    Analytics    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                      Backend API (ASP.NET 9)                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Authentication │ GameManagement │ KnowledgeBase │ Admin        │
│  DocumentProc   │ Workflow       │ SystemConfig  │              │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│              Infrastructure (Docker Compose)                     │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  PostgreSQL │ Qdrant │ Redis │ n8n │ Grafana │ OpenRouter       │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Architettura Frontend

```
apps/web/src/
├── app/                          # Next.js App Router (SSR/SSG)
│   ├── page.tsx                  # Landing Page (Marketing)
│   ├── dashboard/                # Dashboard utente autenticato
│   ├── giochi/[id]/              # Dettaglio gioco
│   ├── chat/                     # Interfaccia chat RAG
│   ├── admin/                    # Panel amministrazione
│   └── settings/                 # Impostazioni profilo
├── components/                   # Componenti UI riutilizzabili
│   ├── landing/                  # Hero, Features, Footer
│   ├── auth/                     # Login, Register, OAuth, 2FA
│   ├── chat/                     # MessageList, Input, GameSelector
│   ├── admin/                    # Dashboard, Metrics, Charts
│   └── ui/                       # Shadcn/UI components
└── lib/api/                      # Client API (httpClient + schemas)
    ├── clients/                  # authClient, gamesClient, chatClient
    ├── schemas/                  # Zod validation schemas
    └── core/                     # httpClient, metrics, errors
```

---

## 2. Personas Utente

### 2.1 Guest (Visitatore Anonimo)
**Obiettivo**: Scoprire il servizio e registrarsi
**Permessi**: Landing page, registrazione, login
**Tecnologia**: Pagine pubbliche SSR/SSG per SEO

### 2.2 User (Utente Registrato)
**Obiettivo**: Ottenere risposte alle regole dei giochi
**Permessi**: Dashboard, Games Catalog, Chat RAG, Settings
**Tecnologia**: Session-based auth (cookie httpOnly)

### 2.3 Editor
**Obiettivo**: Gestire contenuti e giochi
**Permessi**: User + Upload PDF, Edit RuleSpec, FAQ Management
**Tecnologia**: Role-based access control (RBAC)

### 2.4 Admin
**Obiettivo**: Monitorare sistema e configurare piattaforma
**Permessi**: Editor + Dashboard Admin, Analytics, User Management, System Config
**Tecnologia**: Triple-layer auth (middleware + RequireRole + API)

---

## 3. Flussi Pubblici (Guest)

### 3.1 Landing Page Flow

```
┌─────────────┐
│   Browser   │
│  (Desktop/  │
│   Mobile)   │
└──────┬──────┘
       │
       │ GET /
       ↓
┌─────────────────────────────────────────┐
│         Landing Page (SSR)               │
│  ┌────────────────────────────────────┐ │
│  │     HeroSection                    │ │
│  │  • MeepleAvatar (animated)         │ │
│  │  • CTA: "Inizia Gratis" → /register│ │
│  │  • CTA: "Scopri di più" → scroll  │ │
│  └────────────────────────────────────┘ │
│  ┌────────────────────────────────────┐ │
│  │     FeaturesSection                │ │
│  │  • RAG con citazioni               │ │
│  │  • Migliaia di giochi              │ │
│  │  • Mobile-first design             │ │
│  └────────────────────────────────────┘ │
│  ┌────────────────────────────────────┐ │
│  │     HowItWorksSection              │ │
│  │  1. Scegli il gioco                │ │
│  │  2. Fai una domanda                │ │
│  │  3. Ottieni risposta + citazioni   │ │
│  └────────────────────────────────────┘ │
│  ┌────────────────────────────────────┐ │
│  │     LandingFooter                  │ │
│  │  • CTA finale: "Registrati ora"    │ │
│  │  • Link: Privacy, Terms, Docs      │ │
│  └────────────────────────────────────┘ │
└─────────────────────────────────────────┘
       │
       │ User clicks "Inizia Gratis"
       ↓
┌─────────────┐
│  /register  │
└─────────────┘
```

**Componenti Chiave**:
- `apps/web/src/app/page.tsx` (Server Component)
- `apps/web/src/components/landing/HeroSection.tsx` (Client)
- `apps/web/src/components/landing/FeaturesSection.tsx` (Client)

**SEO Features**:
- Metadata completi (Open Graph, Twitter Card)
- Structured data (JSON-LD Schema.org)
- Semantic HTML5
- Performance: Lighthouse ≥90

### 3.2 AuthRedirect Flow

```
┌──────────────────┐
│  Landing Page    │
│  (useEffect)     │
└────────┬─────────┘
         │
         │ Check auth state
         ↓
  ┌──────────────┐
  │ Authenticated?│
  └──────┬───────┘
         │
    ┌────┴────┐
    │         │
   YES       NO
    │         │
    │         └─────→ Stay on landing page
    │                 Show CTAs
    ↓
┌───────────────┐
│ Auto-redirect │
│  to /dashboard│
└───────────────┘
```

**Implementazione**:
- `apps/web/src/components/landing/AuthRedirect.tsx`
- `useCurrentUser()` hook (TanStack Query)
- Redirect immediato per UX migliorata

---

## 4. Flussi Autenticazione

### 4.1 Registration Flow

```
┌──────────────┐
│  /register   │
│  (page.tsx)  │
└──────┬───────┘
       │
       │ Render RegisterForm
       ↓
┌─────────────────────────────────────────┐
│     RegisterForm Component               │
│  ┌───────────────────────────────────┐  │
│  │  Email:    [____________]         │  │
│  │  Password: [____________]         │  │
│  │  Confirm:  [____________]         │  │
│  │                                   │  │
│  │  ☐ Accept Terms & Privacy Policy │  │
│  │                                   │  │
│  │  [Registrati]                     │  │
│  └───────────────────────────────────┘  │
│                                          │
│  OR                                      │
│                                          │
│  ┌───────────────────────────────────┐  │
│  │  OAuth Providers:                 │  │
│  │  [ Google ]  [ GitHub ]           │  │
│  │  [ Discord ]                      │  │
│  └───────────────────────────────────┘  │
└────────────┬─────────────────────────────┘
             │
             │ Submit form
             ↓
┌─────────────────────────────────────────┐
│   POST /api/v1/auth/register            │
│   {                                     │
│     email: "user@example.com",          │
│     password: "SecurePass123!",         │
│     confirmPassword: "SecurePass123!"   │
│   }                                     │
└────────────┬─────────────────────────────┘
             │
             │ Backend validation
             │ • Email unique?
             │ • Password strength?
             │ • Terms accepted?
             ↓
       ┌─────────────┐
       │  Success?   │
       └─────┬───────┘
             │
        ┌────┴────┐
        │         │
       YES       NO
        │         │
        │         └──→ Show error message
        │              • Email exists
        │              • Weak password
        │              • Validation failed
        ↓
┌───────────────────┐
│  Create session   │
│  Set cookie       │
│  (httpOnly,       │
│   secure)         │
└────────┬──────────┘
         │
         │ Auto-redirect
         ↓
┌───────────────────┐
│   /dashboard      │
│   (Welcome!)      │
└───────────────────┘
```

**Componenti Chiave**:
- `apps/web/src/components/auth/RegisterForm.tsx`
- `apps/web/src/lib/api/clients/authClient.ts`
- Backend: `Authentication/Application/Register/RegisterCommandHandler.cs`

**Validazioni Frontend** (Zod Schema):
```typescript
const registerSchema = z.object({
  email: z.string().email('Email non valida'),
  password: z.string()
    .min(8, 'Minimo 8 caratteri')
    .regex(/[A-Z]/, 'Almeno una maiuscola')
    .regex(/[a-z]/, 'Almeno una minuscola')
    .regex(/[0-9]/, 'Almeno un numero')
    .regex(/[^A-Za-z0-9]/, 'Almeno un simbolo'),
  confirmPassword: z.string(),
}).refine(data => data.password === data.confirmPassword, {
  message: 'Le password non coincidono',
  path: ['confirmPassword'],
});
```

### 4.2 Login Flow (Cookie-based)

```
┌──────────────┐
│   /login     │
│  (page.tsx)  │
└──────┬───────┘
       │
       │ Render LoginForm
       ↓
┌─────────────────────────────────────────┐
│        LoginForm Component               │
│  ┌───────────────────────────────────┐  │
│  │  Email:    [____________]         │  │
│  │  Password: [____________]         │  │
│  │                                   │  │
│  │  ☐ Ricordami                      │  │
│  │                                   │  │
│  │  [Accedi]                         │  │
│  │                                   │  │
│  │  Forgot password?                 │  │
│  └───────────────────────────────────┘  │
└────────────┬─────────────────────────────┘
             │
             │ POST /api/v1/auth/login
             ↓
┌─────────────────────────────────────────┐
│   Authentication Flow                    │
│                                          │
│  1. Validate credentials                │
│     • Email exists?                     │
│     • Password correct (PBKDF2)?        │
│                                          │
│  2. Create session                      │
│     • Generate session token            │
│     • Store in PostgreSQL               │
│                                          │
│  3. Set cookie                          │
│     • httpOnly: true                    │
│     • secure: true (HTTPS)              │
│     • sameSite: 'lax'                   │
│     • maxAge: 7 days (remember me)      │
│                                          │
└────────────┬─────────────────────────────┘
             │
             │ Success
             ↓
┌──────────────────────────────┐
│  Return user data + session  │
│  {                           │
│    user: {                   │
│      id, email, role         │
│    },                        │
│    session: { ... }          │
│  }                           │
└────────────┬─────────────────┘
             │
             │ Client-side redirect
             ↓
┌──────────────────────────────┐
│      /dashboard              │
│  (authenticated state)       │
└──────────────────────────────┘
```

**Error Handling**:
```
Login Failed
├── Invalid email → "Email non trovata"
├── Wrong password → "Password errata"
├── Account locked → "Account bloccato (troppi tentativi)"
└── Server error → "Errore di sistema, riprova"
```

### 4.3 OAuth Flow (Google/GitHub/Discord)

```
┌──────────────────┐
│  User clicks     │
│  "Login with     │
│   Google"        │
└────────┬─────────┘
         │
         │ GET /api/v1/auth/oauth/google
         ↓
┌──────────────────────────────────────────┐
│  Backend: Generate OAuth URL             │
│  • redirect_uri = /oauth-callback        │
│  • scope = email, profile                │
│  • state = CSRF token                    │
└────────────┬──────────────────────────────┘
             │
             │ Redirect to provider
             ↓
┌──────────────────────────────────────────┐
│     Google OAuth Consent Screen          │
│  ┌────────────────────────────────────┐  │
│  │  MeepleAI wants to:                │  │
│  │  ☑ See your email                  │  │
│  │  ☑ See your basic info             │  │
│  │                                    │  │
│  │  [Cancel]  [Allow]                 │  │
│  └────────────────────────────────────┘  │
└────────────┬──────────────────────────────┘
             │
             │ User approves
             ↓
┌──────────────────────────────────────────┐
│  Redirect to /oauth-callback             │
│  ?code=AUTH_CODE&state=CSRF_TOKEN        │
└────────────┬──────────────────────────────┘
             │
             │ Backend processes
             ↓
┌──────────────────────────────────────────┐
│  OAuth Callback Handler                  │
│                                           │
│  1. Validate state (CSRF check)          │
│  2. Exchange code for access token       │
│  3. Fetch user profile from provider     │
│  4. Check if user exists                 │
│     ├─ YES → Update profile              │
│     └─ NO → Create new user              │
│  5. Link OAuth account to user           │
│  6. Create session                       │
│  7. Set cookie                           │
└────────────┬──────────────────────────────┘
             │
             │ Success
             ↓
┌──────────────────────────────┐
│      /dashboard              │
│  (authenticated)             │
└──────────────────────────────┘
```

**OAuth Providers Supportati**:
- Google (primary)
- GitHub
- Discord

**Database Schema** (OAuthAccount):
```sql
CREATE TABLE OAuthAccounts (
  Id UUID PRIMARY KEY,
  UserId UUID REFERENCES Users(Id),
  Provider VARCHAR(50),      -- 'Google', 'GitHub', 'Discord'
  ProviderUserId VARCHAR(255),
  Email VARCHAR(255),
  AccessToken TEXT,          -- Encrypted
  RefreshToken TEXT,         -- Encrypted
  ExpiresAt TIMESTAMPTZ,
  CreatedAt TIMESTAMPTZ
);
```

### 4.4 Two-Factor Authentication (2FA) Flow

```
┌──────────────────┐
│   User logs in   │
│   with email/pwd │
└────────┬─────────┘
         │
         │ POST /api/v1/auth/login
         ↓
┌────────────────────────────┐
│  Credentials valid?        │
└────────┬───────────────────┘
         │
         │ YES
         ↓
┌────────────────────────────┐
│  2FA enabled for user?     │
└────────┬───────────────────┘
         │
    ┌────┴────┐
    │         │
   YES       NO
    │         │
    │         └──→ Create full session
    │              Set cookie
    │              → /dashboard
    ↓
┌────────────────────────────────────────┐
│  Return partial session                │
│  {                                     │
│    requires2FA: true,                  │
│    tempToken: "...",                   │
│  }                                     │
└────────────┬───────────────────────────┘
             │
             │ Client-side redirect
             ↓
┌────────────────────────────────────────┐
│     /auth/2fa-verify                   │
│  ┌──────────────────────────────────┐  │
│  │  Enter 6-digit code:             │  │
│  │  [___] [___] [___] [___] [___]  │  │
│  │                                  │  │
│  │  [Verify]                        │  │
│  │                                  │  │
│  │  Use backup code?                │  │
│  └──────────────────────────────────┘  │
└────────────┬───────────────────────────┘
             │
             │ POST /api/v1/auth/2fa/verify
             │ { tempToken, code }
             ↓
┌────────────────────────────────────────┐
│  Backend validation                    │
│                                        │
│  1. Verify tempToken                  │
│  2. Get TOTP secret from DB           │
│  3. Validate 6-digit code             │
│     • Time-based (30s window)         │
│     • Allow ±1 window for clock skew  │
│                                        │
│  4. If valid:                         │
│     • Create full session             │
│     • Set cookie                      │
│     • Invalidate tempToken            │
└────────────┬───────────────────────────┘
             │
             │ Success
             ↓
┌──────────────────────────────┐
│      /dashboard              │
│  (fully authenticated)       │
└──────────────────────────────┘
```

**2FA Setup Flow**:
```
/settings/security → Enable 2FA

1. Generate TOTP secret
2. Show QR code (otpauth://...)
3. User scans with Google Authenticator/Authy
4. User enters verification code
5. Backend validates code
6. Generate 10 backup codes
7. Store encrypted secret in DB
8. Enable 2FA for user
```

**Backup Codes Flow**:
```
If TOTP code fails:
→ Click "Use backup code"
→ Enter one of 10 backup codes
→ Backend validates and invalidates used code
→ Success → full session
```

---


## 5. Flussi Utente Registrato

### 5.1 Dashboard Flow

```
┌──────────────────┐
│  User logged in  │
│  (has session)   │
└────────┬─────────┘
         │
         │ Navigate to /dashboard
         ↓
┌─────────────────────────────────────────────────────────┐
│              Dashboard Page (Client)                     │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  TopNav (Desktop) / BottomNav (Mobile)             │ │
│  │  • Logo                                            │ │
│  │  • Navigation: Dashboard, Games, Chat, Settings   │ │
│  │  • User Avatar + Dropdown                         │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  GreetingSection                                   │ │
│  │  "Ciao, {username}! 👋"                            │ │
│  │  "Benvenuto nella tua dashboard MeepleAI"         │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  RecentGamesSection                                │ │
│  │  "Giochi recenti" (6 cards, 2x3 grid)             │ │
│  │  ┌────────┐  ┌────────┐  ┌────────┐               │ │
│  │  │ Game 1 │  │ Game 2 │  │ Game 3 │               │ │
│  │  │ [img]  │  │ [img]  │  │ [img]  │               │ │
│  │  └────────┘  └────────┘  └────────┘               │ │
│  │  ┌────────┐  ┌────────┐  ┌────────┐               │ │
│  │  │ Game 4 │  │ Game 5 │  │ Game 6 │               │ │
│  │  └────────┘  └────────┘  └────────┘               │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  ChatHistorySection                                │ │
│  │  "Chat recenti" (5 threads)                       │ │
│  │  • Thread 1: "Catan - Regole commercio"          │ │
│  │  • Thread 2: "7 Wonders - Setup 3 giocatori"     │ │
│  │  • Thread 3: "Pandemic - Epidemie"               │ │
│  │  • Thread 4: "Scythe - Movimento unità"          │ │
│  │  • Thread 5: "Gloomhaven - Livello difficoltà"   │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  QuickActions                                      │ │
│  │  ┌──────────────────┐  ┌──────────────────┐       │ │
│  │  │  📥 Add Game     │  │  💬 New Chat     │       │ │
│  │  │  (Upload PDF)    │  │  (Start chatting)│       │ │
│  │  └──────────────────┘  └──────────────────┘       │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

**Data Fetching** (TanStack Query):
```typescript
// Parallel queries for optimal performance
const { data: user } = useCurrentUser();
const { data: games } = useGames(undefined, undefined, 1, 6);
const { data: threads } = useChatThreads({ limit: 5 });

// Auto-refetch on window focus
// Stale time: 5 minutes
// Cache time: 10 minutes
```
```

### 5.2 Games Catalog Flow

```
┌──────────────────┐
│   /games         │
│  (page.tsx)      │
└────────┬─────────┘
         │
         │ GET /api/v1/games (TanStack Query)
         ↓
┌─────────────────────────────────────────────────────────┐
│              Games Catalog Page                          │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Filters Panel (Desktop: Sidebar, Mobile: Modal)   │ │
│  │  ┌──────────────────────────────────────────────┐ │ │
│  │  │  Search: [__________________]                │ │ │
│  │  │                                              │ │ │
│  │  │  Players:                                    │ │ │
│  │  │    Min: [2] Max: [6]                         │ │ │
│  │  │                                              │ │ │
│  │  │  Play Time (min):                            │ │ │
│  │  │    Min: [30] Max: [120]                      │ │ │
│  │  │                                              │ │ │
│  │  │  Year Published:                             │ │ │
│  │  │    From: [2010] To: [2024]                   │ │ │
│  │  │                                              │ │ │
│  │  │  ☐ Only games from BGG                       │ │ │
│  │  │                                              │ │ │
│  │  │  [Apply Filters] [Reset]                     │ │ │
│  │  └──────────────────────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Sort & View Controls                              │ │
│  │  Sort by: [Title ▼] [Year ▼] [Players ▼]         │ │
│  │  View: [Grid] [List]                              │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Games Grid (20 per page)                          │ │
│  │  ┌────────┐  ┌────────┐  ┌────────┐  ┌────────┐  │ │
│  │  │ Catan  │  │ 7 Won. │  │Pandemic│  │ Scythe │  │ │
│  │  │ [img]  │  │ [img]  │  │ [img]  │  │ [img]  │  │ │
│  │  │ ★★★★☆  │  │ ★★★★★  │  │ ★★★★☆  │  │ ★★★★☆  │  │ │
│  │  │ 3-4 👥 │  │ 2-7 👥 │  │ 2-4 👥 │  │ 1-5 👥 │  │ │
│  │  │ 60 min │  │ 30 min │  │ 45 min │  │ 90 min │  │ │
│  │  └────────┘  └────────┘  └────────┘  └────────┘  │ │
│  │  ... (16 more cards)                               │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Pagination                                         │ │
│  │  [<] 1 2 [3] 4 5 ... 15 [>]                        │ │
│  │  Showing 41-60 of 300 games                        │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

**Interaction Flow**:
```
User interaction → Apply filters
├→ Update URL query params (?search=catan&minPlayers=3)
├→ Trigger TanStack Query refetch
├→ Client-side filtering (MVP: no backend support)
├→ Update games grid
└→ Preserve scroll position (optimistic UI)
```

**Game Detail Navigation**:
```
Click game card → /games/[id]
├→ GET /api/v1/games/{id}
├→ Show game details page
│  ├─ Cover image + metadata (title, year, players, time)
│  ├─ Description + rules summary
│  ├─ Documents tab (PDF rulebooks)
│  ├─ Sessions tab (game history)
│  └─ Quick actions: [Start Chat] [Add Session] [Upload PDF]
└→ Breadcrumb: Games > Catan
```

### 5.3 Chat RAG Flow

```
┌──────────────────┐
│   /chat          │
│  (page.tsx)      │
│  Dynamic import  │
│  (SSR disabled)  │
└────────┬─────────┘
         │
         │ GET /api/v1/chat/threads (optional: load history)
         ↓
┌─────────────────────────────────────────────────────────┐
│              Chat Interface                              │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Game Selector (if no active thread)               │ │
│  │  ┌──────────────────────────────────────────────┐ │ │
│  │  │  Select game: [Catan ▼]                      │ │ │
│  │  │  Or search: [__________________________]     │ │ │
│  │  └──────────────────────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Chat Messages Area (scrollable)                   │ │
│  │  ┌──────────────────────────────────────────────┐ │ │
│  │  │  User: "Come si muovono le unità in Scythe?" │ │ │
│  │  └──────────────────────────────────────────────┘ │ │
│  │  ┌──────────────────────────────────────────────┐ │ │
│  │  │  MeepleAI: [Streaming response...]           │ │ │
│  │  │  "Le unità in Scythe si muovono..."         │ │ │
│  │  │                                              │ │ │
│  │  │  📚 Citazioni:                                │ │ │
│  │  │  • Scythe Rulebook, p. 12 (confidence: 0.85) │ │ │
│  │  │  • FAQ Ufficiale, sez. 2.3 (confidence: 0.92)│ │ │
│  │  └──────────────────────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Input Area                                         │ │
│  │  ┌──────────────────────────────────────────────┐ │ │
│  │  │  [_________________________________]  [Send] │ │ │
│  │  │  Type your question...                       │ │ │
│  │  └──────────────────────────────────────────────┘ │ │
│  │  Max 500 chars • Markdown supported              │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

**RAG Pipeline Flow** (Backend):
```
┌──────────────────┐
│  User question   │
│  + game context  │
└────────┬─────────┘
         │
         │ POST /api/v1/chat (SSE streaming)
         ↓
┌─────────────────────────────────────────────────────────┐
│                   RAG Pipeline                           │
│                                                          │
│  1. Query Processing                                    │
│     ├─ Language detection (Italian/English)             │
│     ├─ Query expansion (synonyms, variations)           │
│     └─ Embedding generation (OpenAI text-embedding-3)   │
│                                                          │
│  2. Hybrid Retrieval (RRF 70/30)                        │
│     ├─ Vector Search (Qdrant)                           │
│     │  • Semantic similarity                            │
│     │  • Top 20 chunks                                  │
│     │  • Filter by game_id                              │
│     └─ Keyword Search (PostgreSQL FTS)                  │
│        • Full-text search                               │
│        • Top 10 chunks                                  │
│        • ts_rank scoring                                │
│                                                          │
│  3. Re-ranking (Reciprocal Rank Fusion)                 │
│     • Combine vector (70%) + keyword (30%)              │
│     • Top 5 chunks selected                             │
│                                                          │
│  4. Context Assembly                                    │
│     • Format chunks as context                          │
│     • Add game metadata                                 │
│     • Truncate to 4000 tokens                           │
│                                                          │
│  5. LLM Generation (OpenRouter)                         │
│     ├─ Model: anthropic/claude-3.5-sonnet              │
│     ├─ System prompt: Italian expert, citations         │
│     ├─ Streaming: Server-Sent Events (SSE)             │
│     └─ Max tokens: 1000                                 │
│                                                          │
│  6. 5-Layer Validation                                  │
│     ├─ Hallucination detection                          │
│     ├─ Citation verification                            │
│     ├─ Confidence scoring (≥0.70 required)              │
│     ├─ Language quality check                           │
│     └─ Content safety filter                            │
│                                                          │
│  7. Response Assembly                                   │
│     • Stream answer chunks (SSE)                        │
│     • Append citations with confidence                  │
│     • Store in chat history                             │
└─────────────────────────────────────────────────────────┘
```

**Frontend SSE Handling**:
```typescript
// Chat component uses SSE for streaming
const [response, setResponse] = useState('');
const [citations, setCitations] = useState([]);

const eventSource = new EventSource('/api/v1/chat');
eventSource.onmessage = (event) => {
  const data = JSON.parse(event.data);

  if (data.type === 'chunk') {
    setResponse(prev => prev + data.content);
  } else if (data.type === 'citations') {
    setCitations(data.citations);
  } else if (data.type === 'done') {
    eventSource.close();
  }
};
```

**Error Recovery**:
```
Streaming errors:
├─ Connection lost → Reconnect with last message ID
├─ Low confidence (<0.70) → Show warning + manual verification
├─ No results → Suggest reformulating question
├─ Rate limit → Show retry countdown + queue position
└─ Server error → Fallback to cached response (if available)
```

### 5.4 Document Upload Flow

```
┌──────────────────┐
│   /upload        │
│  (page.tsx)      │
└────────┬─────────┘
         │
         │ Render UploadClient
         ↓
┌─────────────────────────────────────────────────────────┐
│              PDF Upload Interface                        │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Game Selection                                     │ │
│  │  ┌──────────────────────────────────────────────┐ │ │
│  │  │  Select game: [Wingspan ▼]                   │ │ │
│  │  │  Or create new: [+ Add New Game]             │ │ │
│  │  └──────────────────────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Drop Zone                                          │ │
│  │  ┌──────────────────────────────────────────────┐ │ │
│  │  │                                              │ │ │
│  │  │        📄 Drag & Drop PDF Here               │ │ │
│  │  │            or [Browse Files]                 │ │ │
│  │  │                                              │ │ │
│  │  │   Max 50 MB • PDF only • English/Italian    │ │ │
│  │  │                                              │ │ │
│  │  └──────────────────────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Upload Queue (if files selected)                  │ │
│  │  ┌──────────────────────────────────────────────┐ │ │
│  │  │  📄 wingspan-rulebook.pdf (3.2 MB)           │ │ │
│  │  │  [████████░░] 80% - Processing...            │ │ │
│  │  │  [Cancel]                                    │ │ │
│  │  └──────────────────────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

**Upload Pipeline** (3-Stage Extraction):
```
┌──────────────────┐
│  User uploads    │
│  wingspan.pdf    │
└────────┬─────────┘
         │
         │ POST /api/v1/documents/upload
         ↓
┌─────────────────────────────────────────────────────────┐
│              Stage 1: Validation                         │
│  • File type: PDF only                                  │
│  • Size: ≤50 MB                                         │
│  • Language: Italian/English (detect with langdetect)   │
│  • Duplicate check (hash comparison)                    │
│  • Virus scan (optional: ClamAV)                        │
└────────┬─────────────────────────────────────────────────┘
         │ Valid
         ↓
┌─────────────────────────────────────────────────────────┐
│              Stage 2: Storage                            │
│  • Save to PostgreSQL (BYTEA column)                    │
│  • Generate metadata:                                   │
│    - document_id (UUID)                                 │
│    - game_id (selected)                                 │
│    - filename, size, hash                               │
│    - upload_timestamp                                   │
│  • Status: "pending_extraction"                         │
└────────┬─────────────────────────────────────────────────┘
         │
         ↓
┌─────────────────────────────────────────────────────────┐
│              Stage 3: Extraction (3-Tier Fallback)      │
│                                                          │
│  Tier 1: Unstructured.io (Primary)                     │
│  • POST to Unstructured API                             │
│  • Extract: text, tables, images, layout                │
│  • Quality score ≥0.80 → Success                        │
│  • Quality score <0.80 → Fallback to Tier 2             │
│                                                          │
│  Tier 2: SmolDocling (Secondary)                        │
│  • Local model inference                                │
│  • Quality score ≥0.70 → Success                        │
│  • Quality score <0.70 → Fallback to Tier 3             │
│                                                          │
│  Tier 3: Docnet (Tertiary)                              │
│  • PyTesseract OCR fallback                             │
│  • Best effort extraction                               │
│  • Always succeeds (quality varies)                     │
└────────┬─────────────────────────────────────────────────┘
         │
         ↓
┌─────────────────────────────────────────────────────────┐
│              Stage 4: Chunking & Embedding               │
│  • Split text into sentences (spaCy Italian)            │
│  • Group into ~500 token chunks                         │
│  • Generate embeddings (text-embedding-3-small)         │
│  • Store vectors in Qdrant                              │
│    - collection: game_documents                         │
│    - payload: { game_id, chunk_text, page_num, ... }   │
│  • Store full-text in PostgreSQL                        │
│    - ts_vector for FTS                                  │
│  • Status: "ready"                                      │
└────────┬─────────────────────────────────────────────────┘
         │
         ↓
┌──────────────────────────────┐
│  Frontend notification       │
│  "✅ Document ready for RAG" │
│  [View in Chat]              │
└──────────────────────────────┘
```

**Progress Tracking** (Real-time SSE):
```
Frontend subscribes to SSE:
/api/v1/documents/{id}/progress

Events:
├─ { type: "validation", progress: 10 }
├─ { type: "storage", progress: 30 }
├─ { type: "extraction_tier1", progress: 50 }
├─ { type: "extraction_tier2", progress: 60 } (if fallback)
├─ { type: "chunking", progress: 80 }
├─ { type: "embedding", progress: 90 }
└─ { type: "complete", progress: 100, document_id: "..." }
```

---

## 6. Flussi Admin

### 6.1 Admin Dashboard Flow

```
┌──────────────────┐
│   /admin         │
│  RequireRole     │
│  (['Admin'])     │
└────────┬─────────┘
         │
         │ Parallel queries (TanStack Query):
         │ • GET /api/v1/admin/stats
         │ • GET /api/v1/admin/users?page=1
         │ • GET /api/v1/admin/alerts
         ↓
┌─────────────────────────────────────────────────────────┐
│              Admin Dashboard                             │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Stats Overview (Grid: 2x2)                        │ │
│  │  ┌─────────────────┐  ┌─────────────────┐         │ │
│  │  │ 👥 Total Users  │  │ 🎮 Total Games  │         │ │
│  │  │     1,234       │  │      567        │         │ │
│  │  │  +45 this week  │  │  +12 this week  │         │ │
│  │  └─────────────────┘  └─────────────────┘         │ │
│  │  ┌─────────────────┐  ┌─────────────────┐         │ │
│  │  │ 💬 Chat Threads │  │ ⚠️  Alerts      │         │ │
│  │  │     8,901       │  │       3         │         │ │
│  │  │  +234 today     │  │  1 critical     │         │ │
│  │  └─────────────────┘  └─────────────────┘         │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Recent Alerts                                      │ │
│  │  🔴 High error rate in RAG pipeline (5 min ago)    │ │
│  │  🟡 Slow query detected: GetAllGamesQuery (1h ago) │ │
│  │  🟢 System health check passed (2h ago)            │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Quick Actions                                      │ │
│  │  [👥 Manage Users] [⚙️ Configuration]              │ │
│  │  [📊 Analytics]    [🔔 View All Alerts]            │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

### 6.2 User Management Flow

```
/admin/users → GET /api/v1/admin/users?page=1&pageSize=20

┌─────────────────────────────────────────────────────────┐
│              User Management Table                       │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Filters                                            │ │
│  │  Search: [__________] Role: [All ▼] Status: [All ▼]│ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Table (20 rows per page)                          │ │
│  │  ┌──────┬──────────┬──────┬────────┬──────────┐   │ │
│  │  │ ID   │ Email    │ Role │ Status │ Actions  │   │ │
│  │  ├──────┼──────────┼──────┼────────┼──────────┤   │ │
│  │  │ 1234 │ user@... │ User │ Active │ [Edit]   │   │ │
│  │  │      │          │      │        │ [Delete] │   │ │
│  │  ├──────┼──────────┼──────┼────────┼──────────┤   │ │
│  │  │ 5678 │ admin@.. │ Admin│ Active │ [Edit]   │   │ │
│  │  │      │          │      │        │ [Revoke] │   │ │
│  │  └──────┴──────────┴──────┴────────┴──────────┘   │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Pagination                                         │ │
│  │  [<] 1 2 [3] 4 5 ... 62 [>]                        │ │
│  │  Showing 41-60 of 1234 users                       │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

**Admin Actions**:
```
Edit User → Modal dialog
├─ Change role (User/Editor/Admin)
├─ Enable/Disable account
├─ Reset password (send email)
└─ View audit log

Delete User → Confirmation dialog
├─ Soft delete (mark as deleted)
├─ Option: Hard delete (GDPR compliance)
└─ Cascade: sessions, chat threads, uploaded documents
```

### 6.3 Configuration Management Flow

```
/admin/configuration → GET /api/v1/admin/configuration

┌─────────────────────────────────────────────────────────┐
│              System Configuration                        │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  3-Tier Config System                               │ │
│  │  Priority: DB > appsettings.json > defaults         │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Feature Flags                                      │ │
│  │  ☑ Enable RAG                                       │ │
│  │  ☑ Enable OAuth                                     │ │
│  │  ☐ Enable 2FA (experimental)                        │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  PDF Processing                                     │ │
│  │  Provider: [Orchestrator ▼]                         │ │
│  │  Options: Unstructured, SmolDocling, Docnet         │ │
│  │  Fallback: ☑ Enable 3-tier cascade                  │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  RAG Settings                                       │ │
│  │  Vector weight: [0.70] (70%)                        │ │
│  │  Keyword weight: [0.30] (30%)                       │ │
│  │  Confidence threshold: [0.70]                       │ │
│  │  Max chunks: [5]                                    │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  [Save Changes] [Revert] [Export Config]           │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

**Dynamic Config Changes**:
```
Admin changes config → PUT /api/v1/admin/configuration
├─ Validate new values
├─ Store in PostgreSQL (ConfigurationSettings table)
├─ Invalidate cache (Redis)
├─ Trigger IOptionsMonitor<T>.OnChange() event
├─ Update runtime configuration (hot reload, no restart)
└─ Audit log: who changed what, when
```

### 6.4 Analytics Dashboard Flow

```
/admin/analytics → Grafana embed or custom charts

┌─────────────────────────────────────────────────────────┐
│              Analytics Dashboard                         │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Time Range Selector                                │ │
│  │  [Last 7 days ▼] [Custom Range]                     │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  User Activity Chart                                │ │
│  │  ┌──────────────────────────────────────────────┐  │ │
│  │  │         ^                                    │  │ │
│  │  │  Users  │     •••                            │  │ │
│  │  │    200  │   ••   •••                         │  │ │
│  │  │    100  │ ••       •••                       │  │ │
│  │  │      0  └──────────────────────────────>     │  │ │
│  │  │           Mon  Tue  Wed  Thu  Fri  Sat  Sun │  │ │
│  │  └──────────────────────────────────────────────┘  │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  RAG Performance Metrics                            │ │
│  │  • Avg response time: 1.2s (P95: 2.8s)             │ │
│  │  • Recall@10: 75% (target: ≥70%)                   │ │
│  │  • Avg confidence: 0.82 (target: ≥0.70)            │ │
│  │  • Hallucination rate: 1.2% (target: <3%)          │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Top Games by Chat Volume                           │ │
│  │  1. Catan (1,234 threads)                          │ │
│  │  2. 7 Wonders (987 threads)                        │ │
│  │  3. Pandemic (856 threads)                         │ │
│  │  4. Scythe (723 threads)                           │ │
│  │  5. Wingspan (612 threads)                         │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

---

## 7. Performance SLOs

| Metric | Target | Monitoring |
|--------|--------|------------|
| Landing Page FCP | <1.5s | Lighthouse CI |
| Dashboard TTI | <2.5s | Web Vitals |
| Chat Streaming TTFB | <500ms | OpenTelemetry |
| RAG Response P95 | <1500ms | Prometheus |
| API Availability | >99.5% | Health checks |
| Test Coverage | >90% | Codecov |

---

## 8. Cross-Cutting Concerns

### 8.1 Responsive Breakpoints
```css
/* Tailwind CSS 4 breakpoints */
sm:  640px  /* Mobile landscape */
md:  768px  /* Tablet portrait */
lg:  1024px /* Tablet landscape / Desktop */
xl:  1280px /* Desktop */
2xl: 1536px /* Large desktop */
```

### 8.2 i18n Support
```typescript
// Currently: Italian only
// Future: English, Spanish, German
// Implementation: next-intl + backend i18n

const { t } = useTranslation();
<h1>{t('home.hero.title')}</h1>
```

### 8.3 Accessibility (WCAG 2.1 AA)
- Semantic HTML5 (nav, main, article, section)
- ARIA labels on interactive elements
- Keyboard navigation (Tab, Enter, Esc)
- Screen reader support (VoiceOver, NVDA)
- Focus management (FocusTrap in modals)
- Color contrast ratio ≥4.5:1

---

**Fine del documento** | Creato: 2025-12-14 | Frontend Architecture Team

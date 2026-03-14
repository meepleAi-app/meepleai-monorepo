# First-Time User Onboarding — Design Spec

**Date**: 2026-03-13
**Status**: Draft
**Scope**: Wizard modale + checklist card per nuovi utenti

## Problem

Un utente che si registra (OAuth, self-registration, email invite) atterra sulla dashboard vuota — stessi contenuti di un veterano, ma con 0 giochi, 0 sessioni, 0 chat. Nessuna guida, nessun suggerimento su come iniziare.

## Goals

1. **Guidare ad azioni concrete** (priorità alta) — portare l'utente a completare le prime azioni chiave il prima possibile
2. **Presentare le feature** (priorità media) — mostrare cosa può fare MeepleAI durante il percorso
3. **Personalizzare l'esperienza** (priorità bassa) — raccogliere segnali impliciti per contenuti rilevanti

## Decisions

| Decisione | Scelta | Alternativa scartata |
|-----------|--------|---------------------|
| Onboarding "finito" | Mai — checklist dismissabile dall'utente (X o "Non mostrare più") | Flag automatico dopo N azioni |
| Posizionamento | Wizard modale al primo login + checklist card in dashboard | Dashboard stateful / page-based |
| Canali di registrazione | Esperienza identica per tutti (OAuth, self-register, invite) | Flow diversi per canale |
| Profondità wizard | Leggero — 3 schermate | Medio (4-5) o minimo (1) |
| Stato checklist | Derivato dai dati reali — nessun flag artificiale per i singoli step | Flag booleani per ogni step |

## Architecture

### Wizard Modal (primo login)

Componente `<OnboardingWizard />` — dialog modale che appare automaticamente quando l'utente atterra su `/dashboard` per la prima volta.

**Trigger**: `!user.onboardingWizardSeenAt` (server-side, cross-device)

**3 step**:

1. **Benvenuto** — "Ciao {displayName}! Benvenuto in MeepleAI" + tagline + illustrazione. CTA: "Iniziamo →"
2. **Feature showcase** — 4 card compatte (Libreria, Assistente AI, Sessioni, Catalogo). Icona + 1 riga descrittiva per card. CTA: "Avanti →"
3. **Pronto!** — Preview della checklist. CTA: "Vai alla Dashboard 🎯"

**Comportamento**:
- Skippabile in qualsiasi momento (X in alto a destra)
- Dots indicator per navigazione step
- Su chiusura (skip o completamento) → `POST /api/v1/users/me/onboarding-wizard-seen`
- Non si riapre mai dopo la prima chiusura — garantito cross-device via backend
- Responsive: fullscreen su mobile, centered dialog (`max-w-lg`) su desktop
- Animazioni: `framer-motion` `AnimatePresence` tra step (coerente con dashboard), rispetta `prefers-reduced-motion`

### Checklist Card (dashboard)

Componente `<OnboardingChecklist />` — card che appare come primo elemento della dashboard, sopra l'hero.

**4 step**:

| Step | Testo | Completato quando | Link |
|------|-------|-------------------|------|
| Aggiungi il primo gioco | "Cerca nel catalogo o aggiungine uno manualmente" | `userGames > 0` (backend) | `/library?action=add` |
| Crea una sessione | "Registra la tua prima partita" | `userSessions > 0` (backend) | `/sessions/new` |
| Esplora il catalogo | "Scopri giochi dalla community" | `localStorage('hasVisitedDiscover')` (frontend) | `/discover` |
| Completa il profilo | "Aggiungi un avatar e una bio" | `user.AvatarUrl != null OR user.Bio != null` (backend) | `/profile` |

**Nota su "Completa il profilo"**: I campi `AvatarUrl` e `Bio` non esistono attualmente su User. Vanno aggiunti come parte di questa feature (vedi Data Model).

**3 stati visivi**:

1. **Fresh (0/4)** — gradient amber header, tutti gli item aperti con checkbox vuote
2. **In progress (N/4)** — completati in alto (opacità ridotta, testo barrato, checkbox verde), prossimo item con bordo amber, progress bar parziale, messaggio "N di 4 completati — ottimo lavoro!"
3. **Completato (4/4)** — gradient verde, messaggio celebrativo "Tutto completato!", pulsante "Chiudi checklist", auto-dismiss dopo 5 secondi con fade-out (rispetta `prefers-reduced-motion`: nessuna animazione, dismiss istantaneo)

**Loading state**: Durante il fetch iniziale (`isLoading`), renderizza `null` (nessun placeholder) per evitare layout shift — la dashboard sotto si carica indipendentemente.

**Dismiss**:
- X o "Non mostrare più" → `POST /api/v1/users/me/onboarding-dismiss` + localStorage cache
- La card non appare più in nessun device (server-side)
- Nessun modo di riaprirla (guida temporanea, non feature)

## Data Model

### Backend — User Entity

3 campi nuovi sulla entity User (`Authentication` BC):

```csharp
// BoundedContexts/Authentication/Domain/Entities/User.cs

// Onboarding
public string? AvatarUrl { get; private set; }
public string? Bio { get; private set; }
public DateTime? OnboardingWizardSeenAt { get; private set; }
public DateTime? OnboardingDismissedAt { get; private set; }

public void MarkOnboardingWizardSeen()
{
    if (OnboardingWizardSeenAt.HasValue) return;
    OnboardingWizardSeenAt = DateTime.UtcNow;
}

public void DismissOnboarding()
{
    if (OnboardingDismissedAt.HasValue) return;
    OnboardingDismissedAt = DateTime.UtcNow;
}

public void UpdateAvatarUrl(string avatarUrl)
{
    AvatarUrl = avatarUrl;
}

public void UpdateBio(string bio)
{
    Bio = bio;
}
```

Migration: 4 campi nullable (`AvatarUrl`, `Bio`, `OnboardingWizardSeenAt`, `OnboardingDismissedAt`) sulla tabella `Users`.

### Cross-BC Query Strategy

Il `GetOnboardingStatusQueryHandler` necessita dati da 3 bounded context diversi:
- `hasGames` → UserLibrary BC
- `hasSessions` → SessionTracking BC
- `hasCompletedProfile` → Authentication BC (stesso BC)

**Approccio**: Il handler usa `DbContext.Set<T>()` per accesso cross-BC in sola lettura, seguendo il pattern stabilito nel progetto (es. `GetUserLibraryStatsQueryHandler` in Administration BC, `GameLibraryQuotaService`):

```csharp
// Il handler vive in Authentication BC
public class GetOnboardingStatusQueryHandler : IRequestHandler<GetOnboardingStatusQuery, OnboardingStatusResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IUserRepository _userRepository;

    public async Task<OnboardingStatusResponse> Handle(...)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);
        var hasGames = await _dbContext.Set<UserGame>().AnyAsync(g => g.UserId == request.UserId);
        var hasSessions = await _dbContext.Set<Session>().AnyAsync(s => s.UserId == request.UserId);

        return new OnboardingStatusResponse
        {
            WizardSeenAt = user.OnboardingWizardSeenAt,
            DismissedAt = user.OnboardingDismissedAt,
            Steps = new OnboardingStepsDto
            {
                HasGames = hasGames,
                HasSessions = hasSessions,
                HasCompletedProfile = user.AvatarUrl != null || user.Bio != null
            }
        };
    }
}
```

Nessuna nuova query o repository necessari — `DbContext.Set<T>()` accede direttamente alle entità degli altri BC per query count/exists leggere.

### API Endpoints

**`GET /api/v1/users/me/onboarding-status`**

Response:
```json
{
  "wizardSeenAt": null,
  "dismissedAt": null,
  "steps": {
    "hasGames": false,
    "hasSessions": false,
    "hasCompletedProfile": false
  }
}
```

TypeScript response type:
```typescript
interface OnboardingStatusResponse {
  wizardSeenAt: string | null;
  dismissedAt: string | null;
  steps: {
    hasGames: boolean;
    hasSessions: boolean;
    hasCompletedProfile: boolean;
  };
}
```

- `hasGames`: `AnyAsync()` dalla UserLibrary (via `DbContext.Set<UserGame>()`)
- `hasSessions`: `AnyAsync()` dalle Sessions (via `DbContext.Set<Session>()`)
- `hasCompletedProfile`: `user.AvatarUrl != null || user.Bio != null`
- `hasVisitedDiscover` non è nel response — tracciato solo in localStorage, il frontend lo fonde

CQRS: `GetOnboardingStatusQuery` + `GetOnboardingStatusQueryHandler`

**`POST /api/v1/users/me/onboarding-wizard-seen`**

- Setta `OnboardingWizardSeenAt = DateTime.UtcNow`
- Idempotente — se già settato, 200 OK senza modifiche (early return nel domain method)
- No request body

CQRS: `MarkOnboardingWizardSeenCommand` + `MarkOnboardingWizardSeenCommandHandler`

**`POST /api/v1/users/me/onboarding-dismiss`**

- Setta `OnboardingDismissedAt = DateTime.UtcNow`
- Idempotente — se già dismissato, 200 OK senza modifiche (early return nel domain method)
- No request body

CQRS: `DismissOnboardingCommand` + `DismissOnboardingCommandHandler`

**Estensione di `PUT /api/v1/users/profile`** (endpoint esistente)

- Aggiungere i campi opzionali `AvatarUrl` e `Bio` all'esistente `UpdateUserProfileCommand`
- Il handler usa null-guards (pattern esistente): `if (!string.IsNullOrWhiteSpace(command.AvatarUrl)) user.UpdateAvatarUrl(command.AvatarUrl)`
- Nessun nuovo endpoint — si estende l'esistente `PUT /api/v1/users/profile` in `UserProfileEndpoints.cs`

CQRS: Estendere `UpdateUserProfileCommand` e `UpdateUserProfileCommandHandler` esistenti

## Frontend Components

### File Structure

```
src/components/onboarding/
├── OnboardingWizard.tsx          # Dialog modale 3-step
├── OnboardingChecklist.tsx       # Card checklist per dashboard
├── onboarding-steps.ts          # Config step (typed array)
└── use-onboarding-status.ts     # Hook: fetch status + merge localStorage
```

### Step Configuration (`onboarding-steps.ts`)

```typescript
import { Library, Play, Compass, UserCircle, type LucideIcon } from 'lucide-react';

interface OnboardingStep {
  id: 'hasGames' | 'hasSessions' | 'hasVisitedDiscover' | 'hasCompletedProfile';
  title: string;
  description: string;
  href: string;
  icon: LucideIcon;
  source: 'backend' | 'localStorage';
}

const ONBOARDING_STEPS: OnboardingStep[] = [
  {
    id: 'hasGames',
    title: 'Aggiungi il primo gioco',
    description: 'Cerca nel catalogo o aggiungine uno manualmente',
    href: '/library?action=add',
    icon: Library,
    source: 'backend',
  },
  {
    id: 'hasSessions',
    title: 'Crea una sessione',
    description: 'Registra la tua prima partita',
    href: '/sessions/new',
    icon: Play,
    source: 'backend',
  },
  {
    id: 'hasVisitedDiscover',
    title: 'Esplora il catalogo',
    description: 'Scopri giochi dalla community',
    href: '/discover',
    icon: Compass,
    source: 'localStorage',
  },
  {
    id: 'hasCompletedProfile',
    title: 'Completa il profilo',
    description: 'Aggiungi un avatar e una bio',
    href: '/profile',
    icon: UserCircle,
    source: 'backend',
  },
];
```

### Hook `useOnboardingStatus`

```typescript
// Raw API response (3 backend steps)
interface OnboardingStatusResponse {
  wizardSeenAt: string | null;
  dismissedAt: string | null;
  steps: {
    hasGames: boolean;
    hasSessions: boolean;
    hasCompletedProfile: boolean;
  };
}

// Merged hook output (4 steps: 3 backend + 1 localStorage)
interface OnboardingStatus {
  isLoading: boolean;
  showWizard: boolean;        // !wizardSeenAt (server-side, independent from dismiss)
  showChecklist: boolean;     // !dismissedAt (independent from wizard)
  steps: {
    hasGames: boolean;         // backend
    hasSessions: boolean;      // backend
    hasCompletedProfile: boolean; // backend
    hasVisitedDiscover: boolean;  // localStorage
  };
  completedCount: number;     // derivato da steps
  totalSteps: number;         // 4
  dismiss: () => void;        // mutation POST + localStorage cache
  markWizardSeen: () => void; // mutation POST (server-side)
}
```

- `useQuery` con `queryKey: ['onboarding-status']`, `staleTime: 30_000`
- `showWizard = !data.wizardSeenAt` — indipendente da dismiss, garantito cross-device
- `showChecklist = !data.dismissedAt` — indipendente da wizard
- `dismiss` usa `useMutation` → `POST /onboarding-dismiss` con optimistic update (`queryClient.setQueryData` setta `dismissedAt` immediatamente) + `onSettled` → invalidate query
- `markWizardSeen` usa `useMutation` → `POST /onboarding-wizard-seen` con optimistic update (`wizardSeenAt` settato immediatamente) + `onSettled` → invalidate query

### Dashboard Integration

Modifica minima a `gaming-hub-client.tsx`:

```tsx
const { showWizard, showChecklist, isLoading } = useOnboardingStatus();

return (
  <>
    <OnboardingWizard />          {/* si auto-controlla via showWizard */}
    {!isLoading && showChecklist && <OnboardingChecklist />}
    {/* ...dashboard esistente invariata... */}
  </>
);
```

### Discover Page Integration

Modifica minima a `discover/page.tsx`:

```tsx
useEffect(() => {
  localStorage.setItem('hasVisitedDiscover', 'true');
}, []);
```

### API Client

3 metodi nuovi in `lib/api/`:

```typescript
// In the appropriate client factory
getOnboardingStatus(): Promise<OnboardingStatusResponse>
markOnboardingWizardSeen(): Promise<void>
dismissOnboarding(): Promise<void>
```

## Testing

### Frontend — Unit (Vitest)

| Test file | Cosa verifica |
|-----------|---------------|
| `use-onboarding-status.test.ts` | Merge backend + localStorage, derivazione `completedCount`, logica `showWizard` (da `wizardSeenAt`) e `showChecklist` (da `dismissedAt`) indipendenti |
| `OnboardingWizard.test.tsx` | Render 3 step, navigazione avanti, skip chiama `markWizardSeen`, non appare se `wizardSeenAt` è settato |
| `OnboardingChecklist.test.tsx` | Render 4 item, stato completato (checkbox verde + barrato), progress bar width, dismiss chiama API, link corretti |
| `gaming-hub-client.test.tsx` | Checklist appare/non appare in base a `showChecklist`, non appare durante `isLoading`, wizard al primo render |

Pattern: `vi.hoisted()` + `vi.mock('@/lib/api')`, `vi.stubGlobal` per localStorage.

### Frontend — E2E (Playwright)

| Test file | Scenario |
|-----------|----------|
| `onboarding-wizard.spec.ts` | Nuovo utente → wizard appare → naviga 3 step → chiude → non riappare al refresh |
| `onboarding-checklist.spec.ts` | Checklist visibile → click step → naviga a route target → torna dashboard → step aggiornato |
| `onboarding-dismiss.spec.ts` | Click "Non mostrare più" → scompare → refresh → resta nascosta |

Pattern: `page.context().route()` per mock API, `PLAYWRIGHT_AUTH_BYPASS=true`.

### Backend — Unit (xUnit)

| Test | Cosa verifica |
|------|---------------|
| `User.MarkOnboardingWizardSeen()` | Setta `OnboardingWizardSeenAt`, idempotente (early return su seconda chiamata) |
| `User.DismissOnboarding()` | Setta `OnboardingDismissedAt`, idempotente (early return su seconda chiamata) |
| `User.UpdateAvatarUrl()` | Setta `AvatarUrl` |
| `User.UpdateBio()` | Setta `Bio` |
| `GetOnboardingStatusQueryHandler` | Aggrega correttamente: 0 giochi → `hasGames=false`, 1+ → `true`. Idem sessioni e profilo |
| `MarkOnboardingWizardSeenCommandHandler` | Chiama `MarkOnboardingWizardSeen()`, persiste |
| `DismissOnboardingCommandHandler` | Chiama `DismissOnboarding()`, persiste |

### Backend — Integration (xUnit + Testcontainers)

| Test | Cosa verifica |
|------|---------------|
| `GET /users/me/onboarding-status` | 200 con dati corretti, 401 se non autenticato |
| `POST /users/me/onboarding-wizard-seen` | 200, campo persistito nel DB, idempotente |
| `POST /users/me/onboarding-dismiss` | 200, campo persistito nel DB, idempotente |
| `PUT /users/profile` | 200, AvatarUrl e Bio aggiornati (estensione endpoint esistente) |

**Stima totale**: ~15 unit FE + 3 E2E + 10 unit BE + 4 integration BE = ~32 test.

## Out of Scope

- Onboarding differenziato per canale di registrazione
- Riapertura della checklist dopo dismiss
- Chat AI come step della checklist
- Invita amico come step della checklist
- Gamification/XP per completamento step
- A/B testing sugli step del wizard
- Personalizzazione basata su interessi (campo `Interests` nel User entity — futuro)
- Upload avatar (per ora solo URL — upload è feature separata)

# M6 — Gruppo 1 Migration (Auth + Onboarding + Settings + Notifications + Public)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sostituire le 5 aree "Gruppo 1 critiche" dell'app consumer con i mockup Claude Design v1 in `admin-mockups/design_files/` (auth-flow, onboarding, settings, notifications, public).

**Architecture:** Prosecuzione del Design System v2 avviato in M1-M5. Hybrid responsive (`md:` breakpoint 768px): mobile drawer + desktop page. Riuso primitivi `@/components/ui/v2/*` creati in M1-M2 (EntityChip, EntityPip, EntityCard, Drawer, Btn). Questo plan NON crea foundation nuova — assume che M1 sia completato. Strategia **branch isolato** `redesign/v2-m6` rebased su `redesign/v2`, PR verso `main-dev` dopo validazione.

**Tech Stack:** Next.js 16, React 19, Tailwind 4, Zustand 5, React Query 5, Vitest 3 + jest-axe, Playwright, MSW, framer-motion 12, vaul 1.x, shadcn/ui Radix Dialog, next-auth (esistente).

**Depends on:**
- ✅ `2026-04-20-redesign-v2-full-app-migration-m1-m5.md` **M1 Primitivi** completato (EntityChip, EntityPip, EntityCard, Drawer). Se M1 non completo, eseguirlo prima.
- ✅ Mockup sorgente: `admin-mockups/design_files/{auth-flow,onboarding,settings,notifications,public}.{html,jsx}` — tutti presenti, validati in hub `00-hub.html`.
- ✅ Token CSS già allineati: `--e-game|player|session|agent|chat|event|toolkit|tool` in `src/styles/globals.css:505-513` con stessi valori HSL dei mockup `--c-*`.

**Out of scope (separate plans):**
- Gruppo 2 (game-night, play-records, session-wizard) → M7, richiede mockup Claude Design non ancora creati
- Gruppo 3 (editor-pipeline, calendar) → M8 (opzionale)
- Gruppo 4 (profile-player, game-detail, chat-desktop, upload-wizard, live-session, toolkit-ecosystem) → M9, brief pronto in `specs/2026-04-20-claude-design-group4-extended-brief.md`

---

## Scope Coverage

| Area | Route attuale | Mockup | Screen | Entity |
|---|---|---|---|---|
| **Auth** | `src/app/(auth)/{login,register,reset-password,verify-email,...}` | `auth-flow.html` + `.jsx` | Login · Register · Forgot · Reset · Verify · 2FA | — (fallback `--e-game`) |
| **Onboarding** | `src/app/(authenticated)/onboarding/page.tsx` | `onboarding.html` + `.jsx` | Welcome · Step1 games · Step2 agents · Step3 session · Complete | game→agent→session |
| **Settings** | `src/app/(authenticated)/settings/*` | `settings.html` + `.jsx` | Hub · Profile · Account · Preferences · Notifications prefs · API keys · Services | — (fallback `--e-game`) |
| **Notifications** | `src/app/(authenticated)/notifications/*` (se esistente, altrimenti creare) | `notifications.html` + `.jsx` | Feed list · Detail drawer · Empty · Filters · Settings link | per notifica |
| **Public** | `src/app/(public)/{page,about,privacy,terms,...}` | `public.html` + `.jsx` | Landing · Pricing · About · Terms/Privacy · Contact | mix brand |

---

## File Structure

### Nuovi primitivi M6-scoped (estensioni M1-M2)

Create in `src/components/ui/v2/`:
- `AuthCard.tsx` — card centrato fullscreen con logo gradient, phone frame mantenuto
- `StepProgress.tsx` — barra progresso multi-step entity-aware (per onboarding)
- `SettingsList.tsx` + `SettingsRow.tsx` — list+drawer pattern settings
- `ToggleSwitch.tsx` — switch custom entity-aware
- `NotificationCard.tsx` — card con border-left entity-colored + unread dot
- `PricingCard.tsx` — card tier con highlight
- `HeroGradient.tsx` — hero con gradient entity-mix

### Nuove pages (sostituzioni o wrapper)

```
src/app/(auth)/
├── login/page.tsx              [MODIFY — rewrite con AuthCard + Btn primitivi v2]
├── register/page.tsx           [MODIFY]
├── reset-password/page.tsx     [MODIFY]
├── verify-email/page.tsx       [MODIFY]
└── _components/
    ├── AuthLayout.tsx          [CREATE — phone-frame + logo + card wrapper]
    └── OAuthButtons.tsx        [CREATE — Google/Discord outline buttons con icone entity]

src/app/(authenticated)/
├── onboarding/
│   ├── page.tsx                [MODIFY — multi-step con StepProgress]
│   └── _steps/{Welcome,Step1Games,Step2Agents,Step3Session,Complete}.tsx  [CREATE]
├── settings/
│   ├── page.tsx                [MODIFY — SettingsList hub]
│   ├── profile/page.tsx        [MODIFY]
│   ├── account/page.tsx        [MODIFY]
│   ├── preferences/page.tsx    [MODIFY]
│   ├── notifications/page.tsx  [MODIFY]
│   ├── api-keys/page.tsx       [CREATE — power-user area]
│   └── services/page.tsx       [CREATE — Google/Discord/BGG linking]
└── notifications/
    ├── page.tsx                [CREATE — feed grouped by day]
    └── _components/{FeedList,DetailDrawer,EmptyState,FiltersBar}.tsx [CREATE]

src/app/(public)/
├── page.tsx                    [MODIFY — Landing con HeroGradient + stats + features]
├── pricing/page.tsx            [CREATE — 3 PricingCard tier]
├── about/page.tsx              [MODIFY]
├── terms/page.tsx              [MODIFY — markdown styling]
├── privacy/page.tsx            [MODIFY]
└── contact/page.tsx            [CREATE — form + email + social]
```

### Test files

Ogni primitivo v2 e ogni page riscritta ha test paralleli:
- `__tests__/{ComponentName}.test.tsx` — Vitest + RTL + jest-axe
- `e2e/m6/{area}.spec.ts` — Playwright smoke per ogni area (login flow, onboarding flow, settings navigation, notifications open, public landing)

---

## Preflight (prima della Task 1)

- [ ] **P.1 Verifica M1 primitivi presenti**

Run:
```bash
ls src/components/ui/v2/
```
Expected (minimo): `EntityChip.tsx`, `EntityPip.tsx`, `EntityCard.tsx`, `Drawer.tsx`, `Btn.tsx`, `PhoneFrame.tsx`.

Se manca QUALCOSA, stop e completa M1 prima di procedere (vedi `2026-04-20-redesign-v2-library-pilot-plan.md`).

- [ ] **P.2 Verifica token allineamento**

Run:
```bash
grep -E "^\s*--e-(game|player|session|agent|chat|event|toolkit|tool):" src/styles/globals.css
```
Expected: 8 righe con valori HSL corrispondenti a `admin-mockups/design_files/tokens.css:8-17`.

Delta noto accettabile:
- `--e-document` (app) corrisponde a `--c-kb` (mockup) — valori diversi `210 40% 55%` vs `174 60% 40%`. **Allinea su mockup** (teal 174) in Task 0.
- `--e-toolkit: 160 70% 45%` (app) vs `142 70% 45%` (mockup) — verde leggermente diverso. **Allinea su mockup** (142) in Task 0.

- [ ] **P.3 Crea branch**

```bash
git checkout redesign/v2 && git pull
git checkout -b redesign/v2-m6
git config branch.redesign/v2-m6.parent redesign/v2
```

---

## Task 0: Reconcile token drift (kb + toolkit)

**Files:**
- Modify: `src/styles/globals.css:509-512`
- Modify: `src/components/ui/v2/__tests__/EntityPip.test.tsx` (se test lockano i vecchi valori)

- [ ] **Step 1: Write the failing test**

```tsx
// src/styles/__tests__/entity-tokens.test.ts
import { describe, it, expect } from 'vitest';
import fs from 'node:fs';
import path from 'node:path';

describe('entity tokens alignment with Claude Design v1', () => {
  it('kb and toolkit match mockup tokens.css', () => {
    const css = fs.readFileSync(path.join(__dirname, '../globals.css'), 'utf8');
    // --e-document should be teal 174 60% 40% (matches --c-kb in mockup)
    expect(css).toMatch(/--e-document:\s*174\s+60%\s+40%/);
    // --e-toolkit should be 142 70% 45% (matches --c-toolkit in mockup)
    expect(css).toMatch(/--e-toolkit:\s*142\s+70%\s+45%/);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
pnpm vitest run src/styles/__tests__/entity-tokens.test.ts
```
Expected: FAIL (current values are `210 40% 55%` and `160 70% 45%`).

- [ ] **Step 3: Update globals.css**

```css
/* src/styles/globals.css:509-512 */
  --e-document: 174 60% 40%;   /* aligned with mockup --c-kb */
  --e-chat: 220 80% 55%;
  --e-event: 350 89% 60%;
  --e-toolkit: 142 70% 45%;    /* aligned with mockup --c-toolkit */
```

- [ ] **Step 4: Verify test passes**

```bash
pnpm vitest run src/styles/__tests__/entity-tokens.test.ts
```
Expected: PASS

- [ ] **Step 5: Visual regression check**

```bash
pnpm test:e2e -- --grep "library|session|kb" 2>&1 | tail -20
```
Expected: no new failures (se fail visivi su kb/toolkit → screenshot baseline va aggiornato con `--update-snapshots`).

- [ ] **Step 6: Commit**

```bash
git add src/styles/globals.css src/styles/__tests__/entity-tokens.test.ts
git commit -m "refactor(tokens): align --e-document and --e-toolkit with Claude Design v1"
```

---

## Task 1: AuthCard primitive

**Files:**
- Create: `src/components/ui/v2/AuthCard.tsx`
- Test: `src/components/ui/v2/__tests__/AuthCard.test.tsx`

- [ ] **Step 1: Write failing test**

```tsx
import { render, screen } from '@testing-library/react';
import { axe } from 'jest-axe';
import { AuthCard } from '../AuthCard';

describe('AuthCard', () => {
  it('renders title + children inside a card', () => {
    render(
      <AuthCard title="Accedi">
        <form data-testid="f" />
      </AuthCard>
    );
    expect(screen.getByRole('heading', { name: /accedi/i })).toBeInTheDocument();
    expect(screen.getByTestId('f')).toBeInTheDocument();
  });

  it('shows logo mark with gradient by default', () => {
    render(<AuthCard title="x" />);
    expect(screen.getByTestId('auth-logo-mark')).toBeInTheDocument();
  });

  it('is accessible', async () => {
    const { container } = render(<AuthCard title="Accedi" />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Verify fail**

```bash
pnpm vitest run src/components/ui/v2/__tests__/AuthCard.test.tsx
```
Expected: FAIL (module not found).

- [ ] **Step 3: Implement**

```tsx
// src/components/ui/v2/AuthCard.tsx
import { ReactNode } from 'react';

export interface AuthCardProps {
  title: string;
  subtitle?: string;
  children?: ReactNode;
  footer?: ReactNode;
  hideLogo?: boolean;
}

export function AuthCard({ title, subtitle, children, footer, hideLogo = false }: AuthCardProps) {
  return (
    <div className="min-h-screen flex items-center justify-center bg-[var(--bg)] px-4 py-8">
      <div className="w-full max-w-md">
        {!hideLogo && (
          <div className="flex justify-center mb-8">
            <div
              data-testid="auth-logo-mark"
              aria-hidden
              className="w-16 h-16 rounded-2xl grid place-items-center text-2xl font-bold text-white"
              style={{
                background:
                  'linear-gradient(135deg, hsl(var(--e-game)) 0%, hsl(var(--e-event)) 50%, hsl(var(--e-player)) 100%)',
              }}
            >
              M
            </div>
          </div>
        )}
        <div className="bg-[var(--bg-card)] rounded-[var(--r-2xl,24px)] shadow-[var(--shadow-lg)] p-8 border border-[var(--border)]">
          <div className="text-center mb-6">
            <h1 className="text-2xl font-bold text-[var(--text)] font-heading">{title}</h1>
            {subtitle && <p className="text-sm text-[var(--text-sec)] mt-2">{subtitle}</p>}
          </div>
          {children}
        </div>
        {footer && <div className="text-center mt-6 text-sm text-[var(--text-sec)]">{footer}</div>}
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Verify pass**

```bash
pnpm vitest run src/components/ui/v2/__tests__/AuthCard.test.tsx
```
Expected: PASS (3 tests).

- [ ] **Step 5: Add Storybook story** (se progetto usa storybook)

```tsx
// src/components/ui/v2/AuthCard.stories.tsx
import type { Meta, StoryObj } from '@storybook/react';
import { AuthCard } from './AuthCard';

const meta: Meta<typeof AuthCard> = { component: AuthCard };
export default meta;
export const Login: StoryObj<typeof AuthCard> = {
  args: { title: 'Accedi', subtitle: 'Bentornato', children: <div>Form qui</div> },
};
```

- [ ] **Step 6: Export from index**

Modify `src/components/ui/v2/index.ts` — aggiungere `export * from './AuthCard';`.

- [ ] **Step 7: Commit**

```bash
git add src/components/ui/v2/AuthCard.tsx src/components/ui/v2/__tests__/AuthCard.test.tsx src/components/ui/v2/AuthCard.stories.tsx src/components/ui/v2/index.ts
git commit -m "feat(ui-v2): AuthCard primitive for auth flow migration"
```

---

## Task 2: OAuthButtons primitive

**Files:**
- Create: `src/components/ui/v2/OAuthButtons.tsx`
- Test: `src/components/ui/v2/__tests__/OAuthButtons.test.tsx`

- [ ] **Step 1: Test**

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { OAuthButtons } from '../OAuthButtons';

describe('OAuthButtons', () => {
  it('renders Google and Discord buttons', () => {
    render(<OAuthButtons onProvider={vi.fn()} />);
    expect(screen.getByRole('button', { name: /google/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /discord/i })).toBeInTheDocument();
  });

  it('calls onProvider with provider name on click', async () => {
    const fn = vi.fn();
    render(<OAuthButtons onProvider={fn} />);
    await userEvent.click(screen.getByRole('button', { name: /google/i }));
    expect(fn).toHaveBeenCalledWith('google');
  });
});
```

- [ ] **Step 2: Verify fail** — `pnpm vitest run src/components/ui/v2/__tests__/OAuthButtons.test.tsx`

- [ ] **Step 3: Implement**

```tsx
// src/components/ui/v2/OAuthButtons.tsx
import { Btn } from './Btn';

type Provider = 'google' | 'discord';
export interface OAuthButtonsProps {
  onProvider: (p: Provider) => void;
  disabled?: boolean;
}

export function OAuthButtons({ onProvider, disabled }: OAuthButtonsProps) {
  return (
    <div className="grid grid-cols-2 gap-3">
      <Btn variant="outline" disabled={disabled} onClick={() => onProvider('google')}>
        <span aria-hidden className="text-lg">🔵</span> Google
      </Btn>
      <Btn variant="outline" disabled={disabled} onClick={() => onProvider('discord')}>
        <span aria-hidden className="text-lg">💜</span> Discord
      </Btn>
    </div>
  );
}
```

- [ ] **Step 4: Verify pass** — `pnpm vitest run src/components/ui/v2/__tests__/OAuthButtons.test.tsx`

- [ ] **Step 5: Export + commit**

```bash
git add src/components/ui/v2/OAuthButtons.tsx src/components/ui/v2/__tests__/OAuthButtons.test.tsx src/components/ui/v2/index.ts
git commit -m "feat(ui-v2): OAuthButtons primitive"
```

---

## Task 3: Migrate Login page

**Files:**
- Read first: `admin-mockups/design_files/auth-flow.jsx` (screen 1 Login)
- Modify: `src/app/(auth)/login/page.tsx` + `_content.tsx`
- Test: `src/app/(auth)/login/__tests__/page.test.tsx` (crea se non esiste)
- E2E: `e2e/m6/auth-login.spec.ts`

- [ ] **Step 1: Read mockup source**

```bash
sed -n '1,120p' admin-mockups/design_files/auth-flow.jsx  # screen 1
```

- [ ] **Step 2: Write E2E expectation**

```ts
// e2e/m6/auth-login.spec.ts
import { test, expect } from '@playwright/test';

test('login page renders with AuthCard layout', async ({ page }) => {
  await page.goto('/login');
  await expect(page.getByRole('heading', { name: /accedi/i })).toBeVisible();
  await expect(page.getByLabel(/email/i)).toBeVisible();
  await expect(page.getByLabel(/password/i)).toBeVisible();
  await expect(page.getByRole('button', { name: /google/i })).toBeVisible();
  await expect(page.getByRole('link', { name: /registrati/i })).toBeVisible();
});
```

- [ ] **Step 3: Run to fail**

```bash
pnpm test:e2e -- e2e/m6/auth-login.spec.ts
```
Expected: FAIL (layout non ancora migrato).

- [ ] **Step 4: Rewrite `_content.tsx`** con AuthCard + OAuthButtons + Btn v2, mantenendo la logica auth esistente (hook `useLogin`, redirect post-auth, csrf).

Example skeleton:
```tsx
'use client';
import { AuthCard, OAuthButtons, Btn, Input, Label } from '@/components/ui/v2';
import Link from 'next/link';
import { useLoginMutation } from '@/hooks/useLogin'; // riuso logica esistente

export function LoginContent() {
  const login = useLoginMutation();
  return (
    <AuthCard
      title="Accedi"
      subtitle="Bentornato! Continua la tua avventura."
      footer={<>Non hai un account? <Link href="/register" className="underline">Registrati</Link></>}
    >
      <form onSubmit={login.submit} className="space-y-4">
        <div><Label htmlFor="email">Email</Label><Input id="email" type="email" required /></div>
        <div><Label htmlFor="password">Password</Label><Input id="password" type="password" required /></div>
        {login.error && <p role="alert" className="text-sm text-[hsl(var(--c-danger))]">{login.error}</p>}
        <Btn type="submit" variant="primary" entity="game" className="w-full" loading={login.pending}>Accedi</Btn>
      </form>
      <div className="my-4 flex items-center gap-2 text-xs text-[var(--text-muted)]"><hr className="flex-1" />oppure<hr className="flex-1" /></div>
      <OAuthButtons onProvider={login.oauth} />
      <p className="text-center text-xs text-[var(--text-sec)] mt-4">
        <Link href="/reset-password">Hai dimenticato la password?</Link>
      </p>
    </AuthCard>
  );
}
```

- [ ] **Step 5: Verify E2E passes**

```bash
pnpm test:e2e -- e2e/m6/auth-login.spec.ts
```
Expected: PASS.

- [ ] **Step 6: Unit test snapshot**

```bash
pnpm vitest run src/app/\(auth\)/login
```
Expected: PASS.

- [ ] **Step 7: Visual spot-check**

```bash
pnpm dev
# aprire http://localhost:3000/login in light + dark mode
# verificare: logo gradient, AuthCard centrato, bottone primary --c-game, social outline
```

- [ ] **Step 8: Commit**

```bash
git add src/app/\(auth\)/login e2e/m6/auth-login.spec.ts
git commit -m "feat(auth): migrate login page to v2 AuthCard design"
```

---

## Task 4: Migrate Register page

**Files:**
- Modify: `src/app/(auth)/register/page.tsx` (+ `_content.tsx` se esiste)
- Test: `src/app/(auth)/register/__tests__/page.test.tsx`
- E2E: `e2e/m6/auth-register.spec.ts`

Pattern identico a Task 3. Campi aggiuntivi: `username`, terms checkbox.

- [ ] Step 1-8 — replica struttura Task 3 con screen 2 Register del mockup JSX. Commit: `feat(auth): migrate register page to v2 AuthCard`.

---

## Task 5: Migrate Forgot + Reset + Verify pages

**Files:**
- Modify: `src/app/(auth)/reset-password/page.tsx` (forgot + reset in stesso route o `forgot-password`?)
- Modify: `src/app/(auth)/verify-email/page.tsx`
- Modify: `src/app/(auth)/setup-account/page.tsx` (2FA setup se presente)
- E2E: `e2e/m6/auth-recovery.spec.ts`

- [ ] Step 1-8 — replica pattern Task 3-4 per ogni page, un commit per page:
  - `feat(auth): migrate forgot-password page to v2`
  - `feat(auth): migrate reset-password page to v2`
  - `feat(auth): migrate verify-email page to v2`
  - `feat(auth): migrate 2FA setup page to v2` (se esistente)

---

## Task 6: StepProgress primitive

**Files:**
- Create: `src/components/ui/v2/StepProgress.tsx`
- Test: `src/components/ui/v2/__tests__/StepProgress.test.tsx`

- [ ] Step 1-7 — pattern Task 1. Component signature:

```tsx
interface StepProgressProps {
  steps: Array<{ id: string; label: string; entity?: EntityType }>;
  current: number; // 0-indexed
  onSkip?: () => void;
}
```

Renders: barra progresso top + pallini step + entity color cambia in base a `steps[current].entity`, skip link top-right.

Commit: `feat(ui-v2): StepProgress for multi-step flows`.

---

## Task 7: Migrate Onboarding flow

**Files:**
- Read first: `admin-mockups/design_files/onboarding.jsx`
- Modify: `src/app/(authenticated)/onboarding/page.tsx`
- Create: `src/app/(authenticated)/onboarding/_steps/{Welcome,Step1Games,Step2Agents,Step3Session,Complete}.tsx`
- Test: `src/app/(authenticated)/onboarding/__tests__/onboarding-flow.test.tsx`
- E2E: `e2e/m6/onboarding.spec.ts`

- [ ] **Step 1: Review mockup** — leggere tutti i 5 screen in onboarding.jsx, notare entity color per step (game → agent → session).

- [ ] **Step 2-4: Implementare stato multi-step** con Zustand o `useReducer`. Condividere store fra i 5 step.

- [ ] **Step 5-6: Ogni step component** = 1 screen mockup tradotto in React con primitivi v2.

- [ ] **Step 7: E2E** — completa flow start → complete.

- [ ] **Step 8: Commit** (split per step se utile):
  - `feat(onboarding): welcome step with v2 design`
  - `feat(onboarding): step1 choose games`
  - `feat(onboarding): step2 connect agents`
  - `feat(onboarding): step3 first session`
  - `feat(onboarding): complete step + confetti`

---

## Task 8: SettingsList + ToggleSwitch primitives

**Files:**
- Create: `src/components/ui/v2/SettingsList.tsx`, `SettingsRow.tsx`, `ToggleSwitch.tsx`
- Test: `src/components/ui/v2/__tests__/{SettingsList,ToggleSwitch}.test.tsx`

- [ ] Step 1-7 — 2 mini-pattern Task 1. `SettingsRow` accetta `icon`, `label`, `value`, `onPress` (mobile drawer) o nessun onPress (desktop split).

Commit: `feat(ui-v2): SettingsList + ToggleSwitch primitives`.

---

## Task 9: Migrate Settings hub + sottosezioni

**Files:**
- Modify: `src/app/(authenticated)/settings/page.tsx` (hub)
- Modify: `src/app/(authenticated)/settings/{profile,account,preferences,notifications}/page.tsx`
- Create: `src/app/(authenticated)/settings/{api-keys,services}/page.tsx`
- E2E: `e2e/m6/settings.spec.ts`

**Pattern desktop**: Sidebar+Drawer (nav sx fissa 240px, pannello dx). **Pattern mobile**: lista full-width, tap apre drawer full-screen.

- [ ] Step 1: Leggere `settings.jsx` — identificare desktop layout e mobile layout.

- [ ] Step 2-7: migrare ogni sottosezione, un commit per sezione:
  - `feat(settings): migrate hub to v2 Sidebar+Drawer pattern`
  - `feat(settings): migrate profile section`
  - `feat(settings): migrate account section (password + 2FA + delete)`
  - `feat(settings): migrate preferences (theme + language + density)`
  - `feat(settings): migrate notifications prefs (granular toggles)`
  - `feat(settings): add api-keys section for power users`
  - `feat(settings): add connected services (Google/Discord/BGG)`

- [ ] **Step 8: Destructive actions** — Delete account + Revoke API key usano `ConfirmDialog` Radix con `--e-event` (danger).

---

## Task 10: NotificationCard primitive

**Files:**
- Create: `src/components/ui/v2/NotificationCard.tsx`
- Test: `src/components/ui/v2/__tests__/NotificationCard.test.tsx`

Props: `{ entity: EntityType; title: string; body: string; timestamp: Date; read: boolean; onOpen?: () => void; onDismiss?: () => void }`.

Render: border-left 3px `hsl(var(--e))`, unread dot, relative timestamp, tint bg su unread.

- [ ] Step 1-7 — pattern Task 1. Commit: `feat(ui-v2): NotificationCard with entity border + unread state`.

---

## Task 11: Migrate Notifications area

**Files:**
- Create (se non esiste): `src/app/(authenticated)/notifications/page.tsx`
- Create: `src/app/(authenticated)/notifications/_components/{FeedList,DetailDrawer,EmptyState,FiltersBar}.tsx`
- Create: `src/hooks/useNotifications.ts` (se non esiste — altrimenti adapter)
- Test: `src/app/(authenticated)/notifications/__tests__/*`
- E2E: `e2e/m6/notifications.spec.ts`

- [ ] **Step 1**: verificare se API backend notifiche esiste (bc `UserNotifications`). Se sì, usare esistente; se no, mock via MSW.

- [ ] **Step 2-6**: Feed grouped by day (oggi / ieri / questa settimana / più vecchie), DetailDrawer usa `Drawer` v2 esistente, FiltersBar con `EntityChip` toggle-like, swipe-to-dismiss mobile via framer-motion `useDrag`.

- [ ] **Step 7: Badge counter in top nav** — se presente sistema nav, aggiornare `MiniNavSlot` o equivalente per mostrare contatore unread.

- [ ] **Step 8: Commit** (split):
  - `feat(notifications): feed list grouped by day`
  - `feat(notifications): detail drawer with context actions`
  - `feat(notifications): empty state + filters bar`
  - `feat(notifications): integrate badge counter in top nav`

---

## Task 12: PricingCard + HeroGradient primitives

**Files:**
- Create: `src/components/ui/v2/PricingCard.tsx`, `HeroGradient.tsx`
- Test: `src/components/ui/v2/__tests__/{PricingCard,HeroGradient}.test.tsx`

- [ ] Step 1-7 — 2 primitivi Task 1. HeroGradient uses `linear-gradient` entity-mix (come `00-hub.html .hero h1 .mark`). PricingCard highlighted variant wraps border `--c-game`.

Commit: `feat(ui-v2): PricingCard + HeroGradient primitives`.

---

## Task 13: Migrate Public pages

**Files:**
- Modify: `src/app/(public)/page.tsx` (Landing)
- Modify: `src/app/(public)/about/page.tsx`
- Modify: `src/app/(public)/{terms,privacy}/page.tsx`
- Create: `src/app/(public)/pricing/page.tsx`
- Create: `src/app/(public)/contact/page.tsx`
- E2E: `e2e/m6/public.spec.ts`

Desktop-first. Nav `.hub-nav` replicata da `00-hub.html`. Footer con link legali + social.

- [ ] **Step 1-8** — un commit per page:
  - `feat(public): migrate landing with HeroGradient + stats + features`
  - `feat(public): add pricing page with 3 PricingCard tiers`
  - `feat(public): migrate about page`
  - `feat(public): migrate terms/privacy with markdown styling`
  - `feat(public): add contact page with form + social`

---

## Task 14: Dark mode coverage audit

**Files:**
- Test: `e2e/m6/dark-mode-m6.spec.ts`
- Modify: qualsiasi page con contrast issue

- [ ] **Step 1: E2E dark mode smoke**

```ts
import { test, expect } from '@playwright/test';
for (const route of ['/login', '/register', '/onboarding', '/settings', '/notifications', '/', '/pricing']) {
  test(`${route} dark mode`, async ({ page }) => {
    await page.goto(route);
    await page.evaluate(() => document.documentElement.setAttribute('data-theme', 'dark'));
    await expect(page).toHaveScreenshot(`m6-${route.replace(/\//g, '_') || 'home'}-dark.png`, { maxDiffPixels: 1000 });
  });
}
```

- [ ] **Step 2: Run to capture baseline**

```bash
pnpm test:e2e -- e2e/m6/dark-mode-m6.spec.ts --update-snapshots
```

- [ ] **Step 3: Review screenshots** — verificare entity colors più chiari in dark (da `tokens.css:150-158`).

- [ ] **Step 4: Fix contrast issues** — per ogni area con issue, aggiungere classi `dark:` o variabili condizionali.

- [ ] **Step 5: Commit**

```bash
git add e2e/m6/dark-mode-m6.spec.ts __screenshots__
git commit -m "test(m6): dark mode coverage for all migrated areas"
```

---

## Task 15: Accessibility audit

**Files:**
- Test: `e2e/m6/a11y-m6.spec.ts`

- [ ] **Step 1: axe-core integration**

```ts
import { test, expect } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

for (const route of ['/login', '/register', '/onboarding', '/settings', '/notifications', '/', '/pricing']) {
  test(`${route} has no axe violations`, async ({ page }) => {
    await page.goto(route);
    const results = await new AxeBuilder({ page }).analyze();
    expect(results.violations).toEqual([]);
  });
}
```

- [ ] **Step 2: Run and fix**

```bash
pnpm test:e2e -- e2e/m6/a11y-m6.spec.ts
```

- [ ] **Step 3: Fix violations per route** (commit per fix).

- [ ] **Step 4: Commit test file**

```bash
git add e2e/m6/a11y-m6.spec.ts
git commit -m "test(m6): accessibility audit for all migrated areas"
```

---

## Task 16: Hub update + migration note

**Files:**
- Modify: `admin-mockups/design_files/00-hub.html` (nessun cambiamento — già include 10 pagine)
- Modify: `docs/superpowers/plans/2026-04-20-redesign-v2-full-app-migration-m1-m5.md` (aggiungere riferimento M6 completato)
- Create: `docs/frontend/m6-migration-notes.md` (post-mortem + screenshot comparison before/after)

- [ ] **Step 1: Write migration notes**

Document breve (500-800 parole):
- Cosa è stato migrato (5 aree)
- Pattern riusati da M1-M5
- Deviations dai mockup (se presenti)
- Lessons learned per M7+
- Screenshot prima/dopo per ogni area

- [ ] **Step 2: Commit**

```bash
git add docs/frontend/m6-migration-notes.md docs/superpowers/plans/2026-04-20-redesign-v2-full-app-migration-m1-m5.md
git commit -m "docs(m6): migration notes + pointer from m1-m5 plan"
```

---

## Task 17: Performance + bundle check

**Files:**
- Modify: `apps/web/bundle-size-baseline.json`

- [ ] **Step 1: Measure current bundle**

```bash
pnpm build 2>&1 | tee build-output.txt
```

- [ ] **Step 2: Compare vs baseline**

```bash
cat bundle-size-baseline.json | jq .
# check delta - expected +5-8% for new primitives, acceptable <10%
```

- [ ] **Step 3: Update baseline se entro threshold**

```bash
# Se delta <10% e test passano
pnpm update-bundle-baseline  # se script esiste, altrimenti manuale
git add bundle-size-baseline.json
git commit -m "chore: update bundle-size baseline post M6 migration"
```

- [ ] **Step 4: Lighthouse mobile**

```bash
pnpm lighthouse -- --mobile /login /register /onboarding /settings /notifications
```

Expected: Performance ≥85, Accessibility ≥95, Best Practices ≥95.

---

## Task 18: Integration smoke + merge

- [ ] **Step 1: Run full test suite**

```bash
pnpm vitest run && pnpm test:e2e && pnpm typecheck && pnpm lint
```

Expected: ALL PASS.

- [ ] **Step 2: Create PR redesign/v2-m6 → redesign/v2** (non `main-dev`!)

```bash
git push -u origin redesign/v2-m6
gh pr create --base redesign/v2 --title "M6: Gruppo 1 migration (Auth + Onboarding + Settings + Notifications + Public)" --body "$(cat <<'EOF'
## Summary
- Migrated 5 critical app areas to Design System v1 mockups
- Added 9 new v2 primitives: AuthCard, OAuthButtons, StepProgress, SettingsList, SettingsRow, ToggleSwitch, NotificationCard, PricingCard, HeroGradient
- Full dark mode + a11y coverage
- Token drift reconciled (--e-document, --e-toolkit)

## Areas migrated
- ✅ Auth (login, register, forgot, reset, verify, 2FA)
- ✅ Onboarding (5-step flow)
- ✅ Settings (7 sections)
- ✅ Notifications (feed + drawer + filters)
- ✅ Public (landing, pricing, about, terms, contact)

## Test plan
- [ ] Unit tests passing (target 85%+ coverage)
- [ ] E2E smoke for each area
- [ ] Dark mode screenshots verified
- [ ] axe-core zero violations
- [ ] Lighthouse ≥85 mobile

## Not in scope
- Gruppo 2 (game-night, play-records, session-wizard) → M7
- Gruppo 4 (profile-player, game-detail, chat-desktop, upload-wizard, live-session, toolkit-ecosystem) → M9

Depends on M1-M2 primitivi. Merges into `redesign/v2` (NOT main-dev).
EOF
)"
```

- [ ] **Step 3: Wait for CI + code review**

- [ ] **Step 4: Merge after approval**

Squash merge preferred. Update MEMORY.md session log.

---

## Rollback plan

Se in produzione viene rilevato un blocker dopo merge `redesign/v2` → `main-dev`:
1. Route-level rollback: revert commit specifico via feature flag `NEXT_PUBLIC_USE_V2_AUTH=false` (default false fino a validazione)
2. Se feature flag non presente: `git revert <squash-sha>` su `redesign/v2` + nuovo PR fast-track
3. I primitivi v2 in `components/ui/v2/` restano (non-breaking) — si disattiva solo l'uso nelle pages

---

## Success criteria

- [ ] Tutte 5 aree Gruppo 1 riscritte con primitivi v2
- [ ] 9 nuovi primitivi v2 aggiunti + test unit + Storybook stories
- [ ] Dark mode testato su ogni area
- [ ] A11y violations = 0 (axe-core)
- [ ] Lighthouse mobile ≥85 perf / ≥95 a11y
- [ ] Bundle delta <10% vs baseline pre-M6
- [ ] Full suite green (vitest + e2e + typecheck + lint)
- [ ] PR merged in `redesign/v2`
- [ ] Migration notes pubblicate in `docs/frontend/m6-migration-notes.md`

---

## Next plans

- **M7** — Gruppo 2 (game-night, play-records, session-wizard) — richiede mockup Claude Design non ancora creati. Brief aggiornato in `2026-04-20-claude-design-missing-pages-brief.md`.
- **M8** — Gruppo 3 (editor-pipeline, calendar) opzionale.
- **M9** — Gruppo 4 (profile-player, game-detail, chat-desktop, upload-wizard, live-session, toolkit-ecosystem) — brief pronto in `specs/2026-04-20-claude-design-group4-extended-brief.md`.

Quando tutti gli M6-M9 saranno completati, l'app sarà al 100% sul Design System v1, sostituendo completamente l'interfaccia legacy consumer (admin area esclusa — governata da sessioni separate).

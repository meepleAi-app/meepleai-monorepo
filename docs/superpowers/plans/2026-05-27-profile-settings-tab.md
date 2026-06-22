# Profile Settings Tab + 2FA Consolidation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare il 4° tab "Settings" in `/profile` con sub-section routing, rendendo raggiungibile l'enrollment 2FA (sblocca SP5 S3 cutover, chiude #1608).

**Architecture:** `ProfilePage` URL-driven via `useSearchParams` (wrappato in `<Suspense>` per il CSR-bailout di Next 16). Un `SettingsTab` container instrada 7 section via `?section=`. La Security section re-skinna i pattern del mockup `sp5-profile-settings.jsx` MA preserva il wiring BE reale del `SecuritySettingsPage` esistente (che viene poi eliminato). Mobile via `<Drawer>` primitive esistente.

**Tech Stack:** Next.js 16 App Router, React 19, TanStack Query, Tailwind 4 (entity tokens), Vitest, Playwright. `api.auth.*` client (`apps/web/src/lib/api/clients/authClient.ts`).

**Spec:** `docs/for-developers/specs/2026-05-27-profile-settings-tab-design.md`
**Mockup ground-truth:** `admin-mockups/design_files/sp5-profile-settings.jsx` (committed `8931abd17`)

---

## Conventions (read before any task)

- **Component path:** `apps/web/src/components/features/settings/`. PascalCase files, co-located `__tests__/`.
- **Tokens:** entity utilities are `bg-entity-kb` / `text-entity-kb` / `border-entity-kb` (NOT `bg-kb`). Semantic: `text-warning`, `bg-success`, `border-border`, `text-foreground`, `text-muted-foreground`. **Static literals only** — never `bg-entity-${var}` (purged). Never inline `hsl()` in `features/**` (ESLint `meepleai/no-inline-hsl-v2` = error).
- **Reuse primitives:** `@/components/ui/settings-list` (`SettingsList`), `@/components/ui/settings-row` (`SettingsRow`, has `entity` prop), `@/components/ui/drawer` (`Drawer`, vaul-backed, `useDrawerBreakpoint`), `@/components/ui/overlays/dialog`, `@/components/ui/primitives/{button,input}`.
- **Mockup reference:** when a task says "port markup from `<Component>` in sp5-profile-settings.jsx", open that file, copy the JSX structure, then (a) swap dynamic `bg-${entity}` → static entity literal, (b) replace mock data with the wired hook, (c) add the specified `data-testid`.
- **Run tests:** `cd apps/web && pnpm test -- <path>` (Vitest), `pnpm test:e2e -- <spec>` (Playwright), `pnpm typecheck`, `pnpm lint`.
- **Branch:** `feature/issue-1608-settings-tab` (already created from main-dev).

---

## Plan revision (3-agent review fixes — apply DURING execution)

These fixes correct issues raised by the post-write review. Each task below applies them inline; this section is the canonical errata to consult.

**[I1 — B5 import order]** `SecuritySection` in B5 must NOT import `TwoFactorBottomSheet`. B5 renders only `TwoFactorSetupModal` (desktop wizard works on every viewport). D1 introduces both the bottom-sheet file AND the breakpoint switch in `SecuritySection` atomically. This avoids the compile-break between B5 and D1.

**[I2 — `SettingsRow` aria-current]** Before A2 Step 3, add a micro-step that extends `apps/web/src/components/ui/settings-row/settings-row.tsx`: (a) `SettingsRowProps` accepts `aria-current?: 'true'|'false'|'page'|'step'|'location'|'date'|'time'`, (b) forward it on the inner element. Run `pnpm typecheck` immediately. Commit with the SubNav.

**[I3 — E1 e2e scope]** E1 Step 2 must enumerate which test cases (a) get rewritten for the new URL/DOM (tab labels Overview/Achievements/Activity/Settings + section sidebar items via `data-testid`), (b) get **explicitly deleted with rationale** (e.g. change-password / delete-account tests have no corresponding UI in this design — they belong in a separate feature and were never spec'd here; document the deletion + open an issue if the underlying flow still exists in the codebase), (c) get deferred. Do not silently delete coverage.

**[I4 — B4 success:false test]** Add a third test to `TwoFactorSetupModal.test.tsx`:
```tsx
it('shows OTP error when enable2FA returns success: false', async () => {
  vi.mocked(api.auth.enable2FA).mockResolvedValueOnce({ success: false, errorMessage: 'Invalid code', backupCodes: null });
  // render at step 2, submit a 6-digit code, assert error banner + animate-shake on OTPInput6Slot
});
```

**[I5 — A3 stub files]** A3 Step 4 must explicitly create ALL FIVE stub files before `SettingsTab.tsx` imports them. Each stub:
```tsx
// sections/<SectionName>.tsx (PHASE-A STUB — replaced in B5/C1-C4)
import { SectionPlaceholder } from './SectionPlaceholder';
import { SETTINGS_SECTIONS } from '../settings-sections';
export function <SectionName>() {
  const def = SETTINGS_SECTIONS.find(s => s.id === '<section-id>')!;
  return <SectionPlaceholder section={def} />;
}
```
Files to create: `SecuritySection.tsx`, `ProfileSection.tsx`, `PreferencesSection.tsx`, `ApiKeysSection.tsx`, `AiConsentSection.tsx` (each with the corresponding `id`).

**[I6 — A4 typecheck gate]** Add a typecheck step between A4 Step 4 (server-component conversion) and A4 Step 5 (final tests): `cd apps/web && pnpm typecheck`. Catches client-only hooks leaking into the new server component.

**[I7 — E3 TOTP minting]** Before E3 Step 1, add **Step 0**: investigate `apps/web/e2e/` for an existing TOTP mint helper (`grep -rn "OTPAuth\|otpauth\|speakeasy\|totp" apps/web/e2e`). If absent, create `apps/web/e2e/fixtures/totp.ts` using the `otpauth` library (already a transitive dep via Next/Node; verify with `pnpm list otpauth`). The helper signature: `mintTotp(secret: string): string`. E3's enrollment test imports this. If neither helper nor a stable e2e test-account with 2FA pre-seeded exists, document the test as `.skip` with the rationale; do NOT let it become a trivially-passing stub.

**[I8 — commit hygiene]** Throughout the plan, every commit step uses **explicit `git add <paths>`** before `git commit -m "..."` (never `commit -am` for tasks that create new files). Override the `-am` invocations in B2 Step 5, B3 Step 5, B5 Step 6, C1 Step 5, C2 Step 5, C3 Step 5 to:
```bash
git add apps/web/src/components/features/settings/<area>/ apps/web/src/components/features/settings/__tests__/<test>
git commit -m "<msg>"
```

**[I9 — orphan settings/* pages]** Add new task **C5** before Phase D: delete the remaining standalone pages under `apps/web/src/app/(authenticated)/settings/` (`notifications/page.tsx`, `preferences/page.tsx`, `api-keys/page.tsx`, `services/page.tsx`, `profile/page.tsx`, plus client siblings like `notifications/client.tsx`). Before each `git rm`, verify no `<Link href="/settings/<name>">` consumer remains (`grep -rn`); update any to query-param URL. The catch-all redirect in `next.config.js` keeps the routes externally valid. Run `pnpm typecheck` after.

**[G1 — BackupCodesView copy/download test]** Add two test cases to B2 Step 1:
```tsx
it('copies all codes to clipboard on "Copy all"', async () => {
  const writeText = vi.fn().mockResolvedValue(undefined);
  Object.assign(navigator, { clipboard: { writeText } });
  render(<BackupCodesView codes={CODES} acked={false} onAck={()=>{}} />);
  fireEvent.click(screen.getByRole('button', { name: /copy all/i }));
  expect(writeText).toHaveBeenCalledWith(CODES.join('\n'));
});
it('triggers download on "Download .txt"', () => {
  const createObjectURL = vi.fn(() => 'blob:x');
  Object.assign(URL, { createObjectURL, revokeObjectURL: vi.fn() });
  render(<BackupCodesView codes={CODES} acked={false} onAck={()=>{}} />);
  fireEvent.click(screen.getByRole('button', { name: /download/i }));
  expect(createObjectURL).toHaveBeenCalled();
});
```

**[G2 — Lockout countdown banner]** B4 Step 3 (TwoFactorStatusCard) and B4 Step 4 (TwoFactorSetupModal): when `enable2FA` returns `success: false` AND `errorMessage` includes `locked_out` (or BE adds `retryAfterSeconds` to the result — check the actual shape via `apps/web/src/lib/api/schemas/auth.schemas.ts` `Enable2FAResult` first; if `retryAfterSeconds` is absent the FE can only display the message text without a live countdown), render an inline banner with the parsed countdown (`fmtMmSs(seconds)`, `setInterval` decrementing in `useEffect`). Add test in `TwoFactorSetupModal.test.tsx`:
```tsx
it('renders lockout countdown when BE returns locked_out', async () => {
  vi.mocked(api.auth.enable2FA).mockResolvedValueOnce({ success: false, errorMessage: 'locked_out: 900', backupCodes: null });
  // assert banner visible with "15:00" or similar
});
```
If the wire format doesn't include the seconds, render only the static message — do NOT fake a countdown.

**[G3 — Per-section error card]** Update `SettingsTab.tsx` in A3 Step 4: wrap each real-section render in a per-section error boundary. Use a lightweight `<SectionErrorCard>` component (new file `sections/SectionErrorCard.tsx`) rendered when the section's primary `useQuery` reports `isError`. Pattern: each real section exposes `isError` state from its top-level `useQuery` and renders `<SectionErrorCard>` instead of its normal content. Add unit test in `SettingsTab.test.tsx`:
```tsx
it('renders SectionErrorCard when a section reports isError', () => {
  // mock SecuritySection to throw or surface isError prop; render SettingsTab with activeSection='security'
  // assert error card UI with retry button
});
```
The error card has a "Retry" button calling `queryClient.invalidateQueries({queryKey: [<section-key>]})`.

**[G4 — OTPInput6Slot disabled test]** Add to B1 Step 1:
```tsx
it('disables all inputs when disabled prop is set', () => {
  render(<OTPInput6Slot onComplete={()=>{}} disabled />);
  screen.getAllByRole('textbox').forEach(i => expect(i).toBeDisabled());
});
```

**[G5 — Invalid-section fallback unit test]** Add to A4 Step 1:
```tsx
it('replaces the URL when section param is invalid', () => {
  const replace = vi.fn();
  vi.mocked(useSearchParams).mockReturnValue(new URLSearchParams('tab=settings&section=BOGUS'));
  vi.mocked(useRouter).mockReturnValue({ replace, push: vi.fn() } as any);
  render(<ProfilePageContent />);
  expect(replace).toHaveBeenCalledWith('/profile?tab=settings&section=profile', { scroll: false });
});
```
The `ProfilePageContent` implements this via a `useEffect` that detects the invalid section and calls `setQuery({ section: DEFAULT_SECTION })`.

---

## File Structure

**Create:**
- `apps/web/src/app/(authenticated)/profile/_components/ProfilePageContent.tsx` — client content (useSearchParams)
- `apps/web/src/components/features/settings/SettingsTab.tsx` — container + section router
- `apps/web/src/components/features/settings/SettingsSubNav.tsx` — sidebar/list nav
- `apps/web/src/components/features/settings/settings-sections.ts` — SECTION config (id, label, entity, icon)
- `apps/web/src/components/features/settings/sections/{Security,Profile,Preferences,ApiKeys,AiConsent}Section.tsx`
- `apps/web/src/components/features/settings/sections/SectionPlaceholder.tsx`
- `apps/web/src/components/features/settings/sections/SectionErrorCard.tsx` (per-section error boundary, see G3)
- `apps/web/e2e/fixtures/totp.ts` (TOTP mint helper if absent, see I7)
- `apps/web/src/components/features/settings/two-factor/{TwoFactorStatusCard,TwoFactorSetupModal,TwoFactorBottomSheet,TwoFactorDisableDialog,OTPInput6Slot,BackupCodesView,ActiveSessionsCard}.tsx`
- `apps/web/src/components/features/settings/__tests__/*.test.tsx`
- `apps/web/e2e/settings/settings-tab-2fa.spec.ts`

**Modify:**
- `apps/web/src/app/(authenticated)/profile/page.tsx` — Suspense wrapper
- `apps/web/next.config.js` — add `/settings/ai-consent` redirect
- `apps/web/src/app/(authenticated)/profile/__tests__/page.test.tsx` — 4 tabs + useSearchParams mock
- `apps/web/e2e/settings/profile-settings.spec.ts` — new URL/DOM shape (25 tests)
- `apps/web/e2e/auth/{device-management,login-notifications,remember-me}.spec.ts`, `auth.spec.ts`, `gap-analysis-critical.spec.ts` — redirect follow + data-testid

**Delete (after migration):**
- `apps/web/src/app/(authenticated)/settings/security/page.tsx`
- `apps/web/src/app/(authenticated)/settings/ai-consent/page.tsx`
- `apps/web/src/app/(authenticated)/settings/page.tsx` (verify no other consumer first — Task A0)

---

## Phase A — Routing scaffold (commit 1)

### Task A0: Verify pre-conditions + inventory existing settings routes

**Files:** read-only.

- [ ] **Step 1: Inventory `/settings/*` routes + consumers**

Run: `ls apps/web/src/app/\(authenticated\)/settings/` and `grep -rn "settings/page\|SettingsHubPage\|/settings'" apps/web/src --include=*.tsx | head`
Expected: confirm which `settings/*` pages exist (security, ai-consent, page hub, others) and what links to `/settings`.

- [ ] **Step 2: Confirm redirects already present**

Run: `grep -n "settings" apps/web/next.config.js`
Expected: see `/settings/security`, `/settings/notifications`, `/settings`, catch-all `/settings/:path*` already mapped. Note the catch-all line number.

- [ ] **Step 3: Confirm `<Drawer>` + `SettingsRow` APIs**

Read: `apps/web/src/components/ui/drawer/drawer.tsx`, `apps/web/src/components/ui/settings-row/settings-row.tsx`. Note the exact prop names (`entity`, `side`, `open`, `onOpenChange`, breakpoint hook). No code change — this is to ground later tasks.

- [ ] **Step 4: Commit (no-op marker — skip if nothing changed)**

No commit (read-only task). Proceed to A1.

---

### Task A1: SECTION config

**Files:**
- Create: `apps/web/src/components/features/settings/settings-sections.ts`
- Test: `apps/web/src/components/features/settings/__tests__/settings-sections.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
// __tests__/settings-sections.test.ts
import { describe, it, expect } from 'vitest';
import { SETTINGS_SECTIONS, isValidSection, DEFAULT_SECTION } from '../settings-sections';

describe('settings-sections', () => {
  it('defines 7 sections in order', () => {
    expect(SETTINGS_SECTIONS.map(s => s.id)).toEqual([
      'profile', 'security', 'ai-consent', 'notifications', 'preferences', 'api-keys', 'services',
    ]);
  });
  it('default section is profile', () => {
    expect(DEFAULT_SECTION).toBe('profile');
  });
  it('validates known section ids', () => {
    expect(isValidSection('security')).toBe(true);
    expect(isValidSection('xyz')).toBe(false);
  });
  it('marks placeholder sections', () => {
    const notif = SETTINGS_SECTIONS.find(s => s.id === 'notifications');
    expect(notif?.placeholder).toBe(true);
    const sec = SETTINGS_SECTIONS.find(s => s.id === 'security');
    expect(sec?.placeholder).toBeFalsy();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test -- settings-sections`
Expected: FAIL — module not found.

- [ ] **Step 3: Write implementation**

```ts
// settings-sections.ts
import type { LucideIcon } from 'lucide-react';
import { User, Shield, FileCheck, Bell, Settings as Cog, Key, Link2 } from 'lucide-react';

export type SettingsSectionId =
  | 'profile' | 'security' | 'ai-consent' | 'notifications' | 'preferences' | 'api-keys' | 'services';

export interface SettingsSectionDef {
  id: SettingsSectionId;
  label: string;
  subtitle: string;
  /** static entity utility prefix, e.g. 'entity-kb' → bg-entity-kb */
  entity: 'entity-player' | 'entity-kb' | 'entity-chat' | 'entity-tool' | 'entity-agent' | 'entity-toolkit';
  icon: LucideIcon;
  placeholder?: boolean;
}

export const SETTINGS_SECTIONS: SettingsSectionDef[] = [
  { id: 'profile',       label: 'Profile',            subtitle: 'Avatar, display name, lingua', entity: 'entity-player',  icon: User },
  { id: 'security',      label: 'Security',           subtitle: 'Manage 2FA, sessioni',         entity: 'entity-kb',      icon: Shield },
  { id: 'ai-consent',    label: 'AI & data consent',  subtitle: 'GDPR, data retention',          entity: 'entity-kb',      icon: FileCheck },
  { id: 'notifications', label: 'Notifications',      subtitle: 'Email, push, digest',           entity: 'entity-chat',    icon: Bell, placeholder: true },
  { id: 'preferences',   label: 'Preferences',        subtitle: 'Theme, lingua',                 entity: 'entity-tool',    icon: Cog },
  { id: 'api-keys',      label: 'API keys',           subtitle: 'Token per integrazioni',        entity: 'entity-agent',   icon: Key },
  { id: 'services',      label: 'Connected services', subtitle: 'BGG, Discord',                  entity: 'entity-toolkit', icon: Link2, placeholder: true },
];

export const DEFAULT_SECTION: SettingsSectionId = 'profile';

const VALID = new Set(SETTINGS_SECTIONS.map(s => s.id));
export function isValidSection(v: string | null | undefined): v is SettingsSectionId {
  return v != null && VALID.has(v as SettingsSectionId);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm test -- settings-sections`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/settings/settings-sections.ts apps/web/src/components/features/settings/__tests__/settings-sections.test.ts
git commit -m "feat(settings): section config + validation (#1608)"
```

---

### Task A2: SettingsSubNav

**Files:**
- Create: `apps/web/src/components/features/settings/SettingsSubNav.tsx`
- Test: `apps/web/src/components/features/settings/__tests__/SettingsSubNav.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// __tests__/SettingsSubNav.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { SettingsSubNav } from '../SettingsSubNav';

describe('SettingsSubNav', () => {
  it('renders all 7 section labels', () => {
    render(<SettingsSubNav active="profile" onSelect={() => {}} twoFactorEnabled={false} />);
    ['Profile','Security','AI & data consent','Notifications','Preferences','API keys','Connected services']
      .forEach(l => expect(screen.getByText(l)).toBeInTheDocument());
  });
  it('shows 2FA-off badge on Security when disabled', () => {
    render(<SettingsSubNav active="profile" onSelect={() => {}} twoFactorEnabled={false} />);
    expect(screen.getByTestId('subnav-2fa-badge')).toHaveTextContent(/2fa off/i);
  });
  it('hides 2FA-off badge when enabled', () => {
    render(<SettingsSubNav active="profile" onSelect={() => {}} twoFactorEnabled />);
    expect(screen.queryByTestId('subnav-2fa-badge')).toBeNull();
  });
  it('calls onSelect with section id on click', () => {
    const onSelect = vi.fn();
    render(<SettingsSubNav active="profile" onSelect={onSelect} twoFactorEnabled={false} />);
    fireEvent.click(screen.getByText('Security'));
    expect(onSelect).toHaveBeenCalledWith('security');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test -- SettingsSubNav`
Expected: FAIL — module not found.

- [ ] **Step 3: Write implementation**

Port the sub-nav markup from `SettingsSubNav` in `sp5-profile-settings.jsx` (desktop sidebar variant + mobile list). Adapt: dynamic `bg-${sec.entity}` → static via a `switch` returning literal classes; reuse `SettingsRow` from `@/components/ui/settings-row`. Key shape:

```tsx
'use client';
import { SETTINGS_SECTIONS, type SettingsSectionId } from './settings-sections';
import { SettingsList } from '@/components/ui/settings-list';
import { SettingsRow } from '@/components/ui/settings-row';

interface Props {
  active: SettingsSectionId;
  onSelect: (id: SettingsSectionId) => void;
  twoFactorEnabled: boolean;
}

export function SettingsSubNav({ active, onSelect, twoFactorEnabled }: Props) {
  return (
    <nav aria-label="Settings sections" className="md:w-60 md:shrink-0">
      <SettingsList ariaLabel="Settings sections">
        {SETTINGS_SECTIONS.map(sec => {
          const Icon = sec.icon;
          const showBadge = sec.id === 'security' && !twoFactorEnabled;
          return (
            <SettingsRow
              key={sec.id}
              icon={<Icon className="h-5 w-5" aria-hidden />}
              label={sec.label}
              description={sec.subtitle}
              entity={sec.id === 'security' || sec.id === 'ai-consent' ? 'kb' : undefined}
              onClick={() => onSelect(sec.id)}
              aria-current={active === sec.id ? 'true' : undefined}
              trailing={showBadge
                ? <span data-testid="subnav-2fa-badge" className="font-mono text-[10px] font-bold px-1.5 py-0.5 rounded-sm bg-warning/15 text-warning">⚠ 2FA off</span>
                : undefined}
            />
          );
        })}
      </SettingsList>
    </nav>
  );
}
```

> Note: if `SettingsRow` lacks an `aria-current` passthrough, add it minimally to the primitive (it forwards `...rest`). Verify in Task A0 Step 3.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm test -- SettingsSubNav`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/settings/SettingsSubNav.tsx apps/web/src/components/features/settings/__tests__/SettingsSubNav.test.tsx
git commit -m "feat(settings): sub-nav with 2FA-off badge (#1608)"
```

---

### Task A3: SectionPlaceholder + SettingsTab container (section router)

**Files:**
- Create: `apps/web/src/components/features/settings/sections/SectionPlaceholder.tsx`
- Create: `apps/web/src/components/features/settings/SettingsTab.tsx`
- Test: `apps/web/src/components/features/settings/__tests__/SettingsTab.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// __tests__/SettingsTab.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { SettingsTab } from '../SettingsTab';

// Stub heavy sections to keep the router test focused
vi.mock('../sections/SecuritySection', () => ({ SecuritySection: () => <div data-testid="sec-security" /> }));
vi.mock('../sections/ProfileSection', () => ({ ProfileSection: () => <div data-testid="sec-profile" /> }));

describe('SettingsTab router', () => {
  it('renders Security section when section=security', () => {
    render(<SettingsTab activeSection="security" onChangeSection={() => {}} />);
    expect(screen.getByTestId('sec-security')).toBeInTheDocument();
  });
  it('renders placeholder for notifications', () => {
    render(<SettingsTab activeSection="notifications" onChangeSection={() => {}} />);
    expect(screen.getByText(/in development|coming soon/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test -- SettingsTab`
Expected: FAIL — module not found.

- [ ] **Step 3: Write SectionPlaceholder**

```tsx
// sections/SectionPlaceholder.tsx
import type { SettingsSectionDef } from '../settings-sections';

export function SectionPlaceholder({ section }: { section: SettingsSectionDef }) {
  const Icon = section.icon;
  return (
    <div className="text-center py-14 px-6 bg-card border border-dashed border-border rounded-lg">
      <div className="w-14 h-14 rounded-lg flex items-center justify-center mx-auto mb-3 bg-muted text-muted-foreground">
        <Icon className="h-7 w-7" aria-hidden />
      </div>
      <h3 className="font-quicksand font-bold text-lg text-foreground">{section.label}</h3>
      <p className="text-sm text-muted-foreground mt-1">Settings UI in development.</p>
    </div>
  );
}
```

- [ ] **Step 4: Write SettingsTab (router); use placeholder for not-yet-built sections**

```tsx
// SettingsTab.tsx
'use client';
import { SETTINGS_SECTIONS, type SettingsSectionId } from './settings-sections';
import { SettingsSubNav } from './SettingsSubNav';
import { SectionPlaceholder } from './sections/SectionPlaceholder';
import { SecuritySection } from './sections/SecuritySection';
import { ProfileSection } from './sections/ProfileSection';
import { PreferencesSection } from './sections/PreferencesSection';
import { ApiKeysSection } from './sections/ApiKeysSection';
import { AiConsentSection } from './sections/AiConsentSection';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

interface Props {
  activeSection: SettingsSectionId;
  onChangeSection: (id: SettingsSectionId) => void;
}

export function SettingsTab({ activeSection, onChangeSection }: Props) {
  const { data: status } = useQuery({ queryKey: ['2fa-status'], queryFn: () => api.auth.getTwoFactorStatus() });
  const def = SETTINGS_SECTIONS.find(s => s.id === activeSection)!;
  return (
    <div className="flex flex-col md:flex-row gap-6">
      <SettingsSubNav active={activeSection} onSelect={onChangeSection} twoFactorEnabled={status?.isEnabled ?? false} />
      <div className="flex-1 min-w-0">
        {activeSection === 'security' && <SecuritySection />}
        {activeSection === 'profile' && <ProfileSection />}
        {activeSection === 'preferences' && <PreferencesSection />}
        {activeSection === 'api-keys' && <ApiKeysSection />}
        {activeSection === 'ai-consent' && <AiConsentSection />}
        {def.placeholder && <SectionPlaceholder section={def} />}
      </div>
    </div>
  );
}
```

> During Phase A the real section components don't exist yet. Create temporary stub files exporting the named component (`export function SecuritySection(){return <SectionPlaceholder .../>}`) so the import compiles; Phase B/C replace them. Add the stubs in this step.

- [ ] **Step 5: Run test to verify it passes**

Run: `cd apps/web && pnpm test -- SettingsTab`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/features/settings/
git commit -m "feat(settings): tab container + section router + placeholders (#1608)"
```

---

### Task A4: ProfilePage → URL-driven + Suspense + 4th tab

**Files:**
- Create: `apps/web/src/app/(authenticated)/profile/_components/ProfilePageContent.tsx`
- Modify: `apps/web/src/app/(authenticated)/profile/page.tsx`
- Modify: `apps/web/src/app/(authenticated)/profile/__tests__/page.test.tsx`

- [ ] **Step 1: Update the unit test (4 tabs + searchParams mock)**

In `page.test.tsx`, add the navigation mock + the 4th-tab assertion:

```tsx
vi.mock('next/navigation', () => ({
  useSearchParams: () => new URLSearchParams('tab=settings&section=security'),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
  usePathname: () => '/profile',
}));
// update existing "three tabs" test:
it('renders tab bar with four tabs incl. Settings', () => {
  // ...render ProfilePage...
  ['Overview','Achievements','Activity','Settings'].forEach(t =>
    expect(screen.getByRole('tab', { name: t })).toBeInTheDocument());
});
it('activates the Settings tab from ?tab=settings', () => {
  // render → Settings tab is aria-selected, SettingsTab content visible
  expect(screen.getByRole('tab', { name: 'Settings' })).toHaveAttribute('aria-selected', 'true');
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test -- profile/__tests__/page`
Expected: FAIL — Settings tab absent / useState-based page ignores searchParams.

- [ ] **Step 3: Extract ProfilePageContent (client) with useSearchParams**

Move the entire current body of `ProfilePage` (hooks, header, TabBar, tab content) into a new `ProfilePageContent.tsx` ('use client'). Replace the `useState<Tab>('overview')` with:

```tsx
'use client';
import { useSearchParams, useRouter, usePathname } from 'next/navigation';
import { isValidSection, DEFAULT_SECTION, type SettingsSectionId } from '@/components/features/settings/settings-sections';
import { SettingsTab } from '@/components/features/settings/SettingsTab';

type Tab = 'overview' | 'achievements' | 'activity' | 'settings';
const VALID_TABS = new Set<Tab>(['overview','achievements','activity','settings']);

export function ProfilePageContent() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();

  const rawTab = searchParams.get('tab');
  const activeTab: Tab = VALID_TABS.has(rawTab as Tab) ? (rawTab as Tab) : 'overview';
  const rawSection = searchParams.get('section');
  const activeSection: SettingsSectionId = isValidSection(rawSection) ? rawSection : DEFAULT_SECTION;

  function setQuery(next: Partial<{ tab: Tab; section: SettingsSectionId }>) {
    const params = new URLSearchParams(searchParams.toString());
    if (next.tab) params.set('tab', next.tab);
    if (next.section) params.set('section', next.section);
    if (next.tab && next.tab !== 'settings') params.delete('section');
    router.replace(`${pathname}?${params.toString()}`, { scroll: false });
  }

  // ... existing header / TabBar (add 'settings' tab) ...
  // TabBar onChange={(t) => setQuery({ tab: t, ...(t === 'settings' ? { section: DEFAULT_SECTION } : {}) })}
  // content:
  // {activeTab === 'settings' && <SettingsTab activeSection={activeSection} onChangeSection={(s) => setQuery({ tab: 'settings', section: s })} />}
}
```

Add `'settings'` to the `TabBar` tabs array (label "Settings", icon `Settings` from lucide) with `role="tab"` + `aria-selected`.

- [ ] **Step 4: Thin page.tsx with Suspense**

```tsx
// profile/page.tsx
import { Suspense } from 'react';
import { ProfilePageContent } from './_components/ProfilePageContent';

export default function ProfilePage() {
  return (
    <Suspense fallback={<div className="min-h-screen bg-background" />}>
      <ProfilePageContent />
    </Suspense>
  );
}
```

Remove `'use client'` from page.tsx (now a server component wrapping the client child).

- [ ] **Step 5: Run tests + typecheck**

Run: `cd apps/web && pnpm test -- profile/__tests__/page && pnpm typecheck`
Expected: PASS; no type errors.

- [ ] **Step 6: Manual smoke (document, not automated here)**

Run `pnpm dev`, open `/profile?tab=settings&section=security` → Settings tab active, Security stub renders. `/settings/security` → redirect → same.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/profile/
git commit -m "feat(profile): URL-driven tabs + Suspense + Settings tab scaffold (#1608)"
```

---

## Phase B — Security section (commit 2)

### Task B1: OTPInput6Slot (wired to enable2FA)

**Files:**
- Create: `apps/web/src/components/features/settings/two-factor/OTPInput6Slot.tsx`
- Test: `apps/web/src/components/features/settings/__tests__/OTPInput6Slot.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { OTPInput6Slot } from '../two-factor/OTPInput6Slot';

describe('OTPInput6Slot', () => {
  it('auto-advances and calls onComplete with 6 digits', () => {
    const onComplete = vi.fn();
    render(<OTPInput6Slot onComplete={onComplete} />);
    const inputs = screen.getAllByRole('textbox');
    '123456'.split('').forEach((d, i) => fireEvent.change(inputs[i], { target: { value: d } }));
    expect(onComplete).toHaveBeenCalledWith('123456');
  });
  it('fills all slots from a pasted code', () => {
    const onComplete = vi.fn();
    render(<OTPInput6Slot onComplete={onComplete} />);
    const inputs = screen.getAllByRole('textbox');
    fireEvent.paste(inputs[0], { clipboardData: { getData: () => '654321' } });
    expect(onComplete).toHaveBeenCalledWith('654321');
  });
  it('shows error styling when error prop set', () => {
    render(<OTPInput6Slot onComplete={() => {}} error />);
    expect(screen.getByRole('group')).toHaveClass('animate-shake');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test -- OTPInput6Slot`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement (port from mockup `OTPInput6Slot`, strip mock lockout — lockout comes from BE)**

Port the 6-slot logic from `sp5-profile-settings.jsx` `OTPInput6Slot`: auto-advance, backspace, paste. Remove the internal `MOCK_VALID_OTP`/lockout simulation. Expose props: `onComplete(code: string)`, `error?: boolean`, `disabled?: boolean`. Use `bg-card`, `border-border-strong`, `focus:ring-entity-kb/15` static classes. The group wrapper has `role="group"` + `animate-shake` when `error`.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm test -- OTPInput6Slot`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/settings/two-factor/OTPInput6Slot.tsx apps/web/src/components/features/settings/__tests__/OTPInput6Slot.test.tsx
git commit -m "feat(settings): OTP 6-slot input (#1608)"
```

---

### Task B2: BackupCodesView

**Files:**
- Create: `apps/web/src/components/features/settings/two-factor/BackupCodesView.tsx`
- Test: `__tests__/BackupCodesView.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { BackupCodesView } from '../two-factor/BackupCodesView';

const CODES = ['AAAA-1111','BBBB-2222'];
describe('BackupCodesView', () => {
  it('renders all codes', () => {
    render(<BackupCodesView codes={CODES} onAck={() => {}} acked={false} />);
    CODES.forEach(c => expect(screen.getByText(c)).toBeInTheDocument());
  });
  it('calls onAck when checkbox toggled', () => {
    const onAck = vi.fn();
    render(<BackupCodesView codes={CODES} onAck={onAck} acked={false} />);
    fireEvent.click(screen.getByRole('checkbox'));
    expect(onAck).toHaveBeenCalledWith(true);
  });
});
```

- [ ] **Step 2: Run** `pnpm test -- BackupCodesView` → FAIL.

- [ ] **Step 3: Implement** — port `BackupCodesGrid` markup from mockup. Props: `codes: string[]`, `acked: boolean`, `onAck(v: boolean)`. Copy-all (`navigator.clipboard.writeText(codes.join('\n'))`) + download `.txt` blob (reuse the existing pattern from `SecuritySettingsPage:295-303`). Checkbox required.

- [ ] **Step 4: Run** `pnpm test -- BackupCodesView` → PASS.

- [ ] **Step 5: Commit**

```bash
git commit -am "feat(settings): backup codes view (#1608)"
```

---

### Task B3: ActiveSessionsCard (wired)

**Files:**
- Create: `apps/web/src/components/features/settings/two-factor/ActiveSessionsCard.tsx`
- Test: `__tests__/ActiveSessionsCard.test.tsx`

- [ ] **Step 1: Write the failing test (mock api.auth)**

```tsx
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ActiveSessionsCard } from '../two-factor/ActiveSessionsCard';
import { api } from '@/lib/api';

vi.mock('@/lib/api', () => ({ api: { auth: {
  getUserSessions: vi.fn().mockResolvedValue([
    { id: 's1', userAgent: 'Chrome', ipAddress: '1.2.3.4', lastSeenAt: new Date().toISOString(), revokedAt: null },
  ]),
  revokeSession: vi.fn().mockResolvedValue({ ok: true, message: '' }),
  revokeAllSessions: vi.fn().mockResolvedValue({ ok: true, revokedCount: 1 }),
  getSessionStatus: vi.fn().mockResolvedValue({ sessionId: 's1' }),
}}}));

const wrap = (ui: React.ReactNode) => render(<QueryClientProvider client={new QueryClient()}>{ui}</QueryClientProvider>);

describe('ActiveSessionsCard', () => {
  it('lists sessions from getUserSessions', async () => {
    wrap(<ActiveSessionsCard />);
    await waitFor(() => expect(screen.getByText(/Chrome/)).toBeInTheDocument());
  });
  it('revokes a session', async () => {
    wrap(<ActiveSessionsCard />);
    await waitFor(() => screen.getByText(/Chrome/));
    fireEvent.click(screen.getByRole('button', { name: /revoke/i }));
    await waitFor(() => expect(api.auth.revokeSession).toHaveBeenCalledWith('s1'));
  });
});
```

- [ ] **Step 2: Run** `pnpm test -- ActiveSessionsCard` → FAIL.

- [ ] **Step 3: Implement** — `useQuery(['user-sessions'], api.auth.getUserSessions)`. Current-session heuristic: `useQuery(['session-status'], api.auth.getSessionStatus)` → compare `session.id === status.sessionId` to label "CURRENT" and disable its Revoke. `useMutation` for `revokeSession` + `revokeAllSessions`, `invalidateQueries(['user-sessions'])` onSuccess. Parse `userAgent` for a device label (simple `.split('/')[0]` is fine). Port card markup from mockup `ActiveSessionsCard`.

- [ ] **Step 4: Run** `pnpm test -- ActiveSessionsCard` → PASS.

- [ ] **Step 5: Commit**

```bash
git commit -am "feat(settings): active sessions card wired (#1608)"
```

---

### Task B4: TwoFactorSetupModal + TwoFactorDisableDialog + TwoFactorStatusCard (wired)

**Files:**
- Create: `two-factor/TwoFactorSetupModal.tsx`, `two-factor/TwoFactorDisableDialog.tsx`, `two-factor/TwoFactorStatusCard.tsx`
- Test: `__tests__/TwoFactorStatusCard.test.tsx`, `__tests__/TwoFactorSetupModal.test.tsx`

- [ ] **Step 1: Write failing tests**

```tsx
// TwoFactorStatusCard.test.tsx — status badge off/on + data-testid
it('shows enabled badge + data-testid when on', () => {
  render(<TwoFactorStatusCard status={{ isEnabled: true, enabledAt: '2026-01-01' }} onSetup={()=>{}} onDisable={()=>{}} />);
  expect(screen.getByTestId('2fa-status')).toHaveTextContent(/enabled/i);
});
it('shows setup CTA + data-testid when off', () => {
  render(<TwoFactorStatusCard status={{ isEnabled: false, enabledAt: null }} onSetup={()=>{}} onDisable={()=>{}} />);
  expect(screen.getByTestId('enable-2fa')).toBeInTheDocument();
});
```

```tsx
// TwoFactorSetupModal.test.tsx — wizard wired to setup2FA/enable2FA
vi.mock('@/lib/api', () => ({ api: { auth: {
  enable2FA: vi.fn().mockResolvedValue({ success: true, backupCodes: ['X-1','Y-2'] }),
}}}));
it('renders QR (data-testid) from setupData at step 1', () => {
  render(<TwoFactorSetupModal open setupData={{ secret:'S', qrCodeUrl:'data:img', backupCodes:[] }} onClose={()=>{}} onEnabled={()=>{}} />);
  expect(screen.getByTestId('2fa-qr-code')).toBeInTheDocument();
});
```

- [ ] **Step 2: Run** `pnpm test -- TwoFactorStatusCard TwoFactorSetupModal` → FAIL.

- [ ] **Step 3: Implement TwoFactorStatusCard**

Port `TwoFactorStatusCard` markup from mockup (badge off/on). Wire: status from prop (`{ isEnabled, enabledAt }`). Show `enabledAt` (NOT lastVerified — absent from DTO). `data-testid="2fa-status"`; CTA button `data-testid="enable-2fa"` (off) or `data-testid="disable-2fa"` (on). "Regenerate codes" → static "coming soon" disabled button.

- [ ] **Step 4: Implement TwoFactorSetupModal (desktop wizard, wired)**

Use `Dialog` from `@/components/ui/overlays/dialog`. 3-step: (1) QR `<Image src={setupData.qrCodeUrl}>` `data-testid="2fa-qr-code"` + secret, (2) `OTPInput6Slot onComplete={code => enableMutation.mutate(code)}`, (3) `BackupCodesView codes={result.backupCodes}`. `useMutation(api.auth.enable2FA)`, onSuccess → step 3 + `invalidateQueries(['2fa-status'])`. Error from BE → `error` prop on OTP + inline banner. Props: `open`, `setupData`, `onClose`, `onEnabled`.

- [ ] **Step 5: Implement TwoFactorDisableDialog (NEW — password+code)**

`Dialog` with two inputs (password + TOTP code) → `useMutation(() => api.auth.disable2FA(password, code))`. onSuccess → `invalidateQueries(['2fa-status'])` + close. This replaces the old `disable2FA('','')` silent call. `data-testid="disable-2fa-confirm"`.

- [ ] **Step 6: Run** `pnpm test -- TwoFactorStatusCard TwoFactorSetupModal` → PASS.

- [ ] **Step 7: Commit**

```bash
git commit -am "feat(settings): 2FA status card + setup wizard + disable dialog wired (#1608)"
```

---

### Task B5: SecuritySection (compose) + delete SecuritySettingsPage

**Files:**
- Create: `apps/web/src/components/features/settings/sections/SecuritySection.tsx` (replace Phase-A stub)
- Delete: `apps/web/src/app/(authenticated)/settings/security/page.tsx`
- Test: `__tests__/SecuritySection.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// SecuritySection.test.tsx — composes status card + sessions, opens setup on CTA
vi.mock('@/lib/api', () => ({ api: { auth: {
  getTwoFactorStatus: vi.fn().mockResolvedValue({ isEnabled: false, enabledAt: null }),
  setup2FA: vi.fn().mockResolvedValue({ secret:'S', qrCodeUrl:'data:i', backupCodes:[] }),
  getUserSessions: vi.fn().mockResolvedValue([]),
  getSessionStatus: vi.fn().mockResolvedValue({ sessionId: 'x' }),
}}}));
it('opens setup wizard when Enable 2FA clicked', async () => {
  wrap(<SecuritySection />);
  await waitFor(() => screen.getByTestId('enable-2fa'));
  fireEvent.click(screen.getByTestId('enable-2fa'));
  await waitFor(() => expect(api.auth.setup2FA).toHaveBeenCalled());
});
```

- [ ] **Step 2: Run** `pnpm test -- SecuritySection` → FAIL.

- [ ] **Step 3: Implement SecuritySection**

Compose: `TwoFactorStatusCard` + `ActiveSessionsCard` + (mobile) `TwoFactorBottomSheet` / (desktop) `TwoFactorSetupModal` + `TwoFactorDisableDialog`. State: `setupData` from `useMutation(api.auth.setup2FA)` onSuccess opens the wizard. `useQuery(['2fa-status'])`. Migrate the 6 invariants from `SecuritySettingsPage`. Use `<Drawer>`/`useDrawerBreakpoint` to switch modal↔bottom-sheet (TwoFactorBottomSheet stubbed now, built in Phase D — for now always render the Modal; Phase D swaps in the breakpoint switch).

- [ ] **Step 4: Run** `pnpm test -- SecuritySection` → PASS.

- [ ] **Step 5: Delete the standalone page + verify no broken import**

```bash
git rm apps/web/src/app/\(authenticated\)/settings/security/page.tsx
cd apps/web && pnpm typecheck
```
Expected: no type errors (nothing imports the deleted default export — verify with `grep -rn "settings/security/page" apps/web/src`).

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat(settings): SecuritySection wired + remove standalone SecuritySettingsPage (#1608)"
```

---

## Phase C — Profile / Preferences / API keys / ai-consent (commit 3)

### Task C1: ProfileSection (wired)

**Files:** Create `sections/ProfileSection.tsx` (replace stub); Test `__tests__/ProfileSection.test.tsx`

- [ ] **Step 1: Failing test** — `getProfile` mock renders displayName/email; save calls `updateProfile` then re-fetches `getProfile`.

```tsx
vi.mock('@/lib/api', () => ({ api: { auth: {
  getProfile: vi.fn().mockResolvedValue({ displayName:'Marco', email:'m@x.it', language:'it', avatarUrl:null }),
  updateProfile: vi.fn().mockResolvedValue({ ok:true, message:'' }),
}}}));
it('renders profile + saves via updateProfile then refetches', async () => {
  wrap(<ProfileSection />);
  await waitFor(() => screen.getByDisplayValue('Marco'));
  fireEvent.click(screen.getByTestId('save-profile-button'));
  await waitFor(() => expect(api.auth.updateProfile).toHaveBeenCalled());
});
```

- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement** — `useQuery(['profile'], getProfile)`, form (displayName/email), `useMutation(updateProfile)` onSuccess `invalidateQueries(['profile'])` (re-fetch — updateProfile returns only `{ok}`). Reuse `AvatarUpload`. `data-testid="save-profile-button"`. Port markup from mockup `ProfileSection`.
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `git commit -am "feat(settings): profile section wired (#1608)"`

### Task C2: PreferencesSection (wired, NO density)

**Files:** Create `sections/PreferencesSection.tsx`; Test `__tests__/PreferencesSection.test.tsx`

- [ ] **Step 1: Failing test** — `getPreferences` mock (theme/language), toggle theme calls `updatePreferences`.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement** — `useQuery(['preferences'], getPreferences)`, theme select (`light|dark|system`) + language; `useMutation(updatePreferences)` (returns `UserProfile` — invalidate `['preferences']` + `['profile']`). **No density field** (absent from DTO). `data-testid="save-preferences-button"`.
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `git commit -am "feat(settings): preferences section wired (#1608)"`

### Task C3: ApiKeysSection (wired, copy-once)

**Files:** Create `sections/ApiKeysSection.tsx`; Test `__tests__/ApiKeysSection.test.tsx`

- [ ] **Step 1: Failing test** — `listApiKeys` mock renders keyPrefix; create shows `plaintextKey` once in a copy modal; revoke calls `revokeApiKey`.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement** — `useQuery(['api-keys'], () => api.auth.listApiKeys())` renders `items` (keyName, keyPrefix, lastUsedAt). Create form → `useMutation(createApiKey)` onSuccess opens a **copy-once dialog** showing `plaintextKey` (never stored, never re-shown) + `invalidateQueries(['api-keys'])`. Revoke → `useMutation(revokeApiKey)`. `data-testid="create-api-key-button"`, `data-testid="api-key-plaintext"`.
- [ ] **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** `git commit -am "feat(settings): api keys section wired + copy-once (#1608)"`

### Task C4: AiConsentSection + redirect + delete standalone

**Files:** Create `sections/AiConsentSection.tsx`; Modify `next.config.js`; Delete `settings/ai-consent/page.tsx`; Test `__tests__/AiConsentSection.test.tsx`

- [ ] **Step 1: Read the existing ai-consent page** to extract its wiring:

Run: `cat apps/web/src/app/\(authenticated\)/settings/ai-consent/page.tsx`
Note: the API calls (`/api/v1/users/me/ai-consent`), the save handler, `data-testid="save-ai-consent"`.

- [ ] **Step 2: Failing test** — renders consent toggles, save calls the same API.
- [ ] **Step 3: Run** → FAIL.
- [ ] **Step 4: Implement** — migrate the ai-consent page logic verbatim into `AiConsentSection` (same hooks/handlers, drop the page `container` wrapper + `<h1>` since the tab provides the heading). Preserve `data-testid="save-ai-consent"`.
- [ ] **Step 5: Add redirect** in `next.config.js` (before the `/settings/:path*` catch-all):

```js
{ source: '/settings/ai-consent', destination: '/profile?tab=settings&section=ai-consent', permanent: true },
```

- [ ] **Step 6: Delete standalone + verify**

```bash
git rm apps/web/src/app/\(authenticated\)/settings/ai-consent/page.tsx
grep -rn "settings/ai-consent" apps/web/src   # update any in-app <Link> to the new query URL
cd apps/web && pnpm typecheck
```

- [ ] **Step 7: Run** `pnpm test -- AiConsentSection` → PASS.
- [ ] **Step 8: Commit** `git commit -am "feat(settings): ai-consent section + redirect, remove standalone (#1608)"`

---

## Phase D — Mobile bottom-sheet (commit 4)

### Task D1: TwoFactorBottomSheet + breakpoint switch

**Files:** Create `two-factor/TwoFactorBottomSheet.tsx`; Modify `sections/SecuritySection.tsx`; Test `__tests__/TwoFactorBottomSheet.test.tsx`

- [ ] **Step 1: Failing test** — renders the same 3-step wizard content inside the Drawer when `open`; step transitions work; uses `OTPInput6Slot`.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement** — wrap the same wizard body (QR / OTP / backup) used by `TwoFactorSetupModal` in `<Drawer side="bottom">` (from `@/components/ui/drawer`). Extract the shared wizard body into a `TwoFactorWizardBody` component to avoid duplication (DRY) — both Modal and BottomSheet render it. Same wiring (`enable2FA`).
- [ ] **Step 4: Switch in SecuritySection** — use `useDrawerBreakpoint()` (or the project's breakpoint hook) to render `TwoFactorBottomSheet` on mobile, `TwoFactorSetupModal` on desktop.
- [ ] **Step 5: Run** `pnpm test -- TwoFactorBottomSheet` → PASS.
- [ ] **Step 6: Commit** `git commit -am "feat(settings): mobile 2FA bottom-sheet via Drawer primitive (#1608)"`

---

## Phase E — Test rewrite + data-testid sweep (commit 5)

### Task E1: Rewrite e2e/settings/profile-settings.spec.ts

**Files:** Modify `apps/web/e2e/settings/profile-settings.spec.ts`

- [ ] **Step 1: Audit current assertions**

Run: `grep -n "goto('/settings\|getByRole('tab'\|getByTestId" apps/web/e2e/settings/profile-settings.spec.ts`
Note every `/settings` goto + tab-name + testid assertion (25 tests).

- [ ] **Step 2: Rewrite navigation + DOM expectations**

Change `goto('/settings')` → `goto('/profile?tab=settings&section=profile')` (or follow redirect). Tab labels: `Overview/Achievements/Activity/Settings`. Sub-sections are sidebar items (not `role=tab`) — assert via section heading / `data-testid` (`save-profile-button`, `save-preferences-button`, etc.). Keep one test per section reachable via `?section=`.

- [ ] **Step 3: Run** `cd apps/web && pnpm test:e2e -- settings/profile-settings` (or document if e2e needs the dev server / CI).
Expected: PASS (adjust until green).

- [ ] **Step 4: Commit** `git commit -am "test(settings): rewrite profile-settings e2e for tab consolidation (#1608)"`

### Task E2: Update auth e2e suites (redirect follow + data-testid)

**Files:** Modify `apps/web/e2e/auth/{device-management,login-notifications,remember-me}.spec.ts`, `auth.spec.ts`, `gap-analysis-critical.spec.ts`

- [ ] **Step 1: Confirm data-testid coverage**

Run: `grep -n "data-testid=\"2fa-status\"\|enable-2fa\|2fa-qr-code" apps/web/src/components/features/settings`
Expected: present (from B4). If gap-analysis expects more testids, add them to the components.

- [ ] **Step 2: Update navigations**

The 37 `goto('/settings/security')` now redirect to the tab. Verify each spec still finds its target via the new DOM (`data-testid="2fa-status"`, `enable-2fa`, `2fa-qr-code`). Update locators where they relied on the old standalone page DOM.

- [ ] **Step 3: Run** the affected specs → PASS.
- [ ] **Step 4: Commit** `git commit -am "test(auth): update 2FA e2e for settings tab + data-testid (#1608)"`

### Task E3: New e2e — enrollment happy-path (real pipeline)

**Files:** Create `apps/web/e2e/settings/settings-tab-2fa.spec.ts`

- [ ] **Step 1: Write the e2e**

Deep-link `/settings/security` → redirect → Security section visible (`2fa-status`). Click `enable-2fa` → wizard → QR (`2fa-qr-code`) → enter a TOTP minted against the setup secret (mirror the staging pattern from `S3AcceptanceScenariosTests` if a helper exists, else use the test 2FA seed) → backup codes shown → status flips to enabled. **Exercises `api.auth.enable2FA` real** ([[feedback_acceptance_tests_must_exercise_real_pipeline]]). Add a no-redirect-loop assertion: after redirect, URL is `/profile?tab=settings&section=security` and stable.

- [ ] **Step 2: Run** `pnpm test:e2e -- settings/settings-tab-2fa` → PASS (or document CI-only).
- [ ] **Step 3: Commit** `git commit -am "test(settings): e2e 2FA enrollment via settings tab (real pipeline) (#1608)"`

### Task E4: Final gate — typecheck + lint + full unit + verify settings hub

**Files:** possibly Delete `settings/page.tsx` (per A0 finding)

- [ ] **Step 1: If A0 found settings hub page is now orphaned** (only reachable via redirect to a non-existent thing), delete it and confirm the catch-all redirect covers it. Otherwise leave it.
- [ ] **Step 2: Run full quality gate**

Run: `cd apps/web && pnpm typecheck && pnpm lint && pnpm test`
Expected: all green. Fix any `bg-entity-*` purge / `no-inline-hsl-v2` / `no-hardcoded-color-utility` violations the linter surfaces.

- [ ] **Step 3: Commit** `git commit -am "chore(settings): final typecheck/lint/test gate (#1608)"`

---

## Self-Review notes (author)

- **Spec coverage:** routing+Suspense (A4), 7 sections (A1/A3 + B5 + C1-C4 + placeholder), re-skin+wiring (B1-B5), mobile (D1), test rewrite (E1-E3), data-testid (B4+E2), shape adaptations — disable modal (B4), no density (C2), copy-once (C3), re-fetch profile (C1), enabledAt not lastVerified (B4). ✓
- **Placeholder scan:** markup-from-mockup references are intentional (ground-truth file committed), each paired with explicit adaptation (token/wiring/testid). Logic/test code is inline-complete. ✓
- **Type consistency:** `SettingsSectionId` (A1) used in A2/A3/A4; `getTwoFactorStatus`/`enable2FA`/`getUserSessions` signatures match authClient (verified in review). ✓

## Execution Handoff

Stima: ~4-6 giorni, 5 phase / ~18 task, 1 PR.
Branch: `feature/issue-1608-settings-tab`.
Post-implementation: PR → main-dev, code-review, merge → chiude #1608 → re-trigger enrollment staging.

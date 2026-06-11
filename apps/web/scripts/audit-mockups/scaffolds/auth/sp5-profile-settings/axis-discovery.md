# sp5-profile-settings — Axis Discovery

**Source HTML**: `admin-mockups/design_files/sp5-profile-settings.html`
**JSX twin**: `admin-mockups/design_files/sp5-profile-settings.jsx`
**Phase B classification**: `design_intent: current` · no `pair_disagreement`
**Mockup canonical**: HTML (per MOCKUPS_INDEX pairing rule)

## Mockup stage layout

The mockup HTML (sp5-profile-settings.html:1-7) advertises:
> SP5 — Profile · Settings tab + 2FA enrollment wizard
> Route target: /profile?tab=settings(&section=*)
> 8 frames: D1-D6 desktop @ 1280px + M1/M2 mobile @ 375px
> Entity color security = --c-kb (teal). Stand-alone preview.

The mockup explicitly maps frames to query-param URL states (see Frame
labels with `route="?tab=settings..."` attribute, jsx:917-969).

## Axis (canonical)

| Axis | Type | Values | Source | Notes |
|------|------|--------|--------|-------|
| `tab` | enum | `overview` \| `achievements` \| `activity` \| `settings` | tabs array (jsx:152-155) | Owned by ProfilePageContent useSearchParams |
| `section` | enum | SettingsSectionId | sections array (jsx:83-88) | Sub-route under tab=settings |
| `wizardStep` | enum | `null` \| `setup` \| `verify` \| `codes` | TwoFactorSetup wizard state | Modal flag (mockup `withModal` prop, jsx:927-941) |
| `twoFactorEnabled` | boolean | `false` (D2) \| `true` (D6) | 2FA toggle state | Drives SecuritySection rendering |
| `view` | enum | `desktop` \| `mobile` | Mockup D-frames vs M-frames | Mobile DEFERRED Phase 4 |

## Frame matrix (Desktop only Phase C-1)

| Frame | Mockup `Frame label` (line) | Mockup `route` attr | Wizard step | Axis values |
|-------|------------------------------|---------------------|-------------|-------------|
| D1 | `Profile landing — tab Settings (Profile section default)` (jsx:917) | `?tab=settings` | null | section='profile' |
| D2 | `Section Security — 2FA OFF` (jsx:922) | `?tab=settings&section=security` | null | section='security', twoFactorEnabled=false |
| D3 | `Wizard 2FA — Step 1/3 (QR)` (jsx:927) | `?tab=settings&section=security · modal:setup` | 'setup' | wizardStep='setup', withModal |
| D4 | `Wizard 2FA — Step 2/3 (Verify)` (jsx:933) | `?tab=settings&section=security · modal:verify` | 'verify' | wizardStep='verify', withModal |
| D5 | `Wizard 2FA — Step 3/3 (Backup codes)` (jsx:939) | `?tab=settings&section=security · modal:codes` | 'codes' | wizardStep='codes', withModal |
| D6 | `Section Security — 2FA ON` (jsx:945) | `?tab=settings&section=security` | null | section='security', twoFactorEnabled=true |

Mobile frames M1+M2 (jsx:951+969) DEFERRED Phase 4.

## Component mapping (route ↔ canonical)

| Route | Real component | File |
|-------|----------------|------|
| `/profile` (default) | `ProfilePageContent` (tab='overview') | `apps/web/src/app/(authenticated)/profile/_components/ProfilePageContent.tsx` |
| `/profile?tab=settings` | `ProfilePageContent` → `SettingsTab` (section='profile') | same + `apps/web/src/components/features/settings/SettingsTab.tsx` |
| `/profile?tab=settings&section=security` | `ProfilePageContent` → `SettingsTab` → `SecuritySection` | + `apps/web/src/components/features/settings/sections/SecuritySection.tsx` |
| `/profile?tab=settings&section=security` + wizard | same + `TwoFactorSetup` modal | + `apps/web/src/components/auth/TwoFactorSetup.tsx` |
| `/profile?tab=achievements` | `ProfilePageContent` → `AchievementsGrid` | + `apps/web/src/components/profile/AchievementsGrid.tsx` |
| `/profile?tab=activity` | `ProfilePageContent` → `ActivityFeed` | + `apps/web/src/components/profile/ActivityFeed.tsx` |

## Canonical component pick

**Picked**: `apps/web/src/app/(authenticated)/profile/_components/ProfilePageContent.tsx`

**Why**:
1. Production component, full orchestrator of tab + section state via URL.
2. Mockup explicitly targets `/profile?tab=settings` (HTML header comment).
3. Renders TabBar + active tab content (Overview/Achievements/Activity/Settings).
4. For `settings` tab, mounts `SettingsTab` which mounts `SecuritySection`
   which opens `TwoFactorSetup` modal for D3/D4/D5 wizard steps.

**Caveat**: ProfilePageContent uses `usePathname`, `useRouter`,
`useSearchParams`, `useQuery` heavily. Storybook 10.4 + Next.js App Router
mock chain may need decorator wiring. Phase 4 prelude IntlProvider
hardening covers context; router context may need explicit
`<RouterContextProvider>` decorator in `.storybook/preview.tsx`.

## Mockup ↔ codebase divergences

| # | Divergence | Resolution |
|---|------------|------------|
| 1 | Mockup sections array (jsx:83-88) uses `id: 'account'` (mapped to canonical `security`) and `id: 'notifications'` (placeholder canonical). | MOCKUP_TO_CANONICAL handled in settings cluster axis-discovery. |
| 2 | Mockup wizard 3-step (Setup → Verify → Codes); codebase TwoFactorSetup component must match steps 1:1. | Verify TwoFactorSetup.tsx step count matches. |
| 3 | Mockup `entity color = --c-kb (teal)` for security. Codebase uses `entity-kb` Tailwind utility (settings-sections.ts:50). | Parity. |
| 4 | Mockup `withModal` prop adds frame chrome around modal. Codebase opens Drawer/Modal in normal page flow. | UI presentation difference; semantic intent identical. |
| 5 | Mockup shows session list with `current: true` flag. Codebase active sessions list endpoint must include the flag. | Verify in `GET /api/v1/auth/sessions`. |
| 6 | Mockup shows trusted devices list (jsx:66-69). Codebase 2FA status includes `trustedDevices`. | Parity. |

## JSX evidence (line refs)

- Mockup `tabs` array (4 entries): `sp5-profile-settings.jsx:152-155`
- Mockup `sections` array (6 entries): `sp5-profile-settings.jsx:83-88`
- Active sessions list (3 entries with current flag): `sp5-profile-settings.jsx:66-69`
- Wizard step 1 (QR code dialog): `sp5-profile-settings.jsx:598-…`
- Wizard step 2 (PIN verify dialog): `sp5-profile-settings.jsx:505-…`
- Wizard step 3 (recovery codes dialog): `sp5-profile-settings.jsx:748-…`
- Desktop Frame components D1-D6: `sp5-profile-settings.jsx:917-948`
- Mobile Frame components M1+M2: `sp5-profile-settings.jsx:951-984`
- Canonical ProfilePageContent tab routing: `ProfilePageContent.tsx:45-83`
- Canonical SettingsTab section routing: `SettingsTab.tsx:30-55`

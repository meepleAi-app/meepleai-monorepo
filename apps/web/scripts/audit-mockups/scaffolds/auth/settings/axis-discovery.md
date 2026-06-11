# settings — Axis Discovery

**Source HTML**: `admin-mockups/design_files/settings.html`
**JSX twin**: `admin-mockups/design_files/settings.jsx`
**Phase B classification**: `design_intent: current` · no `pair_disagreement`
**Mockup canonical**: HTML (per MOCKUPS_INDEX pairing rule)

## Mockup stage layout

The mockup HTML wraps a Desktop frame (`.desktop-frame`, 640px height,
settings.html:22-30) AND a `.phones-grid` (4 mobile PhoneShell frames).

**Desktop view** (settings.jsx:824-1030): Renders sidebar with 6 menu items
(profile, account, preferences, notifications, apikeys, services) +
content area showing the active section panel.

**Mobile view** (settings.jsx:1066-1085): Renders 4 PhoneShell frames
labeled `01 · Hub`, `02 · Notifiche`, `03 · API Keys`, `04 · Servizi` —
each showing a different mobile drilldown screen.

## Axis (canonical)

| Axis | Type | Values | Source | Notes |
|------|------|--------|--------|-------|
| `section` | enum | `profile` \| `account` \| `preferences` \| `notifications` \| `apikeys` \| `services` | `MENU` array (settings.jsx:824-829) | Mockup section ids; mapped to canonical via MOCKUP_TO_CANONICAL |
| `view` | enum | `desktop` \| `mobile` | Mockup desktop-frame vs phones-grid | Mobile DEFERRED Phase 4 |
| `state` | enum | `default` \| `loading` \| `error` | useQuery state (SettingsTab.tsx:22) | Drives MSW handler scenario |

## Mockup → canonical SettingsSectionId map

| Mockup id | Canonical SettingsSectionId | Component file |
|-----------|------------------------------|----------------|
| `profile` | `profile` | `sections/ProfileSection.tsx` |
| `account` | `security` | `sections/SecuritySection.tsx` |
| `preferences` | `preferences` | `sections/PreferencesSection.tsx` |
| `notifications` | `notifications` | placeholder (SectionPlaceholder) |
| `apikeys` | `api-keys` | `sections/ApiKeysSection.tsx` |
| `services` | `services` | placeholder (SectionPlaceholder) |
| n/a | `ai-consent` | `sections/AiConsentSection.tsx` (codebase-only, not in mockup) |

CRITICAL: The mockup section id `account` does NOT match the canonical
SettingsSectionId — the codebase uses `security` semantically (settings-
sections.ts:84). Mockup mapping documented in story decorator.

## Frame matrix (Desktop only Phase C-1)

| Frame | Mockup section | Canonical SettingsSectionId | Axis values |
|-------|----------------|-----------------------------|-------------|
| 01 | profile | `profile` | `section='profile', view='desktop', state='default'` |
| 02 | account | `security` | `section='security', view='desktop', state='default'` |
| 03 | preferences | `preferences` | `section='preferences', view='desktop', state='default'` |
| 04 | apikeys | `api-keys` | `section='api-keys', view='desktop', state='default'` |
| 05 | (codebase-only) | `ai-consent` | New frame for GDPR section |
| 06 | notifications | `notifications` (placeholder) | Documents placeholder state |
| 07 | services | `services` (placeholder) | Documents placeholder state |

Mobile PhoneShell frames M1-M4 (Mockup labels: 01 Hub, 02 Notifiche,
03 API Keys, 04 Servizi) DEFERRED Phase 4 (viewport sweep, Mobile opt-in).

## Component mapping (route ↔ canonical)

| Route | Real component | File |
|-------|----------------|------|
| `/settings` (placeholder) | redirect to `/profile?tab=settings` | (likely) |
| `/settings/profile` | (none — uses /profile?tab=settings&section=profile) | — |
| `/settings/security` | same | — |
| `/settings/preferences` | same | — |
| `/settings/ai-consent` | same | — |
| `/settings/api-keys` | same | — |
| `/settings/notifications` | same | — |
| `/settings/services` | same | — |

CRITICAL: There is NO standalone `/settings` route in the codebase. All
settings sections are reached via `/profile?tab=settings&section=…` per
DS-17 Phase C-1 spec § 6 + ProfilePageContent.tsx wiring. The mockup
shows a standalone settings hub that does NOT exist as a route — it is
conceptually merged into the Profile tab.

## Canonical component pick

**Picked**: `apps/web/src/components/features/settings/SettingsTab.tsx`

**Why**:
1. Production component composing `SettingsSubNav` + 5 section components
   + 2 placeholders.
2. Controlled component: `activeSection` + `onChangeSection` props. Story
   wraps it in a thin Client decorator to drive section state.
3. The actual route `/profile?tab=settings` mounts SettingsTab via
   ProfilePageContent (apps/web/src/app/(authenticated)/profile/
   _components/ProfilePageContent.tsx) — too heavy for Storybook frame
   matrix (drags in QueryClientProvider, useAuth, useRouter).

## Mockup ↔ codebase divergences

| # | Divergence | Resolution |
|---|------------|------------|
| 1 | Mockup section id `account` ≠ canonical `security`. | MOCKUP_TO_CANONICAL map in story decorator. |
| 2 | Mockup has NO `ai-consent` section. Codebase has GDPR section (Issue #2014). | Codebase superset; Frame05 documents canonical-only frame. |
| 3 | Mockup `services` shows BGG connection. Codebase ADR #1903 blocks BGG user-side. | `services` is SectionPlaceholder in codebase (placeholder: true). |
| 4 | Mockup standalone /settings hub route. Codebase merges into /profile?tab=settings. | Story renders SettingsTab without route wrapper; documents divergence. |
| 5 | Mockup mobile "01 · Hub" PhoneShell shows sidebar collapsed to list. Codebase uses `SettingsSubNav` responsive — auto-collapses below md breakpoint (SettingsTab.tsx:30). | Mobile frames deferred Phase 4. |

## JSX evidence (line refs)

- Desktop sidebar `MENU` array: `settings.jsx:824-829`
- Profile section render: `settings.jsx:270-290`
- Account section (security): `settings.jsx:300-400`
- Preferences (theme/lingua/timezone): `settings.jsx:440-480`
- API Keys with create new: `settings.jsx:530-720`
- Mobile PhoneShell config 4 entries: `settings.jsx:1066-1085`
- Codebase SETTINGS_SECTIONS registry: `settings-sections.ts:30-…`
- Codebase SettingsTab controlled flow: `SettingsTab.tsx:22-50`

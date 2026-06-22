# Profile Settings Tab + 2FA Consolidation — Design

**Date:** 2026-05-27
**Issue:** [#1608](https://github.com/meepleAi-app/meepleai-monorepo/issues/1608) (P0)
**Status:** design approved (brainstorming + spec-panel + 4-agent review)
**Mockup ground-truth:** `admin-mockups/design_files/sp5-profile-settings.{html,jsx}` (PR #1613, merged `8931abd17`)
**Brief:** `admin-mockups/briefs/SP5-profile-settings-tab.md`

## Goal

Implementare il 4° tab "Settings" nel `/profile` page (Next.js 16 App Router) con sub-section navigation, così da rendere **raggiungibile** il flow di enrollment 2FA che oggi è orfano. Questo **sblocca SP5 S3 strict 2FA cutover** (PR #1597 deployed in staging 2026-05-27): i 2 superadmin staging non possono completare l'enrollment perché `/settings/security` redireziona a un tab che non esiste.

## Contesto critico (da review — assunzioni corrette)

La review a 4 agenti ha rivelato che diverse assunzioni iniziali erano errate. Questo design parte dallo stato **reale** del codebase:

1. **I redirect ESISTONO GIÀ.** `next.config.js:234-238` (consolidation #5039) già mappa `/settings`, `/settings/security`, `/settings/notifications` → `/profile?tab=settings&section=*`, più un catch-all `/settings/:path*` → `/profile?tab=settings`. Non c'è "R1 redirect-fix" da fare. Il vero unblock è **implementare il tab** (R2). Il bug #1608 è che il `ProfilePage` usa `useState('overview')` e ignora completamente i query param.

2. **`useSearchParams` richiede `<Suspense>`** in Next.js 16 (CSR bailout durante prerender). Pattern stabilito: `sessions/page.tsx:7-14`, `login/page.tsx`, `discover/page.tsx` estraggono il content in un child component wrappato in `<Suspense>`.

3. **`<Drawer>` primitive esiste** (`components/ui/drawer/drawer.tsx`) e wrappa `vaul` (già in `package.json` v1.1.2) con `useDrawerBreakpoint` auto mobile/desktop. Riuso, NON aggiungo vaul raw. `CampaignSetupDrawer` (gamebook) è il consumer di riferimento.

4. **`SettingsList`/`SettingsRow` esistono** (`ui/settings-list/`, `ui/settings-row/`) con prop `entity: EntityType` che applica il colore icona. Riuso per le list-row delle section.

5. **Token: `bg-entity-kb` NON `bg-kb`.** Le entity utility sono `bg-entity-<name>` (registrate in `globals.css @theme inline`). I dynamic class strings (`bg-${entity}/10`) vengono purgati in produzione → **static literals only**. NON usare `getEntityToken('kb')` (ritorna il legacy `bg-entity-document`). Inline `hsl()` vietato in `components/features/**` da `meepleai/no-inline-hsl-v2`.

6. **`ai-consent` è una page GDPR funzionale** (`settings/ai-consent/page.tsx`) non presente nei mockup. Va consolidata come 7ª section (decisione utente) per evitare l'orphan + compliance risk.

## Decisioni (brainstorming + spec-panel)

| # | Decisione | Valore |
|---|-----------|--------|
| D1 | Scope | **Completo in 1 PR**: 7 section (5 reali + 2 placeholder) + mobile bottom-sheet + delete + test rewrite |
| D2 | URL canonica | `/profile?tab=settings&section=<name>`; `/settings/*` → redirect (già esistente) + nuovo redirect per ai-consent |
| D3 | Section reali | Security, Profile, Preferences, API keys, **ai-consent** |
| D4 | Section placeholder | Notifications, Connected services |
| D5 | Security UI | Re-skin completo coi pattern mockup, **wiring BE preservato** (no mock data) |
| D6 | Mobile | Completo: sub-nav list + bottom-sheet wizard via `<Drawer>` primitive |
| D7 | SecuritySettingsPage | **Eliminato** (route → redirect); wiring migrato nei nuovi component |
| D8 | Component path | `apps/web/src/components/features/settings/` |

## Architettura — Routing

`ProfilePage` diventa URL-driven. Per il vincolo Suspense (Next 16):

```
profile/page.tsx           [Server/thin: <Suspense><ProfilePageContent/></Suspense>]
profile/_components/ProfilePageContent.tsx
                           ['use client'; useSearchParams + useRouter + usePathname]
```

- `activeTab = searchParams.get('tab') ?? 'overview'` (valori: `overview|achievements|activity|settings`)
- `activeSection = searchParams.get('section') ?? 'profile'` (default quando tab=settings)
- Cambio tab/section → helper `buildQuery` (pattern `SessionsLibraryView.tsx:220-234`): `new URLSearchParams(searchParams.toString())` → `set` → `router.replace('/profile?'+params, {scroll:false})`. Preserva gli altri param, no clobber.
- `section` invalido → fallback `profile` + `router.replace` per pulire l'URL.
- **No redirect-loop**: il redirect `next.config.js` è server-side one-shot; il profile page legge il param e renderizza (non ri-naviga verso `/settings/*`).

## Architettura — Component

```
apps/web/src/components/features/settings/
  SettingsTab.tsx                  [container: SettingsSubNav + section router via activeSection]
  SettingsSubNav.tsx               [riusa SettingsList/SettingsRow; desktop sidebar 240px / mobile list]
  sections/
    SecuritySection.tsx
    ProfileSection.tsx
    PreferencesSection.tsx
    ApiKeysSection.tsx
    AiConsentSection.tsx           [porta settings/ai-consent/page.tsx content]
    SectionPlaceholder.tsx         [Notifications, Connected services]
  two-factor/
    TwoFactorStatusCard.tsx        [getTwoFactorStatus → badge off/on; enabledAt (NO lastVerified)]
    TwoFactorSetupModal.tsx        [desktop wizard 3-step; setup2FA → OTP → enable2FA]
    TwoFactorBottomSheet.tsx       [mobile, riusa <Drawer> primitive]
    TwoFactorDisableDialog.tsx     [NEW: confirm modal password+code → disable2FA]
    OTPInput6Slot.tsx              [6-slot auto-advance/paste; onSubmit → enable2FA(code)]
    BackupCodesView.tsx            [grid + copy/download da enable2FA result]
    ActiveSessionsCard.tsx         [getUserSessions/revokeSession/revokeAllSessions]
  __tests__/                       [co-located Vitest]
```

`SecuritySettingsPage` (`settings/security/page.tsx`) eliminato dopo migrazione del wiring.

## Section detail + shape adattamenti BE

| Section | Wiring | Adattamento (da review shape-check) |
|---------|--------|-------------------------------------|
| **Security** | `getTwoFactorStatus`, `setup2FA`, `enable2FA`, `disable2FA`, `getUserSessions`, `revokeSession`, `revokeAllSessions` | `disable2FA(password, code)` richiede **confirm modal** con input (NO simple confirm, NO `disable2FA('','')`); status card mostra `enabledAt` (NON `lastVerified`, assente dal DTO); `UserSessionInfo` non ha `isCurrent` → heuristic FE via `getSessionStatus()`; "Regenerate codes" → coming-soon (no BE endpoint) |
| **Profile** | `getProfile`, `updateProfile`, `uploadAvatar` | `updateProfile` ritorna `{ok,message}` → **re-fetch** `getProfile()` dopo update |
| **Preferences** | `getPreferences`, `updatePreferences` | **NO density toggle** (assente da `UserPreferences`); `updatePreferences` ritorna `UserProfile` (no narrow a `UserPreferences`); theme/language/emailNotifications OK |
| **API keys** | `listApiKeys`, `createApiKey`, `revokeApiKey` | `createApiKey` ritorna `plaintextKey` una volta → **copy-once modal**; `listApiKeys` mostra `keyPrefix` (mai la chiave) |
| **ai-consent** | porta logica esistente da `settings/ai-consent/page.tsx` | redirect `/settings/ai-consent` → `?section=ai-consent` aggiunto a `next.config.js` |
| **Notifications** | — | placeholder (BE: `emailNotifications` esiste in preferences ma scope futuro) |
| **Connected services** | — | placeholder (no OAuth-revoke BE) |

### Security — 6 invarianti preservati (no regressione)

Dal `SecuritySettingsPage` esistente: (1) setup→verify→codes flow, (2) error states inline, (3) `invalidateQueries(['2fa-status'])` post-enable/disable, (4) disable flow, (5) download codes, (6) status badge. Il re-skin cambia la UI (OTPInput6Slot, card mockup) ma preserva questi comportamenti wired.

## Mobile

`SettingsSubNav` responsive: sidebar 240px desktop → list verticale mobile. Wizard 2FA: desktop `TwoFactorSetupModal` (Dialog centrato); mobile `TwoFactorBottomSheet` via `<Drawer>` primitive (`useDrawerBreakpoint` gestisce lo switch). Snap/swipe gestiti dalla primitive (vaul-backed) — no gesture custom.

## Error handling

- 2FA setup/enable fail → banner inline (pattern `SecuritySettingsPage:143-148`)
- Lockout (BE subcode `locked_out`) → banner countdown
- Section fetch fail → per-section error card (non rompe il tab)
- `section` param invalido → fallback profile

## Testing

**Vincolo:** [[feedback_acceptance_tests_must_exercise_real_pipeline]] — l'enrollment e2e esercita `api.auth.enable2FA` reale.

| Tipo | Scope |
|------|-------|
| Vitest unit | `OTPInput6Slot` (auto-advance/paste/lockout), `SettingsSubNav` (deep-link/active), routing fallback, `SettingsTab` section router |
| Vitest unit (update) | `profile/__tests__/page.test.tsx` — "three tabs" → "four tabs" + mock `useSearchParams` |
| E2E (new) | deep-link `/settings/security` → redirect → Security section render; enrollment happy-path pipeline reale; tab/section switching aggiorna URL; no-redirect-loop |
| E2E (rewrite) | `e2e/settings/profile-settings.spec.ts` (25 test): nuovo URL/DOM shape; tab labels `Overview/Achievements/Activity/Settings`; sub-section sidebar non più `role=tab` |
| E2E (update) | `e2e/auth/{device-management,login-notifications,remember-me}.spec.ts` + `auth.spec.ts` + `gap-analysis-critical.spec.ts`: 37 `goto('/settings/security')` → seguono redirect; **`data-testid` richiesti**: `2fa-status`, `enable-2fa`, `2fa-qr-code` (+ altri da gap-analysis) emessi dai nuovi component |

## Files

**Create:** `components/features/settings/**` (vedi sopra), `profile/_components/ProfilePageContent.tsx`, e2e + unit tests.
**Modify:** `profile/page.tsx` (Suspense wrapper), `next.config.js` (+ redirect ai-consent), test suites impattate.
**Delete:** `settings/security/page.tsx`, `settings/ai-consent/page.tsx` (content migrato), eventuale `settings/page.tsx` hub (verificare consumer).

## Out of scope

- Step-up modale command-time (wire-doc già esiste, SP5 S4)
- Regenerate backup codes (no BE endpoint)
- Density preference (no BE field)
- Notifications/Connected-services funzionali (placeholder)
- WebAuthn/passkeys (#186 P1.2)

## Commit structure

PR unico, commit interni incrementali:
1. `ProfilePageContent` + Suspense + 4° tab + `SettingsTab` + `SettingsSubNav` (routing scaffold, section placeholder)
2. `SecuritySection` + two-factor/* (re-skin + wiring + disable-confirm + data-testid) + delete `SecuritySettingsPage`
3. Profile + Preferences + ApiKeys + ai-consent sections + redirect ai-consent + delete standalone pages
4. Mobile bottom-sheet (`TwoFactorBottomSheet` via Drawer)
5. Test rewrite (unit + e2e profile-settings + auth suites)

## Acceptance (chiude #1608)

- [ ] `/profile?tab=settings&section=security` renderizza la Security section interattiva
- [ ] `/settings/security` → redirect → Security section (link email enrollment validi)
- [ ] Enrollment 2FA completabile via UI (setup→verify→codes) wired al BE reale
- [ ] 7 section navigabili (5 reali + 2 placeholder), deep-link funzionante
- [ ] Mobile: sub-nav list + bottom-sheet wizard
- [ ] ai-consent raggiungibile (no GDPR orphan)
- [ ] Test suite verde (unit + e2e aggiornati, data-testid presenti)
- [ ] Sblocca: i 2 superadmin staging completano enrollment → `GET /admin/users/no-2fa` ritorna `[]`

---

*Design validato da: spec-panel (Fowler/Wiegers/Cockburn/Nygard/Newman/Crispin) + 4-agent codebase review (routing, conventions, BE-shapes, risks). Riferimento autoritativo per il plan di implementazione.*

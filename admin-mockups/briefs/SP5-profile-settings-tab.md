# SP5 — Brief Claude Design: Profile · Tab Settings + 2FA Enrollment Wizard

> **Preambolo obbligatorio**: leggi `admin-mockups/briefs/_common.md` prima di iniziare.
> Tutti i token, convenzioni, DoD si applicano a questo brief.

## Contesto

Il consolidation `/settings/*` → `/profile?tab=settings&section=*` introdotto in PR #5267 ha lasciato il redirect attivo (`next.config.js:228-238`) MA il tab `settings` **non è mai stato implementato** nel `/profile/page.tsx`. Il `Tab` type ha solo 3 valori: `overview | achievements | activity`. La conseguenza: il component `SecuritySettingsPage` (`apps/web/src/app/(authenticated)/settings/security/page.tsx`) — che contiene già il wizard 2FA completo (3-step) — è **unreachable via UI navigation**.

Issue tracking: **#1608** (P0 — bloccante per SP5 S3 strict 2FA cutover, [PR #1597](https://github.com/meepleAi-app/meepleai-monorepo/pull/1597) merged 2026-05-26).

Questo brief produce il design del 4° tab "Settings" con sub-section navigation per le 6 sezioni che il redirect promette: `profile · security · notifications · preferences · api-keys · services`. Focus pixel-perfect su **security + 2FA wizard** (lo sblocco di SP5 S3); le altre 5 sezioni come placeholder coerente.

## Fonti di riferimento

- `_common.md` — entity palette + typography + responsive contract
- `tokens.css` — **source of truth tokens** (HSL CSS vars)
- `settings.html` — current settings mockup (qui referenziamo come "vecchio scope") con placeholder `🛡️ 2FA toggle + QR`
- `auth-flow.html` — pattern modale TOTP login (riusabile per il verify-step del wizard)
- `02-desktop-patterns.html` — sidebar-detail pattern (tab + sub-nav)
- `03-drawer-variants.html` — bottom-sheet pattern (variant canonica per il wizard mobile)
- `05-dark-mode.html` — light/dark verification surface
- **Codebase**:
  - `apps/web/src/app/(authenticated)/profile/page.tsx` — TabBar attuale 3-tab da estendere a 4
  - `apps/web/src/app/(authenticated)/settings/security/page.tsx` — `SecuritySettingsPage` esistente (wizard funzionante con QR + verify + backup codes — clona stati e props)
  - `apps/web/src/components/profile/AvatarUpload.tsx`, `EditProfileSheet.tsx` — pattern profile data display
  - `apps/web/next.config.js:228-238` — i 3 redirect target da preservare semanticamente
  - `apps/api/src/Api/Routing/TwoFactorEndpoints.cs` — endpoint contract: `/api/v1/auth/2fa/setup`, `/api/v1/auth/2fa/enable`, `/api/v1/users/me/2fa/status`, `/api/v1/auth/2fa/disable`
  - `docs/api/2fa-step-up-protocol.md` — wire format per future step-up modale (out of scope qui, ma vocabolario coerente)

## Audience

- **Tutti gli utenti autenticati** — primary target (NON solo admin). Mobile-first obbligatorio: il 2FA enrollment va completato anche da telefono.
- **Superadmin (urgency case)**: 2 account staging (`admin@meepleai.app`, `badsworm@gmail.com`) hanno ricevuto email enrollment 2026-05-27 e devono poter completare il flow.

## Schermate da produrre (8 totali)

### Desktop (1280px+)

#### D1. Profile landing — tab Settings active (default section)

**Route target**: `/profile?tab=settings` (no `section` param)
**Pattern**: 4-tab TabBar header + content area con sidebar sub-navigation sticky a sinistra (240px).

**Sezioni**:
- Header `/profile`: avatar grande + display name + email + EditProfile CTA (riusa pattern attuale `/profile/page.tsx`)
- **TabBar** (sotto avatar): Overview · Achievements · Activity · **Settings** ← attivo (entity color `--c-player` violet, l'attuale tabBar profile usa già violet primary)
- Content split-view:
  - **Sidebar sub-nav (240px)**: 6 items con icon + label + (opzionale) badge stato
    - Profile (icon User, default selected)
    - Security (icon Shield, badge `⚠️ 2FA off` quando non enrollato — color `--c-warning` agent amber)
    - Notifications (icon Bell)
    - Preferences (icon Settings)
    - API keys (icon Key, badge "3" se ne esistono)
    - Connected services (icon Link)
  - **Detail pane**: Profile section default — placeholder con avatar+name+email+language+theme controls
- Background `--bg`, cards `--bg-card`, border `--border-light`

**Stati**: tab active highlight, sub-nav hover/active, dark/light coerente.

**Componenti v2 da progettare**: `ProfileTabBar` (estensione del TabBar attuale), `SettingsTab` (container con `SettingsSubNav` + `SettingsContent`), `SettingsSubNav`, `SettingsSubNavItem`, `SettingsSectionPlaceholder` (per i 5 sub non-security).

---

#### D2. Section Security — 2FA OFF (pre-enrollment)

**Route target**: `/profile?tab=settings&section=security`
**Pattern**: stessa sidebar (Security item attivo) + content area con 3 card verticali.

**Sezioni** (in content area):
- **Card "Two-factor authentication"** (priorità top):
  - Header: shield icon + "Two-factor authentication" + badge stato `⚠️ Not enabled` (border `--c-warning` 1px)
  - Body: 3 bullet brevi (security rationale)
  - CTA primary: "Set up two-factor authentication" (button entity-color = `--c-kb` teal, full width su mobile, auto on desktop)
- **Card "Active sessions"** (priorità medium):
  - Lista session attive con device fingerprint + last-seen + IP (mock 3 row: "Chrome on Mac OS · 2 min ago · 192.168.x", "Safari iOS · 1 day ago", "API client · 3 days ago")
  - Per-row CTA "Revoke" (text-button, destructive sull'hover)
  - CTA section: "Sign out all other sessions" (secondary button)
- **Card "Backup codes"** (disabled state):
  - Header: key icon + "Recovery codes" + badge `Disabled` (opacity 50%)
  - Body: "Available after enabling 2FA"
  - CTA disabled: "Generate codes"

**Stati**: 2FA off (default questa schermata), 2FA pending (QR scanned ma TOTP non confermato — banner top), errori validation (toast pattern).

**Componenti v2**: `SecuritySettingsContainer`, `TwoFactorStatusCard`, `ActiveSessionsCard`, `BackupCodesCard`, `SectionStatusBadge`.

---

#### D3. Section Security — Wizard 2FA Step 1/3 (QR scan)

**Route target**: stessa `?section=security` con modal/drawer open
**Pattern**: Modal centrato (desktop) — 560px width, padding generoso. Backdrop blur leggero. Click-outside chiuso solo allo step 1; step 2+3 require confirm.

**Sezioni**:
- Header: "Set up two-factor authentication" + step indicator pill `1 of 3` + close button `×`
- Progress bar 3 segmenti (1 attivo color `--c-kb`)
- Body 2-column:
  - Left (320px): **QR code SVG inline** (placeholder square 240px con `<rect>` pattern noise, NO immagine esterna) + "Scan with your authenticator app"
  - Right: "Can't scan? Enter this code manually" + monospace secret string (formattata in 4 gruppi di 4 caratteri) + copy-to-clipboard icon button
  - Lista app suggerite: "Google Authenticator · Authy · 1Password · Bitwarden" come chip riga
- Footer: "Cancel" (text-button) · "Continue" CTA primary entity `--c-kb`

**Stati**: setup loading (skeleton QR), setup error (banner top "Could not generate setup — try again"), copy-success toast.

**Componenti v2**: `TwoFactorSetupModal`, `WizardStepIndicator`, `QRCodePlaceholder`, `CopyableSecret`.

---

#### D4. Section Security — Wizard 2FA Step 2/3 (TOTP verify)

**Route target**: stessa modal, step 2
**Pattern**: stesso modal 560px, contenuto più compatto.

**Sezioni**:
- Header + close (stessa modal di D3)
- Progress bar 3 segmenti (1 done, 2 attivo, 3 future)
- Body center-aligned:
  - Icon shield large `--c-kb`
  - Title: "Enter the code from your app"
  - 6-segment OTP input (slot per slot, auto-advance, paste-friendly — pattern simile a `auth-flow.html` login 2FA modal)
  - Helper text: "The code refreshes every 30 seconds"
  - Errore state: "Invalid code. Try again." (color `--c-danger`)
- Footer: "Back" (text-button) · "Verify and enable" CTA primary

**Stati**: empty input, partial input, validation failed (**5 fail consecutivi → "Too many failed attempts. Try again in 15 minutes." banner + disable submit per 900s** con countdown mm:ss — allineato al BE `TotpService` Redis bucket 5/5min + lockout 15min, `retryAfterSeconds=900` subcode `locked_out` del wire-doc), success transition (animation freccia → step 3).

**Componenti v2**: `OTPInput6Slot` (riusa quello di `auth-flow.html` se già esiste), `WizardStepBody`.

---

#### D5. Section Security — Wizard 2FA Step 3/3 (backup codes)

**Route target**: stessa modal, step 3
**Pattern**: stesso modal, focus su "salvare i codici".

**Sezioni**:
- Header (no close `×` — solo "Done" finale)
- Progress bar (tutti 3 segmenti done color `--c-kb`)
- Body:
  - Banner success leggero: "2FA enabled · Save your recovery codes"
  - Grid 2 colonne x 5 codici monospace (10 totali — formato `XXXX-XXXX`)
  - Toolbar sotto: "Copy all" · "Download .txt" · "Print"
  - Checkbox required: "I have saved my recovery codes in a safe place"
- Footer: "Done" CTA primary entity `--c-toolkit` green (= success), disabled finché checkbox unchecked

**Stati**: codes pristine, codes copied (toast confirmation), checkbox unchecked (CTA disabled).

**Componenti v2**: `BackupCodesGrid`, `CopyAllAction`, `ConfirmedSavedCheckbox`.

---

#### D6. Section Security — 2FA ON (post-enrollment)

**Route target**: `/profile?tab=settings&section=security` (con 2FA enabled)
**Pattern**: stessa sidebar + content area, card "Two-factor authentication" in stato Enabled.

**Sezioni**:
- Card "Two-factor authentication":
  - Badge stato `✓ Enabled · 2 minutes ago` (border `--c-toolkit` green)
  - Riassunto: "Enabled with authenticator app · Last verified 5 min ago"
  - Actions row: "Regenerate recovery codes" (secondary) · "Disable 2FA" (destructive text-button)
- Card "Active sessions" — stesso pattern
- Card "Recovery codes":
  - Header con badge `✓ 10 codes remaining`
  - Body: "Codes are single-use. Generate new codes to invalidate the current set."
  - CTA: "Regenerate codes" (riapre modal step 3)

**Stati**: 2FA enabled, codes regenerated transition, disable confirm modal (separate concern — non disegnare qui, solo bottone).

### Mobile (375px target)

#### M1. Tab Settings — mobile (vertical sub-nav)

**Route target**: `/profile?tab=settings`
**Pattern**: TabBar orizzontale (4 tab scrollabili, "Settings" attivo, ultimo) + sub-nav verticale come **lista di card-rows** (NON sidebar laterale — mobile è single-column).

**Sezioni**:
- Header avatar+name compatto (riusa pattern profile mobile)
- TabBar orizzontale scrollabile (4 tab, gli ultimi 2 visibili al primo render con scroll indicator)
- **Sub-nav list** verticale, ogni item come row:
  - Icon (24px) · Label · Subtitle (es. "Manage 2FA") · ChevronRight
  - Spacing 16px tra row, separator `--border-light` 1px
  - Tap row → naviga a `?section=<name>` (mobile sostituisce la view della sub-section, niente split-view)
- Item "Security" mostra il badge `⚠️ 2FA off` inline accanto al label

**Stati**: default scroll position, scroll-to-end (tab Settings visibile), dark mode coerente.

---

#### M2. Section Security — mobile + Wizard 2FA bottom-sheet

**Route target**: `/profile?tab=settings&section=security`
**Pattern**: schermata full-screen che sostituisce la list M1 (con back-button top-left). Wizard 2FA come **bottom-sheet drawer** (pattern da `03-drawer-variants.html` variant tabbed) che sale dal basso.

**Sezioni** (schermata base):
- Header sticky: back-arrow + "Security" titolo + nessuna action right
- 2 card verticali stack su mobile: **2FA status + Active sessions** (ridimensionati mobile). La card **Recovery codes (disabled)** è omessa su mobile per density — riappare dopo l'enrollment (2FA ON) quando diventa actionable; pre-enrollment è ridondante con la 2FA-status card. Il set completo 3-card resta su desktop (D2).
- Tap CTA "Set up 2FA" → bottom-sheet drawer apre con step 1

**Bottom-sheet wizard**:
- Snap-point alto (90% viewport height)
- Drag handle in alto (visualmente)
- Step indicator pill + close `×` top-right (chiede confirm allo step 2+3)
- Body identico a D3/D4/D5 ma stacked verticale (QR sopra, secret sotto, app list ancora più sotto)
- Footer fisso bottom con CTA full-width

**Stati**: drawer closed, drawer half-snap (gesture), drawer full, wizard step 1/2/3, success state, error state, "Saved codes" checkbox + Done.

**Componenti v2**: `TwoFactorBottomSheet` (variant del Drawer canonico tabbed), `MobileBackHeader`, `SettingsSubNavList`, `SettingsSubNavRow`.

## Pattern non-negoziabili (oltre `_common.md`)

1. **Entity color security = `--c-kb`** (teal). 2FA è "protect your data" semantica coerente con knowledge-base shield. Warning badge "not enrolled" usa `--c-warning` (agent amber).
2. **NO immagini esterne**: QR code è SVG inline placeholder (`<svg viewBox="0 0 21 21">` con pattern `<rect>` random — non un vero QR. Lo sviluppatore lo sostituirà con `<Image src={qrCodeUrl}>` quando wira il component).
3. **OTP input pattern** = riusa lo stesso di `auth-flow.html` (modal TOTP login). Coerenza visiva non-negoziabile.
4. **Drawer mobile** = variant tabbed bottom-sheet (canonical, dal `03-drawer-variants.html`). NON full-page modal su mobile.
5. **Sub-nav desktop** = sticky sidebar 240px. Sub-nav mobile = vertical list su screen separata.
6. **Deep-link** preservato: `?tab=settings&section=security` rende esattamente quella section, anche dopo refresh.

## Acceptance criteria

- [ ] 8 schermate generate, 6 desktop + 2 mobile (M2 contiene wizard come bottom-sheet inline)
- [ ] Tutte le entity color coerenti con `_common.md` palette (no inventate)
- [ ] Light + Dark mode entrambi corretti per ogni schermata (verifica con `data-theme="dark"`)
- [ ] Tab "Settings" coerente visivamente con altri 3 tab del profile attuale
- [ ] Sub-nav 6 voci, badge stato `⚠️ 2FA off` su Security finché non enrollato
- [ ] Wizard 3-step con progress indicator, retrocede via "Back", interrompe via "Cancel"/close (step 2-3 con confirm)
- [ ] Backup codes copy-to-clipboard + Download .txt funzionanti nello stub JS
- [ ] Mobile bottom-sheet con drag handle, snap-points, swipe-down dismiss (con confirm guard)
- [ ] OTP input 6-slot con auto-advance + paste support
- [ ] Stati: 2FA OFF, pending, ON, lockout (rate limit hit)
- [ ] QR placeholder SVG inline (no asset esterno)
- [ ] Tweaks panel (theme toggle + frame swap) presente come negli altri mockup
- [ ] Coerenza con component esistente `SecuritySettingsPage` per props/state shape

## Out of scope

- **Step-up modale** per command-time 2FA challenge (wire doc già esiste in `docs/api/2fa-step-up-protocol.md`; sarà brief separato per SP5 S4)
- **Profile section** dettaglio (avatar upload flow esistente — solo placeholder qui)
- **Notifications/Preferences/API-keys/Services sections** — placeholder coerente, no dettaglio funzionale
- **Disable 2FA flow** (modal di conferma password + TOTP — separato)
- **Admin override disable 2FA** (admin-side, già implementato BE `POST /auth/admin/2fa/disable`)
- **WebAuthn/passkeys** (Q2 #186 P1.2 — epic separato)

## Output naming

Salva 2 file in `admin-mockups/design_files/`:

- `sp5-profile-settings.html` — stand-alone preview con theme toggle, tweaks panel, stub JS per tab/wizard/copy-clipboard/drawer swipe
- `sp5-profile-settings.jsx` — componente React clonabile in `apps/web/src/app/(authenticated)/profile/_components/SettingsTab.tsx` + sub-components

Aggiungi la coppia in `MOCKUPS_INDEX.md` sezione "SP5 — Admin tools" come page-mock mappata a `/profile?tab=settings*` (6 sub-section).

## DoD

- [ ] HTML stand-alone rendera correttamente aperto in browser senza dipendenze esterne (a parte `tokens.css`/`components.css` via `<link>`)
- [ ] JSX compila in `apps/web` con i Tailwind class mapping standard (`bg-card`, `text-foreground`, `border-border`, `bg-kb`, `bg-warning`, `bg-success`, `text-danger`)
- [ ] Cross-link `<a href="00-hub.html" /* DEMO-NAV */>← Hub</a>` per navigation tra mockup
- [ ] Issue #1608 può chiudersi quando questo brief + i 2 file mockup sono mergiati + un secondo PR di FE implementation li clona pixel-perfect

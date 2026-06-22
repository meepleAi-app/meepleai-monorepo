# US Manual Verification Log — DS-17 Phase B

**Started**: 2026-06-11
**Method**: visual verification (mockup ↔ app side-by-side)
**Order**: most common user-side flows first (high traffic → low traffic)
**Sub-issue**: #2127 | **PR**: #2128 | **Umbrella**: #2063

## Verdict taxonomy

| Symbol | Meaning | Action |
|---|---|---|
| ✅ PASS | Mockup + app match, US functional | Move to next US |
| ⚠️ VISUAL_DRIFT | Functional but UI differs from mockup | Note + decide accept/fix |
| 🔧 FUNCTIONAL_BUG | UI matches but flow breaks | File bug |
| 🚫 NOT_IMPLEMENTED | US sequence not reachable | Confirm scope |
| 📐 MOCKUP_OBSOLETE | Mockup outdated; app correct | Reclassify mockup `forward-refactor-obsolete` |

## Verification queue (priority: most common user-side first)

| # | US | Persona | Title | Mockup | Status |
|---|---|---|---|---|---|
| 1 | US-2 | Marco | Log in + resume session | `auth-flow.html` | 🔧 FUNCTIONAL_BUG (2026-06-11) |
| 2 | US-6 | Marco | Dashboard priority-driven | `sp4-dashboard.html` (📐 obsolete — verificato vs Asse C live) | 🔧 FUNCTIONAL_BUG (2026-06-11) |
| 3 | US-25 | Sara | Notifications inbox | `notifications.html` | ⚠️ VISUAL_DRIFT (2026-06-11) |
| 4 | US-10 | Sara | Library hybrid hub | `sp4-library-desktop.html` | ⚠️ VISUAL_DRIFT (2026-06-11) — finding #1 retracted via Gate 0 |
| 5 | US-8 | Marco | Games hub multi-tab (Discover default) | `sp4-discover.html` | ⚠️ VISUAL_DRIFT + 🔧 routing (2026-06-11) |
| 6 | US-9 | Giulia | Game detail tabs | `sp4-game-detail.html` (+ 5 tab mockups missing) | 🔧 FUNCTIONAL_BUG (MULTIPLE) (2026-06-11) |
| 7 | US-27 | Sara | AI agent chat | `chat-fullscreen.html` + `sp4-agents-index.html` | ⚠️ VISUAL_DRIFT + 🔧 nav gap (2026-06-11) |
| 8 | US-26 | Giulia | Profile + achievements | `settings.html` / `sp5-profile-settings.html` | ⚠️ VISUAL_DRIFT (i18n + BGG mention) (2026-06-11) |
| 9 | US-13 | Marco | GameNight create wizard | `sp7-game-night-create.html` | ✅ PASS (2026-06-11) |
| 10 | US-15 | Marco | GameNight detail | `sp7-game-night-detail-rsvp.html` | ✅ PASS (not-found path) (2026-06-11) |

(More US below queue, added on demand.)

## Verification log entries

(Each US gets a `### US-N — verdict — date` heading appended below.)

---

### US-2 — 🔧 FUNCTIONAL_BUG — 2026-06-11

**Persona / scope**: Marco · Log in + resume session
**Mockup**: `admin-mockups/design_files/auth-flow.html` + `auth-flow.jsx` (6 phone screens)
**Routes verificate**: `/login` (desktop 1280px), `/login` (mobile 375×812), `/login?reason=session_expired&from=...`, `/login?from=https://example.com/evil`
**Modalità**: Socratic — panel Cockburn · Adzic · Wiegers · Crispin · Fowler
**Tool**: Playwright MCP + backend code inspection
**Credenziali**: admin (per validare success path + open redirect)

**TTL / criteri misurabili (estratti dal backend)**
- `Session.DefaultLifetime` = **30 giorni** (`Session.cs:57`)
- `SessionManagementConfiguration.InactivityTimeoutDays` = **30 giorni** (`appsettings.json:109`)
- Temp 2FA session = **5 min, single-use** (`LoginCommandHandler.cs:126`)
- Account lockout = **15 min** dopo N failed (`LoginCommandHandler.cs:111`)
- Device limit = **max 5 unique device** (`LoginCommandHandler.cs:199`)
- Cookie session = HttpOnly (memoria: non accessibile da `document.cookie`)
- Audit: `LoginFailure` (unknown_email / invalid_password / account_locked) + `LoginSuccess` con email PII masked (`al***@example.com`)

**Edge case (Crispin)**
| # | Scenario | Esito |
|---|---|---|
| E1 | Bad credentials → submit | ✅ alert "Invalid email or password" reso; banner session_expired persistente sopra; form non resettato |
| E2 | `?from=https://example.com/evil` + login success | 🚨 **OPEN REDIRECT** — browser navigato a `https://example.com/evil` dopo cookie session settato |
| E3 | Cookie consent dialog presente | Dialog modale all'arrivo; va dismissato manualmente prima del form (backdrop intercetta) — non testata interazione con dialog aperto |
| E4 | 2FA timeout durante session_expired resume | Non testato (admin non ha 2FA attivo) — flag come gap di copertura |
| E5 | OAuth in-flight + session_expired race | Non testato — richiede flusso OAuth completo |

**Verdetto**: 🔧 **FUNCTIONAL_BUG** (HIGH severity — open redirect)

**Findings prioritized**

| # | Severity | Area | Finding | File:line | Recommendation |
|---|---|---|---|---|---|
| 1 | 🚨 **HIGH** | Security | Open redirect: `redirectAfterAuth(targetUrl=from)` accetta URL esterni; navigazione fuori dominio dopo login success | `apps/web/src/app/(auth)/login/_content.tsx:62` | Validare `from` con allowlist relativi (es. `from.startsWith('/') && !from.startsWith('//')`); fallback a `/library` |
| 2 | 🚨 **HIGH** | A11y (WCAG) | 11× "Some page content is not contained by landmarks" su `/login` (manca `<main>` wrapper) — viola WCAG 2.1 1.3.1 | `AuthCard` component (renderizza fuori `<main>`) | Wrappare AuthCard in `<main id="main-content">` |
| 3 | ⚠️ MED | A11y | "No skip link target" — link `#main-content` non risolve | stesso component | Aggiungere `id="main-content"` al landmark `<main>` |
| 4 | ⚠️ MED | Logging | 1 evento errore login → 4 console.error (HTTP 400 + 2× "API request failed" + 1× Logger ERROR `Login failed`) | `apps/web/src/lib/api.ts` + `_content.tsx:97` | Logger ERROR unico in catch; rimuovere duplicate API Error |
| 5 | ⚠️ MED | UX/Drift | Mockup mostra **2 OAuth** (Google+Discord); dev rende **3** (Google+Discord+GitHub) | `_content.tsx:213-217` | Designer decide: aggiungere GitHub in mockup o togliere da dev |
| 6 | ⚠️ MED | UX/Drift | Mockup non mostra banner `session_expired` — concetto "resume session" non visualizzato | `auth-flow.jsx` | Designer aggiunga screen mockup con banner giallo "sessione scaduta" |
| 7 | ⚠️ MED | Architecture | `LoginPageContent` legge `?reason=session_expired` ma non lo pulisce dopo render → al refresh il banner riappare anche se utente l'ha già visto | `_content.tsx:46` | Cleanup query param con `router.replace()` post-mount, o spostare reason in store ephemeral |
| 8 | ⏳ LOW | UX | Cookie consent dialog aperto al 1° arrivo su `/login` intercetta input form (backdrop modale) | `apps/web/src/components/CookieConsent.tsx` | Decisione UX: dialog blocking pre-login (legal compliance) o anchor non-modal? Da audit GDPR/privacy |
| 9 | ⏳ LOW | Test gap | Edge case E4 (2FA durante resume) + E5 (OAuth race) non coperti automatizzati | E2E suite | Aggiungere scenari in `apps/web/e2e/auth-flow.spec.ts` |

**Mockup vs dev — scoreboard**
- Brand mark + AuthCard layout: ✅ match
- Form (Email + Password + Show toggle + Accedi): ✅ match
- OAuth providers count: ⚠️ 2 mockup vs 3 dev (GitHub aggiunto)
- Banner session_expired: 🆕 presente in dev, **assente nel mockup** (gap mockup)
- "Password dimenticata" link: ✅ match (→ `/reset-password`)
- "Registrati" footer link: ✅ match (→ `/register`)
- StrengthMeter (registrazione): N/A per login screen
- Mobile responsive (375×812): ✅ AuthCard si adatta correttamente

**Mockup status update**: `auth-flow.html` rimane `current` ma è **incompleto** — manca screen "session expired banner". Suggerimento Phase B follow-up: aggiornare mockup o fidelity.json `design_intent: forward-refactor` con tracking.

**Tracking issues aperte** (2026-06-11)
- 🚨 **#2168 — P0 Security**: `[SECURITY] Login open redirect via ?from= query param`
- 🚨 **#2169 — P1 A11y**: `[a11y] /login viola WCAG 2.1 1.3.1 — content fuori landmark + skip link rotto`
- ⚠️ **#2170 — P2 Mockup**: `[mockup] auth-flow.html — add session_expired banner screen`
- ⚠️ **#2171 — P2 DX**: `[login] duplicate console.error su credenziali errate (4× per 1 evento)`
- ⏳ **#2172 — P3 Test**: `[e2e] auth-flow — coprire 2FA-during-resume + OAuth race`

**Console errors triage** (15 al peak, 11 baseline, 1 atteso): 11 a11y + 1 HTTP 401 `/auth/me` (atteso non loggato) + 3 duplicate logging su failure (ridondanti).

**Note Socratic** (dominio Marco — da chiarire con product)
- Marco è "mobile-first quick access" oppure cross-device? Mockup è phone, dev è responsive desktop+mobile → testato entrambi.
- "Resume" definito come "session TTL scaduto" (30gg inattività). UI rende il banner. **Manca documentazione user-facing** su quando scatta (es. tooltip o link "perché?").

**Next**: US-6 (Marco · Dashboard priority-driven · `sp4-dashboard.html` obsolete per #2114).

---

### US-6 — 🔧 FUNCTIONAL_BUG — 2026-06-11

**Persona / scope**: Marco · Dashboard priority-driven (Asse C #1898)
**Mockup**: `admin-mockups/design_files/sp4-dashboard.html` → 📐 **MOCKUP_OBSOLETE** (Pre-Stage-3 forward-design, superseded da Asse C, già tracked #2114)
**Routes verificate**: `/dashboard` (admin loggato, desktop + mobile 375×812)
**Modalità**: Socratic manuale — panel Cockburn · Adzic · Wiegers · Crispin · Fowler
**Tool**: utente fornisce screenshot + descrizione, Claude legge codice + screenshots
**Screenshot raccolti**: `_dashboard.png`, `_dashboardMobile.png`, `_sidebarMobile.png`

**Scoperta principale**
SuggestedSection è montata in `DashboardClient.tsx:236-242` ma renderizza `null` quando `gamesQuery.data?.games.length === 0` (silent fallback MAJ-6, vedi `SuggestedSection.tsx:82-84`). Lo screenshot mostra KPI "GIOCHI: 3" (da `useLibraryStats`) ma SuggestedSection assente fra Recenti e FriendsActivity → **`useGames` query restituisce 0 mentre `useLibraryStats` dice 3**. Inconsistenza fra 2 endpoint paralleli.

**Risposte panel da utente**
- (Cockburn primary goal): N/A — utente non ha risposto esplicitamente
- (Adzic concrete scenario): **"Giochi da aggiungere alla libreria che hanno il KB indicizzato (ossia un agent è utilizzabile)"** — chiarisce che Marco cerca DISCOVERY di nuovi giochi agent-ready, non recommendation di giochi già posseduti
- (mobile screenshots forniti): conferma 3 sezioni visibili anche mobile (no SuggestedSection)
- (privacy edge): 0 friends activity nel dataset corrente → test E5 non valutabile

**Edge case (Crispin)**
| # | Scenario | Esito |
|---|---|---|
| E1 | 0 GameNight pianificate | ✅ ProssimiSection rende empty state con CTA "Crea la tua prima Game Night" (corretto) |
| E2 | Live session in-progress (StartedAt non null) | ⏳ Non testato — filtro `Status === 'Published'` strict (DashboardClient.tsx:96) potrebbe escludere `InProgress` |
| E3 | Cascade drawer (asse-B) | Non verificato nel walkthrough — richiede click su card |
| E4 | Mobile responsive 375px | ✅ Layout colonna single, KPI 2×2 grid, bottom-tab bar attiva |
| E5 | Privacy friends | ⏳ Non valutabile (0 amici nel dataset) |

**Verdetto**: 🔧 **FUNCTIONAL_BUG** (HIGH — SuggestedSection mismatch data) + 💡 **PRODUCT GAP** (Marco goal != algoritmo MVP)

**Findings prioritized**

| # | Severity | Area | Finding | File:line | Recommendation |
|---|---|---|---|---|---|
| 1 | 🔧 **HIGH** | Bug | SuggestedSection nascosta nonostante 3 giochi in libreria. `useGames` returna 0/error, `useLibraryStats` returna 3 → silent fallback maschera bug | `DashboardClient.tsx:145-167` + `useGames` | Riconciliare i 2 endpoint o investigare perché `useGames(undefined,undefined,1,20)` non restituisce gli stessi 3 giochi |
| 2 | 💡 **P1** | Product | Marco goal mismatched: cerca "giochi agent-ready DA AGGIUNGERE", MVP rende "giochi già posseduti" | `DashboardClient.tsx:142-167` (MVP fixture) | Decidere: (a) ridefinire SuggestedSection come "discover agent-ready" (filtri `inLibrary=false AND kbIndexed=true`), oppure (b) aggiungere 5ª section "Espandi la libreria" (rompe DEC-1) |
| 3 | ⚠️ P2 | i18n | Label "Cosa fanno i tuoi" troncato (atteso "...amici") | `FriendsActivitySection` i18n key | Verificare i18n string + adattare width header card per copy completo |
| 4 | ⚠️ P2 | Navigation | 3 sistemi nav incongruenti: desktop top-nav (6) vs mobile sidebar (8) vs mobile bottom-tab (4) | `MainSidebar.tsx` mount in `DesktopShell.tsx` | Asse B MainSidebar 8-voce non rendered su desktop /dashboard. Verificare DesktopShell.tsx lg+ breakpoint condition |
| 5 | 📐 P2 | Mockup | `sp4-dashboard.html` confermato obsolete | tracked #2114 | Nessuna nuova issue, conferma reclassificazione |
| 6 | ⏳ P3 | Test gap | Live session in-progress non testato (filtro `Status === 'Published'` strict) | `DashboardClient.tsx:96` | Integration test con GN `Status === 'InProgress'` (post asse A WP1 #15) |
| 7 | ⏳ P3 | Privacy | FriendsActivity privacy edge non valutabile | endpoint friends-activity | Dataset di test con amici privati per validation futura |

**Mockup vs dev — scoreboard**
- DashboardHero + KPI grid: ✅ preserved come da spec Asse C
- 4 priority sections fixed order: 🔧 **3 rendered, 1 hidden** (Suggested silent fallback)
- DEC-1 (refactor in-place /dashboard): ✅
- Hero greeting per ora del giorno: ✅ "Buon pomeriggio"
- Cascade drawer asse-B: ✅ montato (`CascadeDrawerHost`)
- Mobile responsive: ✅ KPI 2×2 + bottom tab + hamburger sidebar

**Mockup status update**: `sp4-dashboard.html` rimane 📐 forward-refactor-obsolete (già tracked #2114).

**Console errors triage**: 4 issues (Next.js dev tools badge) — non triagated nel walkthrough manuale.

**Tracking issues aperte** (2026-06-11)
- 🔧 **#2176 — P0 Bug**: `[dashboard] SuggestedSection nascosta nonostante library > 0`
- 💡 **#2177 — P1 Product**: `[dashboard] Marco goal mismatched: discover agent-ready vs recommendation owned games`
- ⚠️ **#2178 — P2 i18n**: `[dashboard] FriendsActivitySection label troncato 'Cosa fanno i tuoi'`
- ⚠️ **#2179 — P2 Nav**: `[ui-shell] MainSidebar 8-voce assente da desktop /dashboard`

**Next**: US-25 Sara · Notifications inbox · `notifications.html`.

---

### US-25 — ⚠️ VISUAL_DRIFT — 2026-06-11

**Persona / scope**: Sara · Notifications inbox
**Mockup**: `admin-mockups/design_files/notifications.html` + `.jsx` — `design_intent: current`, `viewports: [desktop]`, **MA codice JSX è phone-mobile-first** (5 phone screens 380px con PhoneTopBar) → discrepanza fra fidelity claim e content reale
**Routes verificate**: `/notifications` (admin loggato, desktop)
**Modalità**: Socratic manuale — panel Cockburn · Adzic · Wiegers · Crispin · Fowler
**Tool**: utente fornisce screenshot, Claude legge mockup + dev + backend
**Screenshot**: `_notifications.png` (desktop, empty state)

**User input chiave (US-25)**
- Sara form factor: **Desktop + mobile responsive** → mockup va espanso a variant desktop
- Filter UX: **Rimuovi tab legacy** `Tutte/Non lette` → mantieni solo i 5 pill + toggle "Solo non lette" separato
- Open redirect: investigato backend → **risk LOW** (Notification.Link sempre path relativo hardcoded server-side)

**Backend audit (Fowler request)**
\`Notification.Link\` (`Notification.cs:17`) è sempre costruito server-side da codice trusted:
- Hardcoded: `"/admin/share-requests?sort=oldest"`, `"/settings/notifications"`, `"/library"`
- GUID-interpolated: \`$"/library/private/{notification.PrivateGameId}/toolkit"\`, \`$"/library/games/{evt.GameId}/agent"\`
- **Nessun user input arbitrario** accettato come link

→ Open redirect risk attuale BASSO. Defensive frontend validation comunque consigliata (P3) per resilience futura.

**Edge case (Crispin)**
| # | Scenario | Esito |
|---|---|---|
| E1 | 0 notifications | ✅ Empty state rendered (Bell icon + "Nessuna notifica") |
| E2 | 100+ pagination + filter combo | ⏳ Untested (no data) — URL state NON observato (`useState` puro, no `?page=`) |
| E3 | Detail drawer asse-B open | ⏳ Untested (no data) |
| E4 | Race SSE notifica arriva | ⏳ Untested (no SSE producer in dev) |
| E5 | Filter+tab combo 0 result | ⏳ Untested |
| E6 | Open redirect via `detail.link` | ✅ Backend safe (path relativi only); defensive FE validation MISSING (P3) |
| E7 | Mobile 375×812 | ⏳ Untested (screenshot solo desktop) |

**Verdetto**: ⚠️ **VISUAL_DRIFT** (dev funzionale ma con multiple divergenze UI/UX da mockup)

**Findings prioritized**

| # | Severity | Area | Finding | File:line | Recommendation |
|---|---|---|---|---|---|
| 1 | 💡 **P1** | Mockup | `notifications.html`/`.jsx` è phone-mobile-only mentre Sara è desktop+mobile responsive. `fidelity.json` claim `viewports: [desktop]` ma il content non è desktop | `admin-mockups/design_files/notifications.{html,jsx,fidelity.json}` | Commission variant desktop mockup; correggere fidelity.json a `forward-refactor` fino a delivery |
| 2 | ⚠️ **P2** | UX | Dual filter system: tab `Tutte/Non lette` legacy SOPRA i 5 pill duplicano "Tutte" semantica | `notifications/page.tsx:255-273` | Rimuovi tab legacy; aggiungi toggle "Solo non lette" come switch separato (entity counter sull'header). User input lockato |
| 3 | ⚠️ P2 | UX | Empty state minimale (Bell icon + 1 riga copy) vs mockup ha EmptyState con illustration + CTA | `notifications/page.tsx:313-329` | Aggiornare empty state con illustration (BellOff o custom SVG) + copy contestuale + CTA "Configura preferenze" → `/notifications/preferences` |
| 4 | ⚠️ P2 | UX | "Segna tutte come lette" CTA conditional su `hasUnread` → invisibile quando vuoto, vs mockup ⋯ menu sempre presente | `notifications/page.tsx:242-253` | Sempre visibile (disabled state quando 0 unread), oppure menu ⋯ con quick actions |
| 5 | ⚠️ P2 | UX | Detail Drawer asse-B vs mockup NotificationDetail (phone screen separato) | `notifications/page.tsx:371-406` | Drift di paradigma accettabile (mobile→drawer è pattern moderno); aggiornare mockup quando arriva variant desktop |
| 6 | ⏳ P3 | Security | `window.location.assign(detail.link)` senza validazione path relativo (defensive) | `notifications/page.tsx:396` | Aggiungere check `link?.startsWith('/') && !link.startsWith('//')` fallback `'/'` — anche se BE è safe ora |
| 7 | ⏳ P3 | UX | Pagination URL state assente — Sara non può share link con filter+page | `notifications/page.tsx:161` | Migrare `currentPage` + `filter` + `activeTab` a `useSearchParams` |
| 8 | ⏳ P3 | Test gap | E2-E5+E7 non testati (no data + no mobile screenshot) | E2E suite | Generare seed data + Playwright tests + mobile responsive snapshot |
| 9 | 📐 *No new issue* | Nav | Top-nav 6-voce desktop confermata anche su /notifications — duplicate gap di US-6 #2179 | — | Vedi #2179 (no duplicate) |

**Mockup vs dev — scoreboard**
- 5 filter categories + entity colors: ✅ match
- Group ordering oggi/ieri/settimana/precedenti: ✅ match
- Group startOfDay logic: ✅ match
- NotificationCard rendering: ✅ presumibile (no data per verifica)
- Phone-mobile vs desktop responsive: 🔧 mockup phone-only, dev desktop+mobile responsive (Sara goal "entrambi")
- Tab `Tutte/Non lette` extra in dev: ⚠️ va rimosso
- Empty state: ⚠️ troppo minimale vs mockup
- ⋯ menu mockup: ⚠️ assente in dev
- @mockup DS-17-1 annotation: ✅ presente

**Tracking issues aperte** (2026-06-11)
- 💡 **#2180 — P1 Mockup**: `[mockup] notifications.html: commission variant desktop (Sara è desktop+mobile)`
- ⚠️ **#2181 — P2 UX**: `[notifications] Rimuovi tab legacy 'Tutte/Non lette', aggiungi toggle 'Solo non lette'`
- ⏳ **#2182 — P3 Security**: `[notifications] Defensive validation window.location.assign(detail.link)`
- ⚠️ **#2183 — P2 UX**: `[notifications] Empty state minimale + CTA preferences mancante`

**Next**: US-10 Sara · Library hybrid hub · `sp4-library-desktop.html`.

---

### US-10 — 🔧 FUNCTIONAL_BUG — 2026-06-11

**Persona / scope**: Sara · Library hybrid hub
**Mockup**: `admin-mockups/design_files/sp4-library-desktop.html` + `.jsx` (canonical) + variant mobile + wishlist
**Routes verificate**: `/library` (admin loggato, desktop)
**Modalità**: Socratic manuale — panel Cockburn · Adzic · Wiegers · Crispin · Fowler
**Tool**: utente fornisce 3 screenshot
**Screenshot**: `_libraryFiltrGames.png` (main view), `_libreriaAddGameDrawer.png` (add game drawer), `_libraryFiltrAvanzati.png` (filter panel)

**Scoperta principale**
Hero CTA **"↓ Importa BGG" LIVE in dev** → **viola freeze #2123 + ADR #1903** (BGG user-side banned 2026-06-10). Surface confermata da memoria CLAUDE.md (spec-panel addendum #2 aveva pre-flaggato `sp4-library-desktop.{html,jsx}` come BGG forbidden). Il drawer Add Game è invece **conforme** (no BGG step, vedi `_libreriaAddGameDrawer.png`).

**Edge case (Crispin)**
| # | Scenario | Esito |
|---|---|---|
| E1 | 0 giochi new user | ✅ Empty state rendered: meeple icon + "Aggiungi il tuo primo gioco" + CTA (no BGG fallback) |
| E2 | 500+ giochi pagination | ⏳ Untested (no data) |
| E3 | Filter combo 0 results | ⏳ Untested |
| E4 | MeepleCard click → detail | ⏳ Untested (no data) |
| E5 | Mobile 375px | ⏳ Untested (solo desktop screenshot) |

**Verdetto**: 🔧 **FUNCTIONAL_BUG** (P0 — BGG ToS LIVE violation) + ⚠️ **VISUAL_DRIFT** secondario

**Findings prioritized**

| # | Severity | Area | Finding | File:line | Recommendation |
|---|---|---|---|---|---|
| 1 | 🚨 **P0** | Compliance | Hero CTA "↓ Importa BGG" LIVE viola ADR #1903 + freeze #2123 | `apps/web/src/app/(authenticated)/library/page.tsx` hero | Rimuovere il CTA "Importa BGG" o sostituirlo con "Importa da catalogo condiviso" |
| 2 | 🔧 **P1** | Data | KPI counter inconsistency: hero "0 Giochi totali" vs /dashboard "GIOCHI 3" | useGames vs useLibraryStats | Same root cause di **#2176** — link cross-issue |
| 3 | ⚠️ **P2** | UX | Triple filter overlap: tab entity + sub-tab status + filter panel "Tipo entità" | `library/page.tsx` filter components | Decidere single source of truth per filter type — eco #2181 |
| 4 | ⚠️ **P2** | UX | "Più filtri" panel full-screen invasive con "Applica" full-width orange bar | `library/page.tsx` filter modal | Refactor a side-drawer asse-B oppure action bar fixed compatto |
| 5 | 📐 **P2** | Mockup | `sp4-library-desktop.html` ha 2 BGG forbidden surfaces (hero + empty CTA) — già flagged spec-panel addendum #2 | `admin-mockups/design_files/sp4-library-desktop.{html,jsx,fidelity.json}` | Reclassificazione `forward-refactor-obsolete` per BGG ToS + tracking issue |
| 6 | ✅ | UX | Add Game drawer 2 opzioni "Manualmente" + "Catalogo condiviso" — NO BGG step | `_libreriaAddGameDrawer.png` | Mantieni — match ADR #1903 |
| 7 | 💡 P3 | Naming | "La tua libreria" multi-entity (giochi/agenti/documenti/chat) vs route `/library` semanticamente library = solo giochi | route naming | Considerare rename a `/hub` semantico o documentare hybrid hub concept |
| 8 | ⏳ P3 | Test gap | Mobile 375px responsive non testato | E2E suite | Aggiungere snapshot test mobile |
| 9 | 📐 *No new issue* | Nav | Top-nav 6-voce desktop duplicate da US-6 #2179 | — | Vedi #2179 |
| 10 | 📐 *No new issue* | Data | Stesso useGames/useLibraryStats inconsistency di US-6 | — | Vedi #2176 |

**Mockup vs dev — scoreboard**
- Hero "La tua libreria" + 4 KPI pill: ✅ presumibile match struttura
- "↓ Importa BGG" CTA: 🚨 entrambi (mockup + dev) hanno il bug BGG ToS
- Tab entity (Tutti/Giochi/Agenti/KB/Sessioni/Chat): ✅ presente sia mockup che dev
- Sub-tab status (Posseduti/Wishlist): ✅ presumibile match
- Filter panel "Più filtri": ⚠️ pattern UX invasive — verifica match mockup
- Add Game drawer: ✅ NO BGG step (dev) — corretto

**Tracking issues** (2026-06-11)
- ❌ **#2184 — CLOSED INVALID** (Gate 0 verification 2026-06-11): button role-gated correttamente, mio falso positivo (testato come admin). #1975 fix valido.
- 📐 **#2185 — P3 Mockup** (downgrade post Gate 0): mockup drift minor, designer review optional
- ⚠️ **#2186 — P2 UX**: `[library] Triple filter overlap: entity-tab + status-subtab + 'Più filtri' panel 'Tipo entità'`
- ⚠️ **#2187 — P2 UX**: `[library] 'Più filtri' panel full-screen invasive — refactor a side-drawer`

**Next**: US-8 Marco · Games hub multi-tab (Discover default) · `sp4-discover.html`.

---

### US-8 — ⚠️ VISUAL_DRIFT + 🔧 ROUTING — 2026-06-11

**Persona / scope**: Marco · Games hub multi-tab (Discover default per invariante #20)
**Mockup**: `sp4-discover.html` (queue) ↔ `sp4-games-index.html` (annotation dev) — 2 mockup ambigui per /games
**Routes verificate**: `/games` (default), `/games?tab=discover`, `/games?tab=catalogo`, `/games?tab=trending`, `/games?tab=community`, `/games?tab=invalid_tab_xyz` (fallback test), `/discover` (standalone backward compat)
**Modalità**: Playwright walkthrough automatico (utente ha delegato)
**Tool**: Playwright MCP login admin + snapshot + screenshot + network audit BGG
**Screenshot raccolti**: `us-8-games-default-desktop.png`, `us-8-games-catalogo-coming-soon.png`, `us-8-discover-standalone.png`, `us-8-games-tab-discover.png`, `us-8-games-mobile-375.png`

**Edge case (Crispin)**
| # | Scenario | Esito |
|---|---|---|
| E1 | `?tab=invalid_tab_xyz` | ✅ Fallback Discover content (graceful, URL preservato) |
| E2 | `?tab=catalogo` bookmark direct | ⚠️ Vede ComingSoon, **nessuna CTA fallback** (no "Torna a Discover" / "Vai a Libreria") |
| E3 | MiniNavSlot conformance | ✅ Mini-nav slot presente con breadcrumb "› Games" + 4 tab; ⚠️ button "Library" extra (ref=e38) intent unclear |
| E4 | Mobile 375px | ✅ Hamburger + tab horizontal scroll + bottom tab bar 5-voce |
| E5 | BGG ToS su Discover | ✅ **0 hit a host BGG** (network audit: 91 requests, 0 verso `cf.geekdo-images.com`/`boardgamegeek.com`/`geekdo-images.com`) |

**Network audit (Fowler request)**
- 91 requests totali
- 0 BGG hits ✅
- Endpoint chiamati: `/auth/me`, `/status-banner`, `/catalog/trending?limit=10`, `/sessions/active`, `/sessions/current`, `/users/<id>/chat-sessions/recent`, `/games`
- **Anomalia**: `/api/v1/catalog/trending?limit=10` viene chiamato (BE pronto) ma tab Trending in FE è ComingSoon placeholder → **shipping gap** (BE pronto, FE non wireato)

**Verdetto**: ⚠️ **VISUAL_DRIFT + 🔧 routing bug** (Hub link top-nav/bottom-tab → route obsoleta)

**Findings prioritized**

| # | Severity | Area | Finding | File:line | Recommendation |
|---|---|---|---|---|---|
| 1 | 🔧 **P1** | Routing | Top-nav desktop + bottom-tab mobile entrambi "Hub" link → `/hub/games` (route Stage 3 #1026 obsoleta) | header nav config | Aggiornare link "Hub" a `/games` (multi-tab hub) o rimuovere voce ridondante |
| 2 | 🔧 **P1** | Shipping | `/api/v1/catalog/trending` BE pronto, FE tab Trending è ComingSoon placeholder | `games/page.tsx:69 ComingSoonTab` per `trending` | Wire FE tab Trending all'endpoint BE esistente |
| 3 | ⚠️ **P2** | UX | 3 ComingSoon tabs senza CTA fallback (Catalogo/Trending/Community) — Marco dead-end | `games/page.tsx:68-82 ComingSoonTab` | Aggiungere CTA "↳ Torna a Discover" + "Vai a Libreria" in ComingSoon component |
| 4 | ⚠️ **P2** | UX | Badge "Hub · /discover" hardcoded — appare anche su `/games?tab=discover` | `DiscoverHub` component | Usare `pathnameOverride` prop o `usePathname()` per badge dinamico |
| 5 | ⚠️ P2 | UX | Search box duplicato: top (enabled) + section (disabled) | `DiscoverHub` rendering | Decidere single search; rimuovere disabled duplicate |
| 6 | ⚠️ P2 | UX | Button "Library" (L) extra in mini-nav slot accanto a tab Discover/Catalogo/Trending/Community | mini-nav config | Investigare intent; rimuovere o documentare semantica |
| 7 | ⏳ P3 | Data | 6 sezioni Discover tutte vuote ("Nessun elemento disponibile") — dataset insufficiente o endpoint silenti | endpoint backend | Seed dataset community + verificare endpoint render |
| 8 | ✅ Security | BGG ToS PASS | 0 hit ai host BGG | — | Pass — mantenere ESLint `local/no-bgg-host` gate verde |
| 9 | 📐 *No new* | Nav | Top-nav 6-voce duplicate da US-6 #2179 | — | Vedi #2179 |

**Mockup vs dev — scoreboard**
- Default tab Discover: ✅ match invariante #20
- 4 tab (Discover/Catalogo/Trending/Community): ✅ presenti
- 3 ComingSoon tabs: ⚠️ no CTA fallback
- Backward compat `/discover` standalone: ✅ preservato
- DiscoverHub shared component: ✅ stesso content fra `/discover` e `/games?tab=discover`
- BGG-free: ✅ confermato
- Mobile responsive: ✅ hamburger + bottom tab bar
- @mockup annotation: ⚠️ punta a `sp4-games-index.html` ma queue cita `sp4-discover.html` — 2 mockup per stessa route (da chiarire)

**Tracking issues aperte** (2026-06-11)
- 🔧 **#2190 — P1 Routing**: `[routing] Top-nav 'Hub' link punta a /hub/games (Stage 3 obsoleto)`
- 🔧 **#2191 — P1 Shipping**: `[games] Tab Trending placeholder ma backend già pronto`
- ⚠️ **#2192 — P2 UX**: `[games] ComingSoon tabs senza CTA fallback — dead-end UX`
- ⚠️ **#2193 — P2 UX**: `[discover] DiscoverHub UX cleanup: badge hardcoded + search duplicato + Library button extra`

**Next**: US-9 Giulia · Game detail tabs · `sp4-game-detail.html` (5 sub-tab mockup mancanti — Draft 11).

---

### US-9 — 🔧 FUNCTIONAL_BUG (MULTIPLE) — 2026-06-11

**Persona / scope**: Giulia · Game detail tabs (7 pages: main + 6 sub-routes)
**Mockup**: `sp4-game-detail.html` (canonical) + **5 sub-tab mockups missing** (Draft 11 spec-panel 2026-06-10)
**Routes verificate**: `/games/{id}` (Info default), `/games/{id}/reviews`, `/games/{id}/strategies`, `/games/{id}/chat`
**Test game**: 7 Wonders (id `eefcd3a3-f6f0-4fdd-9233-7c21f2b59d3a`)
**Modalità**: Playwright walkthrough automatico
**Tool**: Playwright MCP login admin + snapshot + screenshot

**Scoperta principale — TAB NAV INCOMPLETA + 3 ROUTES ORPHAN**
UI tab list su `/games/{id}`:
\`\`\`
Info | Regole | FAQ | Partite (🔒) | pages.gameDetail.tabs.stats (🔒) | Agenti | Documenti
\`\`\`

Route directory:
\`\`\`
/games/[id]/page.tsx                ← Info (selected default)
/games/[id]/rules/page.tsx          ← Regole tab
/games/[id]/faqs/page.tsx           ← FAQ tab
/games/[id]/sessions/page.tsx       ← Partite tab (disabled UI)
/games/[id]/reviews/page.tsx        ← ⚠️ NO TAB
/games/[id]/strategies/page.tsx     ← ⚠️ NO TAB
/games/[id]/chat/page.tsx           ← ⚠️ NO TAB (also redirect bug)
\`\`\`

→ **3 routes (`/reviews`, `/strategies`, `/chat`) sono ORPHAN** dalla navigazione UI. Giulia può raggiungerle solo via URL diretto.

**Backend 404**:
- \`GET /api/v1/games/{id}/reviews?pageNumber=1&pageSize=10\` → **404**
- \`GET /api/v1/games/{id}/strategies?pageNumber=1&pageSize=10\` → **404**

→ Anche se Giulia raggiunge le orphan pages, vede error alert.

**i18n broken**:
- Tab label raw \`pages.gameDetail.tabs.stats\` (key non risolta)
- Reviews/Strategies orphan rendono form in EN ("Write a Review", "Your Name", "Rating", "Submit Review", "Back to Game") mentre H1 + altro è IT

**Edge case (Crispin)**
| # | Scenario | Esito |
|---|---|---|
| E1 | Tab "Info" default | ✅ Rendered (KPI list + 2 generic blocks depth-truncated) |
| E2 | Tab disabled 🔒 (Partite, Stats) | ⚠️ Nessuna documentazione/tooltip su lock reason |
| E3 | Tab i18n broken | 🔧 \`pages.gameDetail.tabs.stats\` raw string |
| E4 | Orphan `/reviews` direct | 🔧 BE 404 + form EN mixed IT |
| E5 | Orphan `/strategies` direct | 🔧 BE 404 + back link EN mixed IT |
| E6 | `/chat` direct | ⚠️ Redirect a `/games/{id}` (wrapper trivial) |
| E7 | Back link da orphan | 🔧 Va a `/library/{id}` invece di `/games/{id}` |
| E8 | Console warnings | ⚠️ 58 warnings su game detail main + 59 dopo re-snapshot |
| E9 | Mobile responsive | ⏳ Untested |

**Verdetto**: 🔧 **FUNCTIONAL_BUG (MULTIPLE)** — tab nav incompleta + BE 404 + i18n broken + routing inconsistency

**Findings prioritized** (vedi sopra)

**Mockup vs dev — scoreboard**
- Hero "7 Wonders" + cover placeholder 🎲: ✅ rendered
- Tab list: 🔧 incompleta (3 routes orphan)
- 5 sub-tab mockups: 📐 **MISSING** (Draft 11 P1 unblock backlog)
- CTAs hero ("Torna al catalogo", "+ Aggiungi a libreria", "Condividi"): ✅ rendered
- i18n consistency: 🔧 multiple breaks

**Tracking issues aperte** (2026-06-11)
- 🚨 **#2194 — P0 Architecture**: `[game-detail] Tab nav incompleta: 3 routes orphan (/reviews /strategies /chat)`
- 🔧 **#2195 — P1 Backend**: `[backend] /api/v1/games/{id}/reviews + /strategies ritornano 404`
- 🔧 **#2196 — P1 i18n**: `[i18n] tab label 'pages.gameDetail.tabs.stats' unresolved + form EN mixed IT`
- ⚠️ **#2197 — P2 UX**: `[game-detail] Back link mismatch + tab 🔒 senza tooltip + /chat redirect`
- 💡 **#2198 — P1 Mockup**: `[mockup] Commission 5 sub-tab mockups (Draft 11)`

**Next**: US-27 Sara · AI agent chat · `chat-fullscreen.html` + `sp4-agents-index.html`.

---

### US-27 — ⚠️ VISUAL_DRIFT + 🔧 NAV GAP — 2026-06-11

**Persona / scope**: Sara · AI agent chat
**Mockup**: `chat-fullscreen.html` + `sp4-agents-index.html` (coverage 2/3 per gap report)
**Routes verificate**: `/agents`, `/agents/{id}`, `/chat`, `/chat/new`
**Test agent**: "Rules Expert" (id `d39d68ee-1cee-4ea9-9b90-68d26bebe2d0`, status archived)
**Modalità**: Playwright walkthrough automatico

**Verdetto**: ⚠️ **VISUAL_DRIFT** + 🔧 navigation gap

**Findings prioritized**

| # | Severity | Area | Finding | Recommendation |
|---|---|---|---|---|
| 1 | 🔧 P1 | UX | Agent detail manca tab Chat / CTA "Avvia chat" — Sara forced reverse navigation | Aggiungere tab Chat o sticky CTA "Avvia chat con questo agente" → `/chat/new?agentId={id}` |
| 2 | ⚠️ P2 | Test gap | Solo 1 agent archived nel dataset | Seed dataset agent attivo per E2E |
| 3 | ⚠️ P2 | UX | "↻ Riattiva" agent archived — flow post-reattivazione non chiaro | Documentare expected flow + redirect (auto-redirect a /chat/new o resta detail) |
| 4 | ✅ | UX | `/chat` 3-panel desktop layout | OK |
| 5 | ✅ | UX | `/chat/new` flow + empty state | OK |
| 6 | 📐 *No new* | Nav | Top-nav legacy duplicate da #2179, #2190 | — |

**Tracking issues**: 1 nuova (vedi sotto). Gap mockup `sp4-agent-detail` per detail (Draft 12 implicit) lasciato per Phase B follow-up dedicato.

**Next**: US-26 Giulia · Profile + achievements · `settings.html` / `sp5-profile-settings.html`.

---

### US-26 — ⚠️ VISUAL_DRIFT (i18n + BGG mention) — 2026-06-11

**Persona / scope**: Giulia · Profile + achievements
**Mockup**: `settings.html` / `sp5-profile-settings.html` (BGG forbidden flagged) + `sp4-profile-achievements.html` MISSING
**Routes verificate**: `/profile`, `/profile/achievements` (redirect wrapper), `/profile?tab=settings&section=profile`
**Modalità**: Playwright walkthrough automatico

**Finding chiave**
- Span text-only \"Connected services **BGG, Discord**\" live in profile Settings → text mention senza CTA (no network call BGG-host) MA brand esposto a user-side. **Review ADR #1903 scope** (text vs asset).
- 4 tab labels in EN (Overview/Achievements/Activity/Settings) mentre content IT
- `/profile/achievements` direct → redirect a `/profile?tab=achievements` (pattern wrapper, eco US-9 /chat)
- Link \"Tutti →\" in achievements panel punta a route redirect → loop minore

**Verdetto**: ⚠️ **VISUAL_DRIFT** (i18n + compliance review)

**Tracking issues**: 3 nuove + 1 mockup gap (sp4-profile-achievements absent).

**Next**: US-13 Marco · GameNight create wizard · `sp7-game-night-create.html`.

---

### US-13 — ✅ PASS — 2026-06-11

**Persona / scope**: Marco · GameNight create wizard (4-step)
**Mockup**: `sp7-game-night-create.html` (coverage 4/4 100% per gap report)
**Routes verificate**: `/game-nights/new?step=1`, `/game-nights/new?step=2` (step 2 visited)
**Modalità**: Playwright walkthrough automatico

**Findings**: ✅ Tutto IT consistent · ✅ wizard 4-step (Quando/Dove/Chi/Cosa) · ✅ validation funzionale (Avanti disabled fino a datetime valido) · ✅ live preview "Anteprima invito" aggiornata in real-time ("ven 12 giu, 19:00") · ✅ step URL `?step={N}` shareable · ✅ CTA RSVP preview disabled coerentemente (preview, no live action)

**Tracking issues**: 0 nuove. Top-nav legacy duplicate copre da #2179/#2190.

---

### US-15 — ✅ PASS (not-found path) — 2026-06-11

**Persona / scope**: Marco · GameNight detail + RSVP
**Mockup**: `sp7-game-night-detail-rsvp.html`
**Routes verificate**: `/game-nights/00000000-0000-0000-0000-000000000000` (fake ID, error path)
**Modalità**: Playwright walkthrough automatico
**Test gap**: 0 game-night reali nel dataset → happy path (con RSVP) untested

**Findings**:
- ✅ Not found handling: h2 "Serata non trovata" + paragraph "La serata che cerchi non esiste o è stata rimossa." + CTA "Torna al calendario" → `/game-nights`
- ✅ IT consistent
- ⏳ Happy path RSVP cycle (Crispin) untested — richiede seed game-night

**Tracking issues**: 0 nuove. Test gap RSVP cycle restituito al backlog.

---

---

---

---

---

---

---

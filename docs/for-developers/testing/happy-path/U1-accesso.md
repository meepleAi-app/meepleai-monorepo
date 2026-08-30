# Happy Path — U1 · Accesso, Onboarding & Pubbliche informative

> Catalogo scenari happy-path per l'area **U1**. Solo percorso di successo (nessuno scenario negativo/errore/edge). Formato Given/When/Then per `_TEMPLATE.md`. Osservabili basati su **struttura** (elemento presente, navigazione avvenuta, testo statico), mai su testo generato da LLM.

## Intestazione

- **Area**: U1 — Accesso, Onboarding & Pubbliche informative (25 route in `_coverage-map.md`).
- **Prerequisiti dati (seed)**: `make seed-sp4` — admin (da `infra/secrets/admin.secret`) + 5 utenti standard (`marco|sara|luca|giulia|andrea@meepleai.test`, premium, email-verificati dal seed) + catalogo giochi + PDF indicizzati. La password utente è quella di `seed_password()` (`infra/scripts/seed-sp4/lib/common.sh`) o l'override `SEED_SP4_PASSWORD`.
- **Utenti usati**: `marco@meepleai.test` (login/logout/onboarding/setup/cookie), admin (toggle Registration Mode è A4 — qui si assume lo stato già impostato), utenti *non registrati* (register/request-access/verify-email — istanze effimere), utente invitato (accept-invite/invites/setup-account — richiede token invito).
- **Nota gate email-verification** (spec §10): la registrazione end-to-end (`/register` public mode) colpisce `EmailVerificationMiddleware` — l'account nuovo resta **403** finché l'email non è verificata, **senza bypass admin**. Il token di verifica va recuperato in locale (SMTP fake/Mailhog o log API). Se non recuperabile, lo step di verifica è marcato **⚠️ blocked-env** e per il resto delle aree si usano gli utenti seed già verificati. Gli scenari `register`/`accept-invite`/`setup-account`/`invites` che richiedono un token di invito o di verifica dichiarano la precondizione di recupero token; in sua assenza sono **⚠️ blocked-env**.
- **Nota Registration Mode**: `/register` cambia superficie a runtime in base al flag `RegistrationMode` (DB-persisted). Con `publicRegistrationEnabled=false` mostra il popup `RequestAccessForm` (invito-only); con `=true` mostra il form standard `RegisterForm`. Entrambe le superfici sono coperte (U1-03 invite-only, U1-04 public).

---

## Matrice di copertura

| Route | Liv. | Scenario/i |
|-------|------|-----------|
| `(auth)/login` | Flow | U1-01 (login), U1-02 (logout) |
| `(auth)/register` | Flow | U1-03 (invite-only → RequestAccessForm), U1-04 (public → RegisterForm) |
| `(auth)/reset-password` | Flow | U1-05 (request), U1-06 (confirm con token) |
| `(auth)/setup-account` | Flow | U1-07 (attivazione account invitato) |
| `(auth)/verify-email` | Flow | U1-08 (verifica email via token) |
| `(auth)/verification-pending` | Smoke | U1-09 |
| `(auth)/verification-success` | Smoke | U1-10 |
| `(auth)/welcome` | Smoke | U1-11 |
| `(auth)/oauth-callback` | Smoke | U1-12 (redirect a `/login`) |
| `(auth)/invitation-expired` | Smoke | U1-13 |
| `(public)/` (landing) | Smoke | U1-14 |
| `(public)/accept-invite` | Flow | U1-15 (accetta invito serata via token) |
| `(public)/invites/[token]` | Flow | U1-16 (RSVP invito serata) |
| `(public)/about` | Smoke | U1-17 |
| `(public)/contact` | Smoke | U1-18 (form contatto) |
| `(public)/pricing` | Smoke | U1-19 |
| `(public)/legal` | Smoke | U1-20 (redirect a `/terms`) |
| `(public)/faq` | Smoke | U1-21 (ricerca + tab categorie) |
| `(public)/how-it-works` | Smoke | U1-22 |
| `(public)/privacy` | Smoke | U1-23 (smoke-aggregato legal) |
| `(public)/terms` | Smoke | U1-23 (smoke-aggregato legal) |
| `(public)/cookies` | Smoke | U1-23 (smoke-aggregato legal) |
| `(public)/cookie-settings` | Flow | U1-24 (salva preferenze cookie) |
| `(authenticated)/onboarding` | Flow | U1-25 (wizard 3-step completo), U1-26 (skip 3-step) |
| `(authenticated)/setup` | Flow | U1-27 (guida setup gioco) |

**Auto-verifica matrice**: 25/25 route mappate. Nessuna route scoperta. `privacy`/`terms`/`cookies` condividono lo smoke-aggregato **U1-20** (stesso primitive `LegalPageLayout`). Ogni scenario dichiara ≥1 osservabile.

---

## Scenari

### Autenticazione

```gherkin
Scenario U1-01 [Flow]: Login utente standard
  Given lo stack è avviato (make dev) e il seed è applicato (make seed-sp4)
    And NON sono autenticato
  When apro /login
    And compilo il campo "Email" con marco@meepleai.test
    And compilo il campo "Password" con la password seed
    And clicco il bottone di submit del form (login-form)
  Then vengo reindirizzato a /library
    And la chrome autenticata è presente (top bar utente)
  Osservabile ✅: form login SSR presente all'apertura (data-testid="login-form", campi email + password + separatore OAuth "google/discord/github") · dopo submit URL = /library senza errori Console/Network non attesi
  Route: (auth)/login → /library
  Utente: marco
```

```gherkin
Scenario U1-02 [Flow]: Logout dal menu utente
  Given sono loggato come marco@meepleai.test (post U1-01)
  When apro il menu utente (avatar in top bar)
    And clicco la voce "Esci" (logout-menu-item)
  Then la sessione viene chiusa
    And vengo reindirizzato a /login
  Osservabile ✅: voce "Esci" visibile nel dropdown (data-testid="logout-menu-item") · dopo click URL = /login con il form login visibile · un successivo tentativo di aprire /library rimbalza su /login?from=/library
  Route: /library (menu) → (auth)/login
  Utente: marco
```

```gherkin
Scenario U1-03 [Flow]: Registrazione invite-only mostra il popup Richiesta Accesso
  Given il RegistrationMode ha publicRegistrationEnabled=false (invito-only)
    And NON sono autenticato
  When apro /register
  Then la pagina mostra la superficie invite-only (AuthCard "inviteOnly")
    And è presente il form RequestAccessForm con il solo campo email
  When compilo il campo email con nuovo-tester@meepleai.test
    And clicco "Request Access" (request-access-submit)
  Then compare il messaggio di conferma richiesta inviata (enumeration-safe, sempre 202)
  Osservabile ✅: form richiesta accesso presente (data-testid="request-access-form" con "request-access-email" + "request-access-submit") · dopo submit compare data-testid="request-access-success" (banner verde "Request submitted!")
  Route: (auth)/register
  Utente: nuovo-tester (istanza effimera)
```

```gherkin
Scenario U1-04 [Flow]: Registrazione public mode con form standard
  Given il RegistrationMode ha publicRegistrationEnabled=true (public)
    And NON sono autenticato
  When apro /register
  Then la pagina mostra il form standard RegisterForm
  When compilo "Email" con hp-test-2026-07-10@meepleai.test
    And compilo "Password" con una password valida (≥12, maiuscola, minuscola, numero)
    And spunto il checkbox termini & condizioni (register-terms)
    And clicco il bottone di registrazione (register-form)
  Then l'account viene creato e vengo reindirizzato a /verification-pending?email=...
    And la pagina verification-pending mostra l'email mascherata e il bottone Reinvia
  Osservabile ✅: form registrazione presente (data-testid="register-form" con "register-email" + "register-password" + "register-terms") · dopo submit URL = /verification-pending?email=... con data-testid="verification-pending-page" e l'email in forma mascherata
  Note: la verifica email successiva è coperta in U1-08 e soggetta al gate SMTP (⚠️ blocked-env se il token non è recuperabile in locale)
  Route: (auth)/register → (auth)/verification-pending
  Utente: hp-test-2026-07-10 (istanza effimera)
```

```gherkin
Scenario U1-05 [Flow]: Richiesta reset password (mode request)
  Given NON sono autenticato
  When apro /reset-password (senza query ?token=)
  Then vedo il form "richiesta reset" con il campo email
  When compilo il campo email con marco@meepleai.test
    And clicco il bottone di invio (reset-password-submit)
  Then compare lo stato di conferma "email inviata"
  Osservabile ✅: form request presente (data-testid="reset-password-submit") · dopo submit compare data-testid="reset-password-success" (SuccessCard ✉️ con il messaggio "sentBody" che include l'email) · nessun errore Console/Network non atteso
  Route: (auth)/reset-password
  Utente: marco
```

```gherkin
Scenario U1-06 [Flow]: Conferma reset password con token valido
  Given ho ottenuto un token di reset valido per marco@meepleai.test
    And NON sono autenticato
  When apro /reset-password?token=<tokenValido>
  Then il token viene verificato e compare il form "nuova password"
  When compilo "Password" e "Conferma Password" con la stessa password valida (≥12, maiuscola, minuscola, numero)
    And clicco il bottone di conferma (reset-password-confirm)
  Then compare lo stato di successo e parte il redirect (best-effort auto-login → /chat, fallback / dopo ~2s)
  Osservabile ✅: durante la verifica compare data-testid="reset-password-verifying"; con token valido compare il form con "reset-password-confirm" · dopo conferma compare data-testid="reset-password-success" (icona ✅) e successiva navigazione fuori da /reset-password
  Precondizione token: recupero via SMTP fake/log locale. Se non recuperabile → ⚠️ blocked-env (solo lo step di conferma; il form request U1-05 resta eseguibile).
  Route: (auth)/reset-password
  Utente: marco
```

```gherkin
Scenario U1-07 [Flow]: Attivazione account invitato (setup-account)
  Given esiste un invito valido con token per un nuovo utente (email pre-associata)
    And NON sono autenticato
  When apro /setup-account?token=<tokenInvito>
  Then il token viene validato (POST /auth/validate-invitation) e compare il form "Configura Account"
    And i campi Email (e Nome se presente) sono precompilati in sola lettura
  When compilo "Password" e "Conferma Password" con la stessa password valida (≥12, maiuscola, minuscola, numero)
    And clicco "Configura Account"
  Then l'account viene attivato (POST /auth/activate-account) e compare lo stato "Account attivato!"
    And parte il redirect (/onboarding se requiresOnboarding, altrimenti /library) dopo ~1.5s
  Osservabile ✅: durante la validazione compare "Verifica in corso..."; con token valido compare il form con il campo Email disabilitato precompilato · dopo submit compare la card "Account attivato!" (✅) e successiva navigazione a /onboarding o /library
  Precondizione token: invito generato in locale (admin) + token estratto. Se non recuperabile → ⚠️ blocked-env.
  Route: (auth)/setup-account → /onboarding | /library
  Utente: utente invitato (istanza effimera)
```

```gherkin
Scenario U1-08 [Flow]: Verifica email via token
  Given ho registrato hp-test-2026-07-10@meepleai.test (U1-04) e ho ottenuto il token di verifica
    And NON sono autenticato
  When apro /verify-email?token=<tokenVerifica>&email=hp-test-2026-07-10@meepleai.test
  Then il token viene verificato al mount e compare lo stato di successo
    And parte l'auto-redirect a /library (VerificationSuccess, ~3s)
  Osservabile ✅: durante la verifica compare data-testid="verify-email-page" con messaggio "verifying" · a verifica riuscita compare data-testid="verification-success" e successiva navigazione a /library
  Precondizione token: recupero via SMTP fake/log locale. Se non recuperabile → ⚠️ blocked-env (per il resto delle aree si usano gli utenti seed già verificati).
  Route: (auth)/verify-email → /library
  Utente: hp-test-2026-07-10 (istanza effimera)
```

```gherkin
Scenario U1-09 [Smoke]: Pagina "verifica in sospeso"
  Given NON sono autenticato
  When apro /verification-pending?email=marco@meepleai.test
  Then la pagina carica e mostra l'email mascherata + il bottone Reinvia
  Osservabile ✅: data-testid="verification-pending-page" presente · l'email è mostrata mascherata (es. m***o@meepleai.test) · bottone Reinvia presente (data-testid="resend-verification-button") · nessun errore 4xx/5xx non atteso né errore JS
  Route: (auth)/verification-pending
  Utente: nessuno (route pubblica auth)
```

```gherkin
Scenario U1-10 [Smoke]: Pagina "verifica completata"
  Given NON sono autenticato
  When apro /verification-success
  Then la pagina carica e mostra il componente di successo con conto alla rovescia di redirect
  Osservabile ✅: data-testid="verification-success-page" presente con "verification-success-component" · countdown/redirect verso /library visibile · nessun errore Console/Network non atteso
  Route: (auth)/verification-success
  Utente: nessuno (route pubblica auth)
```

```gherkin
Scenario U1-11 [Smoke]: Pagina di benvenuto post-registrazione
  Given NON sono autenticato
  When apro /welcome
  Then la pagina carica e mostra il messaggio "Benvenuto in MeepleAI!" con progress bar
    And è presente il bottone "Vai alla Dashboard"
  Osservabile ✅: heading "Benvenuto in MeepleAI!" presente · progressbar (role="progressbar") visibile · bottone data-testid="welcome-go-dashboard" presente · auto-redirect verso /library dopo ~2s
  Route: (auth)/welcome → /library
  Utente: nessuno (route pubblica auth)
```

```gherkin
Scenario U1-12 [Smoke]: OAuth callback reindirizza a /login
  Given NON sono autenticato
  When apro /oauth-callback?error=example
  Then la route reindirizza server-side a /login preservando la query string
  Osservabile ✅: la navigazione termina su /login (con la query propagata, es. /login?error=example) e mostra il form login · nessun errore Console/Network non atteso
  Route: (auth)/oauth-callback → (auth)/login
  Utente: nessuno (route pubblica auth)
```

```gherkin
Scenario U1-13 [Smoke]: Pagina invito scaduto
  Given NON sono autenticato
  When apro /invitation-expired
  Then la pagina carica e mostra il messaggio di invito scaduto con le azioni
  Osservabile ✅: data-testid="invitation-expired-page" presente · testo "This invitation link has expired" visibile · link "Request Access" (→ /register) e "Back to Login" (→ /login) presenti · nessun errore Console/Network non atteso
  Route: (auth)/invitation-expired
  Utente: nessuno (route pubblica auth)
```

### Inviti serata (public)

```gherkin
Scenario U1-15 [Flow]: Accetta invito serata via /accept-invite
  Given esiste un invito serata valido con token per marco@meepleai.test
    And NON sono autenticato
  When apro /accept-invite?token=<tokenInvito>
  Then il token viene validato (POST /auth/validate-invitation) e compare la card "Welcome to MeepleAI" con il form password
    And il campo Email è precompilato in sola lettura
  When compilo "Password" e "Confirm Password" con la stessa password valida (≥8, maiuscola, numero, carattere speciale)
    And clicco "Create Account"
  Then compare la card "Account Created!" e parte il redirect a /onboarding (~1.5s)
  Osservabile ✅: durante la validazione compare "Validating your invitation..."; con token valido compare la card "Welcome to MeepleAI" con Email readonly precompilata · lo strength bar + checklist requisiti password reagisce all'input · dopo submit compare la card "Account Created!" e navigazione a /onboarding
  Precondizione token: invito generato in locale + token estratto. Se non recuperabile → ⚠️ blocked-env.
  Route: (public)/accept-invite → /onboarding
  Utente: utente invitato (istanza effimera)
```

```gherkin
Scenario U1-16 [Flow]: RSVP a un invito serata via /invites/[token]
  Given esiste una serata pubblicata con un invito e token per un partecipante
  When apro /invites/<tokenInvito>
  Then la pagina SSR carica il DTO pubblico dell'invito (host, gioco, data)
    And sono presenti le azioni di risposta RSVP (conferma / declina)
  When clicco l'azione di conferma presenza
  Then la risposta viene registrata e la UI riflette lo stato "confermato"
  Osservabile ✅: la pagina invito mostra i dettagli serata (titolo/host) senza cadere sul banner "token-invalid" · le azioni RSVP sono presenti · dopo la conferma la UI mostra lo stato risposto (alreadyRespondedAs aggiornato) senza errori Console/Network non attesi
  Precondizione: serata seed pubblicata + token invito valido (es. da un evento con _publish=true in data.json). Se non recuperabile → ⚠️ blocked-env.
  Route: (public)/invites/[token]
  Utente: partecipante invitato
```

### Pagine informative pubbliche

```gherkin
Scenario U1-14 [Smoke]: Landing marketing pubblica
  Given NON sono autenticato
  When apro / (landing)
  Then la landing SSR carica con hero, "come funziona", social proof e CTA
  Osservabile ✅: sezione hero (WelcomeHero) presente · le sezioni HowItWorksSteps + SocialProofBar + WelcomeCTA sono renderizzate · nessun errore Console/Network non atteso
  Route: (public)/ (landing)
  Utente: nessuno (pubblica)
```

```gherkin
Scenario U1-17 [Smoke]: Pagina About
  Given NON sono autenticato
  When apro /about
  Then la pagina carica con hero, mission, story, valori, team
  Osservabile ✅: heading area presente (data-testid="about-heading") · le 4 card valori (accessibility/precision/community/innovation) sono renderizzate · link footer "Come Funziona" (→ /how-it-works) presente · nessun errore Console/Network non atteso
  Route: (public)/about
  Utente: nessuno (pubblica)
```

```gherkin
Scenario U1-18 [Smoke]: Pagina Contatti con form
  Given NON sono autenticato
  When apro /contact
  Then la pagina carica con il form contatto (nome, email, oggetto, messaggio) e la sidebar info
  When compilo nome/email/oggetto/messaggio e invio il form
  Then compare il messaggio di esito (success) sotto il form
  Osservabile ✅: heading area presente (data-testid="contact-heading") · form con i 4 campi + select oggetto (6 opzioni) presente · dopo invio compare il messaggio "success" (o "error" in caso di API giù, che è comunque un effetto visibile) · azione primaria produce effetto a schermo
  Route: (public)/contact
  Utente: nessuno (pubblica)
```

```gherkin
Scenario U1-19 [Smoke]: Pagina Pricing (3 tier)
  Given NON sono autenticato
  When apro /pricing
  Then la pagina carica con i 3 tier Free / Pro / Team
  Osservabile ✅: hero "board-game friendly" presente · 3 PricingCard renderizzate (Free €0, Pro €9/mese, Team €29/mese) con CTA · link footer "Domande frequenti" (→ /faq) presente · nessun errore Console/Network non atteso
  Route: (public)/pricing
  Utente: nessuno (pubblica)
```

```gherkin
Scenario U1-20 [Smoke]: Pagina Legal reindirizza a /terms
  Given NON sono autenticato
  When apro /legal
  Then la pagina placeholder mostra "Coming Soon" e dopo ~2s reindirizza a /terms
  Osservabile ✅: heading "Coming Soon" presente all'apertura · dopo ~2s la navigazione approda su /terms (pagina Termini renderizzata) senza errori Console/Network non attesi
  Route: (public)/legal → (public)/terms
  Utente: nessuno (pubblica)
```

```gherkin
Scenario U1-21 [Smoke]: FAQ con ricerca e tab categorie
  Given NON sono autenticato
  When apro /faq
  Then la pagina carica con hero, search bar, griglia FAQ popolari e tab categorie
  When digito un termine nella search bar
    And clicco una tab categoria diversa
  Then la lista FAQ si aggiorna in base al filtro
  Osservabile ✅: hero FAQ presente (data-testid="faq-hero") · griglia "popular" (QuickAnswerCard) visibile con query vuota · le CategoryTabs con conteggi sono presenti · digitando un termine il banner risultati (role="status") o l'aggiornamento lista è visibile (azione primaria produce effetto a schermo) · nessun errore Console/Network non atteso
  Route: (public)/faq
  Utente: nessuno (pubblica)
```

```gherkin
Scenario U1-22 [Smoke]: Pagina How It Works
  Given NON sono autenticato
  When apro /how-it-works
  Then la pagina carica con hero, i 3 step e la sezione features
  Osservabile ✅: hero con CTA registrazione presente · i 3 step (numerati 01/02/03) renderizzati · le 4 feature (rag/multilingual/pdfUpload/gameLibrary) renderizzate · CTA "about" (→ /about) e "faq" (→ /faq) presenti · nessun errore Console/Network non atteso
  Route: (public)/how-it-works
  Utente: nessuno (pubblica)
```

```gherkin
Scenario U1-23 [Smoke]: Pagine legali (privacy, terms, cookies) — smoke-aggregato
  Given NON sono autenticato
  When apro /privacy
    And apro /terms
    And apro /cookies
  Then ciascuna pagina carica con il LegalPageLayout (sezioni ad accordion, toggle lingua IT/EN, "ultimo aggiornamento")
    And espando una sezione dell'accordion
  Then il contenuto della sezione diventa visibile
  Osservabile ✅: per ognuna delle 3 route l'accordion di sezioni è renderizzato (privacy: sezione "introduction" aperta di default; terms: "acceptance"; cookies: "whatAreCookies") · l'espansione di una sezione mostra il contenuto (azione primaria produce effetto a schermo) · i link prev/next di navigazione legale sono presenti · nessun errore Console/Network non atteso
  Route: (public)/privacy · (public)/terms · (public)/cookies
  Utente: nessuno (pubbliche)
```

```gherkin
Scenario U1-24 [Flow]: Salva preferenze cookie
  Given NON sono autenticato (route pubblica)
  When apro /cookie-settings
  Then la pagina carica con i toggle Essenziali (disabilitato/on), Analytics, Funzionali
  When attivo il toggle Analytics
    And clicco "Salva"
  Then le preferenze vengono persistite (cookie-consent) e compare il toast di conferma
  Osservabile ✅: i 3 SettingsRow (essential/analytics/functional) presenti con i rispettivi ToggleSwitch · dopo "Salva" compare il toast di successo (sonner) e lo stato dei toggle riflette la scelta · alla riapertura della pagina i toggle salvati sono ripristinati (getStoredConsent)
  Route: (public)/cookie-settings
  Utente: nessuno (pubblica)
```

### Onboarding & Setup (authenticated)

```gherkin
Scenario U1-25 [Flow]: Wizard onboarding 3-step completato
  Given sono loggato come marco@meepleai.test
    And il mio account NON ha onboardingCompleted (per il test; se già completato la route redirige a /library)
  When apro /onboarding
  Then compare il WizardModal 3-step (indicatore "Step 1 of 3")
  When allo step 1 "Interessi" seleziono ≥1 categoria (es. Strategy) e avanzo (Next)
    And allo step 2 "Primo gioco" cerco "Azul", lo seleziono e clicco "Add to Library"
    And avanzo allo step 3 "Invita un amico" (placeholder) e clicco "Complete"
  Then l'onboarding viene marcato completato (api.auth.completeOnboarding) e vengo reindirizzato a /library
  Osservabile ✅: WizardModal presente (data-slot="wizard-modal") con indicatore "Step 1 of 3" · step 1 mostra la griglia di 9 categorie (data-testid="interest-strategy" ecc.) · step 2 la ricerca catalogo interno restituisce "Azul" (game-search-results) e la selezione mostra data-testid="selected-game" · step 3 mostra data-testid="invite-friend-coming-soon" · dopo "Complete" URL = /library + toast "Onboarding completato!"
  Route: (authenticated)/onboarding → /library
  Utente: marco
```

```gherkin
Scenario U1-26 [Flow]: Onboarding 3-step saltando gli step opzionali
  Given sono loggato come marco@meepleai.test
    And il mio account NON ha onboardingCompleted (per il test)
  When apro /onboarding
    And allo step 1 clicco "Skip"
    And allo step 2 clicco "Skip"
    And allo step 3 clicco "Complete"
  Then l'onboarding viene completato e vengo reindirizzato a /library
  Osservabile ✅: il bottone "Skip" (data-slot="wizard-skip") è presente sugli step opzionali 1 e 2 · l'indicatore avanza "Step 1/2/3 of 3" ad ogni skip · allo step 3 il bottone finale mostra "Complete" (data-slot="wizard-next") · dopo "Complete" URL = /library
  Route: (authenticated)/onboarding → /library
  Utente: marco
```

```gherkin
Scenario U1-27 [Flow]: Guida al setup di un gioco
  Given sono loggato come marco@meepleai.test
    And lo stack full (make dev) è attivo (serve l'AI per generare la guida)
    And il gioco "Azul" ha un PDF regole indicizzato (seed KB)
  When apro /setup
  Then vedo l'intestazione "Game Setup Guide" e il selettore gioco popolato dal catalogo
  When seleziono "Azul" dal menu e clicco "Generate Setup Guide"
  Then dopo l'elaborazione compare la guida con la lista di step e la barra di progresso
  When spunto uno degli step come completato
  Then la barra di progresso e il contatore "N / M steps" si aggiornano
  Osservabile ✅: heading "Game Setup Guide" presente · il select gioco contiene "Azul" · dopo "Generate Setup Guide" compare la card con "gameTitle" + progress bar (0%) e ≥1 SetupStepCard · spuntando uno step la percentuale e il contatore "N / M steps" aumentano (azione primaria produce effetto a schermo) · nessun errore Console/Network non atteso
  Note: il *contenuto* degli step è generato da LLM → non è oggetto di asserzione; si verifica solo la struttura (presenza guida, step count, progressione).
  Route: (authenticated)/setup
  Utente: marco
```

---

## Auto-verifica finale

- **Copertura route**: tutte le 25 route U1 di `_coverage-map.md` compaiono nella matrice (23 scenari; `privacy`+`terms`+`cookies` condividono lo smoke-aggregato U1-23). Nessun buco.
- **Osservabili**: ogni scenario dichiara ≥1 osservabile basato su struttura/navigazione/testo statico (mai testo LLM).
- **Solo happy path**: nessuno scenario negativo/errore/edge; gli stati d'errore citati (es. `/contact` API giù) sono menzionati solo come "effetto visibile a schermo" ammesso dal criterio Smoke, non come scenario a sé.
- **Gate ambientali dichiarati**: gli scenari con token (U1-06 conferma, U1-07, U1-08, U1-15, U1-16) e la verifica email dichiarano la precondizione di recupero token e il fallback ⚠️ blocked-env (spec §10).
- **Dati marcati**: le entità create dai Flow usano il marcatore `hp-test-2026-07-10` (email registrazione U1-04/U1-08).

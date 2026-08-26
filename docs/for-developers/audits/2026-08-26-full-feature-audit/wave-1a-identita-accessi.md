# Ondata 1A — Identità e accessi

> **Contesti**: Authentication · SecurityAudit · Administration (utenti, inviti, richieste di accesso, impersonificazione)
> **Ambiente**: locale, `make dev`, stack completo · **Data**: 2026-08-26
> **Ruoli**: `test@meepleai.com` (utente) · `badsworm@gmail.com` (superadmin)

## Esito in breve

I percorsi critici dell'autenticazione **si comportano come promesso**, casi negativi inclusi:
nessuna difformità sul controllo degli accessi, in nessuno dei casi provati.

I due findings stanno altrove: quattro endpoint del blocco utenti rispondono 500, e l'audit di
sicurezza registra solo gli accessi.

| Contesto | Coperte | Totale |
|---|---|---|
| Authentication | 34 | 67 |
| SecurityAudit | 2 | 2 |
| Administration | 84 (75 verificate + 9 findings) | 272 |

L'ondata **non è chiusa**: del blocco utenti restano le 64 mutazioni, e di Authentication mancano
2FA, verifica email e profilo. Vedi *Cosa resta*.

## Verifiche superate

### Accesso e sessione (L2/L3)

| Caso | Osservato |
|---|---|
| Accesso dalla UI | 200, cookie `meepleai_session` emesso, redirect a `/library`, riga in `user_sessions` |
| Uscita | 200; `/auth/me` risponde poi 401; **la riga di sessione risulta revocata nel DB** (`RevokedAt` valorizzato) |
| Password errata | 400, **nessun cookie emesso** |
| Enumerazione utenti | Utente inesistente e password errata su utente reale danno **status e messaggio identici** (`400 "Invalid email or password"`) |
| Utente semplice su `/admin/users` | 403 |
| Utente semplice su audit log | 403 |
| Utente semplice su impersonificazione (2 endpoint) | 403 |

### Registrazione invite-only (L3)

Con `publicRegistrationEnabled=false`, `/register` mostra il modulo di richiesta accesso e **non
espone alcun campo password** — il prodotto mantiene la promessa dichiarata nel CLAUDE.md.

Ciclo completo verificato:

1. `POST /auth/request-access` → **202**, messaggio non rivelatore, riga `Pending` in `access_requests`
2. `GET /admin/access-requests` → 200, la richiesta compare
3. `POST /admin/access-requests/{id}/approve` → 200 `{"status":"approved"}`
4. DB: `Approved` + `reviewed_at` + `invitation_id` valorizzati
5. Email: **"You've been invited to MeepleAI"** consegnata (Mailpit)

### Recupero password (L3)

`POST /auth/password-reset/request` con email esistente → 200, email consegnata, token in
`password_reset_tokens`.

Con email **inesistente**: risposta byte-identica, **nessuna email inviata, nessun token creato**.
È il comportamento corretto — la risposta non permette di dedurre se l'account esista.

## Blocco utenti di Administration — letture

Le 57 letture del blocco (utenti, inviti, richieste di accesso, impersonificazione, allowlist)
sono state provate **con entrambi i ruoli**: 56 eseguite, 1 saltata per parametro non risolvibile.

| Esito | Conteggio |
|---|---|
| Conformi | 51 |
| Difformi | 5 |

Il criterio non è uniforme, ed è la parte che conta: un endpoint **admin** è conforme se respinge
l'utente semplice (401/403); un endpoint **self-service** (`/users/me/…`) è conforme se
all'utente risponde. Applicare il primo criterio a tutti produceva **33 falsi positivi** — ogni
`/users/me/quota` letto dal proprio titolare risultava una falla. Il tracker porta già il ruolo
atteso per riga, e ora il giudizio lo usa.

### Tre difformità che non erano difetti

| Segnalazione | Realtà |
|---|---|
| `GET /users/search` → 400 | Il parametro si chiama `query`, non `q`. Con quello: 200 |
| `GET /users/me/games` → 400 | Richiede `page` e `pageSize`. Con quelli: 200 |
| `GET /admin/staging-allowlist` → 403 all'utente | Corretto: l'endpoint è documentato *"Superadmin only"*. Era il tracker a classificarlo come self-service, perché il parser non ne ha dedotto l'autorizzazione |

Le prime due non sono difetti del prodotto ma del modo in cui la sonda chiama: un endpoint provato
senza i suoi parametri obbligatori risponde 400, e chiamarlo "rotto" sarebbe un errore di metodo.

## Findings

### 🔍 P1 — 500 su quattro endpoint del blocco utenti — [#3839](https://github.com/meepleAi-app/meepleai-monorepo/issues/3839)

| Endpoint | Causa accertata |
|---|---|
| `GET /admin/users/{userId}/library/stats` | `Cannot create a DbSet for 'UserLibraryEntry' because this type is not included in the model for the context` |
| `GET /admin/users/{userId}/ai-usage` | `QueryableMethodTranslatingExpressionVisitor.Translate` — LINQ non traducibile in SQL |
| `GET /admin/users/{id}/rate-limit-status` | come sopra |
| `GET /users/me/ai-usage` | come sopra — ed è **self-service**: lo subisce l'utente, non l'amministratore |

Nota metodologica: cercando la causa nei log, la prima eccezione che compare riguarda
`HealthStatusChangedEvent` e non c'entra nulla — è un job periodico che fallisce in sottofondo
(pattern ADR-063). Correlare per `RequestPath` invece di leggere l'ultimo errore è ciò che ha
evitato di attribuire ai quattro endpoint una causa sbagliata.

### 🔍 P2 — L'audit di sicurezza registra solo gli accessi — [#3838](https://github.com/meepleAi-app/meepleai-monorepo/issues/3838)

`security_audit_logs` contiene **due soli tipi di evento**:

| EventType | Occorrenze |
|---|---|
| `auth.login.success` | 38 |
| `auth.login.failure` | 15 |

I tentativi falliti *sono* tracciati, il che è la parte che si dimentica più spesso. Mancano però
gli eventi per il resto del ciclo di vita dell'identità: **uscita, cambio password, recupero
password, attivazione e disattivazione 2FA, cambio di ruolo**.

Conseguenza concreta: il logout revoca la sessione nel database ma non lascia traccia nell'audit,
quindi da quel registro non si può ricostruire quando una sessione sia stata chiusa
volontariamente, né distinguere una scadenza da un'uscita.

L'impersonificazione è tracciata altrove (`audit_logs` ha le colonne `impersonated_user_id` e
`step_up_token_id`), quindi non rientra nel buco — ma la separazione fra i due registri andrebbe
dichiarata, perché chi cerca gli eventi di sicurezza guarda `security_audit_logs`.

## Cosa resta

| Area | Righe scoperte | Nota |
|---|---|---|
| Administration — mutazioni del blocco utenti | 64 | 40 POST, 12 DELETE, 10 PUT, 2 PATCH: creazione, ruoli, sospensione, tier, inviti, impersonificazione completa. Richiedono un utente di prova dedicato e un ordine che lasci l'ambiente come l'ha trovato |
| Administration — resto del contesto | ~124 | Fuori dal blocco utenti: appartiene all'ondata 1B |
| Authentication — 2FA | 5 endpoint | `setup`, `enable`, `verify`, `disable`, `step-up` |
| Authentication — OAuth | — | `oauthEnabled=false` in locale: verificabile solo su staging |
| Authentication — verifica email | 2 endpoint | `email/verify`, `email/resend` |

**OAuth non è verificabile in locale** (`oauthEnabled: false`): va all'ondata 5, insieme alle altre
funzioni che dipendono da infrastruttura esterna.

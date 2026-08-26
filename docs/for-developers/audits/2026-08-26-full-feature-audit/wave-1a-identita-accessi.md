# Ondata 1A — Identità e accessi

> **Contesti**: Authentication · SecurityAudit · Administration (utenti, inviti, richieste di accesso, impersonificazione)
> **Ambiente**: locale, `make dev`, stack completo · **Data**: 2026-08-26
> **Ruoli**: `test@meepleai.com` (utente) · `badsworm@gmail.com` (superadmin)

## Esito in breve

I percorsi critici dell'autenticazione **si comportano come promesso**, casi negativi inclusi.
L'unico difetto trovato riguarda la completezza dell'audit di sicurezza, non il controllo degli
accessi.

| Contesto | Verificate | Totale |
|---|---|---|
| Authentication | 34 | 67 |
| SecurityAudit | 2 | 2 |
| Administration (blocco utenti) | 24 | 272 |

L'ondata **non è chiusa**: Administration resta largamente scoperta, e di Authentication mancano
2FA, OAuth, verifica email e profilo. Vedi *Cosa resta*.

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

## Findings

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
| Administration — utenti e ruoli | ~248 | CRUD utenti, ruoli, sospensione, tier, inviti: il grosso dell'ondata |
| Authentication — 2FA | 5 endpoint | `setup`, `enable`, `verify`, `disable`, `step-up` |
| Authentication — OAuth | — | `oauthEnabled=false` in locale: verificabile solo su staging |
| Authentication — verifica email | 2 endpoint | `email/verify`, `email/resend` |

**OAuth non è verificabile in locale** (`oauthEnabled: false`): va all'ondata 5, insieme alle altre
funzioni che dipendono da infrastruttura esterna.

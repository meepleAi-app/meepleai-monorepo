# Account & accesso

> **Cos'è in una frase**: come crei un account MeepleAI, fai login, e gestisci sicurezza e dati personali.

---

## In breve

| | |
|---|---|
| **A cosa serve** | Avere una libreria persistente, salvare cronologia chat, far parte di un workspace |
| **Per chi** | Tutti — l'account è gratuito (in fase Alpha) |
| **Metodi di accesso** | Email + password, Google OAuth, GitHub OAuth |
| **Sicurezza opzionale** | 2FA con app authenticator (TOTP) |

---

## Creare un account

1. **Registrati** dalla home → **Sign up**.
2. Scegli:
   - **Email + password** (min 12 caratteri, mix maiuscole/numeri/simboli)
   - **Continua con Google**
   - **Continua con GitHub**
3. Conferma l'email cliccando il link che ricevi (entro 24 ore).
4. Fatto — entri nella libreria vuota.

---

## Login

| Metodo | Note |
|--------|------|
| Email + password | Standard |
| OAuth Google | Un clic se sei già loggato su Google |
| OAuth GitHub | Un clic se sei già loggato su GitHub |
| **2FA TOTP** | Se attivo, dopo password chiede codice 6 cifre dall'app (Google Authenticator / Authy / 1Password / ...) |

Sessione attiva per **30 giorni** (rinnovata ad ogni accesso). Logout esplicito da menu utente → **Esci**.

---

## Reset password

Ho dimenticato la password:

1. Login → **Password dimenticata?**
2. Inserisci email → arriva link valido **1 ora**.
3. Imposta nuova password (min 12 caratteri).

Se hai 2FA attivo, dopo il reset password ti viene chiesto anche il codice TOTP.

---

## 2FA (autenticazione a 2 fattori)

Fortemente raccomandato per admin di workspace.

**Setup**:
1. **Impostazioni** → **Sicurezza** → **Attiva 2FA**.
2. Scansiona il QR con Google Authenticator / Authy / 1Password.
3. Salva i **codici di backup** (10 codici monouso) in un posto sicuro.
4. Inserisci codice di test → 2FA attivo.

**Login con 2FA**:
- Email + password → schermata "Inserisci codice 2FA" → 6 cifre dall'app.
- Codici di backup utilizzabili una sola volta se perdi l'app.

---

## Email e notifiche

**Impostazioni** → **Notifiche** controlla cosa arriva via email:

| Tipo | Default |
|------|---------|
| Login da nuovo dispositivo | ✅ ON (sicurezza, non disattivabile) |
| Inviti a workspace | ✅ ON |
| Riassunto settimanale partite | ❌ OFF |
| Annunci prodotto MeepleAI | ❌ OFF |

---

## Workspace (gruppi di gioco)

Un workspace = un gruppo di persone che condividono catalogo, sessioni, cronologia.

- **Creare**: **Impostazioni** → **Workspace** → **Crea nuovo**. Tu sei admin.
- **Invitare**: link condiviso o invito email (la persona deve creare/avere account).
- **Ruoli**: Admin (gestisce membri/ruoli), Editor (modifica catalogo, carica PDF), User (vede e gioca).

Lasciare un workspace: dalla pagina del workspace → ⋯ → **Esci dal workspace**. I tuoi dati personali restano (libreria privata).

---

## Privacy & GDPR

- **Esporta i tuoi dati**: **Impostazioni** → **Privacy** → **Esporta** → ZIP con libreria + cronologia chat + impostazioni (formato JSON+CSV).
- **Cancella account**: **Impostazioni** → **Privacy** → **Cancella account**. Soft-delete 30 giorni (puoi annullare). Dopo 30 giorni: cancellazione irreversibile di tutti i dati personali. I PDF caricati su workspace condivisi restano nel workspace (non sono "tuoi" privati).
- **Modello AI e dati**: le tue domande chat **non** vengono usate per addestrare modelli di terze parti. Vedi `privacy.md` *(in arrivo)* per dettagli legali.

---

## Limiti noti

- ❌ **No SSO enterprise** (SAML, Okta) in fase Alpha.
- ⚠️ **2FA SMS** non supportato (solo TOTP — più sicuro).
- ⚠️ **Recovery email secondaria** non ancora implementata: se perdi l'app 2FA E i codici di backup, devi contattare support.

---

## Funzioni correlate

- [Catalogo giochi & BGG](./games-bgg.md) — usa il tuo account
- [Libreria personale](./library.md) — collegata all'account

---

**Ultima revisione**: 2026-05-06

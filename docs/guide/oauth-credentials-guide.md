# OAuth Credentials Setup Guide

Guida per ottenere le credenziali OAuth reali per Google, Discord e GitHub.

## ⚠️ Stato Attuale

**Test E2E**: Usano valori **MOCK** in `apps/api/src/Api/.env`
**Produzione**: Richiede credenziali **REALI** da registrare presso ogni provider

---

## 1. Google OAuth (Google Cloud Console)

### Step 1: Crea Progetto
1. Vai a [Google Cloud Console](https://console.cloud.google.com/)
2. Clicca su **Select a project** → **NEW PROJECT**
3. Nome progetto: `MeepleAI` (o nome a tua scelta)
4. Clicca **CREATE**

### Step 2: Abilita OAuth
1. Nel menu laterale: **APIs & Services → Credentials**
2. Clicca **+ CREATE CREDENTIALS → OAuth client ID**
3. Se richiesto, configura **OAuth consent screen**:
   - User Type: **External**
   - App name: `MeepleAI`
   - User support email: tua email
   - Developer contact: tua email
   - Scopes: Add `openid`, `profile`, `email` (già inclusi di default)
   - Test users: Aggiungi la tua email per testing
   - Clicca **SAVE AND CONTINUE**

### Step 3: Crea OAuth Client
1. Torna a **Credentials → + CREATE CREDENTIALS → OAuth client ID**
2. Application type: **Web application**
3. Name: `MeepleAI Web Client`
4. **Authorized JavaScript origins**:
   - Development: `http://localhost:3000`
   - Production: `https://tuodominio.com`
5. **Authorized redirect URIs**:
   - Development: `http://localhost:8080/api/v1/auth/oauth/google/callback`
   - Production: `https://tuodominio.com/api/v1/auth/oauth/google/callback`
6. Clicca **CREATE**
7. **COPIA** il `Client ID` e `Client secret`

### Step 4: Aggiungi al .env
```bash
GOOGLE_OAUTH_CLIENT_ID=123456789-abcdefghijklmnop.apps.googleusercontent.com
GOOGLE_OAUTH_CLIENT_SECRET=GOCSPX-abcdefghijklmnopqrstuvwx
```

---

## 2. Discord OAuth (Discord Developer Portal)

### Step 1: Crea Applicazione
1. Vai a [Discord Developer Portal](https://discord.com/developers/applications)
2. Clicca **New Application**
3. Nome: `MeepleAI`
4. Accetta Terms of Service
5. Clicca **Create**

### Step 2: Configura OAuth2
1. Vai alla sezione **OAuth2** nel menu laterale
2. **CLIENT ID** è già visibile - copialo
3. **CLIENT SECRET**: Clicca **Reset Secret** → **Yes, do it!** → Copia il valore

### Step 3: Aggiungi Redirects
1. Nella sezione **Redirects**:
   - Development: `http://localhost:8080/api/v1/auth/oauth/discord/callback`
   - Production: `https://tuodominio.com/api/v1/auth/oauth/discord/callback`
2. Clicca **Save Changes**

### Step 4: Verifica Scopes
Gli scopes richiesti (`identify`, `email`) sono standard e non richiedono configurazione aggiuntiva.

### Step 5: Aggiungi al .env
```bash
DISCORD_OAUTH_CLIENT_ID=1234567890123456789
DISCORD_OAUTH_CLIENT_SECRET=abcdefghijklmnopqrstuvwxyz123456
```

---

## 3. GitHub OAuth (GitHub Developer Settings)

### Step 1: Crea OAuth App
1. Vai a [GitHub Settings → Developer settings](https://github.com/settings/developers)
2. Clicca **OAuth Apps → New OAuth App**
3. Compila il form:
   - **Application name**: `MeepleAI`
   - **Homepage URL**:
     - Development: `http://localhost:3000`
     - Production: `https://tuodominio.com`
   - **Application description**: `AI-powered board game rules assistant` (opzionale)
   - **Authorization callback URL**:
     - Development: `http://localhost:8080/api/v1/auth/oauth/github/callback`
     - Production: `https://tuodominio.com/api/v1/auth/oauth/github/callback`
4. Clicca **Register application**

### Step 2: Genera Client Secret
1. Copia il **Client ID** (visibile subito)
2. Clicca **Generate a new client secret**
3. **COPIA IMMEDIATAMENTE** il secret (non sarà più visibile!)

### Step 3: Verifica Permissions
Gli scopes richiesti (`read:user`, `user:email`) sono standard OAuth e non richiedono permessi aggiuntivi.

### Step 4: Aggiungi al .env
```bash
GITHUB_OAUTH_CLIENT_ID=Iv1.a1b2c3d4e5f6g7h8
GITHUB_OAUTH_CLIENT_SECRET=a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0
```

---

## ✅ Configurazione Finale

Dopo aver ottenuto tutte le credenziali, il tuo `.env` dovrebbe contenere:

```bash
# apps/api/src/Api/.env

# ... altre variabili ...

# OAuth Credentials (AUTH-06) - REAL VALUES FOR PRODUCTION
GOOGLE_OAUTH_CLIENT_ID=123456789-abcdefghijklmnop.apps.googleusercontent.com
GOOGLE_OAUTH_CLIENT_SECRET=GOCSPX-abcdefghijklmnopqrstuvwx

DISCORD_OAUTH_CLIENT_ID=1234567890123456789
DISCORD_OAUTH_CLIENT_SECRET=abcdefghijklmnopqrstuvwxyz123456

GITHUB_OAUTH_CLIENT_ID=Iv1.a1b2c3d4e5f6g7h8
GITHUB_OAUTH_CLIENT_SECRET=a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6q7r8s9t0
```

---

## 🔒 Sicurezza

### ⚠️ IMPORTANTE
- **MAI committare** `.env` su git (già in `.gitignore`)
- **MAI condividere** client secrets pubblicamente
- **Ruota credenziali** se compromesse
- Usa **variabili d'ambiente** in produzione (Azure Key Vault, AWS Secrets Manager, etc.)

### Ambiente di Sviluppo vs Produzione
- **Development**: Usa `localhost` URLs
- **Production**: Usa HTTPS e domini verificati
- **Callback URLs**: Devono corrispondere ESATTAMENTE (inclusi protocollo e porta)

---

## 🧪 Test dei Provider

### Test Manuale
1. Avvia backend: `cd apps/api/src/Api && dotnet run`
2. Avvia frontend: `cd apps/web && pnpm dev`
3. Vai a `http://localhost:3000/login`
4. Clicca su ogni pulsante OAuth
5. Verifica redirect corretto al provider
6. Completa login sul provider
7. Verifica redirect a MeepleAI con sessione attiva

### Verifica Configurazione
```bash
# Testa endpoint OAuth (deve restituire URL del provider)
curl http://localhost:8080/api/v1/auth/oauth/google/login
curl http://localhost:8080/api/v1/auth/oauth/discord/login
curl http://localhost:8080/api/v1/auth/oauth/github/login
```

---

## 📚 Riferimenti

- **Google OAuth Docs**: https://developers.google.com/identity/protocols/oauth2
- **Discord OAuth Docs**: https://discord.com/developers/docs/topics/oauth2
- **GitHub OAuth Docs**: https://docs.github.com/en/apps/oauth-apps/building-oauth-apps/authorizing-oauth-apps
- **MeepleAI OAuth Docs**: `docs/guide/oauth-setup-guide.md`
- **Security Best Practices**: `docs/security/oauth-security.md`

---

## ❓ FAQ

**Q: I test E2E funzionano con valori mock?**
A: Sì, i test E2E usano mocking e non richiedono OAuth reale.

**Q: Posso usare lo stesso OAuth app per dev e prod?**
A: Sì, ma devi registrare ENTRAMBI i callback URLs.

**Q: Cosa succede se sbaglio il callback URL?**
A: OAuth fallirà con errore "redirect_uri_mismatch". Verifica che coincida esattamente.

**Q: Come ruoto le credenziali compromesse?**
A: Genera nuove credenziali nel developer portal, aggiorna `.env`, riavvia backend.

**Q: Serve HTTPS per development?**
A: No, `http://localhost` è accettato da tutti i provider per development.

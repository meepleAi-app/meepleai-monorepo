# Google Drive Configuration Loader

Script per caricare la configurazione di MeepleAI da Google Drive.

## Metodi Disponibili

### 1. Script Bash con gdown (Semplice)

**Prerequisiti:**
```bash
pip install gdown
```

**Uso:**
```bash
# Con FOLDER_ID passato come argomento
./scripts/gdrive/load-config-from-gdrive.sh 1ABC123xyz_your_folder_id

# Oppure modifica lo script e imposta FOLDER_ID direttamente
```

**Requisiti Google Drive:**
- La cartella deve avere condivisione "Chiunque con il link"
- Non richiede autenticazione OAuth

---

### 2. Script Python con API (Consigliato per team)

**Prerequisiti:**
```bash
pip install google-api-python-client google-auth-oauthlib
```

**Setup OAuth (una volta):**

1. Vai su [Google Cloud Console](https://console.cloud.google.com/apis/credentials)
2. Crea un progetto o seleziona esistente
3. Abilita "Google Drive API" in API & Services > Library
4. Crea credenziali OAuth 2.0:
   - Tipo: Desktop App
   - Nome: MeepleAI Config Loader
5. Scarica il JSON delle credenziali
6. Rinomina il file in `credentials.json` e salvalo nella **root del progetto**

**Uso:**
```bash
# Prima esecuzione: si aprirà il browser per autenticazione
python scripts/gdrive/gdrive_config_loader.py --folder-id 1ABC123xyz

# Esecuzioni successive: usa il token salvato
python scripts/gdrive/gdrive_config_loader.py --folder-id 1ABC123xyz
```

**Vantaggi:**
- Supporta cartelle private
- Autenticazione OAuth sicura
- Token refresh automatico
- Migliore gestione errori

---

## Struttura Cartella Google Drive

Organizza la cartella su Google Drive così:

```
MeepleAI-Config/
├── .env.local              # Variabili ambiente principali
├── .env.staging            # (opzionale) Config staging
└── secrets/
    ├── postgres-password.txt
    ├── redis-password.txt
    ├── openrouter-api-key.txt
    ├── openai-api-key.txt
    ├── n8n-encryption-key.txt
    ├── n8n-basic-auth-password.txt
    ├── initial-admin-password.txt
    ├── grafana-admin-password.txt
    ├── google-oauth-secret.txt
    ├── discord-oauth-secret.txt
    └── github-oauth-secret.txt
```

---

## Come Ottenere il FOLDER_ID

1. Apri la cartella su Google Drive
2. L'URL sarà: `https://drive.google.com/drive/folders/FOLDER_ID_QUI`
3. Copia l'ID dalla URL (la parte dopo `/folders/`)

**Esempio:**
```
URL: https://drive.google.com/drive/folders/1AbCdEfGhIjKlMnOpQrStUvWxYz
FOLDER_ID: 1AbCdEfGhIjKlMnOpQrStUvWxYz
```

---

## Workflow Consigliato

### Setup Iniziale (Nuovo Developer)

```bash
# 1. Clona il repository
git clone <repo-url>
cd meepleai-monorepo-dev

# 2. Ottieni credentials.json dal team lead (per metodo Python)
# Oppure usa metodo bash se la cartella è pubblica

# 3. Carica configurazione
python scripts/gdrive/gdrive_config_loader.py --folder-id <FOLDER_ID>

# 4. Avvia servizi
cd infra && docker compose up -d

# 5. Verifica
docker compose ps
```

### Aggiornamenti Periodici

```bash
# Prima di iniziare a lavorare
git pull origin main-dev
python scripts/gdrive/gdrive_config_loader.py --folder-id <FOLDER_ID>
```

---

## Sicurezza

### File Ignorati da Git

I seguenti file sono in `.gitignore` e NON vengono mai committati:

```
.env.local
.env.staging
.env.production
credentials.json
.gdrive-token.json
infra/secrets/
```

### Best Practice

1. **MAI committare secrets** - Usa sempre Google Drive o altro secret manager
2. **Ruota i secrets periodicamente** - Almeno ogni 90 giorni
3. **Usa cartelle private** - Preferisci il metodo Python con OAuth
4. **Verifica permessi** - I file secrets devono avere permessi 600

---

## Troubleshooting

### "credentials.json non trovato"

```bash
# Verifica che il file sia nella root del progetto
ls -la credentials.json
```

### "FOLDER_ID non valido"

```bash
# Verifica che l'ID sia corretto
# L'ID dovrebbe essere una stringa alfanumerica di ~33 caratteri
```

### "Accesso negato alla cartella"

```bash
# Per metodo bash: la cartella deve essere "Chiunque con il link"
# Per metodo Python: verifica di aver completato l'autenticazione OAuth
```

### "Token expired"

```bash
# Elimina il token e ri-autentica
rm .gdrive-token.json
python scripts/gdrive/gdrive_config_loader.py --folder-id <FOLDER_ID>
```

---

## File Generati

| File | Posizione | Descrizione |
|------|-----------|-------------|
| `credentials.json` | `/` (root) | Credenziali OAuth (da Google Console) |
| `.gdrive-token.json` | `/` (root) | Token di sessione (auto-generato) |
| `.env.local` | `/` (root) | Variabili ambiente (scaricato) |
| `infra/secrets/*.txt` | `/infra/secrets/` | Docker secrets (scaricati) |

---

## Supporto

Per problemi con questi script, contatta il team DevOps o apri una issue su GitHub.

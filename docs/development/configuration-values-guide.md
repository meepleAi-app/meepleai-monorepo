# MeepleAI Configuration Values Guide

**Guida completa ai valori di configurazione per il corretto funzionamento del sistema**

> **Ultimo aggiornamento**: 2026-01-13
> **Versione**: 1.0.0

---

## Indice

1. [Panoramica](#panoramica)
2. [Quick Start - Valori Essenziali](#quick-start---valori-essenziali)
3. [Tabella Completa dei Valori](#tabella-completa-dei-valori)
4. [Configurazione per Ambiente](#configurazione-per-ambiente)
5. [Procedura Caricamento da Google Drive](#procedura-caricamento-da-google-drive)
6. [Secrets Management](#secrets-management)
7. [Troubleshooting](#troubleshooting)

---

## Panoramica

### File di Configurazione Principali

| File | Posizione | Scopo | Ambiente |
|------|-----------|-------|----------|
| `.env.local` | `/` (root) | Variabili locali attive | Dev |
| `.env.development.example` | `/` | Template sviluppo | Dev |
| `.env.staging.example` | `/` | Template staging | Staging |
| `.env.production.example` | `/` | Template produzione | Prod |
| `appsettings.json` | `/apps/api/src/Api/` | Config .NET default | Tutti |
| `appsettings.Production.json` | `/apps/api/src/Api/` | Override produzione | Prod |
| `docker-compose.yml` | `/infra/` | Orchestrazione servizi | Tutti |
| `api.env.dev` | `/infra/env/` | Env API Docker | Dev |
| `web.env.dev` | `/infra/env/` | Env Web Docker | Dev |
| `n8n.env.dev` | `/infra/env/` | Env n8n Docker | Dev |

### Priorità Caricamento Variabili

```
1. Docker Secrets (produzione)
2. File .env locale
3. File env specifico servizio (/infra/env/*.env.dev)
4. appsettings.{Environment}.json
5. appsettings.json (default)
```

---

## Quick Start - Valori Essenziali

### Valori Minimi per Avviare il Sistema

```bash
# Copia il template
cp .env.development.example .env.local

# Modifica SOLO questi valori se necessario:
```

| Variabile | Valore Default | Modifica Richiesta? |
|-----------|----------------|---------------------|
| `POSTGRES_PASSWORD` | `meeplepass` | ⚠️ Cambia in produzione |
| `OPENROUTER_API_KEY` | (vuoto) | ✅ Richiesto per AI |
| `INITIAL_ADMIN_EMAIL` | `admin@meepleai.dev` | Opzionale |
| `INITIAL_ADMIN_PASSWORD` | `Demo123!` | ⚠️ Cambia in produzione |

---

## Tabella Completa dei Valori

### 1. Database PostgreSQL

| Variabile | File Sorgente | Posizione | Esempio | Obbligatorio | Sicurezza |
|-----------|---------------|-----------|---------|--------------|-----------|
| `POSTGRES_USER` | `.env.local` | Riga ~10 | `postgres` | ✅ | 🟢 Basso |
| `POSTGRES_PASSWORD` | `.env.local` | Riga ~11 | `meeplepass` | ✅ | 🔴 Alto |
| `POSTGRES_DB` | `.env.local` | Riga ~12 | `meepleai` | ✅ | 🟢 Basso |
| `POSTGRES_HOST` | `.env.local` | Riga ~13 | `postgres` (Docker) / `localhost` | ✅ | 🟢 Basso |
| `POSTGRES_PORT` | `.env.local` | Riga ~14 | `5432` | ✅ | 🟢 Basso |
| `ConnectionStrings__Postgres` | `appsettings.json` | Sezione `ConnectionStrings` | `Host=postgres;Database=meepleai;Username=postgres;Password=meeplepass;Pooling=true` | ✅ | 🔴 Alto |

**Esempio completo ConnectionString:**
```
Host=postgres;Database=meepleai;Username=postgres;Password=meeplepass;Pooling=true;Minimum Pool Size=2;Maximum Pool Size=20
```

---

### 2. Redis Cache

| Variabile | File Sorgente | Posizione | Esempio | Obbligatorio | Sicurezza |
|-----------|---------------|-----------|---------|--------------|-----------|
| `REDIS_URL` | `.env.local` | Riga ~20 | `redis:6379` | ✅ | 🟢 Basso |
| `REDIS_PASSWORD` | `.env.local` | Riga ~21 | (vuoto in dev) | ❌ Dev / ✅ Prod | 🔴 Alto |
| `HYBRIDCACHE_ENABLE_L2` | `.env.local` | Riga ~22 | `false` (dev) / `true` (prod) | ❌ | 🟢 Basso |

---

### 3. Qdrant Vector Database

| Variabile | File Sorgente | Posizione | Esempio | Obbligatorio | Sicurezza |
|-----------|---------------|-----------|---------|--------------|-----------|
| `QDRANT_URL` | `.env.local` | Riga ~25 | `http://qdrant:6333` | ✅ | 🟢 Basso |
| `QDRANT_API_KEY` | `.env.local` | Riga ~26 | (vuoto in dev) | ❌ Dev / ✅ Prod | 🟡 Medio |

---

### 4. ASP.NET Core API

| Variabile | File Sorgente | Posizione | Esempio | Obbligatorio | Sicurezza |
|-----------|---------------|-----------|---------|--------------|-----------|
| `ASPNETCORE_ENVIRONMENT` | `.env.local` | Riga ~30 | `Development` / `Production` | ✅ | 🟢 Basso |
| `ASPNETCORE_URLS` | `.env.local` | Riga ~31 | `http://+:8080` | ✅ | 🟢 Basso |
| `JWT_ISSUER` | `.env.local` | Riga ~35 | `http://localhost:8080` | ✅ | 🟢 Basso |
| `JWT_AUDIENCE` | `.env.local` | Riga ~36 | `http://localhost:3000` | ✅ | 🟢 Basso |
| `ALLOW_ORIGIN` | `.env.local` | Riga ~37 | `http://localhost:3000` | ✅ | 🟢 Basso |

---

### 5. Next.js Frontend

| Variabile | File Sorgente | Posizione | Esempio | Obbligatorio | Sicurezza |
|-----------|---------------|-----------|---------|--------------|-----------|
| `NODE_ENV` | `.env.local` | Riga ~45 | `development` | ✅ | 🟢 Basso |
| `NEXT_PUBLIC_API_BASE` | `.env.local` | Riga ~46 | `http://localhost:8080` | ✅ | 🟢 Basso |
| `NEXT_PUBLIC_TENANT_ID` | `.env.local` | Riga ~47 | `dev` | ✅ | 🟢 Basso |
| `NEXT_PUBLIC_SITE_URL` | `.env.local` | Riga ~48 | `http://localhost:3000` | ✅ | 🟢 Basso |
| `NEXT_TELEMETRY_DISABLED` | `.env.local` | Riga ~50 | `1` | ❌ | 🟢 Basso |

---

### 6. AI/ML Services - CRITICI

| Variabile | File Sorgente | Posizione | Esempio | Obbligatorio | Sicurezza |
|-----------|---------------|-----------|---------|--------------|-----------|
| `EMBEDDING_PROVIDER` | `.env.local` | Riga ~55 | `ollama` | ✅ | 🟢 Basso |
| `OLLAMA_URL` | `.env.local` | Riga ~56 | `http://ollama:11434` | ✅ | 🟢 Basso |
| `EMBEDDING_MODEL` | `.env.local` | Riga ~57 | `nomic-embed-text` | ✅ | 🟢 Basso |
| `LOCAL_EMBEDDING_URL` | `.env.local` | Riga ~58 | `http://embedding-service:8000` | ✅ | 🟢 Basso |
| **`OPENROUTER_API_KEY`** | `.env.local` | Riga ~60 | `sk-or-v1-xxxxx` | **✅ Per AI Chat** | 🔴 **Alto** |
| `OPENAI_API_KEY` | `.env.local` | Riga ~61 | `sk-xxxxx` | ❌ Opzionale | 🔴 Alto |

**Nota**: `OPENROUTER_API_KEY` è **essenziale** per le funzionalità AI. Ottienilo da [openrouter.ai](https://openrouter.ai)

---

### 7. PDF Processing Services

| Variabile | File Sorgente | Posizione | Esempio | Obbligatorio | Sicurezza |
|-----------|---------------|-----------|---------|--------------|-----------|
| `UNSTRUCTURED_STRATEGY` | `.env.local` | Riga ~70 | `fast` / `hi_res` | ✅ | 🟢 Basso |
| `LANGUAGE` | `.env.local` | Riga ~71 | `ita` / `eng` | ✅ | 🟢 Basso |
| `MAX_FILE_SIZE` | `.env.local` | Riga ~72 | `52428800` (50MB) | ❌ | 🟢 Basso |
| `QUALITY_THRESHOLD` | `.env.local` | Riga ~73 | `0.75` | ❌ | 🟢 Basso |
| `DEVICE` | `.env.local` | Riga ~75 | `cpu` / `cuda` | ✅ | 🟢 Basso |
| `MODEL_NAME` | `.env.local` | Riga ~76 | `docling-project/SmolDocling-256M-preview` | ✅ | 🟢 Basso |

---

### 8. OAuth Authentication

| Variabile | File Sorgente | Posizione | Esempio | Obbligatorio | Sicurezza |
|-----------|---------------|-----------|---------|--------------|-----------|
| `GOOGLE_OAUTH_CLIENT_ID` | `.env.local` | Riga ~85 | `xxxxx.apps.googleusercontent.com` | ❌ Dev / ✅ Prod | 🟡 Medio |
| `GOOGLE_OAUTH_CLIENT_SECRET` | `.env.local` | Riga ~86 | `GOCSPX-xxxxx` | ❌ Dev / ✅ Prod | 🔴 Alto |
| `DISCORD_OAUTH_CLIENT_ID` | `.env.local` | Riga ~88 | `123456789012345678` | ❌ | 🟡 Medio |
| `DISCORD_OAUTH_CLIENT_SECRET` | `.env.local` | Riga ~89 | `xxxxx` | ❌ | 🔴 Alto |
| `GITHUB_OAUTH_CLIENT_ID` | `.env.local` | Riga ~91 | `Iv1.xxxxx` | ❌ | 🟡 Medio |
| `GITHUB_OAUTH_CLIENT_SECRET` | `.env.local` | Riga ~92 | `xxxxx` | ❌ | 🔴 Alto |

---

### 9. Admin & Security

| Variabile | File Sorgente | Posizione | Esempio | Obbligatorio | Sicurezza |
|-----------|---------------|-----------|---------|--------------|-----------|
| `INITIAL_ADMIN_EMAIL` | `.env.local` | Riga ~100 | `admin@meepleai.dev` | ✅ | 🟢 Basso |
| `INITIAL_ADMIN_PASSWORD` | `.env.local` | Riga ~101 | `Demo123!` | ✅ | 🔴 Alto |
| `INITIAL_ADMIN_DISPLAY_NAME` | `.env.local` | Riga ~102 | `Local Admin` | ❌ | 🟢 Basso |
| `SESSION_EXPIRATION_DAYS` | `.env.local` | Riga ~105 | `30` | ❌ | 🟢 Basso |

---

### 10. n8n Workflow Automation

| Variabile | File Sorgente | Posizione | Esempio | Obbligatorio | Sicurezza |
|-----------|---------------|-----------|---------|--------------|-----------|
| `N8N_HOST` | `.env.local` | Riga ~110 | `localhost` | ✅ | 🟢 Basso |
| `N8N_PORT` | `.env.local` | Riga ~111 | `5678` | ✅ | 🟢 Basso |
| `N8N_BASIC_AUTH_USER` | `.env.local` | Riga ~115 | `admin` | ✅ | 🟡 Medio |
| `N8N_BASIC_AUTH_PASSWORD` | `.env.local` | Riga ~116 | `admin` | ✅ | 🔴 Alto |
| `N8N_ENCRYPTION_KEY` | `.env.local` | Riga ~117 | `dev1234567890abcdef...` (64 chars) | ✅ | 🔴 Alto |

---

### 11. Observability & Monitoring

| Variabile | File Sorgente | Posizione | Esempio | Obbligatorio | Sicurezza |
|-----------|---------------|-----------|---------|--------------|-----------|
| `SEQ_URL` | `.env.local` | Riga ~125 | `http://seq:5341` | ❌ | 🟢 Basso |
| `GF_SECURITY_ADMIN_USER` | `.env.local` | Riga ~130 | `admin` | ❌ | 🟡 Medio |
| `GF_SECURITY_ADMIN_PASSWORD` | `.env.local` | Riga ~131 | `admin` | ❌ | 🔴 Alto |
| `LOG_LEVEL` | `.env.local` | Riga ~140 | `Debug` / `Information` / `Warning` | ❌ | 🟢 Basso |

---

### 12. Email Configuration

| Variabile | File Sorgente | Posizione | Esempio | Obbligatorio | Sicurezza |
|-----------|---------------|-----------|---------|--------------|-----------|
| `Email__SmtpHost` | `.env.local` | Riga ~150 | `mailpit` (dev) / `smtp.gmail.com` (prod) | ❌ | 🟢 Basso |
| `Email__SmtpPort` | `.env.local` | Riga ~151 | `1025` (dev) / `587` (prod) | ❌ | 🟢 Basso |
| `Email__EnableSsl` | `.env.local` | Riga ~152 | `false` (dev) / `true` (prod) | ❌ | 🟢 Basso |
| `Email__SmtpUsername` | `.env.local` | Riga ~153 | (vuoto dev) / `user@gmail.com` | ❌ | 🟡 Medio |
| `Email__SmtpPassword` | `.env.local` | Riga ~154 | (vuoto dev) / `app-password` | ❌ | 🔴 Alto |
| `Email__FromAddress` | `.env.local` | Riga ~155 | `noreply@meepleai.dev` | ❌ | 🟢 Basso |

---

### 13. Feature Flags

| Variabile | File Sorgente | Posizione | Esempio | Obbligatorio | Sicurezza |
|-----------|---------------|-----------|---------|--------------|-----------|
| `FEATURE_PROMPT_DATABASE` | `.env.local` | Riga ~165 | `true` | ❌ | 🟢 Basso |
| `FEATURE_STREAMING_RESPONSES` | `.env.local` | Riga ~166 | `true` | ❌ | 🟢 Basso |
| `FEATURE_PDF_UPLOAD` | `.env.local` | Riga ~167 | `true` | ❌ | 🟢 Basso |
| `FEATURE_CHAT_EXPORT` | `.env.local` | Riga ~168 | `true` | ❌ | 🟢 Basso |

---

### 14. External APIs

| Variabile | File Sorgente | Posizione | Esempio | Obbligatorio | Sicurezza |
|-----------|---------------|-----------|---------|--------------|-----------|
| `BGG_API_TOKEN` | `infra/env/api.env.dev` | Riga ~25 | `fbf56d09-385a-43fc-985f-305dbed536c9` | ❌ | 🟡 Medio |
| `SENTRY_DSN` | `.env.local` | Riga ~180 | (vuoto dev) / `https://xxx@sentry.io/xxx` | ❌ | 🟡 Medio |
| `SENTRY_AUTH_TOKEN` | `.env.local` | Riga ~181 | (vuoto dev) | ❌ | 🔴 Alto |

---

## Configurazione per Ambiente

### Development (Locale)

```bash
# 1. Copia template
cp .env.development.example .env.local

# 2. Valori ESSENZIALI da configurare:
OPENROUTER_API_KEY=sk-or-v1-your-key-here  # Da openrouter.ai

# 3. Valori OPZIONALI (lascia default):
# Tutto il resto può rimanere con valori default
```

### Staging

```bash
# 1. Copia template staging
cp .env.staging.example .env.local

# 2. Configura OAuth (richiede app staging separate)
GOOGLE_OAUTH_CLIENT_ID=your-staging-client-id
GOOGLE_OAUTH_CLIENT_SECRET=your-staging-secret

# 3. Configura database staging
POSTGRES_PASSWORD=strong-staging-password

# 4. API Keys
OPENROUTER_API_KEY=sk-or-v1-your-key
```

### Production

```bash
# ⚠️ MAI usare .env.local in produzione!
# Usa Docker Secrets:

# 1. Crea directory secrets
mkdir -p infra/secrets

# 2. Crea file secrets (uno per valore sensibile)
echo "super-secure-password" > infra/secrets/postgres-password.txt
echo "sk-or-v1-production-key" > infra/secrets/openrouter-api-key.txt
# ... altri secrets

# 3. Verifica permessi
chmod 600 infra/secrets/*.txt
```

---

## Procedura Caricamento da Google Drive

### Prerequisiti

1. Account Google con accesso a Google Drive
2. Cartella condivisa con i file di configurazione
3. `gdown` o `rclone` installato

### Metodo 1: Script Automatico con gdown

```bash
#!/bin/bash
# scripts/load-config-from-gdrive.sh

# Installa gdown se non presente
pip install gdown --quiet

# ID della cartella Google Drive (dall'URL)
# https://drive.google.com/drive/folders/FOLDER_ID
FOLDER_ID="your-folder-id-here"

# Directory destinazione
DEST_DIR="./config-from-gdrive"
mkdir -p "$DEST_DIR"

# Scarica tutti i file dalla cartella
gdown --folder "https://drive.google.com/drive/folders/$FOLDER_ID" -O "$DEST_DIR"

# Copia i file nelle posizioni corrette
echo "Copiando file di configurazione..."

# .env.local nella root
if [ -f "$DEST_DIR/.env.local" ]; then
    cp "$DEST_DIR/.env.local" ./.env.local
    echo "✅ .env.local copiato"
fi

# Secrets nella cartella infra/secrets
if [ -d "$DEST_DIR/secrets" ]; then
    mkdir -p ./infra/secrets
    cp "$DEST_DIR/secrets/"*.txt ./infra/secrets/
    chmod 600 ./infra/secrets/*.txt
    echo "✅ Secrets copiati e permessi impostati"
fi

# OAuth credentials
if [ -f "$DEST_DIR/oauth-credentials.json" ]; then
    cp "$DEST_DIR/oauth-credentials.json" ./infra/oauth-credentials.json
    echo "✅ OAuth credentials copiati"
fi

# Cleanup
rm -rf "$DEST_DIR"
echo "✅ Configurazione completata!"
```

### Metodo 2: rclone (Raccomandato per team)

```bash
# 1. Installa rclone
# Windows: winget install rclone
# Mac: brew install rclone
# Linux: sudo apt install rclone

# 2. Configura Google Drive
rclone config
# Scegli: n) New remote
# name> gdrive
# Storage> drive
# Segui il wizard per autenticazione OAuth

# 3. Crea script di sync
cat > scripts/sync-config-from-gdrive.sh << 'EOF'
#!/bin/bash
# Sincronizza configurazione da Google Drive

REMOTE="gdrive"
GDRIVE_PATH="MeepleAI/Config"
LOCAL_PATH="./config-sync"

echo "🔄 Sincronizzando da Google Drive..."

# Sync con rclone
rclone sync "$REMOTE:$GDRIVE_PATH" "$LOCAL_PATH" \
    --progress \
    --exclude "*.bak" \
    --exclude ".DS_Store"

# Applica configurazione
if [ -f "$LOCAL_PATH/.env.local" ]; then
    cp "$LOCAL_PATH/.env.local" ./.env.local
    echo "✅ .env.local aggiornato"
fi

if [ -d "$LOCAL_PATH/secrets" ]; then
    mkdir -p ./infra/secrets
    cp "$LOCAL_PATH/secrets/"*.txt ./infra/secrets/
    chmod 600 ./infra/secrets/*.txt
    echo "✅ Secrets aggiornati"
fi

echo "✅ Sync completato!"
EOF

chmod +x scripts/sync-config-from-gdrive.sh
```

### Metodo 3: Google Drive API (Programmatico)

```python
# scripts/gdrive_config_loader.py
"""
Carica configurazione da Google Drive usando l'API ufficiale.
Richiede: pip install google-api-python-client google-auth-oauthlib
"""

import os
import io
from google.oauth2.credentials import Credentials
from google_auth_oauthlib.flow import InstalledAppFlow
from google.auth.transport.requests import Request
from googleapiclient.discovery import build
from googleapiclient.http import MediaIoBaseDownload

SCOPES = ['https://www.googleapis.com/auth/drive.readonly']
FOLDER_ID = 'your-folder-id-here'  # ID della cartella su Google Drive

def authenticate():
    """Autentica con Google Drive API."""
    creds = None
    if os.path.exists('token.json'):
        creds = Credentials.from_authorized_user_file('token.json', SCOPES)

    if not creds or not creds.valid:
        if creds and creds.expired and creds.refresh_token:
            creds.refresh(Request())
        else:
            flow = InstalledAppFlow.from_client_secrets_file(
                'credentials.json', SCOPES)
            creds = flow.run_local_server(port=0)

        with open('token.json', 'w') as token:
            token.write(creds.to_json())

    return creds

def download_file(service, file_id, file_name, dest_path):
    """Scarica un file da Google Drive."""
    request = service.files().get_media(fileId=file_id)
    fh = io.BytesIO()
    downloader = MediaIoBaseDownload(fh, request)

    done = False
    while not done:
        status, done = downloader.next_chunk()

    dest_file = os.path.join(dest_path, file_name)
    os.makedirs(os.path.dirname(dest_file), exist_ok=True)

    with open(dest_file, 'wb') as f:
        f.write(fh.getvalue())

    print(f"✅ Downloaded: {file_name}")

def list_and_download_folder(service, folder_id, dest_path):
    """Lista e scarica tutti i file da una cartella."""
    query = f"'{folder_id}' in parents and trashed = false"
    results = service.files().list(
        q=query,
        fields="files(id, name, mimeType)"
    ).execute()

    files = results.get('files', [])

    for file in files:
        if file['mimeType'] == 'application/vnd.google-apps.folder':
            # Ricorsione per sottocartelle
            sub_dest = os.path.join(dest_path, file['name'])
            list_and_download_folder(service, file['id'], sub_dest)
        else:
            download_file(service, file['id'], file['name'], dest_path)

def main():
    """Main: scarica tutta la configurazione da Google Drive."""
    creds = authenticate()
    service = build('drive', 'v3', credentials=creds)

    dest_path = './config-from-gdrive'
    print(f"📥 Downloading configuration to {dest_path}...")

    list_and_download_folder(service, FOLDER_ID, dest_path)

    # Copia files nelle posizioni corrette
    env_file = os.path.join(dest_path, '.env.local')
    if os.path.exists(env_file):
        os.rename(env_file, './.env.local')
        print("✅ .env.local installed")

    secrets_dir = os.path.join(dest_path, 'secrets')
    if os.path.exists(secrets_dir):
        os.makedirs('./infra/secrets', exist_ok=True)
        for f in os.listdir(secrets_dir):
            src = os.path.join(secrets_dir, f)
            dst = os.path.join('./infra/secrets', f)
            os.rename(src, dst)
            os.chmod(dst, 0o600)
        print("✅ Secrets installed with secure permissions")

    print("🎉 Configuration loaded successfully!")

if __name__ == '__main__':
    main()
```

### Struttura Consigliata su Google Drive

```
Google Drive/
└── MeepleAI/
    └── Config/
        ├── .env.local                    # Variabili ambiente
        ├── .env.staging                  # Config staging (opzionale)
        ├── secrets/
        │   ├── postgres-password.txt
        │   ├── redis-password.txt
        │   ├── openrouter-api-key.txt
        │   ├── openai-api-key.txt
        │   ├── n8n-encryption-key.txt
        │   ├── google-oauth-secret.txt
        │   ├── discord-oauth-secret.txt
        │   ├── github-oauth-secret.txt
        │   └── grafana-admin-password.txt
        └── README.md                     # Istruzioni team
```

### Workflow Consigliato per Team

```bash
# 1. Prima configurazione (una volta)
./scripts/load-config-from-gdrive.sh

# 2. Aggiornamenti periodici
./scripts/sync-config-from-gdrive.sh

# 3. Prima di ogni sviluppo
git pull origin main-dev
./scripts/sync-config-from-gdrive.sh
docker compose up -d
```

---

## Secrets Management

### Produzione: Docker Secrets

```yaml
# docker-compose.yml (estratto)
services:
  api:
    secrets:
      - postgres-password
      - openrouter-api-key
    environment:
      - POSTGRES_PASSWORD_FILE=/run/secrets/postgres-password
      - OPENROUTER_API_KEY_FILE=/run/secrets/openrouter-api-key

secrets:
  postgres-password:
    file: ./infra/secrets/postgres-password.txt
  openrouter-api-key:
    file: ./infra/secrets/openrouter-api-key.txt
```

### Script Inizializzazione Secrets

```bash
#!/bin/bash
# infra/init-secrets.sh

SECRETS_DIR="./infra/secrets"
mkdir -p "$SECRETS_DIR"

echo "🔐 Inizializzazione secrets..."

# Lista secrets richiesti
SECRETS=(
    "postgres-password"
    "redis-password"
    "openrouter-api-key"
    "n8n-encryption-key"
    "n8n-basic-auth-password"
    "initial-admin-password"
    "grafana-admin-password"
)

for secret in "${SECRETS[@]}"; do
    FILE="$SECRETS_DIR/$secret.txt"
    if [ ! -f "$FILE" ]; then
        echo "⚠️  Secret mancante: $secret"
        read -sp "Inserisci valore per $secret: " value
        echo
        echo "$value" > "$FILE"
        chmod 600 "$FILE"
        echo "✅ $secret creato"
    else
        echo "✓ $secret già presente"
    fi
done

echo "🎉 Secrets inizializzati!"
```

---

## Troubleshooting

### Problema: "Connection refused" al database

```bash
# Verifica che PostgreSQL sia attivo
docker compose ps postgres

# Controlla logs
docker compose logs postgres

# Verifica connection string
echo $ConnectionStrings__Postgres
# Deve corrispondere a: Host=postgres;Database=meepleai;...
```

### Problema: AI non risponde

```bash
# Verifica OPENROUTER_API_KEY
grep OPENROUTER_API_KEY .env.local
# Deve contenere: sk-or-v1-xxxxx

# Testa connessione
curl -H "Authorization: Bearer $(grep OPENROUTER_API_KEY .env.local | cut -d= -f2)" \
     https://openrouter.ai/api/v1/models
```

### Problema: OAuth non funziona

```bash
# Verifica callback URLs configurati nel provider OAuth
# Google: https://console.cloud.google.com/apis/credentials
# Discord: https://discord.com/developers/applications
# GitHub: https://github.com/settings/applications

# Callback URL deve essere:
# Dev: http://localhost:8080/api/v1/auth/callback/{provider}
# Prod: https://yourdomain.com/api/v1/auth/callback/{provider}
```

### Problema: Secrets non caricati

```bash
# Verifica permessi
ls -la infra/secrets/

# Devono essere 600 (-rw--------)
# Se no:
chmod 600 infra/secrets/*.txt

# Verifica che Docker li legga
docker compose config | grep -A5 secrets
```

---

## Checklist Pre-Avvio

- [ ] `.env.local` creato dalla copia del template
- [ ] `OPENROUTER_API_KEY` configurato (obbligatorio per AI)
- [ ] `POSTGRES_PASSWORD` cambiato (se produzione)
- [ ] `INITIAL_ADMIN_PASSWORD` cambiato (se produzione)
- [ ] Secrets creati in `infra/secrets/` (se produzione)
- [ ] OAuth app create e configurate (se autenticazione social richiesta)
- [ ] Docker Desktop avviato
- [ ] `docker compose up -d` eseguito senza errori

---

## Riferimenti

- [OpenRouter API Keys](https://openrouter.ai/keys)
- [Google OAuth Console](https://console.cloud.google.com/apis/credentials)
- [Discord Developer Portal](https://discord.com/developers/applications)
- [GitHub OAuth Apps](https://github.com/settings/developers)
- [Docker Secrets Documentation](https://docs.docker.com/compose/use-secrets/)

---

**Maintainer**: MeepleAI Team
**Licenza**: Proprietario

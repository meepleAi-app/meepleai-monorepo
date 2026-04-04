# MeepleAI — Guida Completa Deploy Staging

> Da zero a "serata giochi con amici" in 3 settimane.
> Budget: 30 EUR/mese (20 infra + 10 LLM)

---

## Indice

1. [Acquisto VPS Hetzner](#1-acquisto-vps-hetzner)
2. [Registrazione Dominio](#2-registrazione-dominio)
3. [Setup Server](#3-setup-server)
4. [Configurazione DNS e SSL](#4-configurazione-dns-e-ssl)
5. [GitHub Secrets e CI/CD](#5-github-secrets-e-cicd)
6. [Deploy Secrets sul Server](#6-deploy-secrets-sul-server)
7. [Primo Deploy (Push main-staging)](#7-primo-deploy)
8. [Setup LLM (OpenRouter)](#8-setup-llm-openrouter)
9. [Upload PDF e Creazione Agenti](#9-upload-pdf-e-creazione-agenti)
10. [Verifica e Troubleshooting](#10-verifica-e-troubleshooting)

---

## 1. Acquisto VPS Hetzner

### Perche Hetzner
- Server ARM64 a basso costo (ideale per Docker multi-arch)
- Datacenter EU (GDPR compliant)
- Il nostro CI/CD runner e gia ARM64

### Procedura

1. **Vai su** [hetzner.com/cloud](https://www.hetzner.com/cloud/)
2. **Crea account**: email + carta/PayPal (serve per verifica)
3. **Nuovo progetto**: "MeepleAI"
4. **Crea server**:

| Parametro | Valore |
|-----------|--------|
| Location | Falkenstein (fsn1) o Nuremberg (nbg1) |
| Image | **Ubuntu 24.04** |
| Type | **CAX21** (ARM64, 4 vCPU, 8GB RAM, 80GB SSD) |
| Networking | Public IPv4 + IPv6 |
| SSH Key | **Aggiungi la tua chiave pubblica** (vedi sotto) |
| Nome | `meepleai-staging` |

5. **Costo**: 7,49 EUR/mese (fatturato ad ore se serve meno)

### Genera SSH Key (se non ne hai una)

```bash
# Sul tuo PC locale
ssh-keygen -t ed25519 -C "meepleai-deploy" -f ~/.ssh/meepleai-staging

# Mostra la chiave pubblica (da incollare in Hetzner)
cat ~/.ssh/meepleai-staging.pub

# Mostra la chiave privata (servira per GitHub Secrets)
cat ~/.ssh/meepleai-staging
```

### Primo Accesso

```bash
# Connettiti al server (usa l'IP che Hetzner ti assegna)
ssh -i ~/.ssh/meepleai-staging root@<IP_SERVER>

# Verifica architettura
uname -m  # Deve mostrare: aarch64
```

---

## 2. Registrazione Dominio

### Opzioni Consigliate

| Registrar | Prezzo .com/anno | DNS incluso | Note |
|-----------|-----------------|-------------|------|
| **Porkbun** | ~9-10 EUR | Si | Economico, interfaccia pulita |
| **Cloudflare** | ~10 EUR | Si + CDN | DNS + proxy + DDoS gratis |
| **Namecheap** | ~10-12 EUR | Si | Popolare, WHOIS privacy gratis |

### Procedura (esempio Cloudflare)

1. **Vai su** [dash.cloudflare.com](https://dash.cloudflare.com)
2. **Crea account** (gratuito)
3. **Register Domain** > cerca il nome desiderato (es. `meepleai.com`)
4. **Acquista** (~10 EUR/anno = ~0,83 EUR/mese)
5. **DNS sara gestito da Cloudflare** automaticamente

### Se usi un registrar diverso da Cloudflare

1. Registra il dominio sul registrar scelto
2. Vai su Cloudflare > **Add Site** > inserisci il dominio
3. Cloudflare ti dara 2 nameserver (es. `anna.ns.cloudflare.com`)
4. Nel registrar, cambia i nameserver con quelli di Cloudflare
5. Attendi propagazione (5-30 minuti)

---

## 3. Setup Server

### Script Setup Completo

Connettiti al server e esegui questi comandi:

```bash
ssh -i ~/.ssh/meepleai-staging root@<IP_SERVER>
```

#### 3.1 Aggiornamento e sicurezza base

```bash
# Aggiorna tutto
apt update && apt upgrade -y

# Installa utilita essenziali
apt install -y curl wget git ufw fail2ban htop

# Crea utente non-root per il deploy
adduser --disabled-password --gecos "MeepleAI Deploy" deploy
usermod -aG sudo deploy
echo "deploy ALL=(ALL) NOPASSWD:ALL" >> /etc/sudoers.d/deploy

# Copia SSH key per l'utente deploy
mkdir -p /home/deploy/.ssh
cp /root/.ssh/authorized_keys /home/deploy/.ssh/
chown -R deploy:deploy /home/deploy/.ssh
chmod 700 /home/deploy/.ssh
chmod 600 /home/deploy/.ssh/authorized_keys
```

#### 3.2 Firewall

```bash
# Configura UFW
ufw default deny incoming
ufw default allow outgoing
ufw allow ssh          # Porta 22
ufw allow http         # Porta 80  (redirect a HTTPS)
ufw allow https        # Porta 443 (Traefik)
ufw --force enable

# Verifica
ufw status
```

#### 3.3 Docker

```bash
# Installa Docker (script ufficiale)
curl -fsSL https://get.docker.com | sh

# Aggiungi utente deploy al gruppo docker
usermod -aG docker deploy

# Verifica
docker --version        # Docker 27.x+
docker compose version  # v2.x+

# Verifica architettura
docker info | grep Architecture  # aarch64
```

#### 3.4 Struttura directory

```bash
# Crea directory progetto
mkdir -p /opt/meepleai/secrets
mkdir -p /opt/meepleai/traefik/letsencrypt
mkdir -p /opt/meepleai/traefik/logs
chown -R deploy:deploy /opt/meepleai

# Permessi per Let's Encrypt
chmod 600 /opt/meepleai/traefik/letsencrypt
```

#### 3.5 Swap (consigliato per 8GB RAM)

```bash
# Crea 4GB di swap
fallocate -l 4G /swapfile
chmod 600 /swapfile
mkswap /swapfile
swapon /swapfile

# Rendi persistente
echo '/swapfile swap swap defaults 0 0' >> /etc/fstab

# Ottimizza swappiness
echo 'vm.swappiness=10' >> /etc/sysctl.conf
sysctl -p

# Verifica
free -h  # Deve mostrare ~8GB RAM + 4GB Swap
```

#### 3.6 Login a GitHub Container Registry

```bash
# Da utente deploy
su - deploy

# Login a GHCR (serve un Personal Access Token con scope read:packages)
echo "<GITHUB_PAT>" | docker login ghcr.io -u <GITHUB_USERNAME> --password-stdin
```

> **Come creare il PAT**: GitHub > Settings > Developer settings > Personal access tokens >
> Generate new token > Seleziona `read:packages` > Copia il token

---

## 4. Configurazione DNS e SSL

### Record DNS (Cloudflare)

Vai su Cloudflare > DNS Records e aggiungi:

| Type | Name | Content | Proxy | TTL |
|------|------|---------|-------|-----|
| A | `@` | `<IP_SERVER>` | Proxied | Auto |
| A | `www` | `<IP_SERVER>` | Proxied | Auto |
| A | `api` | `<IP_SERVER>` | Proxied | Auto |
| A | `grafana` | `<IP_SERVER>` | DNS only | Auto |

> **Nota**: "Proxied" attiva il CDN/DDoS protection di Cloudflare (gratis).
> Per Grafana usa "DNS only" per evitare conflitti WebSocket.

### Cloudflare SSL Settings

1. **SSL/TLS > Overview**: Seleziona **Full (strict)**
2. **SSL/TLS > Edge Certificates**: Abilita "Always Use HTTPS"
3. **SSL/TLS > Edge Certificates**: Abilita "Automatic HTTPS Rewrites"

### Traefik SSL (Let's Encrypt)

Il file `compose.traefik.yml` nel repo e gia configurato. Sul server dovrai solo:

```bash
# Crea il file di configurazione Traefik
cat > /opt/meepleai/traefik.yml << 'EOF'
api:
  dashboard: true
  insecure: false

entryPoints:
  web:
    address: ":80"
    http:
      redirections:
        entryPoint:
          to: websecure
          scheme: https
  websecure:
    address: ":443"

certificatesResolvers:
  letsencrypt:
    acme:
      email: tua-email@esempio.com
      storage: /letsencrypt/acme.json
      httpChallenge:
        entryPoint: web

providers:
  docker:
    exposedByDefault: false
    network: meepleai
EOF
```

---

## 5. GitHub Secrets e CI/CD

### Secrets da Configurare

Vai su GitHub > Repository > Settings > Secrets and variables > Actions

#### Repository Secrets

| Secret | Valore | Dove trovarlo |
|--------|--------|---------------|
| `STAGING_HOST` | `<IP_SERVER>` | Dashboard Hetzner |
| `STAGING_USER` | `deploy` | Creato nel setup server |
| `STAGING_SSH_KEY` | Contenuto di `~/.ssh/meepleai-staging` | La chiave privata generata prima |

#### Repository Variables

| Variable | Valore | Note |
|----------|--------|------|
| `DEPLOY_METHOD` | `ssh` | Il nostro metodo di deploy |
| `RUNNER` | `self-hosted,linux,ARM64` | Gia configurato |

### Come Aggiungere

```
GitHub > Settings > Secrets and variables > Actions

Tab "Secrets" > "New repository secret"
  Name: STAGING_HOST
  Value: 123.456.789.0

  Name: STAGING_USER
  Value: deploy

  Name: STAGING_SSH_KEY
  Value: (incolla tutto il contenuto della chiave privata, inclusi BEGIN/END)

Tab "Variables" > "New repository variable"
  Name: DEPLOY_METHOD
  Value: ssh
```

---

## 6. Deploy Secrets sul Server

I container hanno bisogno dei file `.secret` per funzionare. Copia quelli dal tuo PC locale.

### Metodo 1: SCP (consigliato)

```bash
# Dal tuo PC locale, copia tutti i .secret
scp -i ~/.ssh/meepleai-staging \
  infra/secrets/*.secret \
  deploy@<IP_SERVER>:/opt/meepleai/secrets/
```

### Metodo 2: Crea manualmente sul server

```bash
ssh -i ~/.ssh/meepleai-staging deploy@<IP_SERVER>

# Database
cat > /opt/meepleai/secrets/database.secret << 'EOF'
POSTGRES_PASSWORD=<genera-password-sicura>
POSTGRES_USER=meeple
POSTGRES_DB=meepleai_staging
EOF

# Redis
cat > /opt/meepleai/secrets/redis.secret << 'EOF'
REDIS_PASSWORD=<genera-password-sicura>
EOF

# Qdrant
cat > /opt/meepleai/secrets/qdrant.secret << 'EOF'
QDRANT_API_KEY=<genera-api-key>
EOF

# JWT
cat > /opt/meepleai/secrets/jwt.secret << 'EOF'
JWT_SECRET=<genera-stringa-64-caratteri>
JWT_ISSUER=meepleai-staging
JWT_AUDIENCE=meepleai-users
JWT_EXPIRY_HOURS=24
EOF

# Admin
cat > /opt/meepleai/secrets/admin.secret << 'EOF'
ADMIN_EMAIL=tua-email@esempio.com
ADMIN_PASSWORD=<password-sicura>
ADMIN_USERNAME=admin
EOF

# OpenRouter (compilare dopo registrazione)
cat > /opt/meepleai/secrets/openrouter.secret << 'EOF'
OPENROUTER_API_KEY=sk-or-v1-xxxxx
EOF

# Embedding service
cat > /opt/meepleai/secrets/embedding-service.secret << 'EOF'
EMBEDDING_MODEL=all-MiniLM-L6-v2
EMBEDDING_PORT=8000
EOF

# Monitoring (opzionale)
cat > /opt/meepleai/secrets/monitoring.secret << 'EOF'
GF_SECURITY_ADMIN_PASSWORD=<password-grafana>
GF_SECURITY_ADMIN_USER=admin
EOF

# Proteggi i file
chmod 600 /opt/meepleai/secrets/*.secret
```

### Genera Password Sicure

```bash
# Genera password casuali
openssl rand -base64 32  # Per database, redis
openssl rand -hex 32     # Per JWT secret
openssl rand -hex 16     # Per API keys
```

---

## 7. Primo Deploy

### Opzione A: Deploy Automatico (CI/CD)

```bash
# Dal tuo PC locale
cd D:\Repositories\meepleai-monorepo-backend

# Push main-staging (triggera deploy-staging.yml)
git push origin main-staging
```

Il workflow GitHub Actions:
1. Esegue i test
2. Builda le immagini Docker (API + Web)
3. Pusha su ghcr.io
4. Si connette via SSH al server
5. Fa pull delle immagini
6. Avvia i container
7. Esegue migrazioni DB
8. Health check

**Monitora il deploy**: GitHub > Actions > "Deploy to Staging"

### Opzione B: Deploy Manuale (prima volta / debug)

Se il CI/CD non funziona ancora, puoi deployare manualmente:

```bash
ssh -i ~/.ssh/meepleai-staging deploy@<IP_SERVER>
cd /opt/meepleai

# Clona il repository
git clone https://github.com/meepleAi-app/meepleai-monorepo.git .
git checkout main-staging

# Copia i secret files nella directory infra
cp /opt/meepleai/secrets/*.secret infra/secrets/

# Crea la rete Docker
docker network create meepleai || true

# Avvia servizi core (senza AI, per testare)
cd infra
docker compose -f docker-compose.yml -f compose.staging.yml \
  --profile minimal up -d

# Verifica che partano
docker compose ps

# Se tutto OK, aggiungi i servizi AI
docker compose -f docker-compose.yml -f compose.staging.yml \
  --profile ai up -d

# Esegui le migrazioni DB
docker compose exec api dotnet ef database update --no-build

# Health check
curl -sf http://localhost:8080/health && echo "API OK"
curl -sf http://localhost:3000 && echo "WEB OK"
```

### Post-Deploy Checklist

```
[ ] PostgreSQL attivo e migrazioni applicate
[ ] Redis attivo e connesso
[ ] Qdrant attivo e raggiungibile
[ ] API risponde su /health
[ ] Web carica la homepage
[ ] Login/Registrazione funziona
[ ] HTTPS attivo (se DNS configurato)
```

---

## 8. Setup LLM (OpenRouter)

### Registrazione

1. **Vai su** [openrouter.ai](https://openrouter.ai)
2. **Sign up** con Google/GitHub
3. **Keys** > **Create Key** > copia la chiave `sk-or-v1-...`
4. **Credits** > **Add Credits** > aggiungi 10 EUR

### Configura sul Server

```bash
ssh -i ~/.ssh/meepleai-staging deploy@<IP_SERVER>

# Aggiorna il secret
cat > /opt/meepleai/secrets/openrouter.secret << 'EOF'
OPENROUTER_API_KEY=sk-or-v1-LA-TUA-CHIAVE-QUI
EOF

# Riavvia l'API per caricare il nuovo secret
cd /opt/meepleai/infra
docker compose -f docker-compose.yml -f compose.staging.yml restart api
```

### Modelli Consigliati (configurabili dall'admin panel)

| Uso | Modello | Costo | Qualita |
|-----|---------|-------|---------|
| **Default** | `openai/gpt-4o-mini` | Bassissimo | Buona |
| **Qualita** | `anthropic/claude-3.5-haiku` | Basso | Ottima |
| **Economico** | `meta-llama/llama-3.1-8b-instruct` | Quasi zero | Sufficiente |

### Verifica LLM Funzionante

```bash
# Test rapido dall'API
curl -sf http://localhost:8080/api/v1/health/llm | jq .
```

---

## 9. Upload PDF e Creazione Agenti

### Prerequisiti

- [ ] API funzionante
- [ ] Embedding service attivo
- [ ] Qdrant attivo
- [ ] OpenRouter configurato
- [ ] Account admin creato

### Step 1: Login come Admin

1. Apri `https://tuodominio.com` (o `http://<IP>:3000` in locale)
2. Registra un account o usa le credenziali admin dal secret
3. Vai su **Admin Panel** (`/admin`)

### Step 2: Aggiungi un Gioco

1. Admin > **Catalogo Giochi** > **Aggiungi Gioco**
2. Compila: Nome, Editore, Num. Giocatori, Durata
3. Salva

### Step 3: Upload PDF del Regolamento

1. Vai nella pagina del gioco > **Knowledge Base** > **Upload PDF**
2. Seleziona il PDF del regolamento
3. Attendi il processing: `Uploading → Extracting → Chunking → Indexing`
4. Stato finale: **Indexed** (verde)

> **Tempo stimato**: 2-5 minuti per PDF, dipende dalla dimensione e complessita.
> PDF con OCR (scansioni) richiedono piu tempo.

### Step 4: Crea un Agente AI

1. Admin > **Agenti** > **Nuovo Agente**
2. Configura:
   - **Nome**: "Assistente [Nome Gioco]"
   - **Gioco associato**: Seleziona il gioco
   - **Modello LLM**: `openai/gpt-4o-mini` (o altro)
   - **System Prompt**: Lascia il default o personalizza
3. Salva
4. **Testa**: Apri la chat dell'agente e chiedi una regola

### Step 5: Prepara la Serata

1. Vai su **Game Night** > **Nuova Playlist**
2. Aggiungi i giochi della serata
3. Per ogni gioco verifica che l'agente risponda correttamente
4. Condividi il link della playlist con gli amici

### Giochi da Preparare (esempio)

| Gioco | PDF | Pagine | Tempo Processing |
|-------|-----|--------|-----------------|
| Catan | Regolamento base | ~20 | ~3 min |
| 7 Wonders | Regole + espansioni | ~30 | ~5 min |
| Azul | Regolamento | ~8 | ~1 min |
| Wingspan | Regole + appendice | ~25 | ~4 min |

---

## 10. Verifica e Troubleshooting

### Health Checks

```bash
# Dal server
curl -sf http://localhost:8080/health         # API
curl -sf http://localhost:3000                 # Web
curl -sf http://localhost:6333/healthz         # Qdrant
docker compose exec redis redis-cli ping      # Redis
docker compose exec postgres pg_isready       # PostgreSQL
```

### Problemi Comuni

| Problema | Causa | Soluzione |
|----------|-------|-----------|
| API non parte | Secret mancanti | Verifica `docker compose logs api` |
| DB connection refused | PostgreSQL non pronto | Attendi 30s, verifica `docker compose logs postgres` |
| Embedding timeout | Modello non scaricato | Prima run scarica ~100MB, attendi |
| PDF stuck in "Extracting" | Container Python non attivo | `docker compose logs embedding-service` |
| 502 Bad Gateway | Container non healthy | `docker compose ps` per verificare stato |
| SSL non funziona | DNS non propagato | `nslookup tuodominio.com` per verificare |
| Out of memory | Troppi servizi | Usa profilo `minimal` + `ai` (no monitoring) |
| Login non funziona | Migrazioni non applicate | `docker compose exec api dotnet ef database update` |

### Comandi Utili

```bash
# Stato servizi
docker compose -f docker-compose.yml -f compose.staging.yml ps

# Log di un servizio
docker compose -f docker-compose.yml -f compose.staging.yml logs -f api

# Riavvio singolo servizio
docker compose -f docker-compose.yml -f compose.staging.yml restart api

# Ricostruisci e riavvia
docker compose -f docker-compose.yml -f compose.staging.yml up -d --force-recreate api

# Spazio disco
df -h /

# Risorse in uso
docker stats --no-stream

# Backup database
docker compose exec postgres pg_dump -U meeple meepleai_staging > backup.sql
```

### Monitoraggio

- **Grafana**: `https://grafana.tuodominio.com` (admin / password dal secret)
- **Prometheus**: `http://<IP>:9090` (solo locale, non esposto)
- **API Metrics**: `https://tuodominio.com/api/v1/health`

---

## Riepilogo Budget Mensile

| Voce | Costo |
|------|-------|
| Hetzner CAX21 | 7,49 EUR |
| Dominio (.com/anno amortizzato) | ~0,90 EUR |
| OpenRouter (pay-per-use) | ~5-10 EUR |
| Cloudflare DNS/CDN | GRATIS |
| SSL (Let's Encrypt) | GRATIS |
| Embedding + Reranker | GRATIS (self-hosted) |
| **TOTALE** | **~14-18 EUR / 30 EUR budget** |

---

## Prossimi Passi dopo il Deploy

1. **Test con amici** — Fai provare l'app a 1-2 amici per feedback
2. **Ottimizza** — Monitora latenza risposte AI, regola modelli se necessario
3. **Backup automatico** — Configura cron job per backup DB giornaliero
4. **Custom domain email** — Configura email per notifiche (opzionale)
5. **Serata giochi!** — Prepara playlist, carica PDF, invita gli amici

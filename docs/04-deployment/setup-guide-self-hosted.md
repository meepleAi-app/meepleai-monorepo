# MeepleAI Setup Guide - Full Self-Hosted

**Target**: Staging + Produzione per Beta Testing (5-20 utenti)
**Budget**: ~€25-30/mese
**Approccio**: Completamente self-hosted su singolo VPS
**Regione**: EU (Hetzner Falkenstein/Helsinki)

---

## Panoramica Architettura

```
                         ┌─────────────────┐
                         │   Cloudflare    │ DNS + CDN + SSL
                         │      €0         │
                         └────────┬────────┘
                                  │
               ┌──────────────────┼──────────────────┐
               │                  │                  │
      ┌────────▼────────┐ ┌───────▼───────┐ ┌───────▼───────┐
      │ meepleai.com    │ │ api.meepleai  │ │ staging.*     │
      │   (frontend)    │ │    .com       │ │               │
      └────────┬────────┘ └───────┬───────┘ └───────┬───────┘
               │                  │                  │
               └──────────────────┼──────────────────┘
                                  │
                         ┌────────▼────────┐
                         │  Hetzner VPS    │
                         │  CPX31: €15.59  │
                         │  8 vCPU, 16GB   │
                         │  160GB NVMe     │
                         └────────┬────────┘
                                  │
                         ┌────────▼────────┐
                         │    Traefik      │ Reverse Proxy + SSL
                         └────────┬────────┘
                                  │
        ┌─────────────────────────┼─────────────────────────┐
        │           │             │             │           │
   ┌────▼────┐ ┌────▼────┐ ┌──────▼──────┐ ┌────▼────┐ ┌────▼────┐
   │ .NET    │ │ Python  │ │  PostgreSQL │ │  Redis  │ │ Qdrant  │
   │  API    │ │Services │ │     16      │ │    7    │ │ Vector  │
   │         │ │Embedding│ │  + pgvector │ │         │ │   DB    │
   │         │ │Reranker │ │             │ │         │ │         │
   └─────────┘ └─────────┘ └─────────────┘ └─────────┘ └─────────┘
```

---

## Costi Dettagliati

| Servizio | Provider | Descrizione | Costo/mese |
|----------|----------|-------------|------------|
| **VPS** | Hetzner | CPX31 (8 vCPU, 16GB RAM, 160GB NVMe) | €15.59 |
| **Backup VPS** | Hetzner | Snapshots automatici 7 giorni | €3.08 |
| **Dominio** | Porkbun | meepleai.com (€10/anno) | ~€0.83 |
| **DNS/CDN/SSL** | Cloudflare | Free tier | €0 |
| **Object Storage** | Cloudflare R2 | 10GB free, poi $0.015/GB | ~€0-5 |
| **Email** | Resend | 3000 email/mese free | €0 |
| **PostgreSQL** | Self-hosted | Container Docker | €0 |
| **Redis** | Self-hosted | Container Docker | €0 |
| **Qdrant** | Self-hosted | Container Docker | €0 |
| **TOTALE** | | | **~€20-25/mese** |

**Costi una tantum**:
- Dominio primo anno: ~€10

---

## Requisiti Risorse VPS

### Allocazione RAM (16GB totali)

| Servizio | RAM Min | RAM Max | Note |
|----------|---------|---------|------|
| PostgreSQL | 512MB | 2GB | Shared buffers 512MB |
| Redis | 128MB | 512MB | Maxmemory 256MB |
| Qdrant | 512MB | 2GB | Per ~50K embeddings |
| .NET API | 512MB | 2GB | Con GC ottimizzato |
| Embedding Service | 1GB | 3GB | sentence-transformers |
| Reranker Service | 512MB | 2GB | cross-encoder |
| Traefik | 64MB | 256MB | Reverse proxy |
| Next.js Frontend | 256MB | 1GB | Se self-hosted |
| Sistema Ubuntu | 512MB | 1GB | OS + buffer |
| **Buffer libero** | 2GB | 4GB | Per picchi |
| **TOTALE** | ~6GB | ~16GB | ✅ Rientra nel CPX31 |

### Allocazione Storage (160GB NVMe)

| Uso | Stima | Note |
|-----|-------|------|
| Sistema Ubuntu | 5GB | OS base |
| Docker images | 15GB | Tutti i container |
| PostgreSQL data | 5-20GB | Dipende da utenti |
| Qdrant vectors | 1-5GB | ~200 manuali |
| Redis data | 100MB | Cache |
| PDF uploads | 10-50GB | Manuali caricati |
| Backups locali | 20GB | 7 giorni retention |
| Logs | 5GB | Con rotation |
| **Libero** | 50-100GB | Margine crescita |

---

## Fase 1: Acquisti e Account (Day 1)

### 1.1 Dominio - Porkbun

1. Vai su https://porkbun.com
2. Cerca `meepleai.com`
3. Se disponibile: aggiungi al carrello (~$10.88/anno)
4. Crea account e completa acquisto
5. Abilita:
   - [x] WHOIS privacy (gratuito)
   - [x] Auto-rinnovo

**Se non disponibile**, alternative:
- `meepleai.app` (~$15/anno)
- `getmeepleai.com` (~$10/anno)
- `playmeeple.com` (~$10/anno)

### 1.2 Cloudflare - DNS e CDN

1. Vai su https://dash.cloudflare.com/sign-up
2. Crea account gratuito
3. Aggiungi sito: `meepleai.com`
4. Cloudflare fornirà 2 nameserver, es:
   ```
   ada.ns.cloudflare.com
   bob.ns.cloudflare.com
   ```
5. Vai su Porkbun → Domain Management → meepleai.com → Nameservers
6. Sostituisci nameserver Porkbun con quelli Cloudflare
7. Attendi propagazione (5 min - 48h, solitamente 30 min)

**Verifica propagazione**:
```bash
dig NS meepleai.com
# Deve mostrare i nameserver Cloudflare
```

### 1.3 Hetzner Cloud - VPS

1. Vai su https://console.hetzner.cloud/register
2. Crea account, verifica email
3. Aggiungi metodo pagamento (carta/PayPal)
4. Crea progetto: `MeepleAI`

---

## Fase 2: Provisioning VPS (Day 1)

### 2.1 Crea Server

Hetzner Console → Servers → Add Server:

| Impostazione | Valore |
|--------------|--------|
| **Location** | Falkenstein (fsn1) o Helsinki (hel1) |
| **Image** | Ubuntu 24.04 LTS |
| **Type** | CPX31 (8 vCPU AMD, 16GB RAM, 160GB NVMe) |
| **Networking** | Public IPv4 + IPv6 |
| **SSH Key** | Carica la tua chiave pubblica |
| **Name** | `meepleai-prod-01` |
| **Labels** | `env=production`, `project=meepleai` |
| **Backups** | ✅ Abilita (€3.08/mese) |

Click **Create & Buy Now**

**Copia l'IP**: `___.___.___.___`

### 2.2 Primo Accesso e Setup Base

```bash
# SSH nel server
ssh root@YOUR_IP_ADDRESS

# Aggiorna sistema
apt update && apt upgrade -y

# Installa strumenti essenziali
apt install -y \
    git curl wget nano htop \
    ufw fail2ban \
    jq unzip \
    ca-certificates gnupg

# Configura timezone
timedatectl set-timezone Europe/Rome

# Configura firewall
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp comment 'SSH'
ufw allow 80/tcp comment 'HTTP'
ufw allow 443/tcp comment 'HTTPS'
ufw --force enable
ufw status verbose
```

### 2.3 Crea Utente Non-Root

```bash
# Crea utente
adduser meepleai
# Inserisci password sicura quando richiesto

# Aggiungi a sudo
usermod -aG sudo meepleai

# Copia chiave SSH
mkdir -p /home/meepleai/.ssh
cp ~/.ssh/authorized_keys /home/meepleai/.ssh/
chown -R meepleai:meepleai /home/meepleai/.ssh
chmod 700 /home/meepleai/.ssh
chmod 600 /home/meepleai/.ssh/authorized_keys

# Verifica accesso (da un altro terminale)
ssh meepleai@YOUR_IP_ADDRESS

# Se funziona, disabilita login root
sudo sed -i 's/PermitRootLogin yes/PermitRootLogin no/' /etc/ssh/sshd_config
sudo sed -i 's/#PasswordAuthentication yes/PasswordAuthentication no/' /etc/ssh/sshd_config
sudo systemctl restart sshd
```

### 2.4 Installa Docker

```bash
# Da utente meepleai
sudo -i

# Installa Docker
curl -fsSL https://get.docker.com | sh

# Aggiungi utente al gruppo docker
usermod -aG docker meepleai

# Installa Docker Compose plugin
apt install -y docker-compose-plugin

# Abilita Docker al boot
systemctl enable docker
systemctl start docker

# Verifica installazione
docker --version
docker compose version

# Esci da root
exit

# Riconnetti per applicare gruppo docker
exit
ssh meepleai@YOUR_IP_ADDRESS

# Test Docker (senza sudo)
docker run hello-world
```

### 2.5 Crea Struttura Directory

```bash
# Directory applicazione
mkdir -p /home/meepleai/{app,backups/{postgres,redis,qdrant},scripts,logs}

# Permessi
chmod 750 /home/meepleai/backups
```

---

## Fase 3: Configurazione DNS (Day 2)

### 3.1 Record DNS su Cloudflare

Cloudflare Dashboard → meepleai.com → DNS → Records:

| Type | Name | Content | Proxy | TTL |
|------|------|---------|-------|-----|
| A | `@` | `YOUR_VPS_IP` | ☁️ Proxied | Auto |
| A | `www` | `YOUR_VPS_IP` | ☁️ Proxied | Auto |
| A | `api` | `YOUR_VPS_IP` | 🔘 DNS only | Auto |
| A | `staging` | `YOUR_VPS_IP` | ☁️ Proxied | Auto |
| A | `api-staging` | `YOUR_VPS_IP` | 🔘 DNS only | Auto |

**Importante**: `api` e `api-staging` usano **DNS only** (gray cloud) per supportare WebSocket senza problemi con il proxy Cloudflare.

### 3.2 SSL/TLS Settings

Cloudflare → SSL/TLS:
- **Encryption mode**: Full (strict)
- **Always Use HTTPS**: On
- **Automatic HTTPS Rewrites**: On
- **Minimum TLS Version**: 1.2
- **TLS 1.3**: On

### 3.3 Verifica DNS

```bash
# Aspetta qualche minuto, poi verifica
dig @8.8.8.8 meepleai.com +short
dig @1.1.1.1 api.meepleai.com +short

# Entrambi devono mostrare il tuo IP VPS
```

---

## Fase 4: Deploy Applicazione (Day 2-3)

### 4.1 Clone Repository

```bash
ssh meepleai@YOUR_VPS_IP

cd /home/meepleai/app
git clone https://github.com/YOUR_USERNAME/meepleai-monorepo-frontend.git .

# Verifica struttura
ls -la
# Dovresti vedere: apps/ docs/ infra/ tests/ etc.
```

### 4.2 Configura Secrets

```bash
cd /home/meepleai/app/infra/secrets

# Genera password sicure
generate_password() {
    openssl rand -base64 32 | tr -d '/+=' | head -c 32
}

# ============ database.secret ============
cat > database.secret << EOF
POSTGRES_HOST=postgres
POSTGRES_PORT=5432
POSTGRES_DB=meepleai
POSTGRES_USER=meepleai
POSTGRES_PASSWORD=$(generate_password)
EOF

# ============ redis.secret ============
cat > redis.secret << EOF
REDIS_HOST=redis
REDIS_PORT=6379
REDIS_PASSWORD=$(generate_password)
EOF

# ============ qdrant.secret ============
cat > qdrant.secret << EOF
QDRANT_HOST=qdrant
QDRANT_PORT=6333
QDRANT_API_KEY=$(generate_password)
EOF

# ============ jwt.secret ============
cat > jwt.secret << EOF
JWT_SECRET_KEY=$(openssl rand -base64 64 | tr -d '\n')
JWT_ISSUER=meepleai.com
JWT_AUDIENCE=meepleai-users
JWT_EXPIRY_HOURS=24
JWT_REFRESH_EXPIRY_DAYS=7
EOF

# ============ admin.secret ============
cat > admin.secret << EOF
ADMIN_EMAIL=admin@meepleai.com
ADMIN_PASSWORD=$(generate_password)
EOF

# ============ embedding-service.secret ============
cat > embedding-service.secret << EOF
EMBEDDING_MODEL=sentence-transformers/all-MiniLM-L6-v2
EMBEDDING_DEVICE=cpu
EOF

# ============ openrouter.secret (opzionale, per LLM) ============
cat > openrouter.secret << EOF
OPENROUTER_API_KEY=sk-or-v1-your-key-here
OPENROUTER_DEFAULT_MODEL=anthropic/claude-3.5-sonnet
EOF

# Proteggi i file
chmod 600 *.secret

# Salva una copia delle password generate
cat *.secret > /home/meepleai/backups/secrets-backup-$(date +%Y%m%d).txt
chmod 600 /home/meepleai/backups/secrets-backup-*.txt

echo "✅ Secrets generati! Salva il backup in un password manager!"
cat /home/meepleai/backups/secrets-backup-$(date +%Y%m%d).txt
```

### 4.3 Crea docker-compose.yml per Self-Hosted

```bash
cd /home/meepleai/app/infra

cat > docker-compose.prod.yml << 'EOF'
version: '3.8'

services:
  # ============ TRAEFIK (Reverse Proxy + SSL) ============
  traefik:
    image: traefik:v3.0
    container_name: meepleai-traefik
    command:
      - "--api.dashboard=true"
      - "--api.insecure=false"
      - "--providers.docker=true"
      - "--providers.docker.exposedbydefault=false"
      - "--entrypoints.web.address=:80"
      - "--entrypoints.websecure.address=:443"
      - "--entrypoints.web.http.redirections.entrypoint.to=websecure"
      - "--entrypoints.web.http.redirections.entrypoint.scheme=https"
      - "--certificatesresolvers.letsencrypt.acme.tlschallenge=true"
      - "--certificatesresolvers.letsencrypt.acme.email=admin@meepleai.com"
      - "--certificatesresolvers.letsencrypt.acme.storage=/letsencrypt/acme.json"
      - "--log.level=INFO"
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
      - traefik_letsencrypt:/letsencrypt
    networks:
      - meepleai-network
    restart: unless-stopped

  # ============ POSTGRESQL ============
  postgres:
    image: postgres:16-alpine
    container_name: meepleai-postgres
    env_file:
      - ./secrets/database.secret
    environment:
      POSTGRES_INITDB_ARGS: "--encoding=UTF8 --locale=C"
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./init-scripts:/docker-entrypoint-initdb.d:ro
    ports:
      - "127.0.0.1:5432:5432"
    networks:
      - meepleai-network
    deploy:
      resources:
        limits:
          memory: 2G
        reservations:
          memory: 512M
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U $${POSTGRES_USER} -d $${POSTGRES_DB}"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped

  # ============ REDIS ============
  redis:
    image: redis:7-alpine
    container_name: meepleai-redis
    command: >
      redis-server
      --requirepass $${REDIS_PASSWORD}
      --appendonly yes
      --maxmemory 256mb
      --maxmemory-policy allkeys-lru
    env_file:
      - ./secrets/redis.secret
    volumes:
      - redis_data:/data
    ports:
      - "127.0.0.1:6379:6379"
    networks:
      - meepleai-network
    deploy:
      resources:
        limits:
          memory: 512M
        reservations:
          memory: 128M
    healthcheck:
      test: ["CMD", "redis-cli", "-a", "$${REDIS_PASSWORD}", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    restart: unless-stopped

  # ============ QDRANT (Vector Database) ============
  qdrant:
    image: qdrant/qdrant:v1.12.0
    container_name: meepleai-qdrant
    environment:
      QDRANT__SERVICE__API_KEY: ${QDRANT_API_KEY}
      QDRANT__SERVICE__ENABLE_TLS: "false"
      QDRANT__STORAGE__STORAGE_PATH: /qdrant/storage
      QDRANT__STORAGE__SNAPSHOTS_PATH: /qdrant/snapshots
    volumes:
      - qdrant_data:/qdrant/storage
      - qdrant_snapshots:/qdrant/snapshots
    ports:
      - "127.0.0.1:6333:6333"
      - "127.0.0.1:6334:6334"
    networks:
      - meepleai-network
    deploy:
      resources:
        limits:
          memory: 2G
        reservations:
          memory: 512M
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:6333/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    restart: unless-stopped

  # ============ .NET API ============
  api:
    build:
      context: ../apps/api
      dockerfile: Dockerfile
    image: meepleai-api:latest
    container_name: meepleai-api
    env_file:
      - ./secrets/database.secret
      - ./secrets/redis.secret
      - ./secrets/qdrant.secret
      - ./secrets/jwt.secret
      - ./secrets/admin.secret
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"
      Redis__ConnectionString: "redis:6379,password=${REDIS_PASSWORD}"
      Qdrant__Url: "http://qdrant:6333"
      Qdrant__ApiKey: "${QDRANT_API_KEY}"
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
      qdrant:
        condition: service_healthy
    networks:
      - meepleai-network
    deploy:
      resources:
        limits:
          memory: 2G
        reservations:
          memory: 512M
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.api.rule=Host(`api.meepleai.com`)"
      - "traefik.http.routers.api.entrypoints=websecure"
      - "traefik.http.routers.api.tls.certresolver=letsencrypt"
      - "traefik.http.services.api.loadbalancer.server.port=8080"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    restart: unless-stopped

  # ============ EMBEDDING SERVICE (Python) ============
  embedding-service:
    build:
      context: ../apps/embedding-service
      dockerfile: Dockerfile
    image: meepleai-embedding:latest
    container_name: meepleai-embedding
    env_file:
      - ./secrets/embedding-service.secret
    environment:
      MODEL_NAME: sentence-transformers/all-MiniLM-L6-v2
      DEVICE: cpu
      PORT: 8000
    networks:
      - meepleai-network
    deploy:
      resources:
        limits:
          memory: 3G
        reservations:
          memory: 1G
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8000/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    restart: unless-stopped

  # ============ RERANKER SERVICE (Python) ============
  reranker-service:
    build:
      context: ../apps/reranker-service
      dockerfile: Dockerfile
    image: meepleai-reranker:latest
    container_name: meepleai-reranker
    environment:
      MODEL_NAME: cross-encoder/ms-marco-MiniLM-L-6-v2
      DEVICE: cpu
      PORT: 8001
    networks:
      - meepleai-network
    deploy:
      resources:
        limits:
          memory: 2G
        reservations:
          memory: 512M
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8001/health"]
      interval: 30s
      timeout: 10s
      retries: 3
    restart: unless-stopped

networks:
  meepleai-network:
    driver: bridge

volumes:
  traefik_letsencrypt:
  postgres_data:
  redis_data:
  qdrant_data:
  qdrant_snapshots:
EOF
```

### 4.4 Crea .env per Docker Compose

```bash
cd /home/meepleai/app/infra

# Carica secrets negli environment
cat > .env << EOF
# Loaded from secrets
$(grep -h '' secrets/*.secret | grep -v '^#' | grep '=')
EOF

chmod 600 .env
```

### 4.5 Avvia Servizi

```bash
cd /home/meepleai/app/infra

# Pull/build immagini
docker compose -f docker-compose.prod.yml build

# Avvia database prima
docker compose -f docker-compose.prod.yml up -d postgres redis qdrant

# Attendi che siano healthy
echo "Attendo avvio database..."
sleep 30

# Verifica stato
docker compose -f docker-compose.prod.yml ps

# Avvia servizi applicativi
docker compose -f docker-compose.prod.yml up -d embedding-service reranker-service

# Attendi caricamento modelli
echo "Attendo caricamento modelli ML..."
sleep 60

# Avvia API
docker compose -f docker-compose.prod.yml up -d api

# Infine Traefik
docker compose -f docker-compose.prod.yml up -d traefik

# Verifica tutti i servizi
docker compose -f docker-compose.prod.yml ps

# Controlla logs per errori
docker compose -f docker-compose.prod.yml logs --tail=50
```

### 4.6 Esegui Migrations Database

```bash
# Se hai migrations EF Core
cd /home/meepleai/app/apps/api/src/Api

# Opzione 1: Da container API
docker exec -it meepleai-api dotnet ef database update

# Opzione 2: Manualmente con connection string
docker exec -it meepleai-postgres psql -U meepleai -d meepleai -c "\dt"
```

---

## Fase 5: Verifica Deployment (Day 3)

### 5.1 Health Checks

```bash
# Health check API (da VPS)
curl http://localhost:8080/health
# Expected: {"status":"healthy",...}

# Health check pubblico (dopo DNS propagato)
curl https://api.meepleai.com/health

# Verifica SSL
curl -v https://api.meepleai.com/health 2>&1 | grep "SSL\|TLS\|certificate"

# API Documentation
curl -I https://api.meepleai.com/scalar/v1
# Expected: HTTP/2 200
```

### 5.2 Test Servizi

```bash
# PostgreSQL
docker exec -it meepleai-postgres psql -U meepleai -d meepleai -c "SELECT version();"

# Redis
docker exec -it meepleai-redis redis-cli -a YOUR_REDIS_PASSWORD ping
# Expected: PONG

# Qdrant
curl http://localhost:6333/health
# Expected: {"status":"ok"}

# Embedding Service
curl http://localhost:8000/health

# Reranker Service
curl http://localhost:8001/health
```

### 5.3 Verifica Risorse

```bash
# Uso RAM
free -h

# Uso CPU
htop

# Uso disco
df -h

# Container stats
docker stats --no-stream
```

---

## Fase 6: Backup Automatici (Day 3)

### 6.1 Script Backup Completo

```bash
cat > /home/meepleai/scripts/backup-all.sh << 'SCRIPT'
#!/bin/bash
#
# MeepleAI - Backup Script
# Esegue backup di PostgreSQL, Redis, Qdrant
#

set -euo pipefail

# Configurazione
BACKUP_DIR="/home/meepleai/backups"
DATE=$(date +%Y%m%d_%H%M%S)
RETENTION_DAYS=7
LOG_FILE="/home/meepleai/logs/backup.log"

# Colori
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

log() {
    echo -e "[$(date '+%Y-%m-%d %H:%M:%S')] $1" | tee -a "$LOG_FILE"
}

error_exit() {
    log "${RED}ERROR: $1${NC}"
    exit 1
}

# Crea directory se non esistono
mkdir -p "$BACKUP_DIR"/{postgres,redis,qdrant}
mkdir -p "$(dirname "$LOG_FILE")"

log "${GREEN}========== BACKUP STARTED - $DATE ==========${NC}"

# ============ POSTGRESQL ============
log "📦 Backing up PostgreSQL..."
if docker exec meepleai-postgres pg_dumpall -U meepleai > "$BACKUP_DIR/postgres/dump_$DATE.sql"; then
    gzip -f "$BACKUP_DIR/postgres/dump_$DATE.sql"
    log "${GREEN}✅ PostgreSQL backup: dump_$DATE.sql.gz${NC}"
else
    error_exit "PostgreSQL backup failed"
fi

# ============ REDIS ============
log "📦 Backing up Redis..."
if docker exec meepleai-redis redis-cli -a "${REDIS_PASSWORD:-}" BGSAVE; then
    sleep 5
    if docker cp meepleai-redis:/data/dump.rdb "$BACKUP_DIR/redis/dump_$DATE.rdb"; then
        log "${GREEN}✅ Redis backup: dump_$DATE.rdb${NC}"
    else
        log "${YELLOW}⚠️ Redis backup copy failed${NC}"
    fi
else
    log "${YELLOW}⚠️ Redis BGSAVE failed${NC}"
fi

# ============ QDRANT ============
log "📦 Backing up Qdrant..."
QDRANT_API_KEY="${QDRANT_API_KEY:-}"

# Crea snapshot via API
SNAPSHOT_RESPONSE=$(curl -s -X POST "http://localhost:6333/snapshots" \
    -H "api-key: $QDRANT_API_KEY" \
    -H "Content-Type: application/json")

if echo "$SNAPSHOT_RESPONSE" | grep -q "result"; then
    sleep 10
    # Ottieni nome ultimo snapshot
    SNAPSHOT_NAME=$(curl -s "http://localhost:6333/snapshots" \
        -H "api-key: $QDRANT_API_KEY" | jq -r '.result[-1].name // empty')

    if [ -n "$SNAPSHOT_NAME" ]; then
        curl -s -o "$BACKUP_DIR/qdrant/snapshot_$DATE.snapshot" \
            "http://localhost:6333/snapshots/$SNAPSHOT_NAME" \
            -H "api-key: $QDRANT_API_KEY"
        log "${GREEN}✅ Qdrant backup: snapshot_$DATE.snapshot${NC}"
    else
        log "${YELLOW}⚠️ Qdrant snapshot name not found${NC}"
    fi
else
    log "${YELLOW}⚠️ Qdrant snapshot creation failed${NC}"
fi

# ============ CLEANUP ============
log "🧹 Cleaning old backups (>${RETENTION_DAYS} days)..."
find "$BACKUP_DIR" -type f -mtime +$RETENTION_DAYS -delete
find "$BACKUP_DIR" -type d -empty -delete 2>/dev/null || true

# ============ SUMMARY ============
log "${GREEN}========== BACKUP COMPLETED ==========${NC}"
log "Backup sizes:"
du -sh "$BACKUP_DIR"/* 2>/dev/null || true

# Spazio disco rimanente
DISK_FREE=$(df -h /home | tail -1 | awk '{print $4}')
log "Disk free: $DISK_FREE"

# Notifica se disco quasi pieno (>80%)
DISK_USAGE=$(df /home | tail -1 | awk '{print $5}' | tr -d '%')
if [ "$DISK_USAGE" -gt 80 ]; then
    log "${RED}⚠️ WARNING: Disk usage at ${DISK_USAGE}%${NC}"
fi
SCRIPT

chmod +x /home/meepleai/scripts/backup-all.sh
```

### 6.2 Configura Cron

```bash
# Carica variabili ambiente per cron
cat > /home/meepleai/scripts/backup-env.sh << 'EOF'
#!/bin/bash
source /home/meepleai/app/infra/.env
export REDIS_PASSWORD QDRANT_API_KEY
/home/meepleai/scripts/backup-all.sh
EOF
chmod +x /home/meepleai/scripts/backup-env.sh

# Aggiungi a crontab
crontab -e

# Aggiungi queste righe:
# Backup giornaliero alle 3:00 AM
0 3 * * * /home/meepleai/scripts/backup-env.sh >> /home/meepleai/logs/backup-cron.log 2>&1

# Pulizia log settimanale (domenica 4:00 AM)
0 4 * * 0 find /home/meepleai/logs -name "*.log" -mtime +30 -delete
```

### 6.3 Test Backup Manuale

```bash
# Carica environment
source /home/meepleai/app/infra/.env

# Esegui backup
/home/meepleai/scripts/backup-all.sh

# Verifica risultato
ls -lah /home/meepleai/backups/*/
```

---

## Fase 7: Staging Environment (Day 4)

### 7.1 Docker Compose per Staging

```bash
cat > /home/meepleai/app/infra/docker-compose.staging.yml << 'EOF'
version: '3.8'

# Staging usa gli stessi servizi ma con:
# - Database separato
# - Risorse ridotte
# - Subdomain staging.*

services:
  postgres-staging:
    image: postgres:16-alpine
    container_name: meepleai-postgres-staging
    environment:
      POSTGRES_USER: meepleai_staging
      POSTGRES_PASSWORD: ${POSTGRES_STAGING_PASSWORD}
      POSTGRES_DB: meepleai_staging
    volumes:
      - postgres_staging_data:/var/lib/postgresql/data
    ports:
      - "127.0.0.1:5433:5432"
    networks:
      - meepleai-network
    deploy:
      resources:
        limits:
          memory: 512M
    restart: unless-stopped

  api-staging:
    image: meepleai-api:latest
    container_name: meepleai-api-staging
    environment:
      ASPNETCORE_ENVIRONMENT: Staging
      ASPNETCORE_URLS: http://+:8081
      ConnectionStrings__DefaultConnection: "Host=postgres-staging;Port=5432;Database=meepleai_staging;Username=meepleai_staging;Password=${POSTGRES_STAGING_PASSWORD}"
      Redis__ConnectionString: "redis:6379,password=${REDIS_PASSWORD}"
      Qdrant__Url: "http://qdrant:6333"
    depends_on:
      - postgres-staging
    networks:
      - meepleai-network
    deploy:
      resources:
        limits:
          memory: 1G
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.api-staging.rule=Host(`api-staging.meepleai.com`)"
      - "traefik.http.routers.api-staging.entrypoints=websecure"
      - "traefik.http.routers.api-staging.tls.certresolver=letsencrypt"
      - "traefik.http.services.api-staging.loadbalancer.server.port=8081"
    restart: unless-stopped

volumes:
  postgres_staging_data:

networks:
  meepleai-network:
    external: true
EOF

# Genera password staging
echo "POSTGRES_STAGING_PASSWORD=$(openssl rand -base64 32 | tr -d '/+=')" >> /home/meepleai/app/infra/.env
```

### 7.2 Avvia Staging

```bash
cd /home/meepleai/app/infra

# Avvia solo servizi staging
docker compose -f docker-compose.staging.yml up -d

# Verifica
curl https://api-staging.meepleai.com/health
```

---

## Fase 8: Monitoring Base (Day 4)

### 8.1 Script Health Check

```bash
cat > /home/meepleai/scripts/health-check.sh << 'SCRIPT'
#!/bin/bash
#
# Health Check Script
#

SERVICES=(
    "PostgreSQL|docker exec meepleai-postgres pg_isready -U meepleai"
    "Redis|docker exec meepleai-redis redis-cli ping"
    "Qdrant|curl -sf http://localhost:6333/health"
    "API|curl -sf http://localhost:8080/health"
    "Embedding|curl -sf http://localhost:8000/health"
    "Reranker|curl -sf http://localhost:8001/health"
)

echo "========== HEALTH CHECK $(date) =========="

ALL_OK=true
for service in "${SERVICES[@]}"; do
    NAME="${service%%|*}"
    CMD="${service##*|}"

    if eval "$CMD" > /dev/null 2>&1; then
        echo "✅ $NAME: OK"
    else
        echo "❌ $NAME: FAILED"
        ALL_OK=false
    fi
done

# Risorse
echo ""
echo "========== RESOURCES =========="
echo "RAM: $(free -h | grep Mem | awk '{print $3"/"$2}')"
echo "Disk: $(df -h / | tail -1 | awk '{print $3"/"$2" ("$5" used)"}')"
echo "Load: $(uptime | awk -F'load average:' '{print $2}')"

if [ "$ALL_OK" = false ]; then
    exit 1
fi
SCRIPT

chmod +x /home/meepleai/scripts/health-check.sh
```

### 8.2 Cron per Health Check

```bash
# Aggiungi a crontab
crontab -e

# Health check ogni 5 minuti
*/5 * * * * /home/meepleai/scripts/health-check.sh >> /home/meepleai/logs/health.log 2>&1
```

---

## Fase 9: Security Hardening (Day 5)

### 9.1 Fail2Ban

```bash
sudo apt install -y fail2ban

sudo cat > /etc/fail2ban/jail.local << 'EOF'
[DEFAULT]
bantime = 3600
findtime = 600
maxretry = 5

[sshd]
enabled = true
port = ssh
maxretry = 3
bantime = 86400

[traefik-auth]
enabled = true
port = http,https
filter = traefik-auth
logpath = /var/log/traefik/access.log
maxretry = 10
bantime = 3600
EOF

sudo systemctl enable fail2ban
sudo systemctl restart fail2ban
```

### 9.2 Aggiornamenti Automatici Security

```bash
sudo apt install -y unattended-upgrades

sudo dpkg-reconfigure -plow unattended-upgrades
# Seleziona "Yes" per abilitare aggiornamenti automatici
```

### 9.3 Limita Accesso Servizi

I database sono già esposti solo su `127.0.0.1`, non accessibili dall'esterno.

---

## Fase 10: Documentazione e Runbook (Day 5)

### 10.1 Crea Runbook

```bash
cat > /home/meepleai/RUNBOOK.md << 'EOF'
# MeepleAI Runbook

**Server**: meepleai-prod-01
**IP**: YOUR_VPS_IP
**Provider**: Hetzner Cloud (fsn1)
**OS**: Ubuntu 24.04 LTS

## Accesso

```bash
ssh meepleai@YOUR_VPS_IP
```

## Comandi Comuni

### Stato Servizi
```bash
cd /home/meepleai/app/infra
docker compose -f docker-compose.prod.yml ps
docker stats --no-stream
```

### Logs
```bash
# Tutti i log
docker compose -f docker-compose.prod.yml logs -f

# Singolo servizio
docker compose -f docker-compose.prod.yml logs -f api
```

### Riavvio Servizi
```bash
# Singolo servizio
docker compose -f docker-compose.prod.yml restart api

# Tutti
docker compose -f docker-compose.prod.yml restart
```

### Aggiornamento Applicazione
```bash
cd /home/meepleai/app
git pull
cd infra
docker compose -f docker-compose.prod.yml build
docker compose -f docker-compose.prod.yml up -d
```

### Backup Manuale
```bash
/home/meepleai/scripts/backup-all.sh
```

### Restore da Backup
```bash
# PostgreSQL
gunzip -c /home/meepleai/backups/postgres/dump_YYYYMMDD.sql.gz | \
  docker exec -i meepleai-postgres psql -U meepleai

# Redis
docker cp /home/meepleai/backups/redis/dump_YYYYMMDD.rdb meepleai-redis:/data/dump.rdb
docker restart meepleai-redis
```

## Troubleshooting

### Container non parte
```bash
docker compose -f docker-compose.prod.yml logs SERVICE_NAME
docker inspect meepleai-SERVICE_NAME
```

### Disco pieno
```bash
docker system prune -a  # ATTENZIONE: rimuove immagini non usate
find /home/meepleai/backups -mtime +7 -delete
```

### RAM esaurita
```bash
docker stats --no-stream
# Identifica container che usa troppa RAM
docker restart meepleai-SERVICE_NAME
```

## Contatti

- Hetzner Support: support@hetzner.com
- Cloudflare: community.cloudflare.com

## Credenziali

Tutte le credenziali sono in:
- `/home/meepleai/app/infra/secrets/*.secret`
- Backup: `/home/meepleai/backups/secrets-backup-*.txt`
EOF
```

---

## Checklist Finale

### Pre-Launch
- [ ] Dominio acquistato e DNS configurato
- [ ] VPS provisionato e Docker installato
- [ ] Tutti i container running e healthy
- [ ] SSL certificato valido (https funziona)
- [ ] Backup script configurato e testato
- [ ] Health check automatico attivo
- [ ] Security hardening completato
- [ ] Runbook documentato

### Post-Launch (prime 24h)
- [ ] Monitorare logs: `docker compose logs -f`
- [ ] Verificare backup notturno eseguito
- [ ] Controllare risorse: `htop`, `df -h`
- [ ] Test funzionale completo

### Settimanale
- [ ] Verificare backup funzionanti
- [ ] Review logs errori
- [ ] Aggiornare immagini Docker se necessario
- [ ] Controllare spazio disco

---

## Comandi Utili Quick Reference

```bash
# SSH
ssh meepleai@YOUR_VPS_IP

# Directory
cd /home/meepleai/app/infra

# Stato
docker compose -f docker-compose.prod.yml ps

# Logs
docker compose -f docker-compose.prod.yml logs -f --tail=100

# Restart
docker compose -f docker-compose.prod.yml restart

# Update
git pull && docker compose -f docker-compose.prod.yml up -d --build

# Backup
/home/meepleai/scripts/backup-all.sh

# Health
/home/meepleai/scripts/health-check.sh
```

---

## Costi Riepilogo

| Voce | Costo |
|------|-------|
| VPS Hetzner CPX31 | €15.59/mese |
| Backup Hetzner | €3.08/mese |
| Dominio (annualizzato) | ~€0.83/mese |
| **TOTALE** | **~€19.50/mese** |

+ Cloudflare R2 se usi storage: ~€0-5/mese

**Totale massimo**: ~€25/mese

---

**Creato**: 2026-01-27
**Budget**: ~€20-25/mese
**Approccio**: Full Self-Hosted

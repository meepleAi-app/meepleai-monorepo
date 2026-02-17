# MeepleAI Setup Guide - Opzione B Balanced

**Target**: Staging + Produzione per Beta Testing (5-20 utenti)
**Budget**: ~€75-85/mese
**Approccio**: Mix ibrido (DB managed + container self-hosted)
**Regione**: EU

---

## Panoramica Architettura

```
                    ┌─────────────────┐
                    │   Cloudflare    │ DNS + CDN + SSL (€0)
                    └────────┬────────┘
                             │
              ┌──────────────┴──────────────┐
              │                             │
     ┌────────▼────────┐         ┌──────────▼──────────┐
     │ staging.meepleai│         │ app.meepleai.com    │
     │    .com         │         │ api.meepleai.com    │
     └────────┬────────┘         └──────────┬──────────┘
              │                             │
              └──────────────┬──────────────┘
                             │
                    ┌────────▼────────┐
                    │   Hetzner VPS   │ CPX31: €15.59/m
                    │   8 vCPU, 16GB  │ Docker + Traefik
                    └────────┬────────┘
                             │
         ┌───────────────────┼───────────────────┐
         │                   │                   │
┌────────▼────────┐ ┌────────▼────────┐ ┌────────▼────────┐
│  Neon Postgres  │ │  Upstash Redis  │ │  Qdrant Cloud   │
│     €19/m       │ │    ~€5-10/m     │ │     €25/m       │
│   (managed)     │ │   (managed)     │ │   (managed)     │
└─────────────────┘ └─────────────────┘ └─────────────────┘
```

---

## Costi Dettagliati

| Servizio | Provider | Piano | Costo/mese |
|----------|----------|-------|------------|
| **VPS** | Hetzner | CPX31 (8 vCPU, 16GB RAM) | €15.59 |
| **PostgreSQL** | Neon | Launch (10GB, autoscale) | €19.00 |
| **Redis** | Upstash | Pay-as-you-go | ~€5-10 |
| **Qdrant** | Qdrant Cloud | Starter (4GB) | €25.00 |
| **Dominio** | Porkbun | meepleai.com | ~€1.00 |
| **DNS/CDN/SSL** | Cloudflare | Free | €0 |
| **Object Storage** | Cloudflare R2 | 100GB | ~€5 |
| **Email** | Resend | Free tier (3K/mese) | €0 |
| **Monitoring** | Grafana Cloud | Free tier | €0 |
| **Backup** | Hetzner Snapshots | 7-day | ~€3 |
| **TOTALE** | | | **~€75-85/mese** |

---

## Checklist Acquisti

### Fase 1: Account e Registrazioni (Day 1)

#### 1.1 Dominio - Porkbun
- [ ] Vai su https://porkbun.com
- [ ] Cerca `meepleai.com`
- [ ] Se disponibile: aggiungi al carrello (~$10.88/anno = ~€10)
- [ ] Crea account, completa acquisto
- [ ] Abilita WHOIS privacy (gratuito su Porkbun)
- [ ] Abilita auto-rinnovo

**Alternativa se non disponibile**: `meepleai.app`, `getmeepleai.com`, `playmeeple.com`

#### 1.2 Cloudflare (DNS + CDN)
- [ ] Vai su https://dash.cloudflare.com/sign-up
- [ ] Crea account gratuito
- [ ] Aggiungi sito: `meepleai.com`
- [ ] Cloudflare ti darà 2 nameserver (es. `ada.ns.cloudflare.com`, `bob.ns.cloudflare.com`)
- [ ] Vai su Porkbun → Domain Management → meepleai.com → Nameservers
- [ ] Sostituisci i nameserver Porkbun con quelli Cloudflare
- [ ] Attendi propagazione (2-48 ore, solitamente 30 min)

#### 1.3 Hetzner Cloud (VPS)
- [ ] Vai su https://console.hetzner.cloud/register
- [ ] Crea account, verifica email
- [ ] Aggiungi metodo pagamento (carta o PayPal)
- [ ] Crea progetto: `MeepleAI-Production`

---

### Fase 2: Database Managed (Day 1-2)

#### 2.1 Neon (PostgreSQL)
- [ ] Vai su https://neon.tech
- [ ] Sign up con GitHub/Google
- [ ] Crea progetto: `meepleai-prod`
- [ ] Region: **AWS eu-central-1** (Frankfurt)
- [ ] Piano: **Launch** ($19/mese)
  - 10GB storage incluso
  - Autoscaling compute
  - Branching per staging

**Configurazione**:
```
Host: ep-xxxxx.eu-central-1.aws.neon.tech
Database: meepleai
User: meepleai_user
Password: [generato automaticamente]
```

- [ ] Copia connection string
- [ ] Crea branch `staging` per ambiente staging

#### 2.2 Upstash (Redis)
- [ ] Vai su https://console.upstash.com
- [ ] Sign up con GitHub/Google
- [ ] Crea database Redis
- [ ] Region: **eu-west-1** (Ireland)
- [ ] Piano: **Pay-as-you-go** (€0 fino a 10K comandi/giorno)

**Configurazione**:
```
Endpoint: eu1-xxxxx.upstash.io
Port: 6379
Password: [generato]
TLS: Enabled
```

- [ ] Copia Redis URL: `rediss://default:PASSWORD@eu1-xxxxx.upstash.io:6379`

#### 2.3 Qdrant Cloud (Vector DB)
- [ ] Vai su https://cloud.qdrant.io
- [ ] Sign up
- [ ] Crea cluster
- [ ] Region: **AWS eu-central-1**
- [ ] Piano: **Starter** ($25/mese, 4GB)

**Configurazione**:
```
Cluster URL: https://xxxxx.eu-central-1.aws.cloud.qdrant.io
API Key: [generato]
```

- [ ] Salva API key (mostrata solo una volta!)

---

### Fase 3: Provisioning VPS (Day 2)

#### 3.1 Crea VPS su Hetzner

1. Hetzner Console → Servers → Add Server
2. **Configurazione**:
   - Location: **Falkenstein (fsn1)** o **Helsinki (hel1)**
   - Image: **Ubuntu 24.04 LTS**
   - Type: **CPX31** (8 vCPU AMD, 16GB RAM, 160GB NVMe)
   - Networking: IPv4 + IPv6
   - SSH Key: Carica la tua chiave pubblica
   - Server name: `meepleai-prod-01`
   - Labels: `env=production`, `project=meepleai`

3. [ ] Click "Create & Buy Now" (€15.59/mese)
4. [ ] Copia IP Address: `___.___.___.___ `

#### 3.2 Setup Iniziale VPS

```bash
# SSH nel server
ssh root@YOUR_IP_ADDRESS

# Aggiorna sistema
apt update && apt upgrade -y

# Installa strumenti essenziali
apt install -y git curl wget nano htop ufw fail2ban

# Configura firewall
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable

# Crea utente non-root
adduser meepleai
usermod -aG sudo meepleai

# Copia chiave SSH per nuovo utente
mkdir -p /home/meepleai/.ssh
cp ~/.ssh/authorized_keys /home/meepleai/.ssh/
chown -R meepleai:meepleai /home/meepleai/.ssh
chmod 700 /home/meepleai/.ssh
chmod 600 /home/meepleai/.ssh/authorized_keys

# Disabilita login root SSH
sed -i 's/PermitRootLogin yes/PermitRootLogin no/' /etc/ssh/sshd_config
systemctl restart sshd
```

#### 3.3 Installa Docker

```bash
# Da utente meepleai
su - meepleai

# Installa Docker
curl -fsSL https://get.docker.com | sudo sh

# Aggiungi utente al gruppo docker
sudo usermod -aG docker $USER

# Installa Docker Compose plugin
sudo apt install -y docker-compose-plugin

# Verifica
docker --version
docker compose version

# Riconnetti per applicare gruppo
exit
ssh meepleai@YOUR_IP_ADDRESS

# Test
docker run hello-world
```

---

### Fase 4: Configurazione DNS (Day 2)

#### 4.1 Record DNS su Cloudflare

Vai su Cloudflare → meepleai.com → DNS → Records:

| Type | Name | Content | Proxy | TTL |
|------|------|---------|-------|-----|
| A | @ | `YOUR_VPS_IP` | Proxied ☁️ | Auto |
| CNAME | www | meepleai.com | Proxied ☁️ | Auto |
| A | api | `YOUR_VPS_IP` | DNS only 🔘 | Auto |
| A | staging | `YOUR_VPS_IP` | Proxied ☁️ | Auto |
| A | api-staging | `YOUR_VPS_IP` | DNS only 🔘 | Auto |

**Nota**: `api` usa DNS only (gray cloud) per supportare WebSocket senza problemi.

#### 4.2 SSL Settings

Cloudflare → SSL/TLS:
- [ ] Mode: **Full (strict)**
- [ ] Always Use HTTPS: **On**
- [ ] Automatic HTTPS Rewrites: **On**
- [ ] Minimum TLS Version: **TLS 1.2**

#### 4.3 Verifica Propagazione

```bash
# Da locale
dig @8.8.8.8 meepleai.com
dig @1.1.1.1 api.meepleai.com

# Dovrebbe mostrare il tuo IP VPS
```

---

### Fase 5: Deploy Applicazione (Day 3)

#### 5.1 Clone Repository

```bash
ssh meepleai@YOUR_VPS_IP

# Clone repo
cd /home/meepleai
git clone https://github.com/YOUR_USERNAME/meepleai-monorepo-frontend.git
cd meepleai-monorepo-frontend
```

#### 5.2 Configura Secrets per Servizi Managed

```bash
cd infra/secrets

# Crea file secrets con valori dei servizi managed

# database.secret
cat > database.secret << 'EOF'
POSTGRES_HOST=ep-xxxxx.eu-central-1.aws.neon.tech
POSTGRES_PORT=5432
POSTGRES_DB=meepleai
POSTGRES_USER=meepleai_user
POSTGRES_PASSWORD=YOUR_NEON_PASSWORD
DATABASE_URL=postgresql://meepleai_user:YOUR_NEON_PASSWORD@ep-xxxxx.eu-central-1.aws.neon.tech/meepleai?sslmode=require
EOF

# redis.secret
cat > redis.secret << 'EOF'
REDIS_URL=rediss://default:YOUR_UPSTASH_PASSWORD@eu1-xxxxx.upstash.io:6379
REDIS_HOST=eu1-xxxxx.upstash.io
REDIS_PORT=6379
REDIS_PASSWORD=YOUR_UPSTASH_PASSWORD
REDIS_TLS=true
EOF

# qdrant.secret
cat > qdrant.secret << 'EOF'
QDRANT_URL=https://xxxxx.eu-central-1.aws.cloud.qdrant.io
QDRANT_API_KEY=YOUR_QDRANT_API_KEY
EOF

# Genera altri secrets (JWT, admin, etc.)
# Se hai PowerShell:
pwsh setup-secrets.ps1 -SaveGenerated

# Altrimenti genera manualmente:
cat > jwt.secret << 'EOF'
JWT_SECRET_KEY=$(openssl rand -base64 64 | tr -d '\n')
JWT_ISSUER=meepleai.com
JWT_AUDIENCE=meepleai-users
JWT_EXPIRY_HOURS=24
EOF

cat > admin.secret << 'EOF'
ADMIN_EMAIL=admin@meepleai.com
ADMIN_PASSWORD=$(openssl rand -base64 24)
EOF
```

#### 5.3 Configura docker-compose per Servizi Managed

Modifica `infra/docker-compose.yml` per usare servizi esterni invece di container locali:

```yaml
# Rimuovi o commenta i servizi postgres, redis, qdrant locali
# Mantieni solo: api, embedding-service, reranker-service, traefik, frontend
```

#### 5.4 Avvia Servizi

```bash
cd /home/meepleai/meepleai-monorepo-frontend/infra

# Avvia solo i servizi applicativi (DB sono managed)
docker compose up -d api embedding-service reranker-service

# Attendi avvio
sleep 60

# Verifica
docker compose ps
docker compose logs api | tail -50
```

#### 5.5 Configura Traefik per SSL

```bash
# Avvia Traefik
docker compose up -d traefik

# Verifica certificati SSL
docker compose logs traefik | grep -i "acme\|certificate"

# Dovrebbe mostrare: "certificate obtained for domain meepleai.com"
```

---

### Fase 6: Servizi Aggiuntivi (Day 3-4)

#### 6.1 Cloudflare R2 (Object Storage)

Per upload PDF:
- [ ] Cloudflare Dashboard → R2 → Create bucket
- [ ] Bucket name: `meepleai-uploads`
- [ ] Region: Automatic
- [ ] Crea API token con permessi R2

```bash
# Aggiungi a storage.secret
cat > storage.secret << 'EOF'
R2_ACCOUNT_ID=YOUR_ACCOUNT_ID
R2_ACCESS_KEY_ID=YOUR_ACCESS_KEY
R2_SECRET_ACCESS_KEY=YOUR_SECRET_KEY
R2_BUCKET_NAME=meepleai-uploads
R2_PUBLIC_URL=https://meepleai-uploads.YOUR_ACCOUNT_ID.r2.cloudflarestorage.com
EOF
```

#### 6.2 Resend (Email)

- [ ] Vai su https://resend.com
- [ ] Sign up
- [ ] Verifica dominio meepleai.com (aggiungi record DNS)
- [ ] Crea API key

```bash
cat > email.secret << 'EOF'
RESEND_API_KEY=re_xxxxx
EMAIL_FROM=noreply@meepleai.com
EOF
```

#### 6.3 Backup Automatici

```bash
# Hetzner Console → Servers → meepleai-prod-01 → Backups
# Abilita backup automatici (€3.08/mese aggiuntivi)

# Per backup database Neon:
# Neon fa backup automatici ogni 24h inclusi nel piano
```

---

### Fase 7: Ambiente Staging (Day 4)

#### 7.1 Strategia Staging su Stesso VPS

Usiamo Docker Compose profiles per separare staging/prod:

```yaml
# docker-compose.yml con profiles
services:
  api-prod:
    profiles: ["prod"]
    # ... config prod

  api-staging:
    profiles: ["staging"]
    # ... config staging con risorse ridotte
```

#### 7.2 Database Staging con Neon Branching

Neon supporta branching gratuito:
- [ ] Neon Dashboard → tuo progetto → Branches
- [ ] Crea branch: `staging` da `main`
- [ ] Usa connection string del branch per staging

#### 7.3 Comandi Deploy

```bash
# Deploy produzione
docker compose --profile prod up -d

# Deploy staging
docker compose --profile staging up -d

# Entrambi
docker compose --profile prod --profile staging up -d
```

---

### Fase 8: Verifica Finale (Day 4-5)

#### 8.1 Smoke Tests

```bash
# Health check API
curl https://api.meepleai.com/health
# Expected: {"status":"healthy",...}

# Health check staging
curl https://api-staging.meepleai.com/health

# Frontend
curl -I https://meepleai.com
# Expected: HTTP/2 200

# SSL verification
curl -v https://meepleai.com 2>&1 | grep "SSL\|TLS"
```

#### 8.2 Checklist Finale

- [ ] Dominio raggiungibile
- [ ] HTTPS funzionante (certificato valido)
- [ ] API health endpoint risponde
- [ ] Database connesso (check logs)
- [ ] Redis connesso
- [ ] Qdrant connesso
- [ ] Email funzionanti (test invio)
- [ ] Staging separato da prod
- [ ] Backup configurati
- [ ] Monitoring base attivo

---

## Riepilogo Credenziali da Salvare

| Servizio | Tipo | Dove Salvare |
|----------|------|--------------|
| Porkbun | Account login | Password manager |
| Cloudflare | Account login | Password manager |
| Hetzner | Account + SSH key | Password manager + ~/.ssh |
| Neon | Connection string | infra/secrets/database.secret |
| Upstash | Redis URL | infra/secrets/redis.secret |
| Qdrant | API Key | infra/secrets/qdrant.secret |
| Resend | API Key | infra/secrets/email.secret |
| R2 | Access keys | infra/secrets/storage.secret |

---

## Troubleshooting Comuni

### Errore: "Connection refused" su database

```bash
# Verifica che l'IP VPS sia nella whitelist Neon
# Neon Dashboard → Settings → IP Allow List → Add YOUR_VPS_IP
```

### Errore: SSL certificate non generato

```bash
# Verifica DNS propagato
dig api.meepleai.com

# Verifica porta 80 aperta
sudo ufw status

# Riavvia Traefik
docker compose restart traefik
```

### Errore: Redis connection timeout

```bash
# Upstash richiede TLS - verifica REDIS_TLS=true
# Verifica URL inizia con rediss:// (doppia s)
```

---

## Prossimi Passi

1. **Dopo setup**: Invita 5-10 beta tester
2. **Settimana 1**: Monitora logs e performance
3. **Settimana 2**: Raccogli feedback, fix bug critici
4. **Mese 1**: Valuta se scaling necessario

---

## Link Utili

- [Infrastructure Cost Summary](./infrastructure-cost-summary.md) - Analisi costi dettagliata
- [Infrastructure Deployment Checklist](./infrastructure-deployment-checklist.md) - Checklist Alpha (self-hosted)
- [Hetzner Cloud Console](https://console.hetzner.cloud)
- [Neon Dashboard](https://console.neon.tech)
- [Upstash Console](https://console.upstash.com)
- [Qdrant Cloud](https://cloud.qdrant.io)
- [Cloudflare Dashboard](https://dash.cloudflare.com)

---

**Creato**: 2026-01-27
**Target Budget**: €75-85/mese
**Fase**: Beta (5-20 utenti)

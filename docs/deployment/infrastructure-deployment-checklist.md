# Infrastructure Deployment Checklist

**Version**: 1.0
**Last Updated**: 2026-01-18
**Phase**: Alpha (10 users)
**Total Time**: 6-8 hours (first deployment)

---

## Pre-Deployment

**Budget & Accounts**:
- [ ] Budget approved: €20-25/month Alpha
- [ ] Domain decided: `meepleai.com`
- [ ] VPS provider: Hetzner Cloud
- [ ] Email: Personal or admin@domain
- [ ] Password manager ready
- [ ] Credit card for VPS

**Estimated Costs**:
- Hetzner VPS (CPX31): €15.41/month
- Domain (.com): €9.77/year (€0.81/month)
- **Total**: €19.30/month + €9.77 upfront

---

## Phase 1: Domain (2-3h)

### 1.1 Pre-Purchase (30min)
- [ ] Check availability: `whois meepleai.com`
- [ ] Trademark search: https://euipo.europa.eu/eSearch/
- [ ] Reserve social: @meepleai (Twitter, GitHub)
- [ ] Domain history: web.archive.org (should be clean)

### 1.2 Purchase (30min)
**Provider**: Cloudflare Registrar (€9.77/year)

- [ ] Create account: https://dash.cloudflare.com/sign-up
- [ ] Enable 2FA (mandatory)
- [ ] Search + purchase `meepleai.com`
- [ ] Enable auto-renewal ✅
- [ ] Verify: `whois meepleai.com | grep Cloudflare`

### 1.3 DNS (30min - after VPS)
**Wait**: Complete Phase 2 to get IP address

---

## Phase 2: VPS (1-2h)

### 2.1 Hetzner Account (15min)
- [ ] Sign up: https://console.hetzner.cloud/register
- [ ] Verify email
- [ ] Add payment method

### 2.2 Cloud Project (5min)
- [ ] New Project: `MeepleAI-Alpha`
- [ ] Datacenter: Falkenstein (EU) or Ashburn (US)

### 2.3 Provision VPS (15min)
| Setting | Value |
|---------|-------|
| **Location** | Falkenstein (fsn1) |
| **Image** | Ubuntu 22.04 LTS |
| **Type** | CPX31 (4 vCPU, 16GB RAM, 160GB NVMe) |
| **Networking** | IPv4 + IPv6 |
| **SSH** | Upload key OR password |
| **Name** | `meepleai-alpha-01` |
| **Labels** | `env=alpha`, `project=meepleai` |
| **Cost** | €15.41/month |

- [ ] Create server
- [ ] Copy IP: `95.217.xxx.xxx`

### 2.4 Initial Setup (10min)
```bash
ssh root@95.217.xxx.xxx

# Update + essentials
apt update && apt upgrade -y
apt install -y git curl nano htop ufw

# Firewall
ufw allow 22/tcp 80/tcp 443/tcp && ufw enable

# User
adduser meepleai
usermod -aG sudo meepleai
```

### 2.5 Install Docker (20min)
```bash
# Docker Engine
curl -fsSL https://get.docker.com | sh
apt install -y docker-compose-plugin

# Verify
docker --version && docker compose version
docker run hello-world

# User permissions
usermod -aG docker meepleai
systemctl enable docker && systemctl start docker
```

---

## Phase 3: DNS (30min)

**Add Records** (Cloudflare Dashboard):

| Type | Name | Value | Proxy |
|------|------|-------|-------|
| **A** | @ | `95.217.xxx.xxx` | ☁️ Proxied |
| **CNAME** | www | `meepleai.com` | ☁️ Proxied |
| **A** | api | `95.217.xxx.xxx` | 🌐 DNS Only |
| **CAA** | @ | `letsencrypt.org` | - |

- [ ] Enable DNSSEC (auto for Cloudflare Registrar)
- [ ] Verify propagation (1-2min): `dig @8.8.8.8 meepleai.com`

---

## Phase 4: Application (2-3h)

### 4.1 Clone Repo
```bash
ssh meepleai@95.217.xxx.xxx
cd /home/meepleai
git clone https://github.com/user/meepleai-monorepo.git
cd meepleai-monorepo
```

### 4.2 Secrets
```bash
cd infra/secrets
pwsh setup-secrets.ps1 -SaveGenerated

# Manual configs
nano openrouter.secret  # Add OPENROUTER_API_KEY
nano bgg.secret         # Add BGG_USERNAME, BGG_PASSWORD (optional)
```

### 4.3 Start Infrastructure
```bash
cd infra
docker compose up -d postgres redis qdrant
sleep 30

# Verify
docker ps | grep -E "postgres|redis|qdrant"
docker compose logs postgres redis qdrant | grep -i error
```

### 4.4 Migrations
```bash
cd apps/api/src/Api
dotnet ef database update

# Verify
docker exec -it infra-postgres-1 psql -U meepleai -d meepleai_db -c "\dt"
```

### 4.5 Start Apps
```bash
cd infra
docker compose up -d api embedding-service reranker-service
sleep 60 && docker compose ps
```

### 4.6 Traefik (SSL)
```bash
docker compose up -d traefik
sleep 120  # Wait for Let's Encrypt

# Verify SSL
docker compose logs traefik | grep -i "certificate obtained"
curl https://meepleai.com/health
```

---

## Phase 5: Email & 2FA (1-2h)

**SendGrid Setup** (45min):
- [ ] Create account (free tier)
- [ ] Generate API key
- [ ] Domain authentication (3 CNAME + SPF/DMARC)
- [ ] Test email delivery

**TOTP Implementation** (1-2h):
- Backend: `OtpNet`, `QRCoder` packages + endpoints
- Frontend: QR code display + verification
- See: `email-totp-services.md`

---

## Phase 6: Monitoring (1h)

### 6.1 Start Services
```bash
docker compose up -d grafana prometheus
# Grafana: http://localhost:3000 (admin/admin)
```

### 6.2 Import Dashboards
- [ ] PostgreSQL: Dashboard ID `9628`
- [ ] Docker: Dashboard ID `193`
- [ ] Traefik: Dashboard ID `11462`

### 6.3 Alerts
```yaml
alerts:
  - Database Down → pg_up == 0 → Critical
  - High Error Rate → rate(5xx) > 5% → Critical
  - API Latency → p95 > 1s → Warning
```

---

## Phase 7: Testing (1h)

### Smoke Tests
```bash
# DNS
nslookup meepleai.com → VPS IP

# HTTPS
curl -I http://meepleai.com → 301 → https
curl -v https://meepleai.com 2>&1 | grep TLS

# API
curl https://api.meepleai.com/health → {"status":"healthy"}
curl https://api.meepleai.com/scalar/v1 → HTML
```

### Functional Tests
- [ ] User registration → Email verification
- [ ] Login → Dashboard loads
- [ ] Upload PDF (small) → Processing → RAG query

---

## Phase 8: Backup (30min)

### 8.1 Hetzner Snapshots
- [ ] Console → Servers → Backups → Enable
- [ ] Frequency: Daily
- [ ] Retention: 7 days
- [ ] Cost: +€3.08/month

### 8.2 Manual Test
- [ ] Create snapshot: `meepleai-alpha-baseline-YYYYMMDD`
- [ ] Verify in Console → Snapshots

---

## Phase 9: Security (30min)

### SSH Hardening
```bash
nano /etc/ssh/sshd_config

# Change:
PasswordAuthentication no
PermitRootLogin no
PubkeyAuthentication yes

systemctl restart sshd
```

### Fail2Ban
```bash
apt install -y fail2ban
systemctl enable fail2ban && systemctl start fail2ban
```

### Auto-Updates
```bash
apt install -y unattended-upgrades
dpkg-reconfigure -plow unattended-upgrades
```

---

## Phase 10: Documentation (30min)

### Runbook Template
```markdown
# MeepleAI Alpha Runbook

**Deployed**: 2026-01-18
**Phase**: Alpha (10 users)

## Infrastructure
- VPS: Hetzner CPX31 (fsn1)
- IP: 95.217.xxx.xxx
- Domain: meepleai.com
- SSL: Let's Encrypt

## Credentials
- SSH: meepleai@IP (key: ~/.ssh/meepleai_rsa)
- Grafana: localhost:3000 (admin/password)
- PostgreSQL: localhost:5432 (password in database.secret)

## Services
- API: https://api.meepleai.com
- Docs: https://api.meepleai.com/scalar/v1

## Backup
- Auto: Daily snapshots (7d retention)
- Manual: /home/meepleai/backups/ (cron daily 3AM)

## Emergency
- Hetzner: support@hetzner.com
- On-call: [Your phone]
```

---

## Deployment Verification Checklist

### Pre-Launch
- [ ] All containers running (`docker compose ps`)
- [ ] Migrations applied (`dotnet ef database update`)
- [ ] Health 200 (`curl https://api.meepleai.com/health`)
- [ ] SSL valid (green padlock)
- [ ] Email delivery works
- [ ] 2FA functional
- [ ] Backups enabled
- [ ] Monitoring populated
- [ ] Secrets secured (.gitignore)
- [ ] Firewall configured (22, 80, 443)

### First 24h
- [ ] Watch errors: `docker compose logs -f api | grep -i error`
- [ ] CPU <40%: `htop`
- [ ] RAM <80%: `free -h`
- [ ] Disk <50%: `df -h`
- [ ] Test multi-device/network
- [ ] Grafana every 2-4h

### Week 1
- [ ] Invoice €15.41 (Hetzner)
- [ ] SendGrid usage <100 emails
- [ ] 7 snapshots created
- [ ] Test disaster recovery
- [ ] Document lessons learned

---

## Cost Summary

**One-Time**: €9.77 domain

**Monthly**:
| Item | Cost |
|------|------|
| Hetzner VPS (CPX31) | €15.41 |
| Backup | €3.08 |
| Domain (avg) | €0.81 |
| SendGrid | €0 (free) |
| **Total** | **€19.30** |

**First Month**: €29.07 (upfront + monthly)

---

## Troubleshooting

| Issue | Diagnosis | Solution |
|-------|-----------|----------|
| **Containers not starting** | `docker compose logs` | Fix port conflicts, secrets, RAM |
| **SSL fails** | `docker compose logs traefik` | Check firewall, DNS, ACME |
| **DB connection** | `docker logs infra-postgres-1` | Verify password, wait 60s init |

---

**See Also**: [Domain Setup](./domain-setup-guide.md) | [Email & TOTP](./email-totp-services.md) | [Monitoring](../04-deployment/monitoring/)

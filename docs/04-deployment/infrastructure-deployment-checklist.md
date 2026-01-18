# Infrastructure Deployment Checklist

**Version**: 1.0
**Last Updated**: 2026-01-18
**Estimated Total Time**: 6-8 hours (first deployment)
**Target Phase**: Alpha (10 users)

---

## Pre-Deployment Planning

**Before You Start**:
- [ ] Budget approved: €20-25/mese for Alpha
- [ ] Domain name decided: `meepleai.com` (recommended)
- [ ] VPS provider selected: Hetzner Cloud (recommended)
- [ ] Email for accounts: Use personal email initially
- [ ] Password manager ready: 1Password, Bitwarden, etc.
- [ ] Credit card for VPS payment

**Estimated Costs**:
- Hetzner VPS: €15.41/mese
- Domain: €9.77/anno (€0.81/mese)
- **Total**: €19.30/mese (first month) + €9.77 upfront domain

---

## Phase 1: Domain Acquisition (2-3 hours)

### Step 1.1: Pre-Purchase Checks (30 min)

**Checklist**:
- [ ] **Check availability**: `whois meepleai.com`
- [ ] **Trademark search**: https://euipo.europa.eu/eSearch/ → "MeepleAI"
- [ ] **Social media**: Reserve @meepleai on Twitter, GitHub, Instagram
- [ ] **Domain history**: https://web.archive.org/web/*/meepleai.com (should be empty)

**Decision**: If `meepleai.com` unavailable, alternatives:
- `meeple-ai.com`
- `meepleai.app`
- `getmeeple.com`

---

### Step 1.2: Purchase Domain (30 min)

**Provider**: Cloudflare Registrar (€9.77/anno)

**Procedure**:
1. [ ] Create Cloudflare account: https://dash.cloudflare.com/sign-up
2. [ ] Enable 2FA on Cloudflare account (mandatory!)
3. [ ] Navigate: Domain Registration → Search `meepleai.com`
4. [ ] Add to cart, enable auto-renewal ✅
5. [ ] Complete payment (€9.77)
6. [ ] Verify domain appears in dashboard

**Verification**:
```bash
whois meepleai.com | grep -i "cloudflare"
# Should show Cloudflare as registrar
```

---

### Step 1.3: Basic DNS Configuration (30 min)

**Wait for**: VPS provisioning (Step 2) to get IP address

**Placeholder**: Skip for now, return after Step 2.3

---

## Phase 2: VPS Provisioning (1-2 hours)

### Step 2.1: Create Hetzner Account (15 min)

1. [ ] Navigate: https://console.hetzner.cloud/register
2. [ ] Sign up with email (use admin@meepleai.com via Cloudflare forwarding)
3. [ ] Verify email
4. [ ] Add payment method (credit card or PayPal)

---

### Step 2.2: Create Cloud Project (5 min)

1. [ ] Hetzner Console → New Project
2. [ ] Project name: `MeepleAI-Alpha`
3. [ ] Location: Select datacenter

**Recommended Datacenter**:
- **EU**: Falkenstein, Germany (EU-Central) - Lowest latency for EU users
- **US**: Ashburn, VA (US-East) - If targeting US market

---

### Step 2.3: Provision VPS (15 min)

1. [ ] Console → Servers → Add Server
2. **Server Configuration**:
   - [ ] Location: Falkenstein (fsn1)
   - [ ] Image: Ubuntu 22.04 LTS
   - [ ] Type: **CPX31** (4 vCPU AMD, 16GB RAM, 160GB NVMe)
   - [ ] Networking: **IPv4** + IPv6 (both included)
   - [ ] SSH Key: Upload your public key OR use password
   - [ ] Server name: `meepleai-alpha-01`
   - [ ] Labels: `env=alpha`, `project=meepleai`

3. [ ] Click **Create & Buy Now**
4. [ ] Wait 30-60 seconds for provisioning
5. [ ] **Copy IP Address**: `95.217.163.246` (example, yours will differ)

**Cost Verification**: €15.41/mese (charged hourly, cancel anytime)

---

### Step 2.4: Initial Server Access (10 min)

**SSH into VPS**:
```bash
ssh root@95.217.163.246

# If using password, it was emailed to you
# If using SSH key, should connect directly
```

**First Login Setup**:
```bash
# Update system
apt update && apt upgrade -y

# Install essential tools
apt install -y git curl wget nano htop ufw

# Configure firewall
ufw allow 22/tcp    # SSH
ufw allow 80/tcp    # HTTP
ufw allow 443/tcp   # HTTPS
ufw enable

# Create non-root user
adduser meepleai
usermod -aG sudo meepleai
```

---

### Step 2.5: Install Docker (20 min)

```bash
# Install Docker Engine
curl -fsSL https://get.docker.com -o get-docker.sh
sh get-docker.sh

# Install Docker Compose V2
apt install -y docker-compose-plugin

# Verify installation
docker --version
docker compose version

# Add user to docker group
usermod -aG docker meepleai

# Start Docker service
systemctl enable docker
systemctl start docker
```

**Verification**:
```bash
docker run hello-world
# Should print: "Hello from Docker!"
```

---

## Phase 3: DNS Configuration (30 min)

**Now that you have VPS IP address**: `95.217.163.246` (example)

### Step 3.1: Add DNS Records

**Cloudflare Dashboard** → meepleai.com → DNS → Records:

1. **Root Domain (A Record)**:
   ```
   Type: A
   Name: @ (or meepleai.com)
   IPv4: 95.217.163.246  # Your VPS IP
   Proxy: ☁️ Proxied (orange cloud)
   TTL: Auto
   ```

2. **WWW Subdomain**:
   ```
   Type: CNAME
   Name: www
   Target: meepleai.com
   Proxy: ☁️ Proxied
   ```

3. **API Subdomain**:
   ```
   Type: A
   Name: api
   IPv4: 95.217.163.246
   Proxy: 🌐 DNS Only (gray cloud)  # Important for WebSocket
   ```

4. **CAA Record** (SSL security):
   ```
   Type: CAA
   Name: @
   Tag: issue
   Value: letsencrypt.org
   ```

---

### Step 3.2: Enable DNSSEC

1. [ ] Cloudflare → DNS → Settings → DNSSEC
2. [ ] Click "Enable DNSSEC"
3. [ ] Verify: Auto-configured for Cloudflare Registrar ✅

---

### Step 3.3: Verify DNS Propagation (15 min)

```bash
# Test from multiple DNS servers
dig @8.8.8.8 meepleai.com  # Google DNS
dig @1.1.1.1 meepleai.com  # Cloudflare DNS

# Both should return: 95.217.163.246
```

**Online Tool**: https://www.whatsmydns.net/#A/meepleai.com

**Expected**: 1-2 minutes for Cloudflare propagation

---

## Phase 4: Application Deployment (2-3 hours)

### Step 4.1: Clone Repository

```bash
# SSH into VPS as meepleai user
ssh meepleai@95.217.163.246

# Clone repository
cd /home/meepleai
git clone https://github.com/yourusername/meepleai-monorepo.git
cd meepleai-monorepo
```

---

### Step 4.2: Configure Secrets

**Run Secret Generation Script**:
```bash
cd infra/secrets
pwsh setup-secrets.ps1 -SaveGenerated

# Generates:
#  - database.secret (POSTGRES_PASSWORD, etc.)
#  - jwt.secret (JWT_SECRET_KEY)
#  - redis.secret (REDIS_PASSWORD)
#  - qdrant.secret (QDRANT_API_KEY)
#  - admin.secret (ADMIN_EMAIL, ADMIN_PASSWORD)
#  - embedding-service.secret (API key)
```

**Expected Output**:
```
✅ Generated 11 secure passwords
✅ Created 6 .secret files
✅ Backup saved: .generated-values-20260118.txt
```

**Manual Configuration Needed**:
```bash
# infra/secrets/openrouter.secret
nano openrouter.secret

# Add:
OPENROUTER_API_KEY=sk-or-v1-xxxxxxxxxxxxxxxx  # Get from openrouter.ai
OPENROUTER_DEFAULT_MODEL=anthropic/claude-3.5-sonnet

# infra/secrets/bgg.secret (optional for dev)
nano bgg.secret

# Add:
BGG_USERNAME=your_bgg_username
BGG_PASSWORD=your_bgg_password
```

---

### Step 4.3: Start Infrastructure Services

```bash
cd /home/meepleai/meepleai-monorepo/infra

# Start core services first
docker compose up -d postgres redis qdrant

# Wait 30 seconds for databases to initialize
sleep 30

# Verify services running
docker ps | grep -E "postgres|redis|qdrant"

# Check logs for errors
docker compose logs postgres | grep -i error
docker compose logs redis | grep -i error
docker compose logs qdrant | grep -i error
```

**Expected**: 3 containers running, no errors in logs

---

### Step 4.4: Run Database Migrations

```bash
cd apps/api/src/Api

# Apply migrations
dotnet ef database update

# Verify migration success
docker exec -it infra-postgres-1 psql -U meepleai -d meepleai_db -c "\dt"

# Should show tables: users, games, game_sessions, etc.
```

---

### Step 4.5: Start Application Services

```bash
cd /home/meepleai/meepleai-monorepo/infra

# Start API + Python services
docker compose up -d api embedding-service reranker-service

# Wait for services to start
sleep 60

# Verify
docker compose ps

# Expected: All services "Up" status
```

---

### Step 4.6: Start Traefik (Reverse Proxy + SSL)

```bash
# Start Traefik
docker compose up -d traefik

# Check Traefik dashboard (local access only)
curl http://localhost:8080/dashboard/

# Verify SSL certificate generation
docker compose logs traefik | grep -i "acme"

# Should see: "certificate obtained for domain meepleai.com"
```

**Wait**: 2-5 minutes for Let's Encrypt certificate generation

---

### Step 4.7: Health Check

**Test API Endpoint**:
```bash
# From VPS
curl http://localhost:8080/health
# Expected: {"status":"healthy","database":"healthy","redis":"healthy"}

# From public internet
curl https://meepleai.com/health
# Should work with HTTPS ✅

# Test API documentation
curl https://api.meepleai.com/scalar/v1
# Should return Scalar API docs HTML
```

---

## Phase 5: Email & 2FA Configuration (1-2 hours)

### Step 5.1: SendGrid Setup

**Follow**: [email-totp-services.md - Section 1.1](./email-totp-services.md#11-sendgrid-setup-alphabeta---free-tier)

**Quick Steps**:
1. [ ] Create SendGrid account
2. [ ] Generate API key
3. [ ] Authenticate domain (add 3 CNAME records to Cloudflare)
4. [ ] Add SPF/DMARC TXT records
5. [ ] Test email delivery

**Time**: 45 minutes

---

### Step 5.2: TOTP Implementation

**Backend**:
1. [ ] Install NuGet packages: `OtpNet`, `QRCoder`
2. [ ] Create `TotpService.cs` (see email-totp-services.md Section 2.2)
3. [ ] Add database migration for 2FA fields
4. [ ] Implement Enable/Verify TOTP endpoints

**Frontend**:
1. [ ] Create TOTP setup component (QR code display)
2. [ ] Add 2FA verification to login flow
3. [ ] Backup codes download feature

**Time**: 1-2 hours (if following guide)

---

## Phase 6: Monitoring Setup (1 hour)

### Step 6.1: Enable Grafana/Prometheus

```bash
cd infra
docker compose up -d grafana prometheus

# Access Grafana (local only, setup SSH tunnel for remote access)
# http://localhost:3000
# Default credentials: admin / admin (change on first login!)
```

---

### Step 6.2: Configure Dashboards

**Import Pre-built Dashboards**:

1. **PostgreSQL Dashboard**:
   - Grafana → Dashboards → Import
   - Dashboard ID: `9628` (PostgreSQL Database)
   - Data Source: Select Prometheus

2. **Docker Containers**:
   - Dashboard ID: `193` (Docker monitoring)

3. **Traefik Metrics**:
   - Dashboard ID: `11462` (Traefik 2.x)

**Custom Panels to Add**:
- [ ] API response time (p50, p95, p99)
- [ ] Database connection pool usage
- [ ] Redis cache hit rate
- [ ] Qdrant search latency

---

### Step 6.3: Setup Alerting

**Configure Email Alerts**:

1. Grafana → Alerting → Contact Points
2. **Add Contact Point**:
   - [ ] Name: `Email Alerts`
   - [ ] Integration: Email
   - [ ] Addresses: `admin@meepleai.com`

3. **Create Alert Rules**:

**Critical Alerts**:
```yaml
- Alert: Database Down
  Condition: pg_up == 0
  Duration: 1 minute
  Severity: Critical
  Action: Send email + log

- Alert: High Error Rate
  Condition: rate(http_requests_total{status=~"5.."}[5m]) > 0.05
  Duration: 5 minutes
  Severity: Critical

- Alert: API High Latency
  Condition: histogram_quantile(0.95, http_request_duration_seconds) > 1.0
  Duration: 10 minutes
  Severity: Warning
```

---

## Phase 7: Testing & Verification (1 hour)

### Step 7.1: Smoke Tests

**From Local Machine**:

```bash
# 1. Domain resolves
nslookup meepleai.com
# Expected: Your VPS IP

# 2. HTTPS redirect works
curl -I http://meepleai.com
# Expected: 301 Moved Permanently → https://meepleai.com

# 3. SSL certificate valid
curl -v https://meepleai.com 2>&1 | grep -i "ssl"
# Expected: TLS 1.3, certificate valid

# 4. API health check
curl https://api.meepleai.com/health
# Expected: {"status":"healthy",...}

# 5. API documentation accessible
curl https://api.meepleai.com/scalar/v1
# Should return HTML
```

---

### Step 7.2: Functional Tests

**User Registration Flow**:
1. [ ] Navigate: https://meepleai.com/register
2. [ ] Fill form, submit
3. [ ] Check email inbox for verification (SendGrid)
4. [ ] Click verification link
5. [ ] Account activated ✅

**Login Flow**:
1. [ ] Navigate: https://meepleai.com/login
2. [ ] Enter credentials
3. [ ] If 2FA enabled: Enter TOTP code
4. [ ] Dashboard loads ✅

**RAG Query Flow**:
1. [ ] Upload test PDF (small rulebook, <5MB)
2. [ ] Wait for processing (1-2 minutes)
3. [ ] Ask question: "How do I setup the game?"
4. [ ] Verify RAG response generated ✅

---

### Step 7.3: Load Testing (Optional)

**Light Load Test** (k6):
```javascript
// load-test.js
import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
  vus: 10,        // 10 virtual users
  duration: '2m', // 2 minutes
};

export default function () {
  let response = http.get('https://api.meepleai.com/health');
  check(response, { 'status is 200': (r) => r.status === 200 });
  sleep(1);
}
```

**Run**:
```bash
k6 run load-test.js

# Expected:
# - 95% requests < 500ms
# - 0% errors
# - CPU usage < 50%
```

---

## Phase 8: Backup & Monitoring (30 min)

### Step 8.1: Enable Automated Backups

**Hetzner Snapshots**:
1. [ ] Console → Servers → meepleai-alpha-01 → Backups
2. [ ] Enable automatic backups
3. [ ] Frequency: Daily
4. [ ] Retention: 7 days
5. [ ] Cost: +€3.08/mese

---

### Step 8.2: Manual Backup Test

```bash
# Create manual snapshot
# Hetzner Console → Servers → Actions → Create Snapshot
# Name: meepleai-alpha-baseline-20260118

# Verify snapshot created
# Console → Servers → Snapshots → Should show snapshot

# Test restoration (optional, creates new VPS):
# Console → Servers → Create from snapshot
# (Don't actually create unless testing, will charge for 2nd VPS)
```

---

### Step 8.3: Monitoring Verification

**Check Grafana Dashboards**:
1. [ ] SSH tunnel: `ssh -L 3000:localhost:3000 meepleai@95.217.163.246`
2. [ ] Browser: http://localhost:3000
3. [ ] Login: admin / [password set in setup]
4. [ ] Verify dashboards loading data:
   - [ ] PostgreSQL metrics showing
   - [ ] Docker containers visible
   - [ ] Traefik metrics present

---

## Phase 9: Security Hardening (30 min)

### Step 9.1: SSH Security

**Disable Password Authentication** (SSH key only):
```bash
# Edit SSH config
nano /etc/ssh/sshd_config

# Change:
PasswordAuthentication no
PermitRootLogin no
PubkeyAuthentication yes

# Restart SSH
systemctl restart sshd
```

**Verify**: Try SSH with password (should fail), SSH with key (should work)

---

### Step 9.2: Fail2Ban (Brute-Force Protection)

```bash
# Install Fail2Ban
apt install -y fail2ban

# Configure
cat > /etc/fail2ban/jail.local <<EOF
[sshd]
enabled = true
port = 22
maxretry = 3
bantime = 3600
findtime = 600

[traefik-auth]
enabled = true
port = http,https
maxretry = 5
bantime = 1800
EOF

# Start Fail2Ban
systemctl enable fail2ban
systemctl start fail2ban

# Verify
fail2ban-client status
```

---

### Step 9.3: Automatic Security Updates

```bash
# Install unattended-upgrades
apt install -y unattended-upgrades

# Enable automatic security updates
dpkg-reconfigure -plow unattended-upgrades

# Verify config
cat /etc/apt/apt.conf.d/50unattended-upgrades | grep -i security
# Should be enabled for security updates
```

---

## Phase 10: Documentation & Handoff (30 min)

### Step 10.1: Document Deployed Infrastructure

**Create Runbook**: `docs/runbook-alpha.md`

```markdown
# MeepleAI Alpha Runbook

**Deployed**: 2026-01-18
**Phase**: Alpha (10 users)

## Infrastructure

- **VPS**: Hetzner CPX31 (fsn1-dc14)
- **IP Address**: 95.217.163.246
- **Domain**: meepleai.com (Cloudflare)
- **SSL**: Let's Encrypt (auto-renewed)

## Credentials

- VPS SSH: meepleai@95.217.163.246 (key: ~/.ssh/meepleai_rsa)
- Grafana: http://localhost:3000 (admin / [password])
- PostgreSQL: localhost:5432 (meepleai / [password in database.secret])

## Services

- API: https://api.meepleai.com
- Docs: https://api.meepleai.com/scalar/v1
- Grafana: SSH tunnel to localhost:3000
- Traefik Dashboard: http://localhost:8080/dashboard/

## Backup

- Automated: Daily snapshots (7-day retention)
- Manual: Hetzner Console → Snapshots
- Database dump: `/home/meepleai/backups/` (cron: daily 3 AM)

## Emergency Contacts

- Hetzner Support: support@hetzner.com
- Cloudflare Support: community.cloudflare.com
- On-call: [Your phone number]
```

---

### Step 10.2: Create Disaster Recovery Plan

**Scenario: VPS Fails Completely**

**Recovery Steps** (2-4h RTO):
```bash
# 1. Provision new VPS (same specs)
# 2. Restore latest snapshot
# 3. Verify services start
# 4. Update DNS if IP changed
# 5. Test functionality
```

**RPO** (Recovery Point Objective): 24 hours (daily snapshots)

---

## Deployment Checklist Summary

### Quick Verification (Use this for every deployment)

**Pre-Launch Checklist**:
- [ ] All Docker containers running (`docker compose ps`)
- [ ] Database migrations applied (`dotnet ef database update`)
- [ ] Health endpoint returns 200 (`curl https://api.meepleai.com/health`)
- [ ] SSL certificate valid (green padlock in browser)
- [ ] Email delivery works (send test verification email)
- [ ] TOTP 2FA setup flow functional (scan QR, verify code)
- [ ] Backup enabled (Hetzner snapshot + daily DB dump)
- [ ] Monitoring dashboards populated (Grafana loading data)
- [ ] Secrets secured (all .secret files gitignored)
- [ ] Firewall configured (ufw status shows 22, 80, 443 allowed)

---

**Post-Launch Monitoring** (First 24h):
- [ ] Watch error logs: `docker compose logs -f api | grep -i error`
- [ ] Monitor CPU: `htop` (should be <40% average)
- [ ] Check RAM: `free -h` (should be <80% usage)
- [ ] Verify disk space: `df -h` (should be <50% usage)
- [ ] Test from multiple devices/networks
- [ ] Monitor Grafana dashboards every 2-4 hours
- [ ] Check email deliverability (send test to Gmail, Outlook, Yahoo)

---

**Week 1 Review**:
- [ ] Review Hetzner invoice (verify €15.41 charged correctly)
- [ ] Check SendGrid usage (should be <100 emails if low traffic)
- [ ] Verify backup snapshots created (7 snapshots in Hetzner Console)
- [ ] Test disaster recovery (restore snapshot to test VPS)
- [ ] Document any issues encountered
- [ ] Update runbook with lessons learned

---

## Estimated Costs Summary

**One-Time Costs** (Month 0):
| Item | Cost |
|------|------|
| Domain (meepleai.com) | €9.77 |
| **Total Upfront** | **€9.77** |

**Recurring Monthly Costs**:
| Item | Cost |
|------|------|
| Hetzner VPS (CPX31) | €15.41 |
| Hetzner Backup | €3.08 |
| Domain (monthly equivalent) | €0.81 |
| Email (SendGrid Free) | €0 |
| 2FA (Self-hosted) | €0 |
| **Total Monthly** | **€19.30** |

**First Month Total**: €9.77 + €19.30 = **€29.07**

**Subsequent Months**: €19.30/mese

---

## Troubleshooting Common Issues

### Issue: Docker containers not starting

**Diagnosis**:
```bash
docker compose logs [service-name]
```

**Common Causes**:
1. **Port conflict**: Another service using port 5432, 6379, 8080
   - Solution: `netstat -tlnp | grep [port]` → Kill conflicting process
2. **Secret not found**: Missing .secret file
   - Solution: Run `pwsh infra/secrets/setup-secrets.ps1`
3. **Insufficient RAM**: Docker out of memory
   - Solution: Reduce container memory limits in docker-compose.yml

---

### Issue: SSL certificate not generated

**Diagnosis**:
```bash
docker compose logs traefik | grep -i acme
```

**Common Causes**:
1. **Port 80 blocked**: Firewall blocking Let's Encrypt challenge
   - Solution: `ufw allow 80/tcp`
2. **DNS not propagated**: Domain doesn't point to VPS yet
   - Solution: Wait 24h, verify with `dig meepleai.com`
3. **Domain verification failed**: Cloudflare proxy interfering
   - Solution: Temporarily disable proxy (gray cloud) during certificate generation

---

### Issue: Database connection failed

**Diagnosis**:
```bash
docker exec -it infra-postgres-1 psql -U meepleai -d meepleai_db

# If fails: Check container logs
docker logs infra-postgres-1
```

**Common Causes**:
1. **Wrong password**: database.secret not loaded
   - Solution: Verify `POSTGRES_PASSWORD` in .secret file matches connection string
2. **Container not ready**: PostgreSQL still initializing
   - Solution: Wait 60 seconds after `docker compose up -d postgres`
3. **Port conflict**: Another PostgreSQL running on port 5432
   - Solution: `sudo lsof -i :5432` → Stop conflicting process

---

## Next Steps After Deployment

### Immediate (Day 1):
1. [ ] Send test emails to team members (verify deliverability)
2. [ ] Create admin user and test 2FA setup
3. [ ] Upload 1-2 test PDFs (verify processing pipeline)
4. [ ] Monitor logs for first 24h (catch early issues)

### Week 1:
1. [ ] Configure custom email templates (branding)
2. [ ] Setup monitoring alerts (Grafana → Alerting)
3. [ ] Document API endpoints (Scalar auto-docs)
4. [ ] Create user onboarding guide

### Week 2-4:
1. [ ] Invite alpha users (10 target)
2. [ ] Collect feedback on performance, bugs
3. [ ] Monitor resource usage (CPU, RAM, disk)
4. [ ] Plan Beta scaling trigger (>50 users)

---

**Reference Documents**:
- [Domain Setup Guide](./domain-setup-guide.md)
- [Email & TOTP Services](./email-totp-services.md)
- [Infrastructure Cost Summary](./infrastructure-cost-summary.md)
- [Monitoring Setup](../04-deployment/monitoring/README.md)


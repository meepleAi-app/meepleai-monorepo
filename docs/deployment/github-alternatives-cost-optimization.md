# GitHub Alternatives & Cost Optimization Guide

**Version**: 1.0
**Last Updated**: 2026-01-23
**Status**: Active
**Audience**: DevOps, Engineering Leadership

---

## 📋 Executive Summary

Comprehensive analysis of GitHub Free tier limits and zero-cost alternatives for private repository hosting during alpha/beta phases.

**Key Findings**:
- GitHub Free: **$0/month** (with optimizations or self-hosted runner)
- GitLab Free: **$0/month** (with self-hosted runner)
- Forgejo Self-Hosted: **$0/month** (full control, higher maintenance)

**Recommended**: GitHub Free + Oracle Always Free self-hosted runner

---

## 🎯 Scenario Analysis

### Current MeepleAI Monorepo Context

**Requirements**:
- 1 developer (solo/alpha phase)
- Private repository
- CI/CD automation (backend, frontend, E2E tests)
- Issues & Wiki integration
- Code review workflow (Claude-powered)
- Zero-cost priority for alpha/beta

**Current Workflow**:
```yaml
# .github/workflows/
backend-ci.yml    # .NET 9 build + test
frontend-ci.yml   # Next.js build + lint
e2e-tests.yml     # Playwright E2E tests
```

---

## 📊 GitHub Free Tier Limits (2026)

### Official Limits

| Resource | Quota | Overage Cost | Source |
|----------|-------|--------------|--------|
| **Actions Minutes** | 2,000 min/month | **$0.002/min** (from Jan 2026) | [GitHub Actions Billing](https://docs.github.com/billing/managing-billing-for-github-actions/about-billing-for-github-actions) |
| **Storage** | 500 MB total* | $0.008/GB/day | [GitHub Pricing](https://github.com/pricing) |
| **Bandwidth** | Unlimited | Unlimited | [GitHub Plans](https://docs.github.com/get-started/learning-about-github/githubs-products) |
| **Collaborators** | Unlimited | Free | [GitHub Plans](https://docs.github.com/get-started/learning-about-github/githubs-products) |

**\*Storage Shared Across**: GitHub Packages + Actions artifacts + Actions caches

### 2026 Pricing Changes

**Critical**: New pricing model effective January 1, 2026:
- **Cloud runners**: $0.002/minute for ALL workflows (public repos exempt)
- **Self-hosted runners**: Included in free quota from March 1, 2026
- **Impact**: Self-hosted runners become zero-cost solution

**Source**: [GitHub Actions 2026 Pricing Changes](https://resources.github.com/actions/2026-pricing-changes-for-github-actions/)

---

## 🧮 MeepleAI Monthly Consumption Analysis

### Estimated CI/CD Usage

Based on current workflow configuration:

```yaml
# Workflow Estimates (30 commits/month scenario)
backend-ci.yml:    15 min/run × 30 runs = 450 min/month
frontend-ci.yml:   10 min/run × 30 runs = 300 min/month
e2e-tests.yml:     20 min/run × 30 runs = 600 min/month
                                   TOTAL: 1,350 min/month
```

**Status**: ✅ **Within 2,000 min quota** → **$0 Actions cost**

### High-Activity Scenario (60 commits/month)

```yaml
backend-ci.yml:    15 min × 60 = 900 min
frontend-ci.yml:   10 min × 60 = 600 min
e2e-tests.yml:     20 min × 60 = 1,200 min
                         TOTAL: 2,700 min/month
```

**Overage**: 700 min × $0.002 = **$1.40/month** 💰

### Storage Analysis

```yaml
Docker layer caches:  ~200 MB
Build artifacts:      ~150 MB
NuGet packages:       ~100 MB
                TOTAL: 450 MB
```

**Status**: ✅ **Within 500 MB quota** → **$0 Storage cost**

---

## 🎯 Three Zero-Cost Alternatives

### Option A: GitHub Free + Self-Hosted Runner (RECOMMENDED)

**Strategy**: Eliminate Actions costs via Oracle Always Free Tier VM

#### Oracle Always Free Tier Specs
- **ARM64 VM**: 4 cores, 24 GB RAM
- **Block Storage**: 200 GB
- **Network**: 10 TB/month outbound
- **Cost**: **$0/month forever** (no credit card expiry)

#### Setup Process (2-3 hours)

**Step 1**: Create Oracle Cloud Account
```bash
# Visit: https://signup.cloud.oracle.com
# Select: Always Free tier
# Region: Choose closest datacenter
```

**Step 2**: Provision ARM VM
```bash
# In Oracle Console:
# Compute → Instances → Create Instance
# - Image: Ubuntu 22.04 ARM64
# - Shape: VM.Standard.A1.Flex (4 cores, 24GB)
# - Network: Public IP enabled
# - SSH Key: Upload your public key
```

**Step 3**: Install GitHub Actions Runner
```bash
# SSH into VM
ssh ubuntu@<VM_PUBLIC_IP>

# Download runner (ARM64)
mkdir actions-runner && cd actions-runner
curl -L https://github.com/actions/runner/releases/download/v2.321.0/actions-runner-linux-arm64-2.321.0.tar.gz | tar xz

# Configure runner
./config.sh --url https://github.com/YOUR_USERNAME/meepleai-monorepo-dev --token YOUR_RUNNER_TOKEN

# Install as service
sudo ./svc.sh install
sudo ./svc.sh start
```

**Step 4**: Update Workflows
```yaml
# .github/workflows/backend-ci.yml
name: Backend CI

on:
  push:
    branches: [main-dev, main]
    paths:
      - 'apps/api/**'
      - 'apps/embedding-service/**'
      - 'apps/reranker-service/**'
  pull_request:
    paths:
      - 'apps/api/**'

jobs:
  build:
    runs-on: [self-hosted, linux, ARM64]  # ← Changed from ubuntu-latest

    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - name: Build
        run: dotnet build apps/api/src/Api
      - name: Test
        run: dotnet test apps/api/src/Api
```

#### Optimization Techniques

**1. Conditional Workflows** (40% reduction)
```yaml
# Only run when relevant files change
on:
  push:
    paths:
      - 'apps/api/**'      # Backend changes only
      - 'infra/**'          # Infrastructure changes
  pull_request:
    paths:
      - 'apps/api/**'
```

**2. Docker Layer Caching** (30% reduction)
```yaml
- name: Build Docker
  uses: docker/build-push-action@v5
  with:
    context: .
    cache-from: type=gha
    cache-to: type=gha,mode=max
```

**3. Parallel Jobs** (25% time reduction)
```yaml
jobs:
  backend:
    runs-on: [self-hosted, linux, ARM64]
    # Backend tests

  frontend:
    runs-on: [self-hosted, linux, ARM64]
    # Frontend tests (runs in parallel)
```

#### Cost Analysis

| Component | Cost | Notes |
|-----------|------|-------|
| Oracle VM | $0/month | Always Free tier |
| GitHub Actions | $0/month | Self-hosted = unlimited |
| Storage | $0/month | Within 500MB quota |
| **Total** | **$0/month** | Zero ongoing costs |

#### Pros & Cons

✅ **Advantages**:
- Zero migration effort
- Unlimited CI/CD minutes
- Maintain Claude code review integration
- Keep Issues/Wiki workflow
- Upgrade path to paid if needed

⚠️ **Considerations**:
- Initial setup: 2-3 hours
- Runner maintenance: updates, monitoring
- Single point of failure (mitigated by Oracle SLA)

---

### Option B: GitLab SaaS Free + Self-Hosted Runner

**Strategy**: Leverage GitLab's 10GB storage + self-hosted runner for unlimited CI/CD

#### GitLab Free Tier Limits

| Resource | GitLab Free | MeepleAI Need | Gap |
|----------|-------------|---------------|-----|
| CI/CD Minutes | 400/month | 1,350/month | ❌ Insufficient |
| Storage | 10 GB | ~1 GB | ✅ Sufficient |
| Users | 5 max | 1 | ✅ OK |
| Self-Hosted Runners | **Unlimited** | Required | ✅ Solution |

**Sources**:
- [GitLab Pricing](https://about.gitlab.com/pricing/)
- [GitLab Storage Docs](https://docs.gitlab.com/user/storage_usage_quotas/)

#### Setup Process (3-4 hours)

**Step 1**: Migrate Repository
```bash
# Export from GitHub
git clone --mirror https://github.com/YOUR_USERNAME/meepleai-monorepo-dev.git
cd meepleai-monorepo-dev.git

# Push to GitLab
git remote add gitlab https://gitlab.com/YOUR_USERNAME/meepleai-monorepo-dev.git
git push --mirror gitlab
```

**Step 2**: Install GitLab Runner (Oracle VM)
```bash
# SSH into Oracle VM
ssh ubuntu@<VM_PUBLIC_IP>

# Install GitLab Runner
curl -L "https://packages.gitlab.com/install/repositories/runner/gitlab-runner/script.deb.sh" | sudo bash
sudo apt-get install gitlab-runner

# Register runner
sudo gitlab-runner register
# URL: https://gitlab.com
# Token: [From GitLab Project → Settings → CI/CD → Runners]
# Executor: docker
# Default image: mcr.microsoft.com/dotnet/sdk:9.0
```

**Step 3**: Convert Workflows to GitLab CI
```yaml
# .gitlab-ci.yml (equivalent to GitHub Actions)
stages:
  - build
  - test
  - deploy

backend-ci:
  stage: build
  image: mcr.microsoft.com/dotnet/sdk:9.0
  tags:
    - self-hosted  # Use your Oracle VM runner
  script:
    - cd apps/api/src/Api
    - dotnet restore
    - dotnet build
    - dotnet test
  only:
    changes:
      - apps/api/**

frontend-ci:
  stage: build
  image: node:20-alpine
  tags:
    - self-hosted
  script:
    - cd apps/web
    - npm ci
    - npm run build
    - npm run test
  only:
    changes:
      - apps/web/**
```

#### Cost Analysis

| Component | Cost | Notes |
|-----------|------|-------|
| GitLab SaaS | $0/month | Free tier |
| Oracle VM Runner | $0/month | Always Free |
| **Total** | **$0/month** | Zero costs |

#### Pros & Cons

✅ **Advantages**:
- 10GB storage (20× vs GitHub)
- Unlimited CI/CD with runner
- Built-in container registry
- Issues/Wiki included

⚠️ **Considerations**:
- Migration effort: 3-4 hours
- Learn GitLab CI syntax
- Lose Claude code review integration (requires webhook setup)
- Different UI/workflow paradigm

---

### Option C: Forgejo Self-Hosted + Woodpecker CI

**Strategy**: Full self-hosted stack on Oracle Always Free infrastructure

#### Forgejo vs Gitea

**Forgejo** = Community-driven fork of Gitea (October 2022)

| Aspect | Forgejo | Gitea |
|--------|---------|-------|
| **Governance** | Non-profit (Codeberg e.V.) | For-profit company |
| **License** | Pure FOSS | Open Core model |
| **Development** | 100% free software | GitHub-based (proprietary CI) |
| **Security** | Public disclosure | Customers-first disclosure |
| **Testing** | E2E + upgrade tests | Limited browser tests |
| **Federation** | Active development | No federation work |

**Source**: [Forgejo vs Gitea Comparison](https://forgejo.org/compare-to-gitea/)

#### Architecture

```
Oracle Always Free VM (ARM64, 4 cores, 24GB RAM)
├── Forgejo (Git hosting + Issues + Wiki)
├── Woodpecker CI (pipeline automation)
├── PostgreSQL (metadata storage)
└── 200GB Block Volume (repositories + artifacts)
```

#### Setup Process (4-5 hours)

**Step 1**: Docker Compose Stack
```yaml
# docker-compose.yml
version: '3.8'

services:
  forgejo:
    image: codeberg.org/forgejo/forgejo:8
    container_name: forgejo
    restart: always
    ports:
      - "3000:3000"
      - "22:22"
    volumes:
      - forgejo-data:/data
      - /etc/timezone:/etc/timezone:ro
      - /etc/localtime:/etc/localtime:ro
    environment:
      - USER_UID=1000
      - USER_GID=1000
      - FORGEJO__database__DB_TYPE=postgres
      - FORGEJO__database__HOST=postgres:5432
      - FORGEJO__database__NAME=forgejo
      - FORGEJO__database__USER=forgejo
      - FORGEJO__database__PASSWD=secure_password
    depends_on:
      - postgres

  postgres:
    image: postgres:16-alpine
    restart: always
    volumes:
      - postgres-data:/var/lib/postgresql/data
    environment:
      - POSTGRES_USER=forgejo
      - POSTGRES_PASSWORD=secure_password
      - POSTGRES_DB=forgejo

  woodpecker-server:
    image: woodpeckerci/woodpecker-server:latest
    restart: always
    ports:
      - "8000:8000"
    volumes:
      - woodpecker-data:/var/lib/woodpecker
    environment:
      - WOODPECKER_OPEN=true
      - WOODPECKER_HOST=http://your-domain.com:8000
      - WOODPECKER_FORGEJO=true
      - WOODPECKER_FORGEJO_URL=http://forgejo:3000
      - WOODPECKER_FORGEJO_CLIENT=your_oauth_client_id
      - WOODPECKER_FORGEJO_SECRET=your_oauth_secret
      - WOODPECKER_ADMIN=your_forgejo_username
      - WOODPECKER_AGENT_SECRET=shared_secret_with_agents
    depends_on:
      - forgejo

  woodpecker-agent:
    image: woodpeckerci/woodpecker-agent:latest
    restart: always
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
    environment:
      - WOODPECKER_SERVER=woodpecker-server:9000
      - WOODPECKER_AGENT_SECRET=shared_secret_with_agents
    depends_on:
      - woodpecker-server

volumes:
  forgejo-data:
  postgres-data:
  woodpecker-data:
```

**Step 2**: Initialize Forgejo
```bash
# Start stack
docker compose up -d

# Access Forgejo web installer
# http://<VM_IP>:3000/install
# Complete setup wizard (admin account, database config)
```

**Step 3**: Configure Woodpecker CI
```bash
# In Forgejo: Settings → Applications → Create OAuth2 Application
# Name: Woodpecker CI
# Redirect URI: http://<VM_IP>:8000/authorize

# Update docker-compose.yml with OAuth credentials
# Restart services
docker compose down && docker compose up -d
```

**Step 4**: Create Pipeline Config
```yaml
# .woodpecker.yml (root of repository)
when:
  branch: [main-dev, main]
  path:
    include:
      - 'apps/api/**'
      - 'apps/web/**'

pipeline:
  backend-build:
    image: mcr.microsoft.com/dotnet/sdk:9.0
    commands:
      - cd apps/api/src/Api
      - dotnet restore
      - dotnet build
      - dotnet test
    when:
      path:
        include: ['apps/api/**']

  frontend-build:
    image: node:20-alpine
    commands:
      - cd apps/web
      - npm ci
      - npm run build
      - npm run test
    when:
      path:
        include: ['apps/web/**']

  e2e-tests:
    image: mcr.microsoft.com/playwright:v1.40.0
    commands:
      - cd apps/web
      - npm ci
      - npx playwright install
      - npm run test:e2e
    when:
      event: [push]
      branch: [main-dev, main]
```

#### Cost Analysis

| Component | Cost | Notes |
|-----------|------|-------|
| Oracle VM | $0/month | Always Free |
| Forgejo | $0/month | Open source |
| Woodpecker CI | $0/month | Open source |
| Storage (200GB) | $0/month | Always Free |
| **Total** | **$0/month** | Zero costs |

#### Pros & Cons

✅ **Advantages**:
- **Unlimited everything**: CI/CD, storage, bandwidth
- **Full privacy**: Self-hosted, no cloud vendor
- **Community-driven**: Forgejo governance model
- **Issues/Wiki**: Built-in, no external tools
- **Federation**: Future ActivityPub support
- **No vendor lock-in**: Standard Git protocol

⚠️ **Considerations**:
- **Setup complexity**: 4-5 hours initial configuration
- **Maintenance burden**: Security updates, backups, monitoring
- **No managed services**: You're the SRE
- **Claude integration**: Custom webhooks required
- **Learning curve**: New UI, Woodpecker CI syntax
- **Single VM risk**: Backup strategy essential

---

## 🔀 Decision Matrix

### Comparison Table

| Criterion | GitHub + Runner | GitLab + Runner | Forgejo + Woodpecker |
|-----------|-----------------|-----------------|----------------------|
| **Monthly Cost** | $0 | $0 | $0 |
| **Setup Time** | 2-3 hours | 3-4 hours | 4-5 hours |
| **Maintenance** | Low (runner only) | Low (runner only) | **High** (full stack) |
| **CI/CD Minutes** | Unlimited* | Unlimited* | **Unlimited** |
| **Storage** | 500 MB | 10 GB | **200+ GB** |
| **Issues/Wiki** | ✅ Native | ✅ Native | ✅ Native |
| **Claude Review** | ✅ Integrated | ⚠️ Webhook | ⚠️ Webhook |
| **Privacy** | ⚠️ Microsoft cloud | ⚠️ GitLab cloud | ✅ **Full control** |
| **Migration Effort** | None | 1-2 hours | 2-3 hours |
| **Vendor Lock-in** | Medium | Medium | **None** |
| **Backup Complexity** | Low (managed) | Low (managed) | **High** (self-managed) |

**\*With self-hosted runner**

### Scoring System (0-10 scale)

| Aspect | Weight | GitHub | GitLab | Forgejo |
|--------|--------|--------|--------|---------|
| **Cost** | 30% | 10 | 10 | 10 |
| **Setup Ease** | 20% | 9 | 7 | 5 |
| **Maintenance** | 25% | 8 | 8 | 4 |
| **Features** | 15% | 8 | 9 | 10 |
| **Integration** | 10% | 10 | 6 | 5 |
| **Total** | - | **8.75** | **8.05** | **6.85** |

---

## 🏆 Recommendation

### For MeepleAI Alpha/Beta Phase: GitHub Free + Self-Hosted Runner

**Rationale**:

1. **Zero Migration** → Start saving immediately, no workflow disruption
2. **CI/CD Unlimited** → Oracle Always Free runner = no Actions costs
3. **Claude Integration** → Code review automation continues working
4. **Issues/Wiki** → Existing workflow unchanged
5. **Upgrade Path** → Easy transition to paid GitHub if needed
6. **Risk Mitigation** → Oracle Always Free has no expiration date

### Implementation Timeline

```
Week 1 (2-3 hours):
├─ Day 1: Oracle Cloud account setup (30 min)
├─ Day 2: VM provisioning + runner installation (1.5 hours)
└─ Day 3: Workflow updates + testing (1 hour)

Week 2 (monitoring):
└─ Validate: Zero Actions usage on GitHub, runner functioning
```

### Success Metrics

- ✅ **Cost Reduction**: GitHub Actions bill = $0
- ✅ **CI/CD Performance**: Build times ≤ current
- ✅ **Reliability**: >99% runner uptime
- ✅ **Developer Experience**: No workflow disruption

---

## 🔄 Fallback Strategies

### Scenario 1: Oracle Always Free Unavailable

**Problem**: Oracle suspends Always Free tier or regions full

**Fallback Options**:
1. **Hetzner Cloud** ($4.49/month for CX22: 2 vCPU, 4GB RAM)
2. **DigitalOcean** ($6/month for Basic Droplet: 1 vCPU, 1GB RAM)
3. **GitLab SaaS + Runner** (see Option B)

**Decision**: If cost <$5/month acceptable, use Hetzner; otherwise GitLab

### Scenario 2: Self-Hosted Runner Complexity Too High

**Problem**: Runner maintenance becomes time-consuming

**Fallback**:
- Optimize GitHub workflows to stay under 2,000 min/month
- Use conditional paths, cache Docker layers
- Accept occasional $1-2/month overage cost

### Scenario 3: Need Full Privacy/Control

**Problem**: Compliance requires self-hosted Git

**Solution**: Implement Option C (Forgejo + Woodpecker)
- Budget 1 day/quarter for maintenance
- Implement automated backups
- Document runbooks for recovery

---

## 🛠️ Implementation Guides

### Quick Start: GitHub + Self-Hosted Runner

**Prerequisites**:
- Oracle Cloud account (free tier)
- GitHub personal access token
- SSH key pair

**Step-by-Step**:

```bash
# 1. Provision Oracle VM (via web console)
# - Compartment: Create new or use root
# - Image: Canonical Ubuntu 22.04 (ARM64)
# - Shape: VM.Standard.A1.Flex (4 OCPU, 24GB)
# - Network: Create new VCN with public subnet
# - SSH: Upload your public key

# 2. Connect to VM
ssh ubuntu@<VM_PUBLIC_IP>

# 3. Update system
sudo apt-get update && sudo apt-get upgrade -y

# 4. Install Docker (for containerized builds)
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker ubuntu
newgrp docker

# 5. Download GitHub Actions Runner
mkdir ~/actions-runner && cd ~/actions-runner
curl -L https://github.com/actions/runner/releases/download/v2.321.0/actions-runner-linux-arm64-2.321.0.tar.gz | tar xz

# 6. Configure runner
./config.sh \
  --url https://github.com/YOUR_USERNAME/meepleai-monorepo-dev \
  --token YOUR_RUNNER_TOKEN \
  --name oracle-arm-runner \
  --labels self-hosted,linux,ARM64 \
  --work _work

# 7. Install as systemd service
sudo ./svc.sh install
sudo ./svc.sh start
sudo ./svc.sh status  # Verify running

# 8. Update GitHub workflows
# Change: runs-on: ubuntu-latest
# To:     runs-on: [self-hosted, linux, ARM64]
```

**Validation**:
```bash
# On VM: Check runner logs
cd ~/actions-runner
tail -f _diag/Runner_*.log

# On GitHub: Trigger workflow
git commit -m "test: validate self-hosted runner" --allow-empty
git push

# Verify: Actions tab shows "self-hosted" runner
```

### Troubleshooting Guide

#### Issue: Runner Not Appearing in GitHub

**Symptoms**: `./svc.sh status` shows "active", but GitHub shows offline

**Solutions**:
```bash
# Check network connectivity
curl -I https://github.com

# Verify token not expired
./config.sh remove
./config.sh --url ... --token NEW_TOKEN

# Check firewall rules
sudo ufw status  # Should allow outbound HTTPS
```

#### Issue: Build Failures on ARM64

**Symptoms**: Docker builds fail with "platform mismatch"

**Solutions**:
```yaml
# Dockerfile: Specify ARM64 base images
FROM --platform=linux/arm64 mcr.microsoft.com/dotnet/sdk:9.0

# docker-compose.yml: Add platform
services:
  app:
    platform: linux/arm64
    image: myapp:latest
```

#### Issue: Disk Space Exhaustion

**Symptoms**: Runner fails with "no space left"

**Solutions**:
```bash
# Check usage
df -h

# Clean Docker
docker system prune -af --volumes

# Expand block volume (Oracle Console)
# - Block Storage → Boot Volume → Edit → Increase size
```

---

## 📚 Additional Resources

### Official Documentation

- [GitHub Actions Self-Hosted Runners](https://docs.github.com/en/actions/hosting-your-own-runners)
- [Oracle Cloud Always Free Tier](https://www.oracle.com/cloud/free/)
- [GitLab Runner Installation](https://docs.gitlab.com/runner/install/)
- [Forgejo Documentation](https://forgejo.org/docs/)
- [Woodpecker CI Docs](https://woodpecker-ci.org/docs/)

### Community Resources

- [Awesome Self-Hosted](https://github.com/awesome-selfhosted/awesome-selfhosted)
- [GitHub Actions Runner Controller](https://github.com/actions/actions-runner-controller) (Kubernetes)
- [Forgejo vs Gitea Discussion](https://news.ycombinator.com/item?id=33091097)

### Monitoring & Observability

**Runner Health Monitoring**:
```bash
# Systemd status
sudo systemctl status actions.runner.*

# Resource usage
htop
docker stats

# Disk usage
du -sh ~/actions-runner/_work
```

**Alerting** (optional):
```bash
# Install Prometheus Node Exporter
wget https://github.com/prometheus/node_exporter/releases/download/v1.7.0/node_exporter-1.7.0.linux-arm64.tar.gz
tar xvfz node_exporter-*.*-arm64.tar.gz
cd node_exporter-*.*-arm64
./node_exporter &

# Grafana dashboard: Import ID 1860 (Node Exporter Full)
```

---

## 📅 Review Schedule

**Monthly** (5 min):
- Check Oracle VM status and billing (should be $0)
- Verify runner uptime and job success rate
- Review GitHub Actions usage (should show 0 minutes consumed)

**Quarterly** (30 min):
- Runner software updates (`./svc.sh stop`, download new version, `./svc.sh start`)
- Ubuntu security patches (`sudo apt-get update && sudo apt-get upgrade`)
- Backup configuration files (runner tokens, docker configs)

**Annual** (2 hours):
- Reassess alternatives (GitHub pricing changes, new platforms)
- Capacity planning (storage growth, compute needs)
- Disaster recovery test (restore from backup)

---

## 📞 Support & Questions

**Primary Contact**: DevOps Team
**Documentation Owner**: Engineering Lead
**Last Reviewed**: 2026-01-23

**Related Documentation**:
- [Deployment Guide](README.md)
- [Secret Management](secrets/README.md)
- [CI/CD Pipeline](../development/README.md#cicd-pipeline)

---

**Version History**:
- **v1.0** (2026-01-23): Initial documentation - GitHub alternatives analysis

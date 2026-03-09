# MeepleAI Production Deployment Guide

> **Domain**: www.meepleai.io
> **Stack**: Docker + Traefik + Let's Encrypt
> **Last Updated**: 2026-01-20

---

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Architecture](#architecture)
4. [Server Setup](#server-setup)
5. [DNS Configuration](#dns-configuration)
6. [Secrets Setup](#secrets-setup)
7. [Deployment](#deployment)
8. [Post-Deployment](#post-deployment)
9. [Maintenance](#maintenance)
10. [Troubleshooting](#troubleshooting)

---

## Overview

### Domain Structure

| Subdomain | Service | Port |
|-----------|---------|------|
| `www.meepleai.io` | Next.js Frontend | 3000 |
| `meepleai.io` | Redirect to www | - |
| `api.meepleai.io` | .NET API Backend | 8080 |
| `grafana.meepleai.io` | Monitoring Dashboard | 3000 |
| `traefik.meepleai.io` | Reverse Proxy Admin | 8080 |

### Features

- ✅ Automatic HTTPS via Let's Encrypt
- ✅ HTTP to HTTPS redirect
- ✅ Security headers (OWASP)
- ✅ Rate limiting per endpoint
- ✅ Docker Socket Proxy (security)
- ✅ Centralized logging
- ✅ Health checks
- ✅ Resource limits

---

## Prerequisites

### Server Requirements

| Resource | Minimum | Recommended |
|----------|---------|-------------|
| CPU | 4 cores | 8 cores |
| RAM | 16 GB | 32 GB |
| Storage | 100 GB SSD | 250 GB NVMe |
| OS | Ubuntu 22.04 LTS | Ubuntu 24.04 LTS |

### Software Requirements

```bash
# Docker
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER

# Docker Compose (included with Docker 20.10+)
docker compose version

# Other tools
sudo apt update
sudo apt install -y git htpasswd curl jq
```

### Firewall Rules

```bash
# UFW example
sudo ufw allow 22/tcp    # SSH
sudo ufw allow 80/tcp    # HTTP (for Let's Encrypt)
sudo ufw allow 443/tcp   # HTTPS
sudo ufw enable
```

---

## Architecture

```
                    ┌─────────────────────────────────────────────────┐
                    │                   INTERNET                       │
                    └─────────────────────┬───────────────────────────┘
                                          │
                           ┌──────────────▼──────────────┐
                           │     Traefik (Reverse Proxy)  │
                           │     - SSL Termination        │
                           │     - Rate Limiting          │
                           │     - Security Headers       │
                           └──────────────┬──────────────┘
                                          │
        ┌─────────────────────────────────┼─────────────────────────────────┐
        │                                 │                                 │
        ▼                                 ▼                                 ▼
┌───────────────┐               ┌───────────────┐               ┌───────────────┐
│   Web (Next.js)│               │   API (.NET)  │               │   Grafana     │
│   Port 3000   │               │   Port 8080   │               │   Port 3000   │
└───────────────┘               └───────┬───────┘               └───────────────┘
                                        │
                ┌───────────────────────┼───────────────────────┐
                │                       │                       │
                ▼                       ▼                       ▼
        ┌───────────┐           ┌───────────┐           ┌───────────┐
        │ PostgreSQL│           │   Redis   │           │  Qdrant   │
        │ Port 5432 │           │ Port 6379 │           │ Port 6333 │
        └───────────┘           └───────────┘           └───────────┘
```

---

## Server Setup

### 1. Clone Repository

```bash
# SSH to your server
ssh user@your-server-ip

# Clone repository
git clone https://github.com/DegrassiAaron/meepleai-monorepo.git /opt/meepleai
cd /opt/meepleai
```

### 2. Create System User (Optional but Recommended)

```bash
# Create dedicated user
sudo useradd -r -s /bin/false meepleai

# Set ownership
sudo chown -R meepleai:meepleai /opt/meepleai

# Add current user to meepleai group
sudo usermod -aG meepleai $USER
```

### 3. Create Required Directories

```bash
cd /opt/meepleai/infra

# Traefik directories
mkdir -p traefik/letsencrypt
mkdir -p traefik/logs
chmod 600 traefik/letsencrypt

# Backup directory
mkdir -p ../backups
```

---

## DNS Configuration

### Required DNS Records

Configure these records at your DNS provider (Cloudflare, Route53, etc.):

| Type | Name | Value | TTL |
|------|------|-------|-----|
| A | `@` | `YOUR_SERVER_IP` | 300 |
| A | `www` | `YOUR_SERVER_IP` | 300 |
| A | `api` | `YOUR_SERVER_IP` | 300 |
| A | `grafana` | `YOUR_SERVER_IP` | 300 |
| A | `traefik` | `YOUR_SERVER_IP` | 300 |

### Verify DNS Propagation

```bash
# Check DNS resolution
dig +short meepleai.io
dig +short www.meepleai.io
dig +short api.meepleai.io

# All should return your server IP
```

> ⚠️ **Wait for DNS propagation** (5-30 minutes) before proceeding with Let's Encrypt.

---

## Secrets Setup

### 1. Generate Production Secrets

```bash
cd /opt/meepleai/infra/secrets/prod

# Make script executable
chmod +x setup-prod-secrets.sh

# Run generator
./setup-prod-secrets.sh
```

### 2. Configure Required Secrets

Edit these files with your actual values:

```bash
# OpenRouter API Key (REQUIRED)
nano openrouter-api-key.txt
# Enter: sk-or-v1-your-actual-api-key

# Gmail App Password (if using email)
nano gmail-app-password.txt
# Generate at: https://myaccount.google.com/apppasswords
```

### 3. Update Traefik Dashboard Password

```bash
# Get the generated password
cat traefik-dashboard-password.txt

# Generate bcrypt hash
htpasswd -nbB admin "$(cat traefik-dashboard-password.txt)"

# Copy the output and update middlewares.prod.yml
nano ../traefik/dynamic/middlewares.prod.yml
# Find line: - "admin:$$2y$$05$$REPLACE_WITH_BCRYPT_HASH"
# Replace with actual hash (double $$ signs)
```

### 4. Verify Secrets

```bash
# List all secrets
ls -la /opt/meepleai/infra/secrets/prod/

# Verify permissions (should be 600)
stat -c "%a %n" *.txt
```

---

## Deployment

### 1. Update Email in Traefik Config

```bash
nano /opt/meepleai/infra/traefik/traefik.prod.yml

# Find and update:
# email: "admin@meepleai.io"  # Change to your email
```

### 2. Deploy with Script

```powershell
cd /opt/meepleai

# Start deployment
pwsh scripts/deployment/deploy-meepleai.ps1 up
```

### 3. Alternative: Manual Deployment

```bash
cd /opt/meepleai/infra

# Start all services
docker compose \
  -f docker-compose.yml \
  -f compose.traefik.yml \
  -f compose.prod.yml \
  -f compose.meepleai.yml \
  --profile full \
  up -d
```

### 4. Verify Deployment

```bash
# Check service status
docker compose -f docker-compose.yml -f compose.traefik.yml -f compose.prod.yml -f compose.meepleai.yml ps

# Check Traefik logs for certificate issuance
docker logs meepleai-traefik -f

# Test HTTPS
curl -I https://www.meepleai.io
curl -I https://api.meepleai.io/health
```

---

## Post-Deployment

### 1. Verify SSL Certificates

```bash
# Check certificate
echo | openssl s_client -servername www.meepleai.io -connect www.meepleai.io:443 2>/dev/null | openssl x509 -noout -dates

# Should show valid dates from Let's Encrypt
```

### 2. Create Admin User

```bash
# Access API to create first admin
curl -X POST https://api.meepleai.io/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@meepleai.io",
    "password": "YOUR_SECURE_PASSWORD",
    "displayName": "Admin"
  }'
```

### 3. Access Monitoring

1. Open `https://grafana.meepleai.io`
2. Login with:
   - Username: `admin`
   - Password: `cat /opt/meepleai/infra/secrets/prod/grafana-admin-password.txt`
3. Import dashboards from `infra/dashboards/`

### 4. Setup Backups (Cron)

```bash
# Edit crontab
crontab -e

# Add daily backup at 3 AM
0 3 * * * pwsh /opt/meepleai/scripts/deployment/deploy-meepleai.ps1 backup >> /var/log/meepleai-backup.log 2>&1
```

---

## Maintenance

### View Logs

```bash
# All services
pwsh scripts/deployment/deploy-meepleai.ps1 logs

# Specific service
pwsh scripts/deployment/deploy-meepleai.ps1 logs api
pwsh scripts/deployment/deploy-meepleai.ps1 logs web
pwsh scripts/deployment/deploy-meepleai.ps1 logs traefik
```

### Restart Services

```bash
pwsh scripts/deployment/deploy-meepleai.ps1 restart
```

### Update Application

```bash
cd /opt/meepleai

# Pull latest code
git pull origin main

# Update and restart
pwsh scripts/deployment/deploy-meepleai.ps1 update
```

### Manual Database Backup

```bash
pwsh scripts/deployment/deploy-meepleai.ps1 backup
```

### Certificate Renewal

Let's Encrypt certificates auto-renew via Traefik. To force renewal:

```bash
# Remove existing certificates
rm -rf /opt/meepleai/infra/traefik/letsencrypt/acme.json

# Restart Traefik
docker restart meepleai-traefik
```

---

## Troubleshooting

### Certificate Issues

```bash
# Check Traefik logs
docker logs meepleai-traefik 2>&1 | grep -i "acme\|certificate\|error"

# Common issues:
# - DNS not propagated: wait and retry
# - Port 80 blocked: check firewall
# - Rate limit: wait 1 hour (Let's Encrypt limit)
```

### Service Not Starting

```bash
# Check specific service logs
docker logs meepleai-api
docker logs meepleai-web

# Check container status
docker inspect meepleai-api | jq '.[0].State'
```

### Database Connection Issues

```bash
# Test PostgreSQL connection
docker exec -it meepleai-postgres psql -U meeple -d meepleai_prod -c "SELECT 1"

# Check credentials
cat /opt/meepleai/infra/secrets/prod/postgres-password.txt
```

### Performance Issues

```bash
# Check resource usage
docker stats

# Check disk space
df -h

# Check memory
free -h
```

### Reset Everything

```bash
# Stop all services
pwsh scripts/deployment/deploy-meepleai.ps1 down

# Remove volumes (WARNING: data loss!)
docker volume rm $(docker volume ls -q | grep meepleai)

# Restart
pwsh scripts/deployment/deploy-meepleai.ps1 up
```

---

## Security Checklist

- [ ] All secrets are unique and secure (32+ chars)
- [ ] Traefik dashboard has authentication
- [ ] Firewall allows only ports 80, 443, 22
- [ ] Docker socket proxy is enabled
- [ ] HTTPS is enforced
- [ ] Security headers are active
- [ ] Rate limiting is configured
- [ ] Regular backups are scheduled
- [ ] Monitoring is active
- [ ] Log rotation is configured

---

## Support

- **Issues**: https://github.com/DegrassiAaron/meepleai-monorepo/issues
- **Documentation**: https://github.com/DegrassiAaron/meepleai-monorepo/tree/main/docs

---

## Quick Reference

```bash
# Start
pwsh scripts/deployment/deploy-meepleai.ps1 up

# Stop
pwsh scripts/deployment/deploy-meepleai.ps1 down

# Logs
pwsh scripts/deployment/deploy-meepleai.ps1 logs

# Status
pwsh scripts/deployment/deploy-meepleai.ps1 status

# Backup
pwsh scripts/deployment/deploy-meepleai.ps1 backup

# Update
pwsh scripts/deployment/deploy-meepleai.ps1 update
```

---

**Happy Deploying! 🎲🚀**

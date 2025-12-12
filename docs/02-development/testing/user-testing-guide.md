# User Testing Guide - MeepleAI

**Complete guide for building, deploying, and testing all user features via web interface**

**Last Updated**: 2025-12-12  
**Status**: Active  
**Version**: 1.0

---

## 📋 Table of Contents

1. [Prerequisites](#prerequisites)
2. [Build & Deployment](#build--deployment)
3. [Complete Feature Test List](#complete-feature-test-list)
4. [Step-by-Step Testing Guide](#step-by-step-testing-guide)
5. [Monitoring Tools](#monitoring-tools)
6. [Validation Checklist](#validation-checklist)
7. [Troubleshooting](#troubleshooting)

---

## 🔧 Prerequisites

### Required Software

- **Docker Desktop** (for Windows/Mac) or Docker Engine (Linux)
- **Node.js** 18+ and **pnpm** package manager
- **.NET 9 SDK**
- **Git**

### Verify Installations

```bash
docker --version   # Docker version 20.10+
node --version     # Node v18+
pnpm --version     # pnpm 8+
dotnet --version   # .NET 9.0+
```

---

## 🚀 Build & Deployment

### Step 1: Environment Setup

```bash
# Navigate to infrastructure directory
cd infra

# Copy environment template
cp .env.development.example .env.development
```

**Edit `infra/.env.development`** with your configuration:

```bash
# OpenRouter API Key (get from https://openrouter.ai/)
OPENROUTER_API_KEY=sk-or-v1-your-key-here

# Initial admin credentials
INITIAL_ADMIN_EMAIL=admin@meepleai.local
INITIAL_ADMIN_PASSWORD=Admin123!@#

# Database connections (default values work for Docker)
ConnectionStrings__Postgres=Host=postgres;Port=5432;Database=meepleai;Username=meepleai;Password=meepleai123
QDRANT_URL=http://qdrant:6333
REDIS_URL=redis:6379
```

### Step 2: Start Docker Stack (Minimal Mode)

```bash
# From infra directory
./start-minimal.sh

# Or manually with Docker Compose
docker compose --profile minimal up -d
```

**Services started:**
- PostgreSQL (port 5432)
- Redis (port 6379)
- Qdrant (port 6333)
- API Backend (port 8080)
- Web Frontend (port 3000)

**Estimated time:** 2-3 minutes for first-time pull and start.

### Step 3: Verify Services Health

```bash
# Check all containers are running
docker compose ps

# Verify API health endpoint
curl http://localhost:8080/health

# Expected response: {"status":"Healthy","checks":[...]}
```

### Step 4: Alternative - Local Development Mode

If you prefer running services locally without Docker:

**Backend (API):**
```bash
cd apps/api/src/Api
dotnet restore
dotnet build
dotnet run
# API available at http://localhost:8080
```

**Frontend (Web):**
```bash
cd apps/web
pnpm install
pnpm dev
# Web available at http://localhost:3000
```

### Step 5: Access Application

Open your browser and navigate to:
- **Web UI**: http://localhost:3000
- **API**: http://localhost:8080
- **API Documentation**: http://localhost:8080/swagger (if enabled)

---

## 📝 Complete Feature Test List

### Authentication Features (6 tests)

| # | Feature | Endpoint | Method | Browser URL |
|---|---------|----------|--------|-------------|
| 1 | User Registration | `/api/v1/auth/register` | POST | `/register` |
| 2 | User Login | `/api/v1/auth/login` | POST | `/login` |
| 3 | User Logout | `/api/v1/auth/logout` | POST | Navbar menu |
| 4 | Enable 2FA | `/api/v1/auth/2fa/enable` | POST | `/settings` (Privacy tab) |
| 5 | Verify 2FA Code | `/api/v1/auth/2fa/verify` | POST | Login flow |
| 6 | OAuth Login | `/api/v1/auth/oauth/*` | GET | `/login` buttons |

### Profile Management (8 tests)

| # | Feature | Endpoint | Method | Browser URL |
|---|---------|----------|--------|-------------|
| 7 | View Profile | `/api/v1/user/profile` | GET | `/settings` (Profile tab) |
| 8 | Update Name/Email | `/api/v1/user/profile` | PUT | `/settings` (Profile tab) |
| 9 | Change Password | `/api/v1/user/password` | PUT | `/settings` (Profile tab) |
| 10 | Generate API Key | `/api/v1/apikeys` | POST | `/settings` (Advanced tab) |
| 11 | List API Keys | `/api/v1/apikeys` | GET | `/settings` (Advanced tab) |
| 12 | Revoke API Key | `/api/v1/apikeys/{id}` | DELETE | `/settings` (Advanced tab) |
| 13 | View Active Sessions | `/api/v1/sessions` | GET | `/settings` (Advanced tab) |
| 14 | Terminate Session | `/api/v1/sessions/{id}` | DELETE | `/settings` (Advanced tab) |

### Game Management (4 tests)

| # | Feature | Endpoint | Method | Browser URL |
|---|---------|----------|--------|-------------|
| 15 | List All Games | `/api/v1/games` | GET | `/games` |
| 16 | View Game Details | `/api/v1/games/{id}` | GET | `/games/{id}` |
| 17 | Search Games | `/api/v1/games/search` | GET | `/games` (search bar) |
| 18 | Create Game (Admin) | `/api/v1/games` | POST | Admin panel |

### PDF Document Processing (4 tests)

| # | Feature | Endpoint | Method | Browser URL |
|---|---------|----------|--------|-------------|
| 19 | Upload PDF Rulebook | `/api/v1/documents/upload` | POST | `/upload` |
| 20 | List Documents | `/api/v1/documents` | GET | `/documents` |
| 21 | View Document Details | `/api/v1/documents/{id}` | GET | `/documents/{id}` |
| 22 | Quality Report | `/api/v1/documents/{id}/quality` | GET | Document details page |

### RAG & Chat (5 tests)

| # | Feature | Endpoint | Method | Browser URL |
|---|---------|----------|--------|-------------|
| 23 | Ask Question | `/api/v1/chat` | POST | `/chat` |
| 24 | Streaming Chat | `/api/v1/chat/stream` | POST | `/chat` (SSE) |
| 25 | Hybrid Search | `/api/v1/search` | GET | Search interface |
| 26 | List Chat Threads | `/api/v1/chat/threads` | GET | `/chat/history` |
| 27 | View Thread History | `/api/v1/chat/threads/{id}` | GET | Thread detail |

### Administration (3 tests)

| # | Feature | Endpoint | Method | Browser URL |
|---|---------|----------|--------|-------------|
| 28 | View Configuration | `/admin/configuration` | GET | `/admin/configuration` |
| 29 | Update Configuration | `/admin/configuration` | PUT | `/admin/configuration` |
| 30 | System Statistics | `/admin/stats` | GET | `/admin/stats` |

**Total: 30 testable features**

---

## 🎯 Step-by-Step Testing Guide

See sections below for detailed step-by-step instructions for each test.

### Authentication Tests (1-6)

**Test 1: User Registration**

Via Browser:
1. Navigate to http://localhost:3000/register
2. Fill form: Email, Password, Display Name
3. Click "Register"
4. Verify redirect to login

Via API:
```bash
curl -X POST http://localhost:8080/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@meepleai.local","password":"Test123!@#","displayName":"Mario Rossi"}'
```

**Test 2: User Login**

Via Browser:
1. Navigate to http://localhost:3000/login
2. Enter email and password
3. Click "Login"
4. Verify redirect to dashboard

**Test 3-6**: Follow similar patterns for Logout, 2FA, and OAuth

---

## 📊 Monitoring Tools

| Tool | URL | Purpose |
|------|-----|---------|
| Grafana | http://localhost:3001 | Metrics dashboards |
| Prometheus | http://localhost:9090 | Time-series metrics |
| Qdrant UI | http://localhost:6333/dashboard | Vector database |
| Mailpit | http://localhost:8025 | Email testing |
| Traefik | http://traefik.localhost:8080 | Reverse proxy |

---

## ✅ Validation Checklist

### Pre-Deployment
- [ ] All Docker containers healthy
- [ ] API health check passes
- [ ] Frontend loads without errors
- [ ] Admin user can login

### Feature Validation
- [ ] Registration works
- [ ] Login/Logout functional
- [ ] Profile updates persist
- [ ] PDF upload completes
- [ ] Chat returns answers

---

## 🐛 Troubleshooting

### Common Issues

**CORS Errors**
```bash
# Check environment variable
echo $NEXT_PUBLIC_API_BASE
# Should be: http://localhost:8080
```

**401 Unauthorized**
- Check session cookie in browser DevTools
- Verify credentials: "include" in API calls

**PDF Upload Fails**
```bash
# Check Unstructured service
docker compose ps unstructured-service
docker compose restart unstructured-service
```

**Chat No Answer**
```bash
# Verify Qdrant has vectors
curl http://localhost:6333/collections/meepleai/points/count
```

---

## 📚 Additional Resources

- Architecture: `docs/01-architecture/overview/system-architecture.md`
- API Spec: `docs/03-api/board-game-ai-api-specification.md`
- RAG ADR: `docs/01-architecture/adr/adr-001-hybrid-rag.md`

---

**Version**: 1.0  
**Last Updated**: 2025-12-12  
**Maintained by**: Engineering Team

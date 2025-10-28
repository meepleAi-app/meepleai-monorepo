# 🧪 Testing Guide

Guida completa per testare l'ambiente GitHub Actions Simulator.

## 📋 Checklist Pre-Test

Prima di iniziare i test, verifica:

- [ ] Docker Desktop è in esecuzione
- [ ] File `config/.secrets` configurato con API keys reali
- [ ] Almeno 10GB di spazio disco disponibile
- [ ] Nessun conflitto di porte (5433, 6380, 6335, 6336, 9999)

## 🚀 Setup Iniziale

### Windows (PowerShell)

```powershell
cd github-actions-simulator
.\quickstart.ps1
```

### Linux/Mac

```bash
cd github-actions-simulator
./quickstart.sh
```

### Manuale

```bash
# 1. Setup secrets
cp config/.secrets.example config/.secrets
nano config/.secrets  # Inserisci la tua OPENROUTER_API_KEY

# 2. Build
make build

# 3. Start
make up

# 4. Health check
make health
```

## ✅ Test Base

### 1. Verifica Installazione Tools

```bash
# Entra nel container
docker compose exec act-runner bash

# Verifica tutti i tool
health-check.sh
```

**Output atteso:**
```
✅ act: v0.2.xx
✅ .NET SDK: 9.0.xxx
✅ Node.js: v20.x.x
✅ pnpm: 9.x.x
✅ k6: v0.53.0
✅ Semgrep: x.xx.x
✅ actionlint: x.x.x
✅ Docker: xx.xx.x
```

### 2. Verifica Servizi

```bash
# Dentro il container
health-check.sh
```

**Output atteso:**
```
✅ PostgreSQL: Connected
✅ Redis: Connected
✅ Qdrant: Connected
```

### 3. Validazione Workflow

```bash
# Dentro il container
validate-workflows.sh
```

**Output atteso:**
```
✅ Valid: ci.yml
✅ Valid: security-scan.yml
✅ Valid: semgrep.yml
✅ Valid: load-test.yml
🟢 All workflows are valid!
```

## 🧪 Test Workflow Singoli

### Test 1: CI Workflow (Backend)

```bash
# Opzione 1: Via Makefile (da fuori il container)
make test-api

# Opzione 2: Via script (dentro il container)
docker compose exec act-runner bash
run-job.sh .github/workflows/ci.yml ci-api push
```

**Durata attesa:** ~5-8 minuti

**Cosa verifica:**
- ✅ Build del backend .NET
- ✅ Restore NuGet packages
- ✅ Esecuzione test xUnit
- ✅ Testcontainers (PostgreSQL, Qdrant)

**Log atteso (finale):**
```
✅ Job 'ci-api' completed successfully!
🟢 Status: PASSED
```

### Test 2: CI Workflow (Frontend)

```bash
# Opzione 1: Via Makefile
make test-web

# Opzione 2: Via script
docker compose exec act-runner bash
run-job.sh .github/workflows/ci.yml ci-web push
```

**Durata attesa:** ~3-5 minuti

**Cosa verifica:**
- ✅ Lint (ESLint)
- ✅ Typecheck (TypeScript)
- ✅ Test (Jest)
- ✅ Build (Next.js)

**Log atteso (finale):**
```
✅ Job 'ci-web' completed successfully!
🟢 Status: PASSED
```

### Test 3: Security Scan

```bash
# Via Makefile
make test-security

# Via script
docker compose exec act-runner bash
run-workflow.sh .github/workflows/security-scan.yml push
```

**Durata attesa:** ~10-15 minuti (prima volta)

**Cosa verifica:**
- ✅ CodeQL SAST (C#, TypeScript)
- ✅ Vulnerability scan (dotnet list package, pnpm audit)

**Note:** CodeQL potrebbe non funzionare completamente in locale (limitazione di act)

### Test 4: Semgrep SAST

```bash
# Via Makefile
make test-semgrep

# Via script
docker compose exec act-runner bash
run-workflow.sh .github/workflows/semgrep.yml push
```

**Durata attesa:** ~5-7 minuti

**Cosa verifica:**
- ✅ SAST con Semgrep
- ✅ Security patterns (OWASP, CWE)
- ✅ C# e TypeScript specific rules

**Output atteso:**
```
✅ Workflow completed successfully!
📦 Artifacts: /artifacts/semgrep-results/
```

## 🔍 Debugging Test Falliti

### 1. Visualizza Log Completi

```bash
# Ultimo log CI
view-logs.sh ci --full

# Log specifico per data
view-logs.sh 20241026 --full

# Log di un job specifico
view-logs.sh ci-api --full
```

### 2. Controlla Artifacts

```bash
# Lista artifacts generati
ls -la /artifacts/

# Vedi contenuto specifico
cat /artifacts/semgrep-results/semgrep.json | jq .
```

### 3. Controlla Servizi

```bash
# Da fuori il container
docker compose logs postgres
docker compose logs redis
docker compose logs qdrant

# Verifica connessioni
docker compose exec act-runner bash
pg_isready -h postgres -p 5432 -U meeple
redis-cli -h redis ping
curl http://qdrant:6333/healthz
```

### 4. Web UI per Log Real-Time

Apri nel browser:
```
http://localhost:9999
```

Seleziona il container `github-actions-simulator` e filtra per "error" o "fail".

## 📊 Test Performance

### Cache Hit Rate

```bash
# Primo run (cold cache)
time make test-api

# Secondo run (warm cache)
time make test-api

# Il secondo run dovrebbe essere ~50% più veloce
```

**Tempi attesi:**
- Cold cache: ~8-10 minuti
- Warm cache: ~4-5 minuti

### Disk Usage

```bash
# Controlla dimensioni
docker compose exec act-runner bash
du -sh /logs /artifacts /cache

# Cleanup se necessario
cleanup.sh 3  # Rimuovi file > 3 giorni
```

## 🧹 Pulizia Post-Test

### Pulizia Soft (mantieni cache)

```bash
# Rimuovi solo log e artifacts vecchi
make clean
```

### Pulizia Completa

```bash
# Stop servizi
make down

# Rimuovi volumi
docker compose down -v

# Rimuovi tutti i file temporanei
rm -rf logs/* artifacts/* cache/* .act/*
```

## 🐛 Troubleshooting Comuni

### ❌ Error: "Cannot connect to Docker daemon"

**Causa:** Docker Desktop non è in esecuzione.

**Soluzione:**
```bash
# Windows: Avvia Docker Desktop
# Linux: sudo systemctl start docker
# Mac: Avvia Docker Desktop
```

### ❌ Error: "Port already in use"

**Causa:** Porta già occupata (es. PostgreSQL sulla 5433).

**Soluzione:**
```bash
# Trova il processo
netstat -ano | findstr :5433  # Windows
lsof -i :5433                  # Linux/Mac

# Stoppa MeepleAI principale se in esecuzione
cd ../infra
docker compose down
```

### ❌ Error: "act: not found"

**Causa:** act non è installato o il PATH non è configurato.

**Soluzione:**
```bash
# Rebuild del container
make reset
```

### ❌ Test fallisce con "OPENROUTER_API_KEY not set"

**Causa:** Secret non configurato correttamente.

**Soluzione:**
```bash
# Controlla il file secrets
cat config/.secrets

# Deve contenere:
OPENROUTER_API_KEY=sk-or-v1-your-actual-key-here

# Non deve contenere "placeholder" o "example"
```

### ❌ Warning: "Some services may not be ready yet"

**Causa:** Servizi ancora in startup.

**Soluzione:**
```bash
# Attendi 30 secondi e riprova
sleep 30
make health
```

## 📈 Metriche di Successo

Un test è considerato **green** quando:

✅ Tutti i tool sono installati (`health-check.sh` passa)
✅ Tutti i servizi sono connessi (PostgreSQL, Redis, Qdrant)
✅ Workflow validation passa (0 errori)
✅ Almeno un job completo passa (ci-api o ci-web)
✅ Log mostrano "Status: PASSED"
✅ Exit code = 0

## 🎯 Test Scenario Completo

Esegui questa sequenza per un test completo:

```bash
# 1. Setup iniziale
make setup

# 2. Health check
make health

# 3. Valida workflow
make validate

# 4. Test backend
make test-api

# 5. Test frontend
make test-web

# 6. Security scan
make test-semgrep

# 7. Visualizza risultati
make view-logs

# 8. Cleanup
make clean
```

**Durata totale:** ~25-30 minuti (prima volta)
**Durata successiva:** ~15-20 minuti (con cache)

## 📝 Report Test

Dopo aver completato i test, genera un report:

```bash
# Dentro il container
cat > /logs/test-report.md << EOF
# Test Report - $(date)

## Environment
- act version: $(act --version)
- Docker version: $(docker --version)
- .NET version: $(dotnet --version)

## Tests Executed
- [ ] ci-api: $(test -f /logs/*ci-api*.log && echo "✅ PASSED" || echo "❌ FAILED")
- [ ] ci-web: $(test -f /logs/*ci-web*.log && echo "✅ PASSED" || echo "❌ FAILED")
- [ ] security-scan: $(test -f /logs/*security*.log && echo "✅ PASSED" || echo "❌ FAILED")
- [ ] semgrep: $(test -f /logs/*semgrep*.log && echo "✅ PASSED" || echo "❌ FAILED")

## Artifacts Generated
$(ls -lh /artifacts/)

## Disk Usage
$(du -sh /logs /artifacts /cache)
EOF

cat /logs/test-report.md
```

## 🆘 Support

Se i test continuano a fallire:

1. Controlla i log dettagliati: `view-logs.sh <pattern> --full`
2. Verifica Docker: `docker compose ps`
3. Reset completo: `make reset`
4. Controlla documentazione: `README.md`

## ✨ Best Practices

1. **Test incrementali**: Inizia con `validate`, poi `test-api`, poi gli altri
2. **Usa cache**: Non fare `reset` se non necessario
3. **Monitor risorse**: Controlla `docker stats` durante i test
4. **Log review**: Usa Dozzle (http://localhost:9999) per log real-time
5. **Cleanup regolare**: Esegui `make clean` settimanalmente

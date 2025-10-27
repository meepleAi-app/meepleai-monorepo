# 🎭 GitHub Actions Simulator - Summary

## ✅ Cosa è stato creato

Un ambiente Docker **completamente isolato** per simulare GitHub Actions localmente, con logging completo e web UI.

## 📂 Struttura Completa

```
github-actions-simulator/
├── 📄 README.md                    # Documentazione principale
├── 📄 TESTING.md                   # Guida ai test con esempi
├── 📄 SUMMARY.md                   # Questo file
├── 📄 Makefile                     # Comandi semplificati
├── 📄 .gitignore                   # Protezione secrets
├── 🐳 Dockerfile                   # Immagine con tutti i tool
├── 🐳 docker-compose.yml           # Orchestrazione servizi
├── 🚀 quickstart.ps1               # Setup rapido Windows
├── 🚀 quickstart.sh                # Setup rapido Linux/Mac
│
├── 📁 scripts/                     # Script helper
│   ├── run-workflow.sh             # Esegui workflow completo
│   ├── run-job.sh                  # Esegui singolo job
│   ├── view-logs.sh                # Visualizza log con filtri
│   ├── validate-workflows.sh       # Valida sintassi YAML
│   ├── health-check.sh             # Verifica ambiente
│   └── cleanup.sh                  # Pulisci log/artifacts
│
├── 📁 config/                      # Configurazione
│   ├── .actrc                      # Variabili ambiente
│   ├── .secrets                    # Secrets (gitignored)
│   └── .secrets.example            # Template secrets
│
├── 📁 logs/                        # Log persistenti (gitignored)
├── 📁 artifacts/                   # Artifacts GitHub Actions (gitignored)
├── 📁 cache/                       # Cache act (gitignored)
└── 📁 .act/                        # Runtime act (gitignored)
```

## 🛠️ Strumenti Inclusi

| Tool | Versione | Scopo |
|------|----------|-------|
| **act** | Latest | Simulatore GitHub Actions |
| **.NET SDK** | 9.0.x | Build e test backend |
| **Node.js** | 20.x | Build e test frontend |
| **pnpm** | 9.x | Package manager frontend |
| **k6** | 0.53.0 | Load testing |
| **Semgrep** | Latest | SAST security scanning |
| **actionlint** | Latest | Workflow YAML validation |
| **Docker** | In-container | Docker-in-Docker |
| **PostgreSQL** | 16.4 | Database per test |
| **Redis** | 7.4.1 | Cache per test |
| **Qdrant** | 1.12.4 | Vector DB per test |
| **Dozzle** | Latest | Web UI per log |

## 🚀 Quick Start

### Windows

```powershell
cd github-actions-simulator
.\quickstart.ps1
```

### Linux/Mac

```bash
cd github-actions-simulator
./quickstart.sh
```

### Comandi Base

```bash
# Avvia ambiente
make up

# Verifica salute
make health

# Valida workflow
make validate

# Test CI completo
make test-ci

# Test solo backend
make test-api

# Test solo frontend
make test-web

# Visualizza log
make view-logs

# Entra nel container
make shell

# Stop tutto
make down
```

## 📊 Cosa Puoi Fare

### ✅ Test Pre-Push

```bash
# Testa che tutto sia green prima del push
make validate      # Valida sintassi
make test-ci       # Esegui CI completo
make view-logs     # Controlla errori
```

### ✅ Debug Workflow

```bash
# Testa singolo job problematico
make test-api                              # Solo backend
docker compose exec act-runner bash        # Entra nel container
view-logs.sh ci-api --full                 # Log dettagliato
```

### ✅ Security Scan Locale

```bash
# Esegui security scan prima del push
make test-security    # CodeQL
make test-semgrep     # Semgrep SAST
```

### ✅ Test Modifiche Workflow

```bash
# Dopo aver modificato .github/workflows/*.yml
make validate                              # Valida sintassi
make test-ci                               # Testa esecuzione
```

## 🌐 Web Interfaces

### Dozzle (Log Viewer)

```
http://localhost:9999
```

- ✅ Log in tempo reale
- ✅ Filtri e ricerca
- ✅ Multi-container view
- ✅ Download log

## 🔐 Security

### Secrets Management

**File:** `config/.secrets`

```bash
# Esempio
OPENROUTER_API_KEY=sk-or-v1-your-actual-key
SEMGREP_APP_TOKEN=optional-token
```

**⚠️ IMPORTANTE:** Mai committare `config/.secrets`! È già in `.gitignore`.

## 📈 Performance

### Tempi Attesi

| Operazione | Prima volta | Successive (cache) |
|------------|-------------|---------------------|
| Build Docker | 5-10 min | - |
| CI completo | 10-12 min | 5-7 min |
| Test API | 5-8 min | 3-5 min |
| Test Web | 3-5 min | 2-3 min |
| Security Scan | 10-15 min | 7-10 min |

### Cache

Il sistema cachea automaticamente:
- ✅ NuGet packages (.NET)
- ✅ npm/pnpm packages (Node)
- ✅ Docker layers
- ✅ k6 binary
- ✅ act runner images

## 🆘 Troubleshooting

### ❌ Docker non trovato

```bash
# Windows: Avvia Docker Desktop
# Linux: sudo systemctl start docker
```

### ❌ Porta occupata

```bash
# Stoppa il MeepleAI principale
cd ../infra
docker compose down
```

### ❌ Services not ready

```bash
# Attendi e riprova
sleep 30
make health
```

### ❌ Reset completo

```bash
make reset    # Rebuild tutto
```

## 📚 Documentazione

| File | Contenuto |
|------|-----------|
| [README.md](README.md) | Documentazione completa |
| [TESTING.md](TESTING.md) | Guida test dettagliata |
| [SUMMARY.md](SUMMARY.md) | Questo file |

## 🎯 Workflow Tipico

```bash
# 1. Modifica codice o workflow
vim ../apps/api/src/Api/Program.cs
vim ../.github/workflows/ci.yml

# 2. Avvia simulator
make up

# 3. Valida sintassi
make validate

# 4. Test modifiche
make test-api        # Se hai modificato backend
make test-web        # Se hai modificato frontend
make test-ci         # Test completo

# 5. Controlla risultati
make view-logs       # Vedi log
# Oppure: http://localhost:9999

# 6. Se green, fai push
git add .
git commit -m "feat: ..."
git push

# 7. Cleanup
make clean           # Opzionale
```

## 🔄 Confronto con GitHub

| Aspetto | Locale (Simulator) | GitHub Actions |
|---------|-------------------|----------------|
| **Velocità** | ⚡ Veloce (cache locale) | Normale |
| **Costo** | ✅ Gratis | Limitato (minuti gratis) |
| **Feedback** | ✅ Immediato | Attendi push + queue |
| **Debug** | ✅ Facile (shell access) | Limitato |
| **Artifacts** | ✅ Locale | Cloud |
| **Secrets** | ⚠️ File locale | 🔒 GitHub Secrets |
| **Service Containers** | ✅ Supportati | ✅ Supportati |
| **Matrix Strategy** | ✅ Supportata | ✅ Supportata |
| **Completezza** | ~90% | 100% |

## 💡 Best Practices

1. **Test prima del push**: Sempre eseguire `make test-ci` prima di `git push`
2. **Valida workflow**: Usa `make validate` dopo modifiche ai workflow
3. **Debug incrementale**: Testa singoli job con `make test-api` prima di `test-ci`
4. **Usa cache**: Non fare `make reset` se non necessario
5. **Monitor risorse**: Usa Dozzle (http://localhost:9999) per log real-time
6. **Cleanup regolare**: Esegui `make clean` settimanalmente

## 📞 Support

### Log Analysis

```bash
make view-logs              # Lista log
make view-logs-api          # Log API
make view-logs-web          # Log Web
```

### Health Check

```bash
make health                 # Verifica ambiente
make ps                     # Container status
make stats                  # Risorse usate
```

### Cleanup

```bash
make clean                  # Pulisci vecchi file
make down                   # Stop servizi
make reset                  # Reset completo
```

## ✨ Features

- ✅ **Completo**: Tutti i tool necessari inclusi
- ✅ **Isolato**: Non interferisce con MeepleAI principale
- ✅ **Veloce**: Cache persistente per runs successivi
- ✅ **User-friendly**: Script helper e Makefile
- ✅ **Web UI**: Dozzle per log visualization
- ✅ **Sicuro**: Secrets gitignored
- ✅ **Documentato**: README + TESTING guide

## 🎉 Ready to Use!

Tutto è pronto! Inizia con:

```bash
cd github-actions-simulator
make setup
make test-ci
```

Buon testing! 🚀

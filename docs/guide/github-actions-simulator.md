# GitHub Actions Simulator

Ambiente Docker isolato per testare GitHub Actions workflows localmente.

## 📍 Location

```
github-actions-simulator/
```

## 🎯 Quick Start

```bash
cd github-actions-simulator
make setup
make test-ci
```

## 📚 Documentazione Completa

Vedi [github-actions-simulator/README.md](../../github-actions-simulator/README.md) per:

- ✅ Setup completo
- ✅ Guida all'uso
- ✅ Script helper
- ✅ Troubleshooting
- ✅ Best practices

## 🧪 Testing

Vedi [github-actions-simulator/TESTING.md](../../github-actions-simulator/TESTING.md) per:

- ✅ Test scenarios
- ✅ Debug workflows
- ✅ Esempi pratici
- ✅ Metriche di successo

## 🎉 Features

- ✅ **act** runner con tutti i tool (.NET 9, Node 20, k6, Semgrep)
- ✅ **Docker-in-Docker** per containers reali
- ✅ **Servizi integrati** (PostgreSQL, Redis, Qdrant)
- ✅ **Logging persistente** con timestamp
- ✅ **Web UI** (Dozzle) per log real-time
- ✅ **Completamente isolato** dal progetto principale

## 🚀 Comandi Comuni

```bash
make up         # Avvia ambiente
make health     # Verifica stato
make test-ci    # Test CI completo
make test-api   # Test solo backend
make test-web   # Test solo frontend
make view-logs  # Visualizza log
make down       # Stop ambiente
```

## 🌐 Web Interface

Log Viewer (Dozzle): http://localhost:9999

## ⚡ Use Cases

### 1. Test pre-push

```bash
make validate && make test-ci
```

### 2. Debug workflow falliti

```bash
make test-api
make view-logs-api
```

### 3. Security scan locale

```bash
make test-security
make test-semgrep
```

## 📖 Documentazione Correlata

- [GitHub Actions CI/CD](../CLAUDE.md#cicd)
- [Security Scanning](security-scanning.md)
- [Testing Strategy](../code-coverage.md)

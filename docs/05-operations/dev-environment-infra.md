# MeepleAI Development Stack Setup

Complete development environment con tutti i servizi app e MCP server.

## 🚀 Quick Start

### 1. Setup Environment Variables

```bash
# Copy example files
cd infra/env
cp mcp.env.dev.example mcp.env.dev
cp api.env.dev.example api.env.dev
cp web.env.dev.example web.env.dev
cp n8n.env.dev.example n8n.env.dev

# Edit mcp.env.dev e aggiungi le tue API keys
nano mcp.env.dev
```

### 2. Build e Avvio

```bash
# Dalla directory infra/
cd infra

# Build di tutte le immagini (prima volta o dopo modifiche)
docker compose -f docker-compose.dev.yml build

# Avvio completo dello stack
docker compose -f docker-compose.dev.yml up -d

# Verifica che tutti i servizi siano running
docker compose -f docker-compose.dev.yml ps

# Visualizza i log
docker compose -f docker-compose.dev.yml logs -f

# Ferma tutto
docker compose -f docker-compose.dev.yml down
```

### 3. Verifica Servizi Attivi

```bash
# Tutti i servizi dovrebbero essere "Up" o "Healthy"
docker compose -f docker-compose.dev.yml ps

# Verifica MCP servers
docker ps --filter "name=mcp-"
```

## 📦 Servizi Inclusi

### Infrastruttura (11 servizi)
| Servizio | Porta | Descrizione |
|----------|-------|-------------|
| meepleai-postgres | 5432 | Database PostgreSQL |
| meepleai-qdrant | 6333, 6334 | Vector database |
| meepleai-redis | 6379 | Cache |
| meepleai-ollama | 11434 | Embedding models |
| meepleai-embedding | 8000 | Local embeddings |
| meepleai-seq | 5341, 8081 | Log aggregation |
| meepleai-jaeger | 16686, 4318 | Distributed tracing |
| meepleai-prometheus | 9090 | Metrics |
| meepleai-alertmanager | 9093 | Alerting |
| meepleai-grafana | 3001 | Dashboards |
| meepleai-n8n | 5678 | Workflow automation |

### Applicazioni (2 servizi)
| Servizio | Porta | URL | Descrizione |
|----------|-------|-----|-------------|
| meepleai-api | 8080 | http://localhost:5080 | ASP.NET Core API |
| meepleai-web | 3000 | http://localhost:3000 | Next.js frontend |

### MCP Servers (8 servizi)
| Servizio | Container | Descrizione | API Key Required |
|----------|-----------|-------------|------------------|
| mcp-github | mcp-github | GitHub project manager | ✅ GITHUB_TOKEN |
| mcp-n8n | mcp-n8n | n8n workflow manager | ✅ N8N_API_KEY |
| mcp-memory | mcp-memory | Persistent memory | ❌ Local storage |
| mcp-sequential | mcp-sequential | Sequential thinking | ❌ No key |
| mcp-playwright | mcp-playwright | Browser automation | ❌ No key |
| mcp-magic | mcp-magic | 21st.dev UI generator | ✅ MAGIC_API_KEY |
| mcp-claude-context | mcp-claude-context | Context7 docs | ✅ CONTEXT7_API_KEY |
| mcp-knowledge-graph | mcp-knowledge-graph | Knowledge graph | ❌ Uses Qdrant |

**Totale: 21 servizi**

## 🔑 API Keys Setup

### Required Keys

1. **GitHub Token** (GITHUB_TOKEN)
   - Vai a: https://github.com/settings/tokens
   - Genera "Personal access token (classic)"
   - Scopes: `repo`, `workflow`, `admin:org`
   - Aggiungi a `infra/env/mcp.env.dev`

2. **21st.dev Magic** (MAGIC_API_KEY)
   - Vai a: https://21st.dev/magic/console
   - Genera API key
   - Aggiungi a `infra/env/mcp.env.dev`

3. **Upstash Context7** (CONTEXT7_API_KEY)
   - Vai a: https://console.upstash.com/context7
   - Crea account (free tier disponibile)
   - Genera API key
   - Aggiungi a `infra/env/mcp.env.dev`

4. **n8n API Key** (N8N_API_KEY)
   - Avvia n8n: http://localhost:5678
   - Crea account iniziale
   - Vai a Settings → API Keys
   - Genera chiave e aggiungi a `infra/env/mcp.env.dev`

### Optional Keys

- **OpenRouter** (OPENROUTER_API_KEY) - Per LLM features in api
- **OAuth Keys** (Google/Discord/GitHub) - Per social login

## 🧪 Testing Setup

### 1. Verifica Health dei Servizi

```bash
# Postgres
docker exec -it $(docker ps -qf "name=postgres") pg_isready

# Redis
docker exec -it $(docker ps -qf "name=redis") redis-cli ping

# Qdrant
curl http://localhost:6333/health

# API
curl http://localhost:5080/health

# Web
curl http://localhost:3000
```

### 2. Verifica MCP Servers

```bash
# Check tutti i container MCP sono running
docker ps --filter "name=mcp-" --format "table {{.Names}}\t{{.Status}}"

# Test GitHub MCP (richiede GITHUB_TOKEN configurato)
docker exec -i mcp-github node -e "console.log('MCP GitHub OK')"

# Test Magic MCP (richiede MAGIC_API_KEY configurato)
docker exec -i mcp-magic python -c "print('MCP Magic OK')"
```

### 3. Test Claude Code Integration

Apri Claude Code e verifica che `.mcp.json` sia configurato:

```json
{
  "mcpServers": {
    "github-project-manager": {
      "command": "docker",
      "args": ["exec", "-i", "mcp-github", "node", "/app/src/index.js"]
    },
    "magic": {
      "command": "docker",
      "args": ["exec", "-i", "mcp-magic", "python", "server.py"]
    }
    // ... altri MCP
  }
}
```

Prova comando:
```bash
# In Claude Code
/ui create button component
```

## 🛠️ Comandi Utili

### Build Selettivo

```bash
# Rebuild solo MCP servers
docker compose -f docker-compose.dev.yml build mcp-github mcp-magic mcp-sequential

# Rebuild solo app services
docker compose -f docker-compose.dev.yml build meepleai-api meepleai-web
```

### Restart Servizi

```bash
# Restart tutti i servizi
docker compose -f docker-compose.dev.yml restart

# Restart solo MCP servers
docker compose -f docker-compose.dev.yml restart mcp-github mcp-magic mcp-sequential mcp-playwright mcp-memory mcp-meepleai-n8n mcp-claude-context mcp-knowledge-graph
```

### Log Debugging

```bash
# Log di tutti i servizi
docker compose -f docker-compose.dev.yml logs -f

# Log solo MCP servers
docker compose -f docker-compose.dev.yml logs -f mcp-github mcp-magic mcp-sequential

# Log singolo servizio
docker compose -f docker-compose.dev.yml logs -f mcp-magic

# Ultimi 100 righe
docker compose -f docker-compose.dev.yml logs --tail=100 mcp-magic
```

### Cleanup

```bash
# Stop e rimuovi container (mantiene volumi)
docker compose -f docker-compose.dev.yml down

# Stop, rimuovi container E volumi (⚠️ cancella dati)
docker compose -f docker-compose.dev.yml down -v

# Rimuovi immagini MCP
docker rmi $(docker images --filter=reference='*mcp-*' -q)

# Cleanup completo sistema Docker (⚠️ attenzione)
docker system prune -a --volumes
```

## 🔍 Troubleshooting

### Container MCP non si avvia

**Problema**: `mcp-magic` non parte

**Debug**:
```bash
# Verifica build logs
docker compose -f docker-compose.dev.yml build mcp-magic

# Verifica runtime logs
docker compose -f docker-compose.dev.yml logs mcp-magic

# Controlla se API key è configurata
docker compose -f docker-compose.dev.yml config | grep MAGIC_API_KEY
```

**Soluzione**:
- Verifica API key in `infra/env/mcp.env.dev`
- Riavvia con `docker compose -f docker-compose.dev.yml restart mcp-magic`

### Health Check Failures

**Problema**: Container mostra "unhealthy"

**Debug**:
```bash
# Verifica health status
docker inspect mcp-github | jq '.[0].State.Health'

# Esegui manualmente health check
docker exec mcp-github pgrep -f 'node.*index.js'
```

**Soluzione**:
- Attendi 30-60 secondi per startup completo
- Verifica dipendenze (es. n8n per mcp-n8n)

### Porte Già in Uso

**Problema**: `Error: port 8080 already in use`

**Soluzione**:
```bash
# Windows
netstat -ano | findstr :8080
taskkill /PID <PID> /F

# Linux/Mac
lsof -i :8080
kill -9 <PID>

# Oppure modifica porte in docker-compose.dev.yml
```

### Memory Issues

**Problema**: Docker OOM (Out of Memory)

**Soluzione**:
```bash
# Aumenta memoria Docker Desktop: Settings → Resources → Memory (>= 8GB raccomandato)

# Oppure riduci servizi attivi:
# Commenta servizi non necessari in docker-compose.dev.yml
# Esempio: grafana, prometheus, alertmanager se non servono al momento
```

## 📊 Resource Usage

**Memoria Totale**: ~6-8GB
- Infrastruttura: ~2-3GB
- App: ~1-2GB
- MCP servers: ~2-3GB

**CPU**: 4-6 core raccomandati

**Disco**: ~10-15GB per immagini + volumi

## 🎯 Workflow Tipici

### Sviluppo Frontend

```bash
# Start stack minimo
docker compose -f docker-compose.dev.yml up -d meepleai-postgres meepleai-redis meepleai-qdrant meepleai-api mcp-magic

# Sviluppo locale Next.js (fuori Docker)
cd ../apps/web
pnpm dev

# UI generation con Magic MCP
# In Claude Code: /ui create dashboard component
```

### Sviluppo Backend

```bash
# Start infra + MCP
docker compose -f docker-compose.dev.yml up -d meepleai-postgres meepleai-redis meepleai-qdrant mcp-github mcp-sequential

# Sviluppo locale .NET (fuori Docker)
cd ../apps/api/src/Api
dotnet run

# Planning con Sequential MCP
# In Claude Code: "Pianifica (sequential) implementazione feature X"
```

### Full Stack Development

```bash
# Start tutto
docker compose -f docker-compose.dev.yml up -d

# Access services
# API: http://localhost:5080
# Web: http://localhost:3000
# Grafana: http://localhost:3001 (admin/admin)
# Seq: http://localhost:8081
# n8n: http://localhost:5678
```

## 📚 Riferimenti

- [MCP HOWTO](../mcp/HOWTO.md) - Guida completa uso MCP con Claude Code
- [MCP README](../mcp/README.md) - Documentazione tecnica MCP servers
- [CLAUDE.md](../CLAUDE.md) - Documentazione progetto completa

## 🚀 Next Steps

1. ✅ Setup environment variables
2. ✅ Avvia stack development
3. ✅ Configura API keys MCP
4. ✅ Verifica health dei servizi
5. ✅ Testa integrazione Claude Code
6. 🎨 Inizia a sviluppare con Magic MCP per UI
7. 🧠 Usa Sequential MCP per planning
8. 🤖 Integra GitHub MCP per workflow automation

---

**Versione**: 1.0
**Ultimo aggiornamento**: 2025-11-11


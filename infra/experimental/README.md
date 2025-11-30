# Experimental Infrastructure

## ⚠️ Status: Experimental

Questa cartella contiene configurazioni sperimentali e POC (Proof of Concept) che **non sono production-ready**.

## Contenuto

### docker-compose.infisical.yml

**Status**: POC (Issue #936)

**Scopo**: Automatic secrets rotation con Infisical secrets management platform

**Features**:
- Centralized secrets storage
- Automatic rotation
- Audit trail
- Multi-environment support
- API key management

**Perché Sperimentale**:
- Non testato in produzione
- Potenziale single point of failure
- Costo aggiuntivo (Infisical Cloud)
- Complessità operativa

**Alternative Attuale**: Docker secrets (production) + manual rotation

### infisical.env.example

Template environment variables per Infisical setup.

## Infisical POC

### Cos'è Infisical?

Infisical è una piattaforma open-source per secrets management:
- Vault centralizzato
- Auto-sync con Docker, Kubernetes, AWS Secrets Manager
- Versioning e rollback
- Fine-grained access control
- Audit logging

**Website**: https://infisical.com/

### Architecture

```
Infisical Server (self-hosted o cloud)
    ↓
Infisical Agent (sidecar container)
    ↓
Export secrets as env vars o files
    ↓
MeepleAI containers (api, web, n8n, etc.)
```

### Setup

#### 1. Infisical Cloud (Easiest)

```bash
# 1. Crea account su https://app.infisical.com
# 2. Crea workspace "MeepleAI"
# 3. Aggiungi secrets per environment (dev, staging, prod)
# 4. Genera service token

# 5. Configure env
cp infisical.env.example .env.infisical
# Edit .env.infisical con service token

# 6. Start
docker compose -f docker-compose.infisical.yml up -d
```

#### 2. Self-Hosted Infisical

```bash
# 1. Deploy Infisical server
docker run -d \
  --name infisical \
  -p 8080:8080 \
  infisical/infisical:latest

# 2. Setup account via UI http://localhost:8080

# 3. Create workspace e secrets

# 4. Configure agent come sopra
```

### Configuration

```yaml
# docker-compose.infisical.yml (simplified)
services:
  infisical-agent:
    image: infisical/cli:latest
    environment:
      INFISICAL_TOKEN: ${INFISICAL_TOKEN}
      INFISICAL_PROJECT_ID: ${INFISICAL_PROJECT_ID}
      INFISICAL_ENVIRONMENT: ${ENVIRONMENT:-dev}
    command: ["export", "--env", "--format", "dotenv", "--path", "/secrets/.env"]
    volumes:
      - secrets:/secrets

  api:
    env_file:
      - /secrets/.env  # Secrets da Infisical
    volumes:
      - secrets:/secrets:ro
    depends_on:
      - infisical-agent

volumes:
  secrets:
```

### Testing

```bash
# 1. Verifica agent running
docker compose -f docker-compose.infisical.yml ps

# 2. Check secrets exportati
docker compose -f docker-compose.infisical.yml exec infisical-agent ls -la /secrets/

# 3. Verifica API legge secrets
docker compose -f docker-compose.infisical.yml exec api env | grep POSTGRES_PASSWORD
```

### Pros & Cons

**Vantaggi**:
- ✅ Centralized secrets management
- ✅ Automatic rotation (no manual intervention)
- ✅ Audit trail (chi ha modificato cosa)
- ✅ Multi-environment (dev, staging, prod in un posto)
- ✅ Team access control (developer, devops, admin)
- ✅ Secret versioning e rollback
- ✅ Integration con CI/CD

**Svantaggi**:
- ❌ Additional complexity (nuovo servizio da gestire)
- ❌ Single point of failure (se Infisical down, tutto down)
- ❌ Costo (Infisical Cloud a pagamento per team)
- ❌ Network dependency (Infisical deve essere raggiungibile)
- ❌ Learning curve (team deve imparare nuovo tool)
- ❌ Migration effort (migrate existing secrets)

### Decisione: Production-Ready?

**Current Status** (2025-11-22): **NO**

**Motivi**:
1. Non testato in produzione
2. Docker secrets sufficient per ora (manual rotation acceptable)
3. Team size piccolo (no team secrets access problem)
4. Costo vs benefit non giustificato ora

**Quando Riconsiderare**:
- Team >10 persone (access control diventa critico)
- Secrets rotation frequency aumenta (es. weekly)
- Compliance requirements (audit trail required)
- Multi-cloud deployment (AWS + Azure + GCP)

**Alternative da Considerare**:
- **HashiCorp Vault**: Industry standard, ma più complesso
- **AWS Secrets Manager**: Se già su AWS
- **Azure Key Vault**: Se già su Azure
- **Doppler**: Simile a Infisical, meno features

## Aggiungere Nuovo Esperimento

### Checklist

Prima di aggiungere nuovo POC:

- [ ] Clear use case identificato
- [ ] Alternative valutate (pros/cons)
- [ ] Issue creata con label `experimental`
- [ ] POC timeline definito (es. 2 settimane)
- [ ] Success criteria definiti
- [ ] Rollback plan se fallisce

### Template

```bash
# 1. Crea configurazione
touch experimental/docker-compose.my-feature.yml

# 2. Aggiungi README entry
# Edit experimental/README.md

# 3. Testa localmente
docker compose -f experimental/docker-compose.my-feature.yml up -d

# 4. Documenta risultati in issue

# 5. Decision: Promote to production o Archive
```

### Lifecycle

```
Idea → POC → Evaluation → Decision
                             ├─ Promote to production (merge to docker-compose.yml)
                             ├─ Archive (move to docs/archive/experimental/)
                             └─ Iterate (refine e re-test)
```

## POC Archiviati

Vedi `../docs/archive/experimental/` per POC archiviati:
- Infisical (se deciso di non usare)
- Altri tentativi falliti o superati

## Best Practices

### 1. Isolamento

Esperimenti devono essere isolati:
- Separate Docker Compose file
- No impact su stack principale
- Facile cleanup (`docker compose down -v`)

### 2. Documentazione

Ogni POC deve avere:
- README section (questo file)
- Issue GitHub con context
- Success/failure criteria
- Timeline (max 2-4 settimane)

### 3. Evaluation

Dopo POC, documenta:
- Cosa ha funzionato
- Cosa non ha funzionato
- Metriche (performance, costo, complexity)
- Raccomandazione (promote, archive, iterate)

### 4. Cleanup

Se POC fallisce o viene superato:
- Archive configurazione in `docs/archive/experimental/`
- Close issue con summary
- Remove da `experimental/`
- Update questo README

## Current Experiments

| Experiment | Status | Issue | Timeline | Owner |
|------------|--------|-------|----------|-------|
| Infisical Secrets | POC | #936 | 2025-11-22 → TBD | DevOps |

## Completed Experiments

Nessuno ancora (primo POC).

## Related Documentation

- Issue #936 - Infisical POC
- `../secrets/README.md` - Current secrets management (Docker secrets)
- `docs/06-security/secrets-management.md`
- Infisical docs: https://infisical.com/docs

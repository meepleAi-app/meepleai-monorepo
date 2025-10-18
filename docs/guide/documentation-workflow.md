# Documentation Workflow Guide

Guida completa per lavorare con la documentazione MeepleAI: ricerca, scrittura, manutenzione e best practices.

---

## Quick Start

### Cercare nella Documentazione

**Il modo più veloce per trovare informazioni**:

```bash
# Cerca un argomento
node tools/search-docs.js "authentication"

# Cerca con limite risultati
node tools/search-docs.js "RAG pipeline" --limit 5

# Cerca termini multipli
node tools/search-docs.js "OpenTelemetry metrics Prometheus"
```

**Esempi comuni**:

```bash
# Trova info su streaming
node tools/search-docs.js "streaming SSE"

# Trova guide Docker
node tools/search-docs.js "Docker compose"

# Trova implementazioni specifiche
node tools/search-docs.js "PBKDF2 hash"

# Trova documentazione session management
node tools/search-docs.js "session management"
```

### Generare Docs API

**Quando l'API viene modificata**:

```bash
# Terminal 1: Avvia API in development mode
cd apps/api/src/Api
dotnet run

# Terminal 2: Genera documentazione
node tools/generate-api-docs.js
```

**Output**:
- `docs/api-reference.json` - OpenAPI spec completo
- `docs/api-reference.md` - Documentazione human-readable

**Quando eseguire**:
- Dopo aver aggiunto/modificato endpoint
- Prima di rilasciare una nuova versione
- Durante code review di PR con API changes

### Standardizzare Markdown

**Prima di committare modifiche alla documentazione**:

```bash
node tools/standardize-markdown.js
```

Questo script:
- ✅ Aggiunge tag linguaggio ai code blocks senza tag
- ✅ Verifica heading hierarchy
- ✅ Segnala problemi di formattazione

**Quando eseguire**:
- Dopo aver scritto/modificato file Markdown
- Prima di creare una PR
- Periodicamente per mantenere consistenza

---

## Organizzazione Documentazione

### Struttura Directory

```
docs/
├── README.md                    - Indice principale
├── SECURITY.md                  - Security policies
├── database-schema.md           - DB reference
├── observability.md             - OPS-01 guide
├── ai-06-rag-evaluation.md      - RAG eval guide
│
├── guide/                       - Guide utente/sviluppatore
│   ├── getting-started.md       - Setup locale
│   ├── testing-guide.md         - Test conventions
│   ├── n8n-integration-guide.md - n8n workflows
│   ├── load-testing.md          - k6 load tests
│   └── ...
│
├── technic/                     - Design tecnici
│   ├── architecture-diagrams.md - Mermaid diagrams
│   ├── ops-02-opentelemetry-design.md
│   ├── pdf-processing-design.md
│   └── ...
│
├── issue/                       - Risoluzioni issue/PR
│   ├── chat-01-streaming-sse-implementation.md
│   ├── auth-04-password-reset.md
│   ├── ops-06-ci-optimization.md
│   └── ...
│
└── runbooks/                    - Operational runbooks
    ├── dependency-down.md
    ├── error-spike.md
    └── high-error-rate.md
```

### Dove Mettere Nuova Documentazione

**Guide utente/sviluppatore** → `docs/guide/`
- How-to guides
- Tutorial step-by-step
- Configuration guides
- Best practices

**Design tecnici** → `docs/technic/`
- Architecture decisions
- Technical design documents
- API designs
- System diagrams

**Risoluzioni issue/PR** → `docs/issue/`
- Implementation summaries
- Issue resolution documentation
- PR descriptions dettagliate
- Post-implementation reviews

**Documentazione generale** → `docs/` (root)
- Schema database
- Roadmap
- Features list
- Observability guide

---

## Best Practices

### Scrivere Documentazione

#### 1. Usa Heading Hierarchy Corretta

```markdown
# Titolo Documento (H1) - Uno solo per documento

## Sezione Principale (H2)

### Sottosezione (H3)

#### Dettaglio (H4)
```

**❌ Evita**:
```markdown
# Titolo
### Sottosezione  ← Salta da H1 a H3
```

**✅ Corretto**:
```markdown
# Titolo
## Sezione
### Sottosezione
```

#### 2. Tag Linguaggio nei Code Blocks

**❌ Senza tag**:
```
dotnet build
```

**✅ Con tag**:
```bash
dotnet build
```

**Tag comuni**:
- `bash` - Comandi shell
- `csharp` - Codice C#
- `typescript` - Codice TypeScript
- `javascript` - Codice JavaScript
- `json` - JSON
- `yaml` - YAML
- `sql` - SQL queries
- `powershell` - PowerShell scripts

#### 3. Struttura Standard

Ogni documento dovrebbe avere:

```markdown
# Titolo Descrittivo

Breve descrizione (1-2 frasi) di cosa copre il documento.

## Table of Contents (opzionale per doc lunghi)

- [Sezione 1](#sezione-1)
- [Sezione 2](#sezione-2)

## Sezione 1

Contenuto...

## Sezione 2

Contenuto...

## References

- Link a documenti correlati
- Link esterni
```

#### 4. Cross-References

**Link relativi per docs interni**:

```markdown
Vedi [Database Schema](./database-schema.md) per dettagli.

Per setup, consulta [Getting Started Guide](./guide/getting-started.md).
```

**Link assoluti per risorse esterne**:

```markdown
Documentazione ufficiale: https://docs.microsoft.com/aspnet
```

#### 5. Esempi di Codice

**Fornisci contesto completo**:

```markdown
**File**: `Services/AuthService.cs`

```csharp
public async Task<bool> ValidateApiKeyAsync(string apiKey)
{
    // Implementation...
}
```
```

**Includi output atteso**:

```markdown
**Comando**:
```bash
dotnet test --filter "FullyQualifiedName~AuthTests"
```

**Output**:
```bash
Passed!  - Failed:     0, Passed:    15, Skipped:     0, Total:    15
```
```

### Aggiornare Documentazione

#### Quando Modificare la Docs

**Sempre aggiorna la docs quando**:
- ✅ Aggiungi/modifichi endpoint API
- ✅ Cambi schema database
- ✅ Modifichi flussi di autenticazione
- ✅ Aggiungi nuove feature
- ✅ Cambi configurazione (env vars, appsettings.json)
- ✅ Deprechi funzionalità

**Workflow**:

1. **Modifica il codice**
2. **Aggiorna la documentazione correlata**
3. **Esegui standardizzazione**: `node tools/standardize-markdown.js`
4. **Testa ricerca**: `node tools/search-docs.js "topic"` per verificare sia trovabile
5. **Commit insieme a codice e docs**

#### Checklist PR con API Changes

Quando crei una PR che modifica l'API:

- [ ] Codice implementato e testato
- [ ] Endpoint documentati in codice (XML comments)
- [ ] OpenAPI spec aggiornato (verifica `/api/docs`)
- [ ] Genera API reference: `node tools/generate-api-docs.js`
- [ ] Aggiorna `CLAUDE.md` se necessario
- [ ] Aggiorna `docs/README.md` se nuovo documento
- [ ] Esegui `node tools/standardize-markdown.js`
- [ ] Commit con docs incluse

---

## Workflow Tipici

### Workflow 1: Aggiungere Nuova Feature con Docs

**Scenario**: Implementi una nuova feature "User Notifications"

```bash
# 1. Crea branch
git checkout -b feature/notifications

# 2. Implementa feature
# ... coding ...

# 3. Crea documentazione
# Opzione A: Design tecnico
touch docs/technic/notifications-design.md

# Opzione B: Guida utente
touch docs/guide/notifications-guide.md

# 4. Scrivi documentazione
# (usa editor preferito)

# 5. Standardizza
node tools/standardize-markdown.js

# 6. Verifica sia trovabile
node tools/search-docs.js "notifications"

# 7. Aggiorna indice se necessario
# Modifica docs/README.md

# 8. Commit
git add .
git commit -m "feat(notifications): add user notification system"

# 9. Push e PR
git push origin feature/notifications
```

### Workflow 2: Risolvere Issue con Documentazione

**Scenario**: Risolvi issue #123 "Fix PDF validation"

```bash
# 1. Crea branch da issue
git checkout -b fix/issue-123-pdf-validation

# 2. Implementa fix
# ... coding ...

# 3. Crea issue resolution doc
cat > docs/issue/pdf-validation-fix.md <<EOF
# PDF Validation Fix (Issue #123)

## Problem
...

## Solution
...

## Testing
...
EOF

# 4. Standardizza
node tools/standardize-markdown.js

# 5. Commit
git add .
git commit -m "fix(pdf): resolve validation edge case (#123)"

# 6. Push e PR
git push origin fix/issue-123-pdf-validation
```

### Workflow 3: Revisione Documentazione Periodica

**Scenario**: Revisione mensile della docs

```bash
# 1. Cerca documenti obsoleti
node tools/search-docs.js "TODO"
node tools/search-docs.js "FIXME"

# 2. Standardizza tutto
node tools/standardize-markdown.js

# 3. Genera API reference aggiornata
cd apps/api/src/Api && dotnet run &
sleep 10
node tools/generate-api-docs.js

# 4. Verifica link rotti (manualmente o con tool)
# TODO: Aggiungere tool per link checking

# 5. Commit aggiornamenti
git add docs/
git commit -m "docs: monthly documentation review and updates"
```

---

## Diagrammi di Architettura

### Visualizzare Diagrammi

**Su GitHub**: I diagrammi Mermaid vengono renderizzati automaticamente.

**Localmente**: Usa un editor Markdown con supporto Mermaid:
- VS Code: Installa "Markdown Preview Mermaid Support"
- IntelliJ: Supporto built-in
- Online: https://mermaid.live/

### Creare Nuovi Diagrammi

**Aggiungi a** `docs/technic/architecture-diagrams.md`

**Template base**:

```markdown
## Nome Diagramma

\```mermaid
graph TB
    A[Component A] --> B[Component B]
    B --> C[Component C]

    classDef primary fill:#3b82f6,stroke:#333,stroke-width:2px,color:#fff
    class A,B primary
\```
```

**Tipi di diagrammi disponibili**:
- `graph TB` - Flowchart (top to bottom)
- `graph LR` - Flowchart (left to right)
- `sequenceDiagram` - Sequence diagrams
- `classDiagram` - Class diagrams
- `erDiagram` - Entity-relationship diagrams

**Esempi in** `docs/technic/architecture-diagrams.md`:
- System Architecture
- Authentication Flow (sequence)
- RAG Pipeline (graph LR)
- Observability Stack

---

## Strumenti Utili

### 1. Ricerca Documentazione

**Uso Base**:

```bash
node tools/search-docs.js "query"
```

**Opzioni**:

```bash
# Limita risultati
node tools/search-docs.js "query" --limit 5

# Mostra help
node tools/search-docs.js
```

**Come funziona**:
- Indicizza tutti i file Markdown in `docs/`
- Ranking: Titolo (10pt) > Heading (5pt) > Contenuto (0.5pt/match)
- Estrae snippet con contesto (2 righe prima/dopo)
- Supporta query multi-termine

**Trucchi**:
- Usa termini specifici per risultati migliori
- I termini nei titoli/heading hanno priorità
- Case-insensitive
- Termini < 3 caratteri ignorati

### 2. Generazione API Docs

**Prerequisiti**:
- API in esecuzione (`dotnet run`)
- Development mode (Swagger enabled)

**Uso**:

```bash
node tools/generate-api-docs.js
```

**Output**:
- `docs/api-reference.json` - OpenAPI 3.0 spec
- `docs/api-reference.md` - Markdown documentation

**Customizzazione**:

```bash
# URL API custom
API_BASE_URL=http://localhost:5000 node tools/generate-api-docs.js
```

### 3. Standardizzazione Markdown

**Uso**:

```bash
node tools/standardize-markdown.js
```

**Cosa fa**:
- Scansiona tutti i file `.md` in `docs/`
- Rileva linguaggio code blocks e aggiunge tag
- Verifica heading hierarchy
- Report modifiche in console

**Pattern rilevati automaticamente**:
- C# - `using`, `public class`, `namespace`
- TypeScript - `interface`, `type`, `import from`
- Bash - `cd`, `git`, `docker`, `$`
- SQL - `SELECT`, `INSERT`, `CREATE`
- JSON - `{`, `[`, `:`
- PowerShell - `$var =`, `Get-*`, `Set-*`

---

## FAQ

### Come trovo documentazione su un argomento specifico?

**Opzione 1: Ricerca con tool**

```bash
node tools/search-docs.js "argomento"
```

**Opzione 2: Naviga indice**

Apri `docs/README.md` - contiene indice completo organizzato per categoria.

**Opzione 3: Grep diretto**

```bash
grep -r "argomento" docs/
```

### Come aggiungo documentazione per una nuova feature?

1. **Scegli la directory corretta**:
   - Guida utente → `docs/guide/`
   - Design tecnico → `docs/technic/`
   - Risoluzione issue → `docs/issue/`

2. **Crea file con nome descrittivo**:
   ```bash
   touch docs/guide/nuova-feature-guide.md
   ```

3. **Scrivi documentazione** seguendo template standard

4. **Standardizza**:
   ```bash
   node tools/standardize-markdown.js
   ```

5. **Aggiungi a indice** in `docs/README.md`

6. **Commit**:
   ```bash
   git add docs/
   git commit -m "docs: add nuova-feature guide"
   ```

### Come aggiorno l'API reference?

```bash
# 1. Assicurati che API sia running
cd apps/api/src/Api
dotnet run

# 2. In altro terminal, genera docs
node tools/generate-api-docs.js

# 3. Verifica output
cat docs/api-reference.md

# 4. Commit
git add docs/api-reference.*
git commit -m "docs(api): update API reference"
```

### Come aggiungo un diagramma di architettura?

1. **Apri** `docs/technic/architecture-diagrams.md`

2. **Aggiungi nuova sezione** con diagramma Mermaid:

```markdown
## Nome Nuovo Diagramma

\```mermaid
graph TB
    A[Node A] --> B[Node B]
\```
```

3. **Testa rendering** su https://mermaid.live/ o in VS Code

4. **Standardizza e commit**:

```bash
node tools/standardize-markdown.js
git add docs/technic/architecture-diagrams.md
git commit -m "docs(arch): add nuovo diagramma"
```

### Devo documentare ogni piccola modifica?

**Documenta quando**:
- ✅ API pubbliche cambiano
- ✅ Comportamento visibile all'utente cambia
- ✅ Configurazione richiede aggiornamenti
- ✅ Nuove feature aggiunte
- ✅ Breaking changes

**Non serve documentare**:
- ❌ Refactoring interni senza cambio comportamento
- ❌ Correzioni typo nel codice
- ❌ Ottimizzazioni performance senza API changes
- ❌ Test updates

**Regola generale**: Se un altro sviluppatore deve sapere del cambiamento, documentalo.

---

## References

- **Documentazione Principale**: [docs/README.md](../README.md)
- **Strumenti Sviluppatori**: [tools/README.md](../../tools/README.md)
- **Diagrammi Architettura**: [docs/technic/architecture-diagrams.md](../technic/architecture-diagrams.md)
- **Guida Sviluppatori**: [CLAUDE.md](../../CLAUDE.md)
- **Mermaid Docs**: https://mermaid.js.org/
- **GitHub Flavored Markdown**: https://github.github.com/gfm/

---

**Last Updated**: 2025-10-18

# Claude Code Skills Reference

> Guida completa alle skill disponibili per sviluppo e manutenzione di applicazioni web.
>
> **Formato**: `/<skill>` : `<uso>` : `<esempio>`

---

## Indice

1. [SuperClaude Commands (/sc:*)](#superclaude-commands)
2. [Development Workflow](#development-workflow)
3. [Design & Visual](#design--visual)
4. [Document Generation](#document-generation)
5. [Testing & Quality](#testing--quality)
6. [MCP Servers](#mcp-servers)

---

## SuperClaude Commands

### Analisi e Ricerca

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/sc:analyze` | Analisi completa di code quality, security, performance | `/sc:analyze src/auth --focus security` |
| `/sc:research` | Ricerca web approfondita con fonti multiple | `/sc:research "best practices React Server Components 2026"` |
| `/sc:explain` | Spiegazione chiara di codice o concetti | `/sc:explain src/services/rag.service.ts` |
| `/sc:troubleshoot` | Diagnosi e risoluzione problemi | `/sc:troubleshoot "API returns 500 on login"` |

### Pianificazione e Design

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/sc:brainstorm` | Discovery interattiva dei requisiti | `/sc:brainstorm "sistema di notifiche push"` |
| `/sc:design` | Progettazione architettura e API | `/sc:design "microservice authentication"` |
| `/sc:estimate` | Stima effort per task/feature | `/sc:estimate "implementare OAuth2 con Google"` |
| `/sc:workflow` | Genera workflow da PRD | `/sc:workflow @docs/prd/feature-x.md` |
| `/sc:spec-panel` | Review specifiche con panel di esperti | `/sc:spec-panel @api-spec.yaml` |
| `/sc:business-panel` | Analisi business con 9 esperti virtuali | `/sc:business-panel @strategy.pdf --mode debate` |

### Implementazione

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/sc:implement` | Implementazione feature completa | `/sc:implement "add logout button to header"` |
| `/sc:improve` | Miglioramenti sistematici al codice | `/sc:improve src/utils --focus performance` |
| `/sc:cleanup` | Pulizia codice e dead code removal | `/sc:cleanup src/legacy` |
| `/sc:build` | Build e packaging con error handling | `/sc:build --target production` |

### Testing

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/sc:test` | Esecuzione test con coverage | `/sc:test src/services --coverage` |

### Documentazione

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/sc:document` | Genera documentazione per componenti | `/sc:document src/api --type api` |
| `/sc:index` | Genera knowledge base del progetto | `/sc:index --output docs/` |

### Git & Workflow

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/sc:git` | Operazioni git con commit intelligenti | `/sc:git commit --smart-commit` |

### Session Management

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/sc:load` | Carica contesto progetto (Serena) | `/sc:load` |
| `/sc:save` | Salva sessione corrente | `/sc:save` |
| `/sc:reflect` | Riflessione e validazione task | `/sc:reflect` |

### Orchestrazione

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/sc:pm` | Project Manager agent orchestration | `/sc:pm "coordinate refactoring sprint"` |
| `/sc:spawn` | Task orchestration con delegation | `/sc:spawn "migrate database schema"` |
| `/sc:task` | Esecuzione task complessi | `/sc:task "setup CI/CD pipeline"` |
| `/sc:select-tool` | Selezione intelligente MCP tools | `/sc:select-tool "edit multiple files"` |
| `/sc:help` | Lista tutti i comandi disponibili | `/sc:help` |

---

## Development Workflow

### Feature Development (Plugin)

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/feature-dev` | Sviluppo guidato feature con focus architettura | `/feature-dev "user profile page"` |

### Code Review (Plugin)

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/code-review` | Review PR con analisi approfondita | `/code-review #123` |

### Custom Implementation

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/implement` | Implementazione con issue tracking | `/implement "add dark mode toggle"` |

---

## Design & Visual

### Infografiche e Poster

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/canvas-design` | Crea PDF/PNG con design philosophy | `/canvas-design "RAG architecture infographic"` |
| `/algorithmic-art` | Arte generativa con p5.js | `/algorithmic-art "flow field visualization"` |

### Frontend Design

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/frontend-design` | UI distintiva, non-generic | `/frontend-design "login page brutalist style"` |

### Animazioni

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/slack-gif-creator` | GIF animate per Slack | `/slack-gif-creator "loading spinner orange"` |

### Theming

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/theme-factory` | Applica temi a artifact | `/theme-factory "apply warm-sunset to dashboard"` |

### Brand

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/brand-guidelines` | Applica brand Anthropic | `/brand-guidelines @presentation.pptx` |

---

## Document Generation

### PDF

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/pdf` | Manipolazione PDF completa | `/pdf extract-text report.pdf` |
| `/pdf` | Crea PDF da template | `/pdf create --template invoice` |
| `/pdf` | Merge/split PDF | `/pdf merge doc1.pdf doc2.pdf` |

### Office Documents

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/docx` | Crea/modifica Word documents | `/docx create "technical spec" --template formal` |
| `/pptx` | Crea/modifica presentazioni | `/pptx create "quarterly review" --slides 10` |
| `/xlsx` | Crea/analizza spreadsheet | `/xlsx analyze sales-data.xlsx --pivot` |

### Web Artifacts

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/web-artifacts-builder` | Artifact React complessi | `/web-artifacts-builder "interactive dashboard"` |

---

## Testing & Quality

### Web App Testing

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/webapp-testing` | Test app con Playwright | `/webapp-testing localhost:3000 --flow login` |

### Security

| Skill | Uso | Esempio |
|-------|-----|---------|
| `/security-guidance` | Guida sicurezza (Plugin) | `/security-guidance "JWT implementation"` |

---

## MCP Servers

### Design & UI

| Server | Uso | Trigger |
|--------|-----|---------|
| **superdesign** | UI design, wireframe, logo, icons | `"design a dashboard layout"` |
| **figma** | Design-to-code da Figma | Figma URL o `/figma` |
| **magic** | UI components da 21st.dev | `/ui`, `/21`, component requests |

### Search & Research

| Server | Uso | Trigger |
|--------|-----|---------|
| **tavily** | Web search avanzato | `/sc:research`, current info needs |

### Code Intelligence

| Server | Uso | Trigger |
|--------|-----|---------|
| **sequential** | Multi-step reasoning | `--think`, `--think-hard`, complex analysis |
| **morphllm** | Bulk pattern edits | Multi-file transformations |
| **serena** | Symbol operations, memory | `/sc:load`, `/sc:save`, refactoring |

### Browser Automation

| Server | Uso | Trigger |
|--------|-----|---------|
| **playwright** | E2E testing, screenshots | Browser testing, visual validation |
| **chrome-devtools** | Performance, debugging | Performance audit, console errors |

### External Services

| Server | Uso | Trigger |
|--------|-----|---------|
| **github-project-manager** | GitHub issues/PRs | Issue management, PR creation |
| **n8n** | Workflow automation | Automation triggers |
| **knowledge-graph** | Graph-based knowledge | Entity relationships |

---

## Quick Reference Card

### Ciclo Sviluppo Tipico

```bash
# 1. Inizio sessione
/sc:load

# 2. Pianificazione
/sc:brainstorm "nuova feature X"
/sc:design "architettura feature X"
/sc:estimate "feature X"

# 3. Implementazione
/sc:implement "feature X step 1"
/sc:test src/features/x

# 4. Review & Documentation
/code-review
/sc:document src/features/x --type api

# 5. Git workflow
/sc:git commit --smart-commit

# 6. Fine sessione
/sc:save
```

### Design Workflow

```bash
# Infografica
/canvas-design "system architecture poster"

# UI Component
/frontend-design "hero section glassmorphism"

# Da Figma design
# Passa URL Figma e usa magic per generare

# Asset web
# Genera favicon, app icons, social images
```

### Troubleshooting

```bash
# Problema generico
/sc:troubleshoot "error message here"

# Analisi sicurezza
/sc:analyze src/auth --focus security

# Performance
/sc:analyze src/api --focus performance

# Debug con browser
# Usa playwright per E2E
# Usa chrome-devtools per console/network
```

---

## Flags Utili

| Flag | Effetto |
|------|---------|
| `--think` | Analisi strutturata (~4K tokens) |
| `--think-hard` | Analisi approfondita (~10K tokens) |
| `--ultrathink` | Massima profondità (~32K tokens) |
| `--uc` | Ultra-compressed output |
| `--focus security` | Focus su sicurezza |
| `--focus performance` | Focus su performance |
| `--delegate` | Abilita sub-agent delegation |
| `--validate` | Pre-execution validation |

---

## Installazione Nuove Skills

### Da GitHub

```bash
# Clona nella directory skills
git clone <repo> ~/.claude/skills/<skill-name>
```

### Da Plugin Marketplace

```bash
claude plugin add <plugin-name>
```

### MCP Server

```json
// Aggiungi a .mcp.json
{
  "mcpServers": {
    "server-name": {
      "command": "npx",
      "args": ["-y", "@package/server"]
    }
  }
}
```

---

**Last Updated**: 2026-02-04
**Version**: 1.0

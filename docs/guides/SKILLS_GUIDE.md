# Skills Guide - MeepleAI Development

Guida completa alle **Skills** disponibili in Claude Code per il progetto MeepleAI. Le skills forniscono pattern generici, best practices e boilerplate code che complementano gli Agent specializzati.

## Indice

- [Cosa sono le Skills](#cosa-sono-le-skills)
- [Skills vs Agent vs MCP](#skills-vs-agent-vs-mcp)
- [Skills Utente (Project-Level)](#skills-utente-project-level)
- [Skills Plugin (System-Level)](#skills-plugin-system-level)
- [Quando Usare le Skills](#quando-usare-le-skills)
- [Integrazione con /work Command](#integrazione-con-work-command)

---

## Cosa sono le Skills

Le **Skills** sono moduli specializzati che forniscono:
- Pattern e best practices framework-agnostic
- Boilerplate code riutilizzabile
- Guide step-by-step per task comuni
- Template e convenzioni standard

**Non scrivono codice specifico del dominio** - questo è il compito degli Agent.

---

## Skills vs Agent vs MCP

| Tipo | Scope | Output | Esempio |
|------|-------|--------|---------|
| **Skill** | Generic patterns, best practices | Templates, structure, conventions | `Skill("testing")` → Unit test structure |
| **Agent** | Domain-specific logic | Context-aware code implementation | `deep-think-developer` → Implementa RagService |
| **MCP** | External services | Live data, API integrations | `Context7` → ASP.NET Core 9 docs |

**Workflow consigliato:**
1. **Skill** → Ottieni pattern generico
2. **Agent** → Implementa logica specifica
3. **MCP** → Integra servizi esterni

---

## Skills Utente (Project-Level)

Skills configurate a livello di progetto (`.claude/skills/`).

### 1. development
**Descrizione**: Guidance e code snippets per sviluppo software con linguaggi e framework comuni.

**Quando usare**:
- Setup progetti (dependencies, virtual env, configuration)
- Boilerplate code (CLI apps, API clients, utilities)
- Generic patterns (error handling, logging, Git workflow)
- Documentation structure (README, docstrings, comments)

**NON usare per**:
- Business logic specifica del dominio
- Complex architecture decisions (usa `system-architect` agent)

**Esempio**:
```bash
# Phase 8: Documentation update in /work command
Skill("development")  # → Get README structure
# Then populate with feature-specific content
```

---

### 2. testing
**Descrizione**: Procedure ed esempi per scrivere test automatici (unit, integration, E2E).

**Quando usare**:
- Unit test structure patterns (ALWAYS prima di scrivere test)
- Mocking/stubbing examples
- Test naming conventions
- Arrange-Act-Assert structure

**NON usare per**:
- Business logic assertions (Agent scrive assert specifici)

**Esempio**:
```bash
# Phase 3: Test-First in /work command
Skill("testing")  # → Get xUnit/Jest patterns
# Backend: Arrange-Act-Assert structure
# Frontend: describe/it blocks, expect matchers
```

**Adattamenti**:
- Backend C# (xUnit): Adatta pytest patterns ad xUnit + Testcontainers
- Frontend TypeScript (Jest/Vitest): describe/it, expect matchers, component testing

---

### 3. marketing
**Descrizione**: Frameworks, strategie e template per pianificare ed eseguire campagne marketing.

**Quando usare**:
- Market research
- Crafting messages
- Segmentation
- Analyzing campaign results

**Scope**: Fuori dal normale workflow di sviluppo MeepleAI (ma disponibile se necessario).

---

### 4. pdf-design
**Descrizione**: Istruzioni e code examples per creare, editare e design PDF programmaticamente.

**Quando usare**:
- Generare report PDF
- Fill PDF forms
- Design custom layouts

**Integrazione /work**:
- Phase 4: Se issue richiede PDF generation (es. "Generate game rules PDF")
- Complementa `PdfStorageService`, `PdfTextExtractionService`

**Esempio**:
```bash
# Issue: PDF-12 "Generate setup guide PDF"
Skill("pdf-design")  # → Get PDF layout patterns
# Then implement with QuestPDF library
```

---

### 5. presentation-design
**Descrizione**: Tecniche e code examples per creare slide decks professionali.

**Quando usare**:
- Strutturare narrative
- Applicare design principles
- Automatizzare slide generation

**Scope**: Documentazione presentazioni, rare nel workflow quotidiano.

---

### 6. spreadsheet-tools
**Descrizione**: Guide e code per creare, analizzare e formattare spreadsheet.

**Quando usare**:
- Lavorare con Excel files programmaticamente
- Data analysis techniques
- Export data to spreadsheet

**Integrazione /work**:
- Phase 4: Se issue richiede Excel export (es. "Export analytics to XLSX")

---

### 7. web-design
**Descrizione**: Principi e code snippets per design responsive, accessible websites.

**Quando usare**:
- Building/refining web interfaces
- Responsive design patterns
- Accessibility best practices

**Integrazione /work**:
- Phase 4: Frontend UI features (complementa `typescript-expert-developer`)
- Design principles per componenti Next.js

**Esempio**:
```bash
# Issue: UI-45 "Redesign game selection page"
Skill("web-design")  # → Get responsive layout patterns
# Then implement with Next.js + Tailwind CSS
```

---

## Skills Plugin (System-Level)

Skills fornite da plugin Anthropic (`anthropic-agent-skills`).

### Document Skills

#### 8. document-skills:xlsx
**Descrizione**: Comprehensive spreadsheet creation, editing, analysis con formule, formatting, data visualization.

**Quando usare**:
- Creare nuovi spreadsheet con formule
- Leggere/analizzare data da .xlsx, .xlsm, .csv, .tsv
- Modificare spreadsheet esistenti preservando formule
- Data analysis e visualization
- Ricalcolare formule

**Integrazione /work**:
- Phase 4: Export features (es. "Export chat logs to Excel")
- Phase 5: Data analysis per testing (es. "Analyze RAG evaluation results")

**Esempio**:
```bash
# Issue: EXPORT-03 "Export admin stats to XLSX"
Skill("document-skills:xlsx")  # → Get Excel export patterns
# Then implement with ExcelService
```

---

#### 9. document-skills:docx
**Descrizione**: Document creation, editing, analysis con tracked changes, comments, formatting preservation.

**Quando usare**:
- Creare nuovi documenti .docx
- Modificare/editare content
- Lavorare con tracked changes
- Aggiungere comments

**Integrazione /work**:
- Phase 8: Documentation generation (es. "Generate technical spec .docx")

---

#### 10. document-skills:pptx
**Descrizione**: Presentation creation, editing, analysis.

**Quando usare**:
- Creare nuove presentazioni
- Modificare content, layouts
- Aggiungere comments, speaker notes

**Scope**: Rare nel workflow quotidiano, ma utile per demo/presentations.

---

#### 11. document-skills:pdf
**Descrizione**: Comprehensive PDF manipulation toolkit per extracting text/tables, creating PDFs, merging/splitting, handling forms.

**Quando usare**:
- Fill PDF forms programmaticamente
- Process/generate/analyze PDF documents at scale
- Extract data da PDF esistenti

**Integrazione /work**:
- Phase 4: PDF processing features (complementa `PdfValidationService`, `PdfTextExtractionService`)
- Testing: Validate PDF output

**Esempio**:
```bash
# Issue: PDF-15 "Add PDF form filling capability"
Skill("document-skills:pdf")  # → Get PDF form filling patterns
# Then implement with iText7 or QuestPDF
```

---

### Example Skills

#### 12. example-skills:skill-creator
**Descrizione**: Guide per creare effective skills che estendono Claude capabilities.

**Quando usare**:
- Utente vuole creare nuova skill
- Update skill esistente

**Scope**: Meta-skill per skill development.

---

#### 13. example-skills:mcp-builder
**Descrizione**: Guide per creare high-quality MCP servers (Python FastMCP o Node/TypeScript SDK).

**Quando usare**:
- Build MCP servers per integrare external APIs
- Integrate external services via MCP

**Scope**: Infrastructure development, non workflow quotidiano.

---

#### 14. example-skills:canvas-design
**Descrizione**: Create beautiful visual art (.png, .pdf) usando design philosophy.

**Quando usare**:
- Creare poster, art, design, static pieces
- Visual assets per UI

**NON usare per**:
- Algorithmic art (usa `algorithmic-art`)
- Copying existing artists' work (copyright)

**Integrazione /work**:
- Phase 4: Se issue richiede visual assets (es. "Create game banner image")

**Esempio**:
```bash
# Issue: DESIGN-08 "Create MeepleAI logo variants"
Skill("canvas-design")  # → Get design principles
# Then create original visual designs
```

---

#### 15. example-skills:algorithmic-art
**Descrizione**: Creating algorithmic art con p5.js, seeded randomness, flow fields, particle systems.

**Quando usare**:
- Generative art
- Algorithmic art via code
- Flow fields, particle systems

**Scope**: Specifico per art projects, fuori workflow normale.

---

#### 16. example-skills:internal-comms
**Descrizione**: Resources per scrivere internal communications (status reports, updates, newsletters, FAQs, incident reports).

**Quando usare**:
- Status reports
- Leadership updates
- Company newsletters
- Incident reports
- Project updates

**Scope**: Communication tasks, non development.

---

#### 17. example-skills:webapp-testing
**Descrizione**: Toolkit per testing local web applications con Playwright (frontend, UI behavior, screenshots, browser logs).

**Quando usare**:
- Verify frontend functionality
- Debug UI behavior
- Capture browser screenshots
- View browser logs
- E2E test scaffolding (frontend only)

**Integrazione /work**:
- Phase 5: E2E testing (ALWAYS prima di scrivere E2E tests)

**Esempio**:
```bash
# Phase 5: Local Tests in /work command
Skill("webapp-testing")  # → Get Playwright E2E patterns
# Then write E2E tests for user workflows
pnpm test:e2e
```

**Patterns forniti**:
- Page object model
- User workflow patterns
- Accessibility testing
- Visual regression

---

#### 18. example-skills:artifacts-builder
**Descrizione**: Suite di tools per creare elaborate claude.ai HTML artifacts (React, Tailwind CSS, shadcn/ui).

**Quando usare**:
- Complex artifacts con state management
- Routing requirements
- shadcn/ui components

**NON usare per**:
- Simple single-file HTML/JSX artifacts

**Scope**: Claude.ai artifacts, non MeepleAI app development.

---

#### 19. example-skills:slack-gif-creator
**Descrizione**: Toolkit per creare animated GIFs ottimizzati per Slack con size constraints.

**Quando usare**:
- Animated GIFs per Slack
- Emoji animations

**Scope**: Specifico per Slack integrations.

---

#### 20. example-skills:theme-factory
**Descrizione**: Toolkit per styling artifacts con theme (10 preset + custom generation).

**Quando usare**:
- Apply theme a slides, docs, reports, HTML landing pages
- Consistent visual styling

**Integrazione /work**:
- Phase 4: Se issue richiede themed output (es. "Generate branded report")

**Esempio**:
```bash
# Issue: UI-99 "Apply MeepleAI brand to landing page"
Skill("theme-factory")  # → Get theme application patterns
# Or use brand-guidelines for Anthropic brand
```

---

#### 21. example-skills:brand-guidelines
**Descrizione**: Applica Anthropic's official brand colors e typography a artifacts.

**Quando usare**:
- Brand colors o style guidelines
- Visual formatting
- Company design standards

**Scope**: Anthropic-specific, ma utile come reference per MeepleAI branding.

---

## Quando Usare le Skills

### Decision Flow (da /work command)

```
1. Is task about TEST patterns/structure?
   → Skill("testing" or "webapp-testing")

2. Is task about BOILERPLATE code?
   → Skill("development")

3. Is task about DOCUMENTATION structure?
   → Skill("development")

4. Is task about PDF processing?
   → Skill("document-skills:pdf" or "pdf-design")

5. Is task about Excel/spreadsheet?
   → Skill("document-skills:xlsx" or "spreadsheet-tools")

6. Is task about UI design principles?
   → Skill("web-design")

7. Is task about visual assets?
   → Skill("canvas-design")

8. Is task about themed output?
   → Skill("theme-factory")

9. Otherwise → Use Agent
   (deep-think-developer, typescript-expert-developer)
```

---

## Integrazione con /work Command

Il comando `/work` integra automaticamente le skills appropriate in base alla fase del workflow.

### Phase 3: Test-First Implementation
**Skill**: `testing`

```bash
# BEFORE writing tests, get patterns from skill
Skill("testing")  # → Get unit test structure and best practices

# Backend C# (xUnit):
# → Arrange-Act-Assert structure, test naming conventions

# Frontend TypeScript (Jest):
# → describe/it blocks, expect matchers, component testing

# Then write context-specific tests
git commit -m "test: add BDD tests for <issue-id>"
```

---

### Phase 4: Implementation
**Skills**: `development`, `pdf-design`, `document-skills:*`, `web-design`, `canvas-design`

**Trigger automatico basato su issue keywords**:

```bash
# Backend boilerplate
if issue contains "CLI" or "setup" or "configuration":
    Skill("development")

# PDF features
if issue contains "PDF" or "pdf":
    Skill("pdf-design") or Skill("document-skills:pdf")

# Excel/spreadsheet features
if issue contains "Excel" or "XLSX" or "spreadsheet" or "export":
    Skill("document-skills:xlsx") or Skill("spreadsheet-tools")

# Frontend UI design
if issue contains "redesign" or "responsive" or "accessibility":
    Skill("web-design")

# Visual assets
if issue contains "logo" or "banner" or "image" or "visual":
    Skill("canvas-design")

# Themed output
if issue contains "brand" or "theme" or "styled":
    Skill("theme-factory")
```

**Esempio completo**:
```bash
# Issue: PDF-12 "Add PDF export for setup guides with MeepleAI branding"

# Phase 4: Implementation
Skill("pdf-design")        # → Get PDF layout patterns
Skill("theme-factory")     # → Get theming patterns (or brand-guidelines)
# Then implement with QuestPDF + MeepleAI brand colors
```

---

### Phase 5: Local Tests (E2E)
**Skill**: `webapp-testing` (frontend only)

```bash
# If frontend feature AND DoD requires E2E
Skill("webapp-testing")  # → Get Playwright E2E patterns
# → Page object model, selectors, assertions
# → User workflows, accessibility testing

pnpm test:e2e  # Run E2E tests
```

---

### Phase 8: Documentation Update
**Skill**: `development`

```bash
# If DoD requires documentation update
Skill("development")  # → Get documentation best practices
# → README structure, docstring conventions, comment style

# Then populate template with feature-specific content
```

---

## Best Practices

### ✅ DO

**Use skills for generic patterns**:
```bash
Skill("testing")  # Get test structure
# Then write domain-specific assertions
```

**Combine skills when appropriate**:
```bash
# Issue: "Export branded PDF report"
Skill("pdf-design")     # Layout patterns
Skill("theme-factory")  # Theming patterns
# Then implement specific report logic
```

**Use skills BEFORE agents**:
```bash
# 1. Get pattern from skill
Skill("testing")

# 2. Implement with agent
deep-think-developer → Write xUnit tests for RagService
```

---

### ❌ DON'T

**Don't use skills for domain logic**:
```bash
# ❌ WRONG
Skill("development")  # Then expect it to write RagService logic

# ✅ CORRECT
deep-think-developer → Implement RagService with proper error handling
```

**Don't use skills for code review**:
```bash
# ❌ WRONG
Skill("testing")  # To review test quality

# ✅ CORRECT
Fresh deep-think-developer (Phase 10) → Automated pre-review
```

**Don't skip skills when patterns are needed**:
```bash
# ❌ WRONG (missing pattern guidance)
Write E2E tests directly

# ✅ CORRECT
Skill("webapp-testing")  # Get Playwright patterns FIRST
# Then write E2E tests with proper structure
```

---

## Esempi Pratici

### Esempio 1: Backend API Feature con PDF Export
```bash
/work API-25  # "Add PDF export for game rules"

# Phase 3: Tests
Skill("testing")  # → xUnit patterns
# Write unit tests for PdfExportService

# Phase 4: Implementation
Skill("pdf-design")  # → PDF layout patterns
deep-think-developer → Implement PdfExportService with QuestPDF

# Phase 5: Tests
dotnet test  # All green ✅

# Phase 8: Documentation
Skill("development")  # → README structure
# Update docs/guides/pdf-export-guide.md
```

---

### Esempio 2: Frontend UI Feature con E2E
```bash
/work UI-45  # "Redesign game selection page with responsive layout"

# Phase 3: Tests
Skill("testing")  # → Jest patterns
# Write component tests

# Phase 4: Implementation
Skill("web-design")  # → Responsive design patterns
typescript-expert-developer → Implement responsive GameSelectionPage

# Phase 5: E2E Tests
Skill("webapp-testing")  # → Playwright E2E patterns
# Write E2E test for game selection flow
pnpm test:e2e  # All green ✅
```

---

### Esempio 3: Full-Stack Feature con Excel Export
```bash
/work EXPORT-03  # "Export admin stats to branded XLSX with charts"

# Phase 3: Tests
Skill("testing")  # → xUnit + Jest patterns

# Phase 4: Implementation
## Backend
deep-think-developer → Implement AdminStatsService

## Excel generation
Skill("document-skills:xlsx")  # → Excel patterns with formulas
Skill("theme-factory")         # → Theming patterns
deep-think-developer → Implement ExcelExportService with charts

## Frontend
typescript-expert-developer → Add export button with download

# Phase 8: Documentation
Skill("development")  # → API docs conventions
# Update docs/api/export-endpoints.md
```

---

## Troubleshooting

### Skill non si carica
```bash
# Verifica skill disponibili
# (Skills utente in .claude/skills/, plugin pre-configured)
```

### Skill restituisce pattern non adatti
```bash
# Skill fornisce pattern generici → Adatta al tuo caso
# Example: Skill("testing") → pytest patterns → Adatta a xUnit C#
```

### Quando NON usare skill
```bash
# Se task è altamente specifico del dominio
# → Usa Agent direttamente (deep-think-developer, typescript-expert-developer)
```

---

## Riferimenti

- **Comando /work**: `.claude/commands/work.md` (workflow completo con skill integration)
- **Comando /issue**: `.claude/commands/issue.md` (BDD workflow)
- **Skill utente**: `.claude/skills/` (project-level skills)
- **Plugin skills**: Anthropic Agent Skills (system-level)

---

**Version:** 1.0
**Author:** MeepleAI Development Team
**Last Updated:** 2025-10-19

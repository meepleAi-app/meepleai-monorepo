# GitHub Actions Flow Diagram

Questo documento rappresenta la sequenza completa di GitHub Actions attivate da vari eventi nel repository MeepleAI.

> **Nota**: I diagrammi sono stati suddivisi in sezioni separate per migliorare la leggibilità. Ogni workflow è rappresentato in dettaglio nella propria sezione.

---

## 📋 Indice

1. [Overview: Trigger e Workflow](#overview-trigger-e-workflow)
2. [CI Workflow (ci.yml)](#ci-workflow-ciyml)
3. [Security Workflow (security-scan.yml)](#security-workflow-security-scanyml)
4. [K6 Performance Workflow (k6-performance.yml)](#k6-performance-workflow-k6-performanceyml)
5. [Lighthouse CI Workflow (lighthouse-ci.yml)](#lighthouse-ci-workflow-lighthouse-ciyml)
6. [Storybook Deploy Workflow (storybook-deploy.yml)](#storybook-deploy-workflow-storybook-deployyml)
7. [Dependabot Automerge Workflow (dependabot-automerge.yml)](#dependabot-automerge-workflow-dependabot-automergeyml)
8. [Migration Guard Workflow (migration-guard.yml)](#migration-guard-workflow-migration-guardyml)
9. [Sequenze di Esecuzione](#sequenze-di-esecuzione)

---

## Overview: Trigger e Workflow

Questo diagramma mostra la relazione di alto livello tra eventi trigger e workflow GitHub Actions.

```mermaid
flowchart TB
    %% Trigger Events
    subgraph TRIGGERS["🎯 EVENTI TRIGGER"]
        PR[Pull Request]
        PUSH[Push to Main]
        MERGE[Merge to Main]
        SCHEDULE[Scheduled<br/>📅 Nightly 2 AM<br/>📅 Weekly Monday]
        MANUAL[Manual Trigger<br/>workflow_dispatch]
        COMMIT[New Commit]
    end

    %% Workflow Groups
    subgraph WORKFLOWS["⚙️ WORKFLOW ATTIVI"]
        CI[CI Workflow<br/>~14 min]
        SEC[Security Scan<br/>~8 min]
        K6[K6 Performance<br/>~15-30 min]
        LH[Lighthouse CI<br/>~10 min]
        SB[Storybook Deploy<br/>~5 min]
        DB[Dependabot Automerge<br/>< 1 min]
        MIG[Migration Guard<br/>~3 min]
    end

    %% Connections from PR
    PR --> CI
    PR --> SEC
    PR --> LH
    PR --> SB
    PR --> MIG
    PR -.->|Dependabot PRs| DB

    %% Connections from Push/Merge
    PUSH --> CI
    PUSH --> SEC
    MERGE --> CI
    MERGE --> SEC
    MERGE --> SB

    %% Connections from Commit
    COMMIT --> CI
    COMMIT --> SEC

    %% Scheduled connections
    SCHEDULE -.->|Nightly 2 AM| CI
    SCHEDULE -.->|Nightly 2 AM| K6
    SCHEDULE -.->|Weekly Monday| SEC

    %% Manual connections
    MANUAL --> CI
    MANUAL --> SEC
    MANUAL --> K6

    %% Styling
    classDef triggerClass fill:#E3F2FD,stroke:#1976D2,stroke-width:2px,color:#000
    classDef workflowClass fill:#FFF9C4,stroke:#F57C00,stroke-width:2px,color:#000

    class PR,PUSH,MERGE,SCHEDULE,MANUAL,COMMIT triggerClass
    class CI,SEC,K6,LH,SB,DB,MIG workflowClass
```

### Legenda Eventi Trigger

| Evento | Descrizione | Frequenza |
|--------|-------------|-----------|
| **Pull Request** | Apertura, sincronizzazione o riapertura di una PR | On-demand |
| **Push to Main** | Push diretto al branch main | On-demand |
| **Merge to Main** | Merge di una PR nel branch main | On-demand |
| **Scheduled** | Esecuzione programmata (cron) | Nightly 2 AM UTC + Weekly Monday |
| **Manual Trigger** | Esecuzione manuale via `workflow_dispatch` | On-demand |
| **New Commit** | Nuovo commit su PR o branch | On-demand |

---

## CI Workflow (ci.yml)

Il workflow CI è il cuore del sistema di testing e validazione. Include detection intelligente dei path modificati per eseguire solo i test necessari.

```mermaid
flowchart TB
    START([CI Start]) --> DETECT

    %% Path Detection
    subgraph DETECTION["🔍 PATH DETECTION"]
        DETECT[Detect Changes<br/>dorny/paths-filter]

        WEB_PATH[Web Files<br/>apps/web/**]
        API_PATH[API Files<br/>apps/api/**]
        INFRA_PATH[Infra Files<br/>infra/**]
        SCHEMA_PATH[Schema Files<br/>schemas/**]

        DETECT --> WEB_PATH
        DETECT --> API_PATH
        DETECT --> INFRA_PATH
        DETECT --> SCHEMA_PATH
    end

    %% Validation Phase
    subgraph VALIDATION["✅ VALIDATION PHASE"]
        VAL_SCHEMA[Validate RuleSpec<br/>Schemas]
        VAL_CODEGEN[Validate API<br/>Code Generation]
        VAL_OBS[Validate Prometheus<br/>& Alertmanager]
    end

    %% Web Testing
    subgraph WEB_TESTS["🌐 WEB TESTING (90%+ Coverage)"]
        WEB_UNIT[Web Unit Tests<br/>✓ Jest + Coverage<br/>✓ Lint + Typecheck<br/>✓ Build Storybook<br/>✓ A11y Unit Tests]
        WEB_E2E[Web E2E Tests<br/>✓ Playwright<br/>✓ Sequential Groups]
        WEB_A11Y[Web A11y E2E<br/>✓ Playwright<br/>✓ WCAG 2.1 AA]
    end

    %% API Testing
    subgraph API_TESTS["🔧 API TESTING (90%+ Coverage)"]
        API_SMOKE[API Smoke Tests<br/>✓ Newman + Postman<br/>✓ 11+ Collections]
        API_UNIT[API Unit + Integration<br/>✓ xUnit + Testcontainers<br/>✓ Coverage 90%<br/>✓ Random Order Test]
        API_QUALITY[API Quality Tests<br/>✓ RAG Evaluation<br/>✓ 5-Metric Framework]
        API_LLM[API LLM Integration<br/>✓ Ollama Tests<br/>⏰ Nightly Only]
    end

    %% Flow
    SCHEMA_PATH --> VAL_SCHEMA
    API_PATH --> VAL_CODEGEN
    INFRA_PATH --> VAL_OBS

    WEB_PATH --> WEB_UNIT
    WEB_PATH --> WEB_E2E
    WEB_PATH --> WEB_A11Y

    API_PATH --> API_SMOKE
    API_PATH --> API_UNIT
    API_PATH --> API_QUALITY

    DETECT -.->|Only Schedule/Manual| API_LLM

    %% End
    VAL_SCHEMA --> DONE
    VAL_CODEGEN --> DONE
    VAL_OBS --> DONE
    WEB_UNIT --> DONE
    WEB_E2E --> DONE
    WEB_A11Y --> DONE
    API_SMOKE --> DONE
    API_UNIT --> DONE
    API_QUALITY --> DONE
    API_LLM -.-> DONE

    DONE([✅ CI Complete<br/>~14 min])

    %% Styling
    classDef detectClass fill:#E3F2FD,stroke:#1976D2,stroke-width:2px,color:#000
    classDef validationClass fill:#FFF9C4,stroke:#F57C00,stroke-width:2px,color:#000
    classDef testClass fill:#C8E6C9,stroke:#388E3C,stroke-width:2px,color:#000
    classDef successClass fill:#A5D6A7,stroke:#2E7D32,stroke-width:3px,color:#000

    class DETECT,WEB_PATH,API_PATH,INFRA_PATH,SCHEMA_PATH detectClass
    class VAL_SCHEMA,VAL_CODEGEN,VAL_OBS validationClass
    class WEB_UNIT,WEB_E2E,WEB_A11Y,API_SMOKE,API_UNIT,API_QUALITY,API_LLM testClass
    class DONE successClass
```

### Path Filters & Ottimizzazioni

Il workflow CI utilizza `dorny/paths-filter` per eseguire solo i job necessari:

| Path Pattern | Trigger | Job Eseguiti |
|--------------|---------|--------------|
| `apps/web/**` | Web files | Web unit, E2E, A11y, Lighthouse |
| `apps/api/**` | API files | API unit, smoke, quality tests |
| `infra/**` | Infrastructure | Observability validation |
| `schemas/**` | Schema files | Schema validation |
| `Migrations/**` | Migration files | Migration guard |
| `components/**`, `.storybook/**` | Component files | Storybook build |

---

## Security Workflow (security-scan.yml)

Workflow di sicurezza con SAST, dependency scanning e code analysis.

```mermaid
flowchart TB
    START([🔒 Security Start]) --> PARALLEL

    %% SAST Analysis
    subgraph SAST["🔍 SAST ANALYSIS"]
        CODEQL_CS[CodeQL C#<br/>security-extended<br/>~5 min]
        CODEQL_JS[CodeQL JavaScript<br/>security-and-quality<br/>~4 min]
    end

    %% Dependency Scanning
    subgraph DEPS["📦 DEPENDENCY SCANNING"]
        DEP_DOTNET[.NET Dependencies<br/>dotnet list package<br/>--vulnerable]
        DEP_NPM[NPM Dependencies<br/>pnpm audit<br/>--audit-level=high]
    end

    %% Code Security
    subgraph CODE_SEC["🛡️ CODE SECURITY"]
        DOTNET_SEC[.NET Security Scan<br/>✓ NetAnalyzers<br/>✓ SonarAnalyzer<br/>✓ Meziantou]
        SEMGREP[Semgrep SAST<br/>✓ security-audit<br/>✓ secrets<br/>✓ owasp-top-10]
    end

    PARALLEL{Run in Parallel} --> CODEQL_CS
    PARALLEL --> CODEQL_JS
    PARALLEL --> DEP_DOTNET
    PARALLEL --> DEP_NPM
    PARALLEL --> DOTNET_SEC
    PARALLEL --> SEMGREP

    %% Summary
    CODEQL_CS --> SUMMARY
    CODEQL_JS --> SUMMARY
    DEP_DOTNET --> SUMMARY
    DEP_NPM --> SUMMARY
    DOTNET_SEC --> SUMMARY
    SEMGREP --> SUMMARY

    SUMMARY[📊 Security Summary<br/>Job Status Report]

    SUMMARY --> CHECK{HIGH/CRITICAL<br/>Vulnerabilities?}

    CHECK -->|Yes| FAIL[❌ FAIL BUILD<br/>Security issues found]
    CHECK -->|No| SUCCESS

    FAIL --> END
    SUCCESS[✅ Security Complete<br/>~8 min] --> END([End])

    %% Styling
    classDef sastClass fill:#FFCCBC,stroke:#E64A19,stroke-width:2px,color:#000
    classDef depsClass fill:#F8BBD0,stroke:#C2185B,stroke-width:2px,color:#000
    classDef codeClass fill:#FFE0B2,stroke:#F57C00,stroke-width:2px,color:#000
    classDef failClass fill:#FFCDD2,stroke:#D32F2F,stroke-width:3px,color:#000
    classDef successClass fill:#A5D6A7,stroke:#2E7D32,stroke-width:3px,color:#000

    class CODEQL_CS,CODEQL_JS sastClass
    class DEP_DOTNET,DEP_NPM depsClass
    class DOTNET_SEC,SEMGREP codeClass
    class FAIL failClass
    class SUCCESS successClass
```

### Security Quality Gates

| Tipo | Strumento | Enforcement |
|------|-----------|-------------|
| **SAST** | CodeQL + Semgrep | HIGH/CRITICAL = fail build |
| **Dependencies** | dotnet + pnpm audit | HIGH/CRITICAL vulns = fail |
| **Code Analysis** | NetAnalyzers + SonarAnalyzer | Enforced rules |
| **Secrets Detection** | Semgrep secrets | Auto-fail |

---

## K6 Performance Workflow (k6-performance.yml)

Workflow di performance testing con K6 per load testing, stress testing e smoke testing.

```mermaid
flowchart TB
    START([📊 K6 Start]) --> SETUP

    SETUP[Setup Services<br/>✓ PostgreSQL<br/>✓ Redis<br/>✓ Qdrant<br/>✓ Build API<br/>✓ Apply Migrations<br/>✓ Seed Test Data]

    SETUP --> TEST_TYPE{Test Type}

    TEST_TYPE -->|Scheduled| SMOKE[Smoke Test<br/>Minimal Load<br/>5 VUs, 30s]
    TEST_TYPE -->|Manual: load| LOAD[Load Test<br/>Target: 100 VUs<br/>Duration: 5min]
    TEST_TYPE -->|Manual: stress| STRESS[Stress Test<br/>Ramp to 200 VUs<br/>Duration: 10min]
    TEST_TYPE -->|Manual: spike| SPIKE[Spike Test<br/>Sudden 500 VUs<br/>Duration: 3min]

    SMOKE --> REPORT
    LOAD --> REPORT
    STRESS --> REPORT
    SPIKE --> REPORT

    REPORT[Generate HTML Report<br/>Upload Artifacts<br/>Retention: 90 days]

    REPORT --> CHECK{Tests<br/>Failed?}

    CHECK -->|Yes + Scheduled| NOTIFY[Send Notifications<br/>✓ Slack Alert<br/>✓ GitHub Issue<br/>✓ Include Report]
    CHECK -->|Yes + Manual| FAIL[❌ Tests Failed<br/>Review report]
    CHECK -->|No| SUCCESS[✅ K6 Complete<br/>~15-30 min]

    NOTIFY --> END
    FAIL --> END
    SUCCESS --> END([End])

    %% Styling
    classDef setupClass fill:#E1BEE7,stroke:#7B1FA2,stroke-width:2px,color:#000
    classDef testClass fill:#B2DFDB,stroke:#00796B,stroke-width:2px,color:#000
    classDef reportClass fill:#DCEDC8,stroke:#689F38,stroke-width:2px,color:#000
    classDef failClass fill:#FFCDD2,stroke:#D32F2F,stroke-width:3px,color:#000
    classDef successClass fill:#A5D6A7,stroke:#2E7D32,stroke-width:3px,color:#000

    class SETUP setupClass
    class SMOKE,LOAD,STRESS,SPIKE testClass
    class REPORT reportClass
    class FAIL,NOTIFY failClass
    class SUCCESS successClass
```

### K6 Test Types

| Tipo | VUs | Durata | Trigger | Scopo |
|------|-----|--------|---------|-------|
| **Smoke** | 5 | 30s | Scheduled (nightly) | Validazione base |
| **Load** | 100 | 5min | Manual | Performance normale |
| **Stress** | 200 (ramp) | 10min | Manual | Limiti sistema |
| **Spike** | 500 (sudden) | 3min | Manual | Resilienza picchi |

---

## Lighthouse CI Workflow (lighthouse-ci.yml)

Workflow per performance testing frontend e Core Web Vitals.

```mermaid
flowchart TB
    START([💡 Lighthouse Start]) --> DETECT

    DETECT[Detect Web Changes<br/>apps/web/**]

    DETECT -->|Changes found| BUILD[Build Next.js<br/>✓ Production build<br/>✓ Shared cache<br/>✓ Optimization]
    DETECT -->|No changes| SKIP[Skip Lighthouse<br/>No web changes]

    BUILD --> PARALLEL{Run Tests<br/>in Parallel}

    %% Performance Tests
    subgraph TESTS["⚡ PERFORMANCE TESTS"]
        LH_PERF[Lighthouse Performance<br/>✓ Playwright<br/>✓ Core Web Vitals<br/>✓ LCP, FCP, TBT, CLS]
        LH_CLI[Lighthouse CLI<br/>✓ Performance Scores<br/>✓ A11y Checks<br/>✓ Best Practices<br/>✓ SEO]
    end

    PARALLEL --> LH_PERF
    PARALLEL --> LH_CLI

    LH_PERF --> COMPARE
    LH_CLI --> COMPARE

    COMPARE[Compare with Base<br/>Threshold: >10% regression<br/>Metrics: LCP, FCP, TBT, CLS]

    COMPARE --> CHECK{Performance<br/>Regression?}

    CHECK -->|Yes| FAIL[❌ FAIL BUILD<br/>Performance regression >10%<br/>Optimization required]
    CHECK -->|No| COMMENT[PR Comment<br/>✓ Performance Results<br/>✓ Core Web Vitals<br/>✓ Scores Comparison]

    COMMENT --> SUCCESS[✅ Lighthouse Complete<br/>~10 min]
    FAIL --> END
    SUCCESS --> END
    SKIP --> END([End])

    %% Styling
    classDef detectClass fill:#E3F2FD,stroke:#1976D2,stroke-width:2px,color:#000
    classDef buildClass fill:#FFF9C4,stroke:#F57C00,stroke-width:2px,color:#000
    classDef testClass fill:#E1BEE7,stroke:#7B1FA2,stroke-width:2px,color:#000
    classDef failClass fill:#FFCDD2,stroke:#D32F2F,stroke-width:3px,color:#000
    classDef successClass fill:#A5D6A7,stroke:#2E7D32,stroke-width:3px,color:#000

    class DETECT detectClass
    class BUILD buildClass
    class LH_PERF,LH_CLI,COMPARE testClass
    class FAIL failClass
    class SUCCESS successClass
```

### Core Web Vitals Thresholds

| Metrica | Good | Needs Improvement | Poor | Regression Limit |
|---------|------|-------------------|------|------------------|
| **LCP** (Largest Contentful Paint) | ≤2.5s | 2.5-4.0s | >4.0s | >10% |
| **FCP** (First Contentful Paint) | ≤1.8s | 1.8-3.0s | >3.0s | >10% |
| **TBT** (Total Blocking Time) | ≤200ms | 200-600ms | >600ms | >10% |
| **CLS** (Cumulative Layout Shift) | ≤0.1 | 0.1-0.25 | >0.25 | >10% |

---

## Storybook Deploy Workflow (storybook-deploy.yml)

Workflow per visual testing con Chromatic.

```mermaid
flowchart TB
    START([📚 Storybook Start]) --> DETECT

    DETECT[Detect Component Changes<br/>components/**<br/>.storybook/**]

    DETECT -->|Changes found| BUILD[Build Storybook<br/>pnpm build-storybook<br/>~3 min]
    DETECT -->|No changes| SKIP[Skip Storybook<br/>No component changes]

    BUILD --> CHROMATIC[Publish to Chromatic<br/>✓ Visual Testing<br/>✓ UI Review<br/>✓ Auto-accept unchanged<br/>✓ onlyChanged: true]

    CHROMATIC --> COMMENT[PR Comment<br/>✓ Preview Links<br/>✓ Visual Changes<br/>✓ Storybook URL]

    COMMENT --> SUCCESS[✅ Storybook Complete<br/>~5 min]

    SUCCESS --> END
    SKIP --> END([End])

    %% Styling
    classDef detectClass fill:#E3F2FD,stroke:#1976D2,stroke-width:2px,color:#000
    classDef buildClass fill:#FFF9C4,stroke:#F57C00,stroke-width:2px,color:#000
    classDef chromaticClass fill:#E1BEE7,stroke:#7B1FA2,stroke-width:2px,color:#000
    classDef successClass fill:#A5D6A7,stroke:#2E7D32,stroke-width:3px,color:#000

    class DETECT detectClass
    class BUILD buildClass
    class CHROMATIC,COMMENT chromaticClass
    class SUCCESS successClass
```

### Chromatic Features

| Feature | Descrizione | Beneficio |
|---------|-------------|-----------|
| **Visual Testing** | Snapshot comparison automatico | Detect UI regressions |
| **UI Review** | Commenti direttamente sui componenti | Collaboration migliorata |
| **Auto-accept** | Skip componenti non modificati | Performance migliorata |
| **Preview Links** | Link diretti nella PR | Review facilitato |

---

## Dependabot Automerge Workflow (dependabot-automerge.yml)

Workflow per auto-merge automatico delle PR Dependabot con label `automerge`.

```mermaid
flowchart TB
    START([🤖 Dependabot PR]) --> CHECK

    CHECK{Is Dependabot<br/>+ automerge label?}

    CHECK -->|No| SKIP[Skip Auto-merge<br/>Manual review required]
    CHECK -->|Yes| STATUS

    STATUS[Check PR Status<br/>✓ CI Passing?<br/>✓ Security OK?<br/>✓ All Checks Complete?]

    STATUS --> PENDING{Checks<br/>Pending?}

    PENDING -->|Yes| WAIT[Wait for CI<br/>Re-run on completion<br/>⏳ Polling every 60s]
    PENDING -->|No| SHOULD

    SHOULD{Should<br/>Merge?}

    SHOULD -->|Yes| MERGE[Auto-merge PR<br/>✓ Squash Strategy<br/>✓ Add Comment<br/>✓ Delete branch]
    SHOULD -->|No| MANUAL[Manual Review Required<br/>Checks failed or<br/>security issues]

    WAIT --> END
    MERGE --> SUCCESS[✅ Auto-merged<br/>Dependency updated]
    MANUAL --> END
    SKIP --> END

    SUCCESS --> END([End])

    %% Styling
    classDef checkClass fill:#E3F2FD,stroke:#1976D2,stroke-width:2px,color:#000
    classDef waitClass fill:#FFF9C4,stroke:#F57C00,stroke-width:2px,color:#000
    classDef mergeClass fill:#C8E6C9,stroke:#388E3C,stroke-width:2px,color:#000
    classDef skipClass fill:#F5F5F5,stroke:#9E9E9E,stroke-width:2px,color:#000
    classDef successClass fill:#A5D6A7,stroke:#2E7D32,stroke-width:3px,color:#000

    class CHECK,STATUS,SHOULD checkClass
    class WAIT waitClass
    class MERGE mergeClass
    class SKIP,MANUAL skipClass
    class SUCCESS successClass
```

### Auto-merge Criteria

| Criterio | Requirement | Azione se fallisce |
|----------|-------------|-------------------|
| **Label** | `automerge` presente | Skip auto-merge |
| **Author** | Dependabot | Skip auto-merge |
| **CI Status** | All checks passed | Wait or manual review |
| **Security** | No HIGH/CRITICAL vulns | Manual review |
| **Conflicts** | No merge conflicts | Manual review |

---

## Migration Guard Workflow (migration-guard.yml)

Workflow per validazione delle migrazioni EF Core e prevenzione di breaking changes.

```mermaid
flowchart TB
    START([🗄️ Migration PR]) --> DETECT

    DETECT[Detect Migration Changes<br/>Migrations/**/*.cs]

    DETECT -->|Changes found| COMPARE[Compare Migrations<br/>Base branch vs PR<br/>Check deleted files]
    DETECT -->|No changes| SKIP[Skip Migration Guard<br/>No migration changes]

    COMPARE --> CHECK_DELETED{Migrations<br/>Deleted?}

    CHECK_DELETED -->|Yes| FAIL_DELETE[❌ FAIL BUILD<br/>Migrations cannot be deleted<br/>BREAKING CHANGE<br/>⚠️ Create rollback instead]

    CHECK_DELETED -->|No| NAMING[Check Naming Convention<br/>Pattern: YYYYMMDDHHMMSS_Name.cs<br/>Validate timestamp format]

    NAMING --> NAMING_OK{Naming<br/>Valid?}

    NAMING_OK -->|No| FAIL_NAMING[❌ FAIL BUILD<br/>Invalid migration naming<br/>Expected: YYYYMMDDHHMMSS_Name.cs]

    NAMING_OK -->|Yes| SQL[Generate SQL Scripts<br/>✓ migration-preview.sql<br/>✓ migration-pending.sql<br/>✓ Idempotent script]

    SQL --> COMMENT[PR Comment<br/>✓ SQL Preview<br/>✓ Validation Results<br/>✓ Migration list<br/>✓ Impact analysis]

    COMMENT --> SUCCESS[✅ Migration Check Complete<br/>~3 min]

    FAIL_DELETE --> END
    FAIL_NAMING --> END
    SUCCESS --> END
    SKIP --> END([End])

    %% Styling
    classDef detectClass fill:#E3F2FD,stroke:#1976D2,stroke-width:2px,color:#000
    classDef checkClass fill:#FFF9C4,stroke:#F57C00,stroke-width:2px,color:#000
    classDef sqlClass fill:#C8E6C9,stroke:#388E3C,stroke-width:2px,color:#000
    classDef failClass fill:#FFCDD2,stroke:#D32F2F,stroke-width:3px,color:#000
    classDef successClass fill:#A5D6A7,stroke:#2E7D32,stroke-width:3px,color:#000

    class DETECT detectClass
    class COMPARE,NAMING checkClass
    class SQL,COMMENT sqlClass
    class FAIL_DELETE,FAIL_NAMING failClass
    class SUCCESS successClass
```

### Migration Validation Rules

| Rule | Description | Enforcement |
|------|-------------|-------------|
| **No Deletion** | Migrations cannot be deleted | Hard fail (breaking change) |
| **Naming Convention** | `YYYYMMDDHHMMSS_Name.cs` | Hard fail |
| **SQL Preview** | Generate preview scripts | Informational |
| **Idempotent** | Scripts must be rerunnable | Warning |

---

## Sequenze di Esecuzione

### Sequenza Tipica per Pull Request

```mermaid
sequenceDiagram
    participant Dev as 👨‍💻 Developer
    participant GH as GitHub
    participant CI as ⚙️ CI Workflow
    participant SEC as 🔒 Security
    participant LH as 💡 Lighthouse
    participant MIG as 🗄️ Migration Guard
    participant SB as 📚 Storybook
    participant DB as 🤖 Dependabot

    Dev->>GH: Push commits to PR branch

    par Parallel Workflow Triggers
        GH->>CI: Trigger CI workflow
        GH->>SEC: Trigger Security workflow
    end

    alt Web files changed
        GH->>LH: Trigger Lighthouse CI
        LH->>LH: Build Next.js (shared)
        LH->>LH: Run performance tests
        LH->>LH: Check regression vs base
        LH-->>GH: Comment PR with results
    end

    alt Component files changed
        GH->>SB: Trigger Storybook Deploy
        SB->>SB: Build Storybook
        SB->>SB: Publish to Chromatic
        SB-->>GH: Comment PR with preview link
    end

    alt Migration files changed
        GH->>MIG: Trigger Migration Guard
        MIG->>MIG: Compare base vs PR migrations
        MIG->>MIG: Check for deleted migrations
        MIG->>MIG: Generate SQL scripts
        MIG-->>GH: Comment PR with SQL preview
    end

    CI->>CI: Detect changed paths

    par CI Testing (based on paths)
        CI->>CI: Run web tests (if web changed)
        CI->>CI: Run API tests (if API changed)
        CI->>CI: Validate schemas (if schema changed)
    end

    par Security Scanning
        SEC->>SEC: CodeQL SAST (C# + JS)
        SEC->>SEC: Dependency scan (.NET + npm)
        SEC->>SEC: Semgrep SAST + secrets
    end

    CI-->>GH: Report test results ✅
    SEC-->>GH: Report security findings 🔒

    alt All checks pass
        GH->>Dev: ✅ All checks passed

        alt PR is from Dependabot + has automerge label
            GH->>DB: Trigger Dependabot Automerge
            DB->>DB: Verify all checks passed
            DB->>GH: Auto-merge PR (squash)
            DB-->>GH: Comment PR with merge confirmation
        else Manual merge
            Dev->>GH: Manually merge PR
        end
    else Some checks failed
        GH->>Dev: ❌ Checks failed - review required
    end
```

---

### Sequenza per Push/Merge to Main

```mermaid
sequenceDiagram
    participant Dev as 👨‍💻 Developer
    participant GH as GitHub
    participant CI as ⚙️ CI Workflow
    participant SEC as 🔒 Security
    participant SB as 📚 Storybook

    Dev->>GH: Merge PR to main

    par Full CI + Security
        GH->>CI: Trigger CI workflow (full suite)
        GH->>SEC: Trigger Security workflow
    end

    CI->>CI: Run ALL tests (no path filtering)
    CI->>CI: Upload coverage to Codecov

    SEC->>SEC: CodeQL full analysis
    SEC->>SEC: Comprehensive dependency scan
    SEC->>SEC: Security code analysis

    alt Component files in merge
        GH->>SB: Trigger Storybook Deploy
        SB->>SB: Build and publish to Chromatic
    end

    CI-->>GH: Report results to main branch ✅
    SEC-->>GH: Upload SARIF to Security tab 🔒

    alt All checks pass
        GH->>Dev: ✅ Main branch build succeeded
    else Some checks failed
        GH->>Dev: ❌ Main branch build failed
        Note over GH,Dev: 🚨 Immediate notification required
    end
```

---

### Sequenza per Scheduled Runs (Nightly)

```mermaid
sequenceDiagram
    participant CRON as ⏰ GitHub Scheduler
    participant CI as ⚙️ CI Workflow
    participant K6 as 📊 K6 Performance
    participant SEC as 🔒 Security
    participant TEAM as 👥 Team

    Note over CRON: Every day at 2 AM UTC

    par Nightly Triggers
        CRON->>CI: Trigger nightly CI run
        CRON->>K6: Trigger K6 performance tests
    end

    CI->>CI: Run full test suite (no path filtering)
    CI->>CI: Run LLM integration tests (Ollama)
    CI->>CI: Upload coverage + RAG evaluation

    K6->>K6: Setup services (PG, Redis, Qdrant)
    K6->>K6: Build API + apply migrations
    K6->>K6: Run smoke tests (default)
    K6->>K6: Generate HTML reports

    alt K6 tests fail
        K6->>TEAM: 📧 Send Slack notification
        K6->>GH: 🐛 Create/update GitHub issue
        K6-->>CRON: Report failure ❌
    else K6 tests pass
        K6->>K6: Save baseline report (90 days)
        K6-->>CRON: Report success ✅
    end

    Note over CRON: Every Monday at 00:00 UTC

    CRON->>SEC: Trigger weekly security scan
    SEC->>SEC: Full CodeQL analysis
    SEC->>SEC: Comprehensive dependency audit
    SEC->>SEC: Semgrep SAST + secrets scan
    SEC-->>GH: Upload results to Security tab

    CI-->>CRON: Report nightly results
    SEC-->>CRON: Report weekly security status
```

---

## Dettagli Tecnici

### Concurrency Control

Tutti i workflow utilizzano `concurrency.cancel-in-progress: true` per evitare l'accumulo di esecuzioni:

```yaml
concurrency:
  group: ci-${{ github.workflow }}-${{ github.event.pull_request.number || github.ref }}
  cancel-in-progress: true
```

**Benefici**:
- Risparmio risorse CI/CD
- Feedback più veloce su nuovi commit
- Evita code di esecuzione

---

### Permission Model (Least Privilege)

Ogni workflow dichiara esplicitamente le permission minime necessarie (Issue #1455):

```yaml
permissions:
  contents: read        # Checkout code
  pull-requests: write  # Comment on PRs
  checks: write         # Report test results
  actions: write        # Upload artifacts
  security-events: write # Upload SARIF (security only)
```

**Principio**: Ogni job ottiene solo i permessi strettamente necessari.

---

### Artifact Retention

| Tipo Artifact | Retention | Motivo | Size Avg |
|---------------|-----------|---------|----------|
| Coverage reports | 7 days | Debug failures | ~50 MB |
| Security reports (SARIF) | 30 days | Audit trail | ~5 MB |
| Newman reports | 14 days | API validation | ~10 MB |
| K6 baseline | 90 days | Performance trending | ~20 MB |
| Migration SQL | 30 days | Database audit | ~1 MB |
| Playwright reports | 7 days | E2E debugging | ~100 MB |
| Lighthouse reports | 14 days | Performance tracking | ~10 MB |

---

### Notifiche e Alerting

| Evento | Canale | Condizione | Destinatari |
|--------|--------|------------|-------------|
| **K6 Failures** | Slack + GitHub Issue | Scheduled runs only | @team-backend |
| **Security HIGH/CRITICAL** | GitHub Security tab | Always | @team-security |
| **Performance Regression >10%** | PR comment | PR only | PR author |
| **Migration Deleted** | PR comment + fail build | Always | PR author |
| **Dependabot Auto-merge** | PR comment | Successful merge | @dependabot |
| **Main Build Failure** | Email + Slack | Push to main | @team-all |

---

## Test Coverage & Quality Gates

| Area | Target | Strumento | Enforcement | Fail Build |
|------|--------|-----------|-------------|------------|
| **Frontend Unit** | ≥90% | Jest + RTL | ✅ Enforced | Yes |
| **Backend Unit + Integration** | ≥90% | xUnit + Coverlet | ✅ Enforced | Yes |
| **E2E** | Critical paths | Playwright | ⚠️ Warning | No |
| **A11y** | WCAG 2.1 AA | jest-axe + Playwright | ⚠️ Warning | No |
| **Performance** | Core Web Vitals | Lighthouse | ✅ Enforced (>10%) | Yes |
| **Security** | No HIGH/CRITICAL | CodeQL + Semgrep | ✅ Enforced | Yes |
| **RAG Quality** | 5-metric framework | Custom evaluator | ✅ Enforced | Yes |
| **Dependencies** | No vulnerable deps | dotnet + pnpm audit | ✅ Enforced | Yes |

---

## Palette Colori

I diagrammi utilizzano una palette di colori standard web-safe per garantire la massima compatibilità:

| Colore | Hex | Utilizzo |
|--------|-----|----------|
| **Blu Chiaro** | #E3F2FD | Trigger events, detection |
| **Verde Chiaro** | #C8E6C9 | Test, validation, success states |
| **Arancione Chiaro** | #FFCCBC | Security, SAST |
| **Rosa Chiaro** | #F8BBD0 | Dependencies |
| **Viola Chiaro** | #E1BEE7 | Performance testing |
| **Giallo Chiaro** | #FFF9C4 | Workflow, build phases |
| **Rosso Chiaro** | #FFCDD2 | Failures, errors |
| **Verde Scuro** | #A5D6A7 | Successful completion |
| **Grigio Chiaro** | #F5F5F5 | Skip, neutral states |

Questi colori seguono le linee guida Material Design e sono ottimizzati per:
- ✅ Accessibilità (WCAG 2.1 AA)
- ✅ Compatibilità GitHub
- ✅ Stampa in bianco/nero
- ✅ Daltonismo (protanopia, deuteranopia, tritanopia)

---

## Riepilogo Workflow

| Workflow | Trigger | Durata | Criticità | Path Filter |
|----------|---------|--------|-----------|-------------|
| **CI** | PR, Push, Schedule, Manual | ~14 min | 🔴 Critical | ✅ Yes |
| **Security** | PR, Push, Schedule, Manual | ~8 min | 🔴 Critical | ❌ No |
| **K6 Performance** | Schedule (nightly), Manual | ~15-30 min | 🟡 High | ❌ No |
| **Lighthouse CI** | PR (web files) | ~10 min | 🟡 High | ✅ Yes |
| **Storybook** | PR (components) | ~5 min | 🟢 Medium | ✅ Yes |
| **Dependabot** | Dependabot PR | <1 min | 🟢 Medium | ❌ No |
| **Migration Guard** | PR (migrations) | ~3 min | 🔴 Critical | ✅ Yes |

**Total Average PR Time**: ~22 min (with path filtering)
**Total Full Suite Time**: ~50 min (scheduled, no filtering)

---

**Versione**: 2.0
**Ultimo Aggiornamento**: 2025-11-22
**Autore**: Claude (GitHub Actions Flow Analysis - Refactored)
**Changelog**:
- 2.0: Diagrammi separati per workflow, colori standard, migliorata leggibilità
- 1.0: Versione iniziale con diagramma unico complesso

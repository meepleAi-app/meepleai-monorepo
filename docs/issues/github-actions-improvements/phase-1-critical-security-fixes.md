# Fase 1 - Fix Critici di Sicurezza

**Priorità**: 🔴 CRITICO
**Timeline**: Entro 1 settimana
**Stima effort**: 4-6 ore
**Impatto**: Sicurezza del sistema

---

## 📋 Task Overview

| Task | File | Linee | Effort | Status |
|------|------|-------|--------|--------|
| Fix credenziali hardcoded | k6-performance.yml | 42, 94, 101, 117, 265 | 2h | ⬜ Todo |
| Aggiungere permissions | Tutti i workflow | Varie | 2h | ⬜ Todo |
| Aggiornare SecurityCodeScan | security-scan.yml | 252 | 1h | ⬜ Todo |

---

## 🔴 Issue #1: Credenziali Hardcoded nel K6 Workflow

### Problema
Le credenziali del database PostgreSQL sono hardcoded nel workflow `k6-performance.yml`.

**Severità**: 🔴 CRITICO
**CVE Risk**: CWE-798 (Use of Hard-coded Credentials)

### Codice Problematico

**File**: `.github/workflows/k6-performance.yml`

```yaml
# ❌ LINEA 42-43
postgres:
  image: postgres:16
  env:
    POSTGRES_USER: postgres
    POSTGRES_PASSWORD: postgres  # ← HARDCODED
    POSTGRES_DB: meepleai_test

# ❌ LINEA 94
PGPASSWORD=postgres psql -h localhost -U postgres -c "CREATE DATABASE meepleai_test;"

# ❌ LINEA 101
ConnectionStrings__Postgres: 'Host=localhost;Port=5432;Database=meepleai_test;Username=postgres;Password=postgres'

# ❌ LINEA 117
ConnectionStrings__Postgres: 'Host=localhost;Port=5432;Database=meepleai_test;Username=postgres;Password=postgres'

# ❌ LINEA 265
PGPASSWORD=postgres psql -h localhost -U postgres -c "DROP DATABASE IF EXISTS meepleai_test;"
```

### Impatto
- **Esposizione credenziali**: Visibili in tutti i workflow logs
- **Compliance risk**: Violazione GDPR/SOC2 per dati sensibili
- **Attack surface**: Possibile escalation se qualcuno ottiene accesso al runner

### Soluzione

#### Step 1: Creare Secrets nel Repository

Naviga su GitHub → Settings → Secrets and variables → Actions → New repository secret

Crea i seguenti secrets:
```
Name: TEST_POSTGRES_PASSWORD
Value: <genera password sicura>

Name: TEST_POSTGRES_USER
Value: meeple_test

Name: TEST_DB_CONNECTION_STRING
Value: Host=localhost;Port=5432;Database=meepleai_test;Username=meeple_test;Password=<password-sicura>;Maximum Pool Size=100
```

**Genera password sicura**:
```bash
# Su Linux/Mac
openssl rand -base64 32

# O usa un password manager per generare una password lunga e casuale
```

#### Step 2: Aggiornare il Workflow

**File**: `.github/workflows/k6-performance.yml`

```yaml
# ✅ LINEA 38-52 (services.postgres)
services:
  postgres:
    image: postgres:16
    env:
      POSTGRES_USER: ${{ secrets.TEST_POSTGRES_USER || 'postgres' }}
      POSTGRES_PASSWORD: ${{ secrets.TEST_POSTGRES_PASSWORD || 'postgres' }}
      POSTGRES_DB: meepleai_test
    options: >-
      --health-cmd pg_isready
      --health-interval 10s
      --health-timeout 5s
      --health-retries 5
    ports:
      - 5432:5432

# ✅ LINEA 92-95 (create test database)
- name: Create test database
  env:
    PGPASSWORD: ${{ secrets.TEST_POSTGRES_PASSWORD || 'postgres' }}
  run: |
    psql -h localhost -U ${{ secrets.TEST_POSTGRES_USER || 'postgres' }} -c "CREATE DATABASE meepleai_test;" || true

# ✅ LINEA 96-102 (apply migrations)
- name: Apply migrations
  working-directory: apps/api/src/Api
  run: |
    dotnet ef database update --configuration Release
  env:
    ConnectionStrings__Postgres: ${{ secrets.TEST_DB_CONNECTION_STRING || 'Host=localhost;Port=5432;Database=meepleai_test;Username=postgres;Password=postgres' }}

# ✅ LINEA 111-122 (start API server)
- name: Start API server
  working-directory: apps/api/src/Api
  run: |
    nohup dotnet run --configuration Release > ../../api.log 2>&1 &
    echo $! > ../../api.pid
  env:
    ConnectionStrings__Postgres: ${{ secrets.TEST_DB_CONNECTION_STRING }}
    ConnectionStrings__Redis: 'localhost:6379'
    ConnectionStrings__Qdrant: 'http://localhost:6333'
    ASPNETCORE_ENVIRONMENT: Development
    ASPNETCORE_URLS: 'http://0.0.0.0:5080'

# ✅ LINEA 262-269 (cleanup resources)
- name: Cleanup resources
  if: always()
  env:
    PGPASSWORD: ${{ secrets.TEST_POSTGRES_PASSWORD || 'postgres' }}
  run: |
    # Stop API server
    if [ -f api.pid ]; then
      kill $(cat api.pid) 2>/dev/null || true
      rm api.pid
    fi

    # Cleanup any remaining dotnet processes
    pkill -f "dotnet.*Api" 2>/dev/null || true

    # Clean database
    psql -h localhost -U ${{ secrets.TEST_POSTGRES_USER || 'postgres' }} -c "DROP DATABASE IF EXISTS meepleai_test;" 2>/dev/null || true

    # Flush Redis
    redis-cli FLUSHALL 2>/dev/null || true

    echo "✅ Cleanup complete"
```

#### Step 3: Testing

1. **Local verification**:
   ```bash
   # Testa che le variabili siano accessibili
   echo ${{ secrets.TEST_POSTGRES_PASSWORD }} # Non dovrebbe stampare nulla (è corretto)
   ```

2. **Trigger workflow manualmente**:
   - GitHub → Actions → K6 Performance Tests → Run workflow
   - Seleziona branch: `claude/review-github-actions-01NNd4r9BwoMoPBABtEc1khj`
   - Seleziona test_type: `smoke`
   - Click "Run workflow"

3. **Verifica logs**:
   - Cerca `***` nei logs (GitHub maschera automaticamente i secrets)
   - Verifica che il workflow completi con successo

### Criteri di Accettazione

- [ ] Secrets creati nel repository GitHub
- [ ] Nessuna credenziale hardcoded visibile nei file YAML
- [ ] Workflow esegue con successo usando i secrets
- [ ] Logs non mostrano credenziali in chiaro
- [ ] Fallback a valori di default funziona (per retrocompatibilità)

---

## 🔴 Issue #2: Permissions Non Esplicite

### Problema
5 dei 7 workflow non specificano permissions esplicite, ereditando `write-all` di default.

**Severità**: 🔴 ALTO
**Principio violato**: Least Privilege (OWASP)

### Workflow Interessati
1. `ci.yml`
2. `migration-guard.yml`
3. `lighthouse-ci.yml`
4. `storybook-deploy.yml`
5. `k6-performance.yml`

### Impatto
- **Over-privileged workflows**: Possono modificare codice, eliminare branch, accedere secrets
- **Supply chain risk**: Se compromessi, danno pieno accesso al repository
- **Audit trail**: Difficile tracciare quali permissions servono realmente

### Soluzione

Per ogni workflow, aggiungere permissions esplicite dopo il blocco `concurrency:`.

#### ci.yml

**File**: `.github/workflows/ci.yml`
**Inserire dopo**: linea 28 (dopo `concurrency:`)

```yaml
# Cancel previous runs on same PR/branch to avoid piling up
concurrency:
  group: ci-${{ github.workflow }}-${{ github.event.pull_request.number || github.ref }}
  cancel-in-progress: true

# ✅ AGGIUNGERE QUI
permissions:
  contents: read        # Read repository code
  checks: write         # Upload test results and coverage
  pull-requests: read   # Read PR info for path filtering
```

#### migration-guard.yml

**File**: `.github/workflows/migration-guard.yml`
**Inserire dopo**: linea 8

```yaml
on:
  pull_request:
    paths:
      - 'apps/api/src/Api/Migrations/**'
      - 'apps/api/src/Api/Infrastructure/Migrations/**'

# ✅ AGGIUNGERE QUI
permissions:
  contents: read        # Read repository code
  pull-requests: write  # Comment on PRs with migration info
```

#### lighthouse-ci.yml

**File**: `.github/workflows/lighthouse-ci.yml`
**Inserire dopo**: linea 17

```yaml
concurrency:
  group: lighthouse-ci-${{ github.workflow }}-${{ github.event.pull_request.number || github.ref }}
  cancel-in-progress: true

# ✅ AGGIUNGERE QUI
permissions:
  contents: read        # Read repository code
  pull-requests: write  # Comment on PRs with performance results
  checks: write         # Upload Lighthouse results
```

#### storybook-deploy.yml

**File**: `.github/workflows/storybook-deploy.yml`
**Inserire dopo**: linea 15

```yaml
on:
  push:
    branches:
      - main
    paths:
      - 'apps/web/src/components/**'
      - 'apps/web/.storybook/**'
      - 'apps/web/package.json'
  pull_request:
    paths:
      - 'apps/web/src/components/**'
      - 'apps/web/.storybook/**'
      - 'apps/web/package.json'

# ✅ AGGIUNGERE QUI
permissions:
  contents: read        # Read repository code
  pull-requests: write  # Comment on PRs with Storybook links
```

#### k6-performance.yml

**File**: `.github/workflows/k6-performance.yml`
**Inserire dopo**: linea 29

```yaml
env:
  API_BASE_URL: http://localhost:5080
  TEST_GAME_ID: '00000000-0000-0000-0000-000000000001'

# ✅ AGGIUNGERE QUI
permissions:
  contents: read        # Read repository code
  pull-requests: write  # Comment on PRs with performance results
  actions: read         # Download baseline artifacts
```

### Testing

1. **Push le modifiche** e crea un draft PR
2. **Verifica che i workflow partano** senza errori di permission
3. **Controlla i logs** per errori tipo "Resource not accessible by integration"
4. **Se ci sono errori**, aggiungi la permission mancante:
   ```yaml
   permissions:
     contents: read
     issues: write        # Se serve commentare su issue
     statuses: write      # Se serve aggiornare commit status
     deployments: write   # Se serve fare deploy
   ```

### Criteri di Accettazione

- [ ] Tutti e 5 i workflow hanno permissions esplicite
- [ ] Ogni permission è giustificata e documentata con commento
- [ ] Nessun workflow usa `write-all` o `read-all`
- [ ] I workflow eseguono senza errori di permission
- [ ] Draft PR passa tutti i check

---

## 🟡 Issue #3: SecurityCodeScan Deprecato

### Problema
Il workflow `security-scan.yml` usa `SecurityCodeScan.VS2019` v5.6.7, ultimo aggiornamento 2021.

**Severità**: 🟡 MEDIO
**Issue**: Nessun supporto per .NET 9, pattern moderni, nuove vulnerabilità

### Codice Problematico

**File**: `.github/workflows/security-scan.yml:249-252`

```yaml
# ❌ DEPRECATO
- name: Install Security Code Scan Analyzer
  run: |
    # SecurityCodeScan.VS2019 è fermo al 2021
    dotnet add src/Api package SecurityCodeScan.VS2019 --version 5.6.7
```

### Soluzione

Migrare ai .NET Analyzers moderni mantenuti da Microsoft e SonarSource.

#### Step 1: Rimuovere SecurityCodeScan

**File**: `.github/workflows/security-scan.yml`

```yaml
# ❌ RIMUOVERE COMPLETAMENTE lo step "Install Security Code Scan Analyzer"
# (linee 249-252)
```

#### Step 2: Aggiungere Analyzer Moderni al Progetto

**File**: `apps/api/src/Api/Api.csproj`

Aggiungi nel `<ItemGroup>` degli analyzer:

```xml
<ItemGroup>
  <!-- Existing analyzers... -->

  <!-- ✅ AGGIUNGERE QUESTI -->
  <!-- Microsoft Code Analysis (built-in .NET analyzers) -->
  <PackageReference Include="Microsoft.CodeAnalysis.NetAnalyzers" Version="9.0.0">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
  </PackageReference>

  <!-- SonarAnalyzer for C# (advanced security rules) -->
  <PackageReference Include="SonarAnalyzer.CSharp" Version="9.32.0.97167">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
  </PackageReference>

  <!-- Security Code Scan replacement for .NET 9 -->
  <PackageReference Include="Meziantou.Analyzer" Version="2.0.163">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

#### Step 3: Configurare le Regole di Security

**File**: `apps/api/.editorconfig` (crea se non esiste)

```ini
# Security rules - treat as errors
dotnet_diagnostic.CA2100.severity = error  # SQL Injection
dotnet_diagnostic.CA2109.severity = error  # Review visible event handlers
dotnet_diagnostic.CA2153.severity = error  # Corrupted state exceptions
dotnet_diagnostic.CA3001.severity = error  # XSS
dotnet_diagnostic.CA3002.severity = error  # XSS in Razor
dotnet_diagnostic.CA3003.severity = error  # File path injection
dotnet_diagnostic.CA3004.severity = error  # Info disclosure
dotnet_diagnostic.CA3005.severity = error  # LDAP injection
dotnet_diagnostic.CA3006.severity = error  # Process command injection
dotnet_diagnostic.CA3007.severity = error  # Open redirect
dotnet_diagnostic.CA3008.severity = error  # XPath injection
dotnet_diagnostic.CA3009.severity = error  # XML injection
dotnet_diagnostic.CA3010.severity = error  # XAML injection
dotnet_diagnostic.CA3011.severity = error  # DLL injection
dotnet_diagnostic.CA3012.severity = error  # Regex injection
dotnet_diagnostic.CA5350.severity = error  # Weak crypto
dotnet_diagnostic.CA5351.severity = error  # Weak crypto
dotnet_diagnostic.CA5358.severity = error  # Unsafe cipher modes
dotnet_diagnostic.CA5359.severity = error  # Disable certificate validation
dotnet_diagnostic.CA5360.severity = error  # Dangerous methods in deserialization
dotnet_diagnostic.CA5361.severity = error  # SChannel use strong crypto
dotnet_diagnostic.CA5362.severity = error  # Potential reference cycle in deserialized object
dotnet_diagnostic.CA5363.severity = error  # Request validation disabled
dotnet_diagnostic.CA5364.severity = error  # Deprecated security protocols
dotnet_diagnostic.CA5365.severity = error  # HTTP header checking disabled
dotnet_diagnostic.CA5366.severity = error  # XmlReader use XmlTextReader insecure
dotnet_diagnostic.CA5367.severity = error  # Pointer fields serialization
dotnet_diagnostic.CA5368.severity = error  # ViewState encryption mode disabled
dotnet_diagnostic.CA5369.severity = error  # XmlReader use XmlReader
dotnet_diagnostic.CA5370.severity = error  # XmlValidatingReader is unsafe
dotnet_diagnostic.CA5371.severity = error  # XmlReader use XmlSchemaSet
dotnet_diagnostic.CA5372.severity = error  # XmlTextReader use safe settings
dotnet_diagnostic.CA5373.severity = error  # Obsolete key derivation function
dotnet_diagnostic.CA5374.severity = error  # XslTransform is insecure
dotnet_diagnostic.CA5375.severity = error  # Account shared access signature
dotnet_diagnostic.CA5376.severity = error  # SharedAccessProtocol HttpsOnly
dotnet_diagnostic.CA5377.severity = error  # Container level access policy
dotnet_diagnostic.CA5378.severity = error  # ServicePointManager disabled
dotnet_diagnostic.CA5379.severity = error  # Weak Key Derivation Function
dotnet_diagnostic.CA5380.severity = error  # Certificates validation disabled
dotnet_diagnostic.CA5381.severity = error  # Certificates not added to root store
dotnet_diagnostic.CA5382.severity = error  # Use secure cookies in ASP.NET Core
dotnet_diagnostic.CA5383.severity = error  # Use secure cookies in ASP.NET
dotnet_diagnostic.CA5384.severity = error  # DSA is insecure
dotnet_diagnostic.CA5385.severity = error  # Use Rivest-Shamir-Adleman algorithm with sufficient key size
dotnet_diagnostic.CA5386.severity = error  # Avoid hardcoding SecurityProtocolType
dotnet_diagnostic.CA5387.severity = error  # Weak key derivation function with insufficient iterations
dotnet_diagnostic.CA5388.severity = error  # Weak key derivation function when used with weak algorithm
dotnet_diagnostic.CA5389.severity = error  # Path validation disabled
dotnet_diagnostic.CA5390.severity = error  # Hardcoded encryption key
dotnet_diagnostic.CA5391.severity = error  # Use antiforgery tokens in ASP.NET Core
dotnet_diagnostic.CA5392.severity = error  # P/Invoke DefaultDllImportSearchPaths
dotnet_diagnostic.CA5393.severity = error  # DllImportSearchPath value unsafe
dotnet_diagnostic.CA5394.severity = error  # Insecure randomness
dotnet_diagnostic.CA5395.severity = error  # HttpVerb missing for action methods
dotnet_diagnostic.CA5396.severity = error  # HttpOnly not set for HttpCookie
dotnet_diagnostic.CA5397.severity = error  # Deprecated SslProtocols values
dotnet_diagnostic.CA5398.severity = error  # Avoid hardcoded SslProtocols values
dotnet_diagnostic.CA5399.severity = error  # HttpClients should enable certificate revocation
dotnet_diagnostic.CA5400.severity = error  # HttpClient certificate revocation
dotnet_diagnostic.CA5401.severity = error  # CreateEncryptor with non-default IV
dotnet_diagnostic.CA5402.severity = error  # CreateEncryptor with default IV
dotnet_diagnostic.CA5403.severity = error  # Hardcoded certificate

# SonarAnalyzer security hotspots
dotnet_diagnostic.S2068.severity = error   # Credentials should not be hard-coded
dotnet_diagnostic.S2076.severity = error   # OS command injection
dotnet_diagnostic.S2077.severity = error   # SQL injection
dotnet_diagnostic.S2078.severity = error   # LDAP injection
dotnet_diagnostic.S5131.severity = error   # CSRF
dotnet_diagnostic.S5144.severity = error   # Server-side request forgery
dotnet_diagnostic.S5145.severity = error   # Log injection
dotnet_diagnostic.S5146.severity = error   # Open redirect
dotnet_diagnostic.S5332.severity = error   # Clear-text protocols
dotnet_diagnostic.S5334.severity = error   # EJB authorization
dotnet_diagnostic.S5344.severity = error   # Password hash without salt
dotnet_diagnostic.S5547.severity = error   # Cipher algorithms should be robust
dotnet_diagnostic.S5659.severity = error   # JWT should be signed and verified
dotnet_diagnostic.S5693.severity = error   # Insecure configuration
dotnet_diagnostic.S5867.severity = error   # Deserialization injection
```

#### Step 4: Aggiornare il Workflow

**File**: `.github/workflows/security-scan.yml`

```yaml
# ✅ NUOVO STEP (sostituisce linee 249-318)
- name: Build with Modern Security Analyzers
  id: security_build
  run: |
    # Build with all security analyzers enabled
    # Analyzers are now part of the project (Api.csproj)
    dotnet build --configuration Release /p:TreatWarningsAsErrors=true > security-scan.log 2>&1 || true

    # Display full log
    cat security-scan.log

    # Check for any security warnings (CA* = Code Analysis, S* = SonarAnalyzer)
    set +e  # Don't exit on grep failure
    SECURITY_WARNINGS=$(grep -iE "warning (CA[0-9]{4}|S[0-9]{4}):" security-scan.log || true)
    set -e

    if [ -n "$SECURITY_WARNINGS" ]; then
      echo "## ⚠️ Security Warnings Found" >> $GITHUB_STEP_SUMMARY
      echo "" >> $GITHUB_STEP_SUMMARY

      # Count warnings
      WARNING_COUNT=$(echo "$SECURITY_WARNINGS" | wc -l)
      echo "**Total Security Warnings:** $WARNING_COUNT" >> $GITHUB_STEP_SUMMARY
      echo "" >> $GITHUB_STEP_SUMMARY

      # Show warnings in summary
      echo "<details>" >> $GITHUB_STEP_SUMMARY
      echo "<summary>Security Warning Details</summary>" >> $GITHUB_STEP_SUMMARY
      echo "" >> $GITHUB_STEP_SUMMARY
      echo '```' >> $GITHUB_STEP_SUMMARY
      echo "$SECURITY_WARNINGS" >> $GITHUB_STEP_SUMMARY
      echo '```' >> $GITHUB_STEP_SUMMARY
      echo "</details>" >> $GITHUB_STEP_SUMMARY

      # Fail the build if any security warnings found
      echo "has_warnings=true" >> $GITHUB_OUTPUT
      exit 1
    else
      echo "✅ No security warnings found in .NET code" >> $GITHUB_STEP_SUMMARY
      echo "has_warnings=false" >> $GITHUB_OUTPUT
    fi

- name: Upload Security Scan Report
  if: always()
  uses: actions/upload-artifact@v6
  with:
    name: dotnet-security-scan-report
    path: apps/api/security-scan.log
    retention-days: 30
```

### Testing

1. **Build locale** per verificare che gli analyzer funzionino:
   ```bash
   cd apps/api/src/Api
   dotnet build --configuration Release
   ```

2. **Controlla warnings**: Dovrebbero apparire nuovi warning di sicurezza
   ```bash
   # Esempio output
   warning CA5350: Do not use weak cryptographic algorithms
   warning S2068: Credentials should not be hard-coded
   ```

3. **Fix i warning trovati** prima di committare

4. **Trigger workflow** su un draft PR per verificare che funzioni in CI

### Criteri di Accettazione

- [ ] SecurityCodeScan.VS2019 rimosso
- [ ] 3 analyzer moderni aggiunti al progetto (.csproj)
- [ ] .editorconfig configurato con regole di security come errors
- [ ] Workflow aggiornato per usare gli analyzer built-in
- [ ] Build locale passa senza security warnings
- [ ] CI workflow passa senza errori
- [ ] Documentazione aggiornata con lista dei nuovi analyzer

---

## 📝 Checklist Fase 1

### Pre-Implementation
- [ ] Backup del repository (già su GitHub)
- [ ] Branch creato: `feature/phase-1-security-fixes`
- [ ] Team notificato delle modifiche

### Implementation
- [ ] Issue #1: Fix credenziali k6 completato
- [ ] Issue #2: Permissions aggiunte completato
- [ ] Issue #3: Analyzer aggiornati completato

### Testing
- [ ] Tutti i workflow eseguiti manualmente con successo
- [ ] Nessun secret visibile nei logs
- [ ] Nessun errore di permission
- [ ] Build passa con i nuovi analyzer

### Documentation
- [ ] CLAUDE.md aggiornato con nuove configurazioni
- [ ] SECURITY.md aggiornato con analyzer info
- [ ] Changelog aggiornato

### Review & Merge
- [ ] PR creata con checklist completa
- [ ] Code review approvato
- [ ] CI passa tutti i check
- [ ] Merge su main

---

## 🔗 Riferimenti

- **OWASP Top 10**: https://owasp.org/www-project-top-ten/
- **GitHub Actions Security**: https://docs.github.com/en/actions/security-guides/security-hardening-for-github-actions
- **CWE-798**: https://cwe.mitre.org/data/definitions/798.html
- **.NET Security Rules**: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/security-warnings
- **SonarAnalyzer Rules**: https://rules.sonarsource.com/csharp/tag/security

---

**Creato**: 2025-11-19
**Ultimo aggiornamento**: 2025-11-19

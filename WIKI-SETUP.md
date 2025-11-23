# 📖 GitHub Wiki Setup Guide

**MeepleAI Wiki Publishing Guide**

Questa guida spiega come pubblicare e mantenere la wiki GitHub del progetto MeepleAI utilizzando i contenuti della directory `.wiki/`.

---

## 🎯 Panoramica

Il progetto mantiene i contenuti della wiki in `.wiki/` per versionarli insieme al codice. Quando sei pronto a pubblicare, usiamo uno script automatico per sincronizzare i contenuti con la wiki GitHub.

**Struttura:**
```
.wiki/
├── 00-home.md              → Home (pagina principale)
├── 01-user-guide.md        → User-Guide
├── 02-developer-guide.md   → Developer-Guide
├── 03-testing-guide.md     → Testing-Guide
├── 04-deployment-guide.md  → Deployment-Guide
├── 05-administrator-guide.md → Administrator-Guide
├── 06-architecture-guide.md → Architecture-Guide
├── 07-contributing-guide.md → Contributing-Guide
└── README.md               → _Sidebar (navigazione laterale)
```

---

## 🚀 Quick Start (Metodo Automatico)

### Prerequisiti

1. **Abilita la wiki su GitHub** (solo prima volta):
   ```
   Vai su: https://github.com/DegrassiAaron/meepleai-monorepo/settings
   Scorri a "Features" → Spunta "Wikis"
   ```

2. **Verifica di avere git configurato** con accesso al repository

### Pubblica la Wiki

```bash
# Dalla root del progetto
tools/publish-wiki.sh
```

**Output atteso:**
```
📖 MeepleAI Wiki Publisher

Found 9 markdown files in .wiki/

This will:
  1. Clone the GitHub wiki repository to: /home/user/meepleai-wiki-temp
  2. Copy and rename 9 files from .wiki/
  3. Commit and push to GitHub wiki

Continue? (y/N): y

✓ Home.md (from 00-home.md)
✓ User-Guide.md
✓ Developer-Guide.md
✓ Testing-Guide.md
✓ Deployment-Guide.md
✓ Administrator-Guide.md
✓ Architecture-Guide.md
✓ Contributing-Guide.md
✓ _Sidebar.md (from README.md)

✅ Wiki published successfully!

View your wiki at:
https://github.com/DegrassiAaron/meepleai-monorepo/wiki
```

### Verifica la Pubblicazione

Apri nel browser:
```
https://github.com/DegrassiAaron/meepleai-monorepo/wiki
```

Dovresti vedere:
- ✅ Home page con panoramica del progetto
- ✅ 7 guide per ruolo nella sidebar
- ✅ Navigazione funzionante tra le pagine

---

## 📝 Aggiornare i Contenuti

### 1. Modifica i File Locali

Modifica i file nella directory `.wiki/`:

```bash
# Esempio: aggiorna la guida per sviluppatori
vim .wiki/02-developer-guide.md

# Oppure usa il tuo editor preferito
code .wiki/02-developer-guide.md
```

### 2. Commit nel Repository Principale

```bash
# Stage delle modifiche
git add .wiki/

# Commit
git commit -m "docs(wiki): update developer guide with new setup steps"

# Push al repository principale
git push
```

### 3. Ripubblica la Wiki

```bash
# Esegui di nuovo lo script
tools/publish-wiki.sh
```

Lo script:
- ✅ Rileva automaticamente le modifiche
- ✅ Aggiorna solo i file cambiati
- ✅ Crea un commit con timestamp
- ✅ Pusha alla wiki GitHub

---

## 🔧 Metodo Manuale (Avanzato)

Se preferisci pubblicare manualmente senza lo script:

### 1. Clona il Repository Wiki

```bash
# In una directory temporanea
cd ~
git clone https://github.com/DegrassiAaron/meepleai-monorepo.wiki.git
cd meepleai-monorepo.wiki
```

### 2. Copia i File

```bash
# Imposta il percorso della directory .wiki
WIKI_SOURCE="/home/user/meepleai-monorepo/.wiki"

# Copia e rinomina i file
cp "$WIKI_SOURCE/00-home.md" Home.md
cp "$WIKI_SOURCE/01-user-guide.md" User-Guide.md
cp "$WIKI_SOURCE/02-developer-guide.md" Developer-Guide.md
cp "$WIKI_SOURCE/03-testing-guide.md" Testing-Guide.md
cp "$WIKI_SOURCE/04-deployment-guide.md" Deployment-Guide.md
cp "$WIKI_SOURCE/05-administrator-guide.md" Administrator-Guide.md
cp "$WIKI_SOURCE/06-architecture-guide.md" Architecture-Guide.md
cp "$WIKI_SOURCE/07-contributing-guide.md" Contributing-Guide.md
cp "$WIKI_SOURCE/README.md" _Sidebar.md
```

### 3. Commit e Push

```bash
git add .
git commit -m "Update wiki from .wiki/ directory - $(date +%Y-%m-%d)"
git push origin master
```

### 4. Cleanup

```bash
# Rimuovi la directory temporanea
cd ~
rm -rf meepleai-monorepo.wiki
```

---

## 📋 Convenzioni di Naming

GitHub Wiki ha convenzioni specifiche per i nomi dei file:

| File in .wiki/ | Nome nella Wiki GitHub | URL |
|----------------|----------------------|-----|
| `00-home.md` | `Home.md` | `.../wiki` (homepage) |
| `01-user-guide.md` | `User-Guide.md` | `.../wiki/User-Guide` |
| `02-developer-guide.md` | `Developer-Guide.md` | `.../wiki/Developer-Guide` |
| `03-testing-guide.md` | `Testing-Guide.md` | `.../wiki/Testing-Guide` |
| `04-deployment-guide.md` | `Deployment-Guide.md` | `.../wiki/Deployment-Guide` |
| `05-administrator-guide.md` | `Administrator-Guide.md` | `.../wiki/Administrator-Guide` |
| `06-architecture-guide.md` | `Architecture-Guide.md` | `.../wiki/Architecture-Guide` |
| `07-contributing-guide.md` | `Contributing-Guide.md` | `.../wiki/Contributing-Guide` |
| `README.md` | `_Sidebar.md` | Sidebar (navigazione) |

**Regole:**
- ✅ Usa `Home.md` per la homepage
- ✅ Usa `_Sidebar.md` per la navigazione laterale
- ✅ Usa trattini (`-`) per separare le parole negli URL
- ✅ Prima lettera maiuscola per ogni parola (PascalCase con trattini)
- ❌ Non usare prefissi numerici nella wiki (sono solo per ordinare `.wiki/`)

---

## 🎯 Contenuti delle Pagine Wiki

### Home Page
**File:** `00-home.md` → `Home.md`

Contiene:
- 🎲 Benvenuto e descrizione del progetto
- 🚀 Quick start per utenti e sviluppatori
- 🎯 Funzionalità principali
- 🏗️ Panoramica architettura
- 📊 Metriche chiave e obiettivi
- 🗺️ Fasi del progetto
- 🧩 Bounded contexts (DDD)

### User Guide
**File:** `01-user-guide.md` → `User-Guide.md`

Come usare MeepleAI:
- Registrazione e login
- Fare domande sulle regole
- Navigare la libreria di giochi
- Gestire sessioni di chat
- Impostazioni account

### Developer Guide
**File:** `02-developer-guide.md` → `Developer-Guide.md`

Workflow di sviluppo:
- Setup ambiente locale
- Struttura del codice
- Pattern CQRS/DDD
- Convenzioni di naming
- Branch strategy
- Pull request process

### Testing Guide
**File:** `03-testing-guide.md` → `Testing-Guide.md`

Procedure di testing:
- Strategia di testing (90%+ coverage)
- Unit tests (xUnit)
- Integration tests (Testcontainers)
- E2E tests (Playwright)
- Test dei componenti React
- Quality gates

### Deployment Guide
**File:** `04-deployment-guide.md` → `Deployment-Guide.md`

Deployment e operazioni:
- Ambienti (dev/staging/prod)
- CI/CD pipeline
- Docker Compose setup
- Monitoring e logging
- Troubleshooting

### Administrator Guide
**File:** `05-administrator-guide.md` → `Administrator-Guide.md`

Manutenzione del sistema:
- User management
- System configuration
- Backup e restore
- Performance tuning
- Security hardening

### Architecture Guide
**File:** `06-architecture-guide.md` → `Architecture-Guide.md`

Deep dive tecnico:
- Bounded contexts (7 contesti)
- CQRS/MediatR pattern
- RAG pipeline (hybrid search)
- PDF processing (3-stage)
- Autenticazione (Cookie/OAuth/2FA/API key)
- Observability stack

### Contributing Guide
**File:** `07-contributing-guide.md` → `Contributing-Guide.md`

Come contribuire:
- Code of conduct
- Development workflow
- Commit message conventions
- Pull request template
- Code review process
- Documentation updates

---

## 🔍 Troubleshooting

### ❌ Errore: "fatal: could not read Username"

**Causa:** Git non riesce ad accedere al repository wiki (autenticazione mancante)

**Soluzione:**
```bash
# Configura credenziali GitHub
git config --global user.name "Your Name"
git config --global user.email "your.email@example.com"

# Se usi SSH, verifica la chiave
ssh -T git@github.com

# Se usi HTTPS, potrebbe essere necessario un Personal Access Token
# Genera un token su: https://github.com/settings/tokens
```

### ❌ Errore: "repository not found"

**Causa:** La wiki non è abilitata su GitHub

**Soluzione:**
1. Vai su `https://github.com/DegrassiAaron/meepleai-monorepo/settings`
2. Scorri a "Features"
3. Abilita "Wikis"
4. Crea la prima pagina manualmente su GitHub (poi puoi usare lo script)

### ❌ Errore: "No changes detected"

**Causa:** I file nella wiki sono già aggiornati

**Soluzione:**
- ✅ Questo è normale! Nessuna azione necessaria
- Se hai modificato `.wiki/` ma non vedi cambiamenti, verifica di aver committato nel repo principale

### ⚠️ I link interni non funzionano

**Causa:** I link usano path relativi che funzionano in `.wiki/` ma non nella wiki GitHub

**Soluzione:**
Usa link in formato wiki GitHub:
```markdown
# ❌ Non funziona nella wiki
[Developer Guide](./02-developer-guide.md)

# ✅ Funziona nella wiki
[Developer Guide](Developer-Guide)
```

---

## 🔄 Workflow Completo

### Per Modifiche Rapide

```bash
# 1. Modifica il file
vim .wiki/02-developer-guide.md

# 2. Commit nel repo principale
git add .wiki/
git commit -m "docs(wiki): update developer guide"
git push

# 3. Pubblica alla wiki
tools/publish-wiki.sh
```

### Per Modifiche Multiple

```bash
# 1. Crea un branch
git checkout -b update-wiki-guides

# 2. Modifica più file
vim .wiki/02-developer-guide.md
vim .wiki/03-testing-guide.md
vim .wiki/06-architecture-guide.md

# 3. Commit
git add .wiki/
git commit -m "docs(wiki): major update to developer, testing, and architecture guides

- Developer guide: add Docker setup instructions
- Testing guide: update coverage requirements to 90%
- Architecture guide: document new bounded contexts"

# 4. Push e crea PR
git push -u origin update-wiki-guides
gh pr create --title "Update wiki guides" --body "Updates developer, testing, and architecture guides"

# 5. Dopo il merge, pubblica la wiki
git checkout main
git pull
tools/publish-wiki.sh
```

---

## 📚 Riferimenti

### Script di Pubblicazione
- **Percorso:** [`tools/publish-wiki.sh`](tools/publish-wiki.sh)
- **Funzionalità:**
  - Clone automatico del wiki repo
  - Copia e rinomina dei file
  - Commit e push automatici
  - Cleanup dei file temporanei
  - Prompt di conferma
  - Output colorato e user-friendly

### Directory dei Contenuti
- **Percorso:** [`.wiki/`](.wiki/)
- **File:** 9 markdown files (Home + 7 guide + Sidebar)
- **Versionamento:** Tracciato con Git nel repository principale

### Documentazione Correlata
- **Main Documentation Index:** [`docs/INDEX.md`](docs/INDEX.md)
- **Contributing Guide:** [`CONTRIBUTING.md`](CONTRIBUTING.md)
- **Project README:** [`README.md`](README.md)
- **Claude Instructions:** [`CLAUDE.md`](CLAUDE.md)

### Link Esterni
- **GitHub Wiki:** https://github.com/DegrassiAaron/meepleai-monorepo/wiki
- **GitHub Wiki Help:** https://docs.github.com/en/communities/documenting-your-project-with-wikis
- **Markdown Guide:** https://www.markdownguide.org/

---

## 💡 Best Practices

### ✅ DO

1. **Version Control**
   - Mantieni sempre `.wiki/` nel repository principale
   - Commit le modifiche con messaggi descrittivi
   - Usa branch per modifiche significative

2. **Consistency**
   - Segui le convenzioni di naming
   - Mantieni la struttura esistente
   - Cross-reference tra le pagine

3. **Testing**
   - Testa i link interni prima di pubblicare
   - Verifica la formattazione markdown
   - Controlla che le immagini siano accessibili

4. **Updates**
   - Pubblica regolarmente le modifiche
   - Aggiorna il timestamp nelle pagine
   - Mantieni sincronizzati wiki e docs/

### ❌ DON'T

1. **Non modificare direttamente la wiki GitHub**
   - Usa sempre `.wiki/` come source of truth
   - Le modifiche dirette sulla wiki verranno sovrascritte

2. **Non usare path assoluti**
   - Usa link relativi o link wiki
   - Evita riferimenti a localhost o IP

3. **Non includere contenuti sensibili**
   - Nessuna password o API key
   - Nessun dato di produzione
   - Nessuna informazione confidenziale

4. **Non duplicare contenuti**
   - Cross-reference ai docs/ quando appropriato
   - Mantieni DRY (Don't Repeat Yourself)

---

## 📞 Support

### Problemi con la Wiki?

1. **Check lo stato della wiki:**
   ```bash
   # Verifica che la wiki sia accessibile
   curl -I https://github.com/DegrassiAaron/meepleai-monorepo/wiki
   ```

2. **Controlla i permessi:**
   ```bash
   # Verifica di poter scrivere sul repo wiki
   git clone https://github.com/DegrassiAaron/meepleai-monorepo.wiki.git /tmp/test-wiki
   ```

3. **Review dei log:**
   ```bash
   # L'output dello script mostra eventuali errori
   tools/publish-wiki.sh 2>&1 | tee wiki-publish.log
   ```

### Canali di Supporto

- **GitHub Issues:** https://github.com/DegrassiAaron/meepleai-monorepo/issues
- **GitHub Discussions:** https://github.com/DegrassiAaron/meepleai-monorepo/discussions
- **Documentation:** [`docs/INDEX.md`](docs/INDEX.md)

---

**Last Updated:** 2025-11-23
**Maintainer:** Engineering Team
**Version:** 1.0

#!/bin/bash
# Script per creare label GitHub per MeepleAI
# Uso: ./create-labels.sh OWNER/REPO
# Esempio: ./create-labels.sh username/meepleai

REPO=$1

if [ -z "$REPO" ]; then
  echo "Uso: ./create-labels.sh OWNER/REPO"
  exit 1
fi

echo "Creazione label per repository: $REPO"

# Priorità
gh label create "priority:critical" --repo $REPO --color "d73a4a" --description "P0: Blocca utenti, security issue" --force
gh label create "priority:high" --repo $REPO --color "ff6b6b" --description "P1: Feature chiave, bug importante" --force
gh label create "priority:medium" --repo $REPO --color "fbca04" --description "P2: Miglioramenti significativi" --force
gh label create "priority:low" --repo $REPO --color "0e8a16" --description "P3: Nice-to-have, ottimizzazioni minori" --force

# Tipo
gh label create "type:bug" --repo $REPO --color "d73a4a" --description "Bug da fixare" --force
gh label create "type:feature" --repo $REPO --color "a2eeef" --description "Nuova funzionalità" --force
gh label create "type:enhancement" --repo $REPO --color "84b6eb" --description "Miglioramento esistente" --force
gh label create "type:refactor" --repo $REPO --color "fbca04" --description "Refactoring tecnico" --force
gh label create "type:docs" --repo $REPO --color "0075ca" --description "Documentazione" --force
gh label create "type:security" --repo $REPO --color "d73a4a" --description "Security issue" --force
gh label create "type:performance" --repo $REPO --color "c2e0c6" --description "Ottimizzazione performance" --force

# Epic
gh label create "epic:pdf-management" --repo $REPO --color "5319e7" --description "EPIC-01: Gestione PDF" --force
gh label create "epic:rag-search" --repo $REPO --color "5319e7" --description "EPIC-02: RAG e ricerca semantica" --force
gh label create "epic:chat-ui" --repo $REPO --color "5319e7" --description "EPIC-03: Chat interface" --force
gh label create "epic:editor" --repo $REPO --color "5319e7" --description "EPIC-04: Editor e versioning" --force
gh label create "epic:admin" --repo $REPO --color "5319e7" --description "EPIC-05: Admin e monitoring" --force
gh label create "epic:workflow" --repo $REPO --color "5319e7" --description "EPIC-06: Workflow automation" --force
gh label create "epic:auth" --repo $REPO --color "5319e7" --description "EPIC-07: Auth e security" --force
gh label create "epic:testing" --repo $REPO --color "5319e7" --description "EPIC-08: Testing e QA" --force

# Area Tecnica
gh label create "area:frontend" --repo $REPO --color "bfdadc" --description "Next.js/React" --force
gh label create "area:backend" --repo $REPO --color "c5def5" --description "ASP.NET Core" --force
gh label create "area:database" --repo $REPO --color "d4c5f9" --description "PostgreSQL/Qdrant/Redis" --force
gh label create "area:ai" --repo $REPO --color "f9d0c4" --description "RAG/Embeddings/LLM" --force
gh label create "area:infra" --repo $REPO --color "fef2c0" --description "Docker/CI/CD" --force
gh label create "area:ux" --repo $REPO --color "ffc0cb" --description "User experience" --force

# Status
gh label create "status:needs-triage" --repo $REPO --color "ededed" --description "Da valutare" --force
gh label create "status:needs-discussion" --repo $REPO --color "d4c5f9" --description "Richiede discussione" --force
gh label create "status:needs-estimation" --repo $REPO --color "fbca04" --description "Da stimare" --force
gh label create "status:ready" --repo $REPO --color "0e8a16" --description "Pronta per sviluppo" --force
gh label create "status:in-progress" --repo $REPO --color "1d76db" --description "In lavorazione" --force
gh label create "status:blocked" --repo $REPO --color "b60205" --description "Bloccata" --force
gh label create "status:needs-review" --repo $REPO --color "fbca04" --description "In review" --force

# Effort
gh label create "effort:xs" --repo $REPO --color "c2e0c6" --description "1-2 ore" --force
gh label create "effort:s" --repo $REPO --color "bfd4f2" --description "1-2 giorni" --force
gh label create "effort:m" --repo $REPO --color "d4c5f9" --description "3-5 giorni" --force
gh label create "effort:l" --repo $REPO --color "f9d0c4" --description "1-2 settimane" --force
gh label create "effort:xl" --repo $REPO --color "ff6b6b" --description ">2 settimane" --force

echo "✅ Label create con successo!"
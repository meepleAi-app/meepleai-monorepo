# Manual Test Checklist: Add Game → Ownership → RAG Chat

**User Story**: Un utente aggiunge un gioco dal catalogo condiviso alla propria collezione, dichiara di possederlo, configura un agente tutor, e avvia una chat RAG con citazioni dal manuale.

**Data**: 2026-03-17

---

## Prerequisites

- [ ] `make dev` running from `infra/` (all services healthy)
- [ ] Seed script eseguito: `bash infra/scripts/seed-test-game.sh <path-to-catan-rules.pdf>`
- [ ] Annotare Game ID e PDF status dall'output del seed
- [ ] OpenRouter API key configurata in `infra/secrets/dev/openrouter.secret`

---

## Phase A: Login come Test User

| # | Azione | Verifica | Pass? |
|---|--------|----------|-------|
| A1 | Apri http://localhost:3000 | Pagina login/home carica | [ ] |
| A2 | Login: `testuser@meepleai.dev` / `TestUser123!` | Dashboard visibile | [ ] |

---

## Phase B: Discover & Add Game

| # | Azione | Verifica | Pass? |
|---|--------|----------|-------|
| B1 | Naviga a `/discover` | Pagina catalogo carica | [ ] |
| B2 | Cerca "Coloni di Catan" | Card del gioco appare nei risultati | [ ] |
| B3 | Clicca sulla card del gioco | Pagina dettaglio `/discover/{gameId}` carica | [ ] |
| B4 | Verifica metadata | Immagine, giocatori (3-4), tempo (75 min), complessita | [ ] |
| B5 | Clicca "Aggiungi alla Libreria" | Redirect a `/library/games/{gameId}` | [ ] |
| B6 | Verifica stato | Stato mostra "Nuovo" | [ ] |

---

## Phase C: Declare Ownership

| # | Azione | Verifica | Pass? |
|---|--------|----------|-------|
| C1 | Verifica bottone | "Dichiara Possesso" visibile (giallo/amber) | [ ] |
| C2 | Clicca "Dichiara Possesso" | Dialog popup appare | [ ] |
| C3 | Verifica contenuto dialog | Titolo + lista benefici (Tutor AI, Sessioni, Prestito) | [ ] |
| C4 | Verifica checkbox | Checkbox conferma presente e deselezionata | [ ] |
| C5 | Verifica bottone conferma | "Conferma" disabilitato | [ ] |
| C6 | Spunta checkbox | "Conferma" diventa abilitato | [ ] |
| C7 | Clicca "Conferma" | Dialog di conferma successo appare | [ ] |
| C8 | Chiudi dialog conferma | Stato cambia a "Owned" | [ ] |
| C9 | Verifica bottone sparito | "Dichiara Possesso" non piu visibile | [ ] |

---

## Phase D: Agent Tab

| # | Azione | Verifica | Pass? |
|---|--------|----------|-------|
| D1 | Clicca tab "Agent" | Pagina `/library/games/{gameId}/agent` carica | [ ] |
| D2 | Verifica KB status | KbStatusPanel mostra almeno 1 documento "Ready" | [ ] |
| D3 | Verifica form agente | Dropdown tipologia visibile (tutor/stratega/narratore/arbitro) | [ ] |

---

## Phase E: Create Agent & Start Chat

| # | Azione | Verifica | Pass? |
|---|--------|----------|-------|
| E1 | Seleziona tipologia "Tutor" | Dropdown aggiornato | [ ] |
| E2 | Clicca "Avvia Chat" / "Crea Agente" | Loading spinner appare | [ ] |
| E3 | Attendi creazione | Redirect a `/chat/{threadId}` | [ ] |
| E4 | Verifica chat | Interfaccia chat vuota con input visibile | [ ] |

---

## Phase F: RAG Chat

| # | Azione | Verifica | Pass? |
|---|--------|----------|-------|
| F1 | Scrivi: "Quante risorse riceve ogni giocatore all'inizio?" | Messaggio inviato | [ ] |
| F2 | Attendi risposta (5-30 sec) | Loading indicator visibile | [ ] |
| F3 | Verifica risposta | Risposta pertinente sulle risorse iniziali di Catan | [ ] |
| F4 | Verifica citazioni | Badge/link a sezioni del PDF (se UI le mostra) | [ ] |
| F5 | (Opzionale) "Come funziona il commercio?" | Seconda risposta coerente con regolamento | [ ] |

---

## Risultato

- [ ] **PASS**: Tutte le fasi A-F completate con successo
- [ ] **FAIL**: Step fallito: _____ | Errore: _____

---

## Troubleshooting

| Problema | Diagnosi |
|----------|----------|
| Gioco non trovato in /discover | `curl http://localhost:8080/api/v1/shared-games?searchTerm=Catan` |
| PDF non Completed | `curl http://localhost:8080/api/v1/pdfs/{pdfId}/progress -b cookies.txt` (check `currentStep`=`Completed`) |
| Nessuna risposta RAG | `pwsh -c "docker logs meepleai-embedding-service --tail=20"` |
| Chat timeout | Verificare OpenRouter key: `cat infra/secrets/dev/openrouter.secret` |
| Agent creation fails | Possibile limite slot agente — verificare tier utente |
| Bottone ownership mancante | Gioco potrebbe essere gia in stato "Owned" |
| Features.PdfUpload disabled | Verificare: `SELECT * FROM system_configurations WHERE key='Features.PdfUpload'` |

# US: Add Game → Declare Ownership → RAG Chat — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Seed a test game with indexed PDF in the dev environment, then manually validate the full user story in the browser: discover game → add to library → declare ownership → create tutor agent → RAG chat with citations.

**Architecture:** Admin API seeds a SharedGame (Published, IsRagPublic=false) + uploads a real PDF rulebook. Processing pipeline (extract → chunk → embed → index) runs automatically. A normal test user then performs the full flow in the browser.

**Tech Stack:** .NET 9 API, Next.js 16 frontend, PostgreSQL, Qdrant, embedding-service, OpenRouter LLM

---

## Chunk 1: Seed Script + Test Checklist

### Task 1: Create seed script

**Files:**
- Create: `infra/scripts/seed-test-game.sh`

**Prerequisites:**
- `make dev` running (all services healthy)
- `infra/secrets/dev/admin.secret` exists (contains ADMIN_PASSWORD)
- A PDF rulebook file available locally (e.g., downloaded Catan rules)

- [ ] **Step 1: Create the seed script**

```bash
#!/usr/bin/env bash
# seed-test-game.sh — Seeds a test game + PDF for US validation
# Usage: ./seed-test-game.sh <path-to-pdf>
#
# Prerequisites:
#   - make dev running (all services healthy)
#   - Admin password in infra/secrets/dev/admin.secret

set -euo pipefail

API_BASE="http://localhost:8080/api/v1"
COOKIE_JAR="/tmp/meepleai-seed-cookies.txt"
PDF_FILE="${1:?Usage: $0 <path-to-pdf-file>}"

if [ ! -f "$PDF_FILE" ]; then
  echo "ERROR: PDF file not found: $PDF_FILE"
  exit 1
fi

# Read admin password from secrets
ADMIN_SECRET_FILE="$(dirname "$0")/../secrets/dev/admin.secret"
if [ ! -f "$ADMIN_SECRET_FILE" ]; then
  echo "ERROR: Admin secret not found at $ADMIN_SECRET_FILE"
  echo "Run: cd infra && make secrets-dev"
  exit 1
fi
ADMIN_PASSWORD=$(grep -oP 'ADMIN_PASSWORD=\K.*' "$ADMIN_SECRET_FILE" || true)
if [ -z "$ADMIN_PASSWORD" ]; then
  echo "ERROR: ADMIN_PASSWORD not found in $ADMIN_SECRET_FILE"
  exit 1
fi

ADMIN_EMAIL="admin@meepleai.dev"

echo "=== Step 1: Admin Login ==="
LOGIN_RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$API_BASE/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}" \
  -c "$COOKIE_JAR")
HTTP_CODE=$(echo "$LOGIN_RESPONSE" | tail -1)
BODY=$(echo "$LOGIN_RESPONSE" | sed '$d')

if [ "$HTTP_CODE" != "200" ]; then
  echo "ERROR: Login failed (HTTP $HTTP_CODE): $BODY"
  exit 1
fi
echo "Admin login OK"

echo ""
echo "=== Step 2: Create SharedGame ==="
CREATE_RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$API_BASE/admin/shared-games" \
  -H "Content-Type: application/json" \
  -b "$COOKIE_JAR" \
  -d '{
    "title": "I Coloni di Catan",
    "yearPublished": 1995,
    "description": "Costruisci insediamenti, commercia risorse e diventa il dominatore dell isola di Catan. Un classico gioco di strategia per 3-4 giocatori.",
    "minPlayers": 3,
    "maxPlayers": 4,
    "playingTimeMinutes": 75,
    "minAge": 10,
    "complexityRating": 2.3,
    "averageRating": 7.2,
    "imageUrl": "https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__original/img/IEhEYdJsTGlAmFp-KSOAZX5ypPU=/0x0/filters:format(jpeg)/pic2419375.jpg",
    "designers": ["Klaus Teuber"],
    "publishers": ["Kosmos", "Giochi Uniti"],
    "categories": ["Strategy", "Negotiation"],
    "mechanics": ["Dice Rolling", "Trading", "Route Building"],
    "bggId": 13
  }')
HTTP_CODE=$(echo "$CREATE_RESPONSE" | tail -1)
GAME_ID=$(echo "$CREATE_RESPONSE" | sed '$d' | tr -d '"')

if [ "$HTTP_CODE" != "201" ]; then
  echo "ERROR: Create game failed (HTTP $HTTP_CODE): $GAME_ID"
  exit 1
fi
echo "Game created: $GAME_ID"

echo ""
echo "=== Step 3: Quick-Publish Game ==="
PUBLISH_RESPONSE=$(curl -s -w "\n%{http_code}" -X POST \
  "$API_BASE/admin/shared-games/$GAME_ID/quick-publish" \
  -b "$COOKIE_JAR")
HTTP_CODE=$(echo "$PUBLISH_RESPONSE" | tail -1)

if [ "$HTTP_CODE" != "204" ]; then
  BODY=$(echo "$PUBLISH_RESPONSE" | sed '$d')
  echo "ERROR: Quick-publish failed (HTTP $HTTP_CODE): $BODY"
  exit 1
fi
echo "Game published OK"

echo ""
echo "=== Step 4: Upload PDF ==="
UPLOAD_RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$API_BASE/ingest/pdf" \
  -b "$COOKIE_JAR" \
  -F "file=@$PDF_FILE" \
  -F "gameId=$GAME_ID")
HTTP_CODE=$(echo "$UPLOAD_RESPONSE" | tail -1)
BODY=$(echo "$UPLOAD_RESPONSE" | sed '$d')

if [ "$HTTP_CODE" != "200" ] && [ "$HTTP_CODE" != "201" ]; then
  echo "ERROR: PDF upload failed (HTTP $HTTP_CODE): $BODY"
  exit 1
fi

# Extract pdfId from response
PDF_ID=$(echo "$BODY" | grep -oP '"documentId"\s*:\s*"\K[^"]+' || echo "$BODY" | grep -oP '"id"\s*:\s*"\K[^"]+' || echo "")
if [ -z "$PDF_ID" ]; then
  echo "WARNING: Could not extract PDF ID from response. Full response:"
  echo "$BODY"
  echo ""
  echo "Game ID: $GAME_ID"
  echo "You may need to check processing status manually."
  exit 0
fi
echo "PDF uploaded: $PDF_ID"

echo ""
echo "=== Step 5: Poll Processing Status ==="
MAX_WAIT=300  # 5 minutes
ELAPSED=0
INTERVAL=10

while [ $ELAPSED -lt $MAX_WAIT ]; do
  STATUS_RESPONSE=$(curl -s -X GET "$API_BASE/pdfs/$PDF_ID/progress" \
    -b "$COOKIE_JAR")
  CURRENT_STATE=$(echo "$STATUS_RESPONSE" | grep -oP '"currentState"\s*:\s*"\K[^"]+' || echo "unknown")
  PROGRESS=$(echo "$STATUS_RESPONSE" | grep -oP '"overallProgress"\s*:\s*\K[0-9.]+' || echo "0")

  echo "  [$ELAPSED s] State: $CURRENT_STATE | Progress: ${PROGRESS}%"

  if [ "$CURRENT_STATE" = "Ready" ]; then
    echo ""
    echo "=== PDF Processing Complete! ==="
    break
  fi

  if [ "$CURRENT_STATE" = "Failed" ]; then
    echo "ERROR: PDF processing failed!"
    echo "$STATUS_RESPONSE"
    exit 1
  fi

  sleep $INTERVAL
  ELAPSED=$((ELAPSED + INTERVAL))
done

if [ $ELAPSED -ge $MAX_WAIT ]; then
  echo "WARNING: Timeout waiting for PDF processing. Check status manually."
fi

echo ""
echo "=== Step 6: Register Test User ==="
REG_RESPONSE=$(curl -s -w "\n%{http_code}" -X POST "$API_BASE/auth/register" \
  -H "Content-Type: application/json" \
  -d '{"email":"testuser@meepleai.dev","password":"TestUser123!"}')
HTTP_CODE=$(echo "$REG_RESPONSE" | tail -1)
BODY=$(echo "$REG_RESPONSE" | sed '$d')

if [ "$HTTP_CODE" = "200" ] || [ "$HTTP_CODE" = "201" ]; then
  echo "Test user registered: testuser@meepleai.dev / TestUser123!"
elif echo "$BODY" | grep -q "already exists\|duplicate\|conflict"; then
  echo "Test user already exists (OK): testuser@meepleai.dev / TestUser123!"
else
  echo "WARNING: User registration returned HTTP $HTTP_CODE: $BODY"
  echo "You may need to create a user manually."
fi

echo ""
echo "========================================="
echo "  SEED COMPLETE"
echo "========================================="
echo ""
echo "  Game ID:    $GAME_ID"
echo "  Game Title: I Coloni di Catan"
echo "  PDF ID:     $PDF_ID"
echo "  PDF Status: $CURRENT_STATE"
echo ""
echo "  Test User:  testuser@meepleai.dev"
echo "  Password:   TestUser123!"
echo ""
echo "  Next: Open http://localhost:3000 and follow the manual test checklist"
echo "========================================="

# Cleanup
rm -f "$COOKIE_JAR"
```

- [ ] **Step 2: Make script executable**

```bash
chmod +x infra/scripts/seed-test-game.sh
```

- [ ] **Step 3: Commit seed script**

```bash
git add infra/scripts/seed-test-game.sh
git commit -m "chore(infra): add seed script for US add-game-ownership-rag-chat test"
```

---

### Task 2: Obtain a test PDF

**No file changes — manual step.**

- [ ] **Step 1: Download a freely available board game rulebook**

Options (pick one):
- **Catan**: Download from https://www.catan.com/understand-catan/game-rules (official, free)
- **Ticket to Ride**: Rules PDF from Days of Wonder
- **Any board game**: Check BGG for publicly available rulebook PDFs

Save as e.g. `catan-rules.pdf` in a convenient location.

---

### Task 3: Create manual browser test checklist

**Files:**
- Create: `docs/testing/us-add-game-ownership-rag-chat-checklist.md`

- [ ] **Step 1: Write the checklist**

```markdown
# Manual Test Checklist: Add Game → Ownership → RAG Chat

## Prerequisites
- [ ] `make dev` running from `infra/` (all services healthy)
- [ ] Seed script executed: `./infra/scripts/seed-test-game.sh <path-to-catan-rules.pdf>`
- [ ] Note the Game ID and PDF status from seed output
- [ ] OpenRouter API key configured in `infra/secrets/dev/openrouter.secret`

## Test Flow

### Phase A: Login as Test User
- [ ] Open http://localhost:3000
- [ ] Login with: `testuser@meepleai.dev` / `TestUser123!`
- [ ] Verify: Dashboard loads successfully

### Phase B: Discover & Add Game
- [ ] Navigate to `/discover`
- [ ] Search for "Coloni di Catan" in the search bar
- [ ] Verify: Game card appears in results with correct image and metadata
- [ ] Click on the game card
- [ ] Verify: Game detail page loads at `/discover/{gameId}`
- [ ] Verify: "Aggiungi alla Libreria" button is visible
- [ ] Click "Aggiungi alla Libreria"
- [ ] Verify: Redirected to `/library/games/{gameId}`
- [ ] Verify: Game state shows "Nuovo"

### Phase C: Declare Ownership
- [ ] Verify: "Dichiara Possesso" button is visible (yellow/amber)
- [ ] Click "Dichiara Possesso"
- [ ] Verify: Dialog appears with:
  - [ ] Title about ownership declaration
  - [ ] List of benefits (Tutor AI, Sessioni, Prestito or similar)
  - [ ] Confirmation checkbox (unchecked)
  - [ ] "Conferma" button (disabled until checkbox checked)
- [ ] Check the confirmation checkbox
- [ ] Verify: "Conferma" button becomes enabled
- [ ] Click "Conferma"
- [ ] Verify: Confirmation dialog appears (success)
- [ ] Close confirmation dialog
- [ ] Verify: Game state now shows "Owned"
- [ ] Verify: "Dichiara Possesso" button is no longer visible

### Phase D: Navigate to Agent Tab
- [ ] Click on "Agent" tab (or navigate to `/library/games/{gameId}/agent`)
- [ ] Verify: KbStatusPanel shows at least 1 document with status "Ready"
- [ ] Verify: Agent configuration form is visible
  - [ ] Typology dropdown (tutor/stratega/narratore/arbitro)
  - [ ] Strategy selection (if visible)

### Phase E: Create Agent & Start Chat
- [ ] Select typology: "Tutor" (or equivalent)
- [ ] Click "Avvia Chat" / "Crea Agente" button
- [ ] Verify: Loading/creation spinner appears
- [ ] Verify: Redirected to `/chat/{threadId}`
- [ ] Verify: Chat interface loads with empty message list

### Phase F: RAG Chat Test
- [ ] Type in chat input: "Quante risorse riceve ogni giocatore all'inizio della partita?"
- [ ] Press Enter / click Send
- [ ] Verify: Loading indicator appears (may take 5-30 seconds)
- [ ] Verify: Assistant response appears with:
  - [ ] Relevant answer about Catan initial resources
  - [ ] Citations/references from the PDF (if UI shows them)
  - [ ] Response is coherent and game-specific (not generic)
- [ ] (Optional) Ask a follow-up: "Come funziona il commercio tra giocatori?"
- [ ] Verify: Second response also references the game rules

## Edge Cases to Note (optional)
- [ ] Try asking a question about a DIFFERENT game → Should not return Catan-specific info
- [ ] Check that the game appears in `/library` tab "Collection" with state "Owned"

## Result
- [ ] **PASS**: All phases A-F completed successfully with RAG citations
- [ ] **FAIL**: Note which step failed and the error/behavior observed

## Troubleshooting

| Issue | Check |
|-------|-------|
| Game not found in /discover | Verify game Status=Published: check API `/api/v1/shared-games?searchTerm=Catan` |
| PDF not Ready | Poll: `curl http://localhost:8080/api/v1/pdfs/{pdfId}/progress -b cookies.txt` |
| No RAG response | Check embedding-service logs: `docker logs meepleai-embedding-service --tail=20` |
| Chat timeout | Check OpenRouter key: `cat infra/secrets/dev/openrouter.secret` |
| Agent creation fails | Check agent slots: may need tier upgrade in DB |
| Ownership button missing | Game may already be in "Owned" state |
```

- [ ] **Step 2: Commit checklist**

```bash
git add docs/testing/us-add-game-ownership-rag-chat-checklist.md
git commit -m "docs(testing): add manual test checklist for add-game-ownership-rag-chat US"
```

---

## Chunk 2: Execute Manual Test

### Task 4: Start environment

- [ ] **Step 1: Start Docker services**

```bash
cd infra && make dev
```

- [ ] **Step 2: Verify all services are healthy**

```bash
# Check API
curl -s http://localhost:8080/api/v1/health | head -5

# Check frontend
curl -s -o /dev/null -w "%{http_code}" http://localhost:3000

# Check Qdrant
curl -s http://localhost:6333/collections | head -5

# Check embedding-service
curl -s http://localhost:8000/health | head -5
```

Expected: All return 200/healthy.

---

### Task 5: Run seed script

- [ ] **Step 1: Execute seed script with PDF**

```bash
cd infra && bash scripts/seed-test-game.sh /path/to/catan-rules.pdf
```

- [ ] **Step 2: Verify output**

Expected output:
```
=== SEED COMPLETE ===
Game ID:    <uuid>
Game Title: I Coloni di Catan
PDF ID:     <uuid>
PDF Status: Ready
Test User:  testuser@meepleai.dev
Password:   TestUser123!
```

If PDF status is not "Ready", wait and re-check:
```bash
curl -s http://localhost:8080/api/v1/pdfs/<pdf-id>/progress -b /tmp/meepleai-seed-cookies.txt
```

---

### Task 6: Execute manual browser test

- [ ] **Step 1: Open browser at http://localhost:3000**
- [ ] **Step 2: Follow checklist from Task 3 step by step**
- [ ] **Step 3: Record result (PASS/FAIL) with notes**

---

## API Reference (Quick)

| Step | Method | Endpoint | Auth |
|------|--------|----------|------|
| Admin Login | POST | `/api/v1/auth/login` | None |
| Create Game | POST | `/api/v1/admin/shared-games` | Admin session |
| Quick-Publish | POST | `/api/v1/admin/shared-games/{id}/quick-publish` | Admin session |
| Upload PDF | POST | `/api/v1/ingest/pdf` | Session + Features.PdfUpload |
| Poll Progress | GET | `/api/v1/pdfs/{pdfId}/progress` | Session |
| Register User | POST | `/api/v1/auth/register` | None |
| Add to Library | POST | `/api/v1/library/games/{gameId}` | User session |
| Declare Ownership | POST | `/api/v1/library/{gameId}/declare-ownership` | User session |
| Create Agent+Setup | POST | `/api/v1/agents/create-with-setup` | User session |
| Chat | POST | `/api/v1/chat/sessions` | User session |
| Ask RAG Question | POST | `/api/v1/agents/{agentId}/ask` | User session |

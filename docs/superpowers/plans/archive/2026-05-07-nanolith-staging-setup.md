# Nanolith Staging E2E Setup Implementation Plan

**Status**: ✅ COMPLETED (PR #839 + #863 + #866 — seed-nanolith automation + fixes)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Configure Nanolith on staging so badsworm@gmail.com can run E2E smoke tests anytime: 2 indexed PDFs, 2 active agents (Tutor + Arbitro), working chat with citations.

**Architecture:** Hot-fix S3 retrieve key bug via R2 object copy (Phase 1-5), then proper PR-F fix in code (Phase 6). Agent creation via API endpoints. UI reuses v2 `done` components only (freeze #808).

**Tech Stack:** .NET 9 API + PostgreSQL + R2 (S3 compat) + EF Core, Next.js 16 frontend, SSH tunnel to staging server `204.168.135.69`.

**Spec ref:** `docs/superpowers/specs/2026-05-07-nanolith-staging-setup-design.md`

---

## Pre-flight constants

Save these as shell vars on staging side or copy-paste literally:

```
GAME_ID=6db3e01e-0b21-414c-8e3b-a899782feb40
SHARED_GAME_ID=94e99e38-1a5a-499c-89a9-2ea66173f63e
PDF_PRESS_START_ID=808b83ec-8e93-430e-95a8-c4918b2dab03
PDF_RULES_ID=b680bc20-ef11-4a73-acd0-36f919e2533b
USER_ID=c8ff6a6b-c764-4f63-9ea8-f3c0705225c1
EMAIL=badsworm@gmail.com
PASSWORD=TestNanolith2026!
```

API base URL (via SSH from local): `http://localhost:8080` (from staging server, bypasses CF Access). External: `https://meepleai.app`.

---

## Task 1: Login + verify session works

**Files:**
- No code changes
- Verification only

- [ ] **Step 1.1: Login via API and persist cookies**

Run:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'curl -s -i -c /tmp/cookies.txt -X POST http://localhost:8080/api/v1/auth/login \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"badsworm@gmail.com\",\"password\":\"TestNanolith2026!\"}" \
    | head -10'
```

Expected output contains:
```
HTTP/1.1 200 OK
Set-Cookie: meepleai_session=...
Set-Cookie: meepleai_user_role=superadmin; ...
```

If status != 200, password may be wrong. Fix:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 << 'EOF'
HASH=$(python3 -c '
import hashlib, os, base64
salt = os.urandom(16)
hash_bytes = hashlib.pbkdf2_hmac("sha256", b"TestNanolith2026!", salt, 210000, dklen=32)
print(f"v1.210000.{base64.b64encode(salt).decode()}.{base64.b64encode(hash_bytes).decode()}")
')
docker exec -i meepleai-postgres psql -U meepleai -d meepleai_staging << SQL
UPDATE users SET "PasswordHash" = '$HASH' WHERE "Email" = 'badsworm@gmail.com';
SQL
EOF
```

- [ ] **Step 1.2: Verify session is active**

Run:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'curl -s -b /tmp/cookies.txt http://localhost:8080/api/v1/auth/me'
```

Expected: JSON body with `"role":"superadmin"`, `"email":"badsworm@gmail.com"`.

---

## Task 2: Re-index Rules.pdf via S3 hot-fix workaround

**Files:**
- No code changes (Phase 1 is workaround only)

**⚠️ Sequencing note vs T7**:
The S3 copy created in this task lives at `pdf_uploads/{pdfId}/{pdfId}_*` — exactly where the **buggy** retrieve code (`RetrieveAsync(fileId, fileId)`) looks. It works precisely because it patches the runtime symptom of the bug while the bug is still active.

After T7 merges and staging redeploys with the fix, retrieve will use `(fileId, gameId)` and look at `pdf_uploads/{realGameId}/{pdfId}_*` (the original save path). The T2 workaround S3 object becomes inert at that point — orphaned but harmless. Rules.pdf indexing will continue to work because the original save path is preserved and the new code finds it correctly.

**Order recommendation**: complete T2 → T6 entirely (smoke validated), then run T7 in a follow-up session. If T7 lands while T2 is in flight, re-trigger retry after redeploy (Step 2.3 + 2.4).

- [ ] **Step 2.1: Verify Rules.pdf is currently stuck**

Run:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -tAc \
    "SELECT processing_state, \"ProcessingError\" FROM pdf_documents WHERE \"Id\" = '\''b680bc20-ef11-4a73-acd0-36f919e2533b'\'';"'
```

Expected: `Failed|PDF file not found in blob storage or filesystem: pdf_uploads/...`

If state is `Ready`, skip to Task 3.

- [ ] **Step 2.2: Verify S3 has both copies of Rules.pdf**

Run:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 << 'EOF'
sudo cp /opt/meepleai/secrets/storage.secret /tmp/s.env && sudo chmod 644 /tmp/s.env
source /tmp/s.env
sudo docker run --rm \
  -e AWS_ACCESS_KEY_ID="$S3_ACCESS_KEY" \
  -e AWS_SECRET_ACCESS_KEY="$S3_SECRET_KEY" \
  -e AWS_DEFAULT_REGION=auto \
  amazon/aws-cli:latest \
  --endpoint-url "$S3_ENDPOINT" \
  s3 ls "s3://$S3_BUCKET_NAME/pdf_uploads/b680bc20ef114a73acd036f919e2533b/"
sudo rm -f /tmp/s.env 2>/dev/null || true
EOF
```

Expected: 1 line listing `b680bc20ef114a73acd036f919e2533b_Nanolith Rules ENG.pdf`.

If 0 lines, the workaround copy is missing. Re-run S3 copy:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 << 'EOF'
sudo cp /opt/meepleai/secrets/storage.secret /tmp/s.env && sudo chmod 644 /tmp/s.env
source /tmp/s.env
sudo docker run --rm \
  -e AWS_ACCESS_KEY_ID="$S3_ACCESS_KEY" \
  -e AWS_SECRET_ACCESS_KEY="$S3_SECRET_KEY" \
  -e AWS_DEFAULT_REGION=auto \
  amazon/aws-cli:latest \
  --endpoint-url "$S3_ENDPOINT" \
  s3 cp \
  "s3://$S3_BUCKET_NAME/pdf_uploads/6db3e01e-0b21-414c-8e3b-a899782feb40/b680bc20ef114a73acd036f919e2533b_Nanolith Rules ENG.pdf" \
  "s3://$S3_BUCKET_NAME/pdf_uploads/b680bc20ef114a73acd036f919e2533b/b680bc20ef114a73acd036f919e2533b_Nanolith Rules ENG.pdf" \
  --copy-props none
sudo rm -f /tmp/s.env 2>/dev/null || true
EOF
```

Expected: `copy: s3://...` line.

- [ ] **Step 2.3: Reset Rules.pdf state to Failed for retry**

Run:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -c \
    "UPDATE pdf_documents SET processing_state='\''Failed'\'', \"RetryCount\"=0, \"ProcessingError\"=NULL WHERE \"Id\" = '\''b680bc20-ef11-4a73-acd0-36f919e2533b'\'';"'
```

Expected: `UPDATE 1`.

- [ ] **Step 2.4: Trigger retry via API**

Run:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'curl -s -i -b /tmp/cookies.txt -X POST http://localhost:8080/api/v1/documents/b680bc20-ef11-4a73-acd0-36f919e2533b/retry --max-time 30 | head -3'
```

Expected: `HTTP/1.1 200 OK`.

- [ ] **Step 2.5: Poll until Ready or Failed (max 5 min)**

Run:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 << 'EOF'
for i in 1 2 3 4 5 6 7 8 9 10; do
  sleep 30
  STATE=$(docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -tAc "SELECT processing_state FROM pdf_documents WHERE \"Id\" = 'b680bc20-ef11-4a73-acd0-36f919e2533b';")
  CHUNKS=$(docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -tAc "SELECT COUNT(*) FROM text_chunks WHERE \"PdfDocumentId\" = 'b680bc20-ef11-4a73-acd0-36f919e2533b';")
  echo "[$i] state=$STATE chunks=$CHUNKS"
  if [ "$STATE" = "Ready" ] || [ "$STATE" = "Failed" ]; then break; fi
done
EOF
```

Expected: final line `[N] state=Ready chunks=20+`.

If `state=Failed`, check error:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -tAc "SELECT \"ProcessingError\" FROM pdf_documents WHERE \"Id\" = '\''b680bc20-ef11-4a73-acd0-36f919e2533b'\'';"'
```

Then either retry from 2.3 or escalate to user.

- [ ] **Step 2.6: Acceptance G2 — verify both PDFs Ready**

Run:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -c \
    "SELECT pd.\"FileName\", pd.processing_state, COUNT(tc.*) AS chunks
     FROM pdf_documents pd LEFT JOIN text_chunks tc ON tc.\"PdfDocumentId\" = pd.\"Id\"
     WHERE pd.\"SharedGameId\" = '\''94e99e38-1a5a-499c-89a9-2ea66173f63e'\''
     GROUP BY pd.\"Id\", pd.\"FileName\", pd.processing_state
     ORDER BY pd.\"FileName\";"'
```

Expected: 2 rows, both `processing_state = Ready`, both `chunks ≥ 20`.

---

## Task 3: Create Tutor agent for Nanolith

**Files:**
- No code changes (use existing API)

- [ ] **Step 3.1: Check if Tutor agent already exists**

Run:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'curl -s -b /tmp/cookies.txt "http://localhost:8080/api/v1/agents?gameId=94e99e38-1a5a-499c-89a9-2ea66173f63e" | python3 -m json.tool 2>&1 | head -40'
```

Expected: JSON list of agents. If contains `"agentType":"Tutor"` for game 94e99e38, skip to Task 4.

- [ ] **Step 3.2: Create Tutor agent via quick-create endpoint**

Run:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'curl -s -i -b /tmp/cookies.txt -X POST http://localhost:8080/api/v1/agents/quick-create \
    -H "Content-Type: application/json" \
    -d "{\"gameId\":\"94e99e38-1a5a-499c-89a9-2ea66173f63e\"}" | head -20'
```

Expected: `HTTP/1.1 200 OK` (or 201). Body contains `"agentType":"Tutor"` and an `"id"` field. Save the returned `id` as `TUTOR_AGENT_ID`.

If 4xx error, check error body and try `/api/v1/agents/user` instead:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'curl -s -i -b /tmp/cookies.txt -X POST http://localhost:8080/api/v1/agents/user \
    -H "Content-Type: application/json" \
    -d "{\"gameId\":\"94e99e38-1a5a-499c-89a9-2ea66173f63e\",\"agentType\":\"Tutor\",\"name\":\"Tutor Nanolith\",\"documentIds\":[\"808b83ec-8e93-430e-95a8-c4918b2dab03\",\"b680bc20-ef11-4a73-acd0-36f919e2533b\"]}" | head -20'
```

- [ ] **Step 3.2b: Extract Tutor agent id**

Run:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'curl -s -b /tmp/cookies.txt "http://localhost:8080/api/v1/agents?gameId=94e99e38-1a5a-499c-89a9-2ea66173f63e" \
    | python3 -c "import json,sys; d=json.load(sys.stdin); agents=d if isinstance(d,list) else d.get(\"agents\",[]); 
        [print(a[\"id\"]) for a in agents if a.get(\"agentType\",\"\").lower()==\"tutor\"]" | head -1' \
  | tee /tmp/tutor_agent_id.txt
```

Expected: a UUID printed on one line. Save it to your local shell as `TUTOR_AGENT_ID=$(cat /tmp/local_tutor.txt)` if running follow-ups locally; or reference `$(cat /tmp/tutor_agent_id.txt)` inline.

For inline reuse on the staging server, embed the substitution inside ssh:

```bash
TUTOR_AGENT_ID=$(ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 'cat /tmp/tutor_agent_id.txt' | tr -d '\r\n')
echo "TUTOR_AGENT_ID=$TUTOR_AGENT_ID"
```

- [ ] **Step 3.3: Verify Tutor agent persisted with sources**

Run:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  "curl -s -b /tmp/cookies.txt http://localhost:8080/api/v1/agents/$TUTOR_AGENT_ID | python3 -m json.tool"
```

Expected: agent details with `agentType="Tutor"`, `isActive=true`, `gameId=94e99e38-...`. If sources weren't auto-linked by quick-create, link them in next step.

- [ ] **Step 3.4: Link both PDFs as sources to Tutor agent (if not already)**

Inspect existing agent sources:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  "curl -s -b /tmp/cookies.txt http://localhost:8080/api/v1/agents/$TUTOR_AGENT_ID/configuration | python3 -m json.tool 2>&1 | head -30"
```

If `documentIds` is empty or missing one PDF, update via:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  "curl -s -i -b /tmp/cookies.txt -X PUT http://localhost:8080/api/v1/agents/$TUTOR_AGENT_ID/configuration \
    -H 'Content-Type: application/json' \
    -d '{\"documentIds\":[\"808b83ec-8e93-430e-95a8-c4918b2dab03\",\"b680bc20-ef11-4a73-acd0-36f919e2533b\"]}' | head -10"
```

Expected: `HTTP/1.1 200 OK`.

---

## Task 4: Create Arbitro agent for Nanolith

**Files:**
- No code changes

- [ ] **Step 4.1: Check if Arbitro agent already exists**

Run (same as 3.1):
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'curl -s -b /tmp/cookies.txt "http://localhost:8080/api/v1/agents?gameId=94e99e38-1a5a-499c-89a9-2ea66173f63e" | python3 -m json.tool 2>&1 | head -60'
```

If JSON contains `"agentType":"Arbitro"`, skip to Task 5.

- [ ] **Step 4.2: Create Arbitro agent**

Run:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'curl -s -i -b /tmp/cookies.txt -X POST http://localhost:8080/api/v1/agents/user \
    -H "Content-Type: application/json" \
    -d "{\"gameId\":\"94e99e38-1a5a-499c-89a9-2ea66173f63e\",\"agentType\":\"Arbitro\",\"name\":\"Arbitro Nanolith\",\"documentIds\":[\"b680bc20-ef11-4a73-acd0-36f919e2533b\",\"808b83ec-8e93-430e-95a8-c4918b2dab03\"]}" | head -20'
```

Note: `documentIds` ordered with Rules first (priority 1) per Arbitro typology. Save returned `id` as `ARBITRO_AGENT_ID`.

Expected: `HTTP/1.1 200 OK` (or 201) with `"agentType":"Arbitro"`.

- [ ] **Step 4.3: Acceptance G3 — verify 2 active agents (API view)**

Run:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'curl -s -b /tmp/cookies.txt "http://localhost:8080/api/v1/agents?gameId=94e99e38-1a5a-499c-89a9-2ea66173f63e" | python3 -c "import json,sys; d=json.load(sys.stdin); agents=d if isinstance(d,list) else d.get(\"agents\",[]); [print(f\"{a.get(\"agentType\",\"?\")} | {a.get(\"name\",\"?\")} | active={a.get(\"isActive\",\"?\")}\") for a in agents]"'
```

Expected output:
```
Tutor   | Tutor Nanolith   | active=True
Arbitro | Arbitro Nanolith | active=True
```

- [ ] **Step 4.4: Acceptance G3 — SQL verification (Status + source count)**

The API view does not surface `Status` or `source_count`. The spec G3 acceptance requires both. Run the spec's verbatim SQL:

```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 << 'EOF'
docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -c "
SELECT a.\"Id\", a.\"Name\", a.\"Typology\", a.\"IsActive\", a.\"Status\",
       (SELECT COUNT(*) FROM agent_kb_sources s WHERE s.\"AgentId\" = a.\"Id\") AS source_count
FROM agent_definitions a
WHERE a.\"SharedGameId\" = '94e99e38-1a5a-499c-89a9-2ea66173f63e'
  AND a.\"Typology\" IN ('Tutor', 'Arbitro')
ORDER BY a.\"Typology\";
"
EOF
```

Expected: 2 rows, both `IsActive=t`, `Status='Published'`, `source_count=2`.

If `agent_definitions` table doesn't exist (only AgentDefinition entity in EF, mapped to a different table name), discover the actual table:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -tAc "
    SELECT tablename FROM pg_tables WHERE schemaname=$$public$$ AND tablename ~* $$(agent.*defin|definition)$$ ORDER BY tablename;
  "'
```

Then adapt the table name in the verification query. Document the discovered name in the spec doc References section.

---

## Task 5: Validate Q&A streaming via API (G4)

**Files:**
- No code changes

- [ ] **Step 5.1: Test Tutor Q&A — setup question**

⚠️ The endpoint takes `gameId` (which historically resolves both private and shared game ids). Try the games.Id first; if response is "no agents configured" or empty, retry with the sharedGameId.

Run (private games.Id first):
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'curl -s -N -b /tmp/cookies.txt -X POST http://localhost:8080/api/v1/agents/qa/stream \
    -H "Content-Type: application/json" \
    -d "{\"gameId\":\"6db3e01e-0b21-414c-8e3b-a899782feb40\",\"query\":\"Come si imposta il gioco per 2 giocatori?\",\"responseStyle\":\"concise\"}" \
    --max-time 60 | head -50'
```

If the stream contains `"errorCode":"NO_AGENTS"` or similar, retry with sharedGameId:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'curl -s -N -b /tmp/cookies.txt -X POST http://localhost:8080/api/v1/agents/qa/stream \
    -H "Content-Type: application/json" \
    -d "{\"gameId\":\"94e99e38-1a5a-499c-89a9-2ea66173f63e\",\"query\":\"Come si imposta il gioco per 2 giocatori?\",\"responseStyle\":\"concise\"}" \
    --max-time 60 | head -50'
```

Whichever ID succeeds, document it in the spec References section as the canonical id for chat queries.

Expected: SSE event stream containing:
- `data: {"Type":0,...}` (StateUpdate)
- `data: {"Type":1,...}` (Citations) — at least 1 referencing Press Start
- `data: {"Type":7,...}` (Token) — multiple, with `"token":"..."` content
- `data: {"Type":4,...}` (Complete)

Save the Type=4 event JSON for later validation.

If you see `"Type":5` (Error) with `"errorCode":"INTERNAL_ERROR"`, check API logs:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'docker logs meepleai-api --since 1m 2>&1 | grep -i error | tail -10'
```

Most likely re-emergent bug — escalate.

- [ ] **Step 5.2: Test Arbitro Q&A — rules question**

Run:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 \
  'curl -s -N -b /tmp/cookies.txt -X POST http://localhost:8080/api/v1/agents/qa/stream \
    -H "Content-Type: application/json" \
    -d "{\"gameId\":\"6db3e01e-0b21-414c-8e3b-a899782feb40\",\"query\":\"Quante azioni puo fare un giocatore in un turno?\",\"responseStyle\":\"concise\"}" \
    --max-time 60 | head -50'
```

Expected: similar SSE stream. Citations should reference both PDFs (or primarily Rules, depending on retrieval scoring).

- [ ] **Step 5.3: Acceptance G4 — capture full answer for ground-truth check**

Run with extraction of complete answer text:
```bash
ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69 << 'EOF'
curl -s -N -b /tmp/cookies.txt -X POST http://localhost:8080/api/v1/agents/qa/stream \
  -H "Content-Type: application/json" \
  -d '{"gameId":"6db3e01e-0b21-414c-8e3b-a899782feb40","query":"Come si imposta il gioco per 2 giocatori?","responseStyle":"concise"}' \
  --max-time 60 | python3 -c '
import sys, json
buf = ""
tokens = []
citations = []
for line in sys.stdin:
    if line.startswith("data: "):
        try:
            evt = json.loads(line[6:])
            if evt.get("Type") == 7:
                tokens.append(evt.get("Data", {}).get("token", ""))
            elif evt.get("Type") == 1:
                citations.extend(evt.get("Data", {}).get("citations", []))
        except: pass
print("=== ANSWER ===")
print("".join(tokens))
print("=== CITATIONS ===")
for c in citations[:3]:
    print(f"  - {c.get(\"source\",\"?\")} p.{c.get(\"page\",\"?\")}: {c.get(\"text\",\"\")[:100]}")
'
EOF
```

Expected: an answer text + at least 1 citation. **Acceptance**: user (Aaron) reads the answer and confirms it's coherent with Press Start tutorial PDF (manual ground-truth check).

---

## Task 6: UI smoke validation (G5)

**Files:**
- No code changes

- [ ] **Step 6.1: Verify no v2 component changes**

Run from local repo:
```bash
cd D:/Repositories/meepleai-monorepo-dev
git diff --stat origin/main-dev..HEAD apps/web/src/components/v2/ 2>&1 | tail -5
```

Expected: empty output (no files modified). If any line appears, audit the change against freeze #808.

- [ ] **Step 6.2: Open game detail page in browser via Playwright**

Use `mcp__plugin_playwright_playwright__browser_navigate` to open `https://meepleai.app/games/6db3e01e-0b21-414c-8e3b-a899782feb40`. Note: Aaron must complete CF Access auth manually (Google SSO). Skip if already authenticated in this session.

After navigation, run `mcp__plugin_playwright_playwright__browser_take_screenshot` and save as `nanolith-game-detail.png`.

- [ ] **Step 6.3: Compare screenshot to mockup**

Open `admin-mockups/design_files/sp4-game-detail.html` in second tab. Side-by-side compare:
- Game hero card (title, publisher, year, players)
- KB documents list
- Agent picker / chat entry button

Document gaps in `docs/superpowers/specs/2026-05-07-nanolith-staging-setup-design.md` Section 11 References as "Gap audit 2026-05-07" inline list. Each gap entry: 1 line `- [gap] component X: real layout uses Y, mockup uses Z (post-Q3 follow-up)`.

- [ ] **Step 6.4: Verify chat panel shows typing indicator amber**

In the same browser tab, click "Chat AI" / agent picker. Send query "Come si imposta il gioco per 2 giocatori?". Within 2s of clicking send, the assistant placeholder bubble must show 3 amber dots (PR-B fix verification).

Take screenshot mid-streaming, save as `nanolith-chat-typing.png`.

- [ ] **Step 6.5: Acceptance G5 — commit screenshots + gap notes**

Run:
```bash
cd D:/Repositories/meepleai-monorepo-dev
git checkout -b docs/nanolith-smoke-screenshots-2026-05-07 main-dev
mkdir -p docs/superpowers/specs/screenshots/nanolith-smoke-2026-05-07
mv nanolith-game-detail.png docs/superpowers/specs/screenshots/nanolith-smoke-2026-05-07/
mv nanolith-chat-typing.png docs/superpowers/specs/screenshots/nanolith-smoke-2026-05-07/
git add docs/superpowers/specs/screenshots/nanolith-smoke-2026-05-07/ docs/superpowers/specs/2026-05-07-nanolith-staging-setup-design.md
git commit -m "docs(smoke): Nanolith staging E2E screenshots + gap audit"
git push -u origin docs/nanolith-smoke-screenshots-2026-05-07
```

Then open PR via `gh pr create --base main-dev --title "docs(smoke): Nanolith E2E screenshots + UI gap audit" --body "Screenshots from 2026-05-07 smoke test + UI gaps vs sp4 mockups (post-Q3 follow-up)."`.

---

## Task 7: PR-F clean S3 retrieve key fix (TDD-style)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfStorageKey.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs:334`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/PdfStorageKeyTests.cs`

- [ ] **Step 7.1: Create branch from main-dev**

Run:
```bash
cd D:/Repositories/meepleai-monorepo-dev
git checkout main-dev && git pull origin main-dev
git checkout -b fix/pdf-blob-storage-retrieve-key-mismatch
git config branch.fix/pdf-blob-storage-retrieve-key-mismatch.parent main-dev
```

- [ ] **Step 7.2: Write failing test for PdfStorageKey.BucketGameKey helper**

Edit `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/PdfStorageKeyTests.cs` and add at the end of the `PdfStorageKeyTests` class:

```csharp
    [Fact]
    public void BucketGameKey_PrivateGameSet_ReturnsPrivateGameIdNoHyphens()
    {
        var pdf = new Api.Infrastructure.Entities.PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            PrivateGameId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            FileName = "x.pdf",
            FilePath = "/tmp/x.pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready"
        };

        PdfStorageKey.BucketGameKey(pdf).Should().Be("11111111111111111111111111111111");
    }

    [Fact]
    public void BucketGameKey_SharedGameSet_ReturnsSharedGameIdNoHyphens()
    {
        var pdf = new Api.Infrastructure.Entities.PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            FileName = "x.pdf",
            FilePath = "/tmp/x.pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready"
        };

        PdfStorageKey.BucketGameKey(pdf).Should().Be("22222222222222222222222222222222");
    }

    [Fact]
    public void BucketGameKey_PrivateOverridesShared_ReturnsPrivateGameId()
    {
        var pdf = new Api.Infrastructure.Entities.PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            PrivateGameId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            SharedGameId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            FileName = "x.pdf",
            FilePath = "/tmp/x.pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready"
        };

        PdfStorageKey.BucketGameKey(pdf).Should().Be("33333333333333333333333333333333");
    }

    [Fact]
    public void BucketGameKey_NoGameId_Throws()
    {
        var pdf = new Api.Infrastructure.Entities.PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "x.pdf",
            FilePath = "/tmp/x.pdf",
            UploadedByUserId = Guid.NewGuid(),
            ProcessingState = "Ready"
        };

        Action act = () => PdfStorageKey.BucketGameKey(pdf);
        act.Should().Throw<InvalidOperationException>();
    }
```

- [ ] **Step 7.3: Run tests — verify they fail**

Run:
```bash
cd D:/Repositories/meepleai-monorepo-dev/apps/api
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PdfStorageKeyTests.BucketGameKey" --nologo 2>&1 | tail -10
```

Expected: 4 failures with `'BucketGameKey' is inaccessible due to its protection level` or `does not contain a definition for 'BucketGameKey'`.

- [ ] **Step 7.4: Implement BucketGameKey helper**

Edit `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfStorageKey.cs`:

```csharp
using Api.Infrastructure.Entities;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

public static class PdfStorageKey
{
    /// <summary>
    /// Bucket key for PDF storage. Uses pdf.Id to decouple from game lifecycle.
    /// Pre-migration PDFs stored under gameId bucket must be rebucket-ed by scripts/rebucket-pdfs.*
    /// </summary>
    public static string ForPdf(Guid pdfId) => pdfId.ToString("N");

    /// <summary>
    /// Game id segment for the S3 bucket path <c>pdf_uploads/{gameId}/{pdfId}_*</c>.
    /// </summary>
    /// <remarks>
    /// Save (UploadPdfCommandHandler) and retrieve (PdfProcessingPipelineService) MUST use the
    /// same path. Historically, retrieve passed pdfId as gameId — causing
    /// FileNotFoundException for shared-game PDFs because save used the real game id.
    /// This helper centralizes the resolution: PrivateGameId wins (authored content path),
    /// otherwise SharedGameId, otherwise throw because the bucket key is undefined.
    /// </remarks>
    public static string BucketGameKey(PdfDocumentEntity pdf)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        if (pdf.PrivateGameId.HasValue)
        {
            return pdf.PrivateGameId.Value.ToString("N");
        }

        if (pdf.SharedGameId.HasValue)
        {
            return pdf.SharedGameId.Value.ToString("N");
        }

        throw new InvalidOperationException(
            $"PdfDocumentEntity {pdf.Id} has neither PrivateGameId nor SharedGameId — bucket game key undefined.");
    }
}
```

- [ ] **Step 7.5: Run tests — verify they pass**

Run:
```bash
cd D:/Repositories/meepleai-monorepo-dev/apps/api
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PdfStorageKeyTests" --nologo 2>&1 | tail -5
```

Expected: 5 passed (1 existing + 4 new). 0 failed.

- [ ] **Step 7.6: Wire helper into ExtractTextAsync**

Edit `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs` line 334:

Replace:
```csharp
        var fileId = PdfStorageKey.ForPdf(pdfDoc.Id);
        var fileStream = await _blobStorageService.RetrieveAsync(fileId, fileId, cancellationToken).ConfigureAwait(false);
```

With:
```csharp
        var fileId = PdfStorageKey.ForPdf(pdfDoc.Id);
        var bucketGameKey = PdfStorageKey.BucketGameKey(pdfDoc);
        var fileStream = await _blobStorageService.RetrieveAsync(fileId, bucketGameKey, cancellationToken).ConfigureAwait(false);
```

- [ ] **Step 7.7: Build API to verify compilation**

Run:
```bash
cd D:/Repositories/meepleai-monorepo-dev/apps/api
dotnet build src/Api/Api.csproj --nologo 2>&1 | tail -5
```

Expected: `Compilazione completata. Avvisi: 0. Errori: 0`.

- [ ] **Step 7.8: Run DocumentProcessing unit suite — no regressions**

Run:
```bash
cd D:/Repositories/meepleai-monorepo-dev/apps/api
dotnet test tests/Api.Tests/Api.Tests.csproj --filter "Category=Unit&BoundedContext=DocumentProcessing" --nologo 2>&1 | tail -5
```

Expected: all green (244+ tests passing, none failed).

- [ ] **Step 7.9: Commit**

Run:
```bash
cd D:/Repositories/meepleai-monorepo-dev
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfStorageKey.cs apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Services/PdfStorageKeyTests.cs
git commit -m "$(cat <<'EOF'
fix(pdf-indexing): align S3 retrieve key with save path via BucketGameKey helper

PdfProcessingPipelineService.ExtractTextAsync passed (fileId, fileId) to
RetrieveAsync — using pdfId as the gameId bucket segment. UploadPdfCommandHandler
always saved under the real gameId, so any shared-game PDF triggered a 
FileNotFoundException at extraction time. Manual S3 copies were the only
workaround on staging (see #78).

New PdfStorageKey.BucketGameKey(pdf) helper resolves the bucket game segment:
PrivateGameId → SharedGameId → throw if neither. ExtractTextAsync now uses it
so save and retrieve always agree.

4 new unit tests (private/shared/private-overrides-shared/throws-no-id).
244/244 DocumentProcessing unit suite remains green.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 7.10: Push and open PR**

Run:
```bash
cd D:/Repositories/meepleai-monorepo-dev
git push -u origin fix/pdf-blob-storage-retrieve-key-mismatch
gh pr create --base main-dev --title "fix(pdf-indexing): align S3 retrieve key with save via BucketGameKey helper" --body "$(cat <<'EOF'
## Summary

Fixes the staging blocker where uploading a new shared-game PDF resulted in 
\`FileNotFoundException: PDF file not found in blob storage or filesystem\`.
\`PdfProcessingPipelineService.ExtractTextAsync\` passed pdfId as the gameId
segment to \`RetrieveAsync\`, while \`UploadPdfCommandHandler\` saves under the
real game id. Discovered while smoking Nanolith Q&A on 2026-05-06 (PR #810
session).

## Fix

New helper \`PdfStorageKey.BucketGameKey(pdf)\` returns the correct game id
segment (PrivateGameId → SharedGameId → throw). \`ExtractTextAsync\` now uses
it so save/retrieve agree.

## Test plan

- [x] 4 new unit tests in \`PdfStorageKeyTests\`
- [x] \`dotnet build\` clean (0 warnings, 0 errors)
- [x] DocumentProcessing unit suite green (244+ tests)
- [ ] Post-merge: redeploy staging, smoke upload a fresh PDF, verify Ready without manual S3 copy

## Refs

- Task #78 (S3 retrieve key bug tracking)
- Smoke session 2026-05-06 (Nanolith staging E2E)
- Spec: \`docs/superpowers/specs/2026-05-07-nanolith-staging-setup-design.md\`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Expected: PR URL printed.

- [ ] **Step 7.11: Code review (subagent)**

Dispatch the `feature-dev:code-reviewer` agent on the resulting PR. Address any HIGH/CRITICAL findings inline. Skip MEDIUM/LOW unless trivial.

- [ ] **Step 7.12: Merge after CI green**

Run:
```bash
cd D:/Repositories/meepleai-monorepo-dev
gh pr view <PR_NUM> --json statusCheckRollup --jq '[.statusCheckRollup[] | select(.conclusion=="FAILURE" and (.name | contains("Python") | not))] | length'
```

Expected: `0` (only Python pre-existing failure, if any). If non-zero, fix or escalate.

Then:
```bash
gh pr merge <PR_NUM> --squash --delete-branch --admin
```

- [ ] **Step 7.13: Cleanup local branch**

Run:
```bash
cd D:/Repositories/meepleai-monorepo-dev
git checkout main-dev && git pull origin main-dev
git branch -D fix/pdf-blob-storage-retrieve-key-mismatch 2>/dev/null || true
git remote prune origin
```

---

## Acceptance rollup

After all 7 tasks are complete, verify the SMART goals from the spec:

- [ ] **G1**: Login + game state ready — Task 1 + manual browser check
- [ ] **G2**: Both PDFs Ready, ≥20 chunks each — Task 2.6 SQL output
- [ ] **G3**: 2 active agents (Tutor + Arbitro) — Task 4.3 API output
- [ ] **G4**: Q&A streaming + citation — Task 5.3 captured answer
- [ ] **G5**: No new v2 components, A11y E2E unchanged — Task 6.1 git diff + Task 6.5 PR

When all checked, smoke is "gold path Nanolith" ready for any future test session.

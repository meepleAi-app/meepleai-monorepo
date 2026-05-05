# Smartphone E2E Test Checklist — meepleai.app

> Pre-test requirements per smartphone E2E session di Sara persona.

## Pre-merge (Claude — code complete)

- [x] G2 PR #714 — PhotoBatchProcessor → KB indexing
- [x] G3 PR #715 — IKnowledgeBaseIngestService ACL persistence
- [x] G4 PR #716 — GetParagraphQuery + endpoint
- [x] G5 PR #718 — TranslationViewer mobile UI
- [ ] G6+G7+G8 PR — smartphone-ready bundle (THIS PR)

## Pre-deploy (Aaron actions)

### 1. Merge sequence (squash, admin)

1. Merge PR #714 (G2)
2. Merge PR #715 (G3)
3. Merge PR #716 (G4)
4. Merge PR #718 (G5)
5. Merge G6+G7+G8 PR (this)

### 2. Staging deploy

Trigger via GitHub Actions:
- Workflow: `.github/workflows/deploy-staging.yml`
- Verify successful deploy log
- Health check: `curl https://staging.meepleai.app/health`

### 3. Database migrations applied

EF migrations da applicare on staging Postgres:
- `20260504205240_AddPhotoIngestion` (Sprint 0 Task 1.2)
- `20260505090846_AddGlossaryEntriesToGameMemory` (Phase 2 Task 2.6)

```bash
# On staging server (via SSH)
ssh staging
docker exec meepleai-api dotnet ef database update --context MeepleAiDbContext
```

### 4. Smoldocling service deployed

Verify Python smoldocling-service on staging:
- Endpoint: `http://smoldocling:8500/api/v1/preprocess` reachable internal
- Logs: `docker logs meepleai-smoldocling`
- Test via API container: `docker exec meepleai-api curl -X POST http://smoldocling:8500/health`

### 5. Demo game in SharedGameCatalog

Need at least 1 game seeded. Options:
- **A** Use existing seeded game (verify which games exist via admin panel)
- **B** Create test game: "Tainted Grail (Test)" via admin endpoint POST `/api/v1/shared-games`
- **C** BGG import: use BGG ID 264220 (Tainted Grail) via admin

### 6. Test user account

Create or designate user account:
- Email: aaron+test@meepleai.app (or existing test user)
- Permissions: standard user (no admin needed)
- Password: strong, only for test session

### 7. Smartphone access

- Verify HTTPS cert valid on `meepleai.app` AND `staging.meepleai.app`
- Verify CSP allows HTTPS images from blob storage
- Test from iPhone Safari + Android Chrome
- Camera permission flow works

### 8. Pre-test smoke checks

From smartphone browser:
- [ ] Login flow funziona
- [ ] Navigate to `/gamebook/upload` — game picker shows games list
- [ ] Search box in game picker works
- [ ] Select a game → transitions to upload step
- [ ] "Change game" back button returns to picker
- [ ] Camera button opens camera back (verify `capture="environment"`)
- [ ] Upload 2-3 test photos succeeds (verify <= 5 limit)
- [ ] Status polling shows progress bar
- [ ] On completion, "Inizia chat" (Start chat) button is visible
- [ ] Click button → `/chat?gameId=...` navigates correctly
- [ ] Q&A in chat returns SOMETHING (even "non lo so" — verifies endpoint reachable)

## Test session protocol

Once all 8 checks green, schedule Aaron + Claude session:
- Test scenario 1: happy path (login → pick game → 5 photos → indexing wait → Q&A)
- Test scenario 2: empty page text → semantic fallback (G4)
- Test scenario 3: paragraph view (G5 TranslationViewer)
- Document findings in `docs/libro-game-assistant/SMARTPHONE_TEST_RESULTS.md`

## Known limitations (document, not blockers)

- **G1 deferred**: max 5 photos per batch + soft 1 MB warning (no client-side compression — Phase 3 follow-up)
- **Translation backend deferred**: TranslationViewer shows source text passthrough (Phase 3 Task 3.5e wires `INarrativeTranslationService` HTTP endpoint)
- **No game-night session integration**: each upload is standalone
- **No multi-device sync**: read-only QR pattern deferred MVP-1 (vision §6.2)
- **Game picker**: shows first 20 games from catalog (search supported); paginated browsing deferred

## Rollback plan

If staging is broken post-deploy:
```bash
# On staging server
docker compose down
git revert <last-merge-sha>
git push
# Re-trigger deploy workflow
```

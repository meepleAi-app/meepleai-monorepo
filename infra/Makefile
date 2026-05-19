.DEFAULT_GOAL := help
COMPOSE := docker compose -f docker-compose.yml

# === Environments ===

dev: ## Start full local development (all profiles)
	$(COMPOSE) -f compose.dev.yml \
		--profile ai --profile monitoring --profile automation \
		--profile proxy --profile storage up -d

dev-core: ## Start core only (postgres, redis, api, web)
	$(COMPOSE) -f compose.dev.yml up -d

dev-down: ## Stop dev environment
	$(COMPOSE) -f compose.dev.yml --profile ai --profile monitoring \
		--profile automation --profile proxy --profile storage down

tunnel: ## Open SSH tunnels to staging server
	bash scripts/integration-tunnel.sh start

tunnel-stop: ## Close SSH tunnels
	bash scripts/integration-tunnel.sh stop

tunnel-status: ## Check SSH tunnel status
	bash scripts/integration-tunnel.sh status

integration-check: ## Check prerequisites before make integration (SSH key, tunnel, ports)
	bash scripts/integration-check.sh

integration: ## Start local api+web with remote services (run 'make tunnel' first)
	@echo "Ensure tunnels are open: make tunnel"
	bash scripts/integration-start.sh

integration-down: ## Stop integration (local processes + tunnels)
	bash scripts/integration-start.sh stop
	bash scripts/integration-tunnel.sh stop

work: ## Hybrid dev loop: tunnel + local API+Web + watchdog (single command)
	bash scripts/work.sh start

work-stop: ## Stop work session cleanly (alternative to Ctrl+C)
	bash scripts/work.sh stop

smoke: ## Run automated smoke set against BASE_URL (default localhost:8080) — set TEST_EMAIL/TEST_PASSWORD for auth scenarios
	bash scripts/smoke-set.sh $(BASE_URL) $(TEST_EMAIL) $(TEST_PASSWORD)

smoke-staging: ## Run automated smoke set against staging (https://meepleai.app)
	bash scripts/smoke-set.sh https://meepleai.app $(TEST_EMAIL) $(TEST_PASSWORD)

smoke-pg-readback: ## Run pg-readback.sh DB-direct migration validation (requires open tunnel)
	bash scripts/pg-readback.sh

staging: ## Deploy staging — core + AI + monitoring (CAX31+ recommended, ~14GB reservation, edge via CF Tunnel)
	$(COMPOSE) -f compose.staging.yml \
		--profile ai --profile monitoring up -d

staging-minimal: ## Deploy staging single-tester profile (~3GB RAM) — edge via Cloudflare Tunnel (cloudflared on VPS, not Traefik)
	$(COMPOSE) -f compose.staging.yml \
		--profile ai-essential --profile monitoring-essential up -d

staging-with-tutor: ## Deploy staging single-tester + orchestration-service for tutor agents (~5GB RAM)
	$(COMPOSE) -f compose.staging.yml \
		--profile ai-essential --profile monitoring-essential --profile tutor-agents up -d

staging-tutor-down: ## Stop only orchestration-service (keep rest of staging running)
	$(COMPOSE) -f compose.staging.yml --profile tutor-agents stop orchestration-service
	$(COMPOSE) -f compose.staging.yml --profile tutor-agents rm -f orchestration-service

staging-core: ## (Deprecated, use staging-minimal) Deploy staging core only — no AI services
	$(COMPOSE) -f compose.staging.yml \
		--profile monitoring up -d

staging-down: ## Stop staging (all profiles)
	$(COMPOSE) -f compose.staging.yml \
		--profile ai --profile monitoring down

prod: ## Deploy production (requires PROD_DOMAIN env var, edge via CF Tunnel)
	@test -n "$(PROD_DOMAIN)" || (echo "Error: PROD_DOMAIN is required" && exit 1)
	$(COMPOSE) -f compose.prod.yml \
		--profile ai --profile monitoring up -d

prod-down: ## Stop production
	$(COMPOSE) -f compose.prod.yml \
		--profile ai --profile monitoring down

# === Secrets ===

secrets-setup: ## Generate placeholder secrets from .example files
	pwsh -File secrets/setup-secrets.ps1

secrets-sync: ## Sync secrets from staging server (requires SSH access)
	bash scripts/sync-secrets.sh

# === Database Seeds ===

snapshot: ## Snapshot staging DB to infra/seeds/ (requires SSH access)
	bash scripts/snapshot-staging.sh

restore-staging: ## Restore staging DB from latest snapshot
	bash scripts/restore-staging.sh

restore-local: ## Restore local dev DB from latest snapshot (requires make dev running)
	bash scripts/restore-local.sh

seed-games: ## Seed test games on staging via API (requires SSH access)
	bash scripts/seed-all-games-staging.sh

seed-nanolith-demo: ## Sprint -1: seed Nanolith libro game demo (account + game + 2 KBs + agent), against local stack
	bash scripts/seed-nanolith-demo.sh local

seed-nanolith-demo-staging: ## Same, against staging tunnel (requires SSH key + INITIAL_ADMIN_* secrets)
	bash scripts/seed-nanolith-demo.sh staging

# === Demo Runthrough Phase A ===

demo-smoke-local: ## Stadio 1 G1: smoke test 7 endpoint /api/v1/* contro stack locale (richiede make dev + make seed-nanolith-demo)
	bash scripts/demo-smoke-local.sh local

demo-smoke-staging: ## Stadio 3 G3: smoke test 7 endpoint contro https://meepleai.app (richiede seed-nanolith-demo-staging eseguito)
	bash scripts/demo-smoke-local.sh staging

# === RAG Backup ===
rag-export: ## Full RAG export (one-shot for production go-live)
	@echo "📦 Running full RAG data export..."
	curl -s -X POST "http://localhost:8080/admin/rag-backup/export?dryRun=false" | python3 -m json.tool

rag-export-dry: ## Preview RAG export without executing
	@echo "🔍 Previewing RAG data export (dry run)..."
	curl -s -X POST "http://localhost:8080/admin/rag-backup/export?dryRun=true" | python3 -m json.tool

rag-backup-status: ## Status of latest RAG backup
	@echo "📊 Checking latest RAG backup status..."
	curl -s "http://localhost:8080/admin/rag-backup/snapshots/latest" | python3 -m json.tool

rag-snapshots: ## List all available RAG backup snapshots
	@echo "📋 Available RAG backup snapshots:"
	curl -s "http://localhost:8080/admin/rag-backup/snapshots" | python3 -m json.tool

.PHONY: rag-export rag-export-dry rag-backup-status rag-snapshots

# === Backup & Disaster Recovery ===

backup: ## Run full backup (PostgreSQL + PDFs → local + R2)
	bash scripts/backup.sh

backup-verify: ## Verify latest backup integrity
	bash scripts/backup-verify.sh

backup-restore-test: ## Test restore on temp PostgreSQL container
	bash scripts/backup-restore-test.sh

backup-status: ## Show latest backup info
	@echo "📊 Latest local backups:"
	@ls -lht /backups/meepleai/ 2>/dev/null | head -8 || echo "  No backups found in /backups/meepleai/"
	@echo ""
	@echo "📊 Disk usage:"
	@du -sh /backups/meepleai/ 2>/dev/null || echo "  No backup directory"

backup-cron-install: ## Install all infrastructure cron jobs (backup, verify, prune, DR)
	@echo "Installing infrastructure cron jobs..."
	@(crontab -l 2>/dev/null | grep -vE "meepleai.*(backup|prune|dr-walkthrough|restore-test)"; \
	  echo "0 3 * * * cd /opt/meepleai/repo/infra && bash scripts/backup.sh >> /var/log/meepleai-backup.log 2>&1"; \
	  echo "30 3 * * * cd /opt/meepleai/repo/infra && bash scripts/backup-verify.sh >> /var/log/meepleai-backup-verify.log 2>&1"; \
	  echo "0 5 * * * cd /opt/meepleai/repo/infra && bash scripts/daily-disk-prune.sh >> /var/log/meepleai-disk-prune.log 2>&1"; \
	  echo "0 4 1 * * cd /opt/meepleai/repo/infra && bash scripts/backup-restore-test.sh --with-smoke-readback >> /var/log/meepleai-restore-test.log 2>&1"; \
	  echo "0 9 1 1,4,7,10 * cd /opt/meepleai/repo/infra && bash scripts/dr-walkthrough-reminder.sh >> /var/log/meepleai-dr-reminder.log 2>&1") | crontab -
	@echo "✅ Cron installed:"
	@echo "  daily 03:00  → backup"
	@echo "  daily 03:30  → backup-verify"
	@echo "  daily 05:00  → disk-prune"
	@echo "  monthly 1st 04:00  → restore-test (with smoke read-back)"
	@echo "  quarterly 09:00    → DR walkthrough reminder"

backup-cron-show: ## Show installed backup cron jobs
	@crontab -l 2>/dev/null | grep meepleai || echo "No MeepleAI cron jobs found"

# === DB Snapshot (local dev) ===

db-snapshot: ## Create local DB snapshot before migration reset
	pwsh scripts/db-save-state.ps1

db-snapshot-sanitized: ## Create DB snapshot without sensitive tables
	pwsh scripts/db-save-state.ps1 -Sanitize

db-reset-migrations: ## Reset migrations using snapshot (requires SNAP=path)
	@if [ -z "$(SNAP)" ]; then echo "ERROR: SNAP=<snapshot-path> required" && exit 1; fi
	pwsh scripts/db-reset-migrations.ps1 -SnapshotPath $(SNAP)

db-restore-snapshot: ## Restore data from snapshot (requires SNAP=path)
	@if [ -z "$(SNAP)" ]; then echo "ERROR: SNAP=<snapshot-path> required" && exit 1; fi
	pwsh scripts/db-restore-state.ps1 -SnapshotPath $(SNAP)

db-rollback-safetynet: ## Rollback using snapshot safety-net (requires SNAP=path)
	@if [ -z "$(SNAP)" ]; then echo "ERROR: SNAP=<snapshot-path> required" && exit 1; fi
	pwsh scripts/db-restore-state.ps1 -SnapshotPath $(SNAP) -UseSafetyNet

# === Utilities ===

logs: ## Tail logs for a service (usage: make logs s=api)
	$(COMPOSE) logs -f $(s)

logging: ## Start log aggregation stack on staging (loki + fluent-bit)
	$(COMPOSE) -f compose.staging.yml -f compose.logging.yml --profile logging up -d

logging-down: ## Stop log aggregation stack
	$(COMPOSE) -f compose.logging.yml --profile logging down

logs-ui: ## Open Grafana log explorer in browser (staging)
	@echo "Grafana Logs: https://meepleai.app/grafana"
	@echo "  Explore → Loki → {container_name=~\"meepleai-.*\"}"

# ================================================================
# Seed snapshot lifecycle (bake + consume)
# See docs/development/snapshot-seed-workflow.md
# ================================================================

seed-index: ## Bake: indicizza tutti i PDF e produce snapshot locale
	@bash scripts/seed-index-preflight.sh
	$(COMPOSE) -f compose.dev.yml --profile ai up -d postgres redis api embedding-service smoldocling-service
	@bash scripts/wait-for-healthy.sh api 180
	@bash scripts/seed-index-wait.sh
	@bash scripts/seed-index-dump.sh
	@echo "::notice:: snapshot pronto in data/snapshots/ — lancia 'make seed-index-publish' per caricarlo"

seed-index-publish: ## Upload snapshot più recente a seed blob bucket
	@bash scripts/seed-index-publish.sh

dev-from-snapshot: ## Dev env con seed pre-indicizzato (veloce, consume flow)
	@bash scripts/snapshot-fetch.sh
	@bash scripts/snapshot-verify.sh
	$(COMPOSE) -f compose.dev.yml up -d postgres
	@bash scripts/wait-for-healthy.sh postgres 60
	@bash scripts/snapshot-restore.sh
	@SKIP_CATALOG_SEED=true $(COMPOSE) -f compose.dev.yml --profile ai up -d
	@echo "::notice:: dev env pronto (snapshot $$(cat data/snapshots/.latest))"

dev-from-snapshot-force: ## Come dev-from-snapshot ma azzera il volume postgres
	$(COMPOSE) -f compose.dev.yml rm -svf postgres || true
	$(COMPOSE) -f compose.dev.yml down postgres -v || true
	@$(MAKE) dev-from-snapshot

ps: ## Show running containers
	$(COMPOSE) ps

build: ## Build all images
	$(COMPOSE) build

clean: ## Remove all volumes and containers (DATA LOSS!)
	@echo "WARNING: This will delete ALL data. Press Ctrl+C to cancel."
	@sleep 3
	$(COMPOSE) down -v --remove-orphans

help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | \
		awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-20s\033[0m %s\n", $$1, $$2}'

# === Local CI Simulation (mirrors GitHub Actions) ===

ROOT := $(shell cd .. && pwd)
API := $(ROOT)/apps/api
API_SRC := $(API)/src/Api
API_TESTS := $(API)/tests/Api.Tests/Api.Tests.csproj
WEB := $(ROOT)/apps/web

ci: ci-fe ci-be-unit ## Run full CI (frontend + backend unit) — mirrors ci.yml
	@echo "✅ Local CI passed"

ci-fe: ## Frontend CI: lint + typecheck + test + build — mirrors ci.yml frontend job
	@echo "🔵 Frontend CI..."
	cd $(WEB) && pnpm lint
	cd $(WEB) && pnpm typecheck
	cd $(WEB) && pnpm test
	cd $(WEB) && pnpm build
	@echo "✅ Frontend CI passed"

ci-be-unit: ## Backend unit tests — mirrors ci.yml backend-unit job
	@echo "🔵 Backend unit tests..."
	cd $(API_SRC) && dotnet build --nologo -v q
	cd $(API) && dotnet test $(API_TESTS) --filter "Category=Unit" --nologo -v q
	@echo "✅ Backend unit tests passed"

ci-be-unit-coverage: ## Backend unit tests with coverage report (lcov in tests/Api.Tests/coverage/)
	@echo "🔵 Backend unit tests + coverage..."
	cd $(API_SRC) && dotnet build --nologo -v q
	cd $(API) && dotnet test $(API_TESTS) --filter "Category=Unit" --nologo -v q \
		-p:CollectCoverage=true \
		-p:CoverletOutputFormat=lcov \
		-p:CoverletOutput=./tests/Api.Tests/coverage/unit-coverage.lcov
	@echo "✅ Coverage report: $(API)/tests/Api.Tests/coverage/unit-coverage.lcov"

ci-be-integration: ## Backend integration tests (self-contained via Testcontainers, no make dev needed)
	@echo "🔵 Backend integration tests (Testcontainers auto-spawn)..."
	cd $(API) && dotnet test $(API_TESTS) --filter "Category=Integration" --nologo -v q
	@echo "✅ Backend integration tests passed"

ci-be-integration-fast: ## Backend integration tests using running dev-core containers (faster, requires make dev-core)
	@echo "🔵 Backend integration tests (fast mode, using dev-core containers)..."
	@echo "   Ensure 'make dev-core' is running before this target."
	cd $(API) && \
		TEST_POSTGRES_CONNSTRING="Host=localhost;Port=5432;Database=meepleai_dev;Username=meepleai;Password=$$(grep POSTGRES_PASSWORD $(ROOT)/infra/secrets/database.secret | cut -d= -f2);MaxPoolSize=2;Timeout=30;Command Timeout=60" \
		TEST_REDIS_CONNSTRING="localhost:6379,allowAdmin=true,password=$$(grep REDIS_PASSWORD $(ROOT)/infra/secrets/redis.secret | cut -d= -f2)" \
		dotnet test $(API_TESTS) --filter "Category=Integration" --nologo -v q
	@echo "✅ Backend integration tests passed (fast mode)"

ci-be-e2e: ## Backend E2E tests (Testcontainers) — mirrors backend-e2e-tests.yml
	@echo "🔵 Backend E2E tests..."
	cd $(API) && dotnet test $(API_TESTS) --filter "Category=E2E" --nologo -v q
	@echo "✅ Backend E2E tests passed"

ci-all: ci ci-be-integration ci-be-e2e ## Run everything: FE + BE unit + integration + E2E
	@echo "✅ Full CI simulation passed"

sec-scan: ## Security scan: dependency audit + semgrep — mirrors security-scan.yml
	@echo "🔵 Security scan..."
	cd $(API_SRC) && dotnet list package --vulnerable --include-transitive
	cd $(WEB) && pnpm audit --audit-level=high || true
	@which semgrep > /dev/null 2>&1 && semgrep scan --config=p/secrets --config=p/security-audit $(ROOT) || echo "⚠️  semgrep not installed (pip install semgrep)"
	@echo "✅ Security scan done"

sec-pentest: ## 2FA penetration tests — mirrors security-pentest.yml
	@echo "🔵 Security pentest (2FA)..."
	cd $(API) && dotnet test $(API_TESTS) --filter "FullyQualifiedName~TwoFactorSecurityPenetrationTests" --nologo -v q
	@echo "✅ Security pentest passed"

security-audit: ## External port-scan against staging (default) — verifies only port 22 is publicly reachable. Override with SECURITY_AUDIT_TARGET=ip
	@echo "🔵 Security audit (external port scan)..."
	bash scripts/security-audit.sh
	@echo "✅ Security audit passed"

test-e2e: ## Playwright E2E tests (requires make dev + api + web running) — mirrors test-e2e.yml
	@echo "🔵 E2E Playwright tests..."
	cd $(WEB) && pnpm test:e2e
	@echo "✅ E2E tests passed"

test-visual: ## Visual regression tests — mirrors test-visual.yml
	@echo "🔵 Visual regression tests..."
	cd $(WEB) && pnpm build && pnpm test:e2e:visual --project=desktop-chrome
	@echo "✅ Visual tests passed"

test-perf: ## K6 smoke performance tests (requires api running) — mirrors test-performance.yml
	@echo "🔵 Performance smoke tests..."
	@which k6 > /dev/null 2>&1 || (echo "❌ k6 not installed: https://k6.io/docs/get-started/installation/" && exit 1)
	k6 run $(ROOT)/tests/k6/scenarios/deploy-smoke.js --env TEST_TYPE=smoke
	@echo "✅ Performance smoke passed"

validate-workflows: ## Validate GitHub Actions workflows — mirrors auto-validate.yml
	@echo "🔵 Validating workflows..."
	node $(ROOT)/scripts/validate-workflows.js
	@echo "✅ Workflows valid"

# === Game entity reset (issue #1320) ===
# Scripts live in infra/scripts/game-reset/

game-reset-help: ## Show game-reset workflow help
	@echo "Game entity reset (#1320) — usage:"
	@echo "  make game-reset-backup ENV=dev              # pg_dump + vector count baseline"
	@echo "  make game-reset-mapping ENV=dev             # export bridge CSV (requires backup first)"
	@echo "  make game-reset-rollback-rehearse           # disposable docker test of rollback"
	@echo ""
	@echo "For prod, pass IMEANIT=--i-mean-it (production safety guard):"
	@echo "  make game-reset-backup ENV=prod IMEANIT=--i-mean-it"
	@echo ""
	@echo "Per env, create infra/scripts/game-reset/.env.<ENV> from .env.example."

game-reset-backup: ## Pre-flight DB backup (ENV=dev|staging|prod)
	@test -n "$(ENV)" || (echo "ENV required: make game-reset-backup ENV=dev" && exit 64)
	@test -f scripts/game-reset/.env.$(ENV) || (echo "Missing scripts/game-reset/.env.$(ENV) — copy from .env.example" && exit 66)
	./scripts/game-reset/01-backup.sh scripts/game-reset/.env.$(ENV) $(IMEANIT)

game-reset-mapping: ## Export mapping CSV (ENV=dev|staging|prod, requires backup first)
	@test -n "$(ENV)" || (echo "ENV required: make game-reset-mapping ENV=dev" && exit 64)
	@test -f scripts/game-reset/.env.$(ENV) || (echo "Missing scripts/game-reset/.env.$(ENV) — copy from .env.example" && exit 66)
	./scripts/game-reset/02-export-mapping.sh scripts/game-reset/.env.$(ENV) $(IMEANIT)

game-reset-rollback-rehearse: ## Run disposable docker rollback rehearsal
	../tests/scripts/game-reset/rehearse-rollback.sh

.PHONY: dev dev-core dev-down \
        tunnel tunnel-stop tunnel-status integration-check integration \
        integration-down work work-stop smoke smoke-staging smoke-pg-readback staging staging-minimal staging-with-tutor staging-tutor-down staging-core staging-down prod prod-down \
        alpha alpha-core alpha-down \
        secrets-setup secrets-sync \
        snapshot restore-staging restore-local seed-games \
        logs logging logging-down logs-ui ps build clean help \
        ci ci-fe ci-be-unit ci-be-integration ci-be-e2e ci-all \
        sec-scan sec-pentest security-audit test-e2e test-visual test-perf validate-workflows \
        backup backup-verify backup-restore-test backup-status backup-cron-install backup-cron-show \
        db-snapshot db-snapshot-sanitized db-reset-migrations db-restore-snapshot db-rollback-safetynet \
        game-reset-help game-reset-backup game-reset-mapping game-reset-rollback-rehearse

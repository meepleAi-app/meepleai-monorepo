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

integration: ## Start local api+web with remote services (run 'make tunnel' first)
	@echo "Ensure tunnels are open: make tunnel"
	$(COMPOSE) -f compose.integration.yml up api web -d

integration-down: ## Stop integration
	$(COMPOSE) -f compose.integration.yml down

staging: ## Deploy staging on server (meepleai.app)
	$(COMPOSE) -f compose.staging.yml -f compose.traefik.yml \
		--profile ai --profile monitoring --profile proxy up -d

staging-down: ## Stop staging
	$(COMPOSE) -f compose.staging.yml -f compose.traefik.yml \
		--profile ai --profile monitoring --profile proxy down

prod: ## Deploy production (requires PROD_DOMAIN env var)
	@test -n "$(PROD_DOMAIN)" || (echo "Error: PROD_DOMAIN is required" && exit 1)
	$(COMPOSE) -f compose.prod.yml -f compose.traefik.yml \
		--profile ai --profile monitoring --profile proxy up -d

prod-down: ## Stop production
	$(COMPOSE) -f compose.prod.yml -f compose.traefik.yml \
		--profile ai --profile monitoring --profile proxy down

# === Secrets ===

secrets-dev: ## Generate dev secrets (auto-generated)
	pwsh -File secrets/setup-secrets.ps1 -Environment dev -SaveGenerated

secrets-integration: ## Setup integration secrets
	pwsh -File secrets/setup-secrets.ps1 -Environment integration

secrets-staging: ## Setup staging secrets
	pwsh -File secrets/setup-secrets.ps1 -Environment staging

secrets-prod: ## Setup production secrets
	pwsh -File secrets/setup-secrets.ps1 -Environment prod

# === Database Seeds ===

snapshot: ## Snapshot staging DB to infra/seeds/ (requires SSH access)
	bash scripts/snapshot-staging.sh

restore-staging: ## Restore staging DB from latest snapshot
	bash scripts/restore-staging.sh

restore-local: ## Restore local dev DB from latest snapshot (requires make dev running)
	bash scripts/restore-local.sh

seed-games: ## Seed test games on staging via API (requires SSH access)
	bash scripts/seed-all-games-staging.sh

# === Utilities ===

logs: ## Tail logs for a service (usage: make logs s=api)
	$(COMPOSE) logs -f $(s)

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

.PHONY: dev dev-core dev-down tunnel tunnel-stop tunnel-status integration \
        integration-down staging staging-down prod prod-down \
        secrets-dev secrets-integration secrets-staging secrets-prod \
        snapshot restore-staging restore-local seed-games \
        logs ps build clean help

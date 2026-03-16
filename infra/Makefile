.DEFAULT_GOAL := help
COMPOSE := docker compose -f docker-compose.yml

# === Environments ===

dev: ## Start full local development (all profiles)
	$(COMPOSE) -f compose.dev.yml \
		--profile ai --profile monitoring --profile automation \
		--profile proxy --profile storage up -d

dev-core: ## Start core only (postgres, redis, qdrant, api, web)
	$(COMPOSE) -f compose.dev.yml up -d

dev-down: ## Stop dev environment
	$(COMPOSE) -f compose.dev.yml --profile ai --profile monitoring \
		--profile automation --profile proxy --profile storage down

tunnel: ## Open SSH tunnels to staging server
	pwsh -File scripts/ssh-tunnel.ps1 -Action start

tunnel-stop: ## Close SSH tunnels
	pwsh -File scripts/ssh-tunnel.ps1 -Action stop

tunnel-status: ## Check SSH tunnel status
	pwsh -File scripts/ssh-tunnel.ps1 -Action status

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
        logs ps build clean help

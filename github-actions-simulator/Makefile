.PHONY: help build up down restart logs shell health validate clean test-ci test-api test-web test-security

# Colors for output
CYAN := \033[0;36m
GREEN := \033[0;32m
YELLOW := \033[0;33m
RED := \033[0;31m
NC := \033[0m # No Color

help: ## Show this help message
	@echo "$(CYAN)GitHub Actions Simulator - Commands$(NC)"
	@echo "======================================"
	@echo ""
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | awk 'BEGIN {FS = ":.*?## "}; {printf "$(GREEN)%-20s$(NC) %s\n", $$1, $$2}'
	@echo ""

build: ## Build the Docker environment
	@echo "$(CYAN)Building Docker environment...$(NC)"
	docker compose build

up: ## Start all services
	@echo "$(CYAN)Starting services...$(NC)"
	docker compose up -d
	@echo "$(GREEN)✅ Services started!$(NC)"
	@echo "$(YELLOW)Log Viewer:$(NC) http://localhost:9999"

down: ## Stop all services
	@echo "$(CYAN)Stopping services...$(NC)"
	docker compose down
	@echo "$(GREEN)✅ Services stopped!$(NC)"

restart: down up ## Restart all services

logs: ## View logs from all services
	docker compose logs -f

shell: ## Open shell in act-runner container
	@echo "$(CYAN)Opening shell in act-runner...$(NC)"
	docker compose exec act-runner bash

health: ## Check environment health
	@echo "$(CYAN)Checking environment health...$(NC)"
	docker compose exec act-runner health-check.sh

validate: ## Validate all GitHub Actions workflows
	@echo "$(CYAN)Validating workflows...$(NC)"
	docker compose exec act-runner validate-workflows.sh

clean: ## Clean old logs and artifacts (7 days)
	@echo "$(CYAN)Cleaning old files...$(NC)"
	docker compose exec act-runner cleanup.sh 7
	@echo "$(GREEN)✅ Cleanup complete!$(NC)"

clean-all: ## Remove all logs, artifacts, and volumes
	@echo "$(RED)⚠️  This will delete ALL logs, artifacts, and data!$(NC)"
	@read -p "Are you sure? [y/N] " -n 1 -r; \
	echo; \
	if [[ $$REPLY =~ ^[Yy]$$ ]]; then \
		docker compose down -v; \
		rm -rf logs/* artifacts/* cache/* .act/*; \
		echo "$(GREEN)✅ All data removed!$(NC)"; \
	fi

# Testing shortcuts
test-ci: up ## Run full CI workflow
	@echo "$(CYAN)Running CI workflow...$(NC)"
	docker compose exec act-runner run-workflow.sh .github/workflows/ci.yml push

test-api: up ## Run API tests only
	@echo "$(CYAN)Running API tests...$(NC)"
	docker compose exec act-runner run-job.sh .github/workflows/ci.yml ci-api push

test-web: up ## Run Web tests only
	@echo "$(CYAN)Running Web tests...$(NC)"
	docker compose exec act-runner run-job.sh .github/workflows/ci.yml ci-web push

test-security: up ## Run security scan
	@echo "$(CYAN)Running security scan...$(NC)"
	docker compose exec act-runner run-workflow.sh .github/workflows/security-scan.yml push

test-semgrep: up ## Run Semgrep SAST
	@echo "$(CYAN)Running Semgrep scan...$(NC)"
	docker compose exec act-runner run-workflow.sh .github/workflows/semgrep.yml push

# Viewing results
view-logs: ## View latest logs (interactive)
	docker compose exec act-runner view-logs.sh

view-logs-ci: ## View CI logs
	docker compose exec act-runner view-logs.sh ci

view-logs-api: ## View API test logs
	docker compose exec act-runner view-logs.sh ci-api

view-logs-web: ## View Web test logs
	docker compose exec act-runner view-logs.sh ci-web

# Maintenance
reset: ## Full reset (rebuild and restart)
	@echo "$(RED)⚠️  Full reset: rebuilding and restarting...$(NC)"
	docker compose down -v
	docker compose build --no-cache
	docker compose up -d
	@echo "$(GREEN)✅ Reset complete!$(NC)"

ps: ## Show running containers
	docker compose ps

stats: ## Show container stats
	docker stats --no-stream

# Setup
setup: ## Initial setup (copy secrets template)
	@if [ ! -f config/.secrets ]; then \
		cp config/.secrets.example config/.secrets; \
		echo "$(YELLOW)⚠️  Please edit config/.secrets with your actual secrets$(NC)"; \
	else \
		echo "$(GREEN)✅ config/.secrets already exists$(NC)"; \
	fi
	@echo "$(CYAN)Building environment...$(NC)"
	@make build
	@make up
	@echo ""
	@echo "$(GREEN)✅ Setup complete!$(NC)"
	@echo "$(YELLOW)Next steps:$(NC)"
	@echo "  1. Edit $(CYAN)config/.secrets$(NC) with your API keys"
	@echo "  2. Run $(CYAN)make health$(NC) to verify installation"
	@echo "  3. Run $(CYAN)make test-ci$(NC) to test workflows"
	@echo "  4. Open $(CYAN)http://localhost:9999$(NC) for log viewer"

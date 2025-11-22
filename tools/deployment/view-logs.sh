#!/bin/bash
# View Logs from Staging/Production
# Streams logs from deployed services

set -e

ENVIRONMENT="${1:-staging}"
SERVICE="${2:-all}"
LINES="${3:-100}"

# Colors
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${CYAN}📋 Viewing Logs - ${ENVIRONMENT} (${SERVICE})${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

case "$ENVIRONMENT" in
    staging)
        HOST="staging.meepleai.dev"
        ;;
    production)
        HOST="meepleai.dev"
        ;;
    *)
        echo "Unknown environment: ${ENVIRONMENT}"
        exit 1
        ;;
esac

# View logs based on service
case "$SERVICE" in
    api)
        echo -e "${YELLOW}API logs (last ${LINES} lines, then follow):${NC}"
        # Adjust based on your deployment (docker, k8s, etc.)
        # Example for Docker:
        # ssh ${HOST} "docker logs -f --tail=${LINES} meepleai-api"

        # Example for Kubernetes:
        # kubectl logs -f --tail=${LINES} -l app=meepleai-api -n ${ENVIRONMENT}

        echo "Command: ssh ${HOST} \"docker logs -f --tail=${LINES} meepleai-api\""
        ;;

    web)
        echo -e "${YELLOW}Web logs (last ${LINES} lines, then follow):${NC}"
        echo "Command: ssh ${HOST} \"docker logs -f --tail=${LINES} meepleai-web\""
        ;;

    postgres)
        echo -e "${YELLOW}PostgreSQL logs (last ${LINES} lines, then follow):${NC}"
        echo "Command: ssh ${HOST} \"docker logs -f --tail=${LINES} meepleai-postgres\""
        ;;

    all)
        echo -e "${YELLOW}All services logs (last ${LINES} lines, then follow):${NC}"
        echo "Command: ssh ${HOST} \"docker-compose logs -f --tail=${LINES}\""
        ;;

    *)
        echo "Unknown service: ${SERVICE}"
        echo "Available: api, web, postgres, all"
        exit 1
        ;;
esac

echo ""
echo -e "${YELLOW}Note: Adjust commands based on your infrastructure${NC}"
echo ""
echo "Usage examples:"
echo "  bash tools/deployment/view-logs.sh staging api"
echo "  bash tools/deployment/view-logs.sh production web 200"
echo "  bash tools/deployment/view-logs.sh production all"

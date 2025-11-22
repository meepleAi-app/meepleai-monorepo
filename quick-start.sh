#!/bin/bash
# Quick Start Script - Fast setup without tests
# For full options, use: tools/setup/setup-test-environment.sh --help

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Colors
GREEN='\033[0;32m'
CYAN='\033[0;36m'
NC='\033[0m'

echo -e "${CYAN}🚀 MeepleAI Quick Start${NC}"
echo ""

# Run the full setup script with skip-tests flag
"$SCRIPT_DIR/tools/setup/setup-test-environment.sh" --skip-tests "$@"

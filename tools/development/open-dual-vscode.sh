#!/bin/bash
# Open Dual VS Code Instances for Backend + Frontend Development
# Usage: bash tools/open-dual-vscode.sh [--backend-only|--frontend-only]

BACKEND_PATH="D:/Repositories/meepleai-monorepo-backend"
FRONTEND_PATH="D:/Repositories/meepleai-monorepo-frontend"

BACKEND_ONLY=false
FRONTEND_ONLY=false

# Parse arguments
for arg in "$@"; do
    case $arg in
        --backend-only)
            BACKEND_ONLY=true
            shift
            ;;
        --frontend-only)
            FRONTEND_ONLY=true
            shift
            ;;
    esac
done

echo "🚀 Opening VS Code instances for parallel development..."
echo ""

# Verify worktrees exist
if [ ! -d "$BACKEND_PATH" ] && [ "$FRONTEND_ONLY" = false ]; then
    echo "❌ Backend worktree not found at: $BACKEND_PATH"
    echo "   Run: git worktree add $BACKEND_PATH -b backend-dev"
    exit 1
fi

if [ ! -d "$FRONTEND_PATH" ] && [ "$BACKEND_ONLY" = false ]; then
    echo "❌ Frontend worktree not found at: $FRONTEND_PATH"
    echo "   Run: git worktree add $FRONTEND_PATH -b frontend-dev"
    exit 1
fi

# Open Backend VS Code
if [ "$FRONTEND_ONLY" = false ]; then
    echo "📂 Opening Backend VS Code..."
    echo "   Location: $BACKEND_PATH"
    code "$BACKEND_PATH"
    sleep 2
fi

# Open Frontend VS Code
if [ "$BACKEND_ONLY" = false ]; then
    echo "🎨 Opening Frontend VS Code..."
    echo "   Location: $FRONTEND_PATH"
    code "$FRONTEND_PATH"
    sleep 1
fi

echo ""
echo "✅ VS Code instances opened!"
echo ""
echo "Next steps:"
echo "  Backend  → cd apps/api && dotnet run"
echo "  Frontend → cd apps/web && pnpm dev"
echo ""
echo "Documentation: claudedocs/DUAL-VSCODE-SETUP.md"

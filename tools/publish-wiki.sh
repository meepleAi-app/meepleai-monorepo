#!/bin/bash
# publish-wiki.sh - Pubblica la wiki GitHub da .wiki/

set -e

# Colors
GREEN='\033[0;32m'
CYAN='\033[0;36m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${CYAN}📖 MeepleAI Wiki Publisher${NC}"
echo ""

# Configuration
WIKI_URL="https://github.com/DegrassiAaron/meepleai-monorepo.wiki.git"
WIKI_DIR="$HOME/meepleai-wiki-temp"
SOURCE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/.wiki"

# Verify source directory exists
if [ ! -d "$SOURCE_DIR" ]; then
    echo -e "${RED}❌ Error: .wiki/ directory not found at $SOURCE_DIR${NC}"
    exit 1
fi

# Count files
FILE_COUNT=$(find "$SOURCE_DIR" -maxdepth 1 -name "*.md" | wc -l)
echo -e "${CYAN}Found $FILE_COUNT markdown files in .wiki/${NC}"
echo ""

# Ask for confirmation
echo -e "${YELLOW}This will:${NC}"
echo "  1. Clone the GitHub wiki repository to: $WIKI_DIR"
echo "  2. Copy and rename 9 files from .wiki/"
echo "  3. Commit and push to GitHub wiki"
echo ""
read -p "Continue? (y/N): " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo -e "${YELLOW}Aborted.${NC}"
    exit 0
fi

# Clean up existing directory if it exists
if [ -d "$WIKI_DIR" ]; then
    echo -e "${YELLOW}Removing existing wiki directory...${NC}"
    rm -rf "$WIKI_DIR"
fi

# Clone wiki repository
echo -e "${CYAN}Cloning wiki repository...${NC}"
git clone "$WIKI_URL" "$WIKI_DIR"
cd "$WIKI_DIR"

# Copy and rename files
echo -e "${CYAN}Copying and renaming files...${NC}"

# Home page (special name)
cp "$SOURCE_DIR/00-home.md" "Home.md"
echo -e "${GREEN}✓${NC} Home.md (from 00-home.md)"

# Role-based guides
cp "$SOURCE_DIR/01-user-guide.md" "User-Guide.md"
echo -e "${GREEN}✓${NC} User-Guide.md"

cp "$SOURCE_DIR/02-developer-guide.md" "Developer-Guide.md"
echo -e "${GREEN}✓${NC} Developer-Guide.md"

cp "$SOURCE_DIR/03-testing-guide.md" "Testing-Guide.md"
echo -e "${GREEN}✓${NC} Testing-Guide.md"

cp "$SOURCE_DIR/04-deployment-guide.md" "Deployment-Guide.md"
echo -e "${GREEN}✓${NC} Deployment-Guide.md"

cp "$SOURCE_DIR/05-administrator-guide.md" "Administrator-Guide.md"
echo -e "${GREEN}✓${NC} Administrator-Guide.md"

cp "$SOURCE_DIR/06-architecture-guide.md" "Architecture-Guide.md"
echo -e "${GREEN}✓${NC} Architecture-Guide.md"

cp "$SOURCE_DIR/07-contributing-guide.md" "Contributing-Guide.md"
echo -e "${GREEN}✓${NC} Contributing-Guide.md"

# Sidebar (special name)
cp "$SOURCE_DIR/README.md" "_Sidebar.md"
echo -e "${GREEN}✓${NC} _Sidebar.md (from README.md)"

echo ""

# Check for changes
if [ -z "$(git status --porcelain)" ]; then
    echo -e "${YELLOW}No changes detected. Wiki is already up to date.${NC}"
    cd - > /dev/null
    rm -rf "$WIKI_DIR"
    exit 0
fi

# Show what will be committed
echo -e "${CYAN}Changes to commit:${NC}"
git status --short
echo ""

# Commit changes
echo -e "${CYAN}Committing changes...${NC}"
git add .
git commit -m "Update wiki from .wiki/ directory

Role-based guides:
- Home page with project overview
- User Guide - Using MeepleAI
- Developer Guide - Development workflow
- Testing Guide - Testing procedures
- Deployment Guide - Deployment and CI/CD
- Administrator Guide - System maintenance
- Architecture Guide - Technical deep dive
- Contributing Guide - How to contribute
- Sidebar navigation

Last updated: $(date +%Y-%m-%d)
Source: .wiki/ directory in main repository"

# Push to GitHub
echo -e "${CYAN}Pushing to GitHub wiki...${NC}"
git push origin master

echo ""
echo -e "${GREEN}✅ Wiki published successfully!${NC}"
echo ""
echo -e "${CYAN}View your wiki at:${NC}"
echo "https://github.com/DegrassiAaron/meepleai-monorepo/wiki"
echo ""

# Cleanup
cd - > /dev/null
rm -rf "$WIKI_DIR"
echo -e "${GREEN}Cleaned up temporary directory.${NC}"

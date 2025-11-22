#!/bin/bash
# MeepleAI Cache Cleanup Script
# Automatically cleans cache directories to free disk space

set -e
set -o pipefail

# Configuration
DRY_RUN=false
VERBOSE=false
SKIP_BUILD=false
SKIP_CONFIRMATION=false

# Colors
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --verbose|-v)
            VERBOSE=true
            shift
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --yes|-y)
            SKIP_CONFIRMATION=true
            shift
            ;;
        --help|-h)
            echo "MeepleAI Cache Cleanup Script"
            echo ""
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --dry-run           Show what would be deleted without actually deleting"
            echo "  --verbose, -v       Show detailed output"
            echo "  --skip-build        Skip build artifacts cleanup (.NET obj/bin, Next.js .next)"
            echo "  --yes, -y           Skip confirmation prompt"
            echo "  --help, -h          Show this help message"
            echo ""
            echo "Examples:"
            echo "  $0 --dry-run        # Preview cleanup"
            echo "  $0 --verbose        # Run with detailed output"
            echo "  $0 --skip-build     # Clean only cache directories"
            echo "  $0 --yes            # Run without confirmation"
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Function to calculate directory size
get_size() {
    local dir=$1
    if [ -d "$dir" ]; then
        du -sh "$dir" 2>/dev/null | cut -f1
    else
        echo "0"
    fi
}

# Function to calculate total directory size in bytes
get_size_bytes() {
    local dir=$1
    if [ -d "$dir" ]; then
        du -sb "$dir" 2>/dev/null | cut -f1
    else
        echo "0"
    fi
}

# Function to clean directory
clean_directory() {
    local dir=$1
    local description=$2

    if [ -d "$dir" ]; then
        local size=$(get_size "$dir")

        if [ "$VERBOSE" = true ]; then
            echo -e "${CYAN}Found: $dir ($size)${NC}"
        fi

        if [ "$DRY_RUN" = true ]; then
            echo -e "${YELLOW}[DRY RUN] Would delete: $dir ($size) - $description${NC}"
        else
            echo -e "${RED}Deleting: $dir ($size) - $description${NC}"
            rm -rf "$dir"
        fi

        return 0
    else
        if [ "$VERBOSE" = true ]; then
            echo -e "${GREEN}Not found: $dir (skipping)${NC}"
        fi
        return 0  # Return 0 to prevent set -e from exiting when directory doesn't exist
    fi
}

# Function to find and clean build artifacts
clean_build_artifacts() {
    local base_dir=$1
    local pattern=$2
    local description=$3
    local count=0

    if [ -d "$base_dir" ]; then
        while IFS= read -r dir; do
            if [ -n "$dir" ] && [ -d "$dir" ]; then
                clean_directory "$dir" "$description"
                ((count++))
            fi
        done < <(find "$base_dir" -type d -name "$pattern" 2>/dev/null)

        if [ "$VERBOSE" = true ] && [ $count -eq 0 ]; then
            echo -e "${GREEN}No $pattern directories found in $base_dir${NC}"
        fi
    fi
}

# Main script
echo -e "${BLUE}╔════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║   MeepleAI Cache Cleanup                   ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════╝${NC}"
echo ""

if [ "$DRY_RUN" = true ]; then
    echo -e "${YELLOW}⚠️  DRY RUN MODE - No files will be deleted${NC}"
    echo ""
fi

# Calculate sizes before cleanup
echo -e "${CYAN}📊 Calculating current sizes...${NC}"
BEFORE_SIZE=$(du -sb . 2>/dev/null | cut -f1)

# Track what we're going to clean
TARGETS=()

# Check cache directories
if [ -d ".serena" ]; then
    TARGETS+=(".serena")
fi

if [ -d "codeql-db" ]; then
    TARGETS+=("codeql-db")
fi

if [ -d ".playwright-mcp" ]; then
    TARGETS+=(".playwright-mcp")
fi

# Check build artifacts if not skipped
if [ "$SKIP_BUILD" = false ]; then
    if [ -d "apps/api" ]; then
        OBJ_COUNT=$(find apps/api -type d -name "obj" 2>/dev/null | wc -l)
        BIN_COUNT=$(find apps/api -type d -name "bin" 2>/dev/null | wc -l)
        if [ $OBJ_COUNT -gt 0 ] || [ $BIN_COUNT -gt 0 ]; then
            TARGETS+=("apps/api build artifacts")
        fi
    fi

    if [ -d "apps/web/.next" ]; then
        TARGETS+=("apps/web/.next")
    fi
fi

# Show what will be cleaned
if [ ${#TARGETS[@]} -gt 0 ]; then
    echo -e "${CYAN}📁 Target directories:${NC}"
    for target in "${TARGETS[@]}"; do
        echo "  - $target"
    done
    echo ""
else
    echo -e "${GREEN}✅ No cache directories found. Nothing to clean!${NC}"
    exit 0
fi

# Ask for confirmation unless --yes flag is set or dry-run mode
if [ "$DRY_RUN" = false ] && [ "$SKIP_CONFIRMATION" = false ]; then
    echo -e "${YELLOW}⚠️  This will permanently delete the directories above.${NC}"
    read -p "Continue? (y/N): " -n 1 -r
    echo ""
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo -e "${YELLOW}Cleanup cancelled.${NC}"
        exit 0
    fi
    echo ""
fi

# Clean cache directories
echo -e "${CYAN}🧹 Cleaning cache directories...${NC}"
echo ""

clean_directory ".serena" "Serena MCP cache"
clean_directory "codeql-db" "CodeQL database cache"
clean_directory ".playwright-mcp" "Playwright MCP cache"

# Clean build artifacts if not skipped
if [ "$SKIP_BUILD" = false ]; then
    echo ""
    echo -e "${CYAN}🏗️  Cleaning build artifacts...${NC}"
    echo ""

    clean_build_artifacts "apps/api" "obj" ".NET obj directory"
    clean_build_artifacts "apps/api" "bin" ".NET bin directory"
    clean_directory "apps/web/.next" "Next.js build cache"
fi

# Calculate sizes after cleanup
echo ""
echo -e "${CYAN}📊 Calculating final sizes...${NC}"
AFTER_SIZE=$(du -sb . 2>/dev/null | cut -f1)

# Calculate space freed
FREED_BYTES=$((BEFORE_SIZE - AFTER_SIZE))
FREED_MB=$((FREED_BYTES / 1024 / 1024))
FREED_GB=$(echo "scale=2; $FREED_BYTES / 1024 / 1024 / 1024" | bc)

# Summary
echo ""
echo -e "${BLUE}╔════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║   Cleanup Summary                          ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════╝${NC}"
echo ""

if [ "$DRY_RUN" = true ]; then
    echo -e "${YELLOW}✓ Dry run completed. No files were deleted.${NC}"
    echo -e "${YELLOW}  Estimated space that would be freed: ${FREED_MB} MB (${FREED_GB} GB)${NC}"
else
    echo -e "${GREEN}✅ Cleanup complete!${NC}"
    echo -e "${GREEN}  Space freed: ${FREED_MB} MB (${FREED_GB} GB)${NC}"
fi

echo ""

# Show recommendations
if [ "$SKIP_BUILD" = true ]; then
    echo -e "${CYAN}💡 Tip: Run without --skip-build to also clean build artifacts${NC}"
fi

if [ "$DRY_RUN" = true ]; then
    echo -e "${CYAN}💡 Tip: Run without --dry-run to actually perform the cleanup${NC}"
fi

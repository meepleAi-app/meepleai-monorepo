#!/bin/bash

# Visual Testing Demo Script
# Helps run Playwright UI mode with common scenarios

set -e

BLUE='\033[0;34m'
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}   Playwright Visual Testing - Demo Launcher${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo

# Check if backend is running
echo -e "${YELLOW}📡 Checking if backend API is running...${NC}"
if curl -s http://localhost:8080/health > /dev/null 2>&1; then
  echo -e "${GREEN}✅ Backend is running${NC}"
else
  echo -e "${RED}❌ Backend is NOT running${NC}"
  echo -e "${YELLOW}Please start the backend first:${NC}"
  echo -e "   cd apps/api/src/Api && dotnet run"
  echo
  read -p "Continue anyway? (y/N) " -n 1 -r
  echo
  if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    exit 1
  fi
fi

echo
echo -e "${BLUE}Choose a test scenario:${NC}"
echo
echo -e "  ${GREEN}1)${NC} 🎯 Mock Auth Test (Working) - comments-enhanced.spec.ts"
echo -e "     ${YELLOW}Shows how mock auth bypasses portal issues${NC}"
echo
echo -e "  ${GREEN}2)${NC} ❌ Real Login Test (Failing) - demo-user-login.spec.ts"
echo -e "     ${YELLOW}Demonstrates the nextjs-portal click blocking issue${NC}"
echo
echo -e "  ${GREEN}3)${NC} 📊 Admin Tests (Working) - admin-analytics.spec.ts"
echo -e "     ${YELLOW}Admin role functionality with mock auth${NC}"
echo
echo -e "  ${GREEN}4)${NC} 💬 Chat Streaming (Working) - chat-streaming.spec.ts"
echo -e "     ${YELLOW}User role functionality with mock auth${NC}"
echo
echo -e "  ${GREEN}5)${NC} 🔍 All E2E Tests - Open UI mode browser"
echo -e "     ${YELLOW}Choose tests from the UI${NC}"
echo
echo -e "  ${GREEN}6)${NC} 🐛 Debug Mode - demo-user-login with pause"
echo -e "     ${YELLOW}Runs test in debug mode with manual inspection${NC}"
echo
echo -e "  ${GREEN}7)${NC} 👁️  Headed Mode - demo-user-login in visible browser"
echo -e "     ${YELLOW}See if portal issue exists in non-headless mode${NC}"
echo

read -p "Enter choice (1-7): " choice

echo

case $choice in
  1)
    echo -e "${GREEN}🎯 Running Mock Auth Test (Working Example)${NC}"
    echo -e "${YELLOW}Watch how the test bypasses login completely${NC}"
    echo
    pnpm test:e2e:ui comments-enhanced.spec.ts
    ;;
  2)
    echo -e "${RED}❌ Running Real Login Test (Failing Example)${NC}"
    echo -e "${YELLOW}Watch for the 'nextjs-portal intercepts pointer events' error${NC}"
    echo
    pnpm test:e2e:ui demo-user-login.spec.ts
    ;;
  3)
    echo -e "${GREEN}📊 Running Admin Analytics Test${NC}"
    echo -e "${YELLOW}Admin role with mock auth - fast and reliable${NC}"
    echo
    pnpm test:e2e:ui admin-analytics.spec.ts
    ;;
  4)
    echo -e "${GREEN}💬 Running Chat Streaming Test${NC}"
    echo -e "${YELLOW}User role with mock auth - tests chat functionality${NC}"
    echo
    pnpm test:e2e:ui chat-streaming.spec.ts
    ;;
  5)
    echo -e "${GREEN}🔍 Opening Playwright UI Mode${NC}"
    echo -e "${YELLOW}Select any test from the UI interface${NC}"
    echo
    pnpm test:e2e:ui
    ;;
  6)
    echo -e "${GREEN}🐛 Running in Debug Mode${NC}"
    echo -e "${YELLOW}Test will pause - use Playwright Inspector to investigate${NC}"
    echo -e "${YELLOW}Click 'Resume' to continue or step through actions${NC}"
    echo
    pnpm playwright test demo-user-login.spec.ts --debug
    ;;
  7)
    echo -e "${GREEN}👁️  Running in Headed Mode (Visible Browser)${NC}"
    echo -e "${YELLOW}Watch the actual browser window - portal might behave differently${NC}"
    echo
    pnpm playwright test demo-user-login.spec.ts --headed --project=chromium
    ;;
  *)
    echo -e "${RED}Invalid choice${NC}"
    exit 1
    ;;
esac

echo
echo -e "${GREEN}═══════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}   Test complete! Check the output above.${NC}"
echo -e "${GREEN}═══════════════════════════════════════════════════════${NC}"
echo
echo -e "${BLUE}💡 Tips:${NC}"
echo -e "  • In UI mode, click on actions to see DOM snapshots"
echo -e "  • Use the timeline to replay test step-by-step"
echo -e "  • Compare working (mock auth) vs failing (real login) tests"
echo -e "  • Screenshots are in: test-results/"
echo
echo -e "${BLUE}📚 Full guide:${NC} apps/web/e2e/PLAYWRIGHT-UI-MODE-GUIDE.md"
echo

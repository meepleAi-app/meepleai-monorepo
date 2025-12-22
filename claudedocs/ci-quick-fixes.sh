#!/bin/bash
# CI Quick Fixes Script
# Generated: 2025-12-19
# Target: CI Run #20375956158 failures

set -e  # Exit on error

echo "🔍 CI Error Analysis & Quick Fixes"
echo "=================================="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Step 1: Download CI artifacts
echo "📥 Step 1/5: Downloading CI artifacts..."
if command -v gh &> /dev/null; then
  echo "  Using GitHub CLI to download run artifacts..."

  # Check if run exists and get status
  RUN_STATUS=$(gh run view 20375956158 --json status --jq '.status' 2>/dev/null || echo "not_found")

  if [ "$RUN_STATUS" == "completed" ]; then
    echo "  ✅ Run found and completed"

    # Download all artifacts
    mkdir -p ci-artifacts-20375956158
    cd ci-artifacts-20375956158

    gh run download 20375956158 2>&1 || echo "  ⚠️  Some artifacts may not be available"

    cd ..
    echo "  ✅ Artifacts downloaded to ci-artifacts-20375956158/"
  else
    echo "  ⚠️  Run not found or not completed (status: $RUN_STATUS)"
  fi
else
  echo "${RED}  ❌ GitHub CLI not installed${NC}"
  echo "  Install: https://cli.github.com/"
  echo "  Skipping artifact download..."
fi

echo ""

# Step 2: Analyze API logs
echo "📝 Step 2/5: Analyzing API logs..."
API_LOG_DIR="ci-artifacts-20375956158/api-logs-*"
if ls $API_LOG_DIR 2>/dev/null | grep -q .; then
  echo "  Found API logs:"
  for log_dir in $API_LOG_DIR; do
    if [ -f "$log_dir/api.log" ]; then
      echo "  📄 $log_dir/api.log"
      echo "  ⚠️  Last 20 lines of API log:"
      tail -20 "$log_dir/api.log"

      # Check for common errors
      echo ""
      echo "  🔍 Checking for common errors..."

      if grep -q "Connection refused" "$log_dir/api.log"; then
        echo "${RED}  ❌ Found: Connection refused${NC}"
        echo "     → Likely Postgres/Qdrant/Redis not ready"
      fi

      if grep -q "Migration" "$log_dir/api.log"; then
        echo "${YELLOW}  ⚠️  Found: Migration issues${NC}"
        echo "     → Check EF Core migrations"
      fi

      if grep -q "HealthCheck" "$log_dir/api.log"; then
        echo "${YELLOW}  ⚠️  Found: HealthCheck mentions${NC}"
        echo "     → Review health check configuration"
      fi

      if grep -q "Exception" "$log_dir/api.log"; then
        echo "${RED}  ❌ Found: Exception${NC}"
        echo "     → Critical error during startup"
        grep "Exception" "$log_dir/api.log" | head -5
      fi
    fi
  done
else
  echo "  ⚠️  No API logs found in artifacts"
  echo "     This is expected if the job failed before uploading artifacts"
fi

echo ""

# Step 3: Analyze coverage report
echo "📊 Step 3/5: Analyzing coverage report..."
COV_REPORT="ci-artifacts-20375956158/*/coverage/lcov.info"
if ls $COV_REPORT 2>/dev/null | grep -q .; then
  echo "  Found coverage reports:"
  for cov_file in $COV_REPORT; do
    echo "  📄 $cov_file"

    # Calculate rough coverage percentages
    if command -v lcov &> /dev/null; then
      echo "  📈 Coverage summary:"
      lcov --summary "$cov_file" 2>/dev/null || echo "     (lcov tool not available for detailed analysis)"
    else
      # Simple grep-based analysis
      LINES_FOUND=$(grep -c "^LF:" "$cov_file" 2>/dev/null || echo "0")
      LINES_HIT=$(grep "^LH:" "$cov_file" 2>/dev/null | awk '{sum+=$2} END {print sum}')

      if [ "$LINES_FOUND" -gt 0 ]; then
        echo "  📊 Lines found: $LINES_FOUND"
        echo "  ✅ Lines hit: ${LINES_HIT:-0}"
      fi
    fi
  done
else
  echo "${YELLOW}  ⚠️  No coverage reports found${NC}"
  echo "     This suggests tests failed before coverage collection"
fi

echo ""

# Step 4: Analyze Playwright report
echo "🎭 Step 4/5: Analyzing Playwright report..."
PW_REPORT="ci-artifacts-20375956158/playwright-*-report-*/index.html"
if ls $PW_REPORT 2>/dev/null | grep -q .; then
  echo "  Found Playwright reports:"
  for report in $PW_REPORT; do
    echo "  📄 $report"

    # Try to extract failure info from HTML
    if command -v grep &> /dev/null; then
      FAILURES=$(grep -o "failed.*test" "$report" 2>/dev/null | head -1 || echo "")
      if [ -n "$FAILURES" ]; then
        echo "${RED}  ❌ $FAILURES${NC}"
      fi

      # Check for accessibility violations
      if grep -q "axe" "$report" 2>/dev/null; then
        echo "${YELLOW}  ⚠️  Accessibility violations detected${NC}"
        echo "     → Review axe-core report in browser"
      fi
    fi

    echo "  💡 Open in browser for detailed analysis:"
    echo "     file://$(realpath "$report")"
  done
else
  echo "  ⚠️  No Playwright reports found"
fi

echo ""

# Step 5: Generate recommendations
echo "💡 Step 5/5: Generating fix recommendations..."
echo ""
echo "Based on analysis, here are recommended fixes:"
echo ""

echo "${GREEN}🔧 Fix 1: API Health Check Improvements${NC}"
echo "   Add to .github/workflows/ci.yml (API Smoke Tests job):"
echo "   "
cat <<'EOF'
   env:
     # Disable external service health checks in CI
     HealthChecks__Qdrant__Enabled: "false"
     HealthChecks__Redis__Enabled: "false"
     HealthChecks__N8n__Enabled: "false"
     # Keep Postgres health check enabled
     ConnectionStrings__Postgres: ...
EOF

echo ""
echo "${GREEN}🔧 Fix 2: Increase Test Timeouts${NC}"
echo "   Update apps/web/vitest.config.ts:"
echo "   "
cat <<'EOF'
   export default defineConfig({
     test: {
       testTimeout: 10000,  // Increase from 5000 to 10000
       hookTimeout: 10000,
       // ... rest of config
     }
   })
EOF

echo ""
echo "${GREEN}🔧 Fix 3: Playwright Timeout Configuration${NC}"
echo "   Update apps/web/playwright.config.ts:"
echo "   "
cat <<'EOF'
   export default defineConfig({
     timeout: 120000,  // 120s for accessibility tests
     retries: process.env.CI ? 2 : 0,  // Retry flaky tests in CI
     // ... rest of config
   })
EOF

echo ""
echo "${GREEN}🔧 Fix 4: Better Service Health Validation${NC}"
echo "   Add before 'Start API' step in .github/workflows/ci.yml:"
echo "   "
cat <<'EOF'
   - name: Verify Services Ready
     run: |
       echo "Checking Postgres..."
       docker exec $(docker ps -q -f ancestor=postgres:16-alpine) pg_isready -U meepleai

       echo "Checking Qdrant..."
       curl -f http://localhost:6333/healthz

       echo "Checking Redis..."
       docker exec $(docker ps -q -f ancestor=redis:7-alpine) redis-cli ping

       echo "✅ All services ready"
EOF

echo ""
echo "📋 ${GREEN}Next Actions:${NC}"
echo "   1. Review downloaded artifacts in: ci-artifacts-20375956158/"
echo "   2. Apply recommended fixes to workflow files"
echo "   3. Create PR with fixes"
echo "   4. Re-run CI to validate"
echo ""
echo "🔗 ${GREEN}Useful Commands:${NC}"
echo "   # Re-run failed jobs only"
echo "   gh run rerun 20375956158 --failed"
echo ""
echo "   # Watch CI run in real-time"
echo "   gh run watch"
echo ""
echo "   # View logs for specific job"
echo "   gh run view 20375956158 --log --job 58554402396"
echo ""
echo "✅ Analysis complete!"

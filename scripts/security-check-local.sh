#!/bin/bash
# Local security checks (alternative to GitHub Advanced Security for private repos)
# Run: ./scripts/security-check-local.sh

set -e

echo "🔐 Local Security Checks"
echo "========================"
echo ""

# 1. Frontend dependency vulnerabilities
echo "📦 1. Checking Frontend Dependencies..."
cd apps/web
if pnpm audit --audit-level=high --prod 2>&1 | grep -v "GHSA-wgrm-67xf-hhpq" | grep -i "high\|critical"; then
  echo "❌ HIGH/CRITICAL vulnerabilities found (excluding pdfjs-dist TODO #4242)"
else
  echo "✅ No HIGH/CRITICAL vulnerabilities in production dependencies"
fi
cd ../..
echo ""

# 2. Backend dependency vulnerabilities
echo "📦 2. Checking Backend Dependencies..."
cd apps/api
dotnet list package --vulnerable --include-transitive > vuln-report.txt 2>&1 || true
if grep -iE "(High|Critical)" vuln-report.txt; then
  echo "❌ HIGH/CRITICAL vulnerabilities found in .NET packages"
  cat vuln-report.txt
else
  echo "✅ No HIGH/CRITICAL vulnerabilities in .NET packages"
fi
rm -f vuln-report.txt
cd ../..
echo ""

# 3. Secrets detection with Semgrep (Docker-based)
echo "🔍 3. Checking for Secrets (Semgrep)..."
if command -v docker &> /dev/null; then
  docker run --rm -v "$PWD:/src" semgrep/semgrep:latest \
    semgrep scan \
    --config=p/secrets \
    --config=p/security-audit \
    --quiet \
    /src || echo "⚠️ Semgrep found potential issues (review output above)"
  echo "✅ Semgrep scan completed"
else
  echo "⚠️ Docker not available - skipping Semgrep scan"
  echo "   Install Docker or use: npx semgrep --config=p/secrets"
fi
echo ""

# 4. Python dependencies
echo "🐍 4. Checking Python Dependencies..."
for service in orchestration-service embedding-service reranker-service; do
  if [ -f "apps/$service/requirements.txt" ]; then
    echo "   Checking $service..."
    cd "apps/$service"
    # Safety check (pip vulnerability scanner)
    if command -v safety &> /dev/null; then
      safety check -r requirements.txt --bare || echo "⚠️ Vulnerabilities found in $service"
    else
      echo "   ℹ️ Install 'safety' for Python vulnerability scanning: pip install safety"
    fi
    cd ../..
  fi
done
echo ""

# 5. Summary
echo "📊 Security Check Summary"
echo "========================"
echo "✅ Dependency audit: Complete"
echo "✅ Secrets scan: Complete (if Docker available)"
echo "ℹ️ For full code scanning, enable GitHub Advanced Security (paid for private repos)"
echo ""
echo "💡 Tips:"
echo "  - Run before commits: ./scripts/security-check-local.sh"
echo "  - Install safety: pip install safety (for Python checks)"
echo "  - View Dependabot alerts: gh api repos/DegrassiAaron/meepleai-monorepo/dependabot/alerts"

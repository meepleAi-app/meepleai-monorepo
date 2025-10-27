#!/bin/bash
# health-check.sh - Check health of the simulator environment

echo "🏥 GitHub Actions Simulator - Health Check"
echo "==========================================="
echo ""

ALL_HEALTHY=true

# Check act
echo "🔧 Checking act..."
if act --version > /dev/null 2>&1; then
    VERSION=$(act --version)
    echo "   ✅ act: ${VERSION}"
else
    echo "   ❌ act: NOT FOUND"
    ALL_HEALTHY=false
fi

# Check .NET
echo "🔧 Checking .NET SDK..."
if dotnet --version > /dev/null 2>&1; then
    VERSION=$(dotnet --version)
    echo "   ✅ .NET SDK: ${VERSION}"
else
    echo "   ❌ .NET SDK: NOT FOUND"
    ALL_HEALTHY=false
fi

# Check Node.js
echo "🔧 Checking Node.js..."
if node --version > /dev/null 2>&1; then
    VERSION=$(node --version)
    echo "   ✅ Node.js: ${VERSION}"
else
    echo "   ❌ Node.js: NOT FOUND"
    ALL_HEALTHY=false
fi

# Check pnpm
echo "🔧 Checking pnpm..."
if pnpm --version > /dev/null 2>&1; then
    VERSION=$(pnpm --version)
    echo "   ✅ pnpm: ${VERSION}"
else
    echo "   ❌ pnpm: NOT FOUND"
    ALL_HEALTHY=false
fi

# Check k6
echo "🔧 Checking k6..."
if k6 version > /dev/null 2>&1; then
    VERSION=$(k6 version | head -1)
    echo "   ✅ k6: ${VERSION}"
else
    echo "   ❌ k6: NOT FOUND"
    ALL_HEALTHY=false
fi

# Check Semgrep
echo "🔧 Checking Semgrep..."
if semgrep --version > /dev/null 2>&1; then
    VERSION=$(semgrep --version)
    echo "   ✅ Semgrep: ${VERSION}"
else
    echo "   ❌ Semgrep: NOT FOUND"
    ALL_HEALTHY=false
fi

# Check actionlint
echo "🔧 Checking actionlint..."
if actionlint --version > /dev/null 2>&1; then
    VERSION=$(actionlint --version)
    echo "   ✅ actionlint: ${VERSION}"
else
    echo "   ❌ actionlint: NOT FOUND"
    ALL_HEALTHY=false
fi

# Check Docker
echo "🔧 Checking Docker..."
if docker --version > /dev/null 2>&1; then
    VERSION=$(docker --version)
    echo "   ✅ Docker: ${VERSION}"
else
    echo "   ❌ Docker: NOT FOUND"
    ALL_HEALTHY=false
fi

# Check PostgreSQL connection
echo ""
echo "🗄️  Checking Services..."
echo "🔧 Checking PostgreSQL..."
if pg_isready -h postgres -p 5432 -U meeple > /dev/null 2>&1; then
    echo "   ✅ PostgreSQL: Connected"
else
    echo "   ⚠️  PostgreSQL: Not connected (service may not be running)"
fi

# Check Redis
echo "🔧 Checking Redis..."
if redis-cli -h redis ping > /dev/null 2>&1; then
    echo "   ✅ Redis: Connected"
else
    echo "   ⚠️  Redis: Not connected (service may not be running)"
fi

# Check Qdrant
echo "🔧 Checking Qdrant..."
if curl -sf http://qdrant:6333/healthz > /dev/null 2>&1; then
    echo "   ✅ Qdrant: Connected"
else
    echo "   ⚠️  Qdrant: Not connected (service may not be running)"
fi

# Check directories
echo ""
echo "📁 Checking Directories..."
for dir in /logs /artifacts /cache /workspace/meepleai; do
    if [ -d "${dir}" ]; then
        echo "   ✅ ${dir}: exists"
    else
        echo "   ❌ ${dir}: MISSING"
        ALL_HEALTHY=false
    fi
done

# Summary
echo ""
echo "==========================================="
if [ "${ALL_HEALTHY}" = true ]; then
    echo "🟢 Environment is healthy!"
    exit 0
else
    echo "🔴 Some components are missing or unhealthy!"
    exit 1
fi

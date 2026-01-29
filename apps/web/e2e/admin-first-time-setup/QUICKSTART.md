# Quick Start - Admin First-Time Setup E2E Tests

Fast setup guide for running E2E tests locally.

## ⚡ 5-Minute Quick Start

### 1. Prerequisites
```bash
# Ensure services running
cd apps/api/src/Api && dotnet run &  # Backend (port 8080)
cd apps/web && pnpm dev &             # Frontend (port 3000)
```

### 2. Generate Test PDFs
```bash
cd apps/web

# Generate 3 critical PDFs (Pandemic, Wingspan, Azul)
pnpm test:generate-mock-pdfs

# Or all 9 PDFs
pnpm test:generate-mock-pdfs:all
```

### 3. Run Tests
```bash
# Full suite (3-5 minutes)
pnpm test:e2e:admin-setup

# Interactive UI mode (recommended for first run)
pnpm test:e2e:admin-setup:ui

# Debug mode (step-by-step)
pnpm test:e2e:admin-setup:debug
```

---

## 🎯 Test Execution Modes

### **Mode 1: Quick Smoke Test** (30 seconds)
Tests infrastructure and auth only.
```bash
pnpm test:e2e:admin-setup 01-infra 02-auth
```

### **Mode 2: Wizard Validation** (2 minutes)
Tests complete wizard flow.
```bash
pnpm test:e2e:admin-setup 03-admin-wizard
```

### **Mode 3: Full Journey** (5 minutes)
Complete end-to-end validation.
```bash
pnpm test:e2e:admin-setup 05-complete
```

### **Mode 4: UI Debug** (interactive)
Debug failures with Playwright Inspector.
```bash
pnpm test:e2e:admin-setup:ui
```

---

## 🔧 Configuration Options

### Environment Variables
```bash
# Use .env.test or set directly
cp e2e/admin-first-time-setup/.env.example ../../.env.test

# Or export directly
export ADMIN_EMAIL=admin@meepleai.dev
export ADMIN_PASSWORD=pVKOMQNK0tFNgGlX
```

### Testcontainers (Optional)
For fully isolated infrastructure:

```bash
# 1. Ensure Docker is running
docker info

# 2. Enable Testcontainers
export E2E_USE_TESTCONTAINERS=true

# 3. Uncomment in playwright.config.ts:
#   globalSetup: require.resolve('./e2e/admin-first-time-setup/setup.ts'),
#   globalTeardown: require.resolve('./e2e/admin-first-time-setup/teardown.ts'),

# 4. Run tests
pnpm test:e2e:admin-setup
```

---

## 📊 Expected Results

### Success Output
```
✅ Test Suite 1: Infrastructure & Bootstrap (6/6 passed)
✅ Test Suite 2: Authentication Flow (9/9 passed)
✅ Test Suite 3: Admin Wizard (5/5 passed)
✅ Test Suite 4: Bounded Contexts (9/9 passed)
✅ Test Suite 5: Complete Journey (1/1 passed)

Total: 30 tests, 30 passed, 0 failed
Time: ~4m 30s
```

### Expected Warnings (Acceptable)
```
⚠️  PDF processing timeout (mock PDFs don't require real processing)
⚠️  OAuth tests skipped (requires real credentials)
⚠️  RAG agent response may be generic (requires real backend services)
```

---

## 🐛 Troubleshooting

### "Test timeout" or "Slow tests"
**Cause**: Backend not responding or PDF processing hanging
**Fix**:
```bash
# Check backend is running
curl http://localhost:8080/api/v1/health

# Restart backend if needed
cd apps/api/src/Api && dotnet run
```

### "Admin login failed"
**Cause**: Credentials mismatch or admin not seeded
**Fix**:
```bash
# Verify admin.secret
cat infra/secrets/admin.secret

# Re-seed admin
cd apps/api/src/Api
dotnet ef database drop --force
dotnet ef database update
dotnet run  # Auto-seeds admin on startup
```

### "File not found: pandemic_rulebook.pdf"
**Cause**: Test PDFs not generated
**Fix**:
```bash
cd apps/web
pnpm test:generate-mock-pdfs
```

### "Wizard UI elements not found"
**Cause**: Selectors outdated or wizard route changed
**Fix**:
```bash
# Run in UI mode to inspect elements
pnpm test:e2e:admin-setup:ui

# Update selectors in test files if needed
```

---

## 📈 CI/CD Integration

### GitHub Actions
```yaml
# .github/workflows/e2e-admin-setup.yml
- name: Generate test PDFs
  run: cd apps/web && pnpm test:generate-mock-pdfs

- name: Run admin setup tests
  run: cd apps/web && pnpm test:e2e:admin-setup
  env:
    ADMIN_EMAIL: admin@test.dev
    ADMIN_PASSWORD: TestPass123
```

---

## 🎯 Next Steps

After successful test run:

1. **Review test report**: `apps/web/playwright-report/index.html`
2. **Check videos**: `apps/web/test-results/**/video.webm` (only on failure)
3. **Inspect traces**: `pnpm exec playwright show-trace trace.zip`
4. **Create PR**: Merge tests into main-dev

---

## 📚 Resources

- [Playwright Docs](https://playwright.dev)
- [Testcontainers Docs](https://testcontainers.com)
- [Main README](./README.md) - Full test suite documentation
- [CLAUDE.md](../../../../CLAUDE.md) - Project development guide

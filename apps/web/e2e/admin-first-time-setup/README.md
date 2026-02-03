# E2E Tests - Admin First-Time Setup

Comprehensive E2E test suite for validating the admin first-time setup experience after fresh deployment.

## 📋 Test Coverage

### Test Suite 1: Infrastructure & Bootstrap (`01-infra-bootstrap.spec.ts`)
- ✅ Service health checks (PostgreSQL, Redis, Qdrant)
- ✅ Admin user seeded from `admin.secret`
- ✅ 9 shared games seeded from `SharedGameSeeder`
- ✅ Backend configuration loaded
- ✅ Frontend-Backend API connectivity

### Test Suite 2: Authentication Flow (`02-authentication-flow.spec.ts`)
- ✅ Admin login (email/password, no 2FA)
- ✅ Session persistence across reloads
- ✅ Session token validation
- ✅ Invalid credentials rejection
- ✅ Logout functionality
- ⏭️ OAuth flows (Google, GitHub - manual testing)
- ✅ Admin role badge display
- ✅ Admin-only route access

### Test Suite 3: Admin Wizard (`03-admin-wizard-complete.spec.ts`)
- ✅ Wizard access from navigation
- ✅ Step 1: PDF Upload
- ✅ Step 2: Game Selection
- ✅ Step 3: Chat Setup (PDF processing + RAG)
- ✅ Step 4: Q&A Validation
- ✅ Complete wizard flow with cleanup

## 🚀 Running Tests

### Prerequisites
```bash
# Install dependencies
cd apps/web
pnpm install

# Ensure backend is running
cd ../api/src/Api
dotnet run

# Ensure frontend is running
cd ../../../web
pnpm dev
```

### Run Full Suite
```bash
# Run all admin setup tests
pnpm test:e2e:admin-setup

# Run specific test file
pnpm test:e2e:admin-setup 01-infra-bootstrap

# Run with UI mode for debugging
pnpm test:e2e:admin-setup --ui

# Run in headed mode (see browser)
pnpm test:e2e:admin-setup --headed
```

### Environment Variables
```bash
# Admin credentials (from infra/secrets/admin.secret)
ADMIN_EMAIL=admin@meepleai.dev
ADMIN_PASSWORD=pVKOMQNK0tFNgGlX

# Optional: Enable real OAuth testing
E2E_OAUTH_ENABLED=true
```

## 🎯 Success Criteria

**Setup is complete when:**
1. ✅ All infrastructure services are healthy
2. ✅ Admin user can log in successfully
3. ✅ Wizard completes all 4 steps without errors
4. ✅ RAG agent responds to test question
5. ✅ Game catalog shows agent availability

## 📊 Test Strategy

### Execution Mode
- **Serial**: Tests run in order (not parallel)
- **Rationale**: First-time setup has sequential dependencies

### Assertion Strategy
- **Full-stack**: UI + API + DB validation
- **Response interception**: Verify API contracts
- **Visual validation**: UI state changes

### Test Data
- **Shared Games**: Uses seeded games (Pandemic, Wingspan, etc.)
- **Mock PDFs**: Minimal PDFs for upload testing
- **Cleanup**: Automatic cleanup after test completion

## 🐛 Troubleshooting

### Test Failures

**"Service health check failed"**
- Ensure backend (port 8080) and frontend (port 3000) are running
- Check PostgreSQL, Redis, Qdrant are accessible

**"Admin login failed"**
- Verify `admin.secret` credentials match test expectations
- Check backend seeded admin user correctly

**"PDF processing timeout"**
- Expected for mock PDFs (no real processing backend)
- Can skip validation or use shorter timeout

**"Chat thread not created"**
- Verify RAG backend services are running
- Check document processing service connectivity

### Debug Mode
```bash
# Run with Playwright Inspector
pnpm test:e2e:admin-setup --debug

# Generate trace for failed tests
pnpm test:e2e:admin-setup --trace on

# View trace
pnpm exec playwright show-trace trace.zip
```

## 🔧 Configuration

Tests use the Playwright configuration from `playwright.config.ts`:

```typescript
{
  name: 'admin-first-time-setup',
  testDir: './e2e/admin-first-time-setup',
  fullyParallel: false,  // Serial execution
  workers: 1,            // Single worker
  timeout: 120000,       // 2 min per test
}
```

## 📝 Adding New Tests

1. Create test file in this directory: `XX-test-name.spec.ts`
2. Use serial mode: `test.describe.configure({ mode: 'serial' })`
3. Import helpers from `../utils/admin-setup-helpers.ts`
4. Follow naming convention: `XX-category-name.spec.ts`
5. Add cleanup in `afterAll` hook

## 🎬 Next Steps

Future enhancements:
- [ ] Testcontainers integration (isolated infrastructure)
- [ ] Real PDF processing validation
- [ ] OAuth automated testing (mock providers)
- [ ] Visual regression testing
- [ ] Performance benchmarking

## 📚 Related Documentation

- [CLAUDE.md](../../../../CLAUDE.md) - Project overview
- [E2E Patterns Memory](../../../docs/memories/e2e-playwright-patterns.md)
- [Backend Testing Guide](../../../docs/05-testing/backend/)
- [Playwright Docs](https://playwright.dev/docs/intro)

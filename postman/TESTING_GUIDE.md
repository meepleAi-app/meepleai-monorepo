# MeepleAI API Testing Guide with Postman

**Version**: 1.0
**Last Updated**: 2026-01-18
**Total Test Collections**: 4
**Total Tests**: 60+
**Estimated Setup Time**: 30 minutes

---

## Table of Contents

1. [Overview](#1-overview)
2. [Installation & Setup](#2-installation--setup)
3. [Test Collections](#3-test-collections)
4. [Running Tests](#4-running-tests)
5. [Environment Configuration](#5-environment-configuration)
6. [Writing Custom Tests](#6-writing-custom-tests)
7. [CI/CD Integration](#7-cicd-integration)
8. [Troubleshooting](#8-troubleshooting)

---

## 1. Overview

### 1.1 Available Test Collections

| Collection | Purpose | Tests Count | Auth Required | Duration |
|------------|---------|-------------|---------------|----------|
| **MeepleAI-API-Tests** | Smoke tests (basic functionality) | 17 | Partial | ~30s |
| **Integration-Tests** | Full workflow testing | 25 | Yes | ~2min |
| **Admin-Tests** | Admin-only operations | 12 | Yes (Admin) | ~1min |
| **E2E-Complete-Workflow** | End-to-end user journey | 18 | Yes | ~3min |

**Total Coverage**: 72 automated tests across 4 collections

---

### 1.2 Test Pyramid Strategy

```
        E2E Tests (18)
         /        \
    Integration (25)
       /              \
   Admin (12)    Smoke (17)
```

**Test Distribution**:
- **Smoke Tests** (17): Fast, critical paths, run on every commit
- **Integration Tests** (25): Feature workflows, run pre-merge
- **Admin Tests** (12): Administrative operations, run nightly
- **E2E Tests** (18): Complete user journeys, run before release

---

## 2. Installation & Setup

### 2.1 Install Postman Desktop

**Download**: https://www.postman.com/downloads/

**Or use Postman CLI (Newman)**:
```bash
# Install globally
npm install -g newman newman-reporter-htmlextra

# Verify installation
newman --version
# Expected: 6.0.0 or higher
```

---

### 2.2 Import Collections

**Option 1: Postman Desktop App**:
1. Open Postman
2. File → Import
3. Select all `.postman_collection.json` files from `postman/` directory
4. Click Import (4 collections imported)

**Option 2: Direct Link** (if published):
```
https://www.postman.com/meepleai/workspace/meepleai-api
```

---

### 2.3 Import Environments

**Import Environment Files**:
1. Postman → Environments → Import
2. Select:
   - `Local-Development.postman_environment.json`
   - `Staging.postman_environment.json` (to be created)
   - `Production.postman_environment.json` (to be created)

---

## 3. Test Collections

### 3.1 MeepleAI-API-Tests (Smoke Tests)

**Purpose**: Quick verification of critical API functionality

**Test Groups** (17 tests total):
1. **Health Checks** (2 tests)
   - `/health/ready` - Readiness probe
   - `/health/live` - Liveness probe

2. **Authentication** (7 tests)
   - Register new user
   - Get current user (`/auth/me`)
   - Session status
   - Logout
   - Login with credentials
   - Error cases (invalid credentials, empty credentials)

3. **Game Management** (4 tests)
   - Get all games
   - Get game by ID
   - Error cases (404, unauthenticated)

4. **KnowledgeBase** (3 tests)
   - RAG search (skips if no data)
   - Error cases (empty query, invalid game ID)

5. **Cleanup** (1 test)
   - Final logout

**When to Run**: On every commit, pre-deploy, production health check

---

### 3.2 Integration-Tests (NEW)

**Purpose**: Test complete feature workflows with realistic data

**Test Groups** (25 tests):

**User Onboarding Flow** (8 tests):
1. Register user with validation checks
2. Email verification simulation
3. Complete profile setup
4. Enable 2FA (TOTP setup)
5. Verify 2FA code
6. Generate backup codes
7. Login with 2FA
8. Disable 2FA

**PDF Upload & Processing** (7 tests):
1. Upload small PDF (<5MB)
2. Check processing status (polling)
3. Verify chunks created
4. Query processed content (RAG)
5. Upload large PDF (>10MB) - should work
6. Upload invalid file (not PDF) - should fail (400)
7. Exceed upload limit (Free tier: 5/week) - should fail (429)

**Game Session Management** (6 tests):
1. Create new game session
2. Add players to session
3. Update session state
4. Get session history
5. Archive session
6. Delete session

**Subscription Management** (4 tests):
1. Check current tier (Free)
2. Upgrade to Normal tier
3. Verify tier features enabled
4. Downgrade to Free

---

### 3.3 Admin-Tests (NEW)

**Purpose**: Test admin-only operations (requires Admin role)

**Test Groups** (12 tests):

**User Management** (5 tests):
1. List all users (paginated)
2. Get user by ID
3. Update user role (promote to Admin)
4. Suspend user account
5. Delete user (soft-delete)

**System Configuration** (3 tests):
1. Get system configuration
2. Update feature flags
3. View audit logs

**Analytics & Reporting** (4 tests):
1. Get user statistics (registrations, active users)
2. Get PDF processing metrics
3. Get RAG query analytics
4. Export monthly report (CSV)

**Auth**: Requires `ADMIN_EMAIL` and `ADMIN_PASSWORD` from environment

---

### 3.4 E2E-Complete-Workflow (NEW)

**Purpose**: Simulate complete user journey from registration to subscription

**Test Flow** (18 tests, sequential):

**Phase 1: Discovery & Registration** (3 tests):
1. Browse public game catalog (unauthenticated)
2. Register new account
3. Verify email (simulate click on verification link)

**Phase 2: Free Tier Usage** (5 tests):
4. Login
5. Upload first PDF (Catan rulebook)
6. Wait for processing completion (poll status)
7. Ask first RAG question: "How do I setup the game?"
8. Create game session with friends

**Phase 3: Tier Limit Encounter** (4 tests):
9. Upload 5 more PDFs (reach Free tier limit: 5/week)
10. Attempt 6th PDF → 429 Too Many Requests
11. Check upgrade pricing
12. Review usage statistics

**Phase 4: Upgrade to Normal** (3 tests):
13. Upgrade to Normal tier (€6/mese)
14. Upload additional PDFs (now allowed)
15. Access advanced analytics (newly unlocked)

**Phase 5: Power User Actions** (3 tests):
16. Enable 2FA for account security
17. Generate API key for third-party integrations
18. Export game session data (CSV)

**Total Duration**: ~3 minutes (includes wait times for PDF processing)

---

## 4. Running Tests

### 4.1 Run Single Collection (Newman CLI)

**Smoke Tests**:
```bash
newman run MeepleAI-API-Tests.postman_collection.json \
  -e Local-Development.postman_environment.json
```

**Integration Tests**:
```bash
newman run Integration-Tests.postman_collection.json \
  -e Local-Development.postman_environment.json \
  --timeout-request 30000  # 30s timeout for PDF processing
```

**Admin Tests** (requires admin credentials):
```bash
newman run Admin-Tests.postman_collection.json \
  -e Local-Development.postman_environment.json \
  --env-var "adminEmail=admin@meepleai.com" \
  --env-var "adminPassword=YourAdminPassword"
```

**E2E Tests**:
```bash
newman run E2E-Complete-Workflow.postman_collection.json \
  -e Local-Development.postman_environment.json \
  --delay-request 1000  # 1s delay between requests (realistic simulation)
```

---

### 4.2 Run All Collections (Sequential)

**Bash Script**: `run-all-tests.sh`

```bash
#!/bin/bash
# postman/run-all-tests.sh

ENV_FILE="Local-Development.postman_environment.json"
REPORT_DIR="./reports"
mkdir -p $REPORT_DIR

echo "🧪 Running MeepleAI API Tests..."
echo "================================"

# 1. Smoke Tests (fast)
echo "1/4 Running Smoke Tests..."
newman run MeepleAI-API-Tests.postman_collection.json \
  -e $ENV_FILE \
  -r cli,htmlextra \
  --reporter-htmlextra-export $REPORT_DIR/smoke-tests-report.html

# 2. Integration Tests
echo "2/4 Running Integration Tests..."
newman run Integration-Tests.postman_collection.json \
  -e $ENV_FILE \
  --timeout-request 30000 \
  -r cli,htmlextra \
  --reporter-htmlextra-export $REPORT_DIR/integration-tests-report.html

# 3. Admin Tests (skip if no admin credentials)
if [ -n "$ADMIN_EMAIL" ] && [ -n "$ADMIN_PASSWORD" ]; then
    echo "3/4 Running Admin Tests..."
    newman run Admin-Tests.postman_collection.json \
      -e $ENV_FILE \
      --env-var "adminEmail=$ADMIN_EMAIL" \
      --env-var "adminPassword=$ADMIN_PASSWORD" \
      -r cli,htmlextra \
      --reporter-htmlextra-export $REPORT_DIR/admin-tests-report.html
else
    echo "3/4 Skipping Admin Tests (no credentials)"
fi

# 4. E2E Tests
echo "4/4 Running E2E Tests..."
newman run E2E-Complete-Workflow.postman_collection.json \
  -e $ENV_FILE \
  --delay-request 1000 \
  --timeout-request 60000 \
  -r cli,htmlextra \
  --reporter-htmlextra-export $REPORT_DIR/e2e-tests-report.html

echo "================================"
echo "✅ All tests completed!"
echo "📊 Reports available in: $REPORT_DIR/"
```

**Make executable**:
```bash
chmod +x postman/run-all-tests.sh
```

**Run**:
```bash
cd postman
./run-all-tests.sh

# Or with admin credentials
ADMIN_EMAIL=admin@meepleai.com ADMIN_PASSWORD=YourPassword ./run-all-tests.sh
```

---

### 4.3 Run with HTML Report

**Generate Detailed HTML Report**:
```bash
newman run MeepleAI-API-Tests.postman_collection.json \
  -e Local-Development.postman_environment.json \
  -r htmlextra \
  --reporter-htmlextra-export reports/api-tests-$(date +%Y%m%d-%H%M%S).html \
  --reporter-htmlextra-title "MeepleAI API Tests" \
  --reporter-htmlextra-darkTheme
```

**Open Report**:
```bash
# Windows
start reports/api-tests-*.html

# Linux/Mac
open reports/api-tests-*.html
```

**Report Includes**:
- Total requests, passed/failed tests
- Response time statistics (min, max, avg, p95)
- Failure details with stack traces
- Request/response bodies for debugging
- Environment variables used

---

## 5. Environment Configuration

### 5.1 Local Development Environment

**File**: `Local-Development.postman_environment.json`

```json
{
  "id": "local-dev-env",
  "name": "Local Development",
  "values": [
    {
      "key": "baseUrl",
      "value": "http://localhost:8080/api/v1",
      "enabled": true
    },
    {
      "key": "healthBaseUrl",
      "value": "http://localhost:8080",
      "enabled": true
    },
    {
      "key": "testPassword",
      "value": "TestPassword123!",
      "enabled": true
    },
    {
      "key": "adminEmail",
      "value": "admin@local.dev",
      "enabled": true
    },
    {
      "key": "adminPassword",
      "value": "Admin123!",
      "enabled": true
    }
  ]
}
```

---

### 5.2 Staging Environment (NEW)

**File**: `Staging.postman_environment.json`

```json
{
  "id": "staging-env",
  "name": "Staging",
  "values": [
    {
      "key": "baseUrl",
      "value": "https://api-staging.meepleai.com/api/v1",
      "enabled": true
    },
    {
      "key": "healthBaseUrl",
      "value": "https://api-staging.meepleai.com",
      "enabled": true
    },
    {
      "key": "testPassword",
      "value": "StagingTest123!",
      "enabled": true
    },
    {
      "key": "adminEmail",
      "value": "admin-staging@meepleai.com",
      "enabled": true
    },
    {
      "key": "adminPassword",
      "value": "",
      "enabled": true,
      "type": "secret"
    }
  ]
}
```

**Note**: `adminPassword` should be set as **secret** type (not committed to Git)

---

### 5.3 Production Environment (NEW)

**File**: `Production.postman_environment.json`

```json
{
  "id": "production-env",
  "name": "Production",
  "values": [
    {
      "key": "baseUrl",
      "value": "https://api.meepleai.com/api/v1",
      "enabled": true
    },
    {
      "key": "healthBaseUrl",
      "value": "https://api.meepleai.com",
      "enabled": true
    },
    {
      "key": "adminEmail",
      "value": "admin@meepleai.com",
      "enabled": true
    },
    {
      "key": "adminPassword",
      "value": "",
      "enabled": true,
      "type": "secret"
    }
  ]
}
```

**⚠️ WARNING**: Do NOT run destructive tests (delete user, delete game) on production!

**Safe Production Tests**:
- Health checks ✅
- Authentication (with test account) ✅
- GET endpoints (read-only) ✅
- POST/PUT/DELETE → ❌ **Disable in production runs**

---

## 6. Writing Custom Tests

### 6.1 Test Structure

**Postman Test Anatomy**:

```javascript
// 1. Pre-request Script (setup)
// Runs BEFORE request is sent
pm.collectionVariables.set('testEmail', `test-${Date.now()}@example.com`);

// 2. Request execution (automatic)

// 3. Test Script (assertions)
// Runs AFTER response received
pm.test('Status code is 200', function() {
    pm.response.to.have.status(200);
});

pm.test('Response has expected structure', function() {
    const json = pm.response.json();
    pm.expect(json.user).to.exist;
    pm.expect(json.user.id).to.be.a('string');
});

// 4. Save variables for next request
pm.collectionVariables.set('userId', json.user.id);
```

---

### 6.2 Common Test Patterns

**Pattern 1: Status Code Assertion**:
```javascript
pm.test('Status code is 200 OK', function() {
    pm.response.to.have.status(200);
});

pm.test('Status code is 201 Created', function() {
    pm.response.to.have.status(201);
});

pm.test('Status code is 4xx for client errors', function() {
    pm.expect(pm.response.code).to.be.oneOf([400, 401, 404, 429]);
});
```

---

**Pattern 2: Response Body Validation**:
```javascript
pm.test('Response contains required fields', function() {
    const json = pm.response.json();

    // Check existence
    pm.expect(json.user).to.exist;
    pm.expect(json.user.id).to.exist;
    pm.expect(json.user.email).to.exist;

    // Check types
    pm.expect(json.user.id).to.be.a('string');
    pm.expect(json.user.email).to.match(/^.+@.+\..+$/);  // Email regex

    // Check values
    pm.expect(json.user.email).to.equal(pm.collectionVariables.get('testEmail'));
});
```

---

**Pattern 3: Response Time Assertion**:
```javascript
pm.test('Response time is acceptable', function() {
    pm.expect(pm.response.responseTime).to.be.below(500);  // < 500ms
});

pm.test('API latency is excellent', function() {
    pm.expect(pm.response.responseTime).to.be.below(100);  // < 100ms
});
```

---

**Pattern 4: Cookie/Header Validation**:
```javascript
pm.test('Session cookie is set', function() {
    pm.expect(pm.cookies.has('meepleai_session')).to.be.true;

    const cookie = pm.cookies.get('meepleai_session');
    pm.expect(cookie).to.have.property('httpOnly', true);  // Security
    pm.expect(cookie).to.have.property('secure', true);    // HTTPS only
});

pm.test('CORS headers present', function() {
    pm.expect(pm.response.headers.get('Access-Control-Allow-Origin')).to.exist;
});
```

---

**Pattern 5: Conditional Testing (Skip if No Data)**:
```javascript
// Pre-request script
const testGameId = pm.collectionVariables.get('testGameId');
if (!testGameId) {
    console.log('SKIP: No game data available in database');
    pm.execution.skipRequest();  // Skip this request
}

// Test script (only runs if not skipped)
pm.test('Game data returned', function() {
    const json = pm.response.json();
    pm.expect(json.id).to.equal(testGameId);
});
```

---

**Pattern 6: Variable Extraction**:
```javascript
// Extract user ID from registration response
pm.test('Extract userId for later tests', function() {
    const json = pm.response.json();
    pm.collectionVariables.set('userId', json.user.id);
    console.log('Saved userId:', json.user.id);
});

// Extract pagination token
pm.test('Extract next page token', function() {
    const json = pm.response.json();
    if (json.pagination && json.pagination.nextToken) {
        pm.collectionVariables.set('nextPageToken', json.pagination.nextToken);
    }
});
```

---

### 6.3 Advanced Testing Techniques

**Dynamic Data Generation**:
```javascript
// Pre-request Script
const faker = require('faker');  // If using Postman, faker is built-in

// Generate realistic test data
pm.collectionVariables.set('testEmail', faker.internet.email());
pm.collectionVariables.set('testName', faker.name.findName());
pm.collectionVariables.set('testPhone', faker.phone.phoneNumber());

// Or without faker:
const timestamp = Date.now();
const randomId = Math.random().toString(36).substring(7);
pm.collectionVariables.set('testEmail', `test-${timestamp}-${randomId}@example.com`);
```

---

**Retry Logic for Flaky Tests**:
```javascript
// For polling endpoints (PDF processing status)
const maxRetries = 10;
const retryDelay = 2000;  // 2 seconds

pm.test('PDF processing completes', function() {
    const json = pm.response.json();

    if (json.status === 'processing' && pm.info.iteration < maxRetries) {
        // Still processing, retry
        setTimeout(() => {
            pm.execution.setNextRequest(pm.info.requestName);  // Retry same request
        }, retryDelay);
    } else {
        pm.expect(json.status).to.equal('completed');
    }
});
```

---

**Cleanup After Tests**:
```javascript
// Final cleanup request (delete test user)
pm.test('Cleanup test data', function() {
    const userId = pm.collectionVariables.get('testUserId');

    if (userId) {
        // Send DELETE request
        pm.sendRequest({
            url: `${pm.environment.get('baseUrl')}/admin/users/${userId}`,
            method: 'DELETE',
            header: {
                'Authorization': `Bearer ${pm.collectionVariables.get('adminToken')}`
            }
        }, (err, res) => {
            if (!err) {
                console.log('✅ Test user deleted:', userId);
            }
        });
    }
});
```

---

## 7. CI/CD Integration

### 7.1 GitHub Actions Workflow

**File**: `.github/workflows/api-tests.yml`

```yaml
name: API Tests (Postman)

on:
  push:
    branches: [main-dev, main]
  pull_request:
    branches: [main-dev, main]

jobs:
  postman-tests:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_USER: meepleai
          POSTGRES_PASSWORD: test_password
          POSTGRES_DB: meepleai_test
        ports:
          - 5432:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

      redis:
        image: redis:7-alpine
        ports:
          - 6379:6379

      qdrant:
        image: qdrant/qdrant:v1.7
        ports:
          - 6333:6333

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Start API
        run: |
          cd apps/api/src/Api
          dotnet run &
          sleep 30  # Wait for API to start

      - name: Install Newman
        run: npm install -g newman newman-reporter-htmlextra

      - name: Run Smoke Tests
        run: |
          newman run postman/MeepleAI-API-Tests.postman_collection.json \
            -e postman/Local-Development.postman_environment.json \
            -r cli,junit \
            --reporter-junit-export test-results/smoke-tests.xml

      - name: Run Integration Tests
        run: |
          newman run postman/Integration-Tests.postman_collection.json \
            -e postman/Local-Development.postman_environment.json \
            --timeout-request 30000 \
            -r cli,junit \
            --reporter-junit-export test-results/integration-tests.xml

      - name: Upload Test Results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: postman-test-results
          path: test-results/

      - name: Publish Test Results
        if: always()
        uses: EnricoMi/publish-unit-test-result-action@v2
        with:
          files: test-results/*.xml
```

---

### 7.2 Pre-Commit Hook (Local)

**File**: `.husky/pre-commit`

```bash
#!/bin/sh
. "$(dirname "$0")/_/husky.sh"

# Run quick smoke tests before commit
echo "🧪 Running API smoke tests..."

cd postman
newman run MeepleAI-API-Tests.postman_collection.json \
  -e Local-Development.postman_environment.json \
  --bail  # Stop on first failure

if [ $? -eq 0 ]; then
  echo "✅ All smoke tests passed"
else
  echo "❌ Smoke tests failed - commit blocked"
  exit 1
fi
```

**Install Husky**:
```bash
npm install --save-dev husky
npx husky install
npx husky add .husky/pre-commit "bash postman/run-smoke-tests.sh"
```

---

## 8. Test Data Management

### 8.1 Test Data Fixtures

**Create**: `postman/fixtures/test-data.json`

```json
{
  "testUsers": [
    {
      "email": "alice@example.com",
      "password": "Alice123!",
      "displayName": "Alice Test",
      "role": "User"
    },
    {
      "email": "bob-admin@example.com",
      "password": "BobAdmin123!",
      "displayName": "Bob Admin",
      "role": "Admin"
    }
  ],
  "testGames": [
    {
      "title": "Test Game: Catan",
      "bggId": 13,
      "complexity": 2.5
    }
  ],
  "testPdfs": [
    {
      "filename": "small-rulebook.pdf",
      "size": "2MB",
      "url": "https://example.com/test-pdfs/small.pdf"
    }
  ]
}
```

**Load in Pre-Request**:
```javascript
// Load fixture data
const testData = pm.collectionVariables.get('testDataFixture');
if (!testData) {
    // Load from file (first run only)
    const rawData = pm.sendRequest('http://localhost:8080/test-fixtures/test-data.json');
    pm.collectionVariables.set('testDataFixture', rawData);
}

// Use fixture
const users = JSON.parse(testData).testUsers;
pm.collectionVariables.set('testEmail', users[0].email);
```

---

### 8.2 Database Seeding for Tests

**Seed Script**: `scripts/seed-test-data.sh`

```bash
#!/bin/bash
# Seed database with test games for Postman tests

echo "Seeding test data..."

# Insert test game (Catan)
docker exec infra-postgres-1 psql -U meepleai -d meepleai_db -c "
  INSERT INTO games (id, title, bgg_id, complexity, min_players, max_players, created_at)
  VALUES (
    'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
    'Catan',
    13,
    2.5,
    3,
    4,
    NOW()
  )
  ON CONFLICT (id) DO NOTHING;
"

# Insert test user
docker exec infra-postgres-1 psql -U meepleai -d meepleai_db -c "
  INSERT INTO users (id, email, password_hash, display_name, role, created_at)
  VALUES (
    'b1ffcc99-9c0b-4ef8-bb6d-6bb9bd380a22',
    'testuser@example.com',
    '\$2a\$11\$hashed_password_here',
    'Test User',
    'User',
    NOW()
  )
  ON CONFLICT (email) DO NOTHING;
"

echo "✅ Test data seeded"
```

**Run Before Tests**:
```bash
bash scripts/seed-test-data.sh
newman run Integration-Tests.postman_collection.json -e Local-Development.postman_environment.json
```

---

## 9. Test Coverage Matrix

### 9.1 API Endpoint Coverage

| Endpoint | Smoke | Integration | Admin | E2E | Coverage |
|----------|-------|-------------|-------|-----|----------|
| **Authentication** |
| POST /auth/register | ✅ | ✅ | - | ✅ | 100% |
| POST /auth/login | ✅ | ✅ | - | ✅ | 100% |
| POST /auth/logout | ✅ | ✅ | - | ✅ | 100% |
| GET /auth/me | ✅ | ✅ | - | ✅ | 100% |
| POST /auth/totp/enable | - | ✅ | - | ✅ | 67% |
| POST /auth/totp/verify | - | ✅ | - | ✅ | 67% |
| **Game Management** |
| GET /games | ✅ | ✅ | ✅ | ✅ | 100% |
| GET /games/{id} | ✅ | ✅ | - | ✅ | 75% |
| POST /games | - | ✅ | ✅ | - | 67% |
| PUT /games/{id} | - | - | ✅ | - | 33% |
| DELETE /games/{id} | - | - | ✅ | - | 33% |
| **KnowledgeBase** |
| POST /knowledge-base/search | ✅ | ✅ | - | ✅ | 75% |
| POST /knowledge-base/ask | - | ✅ | - | ✅ | 67% |
| **Document Processing** |
| POST /documents/upload | - | ✅ | ✅ | ✅ | 75% |
| GET /documents/{id}/status | - | ✅ | - | ✅ | 67% |
| **Admin** |
| GET /admin/users | - | - | ✅ | - | 33% |
| PUT /admin/users/{id}/role | - | - | ✅ | - | 33% |
| GET /admin/analytics | - | - | ✅ | - | 33% |

**Overall Coverage**: 72 tests across 25 endpoints = **~75% coverage** ✅

**Missing Coverage** (TODO):
- Game session real-time updates (WebSocket)
- Bulk operations (batch upload PDFs)
- Export endpoints (CSV, JSON downloads)

---

## 10. Performance Testing

### 10.1 Load Testing with Newman

**Load Test Script**: `postman/load-test-runner.sh`

```bash
#!/bin/bash
# Run smoke tests repeatedly to simulate load

CONCURRENT_USERS=10
ITERATIONS=100

echo "🔥 Load testing with $CONCURRENT_USERS concurrent users, $ITERATIONS iterations each"

# Run in parallel
for i in $(seq 1 $CONCURRENT_USERS); do
    (
        newman run MeepleAI-API-Tests.postman_collection.json \
          -e Local-Development.postman_environment.json \
          -n $ITERATIONS \
          --bail false \
          --reporter-cli-no-assertions \
          --reporter-cli-no-console 2>&1 | \
          grep -E "(passed|failed|avg|min|max)" > load-test-user-$i.log
    ) &
done

wait  # Wait for all background jobs to finish

echo "✅ Load test completed"
echo "📊 Results:"
cat load-test-user-*.log | grep "executed" | awk '{sum+=$3} END {print "Total requests:", sum}'
cat load-test-user-*.log | grep "avg:" | awk '{sum+=$2; count++} END {print "Average response time:", sum/count "ms"}'
```

**Run**:
```bash
bash postman/load-test-runner.sh
```

**Expected Output**:
```
Total requests: 1,700
Average response time: 245ms
```

---

### 10.2 Performance Benchmarks

**Target SLAs** (from infrastructure analysis):

| Endpoint Type | P50 Target | P95 Target | Max Acceptable |
|---------------|------------|------------|----------------|
| Health checks | <10ms | <20ms | 50ms |
| Authentication | <100ms | <200ms | 500ms |
| GET endpoints | <50ms | <100ms | 300ms |
| POST/PUT (simple) | <100ms | <200ms | 500ms |
| RAG queries | <2s | <3.5s | 5s |
| PDF upload | <5s | <10s | 30s |

**Automated Performance Test**:
```javascript
// Test Script
pm.test('Response time within SLA', function() {
    const endpoint = pm.request.url.getPath();
    let threshold;

    if (endpoint.includes('/health')) {
        threshold = 50;
    } else if (endpoint.includes('/auth')) {
        threshold = 500;
    } else if (endpoint.includes('/knowledge-base')) {
        threshold = 5000;
    } else {
        threshold = 300;  // Default for GET/POST
    }

    pm.expect(pm.response.responseTime).to.be.below(threshold,
        `Response time ${pm.response.responseTime}ms exceeds ${threshold}ms threshold`);
});
```

---

## 11. Troubleshooting

### 11.1 Common Issues

**Issue**: `ECONNREFUSED 127.0.0.1:8080`

**Cause**: API not running

**Fix**:
```bash
# Start API
cd apps/api/src/Api
dotnet run

# Verify running
curl http://localhost:8080/health/ready
# Should return: "Healthy"
```

---

**Issue**: `401 Unauthorized` on authenticated endpoints

**Cause**: Session cookie not persisting between requests

**Fix**:
```javascript
// In Postman: Settings → General → Cookies
// Enable: "Automatically follow redirects"
// Enable: "Send cookies with requests"

// In Newman: Cookies are handled automatically via cookie jar
```

---

**Issue**: Tests timeout on PDF upload

**Cause**: Default timeout too short (5s)

**Fix**:
```bash
# Increase timeout in Newman
newman run Integration-Tests.postman_collection.json \
  -e Local-Development.postman_environment.json \
  --timeout-request 60000  # 60 seconds
```

---

**Issue**: `Variable 'testGameId' not defined`

**Cause**: Database empty (no games seeded)

**Fix**:
```bash
# Seed test data
bash scripts/seed-test-data.sh

# Or skip tests gracefully (already implemented in collections)
# Tests auto-skip if testGameId is empty
```

---

### 11.2 Debug Mode

**Enable Verbose Logging**:
```bash
# Newman verbose mode
newman run MeepleAI-API-Tests.postman_collection.json \
  -e Local-Development.postman_environment.json \
  --verbose \
  --debug

# Output shows:
# - Request headers, body
# - Response headers, body
# - Variable values
# - Test execution details
```

**Postman Console**:
```javascript
// Add console.log in test scripts
pm.test('Debug test', function() {
    const json = pm.response.json();
    console.log('Response:', JSON.stringify(json, null, 2));
    console.log('Status:', pm.response.code);
    console.log('Time:', pm.response.responseTime + 'ms');
});
```

**View Console**: Postman → View → Show Postman Console (Alt + Ctrl + C)

---

## 12. Best Practices

### 12.1 Test Organization

**✅ DO**:
- Group related tests in folders (Authentication, Games, Admin)
- Use descriptive test names: "Login - Invalid Credentials (400)"
- Extract common setup to Collection Pre-request Script
- Use environment variables for URLs, credentials
- Clean up test data after tests complete

**❌ DON'T**:
- Hardcode URLs, passwords in requests
- Create tests with external dependencies (use mocks)
- Leave test users/data in production database
- Run destructive tests (DELETE) on production
- Commit sensitive data (API keys, passwords) in environment files

---

### 12.2 Security Considerations

**Sensitive Data Handling**:
```json
// Environment file
{
  "key": "apiKey",
  "value": "",
  "type": "secret"  // Mark as secret (hidden in Postman UI)
}
```

**Never commit**:
- Production API keys
- Admin passwords
- Real user emails
- Production environment files

**Use Git-ignored files for secrets**:
```bash
# .gitignore
postman/*Production*.json
postman/secrets/
```

---

## 13. Quick Reference

### 13.1 Newman Commands Cheat Sheet

```bash
# Basic run
newman run collection.json -e environment.json

# With HTML report
newman run collection.json -e environment.json -r htmlextra

# Run specific folder
newman run collection.json --folder "Authentication Tests"

# Run with iterations (repeat N times)
newman run collection.json -n 10

# Run with delay between requests
newman run collection.json --delay-request 1000  # 1s delay

# Run with custom timeout
newman run collection.json --timeout-request 30000  # 30s

# Bail on first failure
newman run collection.json --bail

# Export environment after run
newman run collection.json -e env.json --export-environment env-output.json
```

---

### 13.2 Postman Scripting API

**Common Functions**:
```javascript
// Response
pm.response.code          // Status code (number)
pm.response.status        // Status text ("OK", "Not Found")
pm.response.responseTime  // Time in ms
pm.response.json()        // Parse JSON response
pm.response.text()        // Raw response text

// Request
pm.request.url            // Request URL
pm.request.method         // HTTP method
pm.request.headers        // Request headers

// Variables
pm.environment.get('key')
pm.environment.set('key', 'value')
pm.collectionVariables.get('key')
pm.collectionVariables.set('key', 'value')
pm.globals.get('key')

// Cookies
pm.cookies.has('cookieName')
pm.cookies.get('cookieName')

// Assertions
pm.expect(value).to.equal(expected)
pm.expect(value).to.be.a('string')
pm.expect(value).to.be.above(100)
pm.expect(value).to.be.oneOf([200, 201, 204])
pm.expect(value).to.match(/regex/)
```

---

## 14. Next Steps

**After Setup**:
1. [ ] Run all 4 collections locally (verify 72 tests pass)
2. [ ] Create staging environment file
3. [ ] Add performance assertions to critical endpoints
4. [ ] Integrate with CI/CD pipeline (GitHub Actions)
5. [ ] Setup monitoring for test failures (Slack notifications)
6. [ ] Document custom test scenarios for new features

**Advanced**:
- [ ] Create contract tests (Pact for API contract validation)
- [ ] Add load testing with k6 (complement Postman)
- [ ] Setup automated test reporting dashboard
- [ ] Implement test data factory (realistic test data generation)

---

**Reference Links**:
- Postman Documentation: https://learning.postman.com/docs/
- Newman Documentation: https://github.com/postmanlabs/newman
- Chai Assertion Library: https://www.chaijs.com/api/bdd/
- Postman Test Examples: https://www.postman.com/postman/workspace/postman-answers


# MeepleAI Postman API Tests

Postman collections for testing the MeepleAI API.

## Collection Structure

### MeepleAI-API-Tests.postman_collection.json

A consolidated test suite covering all essential API functionality:

1. **Health Checks** - Basic API health verification (no auth required)
   - `/health/ready` - Readiness probe
   - `/health/live` - Liveness probe

2. **Authentication** - User registration, login, and session management
   - Register new user (generates unique email per run)
   - Get current user (`/auth/me`)
   - Session status check
   - Logout
   - Login with credentials
   - Error cases (invalid credentials, empty credentials)

3. **Game Management** - Game CRUD operations (requires auth)
   - Get all games
   - Get game by ID
   - Error cases (unauthenticated access, non-existent game)

4. **KnowledgeBase** - RAG search and Q&A (requires auth + game data)
   - Search endpoint (skips gracefully if no games available)
   - Error cases (empty query, invalid game ID)

5. **Cleanup** - Final logout

## Running Tests

### Prerequisites

- Node.js and npm installed
- Newman installed globally: `npm install -g newman newman-reporter-htmlextra`
- API running on `http://localhost:8080`

### Local Development

```bash
# Run all tests
newman run postman/MeepleAI-API-Tests.postman_collection.json \
  -e postman/Local-Development.postman_environment.json

# Run with HTML report
newman run postman/MeepleAI-API-Tests.postman_collection.json \
  -e postman/Local-Development.postman_environment.json \
  -r cli,htmlextra \
  --reporter-htmlextra-export newman-report.html
```

### CI/CD

Tests are automatically run in GitHub Actions CI pipeline as part of the `ci-api-smoke` job.

## Test Design Principles

1. **Idempotent** - Tests can be run multiple times without side effects
2. **Self-contained** - Each test generates its own test data (unique emails)
3. **CI-friendly** - Tests skip gracefully when dependencies (e.g., game data) are unavailable
4. **Session-aware** - Tests use cookies for authentication, mimicking browser behavior

## Environment Variables

The `Local-Development.postman_environment.json` file contains:

- `baseUrl`: API base URL (default: `http://localhost:8080/api/v1`)
- `healthBaseUrl`: Health check base URL (default: `http://localhost:8080`)
- `testPassword`: Default test password (default: `TestPassword123!`)

**Note**: `testEmail`, `testUserId`, and `testGameId` are dynamically generated during test execution and should NOT be defined in the environment file.

## Test Count

- **Health Checks**: 2 tests
- **Authentication**: 7 tests
- **Game Management**: 4 tests
- **KnowledgeBase**: 3 tests
- **Cleanup**: 1 test
- **Total**: 17 tests

## Troubleshooting

### Authentication failures
- Ensure the API is running and healthy: `curl http://localhost:8080/health`
- Check that cookies are being set correctly (session cookie name: `meepleai_session`)

### KnowledgeBase tests skipped
- This is expected in CI environments without seeded game data
- Tests use `pm.execution.skipRequest()` to skip gracefully

### Health check failures
- Verify PostgreSQL, Redis, and Qdrant are running
- Check API logs for connection errors

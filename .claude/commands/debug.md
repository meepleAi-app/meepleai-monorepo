# Debug Command

Automated debugging workflow with comprehensive error analysis, replication, and permanent resolution.

**Works with ANY type of error:** C#/.NET, TypeScript/JavaScript, React/Next.js, databases (PostgreSQL, Redis, Qdrant), Docker, APIs, configuration, infrastructure, etc.

## Usage

```
/debug <error_text_or_description>
```

## Examples

```bash
# .NET compilation error
/debug CS0246: The type or namespace name 'QdrantService' could not be found

# Runtime exception
/debug System.NullReferenceException: Object reference not set to an instance of an object at RagService.SearchAsync

# Database error
/debug Npgsql.PostgresException (0x80004005): 42P01: relation "game_rules" does not exist

# TypeScript error
/debug TS2339: Property 'chatId' does not exist on type 'StreamingResponse'

# Docker/Container error
/debug docker: Error response from daemon: Conflict. The container name "/meepleai-postgres" is already in use

# API/HTTP error
/debug Failed to fetch: POST http://localhost:8080/api/v1/agents/qa returned 500 Internal Server Error

# Build error
/debug pnpm build failed with exit code 1: Type error in pages/chat.tsx
```

## Workflow

You are tasked with debugging an error using a comprehensive, systematic approach with MCP integrations.

**Error to debug:**
```
{{input}}
```

## Phase 1: Analysis & Context Gathering

1. **Extract Error Information:**
   - Parse error message, stack trace, and error codes
   - Identify affected files, line numbers, and components
   - Determine error category (runtime, compilation, network, database, etc.)
   - Extract library/framework names mentioned in the error

2. **Get Library Documentation (Context7 MCP):**
   - **For EVERY library/framework identified in the error**, retrieve up-to-date docs:
     - Use `mcp__upstash-context-7-mcp__resolve-library-id` to find the library
     - Use `mcp__upstash-context-7-mcp__get-library-docs` to get documentation
     - Focus on: error handling, common issues, troubleshooting, best practices
   - **Backend examples**: ASP.NET Core, Entity Framework Core, Npgsql, StackExchange.Redis, Docnet.Core, iText7, Serilog, xUnit, Moq, Testcontainers
   - **Frontend examples**: React, Next.js, TypeScript, Jest, Playwright, axios
   - **Infrastructure examples**: PostgreSQL, Redis, Qdrant, Docker, Prometheus, Jaeger, OpenTelemetry
   - **Other examples**: Any npm package, NuGet package, or technology mentioned in the error

3. **Check IDE Diagnostics (IDE MCP):**
   - Use `mcp__ide__getDiagnostics` to get current language server diagnostics
   - Identify related errors, warnings, and type issues
   - Correlate diagnostics with the reported error

4. **Search Codebase:**
   - Use Grep to find:
     - Error message occurrences
     - Related error handling code
     - Similar patterns that might cause the same issue
     - Test files that might be affected
   - Use Glob to find:
     - Affected source files
     - Related configuration files
     - Test files for the affected components

5. **Analyze UI Components (Magic MCP - if UI error):**
   - **Only for UI component errors** (React, Vue, Svelte, TypeScript components):
     - Use `mcp__magic__magic_analyze` to analyze UI component structure and patterns
     - Focus on: component design, accessibility issues, UI patterns, styling problems
   - **Skip this step** for backend errors, database errors, Docker errors, etc.

## Phase 2: Error Replication

1. **Create Reproduction Scenario:**
   Based on error context, create minimal reproduction case appropriate to error type:

   - **Backend API errors**: Create xUnit integration test (WebApplicationFactory + Testcontainers)
   - **Backend service errors**: Create unit test with mocked dependencies
   - **Database errors**: Create EF Core migration or seed data scenario
   - **Frontend UI errors**: Create Jest unit test or Playwright E2E test
   - **Frontend component errors**: Create React component test with mocked API
   - **TypeScript errors**: Create minimal .ts file demonstrating the type issue
   - **Build errors**: Document build command and affected files
   - **Docker errors**: Create docker-compose scenario or Dockerfile snippet
   - **Configuration errors**: Create test config file demonstrating the issue
   - **Integration errors**: Create test demonstrating interaction between services

2. **Verify Reproduction:**
   - Run the test/scenario to confirm the error occurs
   - Capture full error output, stack trace, and logs
   - For backend: Check Seq logs, Jaeger traces if applicable
   - For frontend: Check browser console, network tab
   - Document exact reproduction steps

## Phase 3: Root Cause Analysis

1. **Identify Root Cause:**
   Analyze error using multiple information sources:
   - **Library documentation** from Context7 (error patterns, common pitfalls)
   - **Codebase patterns** from Grep/Glob (existing implementations)
   - **IDE diagnostics** (type errors, warnings, language server)
   - **UI component analysis** (Magic MCP if UI error - see Phase 1 step 5)
   - **Git history** (`git log`, `git blame` for recent changes)
   - **Configuration files** (appsettings.json, .env, docker-compose.yml, package.json)
   - **Logs** (Seq for backend, browser console for frontend)

   Common root causes by error type:
   - **Configuration**: Missing env vars, wrong connection strings, incorrect JSON
   - **Logic errors**: Null reference, off-by-one, incorrect conditions
   - **Type errors**: TypeScript mismatches, C# type conversions
   - **Database**: Missing migrations, wrong entity relationships, SQL syntax
   - **API**: Wrong HTTP methods, missing auth headers, CORS issues
   - **Dependency**: Version conflicts, missing packages, breaking changes
   - **Race conditions**: Async/await issues, concurrent access
   - **Resource exhaustion**: Memory leaks, connection pool exhaustion
   - **Docker**: Port conflicts, volume permissions, network issues

2. **Validate Hypothesis:**
   - Read relevant code sections with Read tool
   - Check all related configuration files
   - Review git history: `git log --oneline`, `git blame <file>`
   - Verify dependencies: `dotnet list package`, `pnpm list`
   - Check runtime state: Seq logs, Jaeger traces, database state
   - Consult Context7 docs for library-specific troubleshooting

## Phase 4: Solution Implementation

1. **Design Solution:**
   - Based on library best practices from Context7
   - Following project patterns from CLAUDE.md
   - Ensuring the fix prevents future occurrences
   - Consider:
     - Input validation
     - Error handling
     - Logging improvements
     - Configuration validation
     - Type safety
     - Defensive programming

2. **Implement Fix:**
   - Use Edit tool to apply changes
   - Follow coding standards from CLAUDE.md
   - Add defensive code (null checks, validation, etc.)
   - Improve error messages for better debugging
   - Add logging at critical points

3. **Add Preventive Measures:**
   - **Tests:** Create unit/integration tests that would catch this error
   - **Validation:** Add input validation if missing
   - **Type Safety:** Strengthen types if applicable (TypeScript, C#)
   - **Error Handling:** Add try-catch with proper error messages
   - **Configuration Validation:** Validate config at startup if needed
   - **Documentation:** Add inline comments explaining the fix

## Phase 5: Verification

1. **Run Reproduction Test:**
   - Execute the test created in Phase 2
   - Verify the error is resolved
   - Ensure no new errors introduced
   - For integration tests: Verify Testcontainers start correctly

2. **Run Test Suite:**
   Based on affected area:
   - **Backend full**: `dotnet test` in apps/api
   - **Backend filtered**: `dotnet test --filter "FullyQualifiedName~<TestClass>"`
   - **Frontend full**: `pnpm test` in apps/web
   - **Frontend filtered**: `pnpm test <specific-test-file>`
   - **E2E tests**: `pnpm test:e2e` (if UI changes)
   - **Coverage**: `pwsh tools/measure-coverage.ps1 -Project api|web`
   - Ensure all existing tests still pass

3. **Check Build:**
   Based on affected stack:
   - **Backend**: `dotnet build` in apps/api
   - **Frontend**: `pnpm typecheck` AND `pnpm build` in apps/web
   - **Both**: Build both to verify no cross-stack issues
   - Verify no compilation errors or warnings

4. **Verify Runtime:**
   - **Backend**: Start API (`dotnet run`), check health endpoints
   - **Frontend**: Start dev server (`pnpm dev`), verify page loads
   - **Integration**: Start full stack (docker compose + api + web)
   - **Logs**: Check Seq for errors, warnings
   - **Traces**: Check Jaeger for error spans (if applicable)
   - **Metrics**: Check Prometheus/Grafana for anomalies

5. **Verify Diagnostics:**
   - Use `mcp__ide__getDiagnostics` to check language server
   - Ensure the error is gone and no new issues introduced
   - Check both C# and TypeScript diagnostics if applicable

## Phase 6: Documentation

1. **Create Issue Documentation:**
   - Create a markdown file in `docs/issue/` with:
     - Error description and reproduction steps
     - Root cause analysis
     - Solution explanation
     - Preventive measures added
     - Library references from Context7 (if applicable)
   - Format: `docs/issue/<issue-id>-<short-description>.md`

2. **Update Code Comments:**
   - Add comments explaining the fix
   - Reference the issue documentation
   - Explain why the solution prevents future occurrences

3. **Generate Summary:**
   - Provide a concise summary to the user:
     - What was the error
     - What caused it
     - How it was fixed
     - What prevents it from happening again
     - Files changed
     - Tests added

## MCP Tools Usage Summary

- **Context7 (`mcp__upstash-context-7-mcp__*`)**: Get up-to-date library documentation for ANY framework/library
- **IDE (`mcp__ide__getDiagnostics`)**: Get language server diagnostics (TypeScript, C#, etc.)
- **Magic (`mcp__magic__*`)**: UI component generation, transformation, and analysis (React, Vue, Svelte)
  - `magic_generate`: Generate UI components from description
  - `magic_transform`: Transform UI components (e.g., MUI → Tailwind)
  - `magic_analyze`: Analyze UI component structure, accessibility, patterns
  - **Use ONLY for UI component errors**, not for backend/database/Docker errors
- **Sequential (`mcp__sequential__*`)**: For multi-step debugging workflows (optional)
- **Memory Bank (`mcp__aakarsh-sasi-memory-bank-mcp__track_progress`)**: Track debugging progress (optional)

## Quality Checklist

Before completing, ensure:
- [ ] Error is fully understood and documented
- [ ] Library documentation consulted via Context7
- [ ] Error successfully reproduced
- [ ] Root cause identified with evidence
- [ ] Fix implemented following best practices
- [ ] Tests added to prevent regression
- [ ] All tests pass
- [ ] No new diagnostics/warnings
- [ ] Documentation created in docs/issue/
- [ ] Code comments explain the fix
- [ ] User summary provided

## Workflow Adaptation by Error Type

The command adapts its approach based on the error type:

### Backend C# Error
```
/debug System.NullReferenceException in RagService.SearchAsync
```
- Context7: ASP.NET Core, Entity Framework Core
- Replication: xUnit integration test
- Fix: Null checks, validation
- Verification: `dotnet test`, check Seq logs

### Frontend TypeScript Error
```
/debug TS2339: Property 'chatId' does not exist on type 'StreamingResponse'
```
- Context7: TypeScript, React, Next.js
- Replication: Jest unit test
- Fix: Update type definition
- Verification: `pnpm typecheck`, `pnpm test`

### Database Migration Error
```
/debug Npgsql.PostgresException: 42P01: relation "game_rules" does not exist
```
- Context7: PostgreSQL, Npgsql, Entity Framework Core
- Replication: EF Core migration scenario
- Fix: Create/update migration
- Verification: `dotnet ef database update`, integration tests

### Docker Error
```
/debug docker: Error response from daemon: port is already allocated
```
- Context7: Docker
- Replication: docker-compose scenario
- Fix: Update port mapping, check conflicts
- Verification: `docker compose up`, verify services

### API Integration Error
```
/debug CORS policy: No 'Access-Control-Allow-Origin' header
```
- Context7: ASP.NET Core, CORS
- Replication: Integration test with CORS headers
- Fix: Update CORS configuration in Program.cs
- Verification: Test from frontend, check network tab

## Notes

- **Universal Coverage**: This command works with ANY error in the monorepo (backend, frontend, database, Docker, CI/CD, etc.)
- Use TodoWrite to track progress through phases
- Be thorough in analysis - don't skip to solutions
- Always add tests to prevent regression
- **Context7 is key**: Consult up-to-date library docs before implementing fixes
- Document learnings for future reference in `docs/issue/`
- If error is critical or recurring, consider creating a GitHub issue via github-project-manager MCP
- Adapt the workflow based on error type - not all phases apply to all errors
- For simple errors (typos, missing imports), phases can be abbreviated
- For complex errors (race conditions, performance), use all MCP tools available

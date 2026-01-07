# Test Generation Guide - Issue #2308 Week 4

**Objective**: Generate 40-50 comprehensive backend handler tests efficiently using automated templates.

## Overview

This test generation system analyzes C# CommandHandler files and generates comprehensive unit test suites with:
- Null command validation test
- Valid command success test
- Validation error tests (for each required property)
- Repository/Service exception test

## Prerequisites

1. **dotnet-script** (required for C# script execution):
   ```bash
   dotnet tool install -g dotnet-script
   ```

2. **Verify installation**:
   ```bash
   dotnet script --version
   ```

## Files

| File | Purpose |
|------|---------|
| `TestGenerator.csx` | Core C# script that analyzes handlers and generates tests |
| `GenerateAllTests.ps1` | PowerShell batch processor for multiple handlers |
| `TEST_GENERATION_GUIDE.md` | This guide |

## Usage Patterns

### Pattern 1: Generate Single Test File

Generate tests for one specific handler:

```bash
# Basic usage
dotnet script scripts/TestGenerator.csx path/to/Handler.cs

# Specify output directory
dotnet script scripts/TestGenerator.csx path/to/Handler.cs tests/output/

# Example: Generate tests for RegisterCommandHandler
dotnet script scripts/TestGenerator.csx `
  ./apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Registration/RegisterCommandHandler.cs `
  ./apps/api/tests/Api.Tests/BoundedContexts/Authentication/Commands/Registration/
```

**Output**:
- `RegisterCommandHandlerTests.cs` with 5-8 tests
- Proper namespace, usings, and test structure

### Pattern 2: Batch Generate Tests (Recommended)

Generate tests for all priority handlers:

```powershell
# Dry-run to preview what will be generated
./scripts/GenerateAllTests.ps1 -DryRun

# Generate all tests (prompts for overwrite confirmation)
./scripts/GenerateAllTests.ps1

# Generate with verbose output
./scripts/GenerateAllTests.ps1 -Verbose

# Generate without overwrite prompts (careful!)
./scripts/GenerateAllTests.ps1 -Force
```

**Output**:
- 15 test files (Phase 1)
- ~90-150 total tests generated
- Organized by Bounded Context

### Pattern 3: Custom Handler List

Create your own handler list and process it:

```powershell
# Create custom list
@(
    ".\apps\api\src\Api\BoundedContexts\CustomContext\Handler1.cs",
    ".\apps\api\src\Api\BoundedContexts\CustomContext\Handler2.cs"
) | Out-File custom-handlers.txt

# Generate from custom list
./scripts/GenerateAllTests.ps1 -HandlerListFile custom-handlers.txt
```

## Generated Test Structure

Each generated test file follows this pattern:

```csharp
namespace Api.Tests.BoundedContexts.{Context}.{SubPath};

/// <summary>
/// Tests for {Handler} - Issue #2308 Week 4.
/// Auto-generated tests with branch coverage for all validation paths.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "{Context}")]
[Trait("Issue", "2308")]
public class {Handler}Tests
{
    // Mock dependencies
    private readonly Mock<IDependency> _mockDependency;
    private readonly {Handler} _handler;

    // Constructor with mock setup
    public {Handler}Tests() { ... }

    // Test 1: Null command validation
    [Fact]
    public async Task Handle_WithNullCommand_ShouldThrowArgumentNullException() { ... }

    // Test 2: Valid command success
    [Fact]
    public async Task Handle_WithValidCommand_ShouldSucceed() { ... }

    // Test 3-N: Validation errors for each required property
    [Fact]
    public async Task Handle_WithEmptyGuid{Property}_ShouldThrowArgumentException() { ... }

    [Fact]
    public async Task Handle_WithNull{Property}_ShouldThrowArgumentException() { ... }

    // Test N+1: Repository/Service exception
    [Fact]
    public async Task Handle_WhenServiceThrowsException_ShouldPropagateException() { ... }
}
```

## Handler Analysis

The generator automatically analyzes:

### 1. **Command Properties** (from validation logic)
- Guid properties: Detects `command.PropertyId == Guid.Empty` checks
- String properties: Detects `string.IsNullOrWhiteSpace(command.Property)` checks
- Other properties: Inferred from `command.Property` access patterns

### 2. **Dependencies** (from constructor)
- Repositories: `IRepository` interfaces
- Services: `IService` interfaces
- UnitOfWork: `IUnitOfWork` interface
- Loggers: `ILogger<T>` interface

### 3. **Return Type** (from Handle method)
- Void: `Task` or `Task<Unit>`
- Non-void: `Task<TResult>`

### 4. **Bounded Context** (from namespace)
- Extracted from: `Api.BoundedContexts.{Context}.Application...`

## Manual Refinement Required

After generation, manually review and adjust:

### 1. **Mock Method Names**
The generator uses placeholder method names. Replace with actual:

```csharp
// Generated (placeholder)
.Setup(s => s.MethodAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))

// Manual refinement
.Setup(s => s.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
```

### 2. **Return Value Construction**
Adjust mock return values to match actual types:

```csharp
// Generated (placeholder)
var mockResult = new SomeResult();

// Manual refinement
var mockResult = new TotpSetupResponse
{
    Secret = "BASE32SECRET",
    QrCodeUrl = "otpauth://...",
    BackupCodes = new List<string> { "12345678" }
};
```

### 3. **Domain-Specific Logic**
Add tests for domain business rules not detected by static analysis:

```csharp
// Example: Add test for "same password" business rule
[Fact]
public async Task Handle_WhenNewPasswordSameAsOld_ShouldThrowDomainException()
{
    // Domain-specific test logic
}
```

### 4. **Assertion Refinement**
Enhance assertions with specific expectations:

```csharp
// Generated (basic)
result.Should().NotBeNull();

// Manual refinement
result.Should().NotBeNull();
result.Secret.Should().MatchRegex(@"^[A-Z2-7]+$"); // Base32 format
result.BackupCodes.Should().HaveCount(8);
result.BackupCodes.Should().OnlyContain(c => c.Length == 8);
```

## Priority Handler List (Phase 1)

**15 handlers → ~90-150 tests**

### Authentication (6 handlers)
1. ✅ RegisterCommandHandler
2. ✅ LoginCommandHandler
3. ✅ CreateApiKeyCommandHandler
4. ✅ RotateApiKeyCommandHandler
5. ✅ CreateSessionCommandHandler
6. ✅ RevokeSessionCommandHandler

### Game Management (4 handlers)
7. ✅ StartGameSessionCommandHandler
8. ✅ EndGameSessionCommandHandler
9. ✅ CreateGameFAQCommandHandler
10. ✅ UpdateGameFAQCommandHandler

### Knowledge Base (3 handlers)
11. ✅ CreateChatThreadCommandHandler
12. ✅ AddMessageCommandHandler
13. ✅ CreateAgentCommandHandler

### Administration (2 handlers)
14. ✅ CreateUserCommandHandler
15. ✅ ChangeUserRoleCommandHandler

## Phase 2 Expansion (Additional 25-35 handlers)

Uncomment handlers in `GenerateAllTests.ps1` to expand coverage:
- Authentication: +5 handlers (OAuth, Password Reset, API Key Management)
- Game Management: +8 handlers (Session lifecycle, Editor locks, Rules)
- Knowledge Base: +5 handlers (Thread management, Agents)
- System Configuration: +4 handlers (Config management, Cache)
- Administration: +8 handlers (User management, Alerts, Reports)

## Quality Checks

After generation, verify:

### 1. **Compilation**
```bash
dotnet build apps/api/tests/Api.Tests
```

### 2. **Test Execution**
```bash
# Run all generated tests
dotnet test --filter "Issue=2308"

# Run specific bounded context
dotnet test --filter "BoundedContext=Authentication&Issue=2308"
```

### 3. **Coverage Analysis**
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

Target: **90%+ branch coverage** for generated handlers

## Workflow Integration

### 1. **Generation Phase**
```bash
# Generate all priority tests
./scripts/GenerateAllTests.ps1

# Review generated files
git diff apps/api/tests/
```

### 2. **Refinement Phase**
- Review each generated test file
- Refine mock setups and assertions
- Add domain-specific test cases
- Verify compilation

### 3. **Validation Phase**
```bash
# Run tests
dotnet test --filter "Issue=2308"

# Check coverage
dotnet test /p:CollectCoverage=true

# Verify quality
dotnet test --logger:"console;verbosity=detailed"
```

### 4. **Integration Phase**
```bash
# Add to version control
git add apps/api/tests/
git commit -m "Week 4: Add auto-generated backend handler tests (Issue #2308)"

# Update issue progress
# Document test count and coverage improvements
```

## Troubleshooting

### Issue: dotnet-script not found
```bash
# Install globally
dotnet tool install -g dotnet-script

# Verify installation
dotnet script --version
```

### Issue: Handler analysis fails
- **Cause**: Handler doesn't follow expected CQRS pattern
- **Fix**: Generate minimal template and refine manually

### Issue: Tests don't compile
- **Cause**: Placeholder method names or incorrect types
- **Fix**: Manual refinement of mock setups (expected, part of workflow)

### Issue: Low test coverage
- **Cause**: Generated tests cover only validation paths
- **Fix**: Add domain-specific business logic tests manually

## Performance Metrics

**Expected Generation Performance**:
- Single handler: ~2-5 seconds
- 15 handlers (Phase 1): ~30-75 seconds
- 50 handlers (Full): ~100-250 seconds

**Expected Test Output**:
- Per handler: 5-8 tests (avg 6.5)
- Phase 1 (15 handlers): ~90-120 tests
- Full (50 handlers): ~300-400 tests

## Best Practices

1. **Review Before Commit**: Always manually review generated tests
2. **Incremental Generation**: Generate in small batches, refine, then continue
3. **Domain Knowledge**: Add business rule tests manually after generation
4. **Coverage Monitoring**: Track coverage improvements after test addition
5. **Documentation**: Update Issue #2308 with test counts and coverage gains

## Example Session

```powershell
# 1. Dry-run to preview
./scripts/GenerateAllTests.ps1 -DryRun

# Output:
# 📊 Processing 15 priority handlers...
# [1/15] RegisterCommandHandler.cs
#   Context: Authentication
#   📝 Would generate: tests/Api.Tests/.../RegisterCommandHandlerTests.cs
# ...
# ✅ Successful: 15, ❌ Failed: 0, ⏭️ Skipped: 0

# 2. Generate all tests
./scripts/GenerateAllTests.ps1

# 3. Review generated files
git diff apps/api/tests/

# 4. Refine mocks and assertions
# (Open generated files in IDE)

# 5. Verify compilation
dotnet build apps/api/tests/Api.Tests

# 6. Run tests
dotnet test --filter "Issue=2308"

# 7. Commit
git add apps/api/tests/
git commit -m "Week 4: Add 90+ auto-generated backend tests (Issue #2308)"
```

## Related Files

- **Existing Manual Tests**: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Commands/TwoFactor/GenerateTotpSetupCommandHandlerTests.cs`
- **Test Constants**: `apps/api/tests/Api.Tests/Constants/TestCategories.cs`
- **Project Reference**: `apps/api/tests/Api.Tests/Api.Tests.csproj`

## Support

For issues or questions:
1. Review existing manual tests for patterns
2. Check handler implementation for validation logic
3. Consult Issue #2308 for context and requirements
4. Refer to CLAUDE.md for project architecture

---

**Version**: 1.0
**Created**: 2026-01-07
**Issue**: #2308 Week 4 - Backend Handler Tests
**Target**: 40-50 handler tests, 90%+ coverage

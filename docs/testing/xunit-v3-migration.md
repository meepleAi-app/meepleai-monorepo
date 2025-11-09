# xUnit v3 Migration Guide

**Migration Date**: 2025-11-09
**Issue**: #819
**PR**: #828

## Overview

Migrated test project from xUnit v2.9.3 to xUnit v3.2.0 to enable native AssemblyFixture support for test process cleanup.

## Why Migrate?

xUnit v3 provides:
- **Native AssemblyFixture**: No external extension packages needed
- **Better Performance**: Tests run as executables, not libraries
- **Modern Architecture**: Designed for .NET 9+ with improved parallelization
- **Simplified API**: Reduced complexity in test framework

## Package Changes

### Before (v2)
```xml
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
```

### After (v3)
```xml
<PackageReference Include="xunit.v3" Version="3.2.0" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
```

**Note**: `xunit.runner.visualstudio` v3.1.5 is compatible with xUnit v3.2.0

## Breaking Changes Fixed

### 1. Namespace Migration

**Change**: `Xunit.Abstractions` removed, types moved to `Xunit`

```diff
- using Xunit.Abstractions;
+ using Xunit;  // ITestOutputHelper now in root namespace
```

**Affected**: All test files using `ITestOutputHelper` (189 files)

### 2. IAsyncLifetime Signature Changes

**Change**: Return type changed from `Task` to `ValueTask`

```diff
- public async Task InitializeAsync()
+ public async ValueTask InitializeAsync()

- public async Task DisposeAsync()
+ public async ValueTask DisposeAsync()
```

**Reason**: IAsyncLifetime now inherits from `IAsyncDisposable` in v3

**Affected**:
- All test base classes (`IntegrationTestBase`, `PostgresIntegrationTestBase`, `QdrantIntegrationTestBase`, etc.)
- Test fixtures (`AdminTestFixture`, `QdrantRagTestFixture`, etc.)
- Integration tests with explicit lifecycle

### 3. Completion Methods

```diff
- return Task.CompletedTask;
+ return ValueTask.CompletedTask;
```

**Affected**: Non-async `InitializeAsync()` methods

### 4. Method Modifiers

**Virtual Methods**: Base classes need `virtual` for overrides

```diff
- public async ValueTask InitializeAsync()
+ public virtual async ValueTask InitializeAsync()
```

**Override Methods**: Derived classes use `override`

```diff
- async ValueTask IAsyncLifetime.InitializeAsync()  // Explicit interface
+ public override async ValueTask InitializeAsync()  // Override base
```

### 5. Explicit Interface Implementations

**Change**: Explicit interface implementations no longer work for `IAsyncLifetime.DisposeAsync()`

```diff
- async ValueTask IAsyncLifetime.DisposeAsync()  // ❌ Not valid in v3
+ public override async ValueTask DisposeAsync()  // ✅ Correct
```

**Reason**: IAsyncLifetime inherits from IAsyncDisposable, so explicit implementation is ambiguous

## Migration Process

### Automated Migration Script

Created bash script to handle bulk changes:

```bash
#!/bin/bash
PROJECT_PATH="apps/api/tests/Api.Tests"

# 1. Fix namespace
find "$PROJECT_PATH" -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*" \
  -exec sed -i 's/using Xunit\.Abstractions;/using Xunit;/g' {} +

# 2-7. Fix IAsyncLifetime signatures (see tools/migrate-xunit-v3.sh for full script)
```

### Manual Fixes Required

1. **Base class virtuals**: Added `virtual` to base class methods
2. **Explicit interfaces**: Changed to `override` methods
3. **Task.CompletedTask**: Changed to `ValueTask.CompletedTask`

## Assembly Fixture Enablement

### AssemblyInfo.cs

```csharp
// Enable assembly-wide test process cleanup (xUnit v3 native support)
[assembly: AssemblyFixture(typeof(Api.Tests.TestProcessCleanup))]
```

### How It Works

1. **Before all tests**: xUnit v3 creates `TestProcessCleanup` instance
2. **After all tests**: xUnit v3 calls `TestProcessCleanup.Dispose()`
3. **Cleanup script**: PowerShell script kills hanging test processes (Windows only)

## Testing Results

### Local Testing
- ✅ Build: Zero compilation errors
- ✅ Sample test: 24/24 AdminAuthorizationTests passed
- ✅ AssemblyFixture: Enabled and functional

### CI Testing
- ⏳ Pending: Ubuntu with Testcontainers
- 🎯 Expected: All existing tests should pass

## Files Modified

**Total**: 189 files (180 test files + 9 supporting files)

**Categories**:
- Test classes: 150+ files
- Base classes: 5 files (IntegrationTestBase, PostgresIntegrationTestBase, QdrantIntegrationTestBase, AdminTestFixture, TransactionalTestBase)
- Fixtures: 3 files
- Helpers: 10+ files
- Integration tests: 20+ files

## Common Migration Patterns

### Pattern 1: Simple Test Class

```diff
  using System;
- using Xunit.Abstractions;
+ using Xunit;

  public class MyTests
  {
      private readonly ITestOutputHelper _output;

      public MyTests(ITestOutputHelper output)
      {
          _output = output;
      }
  }
```

### Pattern 2: Test with IAsyncLifetime

```diff
  public class MyIntegrationTests : IAsyncLifetime
  {
-     public async Task InitializeAsync()
+     public async ValueTask InitializeAsync()
      {
          // Setup code
      }

-     public async Task DisposeAsync()
+     public async ValueTask DisposeAsync()
      {
          // Cleanup code
      }
  }
```

### Pattern 3: Test Base Class

```diff
  public abstract class MyTestBase : IAsyncLifetime
  {
-     public virtual Task InitializeAsync()
+     public virtual ValueTask InitializeAsync()
      {
-         return Task.CompletedTask;
+         return ValueTask.CompletedTask;
      }

-     public virtual async Task DisposeAsync()
+     public virtual async ValueTask DisposeAsync()
      {
          // Cleanup
      }
  }
```

### Pattern 4: Derived Test Class

```diff
  public class MyDerivedTests : MyTestBase
  {
-     public override async Task InitializeAsync()
+     public override async ValueTask InitializeAsync()
      {
          await base.InitializeAsync();
          // Additional setup
      }
  }
```

## Known Issues

### xUnit v3 Warnings

Some analyzer warnings introduced in v3:
- `xUnit1051`: Prefer `TestContext.Current.CancellationToken` for cancellation
- These are recommendations, not errors

### Line Ending Warnings

Git warnings about LF → CRLF conversions:
- Cosmetic issue from sed script usage
- Does not affect functionality
- Files will normalize on next commit

## Rollback Plan

If critical issues arise:

1. **Revert package changes**:
   ```xml
   <PackageReference Include="xunit" Version="2.9.3" />
   ```

2. **Revert code changes**: `git revert <commit-hash>`

3. **Alternative**: Use `Xunit.Extensions.AssemblyFixture` v2.6.0 with xUnit v2

## References

- [xUnit v3 What's New](https://xunit.net/docs/getting-started/v3/whats-new)
- [xUnit v3 Migration Guide](https://xunit.net/docs/getting-started/v3/migration)
- [xUnit v3 NuGet Package](https://www.nuget.org/packages/xunit.v3)
- [AssemblyFixture Documentation](https://xunit.net/docs/shared-context#assembly-fixtures)

## Future Improvements

Potential v3 features to explore:
- **Test Context**: `TestContext.Current` for cancellation tokens
- **Console Capture**: Assembly-level attributes for console/debug/trace output
- **Test Pipeline Startup**: Very early initialization hooks
- **Executable Tests**: Run tests with `dotnet run` instead of `dotnet test`

## Lessons Learned

1. **Estimate Scope**: Initial estimate was "10 minutes", actual was "2 hours" due to 195 breaking changes
2. **Automated Migration**: Bash/sed scripts essential for bulk refactoring
3. **Incremental Verification**: Fix compilation errors in batches, verify frequently
4. **Pattern Recognition**: Identified common patterns and automated fixes
5. **Test Base Classes**: Virtual modifiers critical for override chain

## Team Notes

- **New Tests**: Use `ValueTask` for `IAsyncLifetime` methods
- **Test Context**: Consider using `TestContext.Current.CancellationToken`
- **Assembly Fixtures**: Available for shared state across entire test assembly
- **Collection Fixtures**: Still available for shared state within collection

---

**Migration Completed**: 2025-11-09
**Migrated By**: Claude Code (AI Assistant)
**Verified By**: CI Pipeline (pending)

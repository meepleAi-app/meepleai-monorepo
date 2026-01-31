# Bash to PowerShell Migration Guide

**Status**: All scripts have been migrated to PowerShell for Windows compatibility.

## Migration Summary

| Bash Script | PowerShell Equivalent | Status |
|-------------|----------------------|--------|
| `deployment/deploy-meepleai.sh` | `deployment/deploy-meepleai.ps1` | ✅ Migrated |
| `monitoring/docker-resource-monitor.sh` | `monitoring/docker-resource-monitor.ps1` | ✅ Migrated |
| `quality/validate-doc-links.sh` | `quality/validate-doc-links.ps1` | ✅ Migrated |
| `testing/test-oauth-health.sh` | `testing/test-oauth-health.ps1` | ✅ Migrated |
| `testing/test-runbooks.sh` | `testing/test-runbooks.ps1` | ✅ Migrated |
| `testing/test-services.sh` | `testing/test-services.ps1` | ✅ Migrated |

## Why PowerShell?

1. **Windows Native**: PowerShell Core is installed by default on Windows 10/11
2. **Cross-Platform**: PowerShell Core (pwsh) runs on Windows, Linux, macOS
3. **Better Windows Integration**: Native access to .NET Framework and Windows APIs
4. **Structured Data**: Objects instead of text parsing
5. **Strong Typing**: Parameter validation and type safety
6. **Modern Tooling**: Better IDE support in VS Code

## Key Differences

### Parameter Syntax

**Bash:**
```bash
./script.sh --watch 5 --threshold 90
```

**PowerShell:**
```powershell
.\script.ps1 -Watch -WatchInterval 5 -Threshold 90
```

### Color Output

**Bash:**
```bash
RED='\033[0;31m'
echo -e "${RED}Error${NC}"
```

**PowerShell:**
```powershell
Write-Host "Error" -ForegroundColor Red
```

### Error Handling

**Bash:**
```bash
set -e
if [ $? -ne 0 ]; then
    echo "Error"
    exit 1
fi
```

**PowerShell:**
```powershell
$ErrorActionPreference = 'Stop'
try {
    # operations
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}
```

### HTTP Requests

**Bash:**
```bash
curl -s "$URL" | jq '.field'
```

**PowerShell:**
```powershell
$response = Invoke-RestMethod -Uri $URL
$response.field
```

### Docker Commands

**Bash:**
```bash
docker exec container-name command
```

**PowerShell:**
```powershell
docker exec container-name command  # Same syntax!
```

## Migration Checklist

When migrating a bash script to PowerShell:

- [ ] Convert parameters to `[CmdletBinding()]` and `param()` block
- [ ] Replace `echo` with `Write-Host` for colored output
- [ ] Replace `curl` with `Invoke-RestMethod` or `Invoke-WebRequest`
- [ ] Replace `jq` with native PowerShell object access
- [ ] Replace `grep` with `Select-String` or `Where-Object`
- [ ] Replace `sed`/`awk` with `-replace` operator
- [ ] Use `Join-Path` for cross-platform path handling
- [ ] Test on both Windows PowerShell 5.1 and PowerShell Core 7+
- [ ] Add parameter validation with `[ValidateSet()]`, `[ValidateRange()]`, etc.
- [ ] Update documentation and usage examples

## Running PowerShell Scripts

### Windows (PowerShell 5.1+)
```powershell
.\scripts\deployment\deploy-meepleai.ps1 up
```

### Windows/Linux/macOS (PowerShell Core)
```bash
pwsh ./scripts/deployment/deploy-meepleai.ps1 up
```

### Execution Policy (Windows)
If you get "execution policy" errors:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

## Bash Scripts Status

**Deprecated**: The following bash scripts are deprecated and will be removed in a future release:
- `deployment/deploy-meepleai.sh` → Use `deploy-meepleai.ps1`
- `monitoring/docker-resource-monitor.sh` → Use `docker-resource-monitor.ps1`
- `quality/validate-doc-links.sh` → Use `validate-doc-links.ps1`
- `testing/test-oauth-health.sh` → Use `test-oauth-health.ps1`
- `testing/test-runbooks.sh` → Use `test-runbooks.ps1`
- `testing/test-services.sh` → Use `test-services.ps1`

**Kept for Linux/macOS**: Bash scripts remain available for non-Windows environments, but are no longer actively maintained. PowerShell Core is recommended for all platforms.

## Resources

- [PowerShell Documentation](https://docs.microsoft.com/en-us/powershell/)
- [PowerShell Core (Cross-Platform)](https://github.com/PowerShell/PowerShell)
- [Bash to PowerShell Cheat Sheet](https://mathieubuisson.github.io/powershell-linux-bash/)

---

**Last Updated:** 2026-01-24

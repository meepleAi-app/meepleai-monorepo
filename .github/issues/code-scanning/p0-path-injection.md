---
title: "[SECURITY] Fix Path Injection and User-Controlled Bypass (4 instances)"
labels: ["security", "priority-critical", "P0", "code-scanning", "path-traversal"]
---

## Summary

**4 open ERROR alerts** for path injection vulnerabilities where user-controlled input is used to construct file paths, enabling directory traversal and arbitrary file access.

### Impact
- **Severity**: 🔴 **ERROR** (Critical Priority - P0)
- **CWE**: [CWE-22: Improper Limitation of a Pathname to a Restricted Directory](https://cwe.mitre.org/data/definitions/22.html)
- **CWE**: [CWE-73: External Control of File Name or Path](https://cwe.mitre.org/data/definitions/73.html)
- **OWASP**: [A01:2021 – Broken Access Control](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)
- **Risk**: Arbitrary file read/write, source code disclosure, configuration exposure, data breach
- **Production Impact**: CRITICAL - Attackers can read sensitive files (/etc/passwd, config files, source code) or write to arbitrary locations

---

## Problem

User-controlled input is directly used to construct file paths without validation, allowing attackers to:
1. **Directory traversal** (`../../../etc/passwd`)
2. **Absolute path injection** (`/etc/passwd`, `C:\Windows\System32\config\SAM`)
3. **Read sensitive files** (config, source code, database)
4. **Write to arbitrary locations** (overwrite code, create backdoors)
5. **Bypass security checks** (access control, file type restrictions)

### Example Vulnerable Code

```csharp
// ❌ BAD: User-controlled filename without validation
public async Task<Stream> GetPdfAsync(string filename)
{
    var path = Path.Combine(_pdfDirectory, filename);
    return File.OpenRead(path);
    // Attack: filename = "../../appsettings.json"
    // Result: Reads sensitive config file
}

// ❌ BAD: User-controlled path component
[HttpGet("download/{*filepath}")]
public IActionResult DownloadFile(string filepath)
{
    var fullPath = Path.Combine(_dataDirectory, filepath);
    return PhysicalFile(fullPath, "application/octet-stream");
    // Attack: filepath = "../../../etc/passwd"
    // Result: Reads system file
}

// ❌ BAD: Filename from upload without sanitization
public async Task<string> SaveUploadAsync(IFormFile file)
{
    var path = Path.Combine(_uploadDirectory, file.FileName);
    using var stream = File.Create(path);
    await file.CopyToAsync(stream);
    return path;
    // Attack: file.FileName = "../../Program.cs"
    // Result: Overwrites source code!
}

// ❌ BAD: Building path from multiple user inputs
public async Task<string> GetGameFileAsync(string gameId, string fileType)
{
    var path = $"{_gameDirectory}/{gameId}/{fileType}.json";
    return await File.ReadAllTextAsync(path);
    // Attack: fileType = "../../../secrets/apikeys"
    // Result: Reads secret keys
}
```

### Attack Examples

```csharp
// Example 1: Directory Traversal (Unix)
filename = "../../../etc/passwd";
// Resolves to: /var/www/app/pdfs/../../../etc/passwd
//           => /etc/passwd

// Example 2: Directory Traversal (Windows)
filename = "..\\..\\..\\Windows\\System32\\config\\SAM";
// Resolves to: C:\app\uploads\..\..\..\Windows\System32\config\SAM
//           => C:\Windows\System32\config\SAM

// Example 3: Absolute Path Injection
filename = "/etc/shadow";
// Ignores _pdfDirectory, reads /etc/shadow directly

// Example 4: Null Byte Injection (legacy .NET Framework)
filename = "allowed.pdf\0../../secrets.txt";
// Bypasses extension checks

// Example 5: Unicode/Encoding Bypass
filename = "%2e%2e%2f%2e%2e%2f%2e%2e%2fetc%2fpasswd"; // URL-encoded ../
filename = "..%252f..%252f..%252fetc%252fpasswd"; // Double-encoded
```

### Secure Solution

```csharp
// ✅ GOOD: Whitelist allowed filenames (best approach)
public async Task<Stream> GetPdfAsync(Guid pdfId)
{
    // Use database-controlled IDs, not user filenames
    var pdf = await _dbContext.Pdfs.FindAsync(pdfId);
    if (pdf == null)
        throw new NotFoundException("PDF not found");

    var safePath = Path.Combine(_pdfDirectory, $"{pdf.Id}.pdf");

    // Verify path is within allowed directory
    var fullPath = Path.GetFullPath(safePath);
    var allowedDirectory = Path.GetFullPath(_pdfDirectory);

    if (!fullPath.StartsWith(allowedDirectory, StringComparison.OrdinalIgnoreCase))
        throw new SecurityException("Path traversal detected");

    return File.OpenRead(fullPath);
}

// ✅ GOOD: Sanitize filename and validate
public async Task<string> SaveUploadAsync(IFormFile file)
{
    // Validate file type
    var allowedExtensions = new[] { ".pdf", ".png", ".jpg" };
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

    if (!allowedExtensions.Contains(extension))
        throw new ValidationException("Invalid file type");

    // Generate safe filename (don't trust user input)
    var safeFilename = $"{Guid.NewGuid()}{extension}";
    var safePath = Path.Combine(_uploadDirectory, safeFilename);

    // Double-check resolved path is safe
    var fullPath = Path.GetFullPath(safePath);
    var allowedDirectory = Path.GetFullPath(_uploadDirectory);

    if (!fullPath.StartsWith(allowedDirectory, StringComparison.OrdinalIgnoreCase))
        throw new SecurityException("Path traversal detected");

    // Save file
    using var stream = File.Create(fullPath);
    await file.CopyToAsync(stream);

    return safeFilename; // Return safe filename, not full path
}

// ✅ GOOD: Use resource IDs instead of filenames
[HttpGet("games/{gameId}/rules")]
public async Task<IActionResult> GetGameRules(Guid gameId)
{
    // Look up in database by ID
    var game = await _dbContext.Games
        .Include(g => g.RuleSpecs)
        .FirstOrDefaultAsync(g => g.Id == gameId);

    if (game == null)
        return NotFound();

    // Construct path using trusted data only
    var safePath = Path.Combine(_gameDirectory, $"{game.Id}.json");

    if (!System.IO.File.Exists(safePath))
        return NotFound();

    return PhysicalFile(safePath, "application/json");
}
```

---

## Path Security Utility

Create a centralized path validation utility:

```csharp
// Infrastructure/Security/PathSecurity.cs
public static class PathSecurity
{
    /// <summary>
    /// Validates that resolved path is within allowed directory (prevents directory traversal)
    /// </summary>
    /// <exception cref="SecurityException">Thrown if path escapes allowed directory</exception>
    public static string ValidatePathIsInDirectory(string basePath, string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            throw new ArgumentException("Filename cannot be empty", nameof(filename));

        // Combine and resolve to absolute path
        var combinedPath = Path.Combine(basePath, filename);
        var fullPath = Path.GetFullPath(combinedPath);

        // Get canonical base directory path
        var baseDirectory = Path.GetFullPath(basePath);

        // Verify resolved path is within base directory
        if (!fullPath.StartsWith(baseDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityException(
                $"Path traversal detected: '{filename}' resolves outside allowed directory");
        }

        return fullPath;
    }

    /// <summary>
    /// Sanitizes filename by removing dangerous characters
    /// </summary>
    public static string SanitizeFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            throw new ArgumentException("Filename cannot be empty", nameof(filename));

        // Remove path separators and dangerous characters
        var invalidChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' })
            .Distinct()
            .ToArray();

        var sanitized = new string(filename
            .Where(c => !invalidChars.Contains(c))
            .ToArray());

        // Remove leading/trailing dots and spaces
        sanitized = sanitized.Trim('.', ' ');

        if (string.IsNullOrWhiteSpace(sanitized))
            throw new ArgumentException("Filename contains only invalid characters", nameof(filename));

        return sanitized;
    }

    /// <summary>
    /// Validates file extension against whitelist
    /// </summary>
    public static void ValidateFileExtension(string filename, params string[] allowedExtensions)
    {
        var extension = Path.GetExtension(filename).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
        {
            throw new ValidationException(
                $"Invalid file extension: '{extension}'. Allowed: {string.Join(", ", allowedExtensions)}");
        }
    }

    /// <summary>
    /// Generates a safe random filename with original extension
    /// </summary>
    public static string GenerateSafeFilename(string originalFilename)
    {
        var extension = Path.GetExtension(originalFilename);
        var sanitizedExtension = SanitizeFilename(extension);
        return $"{Guid.NewGuid()}{sanitizedExtension}";
    }

    /// <summary>
    /// Checks if path exists and is within allowed directory
    /// </summary>
    public static bool SafeFileExists(string basePath, string filename)
    {
        try
        {
            var safePath = ValidatePathIsInDirectory(basePath, filename);
            return File.Exists(safePath);
        }
        catch (SecurityException)
        {
            return false;
        }
    }
}
```

### Usage Examples

```csharp
// Example 1: Validate path before reading
public async Task<Stream> GetPdfAsync(string filename)
{
    var safePath = PathSecurity.ValidatePathIsInDirectory(_pdfDirectory, filename);
    // Throws SecurityException if path traversal detected
    return File.OpenRead(safePath);
}

// Example 2: Sanitize uploaded filename
public async Task<string> SaveUploadAsync(IFormFile file)
{
    PathSecurity.ValidateFileExtension(file.FileName, ".pdf", ".png", ".jpg");

    var safeFilename = PathSecurity.GenerateSafeFilename(file.FileName);
    var safePath = PathSecurity.ValidatePathIsInDirectory(_uploadDirectory, safeFilename);

    using var stream = File.Create(safePath);
    await file.CopyToAsync(stream);

    return safeFilename;
}

// Example 3: Check file exists safely
public bool PdfExists(string filename)
{
    return PathSecurity.SafeFileExists(_pdfDirectory, filename);
}
```

---

## Vulnerable Code Locations

### 1. PDF Storage Service

**File**: `PdfStorageService.cs`

```csharp
// ❌ FIND patterns like:
Path.Combine(_pdfDirectory, userProvidedFilename)
Path.Combine(_uploadPath, request.FileName)

// ✅ FIX with:
PathSecurity.ValidatePathIsInDirectory(_pdfDirectory, filename)
PathSecurity.GenerateSafeFilename(request.FileName)
```

### 2. File Download Endpoints

**File**: `Program.cs` (endpoints returning `PhysicalFile`)

```csharp
// ❌ FIND patterns like:
[HttpGet("download/{filename}")]
PhysicalFile(Path.Combine(_dir, filename), ...)

// ✅ FIX with:
var safePath = PathSecurity.ValidatePathIsInDirectory(_dir, filename);
PhysicalFile(safePath, ...)
```

### 3. File Upload Handlers

**File**: Any service handling `IFormFile`

```csharp
// ❌ FIND patterns like:
Path.Combine(_uploadDir, file.FileName)

// ✅ FIX with:
var safeFilename = PathSecurity.GenerateSafeFilename(file.FileName);
var safePath = PathSecurity.ValidatePathIsInDirectory(_uploadDir, safeFilename);
```

---

## Automated Detection

```bash
# Find Path.Combine with non-constant second argument
cd apps/api
rg 'Path\.Combine\([^,]+,\s*[^"$]' --glob '*.cs' -n

# Find PhysicalFile usage
rg 'PhysicalFile\(' --glob '*.cs' -n

# Find File.OpenRead/Create with Path.Combine
rg 'File\.(OpenRead|Create|OpenWrite).*Path\.Combine' --glob '*.cs' -n
```

---

## Remediation Plan

### Phase 1: Critical File Operations (0.5 days)
- [ ] `PdfStorageService.cs` - PDF file access
- [ ] `PdfTextExtractionService.cs` - PDF reading
- [ ] File download endpoints in `Program.cs`
- [ ] Implement `PathSecurity` utility

### Phase 2: File Uploads (0.5 days)
- [ ] All `IFormFile` upload handlers
- [ ] Temp file creation
- [ ] File validation

### Phase 3: Verification (0.5 day)
- [ ] Security audit of all file operations
- [ ] Penetration testing
- [ ] Code review

---

## Testing

### Unit Test Template

```csharp
[Theory]
[InlineData("../../etc/passwd")]
[InlineData("../../../Windows/System32/config/SAM")]
[InlineData("/etc/shadow")]
[InlineData("C:\\Windows\\System32\\config\\SAM")]
[InlineData("....//....//etc/passwd")] // Double encoding
public void ValidatePathIsInDirectory_ThrowsOnTraversal(string maliciousFilename)
{
    // Arrange
    var basePath = "/var/www/app/uploads";

    // Act & Assert
    Assert.Throws<SecurityException>(() =>
        PathSecurity.ValidatePathIsInDirectory(basePath, maliciousFilename)
    );
}

[Fact]
public void ValidatePathIsInDirectory_AllowsLegitimateFilename()
{
    // Arrange
    var basePath = "/var/www/app/uploads";
    var legitimateFilename = "document.pdf";

    // Act
    var result = PathSecurity.ValidatePathIsInDirectory(basePath, legitimateFilename);

    // Assert
    Assert.EndsWith("document.pdf", result);
    Assert.StartsWith(basePath, result);
}

[Theory]
[InlineData("test.pdf", ".pdf", ".png")] // Allowed
[InlineData("test.PDF", ".pdf", ".png")] // Case insensitive
public void ValidateFileExtension_AllowsWhitelistedExtensions(
    string filename, params string[] allowed)
{
    // Act & Assert (no exception)
    PathSecurity.ValidateFileExtension(filename, allowed);
}

[Theory]
[InlineData("test.exe", ".pdf", ".png")]
[InlineData("test.sh", ".pdf", ".png")]
public void ValidateFileExtension_RejectsNonWhitelisted(
    string filename, params string[] allowed)
{
    // Act & Assert
    Assert.Throws<ValidationException>(() =>
        PathSecurity.ValidateFileExtension(filename, allowed)
    );
}
```

### Integration Test

```csharp
[Fact]
public async Task DownloadFile_WithTraversalAttempt_Returns400()
{
    // Arrange
    var client = _factory.CreateClient();

    // Act - Try to download sensitive file
    var response = await client.GetAsync("/api/v1/files/../../appsettings.json");

    // Assert
    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    // OR HttpStatusCode.NotFound if path validation returns 404
}

[Fact]
public async Task UploadFile_WithMaliciousFilename_SanitizesFilename()
{
    // Arrange
    var client = _factory.CreateClient();
    var maliciousFilename = "../../backdoor.aspx";
    var content = new MultipartFormDataContent();
    var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3 });
    content.Add(fileContent, "file", maliciousFilename);

    // Act
    var response = await client.PostAsync("/api/v1/upload", content);

    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var result = await response.Content.ReadFromJsonAsync<UploadResult>();

    // Verify filename was sanitized (should be a GUID, not original filename)
    Assert.DoesNotContain("..", result.Filename);
    Assert.DoesNotContain("backdoor", result.Filename);
}
```

---

## Prevention Strategy

### 1. Always Use Database IDs for File Lookup

```csharp
// ❌ BAD: User provides filename
[HttpGet("pdfs/{filename}")]
public IActionResult GetPdf(string filename) { ... }

// ✅ GOOD: User provides ID, lookup filename in database
[HttpGet("pdfs/{id:guid}")]
public async Task<IActionResult> GetPdf(Guid id)
{
    var pdf = await _dbContext.Pdfs.FindAsync(id);
    var safePath = Path.Combine(_pdfDirectory, $"{pdf.Id}.pdf");
    // ...
}
```

### 2. Generate Safe Filenames for Uploads

```csharp
// ❌ BAD: Use original filename
var path = Path.Combine(_uploadDir, file.FileName);

// ✅ GOOD: Generate new filename
var safeFilename = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
var path = Path.Combine(_uploadDir, safeFilename);
```

### 3. Always Validate Resolved Path

```csharp
// After Path.Combine, ALWAYS validate:
var fullPath = Path.GetFullPath(combinedPath);
if (!fullPath.StartsWith(Path.GetFullPath(_baseDirectory)))
    throw new SecurityException("Path traversal detected");
```

### 4. Code Review Checklist

```markdown
## Security Review - Path Injection
- [ ] User input is NEVER directly used in file paths
- [ ] All filenames are validated with `PathSecurity.ValidatePathIsInDirectory()`
- [ ] File extensions are whitelisted
- [ ] Generated filenames use GUIDs, not user input
- [ ] All file operations log the validated path
- [ ] Database IDs used for file lookup instead of filenames
```

---

## Acceptance Criteria

- [ ] All 4 instances fixed with `PathSecurity` utility
- [ ] No user input directly used in file paths
- [ ] All file operations validate resolved paths
- [ ] File extension whitelist enforced
- [ ] Upload filenames are sanitized or generated
- [ ] Unit tests cover traversal attempts
- [ ] Integration tests verify endpoint security
- [ ] Security audit completed
- [ ] Penetration test passed

---

## Estimated Effort

- **Total Time**: 1 day (1 developer)
- **Complexity**: Medium (4 instances + utility + testing)
- **Risk**: CRITICAL (prevents arbitrary file access)

### Breakdown
- Phase 1 (Critical): 0.5 days
- Phase 2 (Uploads): 0.5 days
- Phase 3 (Verification): Included in testing

---

## References

- [CWE-22: Path Traversal](https://cwe.mitre.org/data/definitions/22.html)
- [CWE-73: External Control of File Name or Path](https://cwe.mitre.org/data/definitions/73.html)
- [OWASP: Path Traversal](https://owasp.org/www-community/attacks/Path_Traversal)
- [Microsoft: Security best practices for file I/O](https://learn.microsoft.com/en-us/dotnet/standard/io/file-security)

---

**Priority**: P0 - CRITICAL
**Category**: Security > Path Traversal
**Related Issues**: #[code-scanning-tracker]

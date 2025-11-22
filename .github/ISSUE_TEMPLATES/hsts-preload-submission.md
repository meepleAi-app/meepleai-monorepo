# 🔐 [LOW] Submit Domain to HSTS Preload List

## Summary

Submit MeepleAI domain to the HSTS preload list after 90 days of stable production operation, providing maximum protection against SSL stripping attacks and man-in-the-middle attacks.

**Related**: #1447 (SecurityHeadersMiddleware implementation)
**Priority**: 🟢 LOW
**Complexity**: Low
**Estimated Time**: 2-3 hours (mostly waiting for approval)

## Current State

HSTS is currently enabled with preload directive:
```
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
```

**Status**: Ready for preload submission, but not yet submitted
**Preload List**: Domain NOT on Chrome/Firefox/Safari preload list

## What is HSTS Preload?

HSTS Preload is a list of sites hardcoded into browsers (Chrome, Firefox, Safari, Edge) that should **only** be accessed over HTTPS. Once on the list:

- Browsers never attempt HTTP connection (even first visit)
- Protection begins before any network request
- Protects against SSL stripping attacks
- Permanent commitment (very difficult to remove)

## Prerequisites (Already Met)

✅ Valid SSL/TLS certificate for all domains
✅ HTTPS accessible on all pages
✅ HTTP redirects to HTTPS (307/308)
✅ HSTS header includes `max-age=31536000` (1 year)
✅ HSTS header includes `includeSubDomains`
✅ HSTS header includes `preload`
✅ All subdomains HTTPS-ready (or redirected)

## Pre-Submission Checklist

Before submitting to HSTS preload list, verify:

### 1. Domain Configuration

- [ ] **Production domain verified**: `meepleai.dev` (or actual domain)
- [ ] **All subdomains HTTPS**: `www`, `api`, `admin`, etc.
- [ ] **No HTTP-only subdomains**: Remove or migrate to HTTPS
- [ ] **Wildcard certificate**: Covers all subdomains
- [ ] **Certificate validity**: At least 90 days remaining

### 2. HSTS Header Validation

Test HSTS header on production:
```bash
curl -I https://meepleai.dev

# Expected output:
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
```

**Verify**:
- [ ] `max-age` ≥ 31536000 (1 year)
- [ ] `includeSubDomains` present
- [ ] `preload` present
- [ ] Header present on all HTTPS responses

### 3. Subdomain Audit

- [ ] List all subdomains in use
- [ ] Verify each subdomain has HTTPS
- [ ] Test HTTP → HTTPS redirect for each
- [ ] Document any HTTP-only subdomains (if any, migrate first)

### 4. Stable Production Period

- [ ] **90 days in production** with HSTS enabled
- [ ] No HSTS-related issues reported
- [ ] No certificate expiry incidents
- [ ] No unexpected HTTP downgrades

### 5. Rollback Preparation

⚠️ **WARNING**: HSTS preload is **nearly permanent**. Removal is:
- Slow (6-12 months)
- Requires manual approval
- Browsers may cache for 1+ year

**Prepare**:
- [ ] Document rollback process (if ever needed)
- [ ] Certificate renewal automation verified
- [ ] Monitoring alerts for certificate expiry
- [ ] Backup certificate authority configured

## Submission Process

### Step 1: Validate Domain

Visit: [https://hstspreload.org/](https://hstspreload.org/)

Enter domain: `meepleai.dev`

Expected result: **Eligible for preload**

If errors appear, fix before proceeding.

### Step 2: Submit Domain

1. Click **"Submit meepleai.dev to the preload list"**
2. Read and accept the permanent nature of preload
3. Confirm submission
4. Note submission ID (for tracking)

### Step 3: Wait for Approval

- **Timeline**: 2-8 weeks typically
- **Status**: Check at [https://hstspreload.org/?domain=meepleai.dev](https://hstspreload.org/?domain=meepleai.dev)
- **Updates**: Chromium updates list every 6-8 weeks
- **Propagation**: Browsers update preload list with new releases

### Step 4: Verify Inclusion

After approval, verify in browsers:

**Chrome**:
```
chrome://net-internals/#hsts
```
Search for domain, verify "static_sts_domain" is true

**Firefox**:
Check source: [https://hg.mozilla.org/mozilla-central/raw-file/tip/security/manager/ssl/nsSTSPreloadList.inc](https://hg.mozilla.org/mozilla-central/raw-file/tip/security/manager/ssl/nsSTSPreloadList.inc)

**Safari**:
Uses Chromium list (same as Chrome)

## Implementation Tasks

### Pre-Submission

- [ ] Verify 90 days of stable HSTS in production
- [ ] Audit all subdomains for HTTPS support
- [ ] Test HTTP → HTTPS redirects on all pages
- [ ] Verify certificate renewal automation
- [ ] Set up certificate expiry monitoring
- [ ] Document current HSTS configuration

### Submission

- [ ] Run hstspreload.org eligibility check
- [ ] Fix any reported issues
- [ ] Submit domain to preload list
- [ ] Record submission ID and date
- [ ] Add calendar reminder to check status

### Post-Approval

- [ ] Verify inclusion in Chromium source
- [ ] Test in multiple browsers
- [ ] Update documentation with preload status
- [ ] Monitor for any HTTPS issues
- [ ] Document preload removal process (emergency only)

### Documentation

- [ ] Update `docs/06-security/security-headers.md` with preload status
- [ ] Update SECURITY.md with HSTS preload information
- [ ] Document certificate renewal process
- [ ] Add monitoring alerts documentation

## Acceptance Criteria

✅ Domain meets all HSTS preload requirements
✅ 90+ days of stable production with HSTS
✅ All subdomains HTTPS-ready
✅ Certificate renewal automated
✅ Monitoring alerts configured
✅ Domain submitted to hstspreload.org
✅ Approval received (2-8 weeks)
✅ Domain appears in Chromium source
✅ Verification across browsers complete
✅ Documentation updated

## Risks & Mitigation

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Certificate expiry breaks site | Low | CRITICAL | Automated renewal + monitoring alerts |
| Need to remove from preload | Very Low | High | Very difficult, takes 6-12 months |
| HTTP-only subdomain needed | Low | High | Plan HTTPS for ALL subdomains before submission |
| Browser caching issues | Low | Medium | Wait 90 days post-submission to monitor |

## Benefits

- **Zero-Day Protection**: Protection from first visit, before any network request
- **Maximum Security**: Hardcoded in browser, can't be bypassed
- **Attack Prevention**: Eliminates SSL stripping and downgrade attacks
- **Trust Signal**: Demonstrates security commitment
- **SEO Benefit**: Google favors HTTPS-only sites

## Configuration Check Commands

```bash
# Check HSTS header
curl -I https://meepleai.dev | grep -i strict-transport-security

# Check certificate validity
openssl s_client -connect meepleai.dev:443 -servername meepleai.dev 2>/dev/null | openssl x509 -noout -dates

# Test HTTP redirect
curl -I http://meepleai.dev

# Check preload eligibility
curl -X POST https://hstspreload.org/api/v2/status -d "domain=meepleai.dev"
```

## References

- [HSTS Preload List Official Site](https://hstspreload.org/)
- [Chromium HSTS Preload List](https://source.chromium.org/chromium/chromium/src/+/main:net/http/transport_security_state_static.json)
- [MDN HSTS Preload](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Strict-Transport-Security#preloading_strict_transport_security)
- [OWASP HSTS Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/HTTP_Strict_Transport_Security_Cheat_Sheet.html)

---

**Labels**: `security`, `enhancement`, `priority:low`, `hsts`, `production`
**Blocked by**: None (but should wait 90 days post-production)
**Blocks**: None

**Timeline**:
- ⏱️ **Wait Period**: 90 days from production launch with HSTS
- ⏱️ **Submission**: 1 hour
- ⏱️ **Approval**: 2-8 weeks
- ⏱️ **Propagation**: 6-12 weeks (browser updates)

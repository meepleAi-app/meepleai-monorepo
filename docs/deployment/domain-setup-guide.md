# MeepleAI Domain Setup Guide

**Version**: 1.0
**Last Updated**: 2026-01-18
**Audience**: DevOps, Technical Lead
**Estimated Time**: 2-4 hours

---

## Table of Contents

1. [Pre-Purchase Checklist](#1-pre-purchase-checklist)
2. [Domain Registration](#2-domain-registration)
3. [DNS Configuration](#3-dns-configuration)
4. [Email Setup](#4-email-setup)
5. [SSL Certificate](#5-ssl-certificate)
6. [Security Hardening](#6-security-hardening)
7. [Verification & Testing](#7-verification--testing)
8. [Cost Summary](#8-cost-summary)

---

## 1. Pre-Purchase Checklist

**Estimated Time**: 1-2 hours

### 1.1 Domain Availability Check

**Tools Needed**:
- WHOIS lookup: https://www.whois.com/whois/
- Domain search: https://www.namecheap.com/domains/
- Cloudflare Registrar: https://www.cloudflare.com/products/registrar/

**Checklist**:
- [ ] **Check primary domain**: `meepleai.com`
  ```bash
  whois meepleai.com
  # Expected: "No match" or "Domain not found" = Available ✅
  ```

- [ ] **Check alternative TLDs**:
  - [ ] `meepleai.io` (tech-focused alternative)
  - [ ] `meepleai.app` (budget alternative)
  - [ ] `meeple.ai` (premium branding)

- [ ] **Verify no typosquatting risk**:
  - [ ] `meeple-ai.com` (hyphenated version)
  - [ ] `meeplai.com` (missing 'e')
  - [ ] `meepleai.net` (common typo TLD)

---

### 1.2 Trademark Conflict Check

**Why Important**: Avoid legal issues and potential domain seizure

**Resources**:
- **EU Trademark Database**: https://euipo.europa.eu/eSearch/
- **US Trademark Database**: https://www.uspto.gov/trademarks
- **Global Trademark Database**: https://www.wipo.int/branddb/

**Checklist**:
- [ ] Search "MeepleAI" in EU trademark database
- [ ] Search "Meeple AI" (with space)
- [ ] Search "Meeple" alone (board game industry term check)
- [ ] Verify no active registrations in Class 41 (Gaming/Entertainment)

**Expected Result**: ✅ "Meeple" is generic board game term (like "gamer"), low conflict risk

---

### 1.3 Social Media Handle Availability

**Platforms to Check**:
- [ ] **Twitter/X**: https://twitter.com/meepleai
- [ ] **GitHub**: https://github.com/meepleai
- [ ] **Instagram**: https://instagram.com/meepleai
- [ ] **LinkedIn**: https://linkedin.com/company/meepleai
- [ ] **Discord**: meepleai (username)

**Recommendation**: Reserve all handles even if not used immediately (prevent squatting)

**Cost**: Free for all platforms

---

### 1.4 Historical Domain Check

**Why Important**: Verify domain has no negative SEO history or spam reputation

**Tools**:
- [ ] **Wayback Machine**: https://web.archive.org/web/*/meepleai.com
  - Expected: No snapshots (never registered before)
  - ⚠️ If snapshots exist: Check for spam, adult content, malware

- [ ] **Google Site Search**: `site:meepleai.com`
  - Expected: "No results found"
  - ⚠️ If indexed: Domain previously used, check reputation

- [ ] **Blacklist Check**: https://mxtoolbox.com/blacklists.aspx
  - Enter: meepleai.com
  - Expected: "Not listed on any blacklists"

---

## 2. Domain Registration

**Recommended Registrar**: **Cloudflare Registrar** (at-cost pricing + best DNS)

### 2.1 Create Cloudflare Account

**Steps**:
1. **Navigate**: https://dash.cloudflare.com/sign-up
2. **Register with**:
   - [ ] Email: admin@your-current-email.com (use personal email initially)
   - [ ] Password: Strong password (16+ characters, use password manager)
   - [ ] Enable 2FA: ✅ **Mandatory** (use authenticator app)

3. **Verify Email**: Check inbox for verification link

**Security**: Store Cloudflare credentials in password manager (1Password, Bitwarden)

---

### 2.2 Purchase Domain

**Procedure**:

1. **Navigate to Domain Registration**:
   - Cloudflare Dashboard → Domain Registration
   - Search: `meepleai.com`

2. **Add to Cart**:
   - [ ] Verify price: ~€9.77/anno (.com wholesale cost)
   - [ ] Add WHOIS Privacy: ✅ Included FREE
   - [ ] Auto-renewal: ✅ **Enable** (prevent accidental expiration)

3. **Complete Purchase**:
   - [ ] Payment method: Credit card (backup: PayPal)
   - [ ] Billing email: admin@your-email.com
   - [ ] Complete transaction

4. **Confirmation**:
   - [ ] Check email for registration confirmation
   - [ ] Verify domain appears in Cloudflare Dashboard
   - [ ] Confirm auto-renewal enabled

**Cost**: €9.77 (first year + renewals, no price increases)

---

### 2.3 Optional: Purchase Typo Protection Domains

**Recommended** (for Beta+ phases):
- [ ] `meeple-ai.com` (€9.77) - Redirect to main domain
- [ ] `meeplai.com` (€9.77) - Common typo protection

**Setup**:
1. Purchase additional domains via Cloudflare
2. Configure redirect (see Section 3.4)

**Total Cost**: €19.54/anno for 2 typo domains

---

## 3. DNS Configuration

**Estimated Time**: 30 minutes

### 3.1 Basic DNS Records

**Navigate**: Cloudflare Dashboard → meepleai.com → DNS → Records

**Add Records**:

**1. Root Domain (A Record)**:
```
Type: A
Name: @  (or meepleai.com)
IPv4 Address: [YOUR_VPS_IP_ADDRESS]
Proxy Status: ☁️ Proxied (orange cloud) - Enables CDN + SSL
TTL: Auto
```

**Example**: `95.217.163.246` (replace with your VPS IP)

---

**2. WWW Subdomain (CNAME)**:
```
Type: CNAME
Name: www
Target: meepleai.com
Proxy Status: ☁️ Proxied
TTL: Auto
```

**Purpose**: Redirect www.meepleai.com → meepleai.com

---

**3. API Subdomain (A Record)**:
```
Type: A
Name: api
IPv4 Address: [YOUR_VPS_IP_ADDRESS]
Proxy Status: 🌐 DNS Only (gray cloud) - No proxy for API
TTL: Auto
```

**Important**: API endpoints should NOT be proxied (WebSocket/SSE issues with Cloudflare proxy)

---

**4. CAA Record (SSL Authority)**:
```
Type: CAA
Name: @
Tag: issue
Value: letsencrypt.org
TTL: Auto
```

**Purpose**: Authorize only Let's Encrypt to issue SSL certificates (security)

---

### 3.2 Email DNS Records (for SendGrid/SES)

**Will be configured in Section 4** after email provider chosen.

**Placeholder**:
```
Type: TXT
Name: @
Value: v=spf1 include:sendgrid.net include:amazonses.com ~all
TTL: Auto
```

**Purpose**: Allow SendGrid and AWS SES to send emails on behalf of @meepleai.com

---

### 3.3 DNSSEC (Security Extension)

**Enable DNSSEC**:
1. Cloudflare Dashboard → DNS → Settings
2. **DNSSEC**: Click "Enable DNSSEC"
3. **Copy DS Record**: Cloudflare generates DS record
4. **Add to Registrar**: (For Cloudflare Registrar, automatically configured ✅)

**Verification**:
```bash
dig +dnssec meepleai.com
# Should show RRSIG records (signature present)
```

**Purpose**: Protects against DNS spoofing and cache poisoning attacks

---

### 3.4 Typo Domain Redirects (Optional)

**If purchased meeple-ai.com**:

**Setup Page Rule** (Cloudflare):
1. Dashboard → Rules → Page Rules → Create Page Rule
2. **URL**: `meeple-ai.com/*`
3. **Setting**: Forwarding URL (Status Code: 301 - Permanent Redirect)
4. **Destination**: `https://meepleai.com/$1`
5. Save

**Result**: All traffic to meeple-ai.com → meepleai.com (SEO-friendly)

---

### 3.5 DNS Propagation Verification

**Check Propagation** (1-24 hours):
```bash
# Global DNS propagation check
dig @8.8.8.8 meepleai.com         # Google DNS
dig @1.1.1.1 meepleai.com         # Cloudflare DNS
dig @208.67.222.222 meepleai.com  # OpenDNS

# All should return your VPS IP address
```

**Online Tools**:
- https://www.whatsmydns.net/#A/meepleai.com
- Check from multiple global locations

**Expected Propagation Time**:
- Cloudflare nameservers: 1-2 minutes
- Global DNS caches: 24-48 hours (full propagation)

---

## 4. Email Setup

**Estimated Time**: 30 minutes

### 4.1 Cloudflare Email Routing (Free)

**Enable Email Routing**:
1. Cloudflare Dashboard → Email → Email Routing
2. Click "Get Started"
3. **Destination Address**: your-personal-email@gmail.com
4. **Verify**: Check inbox for verification email

---

### 4.2 Create Email Addresses

**Add Custom Addresses**:

1. **Admin Email**:
   ```
   Custom Address: admin@meepleai.com
   Destination: your-personal-email@gmail.com
   Action: Create
   ```

2. **Support Email**:
   ```
   Custom Address: support@meepleai.com
   Destination: your-personal-email@gmail.com
   Action: Create
   ```

3. **No-Reply Email** (for automated emails):
   ```
   Custom Address: noreply@meepleai.com
   Destination: (Leave blank - no forwarding)
   Action: Create
   ```

---

### 4.3 Configure Catch-All (Optional)

**Purpose**: Receive emails sent to any address @meepleai.com

**Setup**:
1. Email Routing → Catch-all address
2. **Action**: Send to destination
3. **Destination**: your-personal-email@gmail.com

**Use Case**: Receive emails to typos like `info@meepleai.com`, `hello@meepleai.com`

---

### 4.4 Email Provider DNS Configuration

**For SendGrid** (Alpha/Beta):

**Add DNS Records**:
1. Navigate to: DNS → Records
2. **Add TXT Record (SPF)**:
   ```
   Type: TXT
   Name: @
   Value: v=spf1 include:sendgrid.net ~all
   TTL: Auto
   ```

3. **Add CNAME Records (DKIM)** - SendGrid will provide 3 records:
   ```
   Type: CNAME
   Name: s1._domainkey
   Target: s1.domainkey.u12345678.wl.sendgrid.net

   Type: CNAME
   Name: s2._domainkey
   Target: s2.domainkey.u12345678.wl.sendgrid.net

   (SendGrid provides exact values in dashboard)
   ```

4. **Add TXT Record (DMARC)**:
   ```
   Type: TXT
   Name: _dmarc
   Value: v=DMARC1; p=quarantine; rua=mailto:dmarc-reports@meepleai.com
   TTL: Auto
   ```

---

**For AWS SES** (Release 1K+):

**Add DNS Records**:
1. **TXT Record (SPF)**:
   ```
   Type: TXT
   Name: @
   Value: v=spf1 include:amazonses.com ~all
   ```

2. **TXT Record (DKIM)** - AWS provides values:
   ```
   Type: TXT
   Name: [random]._domainkey
   Value: [AWS-provided DKIM value]

   (3 records total, AWS SES console provides exact values)
   ```

3. **MX Record** (if receiving email via SES):
   ```
   Type: MX
   Name: @
   Priority: 10
   Value: inbound-smtp.eu-west-1.amazonaws.com
   ```

---

**Verification**:
```bash
# Check SPF record
dig +short TXT meepleai.com
# Should include: "v=spf1 include:sendgrid.net"

# Check DKIM
dig +short TXT s1._domainkey.meepleai.com
# Should return DKIM public key

# Check DMARC
dig +short TXT _dmarc.meepleai.com
# Should return: "v=DMARC1; p=quarantine..."
```

---

## 5. SSL Certificate

**Estimated Time**: 15 minutes (automatic)

### 5.1 Let's Encrypt via Traefik (Recommended)

**Traefik Configuration** (already in docker-compose.yml):
```yaml
services:
  traefik:
    image: traefik:v2.10
    command:
      - "--certificatesresolvers.letsencrypt.acme.email=admin@meepleai.com"
      - "--certificatesresolvers.letsencrypt.acme.storage=/letsencrypt/acme.json"
      - "--certificatesresolvers.letsencrypt.acme.httpchallenge.entrypoint=web"
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - "./letsencrypt:/letsencrypt"
```

**Automatic Certificate Generation**:
1. Start Traefik: `docker compose up -d traefik`
2. Traefik detects domain (from labels)
3. Requests certificate from Let's Encrypt
4. Certificate saved in `/letsencrypt/acme.json`
5. Auto-renewal every 60 days (30 days before expiry)

**No manual intervention required** ✅

---

### 5.2 SSL Verification

**Check Certificate**:
1. Navigate: https://meepleai.com
2. Click padlock icon in browser → Certificate
3. **Verify**:
   - [ ] Issued by: Let's Encrypt Authority
   - [ ] Valid from: [Today's date]
   - [ ] Valid until: [90 days from today]
   - [ ] Subject Alternative Names: meepleai.com, www.meepleai.com

**SSL Labs Test**:
1. Navigate: https://www.ssllabs.com/ssltest/
2. Enter: `meepleai.com`
3. **Target Grade**: A or A+ ✅

**Expected Results**:
- Protocol Support: TLS 1.2, TLS 1.3 ✅
- Cipher Strength: 256-bit ✅
- Forward Secrecy: Yes ✅
- HSTS: Yes (via Cloudflare) ✅

---

## 6. Security Hardening

**Estimated Time**: 20 minutes

### 6.1 Domain Lock

**Enable Transfer Lock**:
1. Cloudflare Dashboard → Domain Registration → meepleai.com
2. **Domain Lock**: Toggle ON ✅

**Purpose**: Prevents unauthorized domain transfers

**Important**: Must disable temporarily if transferring to another registrar

---

### 6.2 Account Security

**2FA on Cloudflare Account** (MANDATORY):
1. Cloudflare Dashboard → Profile → Authentication
2. **Two-Factor Authentication**: Enable
3. **Method**: Use authenticator app (Google Authenticator, Authy, 1Password)
4. **Backup Codes**: Download and store securely

**Recovery Email**:
- [ ] Add recovery email (different from primary)
- [ ] Verify recovery email

---

### 6.3 Registry Lock (Optional - Release 10K+)

**What**: Premium protection requiring manual unlock for ANY DNS changes

**Cost**: €100-200/anno

**When to Enable**: Release 10K phase (high-value domain protection)

**Not needed for Alpha/Beta** ✅

---

## 7. Verification & Testing

**Estimated Time**: 30 minutes

### 7.1 DNS Resolution Test

**From Multiple Locations**:
```bash
# From local machine
nslookup meepleai.com
# Expected: Your VPS IP

# From different DNS servers
nslookup meepleai.com 8.8.8.8         # Google
nslookup meepleai.com 1.1.1.1         # Cloudflare
nslookup meepleai.com 208.67.222.222  # OpenDNS

# All should return same IP
```

---

### 7.2 Website Access Test

**HTTP → HTTPS Redirect**:
```bash
curl -I http://meepleai.com
# Expected: HTTP/1.1 301 Moved Permanently
# Location: https://meepleai.com

curl -I https://meepleai.com
# Expected: HTTP/2 200 OK
```

**Browser Test**:
- [ ] Navigate to: http://meepleai.com
- [ ] Should redirect to: https://meepleai.com ✅
- [ ] Certificate valid (green padlock) ✅
- [ ] API accessible: https://api.meepleai.com/health ✅

---

### 7.3 Email Delivery Test

**Send Test Email via SendGrid**:
```bash
# Using SendGrid API
curl -X POST https://api.sendgrid.com/v3/mail/send \
  -H "Authorization: Bearer YOUR_SENDGRID_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "personalizations": [{"to": [{"email": "your-email@gmail.com"}]}],
    "from": {"email": "noreply@meepleai.com", "name": "MeepleAI"},
    "subject": "Test Email from MeepleAI",
    "content": [{"type": "text/plain", "value": "DNS configured correctly!"}]
  }'
```

**Verification**:
- [ ] Email received in inbox (not spam folder) ✅
- [ ] From address shows: "MeepleAI <noreply@meepleai.com>" ✅
- [ ] No security warnings ✅

---

### 7.4 Email Forwarding Test

**Send Test Email to admin@meepleai.com**:
1. From external email (Gmail, Outlook), send email to: admin@meepleai.com
2. **Expected**: Email forwarded to your-personal-email@gmail.com within 1 minute

**Verify**:
- [ ] Email received ✅
- [ ] From address preserved (shows original sender) ✅
- [ ] Subject intact ✅

---

## 8. Cost Summary

### 8.1 Initial Purchase Costs

| Item | Provider | One-Time Cost | Notes |
|------|----------|---------------|-------|
| Domain registration (.com) | Cloudflare | €9.77 | First year |
| WHOIS Privacy | Cloudflare | €0 | Included FREE |
| DNS Hosting | Cloudflare | €0 | Unlimited queries |
| SSL Certificate | Let's Encrypt | €0 | Auto-renewed |
| Email Forwarding | Cloudflare | €0 | Unlimited addresses |
| **Total Setup** | - | **€9.77** | - |

---

### 8.2 Annual Renewal Costs

**Single Domain** (Alpha):
| Year | Domain Renewal | Notes |
|------|---------------|-------|
| Year 1 | €9.77 | Initial purchase |
| Year 2 | €9.77 | Same price (no increase) |
| Year 3 | €9.77 | Flat pricing ✅ |
| **5-Year Total** | **€48.85** | Predictable costs |

---

**Multiple Domains** (Beta+):
| Domains | Annual Cost | Monthly Impact |
|---------|-------------|----------------|
| meepleai.com (primary) | €9.77 | €0.81/mese |
| meeple-ai.com (typo) | €9.77 | €0.81/mese |
| **Total** | **€19.54** | **€1.63/mese** |

---

**Premium Strategy** (Release 10K):
| Domain | TLD | Annual Cost | Purpose |
|--------|-----|-------------|---------|
| meepleai.com | .com | €9.77 | Primary |
| meepleai.io | .io | €35.00 | Tech branding |
| meeple.ai | .ai | €100.00 | AI branding (premium) |
| meeple-ai.com | .com | €9.77 | Typo protection |
| meeplai.com | .com | €9.77 | Typo protection |
| **Total** | - | **€164.31/anno** | **€13.69/mese** |

---

### 8.3 Budget Impact by Phase

| Phase | Infrastructure | Domain | Email | 2FA | **Total** |
|-------|---------------|--------|-------|-----|-----------|
| Alpha | €18.49 | €0.81 | €0 | €0 | **€19.30/mese** ✅ |
| Beta | €75.27 | €1.63 | €0 | €1.85 | **€78.75/mese** ✅ |
| Release 1K | €348.30 | €4.55 | €0 (SES) | €14.80 | **€367.65/mese** ⚠️ |
| Release 10K | €1,660 | €13.69 | €1.80 | €26 | **€1,701.49/mese** ❌ |

**Budget Alignment**:
- Alpha/Beta: Well within €200/mese budget ✅
- Release 1K+: Requires revenue stream or funding ⚠️

---

## 9. Troubleshooting

### 9.1 Common Issues

**Issue**: DNS not resolving after 24 hours
```
Diagnosis:
  1. Check nameservers: dig NS meepleai.com
  2. Should return: amir.ns.cloudflare.com, lola.ns.cloudflare.com

Fix:
  - Verify nameservers set correctly at registrar
  - Wait additional 24h for full propagation
  - Clear local DNS cache: ipconfig /flushdns (Windows)
```

---

**Issue**: SSL certificate not generated
```
Diagnosis:
  1. Check Traefik logs: docker compose logs traefik | grep -i acme
  2. Look for errors like "challenge failed"

Common Causes:
  - Port 80 not accessible (firewall blocking)
  - Domain not pointing to correct IP
  - DNS not propagated yet

Fix:
  1. Verify VPS firewall allows port 80, 443
  2. Wait for DNS propagation (24h)
  3. Restart Traefik: docker compose restart traefik
```

---

**Issue**: Emails going to spam folder
```
Diagnosis:
  1. Check SPF: dig +short TXT meepleai.com
  2. Check DKIM: dig +short TXT s1._domainkey.meepleai.com
  3. Check DMARC: dig +short TXT _dmarc.meepleai.com

Fix:
  - Verify all DNS records added correctly
  - Wait 48h for email providers to update reputation
  - Send test emails to Gmail, Outlook, Yahoo
  - Check SendGrid sender reputation score
```

---

## 10. Maintenance & Renewal

### 10.1 Domain Renewal Alerts

**Setup Alerts** (Cloudflare sends automatically):
- 60 days before expiry
- 30 days before expiry
- 7 days before expiry

**Action**: Verify payment method up-to-date

---

### 10.2 Annual Domain Audit (Recommended)

**Every 12 months**:
- [ ] Verify auto-renewal enabled
- [ ] Check registrar pricing (compare with competitors)
- [ ] Review DNS records (remove unused)
- [ ] Verify SSL certificate auto-renewing
- [ ] Check WHOIS privacy still enabled
- [ ] Audit email forwarding destinations
- [ ] Review typo domain usage (discontinue if unused)

**Calendar Reminder**: Set for domain anniversary date

---

## 11. Quick Reference

### 11.1 Important URLs

| Resource | URL |
|----------|-----|
| Cloudflare Dashboard | https://dash.cloudflare.com |
| DNS Management | https://dash.cloudflare.com/[account]/meepleai.com/dns |
| Email Routing | https://dash.cloudflare.com/[account]/meepleai.com/email |
| SSL/TLS Settings | https://dash.cloudflare.com/[account]/meepleai.com/ssl-tls |
| SSL Labs Test | https://www.ssllabs.com/ssltest/analyze.html?d=meepleai.com |
| DNS Propagation | https://www.whatsmydns.net/#A/meepleai.com |

---

### 11.2 Key Credentials

**Store Securely** (1Password, Bitwarden):
- [ ] Cloudflare account email + password
- [ ] Cloudflare 2FA backup codes
- [ ] Domain EPP/Auth code (for transfers)
- [ ] SendGrid API key
- [ ] AWS SES SMTP credentials

---

### 11.3 Emergency Contacts

| Issue Type | Contact | SLA |
|------------|---------|-----|
| Domain renewal failure | Cloudflare Support (email) | 24-48h |
| DNS propagation issues | Cloudflare Community | 2-12h |
| SSL certificate problems | Let's Encrypt Community | Best-effort |
| Email deliverability | SendGrid Support | 24h (paid tier) |

---

**Next Steps**: Proceed to [Email & TOTP Services Setup](./email-totp-services.md)


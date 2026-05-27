# Email Provider Setup (Resend)

**Issue**: [#1629](https://github.com/meepleAi-app/meepleai-monorepo/issues/1629)
**Status**: active since 2026-05-27

MeepleAI sends transactional email (invitations, password reset, email verification,
report delivery, share-request notifications) through a pluggable transport selected at
startup via the `EMAIL_PROVIDER` environment variable.

| Provider | When | Notes |
|----------|------|-------|
| `resend` | staging / prod (and dev once configured) | Transactional API, requires a Resend-verified sender domain. **Default.** |
| `smtp` | dev / fallback | Legacy `SmtpClient`. ⚠️ Gmail SMTP silently drops transactional mail (accept-then-discard) — never rely on it for real delivery. |

## Why we left Gmail SMTP

Gmail SMTP accepted the submission (`SendMailAsync` returned no exception, logs said
`Invitation email sent successfully`) but **the email never left Gmail** — it was not even
in the sender's "Sent" folder. Gmail's abuse detection drops mail when the FROM equals the
authenticated account, the recipient is external, and the body contains non-canonical links
(e.g. `localhost`). There is no bounce, no log — a silent black hole. A dedicated
transactional provider with a verified domain (SPF/DKIM/DMARC) is the correct fix.

## Architecture

```
EmailService (template building)
   └── IEmailSender (transport abstraction)        ← Issue #1629
         ├── ResendEmailSender   (Resend HTTP API)
         └── SmtpEmailSender     (System.Net.Mail SmtpClient, legacy/fallback)
```

- `IEmailSender` / `EmailRequest`: `apps/api/src/Api/Services/Email/IEmailSender.cs`
- `ResendEmailSender`: `apps/api/src/Api/Services/Email/ResendEmailSender.cs`
- `SmtpEmailSender`: `apps/api/src/Api/Services/Email/SmtpEmailSender.cs`
- DI resolver: `apps/api/src/Api/Extensions/EmailSenderServiceExtensions.cs`

`EmailService` builds the subject + HTML body, then delegates raw transmission to the
injected `IEmailSender`. Adding a new provider = implement `IEmailSender` + wire it in the
resolver; no template code changes.

> **Incremental migration note**: as of #1629 only the invitation flow
> (`SendInvitationEmailAsync`, both overloads) routes through `IEmailSender`. The remaining
> `EmailService` methods (password reset, verification, reports, share-requests) still
> instantiate `SmtpClient` directly and will be migrated in follow-up PRs. Track via the
> "Out of scope" section of #1629.

## One-time setup

### 1. Resend account + API key

1. Sign up at <https://resend.com/signup>.
2. **API Keys → Create API Key** → name `meepleai-<env>` → permission **Full access**.
3. Copy the `re_…` key into `infra/secrets/email.secret` as `RESEND_API_KEY`.

### 2. Verify the sender domain

1. **Domains → Add Domain** → `meepleai.app`.
2. Resend shows **3 DNS records** (SPF, DKIM, DMARC). Add each to **Cloudflare DNS**:
   - **DNS only** (grey cloud — NOT proxied). TXT auth records must not be proxied.
   - Typical records:
     - `TXT  send`            → `v=spf1 include:amazonses.com ~all` (SPF; exact value per Resend)
     - `TXT  resend._domainkey` → DKIM public key (long value from Resend)
     - `TXT  _dmarc`          → `v=DMARC1; p=none;` (DMARC; tighten to `p=quarantine` later)
3. Wait 5–15 min for propagation, then click **Verify** in Resend. Status must be **Verified**.

### 3. Configure the app

In `infra/secrets/email.secret`:

```ini
EMAIL_PROVIDER=resend
RESEND_API_KEY=re_xxxxxxxxxxxxxxxxxxxx
RESEND_FROM_EMAIL=noreply@meepleai.app
FRONTEND_BASE_URL=http://localhost:3000   # staging/prod: public app URL
```

Restart the API to pick up the new config:

```powershell
pwsh -c "docker restart meepleai-api"
```

## Verifying delivery

1. Send a test invitation (admin session required):
   ```bash
   curl -X POST http://localhost:8080/api/v1/admin/users/invite \
     -H "Content-Type: application/json" -b cookies.txt \
     -d '{"email":"you@example.com","role":"User"}'
   ```
2. Check the **Resend dashboard → Emails**: the message should show `delivered` (not just
   `sent`). Resend reports bounces/complaints explicitly — unlike Gmail SMTP.
3. API log line: `Resend accepted email {EmailId} to {Email}` (from `ResendEmailSender`).

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| Startup warning `EMAIL_PROVIDER=resend … RESEND_API_KEY is not configured` | key missing | add `RESEND_API_KEY` to `email.secret`, restart |
| Resend dashboard shows `bounced` | recipient invalid / domain not verified | verify domain status; check recipient |
| Email `delivered` in Resend but not in inbox | recipient spam filter | check spam; tighten DMARC after warm-up |
| `Resend API rejected the email submission` in logs | API key invalid or domain unverified | re-check key + domain verification |
| Link in email is `/accept-invite?token=…` (404) | stale build pre-#1629 | rebuild; fixed to `/invites/{token}` |

## Free tier limits

Resend free tier: **3000 emails/month, 100/day**. Sufficient for staging + early access.
Upgrade when onboarding scales past the daily cap.

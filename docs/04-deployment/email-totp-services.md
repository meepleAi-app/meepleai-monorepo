# Email & TOTP Services Setup

**Version**: 1.0 | **Last Updated**: 2026-01-18 | **Time**: 2-3 hours

---

## Quick Decision Matrix

| Service | Phase | Cost | Daily Limit | Setup Time | Deliverability |
|---------|-------|------|-------------|------------|----------------|
| **SendGrid Free** | Alpha/Beta | €0 | 100/day | 30min | 95-98% |
| **AWS SES** | Release 1K+ | €0 (62K free) | None | 2h | 85-90% |
| **Postmark** | Critical emails | €13.90 | None | 45min | 98-99% |

**Recommendation**: SendGrid (Alpha) → AWS SES (Release) → Add Postmark for critical

---

## 1. SendGrid Setup (30min)

### 1.1 Create Account & API Key

1. **Register**: https://signup.sendgrid.com → Verify email
2. **API Key**: Settings → API Keys → Create (Full Access)
3. **Store**:
   ```bash
   # infra/secrets/email.secret
   SENDGRID_API_KEY=SG.xxxxxxxxxxxx
   SENDGRID_FROM_EMAIL=noreply@meepleai.com
   SENDGRID_FROM_NAME=MeepleAI
   ```

### 1.2 Domain Authentication

1. Settings → Sender Authentication → Authenticate Domain
2. Add 3 CNAME records to Cloudflare DNS (Proxy: **DNS Only**)
3. **SPF**: `v=spf1 include:sendgrid.net ~all`
4. **DMARC**: `v=DMARC1; p=quarantine; rua=mailto:dmarc@meepleai.com`

### 1.3 Test

```csharp
// .NET
var client = new SendGridClient(apiKey);
var msg = MailHelper.CreateSingleEmail(
    new EmailAddress("noreply@meepleai.com", "MeepleAI"),
    new EmailAddress("test@example.com"),
    "Welcome!", "Thanks!", "<strong>Thanks!</strong>"
);
var response = await client.SendEmailAsync(msg);
// Expect: 202 Accepted
```

---

## 2. AWS SES Setup (2h)

### 2.1 Verify Domain

1. AWS Console → SES → Create Identity → Domain: `meepleai.com`
2. Add 3 CNAME records (DKIM) to Cloudflare (DNS Only)
3. Update SPF: `v=spf1 include:sendgrid.net include:amazonses.com ~all`
4. Verify (1-24h)

### 2.2 Production Access

1. SES → Account Dashboard → Request Production Access
2. **Form**: Type=Transactional, Volume=10K/month, Use case="Board game assistant - verification emails"
3. Approval: 24h

### 2.3 SMTP Credentials

1. SES → SMTP Settings → Create SMTP Credentials
2. **Store**:
   ```bash
   # infra/secrets/email.secret
   AWS_SES_SMTP_USERNAME=AKIAIOSFODNN7EXAMPLE
   AWS_SES_SMTP_PASSWORD=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY
   AWS_SES_REGION=eu-west-1
   ```

### 2.4 Test

```csharp
var client = new AmazonSimpleEmailServiceClient(RegionEndpoint.EUWest1);
var request = new SendEmailRequest {
    Source = "noreply@meepleai.com",
    Destination = new Destination { ToAddresses = new List<string> { "test@example.com" } },
    Message = new Message {
        Subject = new Content("Welcome!"),
        Body = new Body { Html = new Content("<h1>Welcome!</h1>") }
    }
};
var response = await client.SendEmailAsync(request);
```

---

## 3. TOTP 2FA Implementation

### 3.1 Install Packages

```bash
cd apps/api/src/Api
dotnet add package OtpNet --version 1.9.3
dotnet add package QRCoder --version 1.4.3
```

### 3.2 Service

```csharp
// TotpService.cs
public class TotpService : ITotpService {
    public string GenerateSecret() => Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));

    public string GenerateQrCodeUri(string secret, string email) =>
        $"otpauth://totp/MeepleAI:{email}?secret={secret}&issuer=MeepleAI";

    public byte[] GenerateQrCodeImage(string uri) {
        using var qrGen = new QRCodeGenerator();
        using var qrData = qrGen.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrData);
        return qrCode.GetGraphic(20);
    }

    public bool ValidateTotp(string secret, string code) {
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        return totp.VerifyTotp(code, out _, window: VerificationWindow.RfcSpecifiedNetworkDelay);
    }

    public List<string> GenerateBackupCodes(int count = 10) =>
        Enumerable.Range(0, count).Select(_ => GenerateSecureCode()).ToList();

    private string GenerateSecureCode() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(6))
            .Replace("+", "").Replace("/", "").Replace("=", "")
            .Substring(0, 8).ToUpperInvariant(); // "A3F7K9P2"
}
```

### 3.3 Database Schema

```csharp
public class User {
    public bool TwoFactorEnabled { get; private set; }
    public string? TotpSecret { get; private set; }  // Encrypted
    public DateTime? TotpEnabledAt { get; private set; }
    public List<BackupCode> BackupCodes { get; private set; } = new();

    public void EnableTotp(string encryptedSecret) {
        TotpSecret = encryptedSecret;
        TwoFactorEnabled = true;
        TotpEnabledAt = DateTime.UtcNow;
    }
}

public class BackupCode {
    public string Code { get; private set; }  // Hashed
    public bool IsUsed { get; private set; }
    public DateTime? UsedAt { get; private set; }

    public void MarkAsUsed() { IsUsed = true; UsedAt = DateTime.UtcNow; }
}
```

```bash
dotnet ef migrations add AddTwoFactorAuth
dotnet ef database update
```

### 3.4 Frontend

```tsx
// components/TotpSetup.tsx
export function TotpSetup() {
  const [qrCode, setQrCode] = useState<string | null>(null);
  const [secret, setSecret] = useState<string | null>(null);
  const [backupCodes, setBackupCodes] = useState<string[]>([]);

  const handleEnable = async () => {
    const res = await fetch('/api/v1/auth/totp/enable', { method: 'POST' });
    const data = await res.json();
    setQrCode(data.qrCodeDataUri);
    setSecret(data.secret);
    setBackupCodes(data.backupCodes);
  };

  return (
    <div>
      <button onClick={handleEnable}>Enable 2FA</button>
      {qrCode && (
        <>
          <QRCodeSVG value={qrCode} size={256} />
          <p>Manual: <code>{secret}</code></p>
          <input maxLength={6} placeholder="6-digit code" />
          <button onClick={handleVerify}>Verify</button>
          <ul>{backupCodes.map(c => <li key={c}>{c}</li>)}</ul>
        </>
      )}
    </div>
  );
}
```

---

## 4. SMS 2FA (Twilio)

**Phase**: Beta+ | **Cost**: $0.05/SMS | **Use**: Fallback for no-app users

### 4.1 Setup

1. **Create**: https://www.twilio.com/try-twilio
2. **Credentials**: Console → Account SID + Auth Token
3. **Phone**: Buy number ($1/month) with SMS capability
4. **Store**:
   ```bash
   # infra/secrets/sms.secret
   TWILIO_ACCOUNT_SID=ACxxxxxxxx
   TWILIO_AUTH_TOKEN=your_token
   TWILIO_PHONE_NUMBER=+1234567890
   ```

### 4.2 Service

```bash
dotnet add package Twilio --version 6.15.0
```

```csharp
public class SmsService : ISmsService {
    public async Task SendTotpCodeAsync(string toPhone, string code) {
        var msg = await MessageResource.CreateAsync(
            body: $"MeepleAI code: {code}. Valid 5min.",
            from: new PhoneNumber(_fromNumber),
            to: new PhoneNumber(toPhone)
        );
        if (msg.Status == StatusEnum.Failed) throw new($"SMS failed: {msg.ErrorMessage}");
    }

    public string GenerateSmsCode() => RandomNumberGenerator.GetInt32(100000, 999999).ToString();
}
```

### 4.3 Rate Limiting

```csharp
public async Task<bool> CanSendSmsAsync(Guid userId) {
    var sentToday = await _db.SmsCodes
        .Where(c => c.UserId == userId && c.CreatedAt > DateTime.UtcNow.AddHours(-24))
        .CountAsync();
    return sentToday < 5;  // Max 5/day
}
```

---

## 5. Email Templates

### Verification Template

```html
<!DOCTYPE html>
<body style="font-family: Arial; padding: 20px; background: #f4f4f4;">
  <div style="max-width: 600px; margin: 0 auto; background: white; padding: 30px; border-radius: 8px;">
    <h1 style="color: #333;">Welcome to MeepleAI! 🎲</h1>
    <p style="color: #666;">Verify your email to get started.</p>
    <a href="{{verificationLink}}" style="display: inline-block; background: #4F46E5; color: white; padding: 12px 30px; text-decoration: none; border-radius: 6px; font-weight: bold;">
      Verify Email
    </a>
    <p style="color: #999; font-size: 14px;">Expires in 24h. Ignore if you didn't register.</p>
  </div>
</body>
```

### Password Reset Template

```html
<body style="font-family: Arial; padding: 20px; background: #f4f4f4;">
  <div style="max-width: 600px; margin: 0 auto; background: white; padding: 30px; border-radius: 8px;">
    <h1>Password Reset 🔐</h1>
    <p>Click to reset your password.</p>
    <a href="{{resetLink}}" style="display: inline-block; background: #DC2626; color: white; padding: 12px 30px; border-radius: 6px;">
      Reset Password
    </a>
    <p style="color: #999;">Expires in 1h. Ignore if you didn't request this.</p>
    <div style="background: #FEF3C7; padding: 15px; border-left: 4px solid #F59E0B;">
      <strong>Security Tip:</strong> Never share this link.
    </div>
  </div>
</body>
```

---

## 6. Testing & Monitoring

### Email Test

```bash
curl -X POST https://api.sendgrid.com/v3/mail/send \
  -H "Authorization: Bearer $SENDGRID_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"personalizations":[{"to":[{"email":"test@gmail.com"}]}],"from":{"email":"noreply@meepleai.com"},"subject":"Test","content":[{"type":"text/html","value":"<h1>OK!</h1>"}]}'
# Expect: 202 Accepted
```

### TOTP Test

```csharp
[Fact]
public void ValidateTotp_AcceptsValid() {
    var service = new TotpService();
    var secret = service.GenerateSecret();
    var totp = new Totp(Base32Encoding.ToBytes(secret));
    var validCode = totp.ComputeTotp();

    Assert.True(service.ValidateTotp(secret, validCode));
}
```

### Cost Monitoring

**SendGrid**: Dashboard → Stats → Check daily sends (100/day limit on free)

**Twilio**: Billing → Usage → Set alert at $20/month

---

## Security Checklist

**Email**:
- [x] SPF configured
- [x] DKIM enabled
- [x] DMARC policy (p=quarantine)
- [ ] Monitor bounce rate (<5%)
- [ ] Rate limit password reset (3/hour/user)

**TOTP**:
- [x] Secrets encrypted (AES-256)
- [x] Time window ±30s (RFC 6238)
- [x] Rate limit verification (5/15min)
- [ ] Backup codes hashed
- [ ] Secret rotation (yearly)

**SMS**:
- [x] Rate limit (5/day/user)
- [x] Code expiry (5min)
- [ ] Phone verification
- [ ] Fraud detection (volume spikes)

---

## Troubleshooting

### Email Not Delivered

```bash
dig +short TXT meepleai.com | grep spf          # Check SPF
dig +short TXT s1._domainkey.meepleai.com       # Check DKIM
```

**SendGrid Activity**: Filter by email → Check status (`delivered` ✅ | `bounce` ❌)

### TOTP Code Invalid

**Time sync issue**: Increase tolerance window
```csharp
var window = new VerificationWindow(previous: 2, future: 2); // ±60s vs ±30s
```

### SMS Failed

**Twilio Logs**: Monitor → Messaging → Check error codes
- `30007`: Number invalid
- `30008`: Wrong country code
- `21610`: Opted out

---

**Support**:
- SendGrid: https://support.sendgrid.com (24-48h)
- AWS SES: AWS Support (12-24h)
- Twilio: https://support.twilio.com (24h)

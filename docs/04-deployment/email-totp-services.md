# Email & TOTP Services Setup Guide

**Version**: 1.0
**Last Updated**: 2026-01-18
**Estimated Time**: 2-3 hours
**Prerequisites**: Domain configured (see [domain-setup-guide.md](./domain-setup-guide.md))

---

## Table of Contents

1. [Email Service Setup](#1-email-service-setup)
   - SendGrid (Alpha/Beta)
   - AWS SES (Release 1K+)
2. [TOTP 2FA Implementation](#2-totp-2fa-implementation)
3. [SMS 2FA Setup (Twilio)](#3-sms-2fa-setup-twilio)
4. [Email Templates](#4-email-templates)
5. [Testing & Verification](#5-testing--verification)
6. [Cost Monitoring](#6-cost-monitoring)

---

## 1. Email Service Setup

### 1.1 SendGrid Setup (Alpha/Beta - FREE Tier)

**Phase**: Alpha, Beta
**Cost**: €0 (3,000 email/mese free)
**Use Case**: Transactional emails (verification, password reset)

---

#### Step 1: Create SendGrid Account

1. **Navigate**: https://signup.sendgrid.com
2. **Register**:
   - [ ] Email: admin@meepleai.com (use Cloudflare forwarding)
   - [ ] Password: Strong password (save in password manager)
   - [ ] Complete email verification
3. **Select Plan**: Free (100 email/day limit)

---

#### Step 2: Create API Key

1. **Navigate**: Settings → API Keys
2. **Create API Key**:
   - [ ] Name: `MeepleAI-Production`
   - [ ] Permissions: **Full Access** (or restricted: Mail Send only)
   - [ ] Click "Create & View"
   - [ ] **Copy API Key**: `SG.xxxxxxxxxxxx` (show only once!)

3. **Store Securely**:
   ```bash
   # Add to infra/secrets/email.secret
   SENDGRID_API_KEY=SG.xxxxxxxxxxxx
   SENDGRID_FROM_EMAIL=noreply@meepleai.com
   SENDGRID_FROM_NAME=MeepleAI
   ```

---

#### Step 3: Domain Authentication

**Purpose**: Enable sending from @meepleai.com (vs @sendgrid.net)

1. **Navigate**: Settings → Sender Authentication → Authenticate Your Domain
2. **Enter Domain**: `meepleai.com`
3. **SendGrid Provides 3 CNAME Records**:
   ```
   Example (actual values will differ):
   s1._domainkey.meepleai.com → s1.domainkey.u12345.wl.sendgrid.net
   s2._domainkey.meepleai.com → s2.domainkey.u12345.wl.sendgrid.net
   em1234.meepleai.com → u12345.wl.sendgrid.net
   ```

4. **Add to Cloudflare DNS**:
   - [ ] Copy exact CNAME records from SendGrid
   - [ ] Cloudflare → DNS → Add Record (3 total)
   - [ ] Proxy Status: **DNS Only** (gray cloud)

5. **Verify**:
   - Return to SendGrid → Click "Verify"
   - Status should show: ✅ **Verified**

---

#### Step 4: Configure SPF/DMARC

**Add SPF Record** (if not exists):
```dns
Type: TXT
Name: @
Value: v=spf1 include:sendgrid.net ~all
TTL: Auto
```

**Add DMARC Record**:
```dns
Type: TXT
Name: _dmarc
Value: v=DMARC1; p=quarantine; rua=mailto:dmarc@meepleai.com; pct=100; adkim=r; aspf=r
TTL: Auto
```

**Verification**:
```bash
dig +short TXT meepleai.com | grep spf
# Expected: "v=spf1 include:sendgrid.net ~all"

dig +short TXT _dmarc.meepleai.com
# Expected: "v=DMARC1; p=quarantine..."
```

---

#### Step 5: Application Integration

**Update .NET API Configuration**:

```csharp
// appsettings.Production.json
{
  "Email": {
    "Provider": "SendGrid",
    "SendGrid": {
      "ApiKey": "",  // Loaded from environment variable
      "FromEmail": "noreply@meepleai.com",
      "FromName": "MeepleAI"
    }
  }
}
```

**Environment Variable** (docker-compose.yml):
```yaml
services:
  api:
    env_file:
      - ./secrets/email.secret
    environment:
      - Email__SendGrid__ApiKey=${SENDGRID_API_KEY}
```

**Send Test Email** (.NET):
```csharp
using SendGrid;
using SendGrid.Helpers.Mail;

var client = new SendGridClient(apiKey);
var from = new EmailAddress("noreply@meepleai.com", "MeepleAI");
var to = new EmailAddress("test@example.com");
var subject = "Welcome to MeepleAI!";
var plainTextContent = "Thanks for joining!";
var htmlContent = "<strong>Thanks for joining!</strong>";

var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
var response = await client.SendEmailAsync(msg);

// Check response
Console.WriteLine($"Status Code: {response.StatusCode}");  // Should be 202 Accepted
```

---

### 1.2 AWS SES Setup (Release 1K+ - Cost-Optimized)

**Phase**: Release 1K, Release 10K
**Cost**: €0 (first 62,000 email/mese free), then €0.10 per 1,000
**Use Case**: High-volume transactional emails

---

#### Step 1: Create AWS Account

1. **Navigate**: https://aws.amazon.com
2. **Sign Up**: Business account (MeepleAI)
3. **Verify Identity**: Credit card required (won't be charged for free tier)

---

#### Step 2: Verify Domain in SES

1. **Navigate**: AWS Console → SES → Verified Identities
2. **Verify Domain**:
   - [ ] Click "Create Identity"
   - [ ] Identity type: **Domain**
   - [ ] Domain: `meepleai.com`
   - [ ] Enable DKIM: ✅ Yes
   - [ ] Click "Create Identity"

3. **AWS Provides DNS Records** (example):
   ```
   3 CNAME records for DKIM:
   abc123._domainkey.meepleai.com → abc123.dkim.amazonses.com
   def456._domainkey.meepleai.com → def456.dkim.amazonses.com
   ghi789._domainkey.meepleai.com → ghi789.dkim.amazonses.com
   ```

4. **Add to Cloudflare DNS**:
   - [ ] Add all 3 CNAME records
   - [ ] Proxy Status: DNS Only (gray cloud)

5. **Update SPF Record**:
   ```dns
   Old: v=spf1 include:sendgrid.net ~all
   New: v=spf1 include:sendgrid.net include:amazonses.com ~all
   ```

6. **Verify**:
   - SES Console → Check verification status
   - Should show: ✅ **Verified** (within 1-24h)

---

#### Step 3: Request Production Access

**Important**: AWS SES starts in **Sandbox Mode** (can only send to verified emails)

**Request Production**:
1. SES Console → Account Dashboard → Request Production Access
2. **Fill Form**:
   - [ ] Mail Type: **Transactional**
   - [ ] Website URL: https://meepleai.com
   - [ ] Use case: "Board game assistant app - send verification emails, password resets"
   - [ ] Will you comply with AWS policies? **Yes**
   - [ ] Expected volume: 10,000 email/mese initially

3. **Submit**: AWS reviews within 24h
4. **Approval Email**: Check inbox
5. **Verify Production Mode**: SES Dashboard should show "Production"

---

#### Step 4: Create SMTP Credentials

1. **Navigate**: SES → SMTP Settings → Create SMTP Credentials
2. **IAM User Name**: `MeepleAI-SES-SMTP`
3. **Download Credentials**: Save CSV file
   - SMTP Username: `AKIAIOSFODNN7EXAMPLE`
   - SMTP Password: `wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY`

4. **Store in Secrets**:
   ```bash
   # infra/secrets/email.secret
   AWS_SES_SMTP_USERNAME=AKIAIOSFODNN7EXAMPLE
   AWS_SES_SMTP_PASSWORD=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY
   AWS_SES_REGION=eu-west-1
   ```

---

#### Step 5: Application Integration

**Update appsettings.json**:
```json
{
  "Email": {
    "Provider": "AWSSES",
    "AWSSES": {
      "Region": "eu-west-1",
      "FromEmail": "noreply@meepleai.com",
      "FromName": "MeepleAI",
      "ConfigurationSet": "MeepleAI-Tracking"  // Optional for analytics
    }
  }
}
```

**Send Email via AWS SDK** (.NET):
```csharp
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;

var client = new AmazonSimpleEmailServiceClient(RegionEndpoint.EUWest1);

var request = new SendEmailRequest
{
    Source = "MeepleAI <noreply@meepleai.com>",
    Destination = new Destination
    {
        ToAddresses = new List<string> { "user@example.com" }
    },
    Message = new Message
    {
        Subject = new Content("Welcome to MeepleAI!"),
        Body = new Body
        {
            Html = new Content("<h1>Welcome!</h1><p>Thanks for joining MeepleAI.</p>"),
            Text = new Content("Welcome! Thanks for joining MeepleAI.")
        }
    }
};

var response = await client.SendEmailAsync(request);
Console.WriteLine($"Message ID: {response.MessageId}");
```

---

### 1.3 Email Service Comparison

**Quick Decision Matrix**:

| Feature | SendGrid Free | AWS SES | Postmark |
|---------|--------------|---------|----------|
| **Cost (8K email/mese)** | €0 | €0 | €13.90 |
| **Daily Limit** | 100/day ⚠️ | None ✅ | None ✅ |
| **Monthly Limit** | 3,000 | 62,000 | 10,000 (base plan) |
| **Setup Time** | 30 min | 2h (domain verification + production request) | 45 min |
| **Deliverability** | 95-98% | 85-90% (needs warm-up) | 98-99% |
| **Support** | Email (slow) | Docs only | Email 24h |

**Recommendation**:
- **Alpha/Beta**: SendGrid Free (easiest setup, sufficient volume)
- **Release 1K**: AWS SES (cost-free, handles spikes)
- **Critical Emails**: Consider Postmark for verification/password reset (98% deliverability)

---

## 2. TOTP 2FA Implementation

### 2.1 Install NuGet Package

```bash
cd apps/api/src/Api
dotnet add package OtpNet --version 1.9.3
dotnet add package QRCoder --version 1.4.3  # For QR code generation
```

---

### 2.2 Create TOTP Service

**File**: `BoundedContexts/Authentication/Infrastructure/Services/TotpService.cs`

```csharp
using OtpNet;
using QRCoder;
using System.Security.Cryptography;

public class TotpService : ITotpService
{
    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    public string GenerateQrCodeUri(string secret, string userEmail)
    {
        var issuer = "MeepleAI";
        return $"otpauth://totp/{issuer}:{userEmail}?secret={secret}&issuer={issuer}";
    }

    public byte[] GenerateQrCodeImage(string qrCodeUri)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(qrCodeUri, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(20);  // 20 pixels per module
    }

    public bool ValidateTotp(string secret, string userCode)
    {
        var totp = new Totp(Base32Encoding.ToBytes(secret));
        return totp.VerifyTotp(
            userCode,
            out long timeStepMatched,
            window: VerificationWindow.RfcSpecifiedNetworkDelay  // ±30 seconds tolerance
        );
    }

    public List<string> GenerateBackupCodes(int count = 10)
    {
        return Enumerable.Range(0, count)
            .Select(_ => GenerateSecureBackupCode())
            .ToList();
    }

    private string GenerateSecureBackupCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(6);
        return Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, 8)
            .ToUpperInvariant();  // Example: "A3F7K9P2"
    }
}
```

---

### 2.3 Database Schema for 2FA

**Migration**: Add to User entity

```csharp
// Domain/Entities/User.cs
public class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; }

    // TOTP 2FA
    public bool TwoFactorEnabled { get; private set; }
    public string? TotpSecret { get; private set; }  // Encrypted at rest
    public DateTime? TotpEnabledAt { get; private set; }

    // Backup Codes
    public List<BackupCode> BackupCodes { get; private set; } = new();

    // SMS 2FA (optional)
    public string? PhoneNumber { get; private set; }
    public bool SmsEnabled { get; private set; }

    public void EnableTotp(string encryptedSecret)
    {
        TotpSecret = encryptedSecret;
        TwoFactorEnabled = true;
        TotpEnabledAt = DateTime.UtcNow;
    }

    public void DisableTotp()
    {
        TwoFactorEnabled = false;
        TotpSecret = null;
        TotpEnabledAt = null;
    }
}

public class BackupCode
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Code { get; private set; }  // Hashed
    public bool IsUsed { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public void MarkAsUsed()
    {
        IsUsed = true;
        UsedAt = DateTime.UtcNow;
    }
}
```

**Create Migration**:
```bash
dotnet ef migrations add AddTwoFactorAuthentication
dotnet ef database update
```

---

### 2.4 TOTP Setup Endpoint

**Command**: `Application/Commands/EnableTotpCommand.cs`

```csharp
public record EnableTotpCommand(Guid UserId) : IRequest<EnableTotpResult>;

public record EnableTotpResult(
    string QrCodeDataUri,  // Base64 PNG for QR code
    string Secret,  // For manual entry (if QR scan fails)
    List<string> BackupCodes  // 10 backup codes
);

public class EnableTotpCommandHandler : IRequestHandler<EnableTotpCommand, EnableTotpResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly ITotpService _totpService;
    private readonly IEncryptionService _encryption;

    public async Task<EnableTotpResult> Handle(EnableTotpCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { request.UserId }, ct)
            ?? throw new NotFoundException($"User {request.UserId} not found");

        // Generate TOTP secret
        var secret = _totpService.GenerateSecret();
        var encryptedSecret = _encryption.Encrypt(secret);

        // Generate QR code
        var qrCodeUri = _totpService.GenerateQrCodeUri(secret, user.Email);
        var qrCodeBytes = _totpService.GenerateQrCodeImage(qrCodeUri);
        var qrCodeDataUri = $"data:image/png;base64,{Convert.ToBase64String(qrCodeBytes)}";

        // Generate backup codes
        var backupCodes = _totpService.GenerateBackupCodes(10);
        var backupCodeEntities = backupCodes.Select(code => new BackupCode
        {
            UserId = user.Id,
            Code = _encryption.Hash(code),  // Store hashed
            CreatedAt = DateTime.UtcNow
        }).ToList();

        // Save (but don't enable yet - wait for verification)
        user.TotpSecret = encryptedSecret;
        _db.BackupCodes.AddRange(backupCodeEntities);
        await _db.SaveChangesAsync(ct);

        return new EnableTotpResult(qrCodeDataUri, secret, backupCodes);
    }
}
```

---

### 2.5 TOTP Verification Endpoint

**Command**: `Application/Commands/VerifyTotpCommand.cs`

```csharp
public record VerifyTotpCommand(Guid UserId, string Code) : IRequest<bool>;

public class VerifyTotpCommandHandler : IRequestHandler<VerifyTotpCommand, bool>
{
    private readonly MeepleAiDbContext _db;
    private readonly ITotpService _totpService;
    private readonly IEncryptionService _encryption;

    public async Task<bool> Handle(VerifyTotpCommand request, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { request.UserId }, ct)
            ?? throw new NotFoundException();

        if (string.IsNullOrEmpty(user.TotpSecret))
            throw new InvalidOperationException("TOTP not configured");

        // Decrypt secret
        var secret = _encryption.Decrypt(user.TotpSecret);

        // Validate code
        var isValid = _totpService.ValidateTotp(secret, request.Code);

        // If valid and first verification, enable 2FA
        if (isValid && !user.TwoFactorEnabled)
        {
            user.EnableTotp(user.TotpSecret);
            await _db.SaveChangesAsync(ct);
        }

        return isValid;
    }
}
```

---

### 2.6 Frontend Integration

**React Component** (TOTP Setup):

```typescript
// components/TotpSetup.tsx
import { useState } from 'react';
import { QRCodeSVG } from 'qrcode.react';

export function TotpSetup() {
  const [qrCodeUri, setQrCodeUri] = useState<string | null>(null);
  const [secret, setSecret] = useState<string | null>(null);
  const [backupCodes, setBackupCodes] = useState<string[]>([]);
  const [verificationCode, setVerificationCode] = useState('');

  const handleEnableTotp = async () => {
    const response = await fetch('/api/v1/auth/totp/enable', {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${token}` }
    });

    const data = await response.json();
    setQrCodeUri(data.qrCodeDataUri);
    setSecret(data.secret);
    setBackupCodes(data.backupCodes);
  };

  const handleVerify = async () => {
    const response = await fetch('/api/v1/auth/totp/verify', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ code: verificationCode })
    });

    if (response.ok) {
      alert('2FA enabled successfully!');
    }
  };

  return (
    <div>
      <h2>Setup Two-Factor Authentication</h2>

      {!qrCodeUri && (
        <button onClick={handleEnableTotp}>Enable 2FA</button>
      )}

      {qrCodeUri && (
        <>
          <div>
            <h3>Step 1: Scan QR Code</h3>
            <QRCodeSVG value={qrCodeUri} size={256} />
            <p>Or enter manually: <code>{secret}</code></p>
          </div>

          <div>
            <h3>Step 2: Verify Code</h3>
            <input
              type="text"
              placeholder="Enter 6-digit code"
              value={verificationCode}
              onChange={(e) => setVerificationCode(e.target.value)}
              maxLength={6}
            />
            <button onClick={handleVerify}>Verify</button>
          </div>

          <div>
            <h3>Step 3: Save Backup Codes</h3>
            <button onClick={() => downloadBackupCodes(backupCodes)}>
              Download Backup Codes (PDF)
            </button>
            <ul>
              {backupCodes.map((code, i) => <li key={i}>{code}</li>)}
            </ul>
          </div>
        </>
      )}
    </div>
  );
}
```

---

## 3. SMS 2FA Setup (Twilio)

**Phase**: Beta+ (optional fallback)
**Cost**: $0.05 per SMS (€0.046)
**Use Case**: Users without smartphone authenticator app

---

### 3.1 Create Twilio Account

1. **Navigate**: https://www.twilio.com/try-twilio
2. **Sign Up**:
   - [ ] Email: admin@meepleai.com
   - [ ] Create password
   - [ ] Verify email
3. **Complete Onboarding**:
   - [ ] Use case: "Two-factor authentication"
   - [ ] Language: C#

---

### 3.2 Get Credentials

1. **Console Home**: https://console.twilio.com
2. **Copy Credentials**:
   - [ ] Account SID: `ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`
   - [ ] Auth Token: `your_auth_token`

3. **Get Phone Number**:
   - Navigate: Phone Numbers → Buy a Number
   - [ ] Select country: Italy (+39) or US (+1)
   - [ ] Capabilities: SMS ✅
   - [ ] Buy number (€1/mese)

4. **Store Secrets**:
   ```bash
   # infra/secrets/sms.secret
   TWILIO_ACCOUNT_SID=ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
   TWILIO_AUTH_TOKEN=your_auth_token
   TWILIO_PHONE_NUMBER=+1234567890
   ```

---

### 3.3 Implementation

**Install Twilio SDK**:
```bash
dotnet add package Twilio --version 6.15.0
```

**SMS Service**:
```csharp
using Twilio;
using Twilio.Rest.Api.V2010.Account;

public class SmsService : ISmsService
{
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _phoneNumber;

    public SmsService(IConfiguration config)
    {
        _accountSid = config["Twilio:AccountSid"];
        _authToken = config["Twilio:AuthToken"];
        _phoneNumber = config["Twilio:PhoneNumber"];

        TwilioClient.Init(_accountSid, _authToken);
    }

    public async Task SendTotpCodeAsync(string toPhoneNumber, string code)
    {
        var message = await MessageResource.CreateAsync(
            body: $"Your MeepleAI verification code is: {code}. Valid for 5 minutes.",
            from: new Twilio.Types.PhoneNumber(_phoneNumber),
            to: new Twilio.Types.PhoneNumber(toPhoneNumber)
        );

        if (message.Status == MessageResource.StatusEnum.Failed)
            throw new Exception($"SMS failed: {message.ErrorMessage}");
    }

    public string GenerateSmsCode()
    {
        var random = RandomNumberGenerator.GetInt32(100000, 999999);
        return random.ToString();  // 6-digit code
    }
}
```

**Store Code in Database** (5-minute expiry):
```csharp
public class SmsCode
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Code { get; private set; }  // Hashed
    public DateTime ExpiresAt { get; private set; }
    public bool IsUsed { get; private set; }

    public static SmsCode Create(Guid userId, string code)
    {
        return new SmsCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Code = HashCode(code),  // Hash before storing
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            IsUsed = false
        };
    }
}
```

---

### 3.4 Rate Limiting (Prevent Abuse)

**Implementation**:
```csharp
public class SmsRateLimiter
{
    private readonly MeepleAiDbContext _db;

    public async Task<bool> CanSendSmsAsync(Guid userId)
    {
        var sentToday = await _db.SmsCodes
            .Where(c => c.UserId == userId
                     && c.CreatedAt > DateTime.UtcNow.AddHours(-24))
            .CountAsync();

        return sentToday < 5;  // Max 5 SMS per day
    }

    public async Task<bool> CanSendSmsNowAsync(Guid userId)
    {
        var lastSent = await _db.SmsCodes
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (lastSent == null) return true;

        var timeSinceLastSms = DateTime.UtcNow - lastSent.CreatedAt;
        return timeSinceLastSms.TotalMinutes >= 1;  // Min 1 minute between SMS
    }
}
```

---

## 4. Email Templates

### 4.1 Account Verification Template

**File**: `templates/email/verify-account.html`

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Verify Your Account</title>
</head>
<body style="font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f4f4f4;">
    <div style="max-width: 600px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 8px;">
        <h1 style="color: #333; margin-bottom: 20px;">Welcome to MeepleAI! 🎲</h1>

        <p style="color: #666; line-height: 1.6;">
            Thanks for joining MeepleAI! To get started, please verify your email address.
        </p>

        <div style="text-align: center; margin: 30px 0;">
            <a href="{{verificationLink}}"
               style="display: inline-block; background-color: #4F46E5; color: white; padding: 12px 30px; text-decoration: none; border-radius: 6px; font-weight: bold;">
                Verify Email Address
            </a>
        </div>

        <p style="color: #999; font-size: 14px;">
            This link expires in 24 hours.<br>
            If you didn't create this account, you can safely ignore this email.
        </p>

        <hr style="border: none; border-top: 1px solid #ddd; margin: 30px 0;">

        <p style="color: #999; font-size: 12px; text-align: center;">
            © 2026 MeepleAI | Your AI-powered board game assistant
        </p>
    </div>
</body>
</html>
```

---

### 4.2 Password Reset Template

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>Reset Your Password</title>
</head>
<body style="font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f4f4f4;">
    <div style="max-width: 600px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 8px;">
        <h1 style="color: #333;">Password Reset Request 🔐</h1>

        <p style="color: #666; line-height: 1.6;">
            We received a request to reset your password. Click the button below to create a new password.
        </p>

        <div style="text-align: center; margin: 30px 0;">
            <a href="{{resetLink}}"
               style="display: inline-block; background-color: #DC2626; color: white; padding: 12px 30px; text-decoration: none; border-radius: 6px; font-weight: bold;">
                Reset Password
            </a>
        </div>

        <p style="color: #999; font-size: 14px;">
            This link expires in 1 hour.<br>
            <strong>Didn't request this?</strong> Your account is still secure. You can safely ignore this email.
        </p>

        <div style="background-color: #FEF3C7; padding: 15px; border-left: 4px solid #F59E0B; margin: 20px 0;">
            <p style="margin: 0; color: #92400E;">
                <strong>Security Tip:</strong> Never share this link with anyone.
            </p>
        </div>
    </div>
</body>
</html>
```

---

### 4.3 Login Alert Template

```html
<!DOCTYPE html>
<html>
<body style="font-family: Arial, sans-serif; margin: 0; padding: 20px; background-color: #f4f4f4;">
    <div style="max-width: 600px; margin: 0 auto; background-color: white; padding: 30px; border-radius: 8px;">
        <h1 style="color: #333;">New Login Detected 🔔</h1>

        <p style="color: #666;">
            Your MeepleAI account was accessed from a new device.
        </p>

        <table style="width: 100%; margin: 20px 0; border-collapse: collapse;">
            <tr>
                <td style="padding: 10px; border-bottom: 1px solid #ddd;"><strong>Device:</strong></td>
                <td style="padding: 10px; border-bottom: 1px solid #ddd;">{{deviceName}}</td>
            </tr>
            <tr>
                <td style="padding: 10px; border-bottom: 1px solid #ddd;"><strong>Location:</strong></td>
                <td style="padding: 10px; border-bottom: 1px solid #ddd;">{{ipLocation}}</td>
            </tr>
            <tr>
                <td style="padding: 10px; border-bottom: 1px solid #ddd;"><strong>Time:</strong></td>
                <td style="padding: 10px; border-bottom: 1px solid #ddd;">{{loginTime}}</td>
            </tr>
            <tr>
                <td style="padding: 10px;"><strong>IP Address:</strong></td>
                <td style="padding: 10px;">{{ipAddress}}</td>
            </tr>
        </table>

        <p style="color: #666;">
            <strong>Was this you?</strong> No action needed.
        </p>

        <div style="text-align: center; margin: 30px 0;">
            <a href="{{secureAccountLink}}"
               style="display: inline-block; background-color: #DC2626; color: white; padding: 12px 30px; text-decoration: none; border-radius: 6px;">
                Secure My Account
            </a>
        </div>

        <p style="color: #999; font-size: 12px;">
            If this wasn't you, change your password immediately and enable 2FA.
        </p>
    </div>
</body>
</html>
```

---

## 5. Testing & Verification

### 5.1 Email Delivery Test

**SendGrid Test**:
```bash
curl -X POST https://api.sendgrid.com/v3/mail/send \
  -H "Authorization: Bearer $SENDGRID_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "personalizations": [{"to": [{"email": "your-email@gmail.com"}]}],
    "from": {"email": "noreply@meepleai.com", "name": "MeepleAI"},
    "subject": "Test Email - MeepleAI",
    "content": [{"type": "text/html", "value": "<h1>Test successful!</h1>"}]
  }'
```

**Expected Response**: `202 Accepted`

**Verify in Inbox**:
- [ ] Email received (check spam folder if not in inbox)
- [ ] From: "MeepleAI <noreply@meepleai.com>" ✅
- [ ] No security warnings ✅

---

### 5.2 TOTP Validation Test

**Test TOTP Generation**:
```csharp
// Unit test
[Fact]
public void ValidateTotp_ShouldAcceptValidCode()
{
    // Arrange
    var service = new TotpService();
    var secret = service.GenerateSecret();
    var totp = new Totp(Base32Encoding.ToBytes(secret));
    var validCode = totp.ComputeTotp();

    // Act
    var isValid = service.ValidateTotp(secret, validCode);

    // Assert
    Assert.True(isValid);
}

[Fact]
public void ValidateTotp_ShouldRejectInvalidCode()
{
    // Arrange
    var service = new TotpService();
    var secret = service.GenerateSecret();

    // Act
    var isValid = service.ValidateTotp(secret, "000000");  // Invalid code

    // Assert
    Assert.False(isValid);
}
```

---

### 5.3 SMS Delivery Test

**Twilio Test**:
```csharp
var smsService = new SmsService(configuration);
await smsService.SendTotpCodeAsync("+393331234567", "123456");

// Check Twilio Console → Monitor → Logs → SMS Logs
// Status should be: "delivered" ✅
```

---

## 6. Cost Monitoring

### 6.1 SendGrid Usage Dashboard

**Monitor Usage**:
1. SendGrid Dashboard → Stats → Overview
2. **Check Metrics**:
   - [ ] Daily sends (limit: 100/day on free tier)
   - [ ] Monthly sends (limit: 3,000/mese)
   - [ ] Bounce rate (target: <5%)
   - [ ] Spam report rate (target: <0.1%)

**Alert Setup**: SendGrid sends email when approaching limits (90% = 90 email/day)

---

### 6.2 Twilio Cost Monitoring

**Monitor SMS Costs**:
1. Twilio Console → Billing → Usage
2. **Check**:
   - [ ] SMS sent this month
   - [ ] Current charges
   - [ ] Projected month-end cost

**Budget Alert**:
1. Billing → Set up notifications
2. **Threshold**: $20/mese
3. **Action**: Email alert when exceeded

---

### 6.3 Cost Tracking Spreadsheet

**Monthly Template**:

| Service | Provider | Volume | Unit Cost | Total Cost | Budget | Variance |
|---------|----------|--------|-----------|------------|--------|----------|
| Email | SendGrid | 900 | €0 | €0 | €0 | 0% |
| SMS 2FA | Twilio | 40 | $0.05 | €1.85 | €2.00 | -7.5% |
| **Total** | - | - | - | **€1.85** | **€2.00** | **-7.5%** |

---

## 7. Troubleshooting

### Email Not Delivered

**Check SPF/DKIM**:
```bash
# SPF check
dig +short TXT meepleai.com | grep spf

# DKIM check
dig +short TXT s1._domainkey.meepleai.com

# If empty, DNS not propagated yet (wait 24-48h)
```

**Check SendGrid Activity**:
1. SendGrid → Activity
2. Filter by recipient email
3. **Status values**:
   - `processed`: Sent to ISP ✅
   - `delivered`: Confirmed delivery ✅
   - `bounce`: Invalid email ❌
   - `dropped`: Spam or invalid sender ❌

---

### TOTP Code Not Working

**Common Issues**:

**1. Time Sync Issue**:
```csharp
// Verify server time is accurate
var serverTime = DateTime.UtcNow;
Console.WriteLine($"Server UTC time: {serverTime}");

// User's device clock might be off
// Solution: Increase time window tolerance
var window = new VerificationWindow(previous: 2, future: 2);  // ±60s instead of ±30s
```

**2. Secret Not Encrypted Correctly**:
```csharp
// Verify encryption round-trip
var secret = totpService.GenerateSecret();
var encrypted = encryption.Encrypt(secret);
var decrypted = encryption.Decrypt(encrypted);

Assert.Equal(secret, decrypted);  // Must match
```

---

### SMS Not Delivered

**Check Twilio Logs**:
1. Console → Monitor → Logs → Messaging
2. Find SMS by recipient number
3. **Error Codes**:
   - `30007`: Carrier blocked (number invalid)
   - `30008`: Unknown destination (wrong country code)
   - `21610`: Number opted out of SMS

**Solution**:
- Verify phone number format: `+[country][number]` (e.g., `+393331234567`)
- Check user opted in to receive SMS
- Verify Twilio balance >$0

---

## 8. Security Best Practices Checklist

**Email Security**:
- [x] SPF record configured
- [x] DKIM signing enabled
- [x] DMARC policy set (p=quarantine)
- [ ] Monitor bounce rate (<5% target)
- [ ] Implement email validation before sending (syntax check)
- [ ] Rate limit password reset emails (max 3/hour per user)

**TOTP Security**:
- [x] Secrets encrypted at rest (AES-256)
- [x] Time window ±30s (RFC 6238 compliant)
- [x] Rate limit verification attempts (5/15min)
- [ ] Backup codes generated and hashed
- [ ] TOTP secret rotation (every 365 days recommended)

**SMS Security**:
- [x] Rate limit SMS sends (5/day per user)
- [x] Code expiry (5 minutes)
- [ ] Phone number verification (send confirmation code first)
- [ ] Detect SMS pumping fraud (sudden volume spike)
- [ ] Geographic restriction (only allow EU/US numbers if applicable)

---

## 9. Quick Reference

### Service Credentials Location

| Service | Credentials File | Environment Variables |
|---------|-----------------|----------------------|
| SendGrid | `infra/secrets/email.secret` | `SENDGRID_API_KEY` |
| AWS SES | `infra/secrets/email.secret` | `AWS_SES_SMTP_USERNAME`, `AWS_SES_SMTP_PASSWORD` |
| Twilio | `infra/secrets/sms.secret` | `TWILIO_ACCOUNT_SID`, `TWILIO_AUTH_TOKEN` |

---

### Support Contacts

| Service | Support Channel | Response Time |
|---------|----------------|---------------|
| SendGrid | https://support.sendgrid.com | 24-48h (free tier) |
| AWS SES | AWS Support (Developer plan) | 12-24h |
| Twilio | https://support.twilio.com | 24h (email) |
| Cloudflare | https://community.cloudflare.com | Community (varies) |

---

**Next Steps**:
1. Configure email templates in application
2. Implement 2FA enforcement for admin users
3. Setup monitoring for email deliverability
4. Test complete authentication flow (register → verify → login with 2FA)


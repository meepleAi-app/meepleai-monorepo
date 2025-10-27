# Two-Factor Authentication (2FA) User Guide

**Feature**: AUTH-07
**Status**: Available
**Type**: TOTP-based (Time-based One-Time Password)

---

## Overview

Two-factor authentication adds an extra layer of security to your MeepleAI account. After entering your password, you'll need to provide a 6-digit code from your authenticator app.

**Benefits**:
- Protects account even if password is compromised
- Industry-standard TOTP (works with all major authenticator apps)
- 10 backup codes for account recovery

---

## Enabling 2FA

### Prerequisites

- Authenticator app installed:
  - Google Authenticator (iOS/Android)
  - Microsoft Authenticator (iOS/Android)
  - Authy (iOS/Android/Desktop)
  - 1Password, Bitwarden, or other TOTP-compatible apps

### Steps

1. **Navigate to Settings**:
   - Visit `/settings` or click your profile → Settings
   - Find "Two-Factor Authentication" section

2. **Click "Enable Two-Factor Authentication"**:
   - System generates TOTP secret and 10 backup codes

3. **Scan QR Code**:
   - Open your authenticator app
   - Scan the displayed QR code
   - **Alternative**: Manual entry of secret key (if QR scan fails)

4. **Save Backup Codes**:
   - ⚠️ **CRITICAL**: Save the 10 backup codes in a secure location
   - Each code can only be used once
   - You WON'T be able to see them again
   - **Download** option available (saves as .txt file)

5. **Verify Setup**:
   - Enter the 6-digit code from your authenticator app
   - Click "Verify & Enable"
   - 2FA is now active on your account

---

## Logging In with 2FA

### Normal Flow

1. Enter email and password (as usual)
2. If 2FA is enabled, you'll see:
   - "Two-Factor Authentication Required" screen
   - Input field for 6-digit code
3. Open your authenticator app
4. Enter the current 6-digit code (refreshes every 30 seconds)
5. Click "Verify"
6. You're logged in ✅

### Using Backup Codes

If you don't have access to your authenticator app:

1. After entering password, you'll see 2FA screen
2. Enter a backup code instead of TOTP code
   - Format: `XXXX-XXXX` (8 characters)
   - Example: `A3F9-2K7L`
3. Backup code is consumed (single-use)
4. You're logged in ✅

**⚠️ Warning**: Backup codes are single-use. You have 10 codes. When running low, consider disabling and re-enabling 2FA to generate new codes.

---

## Disabling 2FA

### Steps

1. Go to `/settings`
2. In "Two-Factor Authentication" section, find "Disable" area
3. Enter:
   - Your current password
   - A 6-digit TOTP code OR a backup code
4. Click "Disable 2FA"
5. Confirm the warning dialog
6. 2FA is now disabled

**Note**: Disabling 2FA:
- Removes TOTP secret from your account
- Deletes all backup codes (used and unused)
- Your account will be less secure
- You can re-enable anytime

---

## Troubleshooting

### "Invalid verification code" Error

**Causes**:
- Code expired (TOTP codes change every 30 seconds)
- Time sync issue between server and phone
- Wrong authenticator app account

**Solutions**:
- Wait for code to refresh in app, try new code
- Check your phone's time is set to automatic
- Ensure you're looking at correct account in authenticator app

### Rate Limit (Too Many Attempts)

**Error**: "Too many attempts. Please wait a minute."

**Cause**: 3 failed verification attempts within 1 minute

**Solution**: Wait 60 seconds, then try again

### Lost Authenticator Device

**If you have backup codes**:
1. Login with email/password
2. Use a backup code when prompted for 2FA
3. Go to Settings → Disable 2FA
4. Re-enable 2FA with new device

**If you lost backup codes too**:
- Contact administrator
- Admin can disable 2FA for your account
- You'll receive email notification

### Backup Codes Running Low

**Warning**: "You have only X backup codes remaining"

**Recommended**:
1. Disable 2FA (requires password + TOTP/backup code)
2. Re-enable 2FA immediately
3. Save new set of 10 backup codes

---

## Security Best Practices

### ✅ DO

- Save backup codes in a secure password manager
- Print backup codes and store in safe place
- Use biometric lock on authenticator app
- Keep authenticator app up-to-date
- Test backup codes before relying on them

### ❌ DON'T

- Screenshot backup codes (can be leaked)
- Email backup codes to yourself
- Share TOTP secret with anyone
- Use same authenticator account for multiple services (use separate for MeepleAI)
- Ignore low backup codes warning

---

## Technical Details

### TOTP Standard

- **Algorithm**: TOTP (RFC 6238)
- **Time Step**: 30 seconds
- **Code Length**: 6 digits
- **Time Window**: ±60 seconds (tolerates clock skew)
- **Hash**: HMAC-SHA1 (standard)

### Backup Codes

- **Count**: 10 codes per enrollment
- **Format**: `XXXX-XXXX` (8 characters, no ambiguous chars)
- **Hashing**: PBKDF2 with 210,000 iterations (same as passwords)
- **Single-Use**: Each code can only be used once
- **Storage**: Hashed, not reversible

### Temp Sessions

- **Duration**: 5 minutes (between password and 2FA)
- **Token**: 256-bit cryptographically secure
- **Single-Use**: Consumed after successful 2FA
- **Rate Limit**: 3 verification attempts per minute

---

## FAQ

**Q: Is 2FA mandatory?**
A: No, 2FA is opt-in. You can continue using password-only authentication.

**Q: Can I use SMS instead of authenticator app?**
A: Not currently. We use TOTP (app-based) for better security.

**Q: What happens if I lose both my phone and backup codes?**
A: Contact support. Admin can disable 2FA for your account.

**Q: Can I have 2FA on multiple devices?**
A: Yes! Scan the same QR code on multiple devices during setup, or use backup codes.

**Q: Do backup codes expire?**
A: No, backup codes don't expire. They remain valid until used or you disable 2FA.

**Q: How do I generate new backup codes?**
A: Disable 2FA, then re-enable it. You'll get a new set of 10 codes.

---

## Support

**Issues**: https://github.com/DegrassiAaron/meepleai-monorepo/issues
**Docs**: `docs/issue/auth-07-2fa-implementation-spec.md`
**PR**: https://github.com/DegrassiAaron/meepleai-monorepo/pull/573

---

Generated with [Claude Code](https://claude.com/claude-code)
Feature: AUTH-07 - Two-Factor Authentication

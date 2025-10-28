# OAuth 2.0 User Guide

**Feature**: Social Login (Google, Discord, GitHub)
**Audience**: End Users
**Last Updated**: 2025-10-27

---

## Overview

MeepleAI supports logging in with your Google, Discord, or GitHub account. No password required—just click a button and authorize the app.

**Benefits**:
- ✅ **No Password**: No need to remember another password
- ✅ **Fast Login**: One-click authentication
- ✅ **Secure**: Powered by trusted providers (Google, Discord, GitHub)
- ✅ **Multi-Provider**: Link multiple accounts to the same MeepleAI profile

---

## Table of Contents

1. [Logging In with OAuth](#logging-in-with-oauth)
2. [Linking Additional Providers](#linking-additional-providers)
3. [Unlinking OAuth Accounts](#unlinking-oauth-accounts)
4. [Switching Between Accounts](#switching-between-accounts)
5. [Troubleshooting](#troubleshooting)
6. [Privacy & Security](#privacy--security)
7. [FAQ](#faq)

---

## Logging In with OAuth

### First-Time Login (New User)

1. Navigate to the [Login Page](https://app.meepleai.com/login)
2. Click one of the social login buttons:
   - **Continue with Google**
   - **Continue with Discord**
   - **Continue with GitHub**

3. **Authorize MeepleAI** on the provider's page:
   - Review requested permissions (email, profile)
   - Click **Allow** or **Authorize**

4. **Automatic Account Creation**:
   - MeepleAI creates your account using your email address
   - You're automatically logged in
   - Welcome message appears

**What MeepleAI Accesses**:
- **Email address**: Used as your unique identifier
- **Display name**: Used for your profile
- **Profile picture**: (Optional) Used for avatar

**What MeepleAI DOES NOT Access**:
- Private messages
- Friends list
- Activity history
- Files or documents

### Returning User Login

If you've already created an account:

1. Navigate to [Login Page](https://app.meepleai.com/login)
2. Click the provider you originally used:
   - If you signed up with **Google** → Click **Continue with Google**
   - If you signed up with **Discord** → Click **Continue with Discord**
   - If you signed up with **GitHub** → Click **Continue with GitHub**

3. Authorize (if required)
4. You're logged in automatically

**Linking Existing Account**:
- If you log in with a different provider using the **same email address**, MeepleAI automatically links the accounts
- Example: Signed up with Google? Logging in with Discord (same email) links both

---

## Linking Additional Providers

You can link multiple OAuth providers to the same MeepleAI account for flexibility.

### Why Link Multiple Providers?

- **Backup Access**: If one provider is down, use another
- **Convenience**: Login with whichever account you're already signed into
- **Single Profile**: All providers access the same MeepleAI account

### How to Link Providers

1. **Log in** to MeepleAI with any existing provider
2. Navigate to **Profile** or **Settings** page
3. Scroll to **Linked Accounts** section
4. Click **Link** next to the provider you want to add:
   - Link Google Account
   - Link Discord Account
   - Link GitHub Account

5. **Authorize** on the provider's page
6. **Confirmation**: Provider now linked to your account

**Example**:
```
Current Account: user@example.com
Linked Providers:
✅ Google (user@example.com)
❌ Discord (not linked) → Click "Link"
❌ GitHub (not linked)
```

After linking Discord:
```
Linked Providers:
✅ Google (user@example.com)
✅ Discord (user@example.com)
❌ GitHub (not linked)
```

**Note**: All linked providers must use the same email address.

---

## Unlinking OAuth Accounts

You can unlink OAuth providers if you no longer want to use them for login.

### When to Unlink

- You no longer use that provider
- Security concern (provider account compromised)
- Switching to a different login method

### How to Unlink Providers

1. **Log in** to MeepleAI
2. Navigate to **Profile** or **Settings**
3. Scroll to **Linked Accounts** section
4. Click **Unlink** next to the provider:
   - Unlink Google Account
   - Unlink Discord Account
   - Unlink GitHub Account

5. **Confirm Unlinking** in dialog:
   ```
   Are you sure you want to unlink your Google account?
   You will no longer be able to login with Google.
   [Cancel] [Unlink]
   ```

6. **Confirmation**: Provider removed from your account

**⚠️ Important**:
- You must have at least one login method active
- If you only have one OAuth provider linked, you cannot unlink it
- If you also have a password set, you can unlink all OAuth providers

**Safety Check**:
```
Linked Providers:
✅ Google (user@example.com) → Cannot unlink (only method)

vs.

Linked Providers:
✅ Google (user@example.com) → Can unlink
✅ Discord (user@example.com) → Can unlink
```

---

## Switching Between Accounts

### Using Multiple MeepleAI Accounts

If you have multiple MeepleAI accounts (different email addresses), you can switch between them:

1. **Log out** of current account (click profile → **Logout**)
2. **Log in** with a different provider/email:
   - Example: Switch from `work@example.com` (Google) to `personal@example.com` (Discord)

3. Each email address creates a separate MeepleAI account

**Recommendation**: Use a single email address and link multiple providers for unified access.

---

## Troubleshooting

### Issue: "Error during OAuth login"

**Symptoms**: Redirected to login page with error message

**Possible Causes**:
1. **Denied Authorization**: You clicked "Deny" or "Cancel" on provider page
2. **Network Issue**: Connection lost during OAuth flow
3. **Account Mismatch**: Provider email doesn't match MeepleAI account

**Solutions**:
- **Retry**: Click the OAuth button again
- **Check Email**: Ensure provider email matches your MeepleAI account
- **Clear Cookies**: Clear browser cookies and try again
- **Different Provider**: Try logging in with a different linked provider

### Issue: "Cannot link account - email mismatch"

**Symptom**: Error when trying to link a new provider

**Cause**: Provider email address differs from your MeepleAI account email

**Solution**:
- Verify email address on the provider (Google/Discord/GitHub settings)
- Use a provider with the same email address
- Or create a new MeepleAI account with the different email

**Example**:
```
MeepleAI Account: user@gmail.com
Trying to link: Discord (differentuser@example.com)
Result: ERROR - Email mismatch
```

### Issue: "Already linked to another account"

**Symptom**: "This provider is already linked to another MeepleAI account"

**Cause**: You're trying to link a provider that's already used by a different MeepleAI account

**Solution**:
1. Log out
2. Log in with the provider to access the other account
3. **Unlink** the provider from that account
4. Log back into your desired account
5. **Link** the provider

### Issue: "Rate limit exceeded"

**Symptom**: "Too many login attempts. Please try again later."

**Cause**: More than 10 OAuth login attempts in 1 minute

**Solution**:
- **Wait 60 seconds** and try again
- Avoid repeatedly clicking the OAuth button

### Issue: "Session expired"

**Symptom**: Redirected to login page while using the app

**Cause**: Session expired (default: 30 days of inactivity)

**Solution**:
- Click any OAuth button to log back in
- Your data is safe and preserved

---

## Privacy & Security

### What Data Does MeepleAI Store?

**From OAuth Providers**:
- ✅ Email address (unique identifier)
- ✅ Display name (for profile)
- ✅ Provider user ID (internal linking)
- ✅ OAuth access token (encrypted)
- ❌ Password (OAuth providers don't share passwords)

**Token Security**:
- All OAuth tokens encrypted at rest using industry-standard encryption
- Tokens never exposed in logs or error messages
- Automatic token refresh (when supported by provider)

### Can Providers See My MeepleAI Activity?

**No.** OAuth providers (Google, Discord, GitHub) cannot see:
- What games you search
- What questions you ask
- Your chat history
- Your MeepleAI usage patterns

**One-Way Access**: MeepleAI accesses your email/name from the provider, but providers cannot access your MeepleAI data.

### Revoking Access

You can revoke MeepleAI's access from the provider side:

**Google**:
1. Go to [Google Account Security](https://myaccount.google.com/permissions)
2. Find **MeepleAI**
3. Click **Remove Access**

**Discord**:
1. Go to [Discord Authorized Apps](https://discord.com/settings/authorized-apps)
2. Find **MeepleAI**
3. Click **Deauthorize**

**GitHub**:
1. Go to [GitHub Settings → Applications](https://github.com/settings/applications)
2. Find **MeepleAI** in **Authorized OAuth Apps**
3. Click **Revoke**

**After Revoking**:
- You'll need to re-authorize MeepleAI next time you log in
- Your MeepleAI account data remains intact
- No data is deleted

### GDPR & Data Deletion

To delete your MeepleAI account and all associated data:

1. Email **privacy@meepleai.com** with subject "Account Deletion Request"
2. Include your registered email address
3. Confirmation sent within 24 hours
4. Account and all data deleted within 30 days

**What Gets Deleted**:
- User profile
- Linked OAuth accounts
- Chat history
- Saved searches
- All personal data

---

## FAQ

### Q: Can I use OAuth login without a password?

**A:** Yes! OAuth login doesn't require a password. You authenticate through Google, Discord, or GitHub directly.

### Q: What if I lose access to my OAuth provider account?

**A:**
- If you have multiple providers linked, use another provider to log in
- If only one provider linked, contact support: **support@meepleai.com**
- We can help you recover access or set up a password-based login

### Q: Can I add a password later?

**A:** Yes, you can set a password for backup access:
1. Log in with OAuth
2. Go to **Settings** → **Security**
3. Click **Set Password**
4. Choose a strong password
5. Now you can login with either OAuth or password

### Q: Why does MeepleAI need my email address?

**A:** Your email is your unique identifier in MeepleAI. It allows:
- Account recovery
- Linking multiple OAuth providers
- Important notifications (optional)
- No marketing emails (we don't send them)

### Q: Is OAuth login secure?

**A:** Yes. OAuth 2.0 is an industry-standard security protocol used by millions of applications. MeepleAI implements:
- CSRF protection
- Token encryption
- Secure session management
- Rate limiting
- Regular security audits

See [OAuth Security Documentation](../security/oauth-security.md) for technical details.

### Q: Can I use different email addresses for different providers?

**A:** No. All OAuth providers linked to your account must use the same email address. This ensures account security and prevents unauthorized account linking.

### Q: What happens if I unlink all providers?

**A:** You can only unlink all OAuth providers if you have a password set. Otherwise, you must keep at least one provider linked to maintain account access.

### Q: Does MeepleAI sell my OAuth data?

**A:** **Absolutely not.** MeepleAI does not sell, rent, or share your personal data with third parties. Your OAuth data (email, name) is used solely for authentication and account management.

See our [Privacy Policy](https://app.meepleai.com/privacy) for full details.

---

## Additional Resources

### Related Guides

- [OAuth Setup Guide](oauth-setup-guide.md) - For administrators
- [OAuth Security Documentation](../security/oauth-security.md) - For developers

### Support

**Need Help?**
- Email: **support@meepleai.com**
- Response time: Within 24 hours
- Include: Your registered email address and detailed description of the issue

**Report Security Issues**:
- Email: **security@meepleai.com**
- We take security seriously and respond to all reports within 24 hours

---

**Guide Status**: ✅ COMPLETE
**Last Updated**: 2025-10-27
**Next Review**: 2026-01-27 (Quarterly)

# OAuth 2.0 Setup Guide

**Feature**: AUTH-06 OAuth Providers
**Audience**: DevOps Engineers, System Administrators
**Last Updated**: 2025-10-27

---

## Overview

This guide covers the complete setup process for OAuth 2.0 authentication with Google, Discord, and GitHub providers. Follow these steps to enable social login in MeepleAI.

**Prerequisites**:
- Admin access to Google Cloud Console, Discord Developer Portal, and GitHub Settings
- Production domain with HTTPS enabled
- Email address for OAuth app registration

---

## Table of Contents

1. [Google OAuth 2.0 Setup](#google-oauth-20-setup)
2. [Discord OAuth 2.0 Setup](#discord-oauth-20-setup)
3. [GitHub OAuth 2.0 Setup](#github-oauth-20-setup)
4. [Backend Configuration](#backend-configuration)
5. [Frontend Configuration](#frontend-configuration)
6. [Testing OAuth Flow](#testing-oauth-flow)
7. [Production Deployment](#production-deployment)
8. [Troubleshooting](#troubleshooting)

---

## Google OAuth 2.0 Setup

### Step 1: Create Google Cloud Project

1. Navigate to [Google Cloud Console](https://console.cloud.google.com/)
2. Click **Select a project** → **New Project**
3. Enter project details:
   - **Project name**: `MeepleAI` (or your app name)
   - **Organization**: (Optional)
   - **Location**: (Optional)
4. Click **Create**

### Step 2: Enable Google+ API

1. In the Google Cloud Console, navigate to **APIs & Services** → **Library**
2. Search for `Google+ API`
3. Click **Google+ API** → **Enable**

**Note**: Google+ API is required for user profile information (name, email).

### Step 3: Configure OAuth Consent Screen

1. Navigate to **APIs & Services** → **OAuth consent screen**
2. Select **External** user type → **Create**
3. Fill in required fields:

**App information**:
- **App name**: `MeepleAI`
- **User support email**: `support@meepleai.com` (your email)
- **App logo**: (Optional, 120x120 PNG)

**App domain**:
- **Application home page**: `https://app.meepleai.com` (your domain)
- **Application privacy policy link**: `https://app.meepleai.com/privacy`
- **Application terms of service link**: `https://app.meepleai.com/terms`

**Developer contact information**:
- **Email addresses**: `dev@meepleai.com`

4. Click **Save and Continue**

**Scopes** (Step 2):
- Click **Add or Remove Scopes**
- Select:
  - `openid` - OpenID Connect authentication
  - `profile` - User profile information
  - `email` - Email address
- Click **Update** → **Save and Continue**

**Test users** (Step 3, for development):
- Click **Add Users**
- Enter test email addresses (e.g., `test@example.com`)
- Click **Add** → **Save and Continue**

5. Review summary → **Back to Dashboard**

### Step 4: Create OAuth 2.0 Credentials

1. Navigate to **APIs & Services** → **Credentials**
2. Click **Create Credentials** → **OAuth client ID**
3. Select application type:
   - **Application type**: `Web application`
   - **Name**: `MeepleAI Web Client`

**Authorized JavaScript origins** (for development):
```
http://localhost:3000
http://localhost:8080
```

**Authorized redirect URIs**:
```
Development:
http://localhost:8080/api/v1/auth/oauth/google/callback

Production:
https://api.meepleai.com/api/v1/auth/oauth/google/callback
```

4. Click **Create**
5. **Copy credentials**:
   - **Client ID**: `1234567890-abcdefghijklmnopqrstuvwxyz123456.apps.googleusercontent.com`
   - **Client Secret**: `GOCSPX-abcdefghijklmnopqrstuvwxyz`

**⚠️ Security**: Store Client Secret securely (environment variable, never commit to git)

### Step 5: Publish OAuth Consent Screen (Production)

For production deployment:
1. Navigate to **OAuth consent screen**
2. Click **Publish App**
3. Confirm **Make App Public**

**Verification** (if required):
- Google may require app verification for sensitive scopes
- Verification takes 4-6 weeks
- Required for >100 users
- See: https://support.google.com/cloud/answer/9110914

---

## Discord OAuth 2.0 Setup

### Step 1: Create Discord Application

1. Navigate to [Discord Developer Portal](https://discord.com/developers/applications)
2. Click **New Application**
3. Enter application details:
   - **Name**: `MeepleAI`
   - **Team**: (Optional, for team ownership)
4. Read and accept **Developer Terms of Service**
5. Click **Create**

### Step 2: Configure OAuth2 Settings

1. In the application page, navigate to **OAuth2** → **General**
2. **Client Information**:
   - **Client ID**: (auto-generated, copy this)
   - **Client Secret**: Click **Reset Secret** → **Copy** (only shown once)

**⚠️ Security**: Client Secret shown only once. Store securely immediately.

3. **Redirects**:
   - Click **Add Redirect**

Development:
```
http://localhost:8080/api/v1/auth/oauth/discord/callback
```

Production:
```
https://api.meepleai.com/api/v1/auth/oauth/discord/callback
```

4. **Default Authorization Link**: Leave blank (app generates dynamically)
5. Click **Save Changes**

### Step 3: Configure Application Settings

1. Navigate to **General Information**
2. Fill in application details:

**Description**: `AI-powered board game rules assistant`

**App Icon**: Upload 512x512 PNG (optional)

**Tags**: `games`, `ai`, `assistant` (max 5 tags)

3. Click **Save Changes**

### Step 4: OAuth2 URL Generator (for testing)

1. Navigate to **OAuth2** → **URL Generator**
2. Select scopes:
   - `identify` - Read user ID, username, avatar
   - `email` - Read user email address
3. **Redirect URL**: Select from configured redirects
4. **Generated URL**: Copy for manual testing

**Example**:
```
https://discord.com/api/oauth2/authorize?client_id=1234567890&redirect_uri=http%3A%2F%2Flocalhost%3A8080%2Fapi%2Fv1%2Fauth%2Foauth%2Fdiscord%2Fcallback&response_type=code&scope=identify%20email
```

---

## GitHub OAuth 2.0 Setup

### Step 1: Create GitHub OAuth App

**For Personal Account**:
1. Navigate to [GitHub Settings](https://github.com/settings/developers)
2. Click **OAuth Apps** → **New OAuth App**

**For Organization**:
1. Navigate to **Organization Settings** → **Developer settings** → **OAuth Apps**
2. Click **New OAuth App**

### Step 2: Register Application

Fill in application details:

**Application name**: `MeepleAI`

**Homepage URL**:
```
Development: http://localhost:3000
Production: https://app.meepleai.com
```

**Application description** (optional):
```
AI-powered board game rules assistant with semantic search and natural language Q&A
```

**Authorization callback URL**:
```
Development:
http://localhost:8080/api/v1/auth/oauth/github/callback

Production:
https://api.meepleai.com/api/v1/auth/oauth/github/callback
```

**Enable Device Flow**: Leave unchecked (not required)

Click **Register application**

### Step 3: Generate Client Secret

1. After registration, you'll see the **Client ID** (copy this)
2. Click **Generate a new client secret**
3. **Copy** the client secret immediately (shown only once)

**⚠️ Security**: Client Secret shown only once. Store in password manager or secrets vault.

### Step 4: Configure Application Permissions (Optional)

**Webhook URL**: Leave blank (not used for OAuth)

**Active**: Leave checked

**Permissions**: OAuth apps have limited permissions by default:
- `read:user` - Read user profile data
- `user:email` - Read user email addresses

These are automatically requested during OAuth flow.

### Step 5: Update Application Settings (Production)

For production deployment:

1. Navigate to **Settings** → **OAuth Apps** → **MeepleAI**
2. Update **Homepage URL** to production domain
3. Update **Authorization callback URL** to production API endpoint
4. Add **Application logo** (200x200 PNG, optional)
5. Click **Update application**

---

## Backend Configuration

### Step 1: Set Environment Variables

Create or update environment files:

**Development** (`infra/env/api.env.dev`):
```bash
# Google OAuth
GOOGLE_OAUTH_CLIENT_ID=1234567890-abcdefghijklmnopqrstuvwxyz123456.apps.googleusercontent.com
GOOGLE_OAUTH_CLIENT_SECRET=GOCSPX-abcdefghijklmnopqrstuvwxyz

# Discord OAuth
DISCORD_OAUTH_CLIENT_ID=1234567890
DISCORD_OAUTH_CLIENT_SECRET=abcdefghijklmnopqrstuvwxyz123456

# GitHub OAuth
GITHUB_OAUTH_CLIENT_ID=Iv1.1234567890abcdef
GITHUB_OAUTH_CLIENT_SECRET=abcdef1234567890abcdef1234567890abcdef12
```

**Production** (`infra/env/api.env.prod`):
```bash
# Use same format, but with production credentials
GOOGLE_OAUTH_CLIENT_ID=${GOOGLE_OAUTH_CLIENT_ID_PROD}
GOOGLE_OAUTH_CLIENT_SECRET=${GOOGLE_OAUTH_CLIENT_SECRET_PROD}
# ... (repeat for Discord, GitHub)
```

**⚠️ Security**:
- Never commit `.env.dev` or `.env.prod` files to git
- Use secrets management (Azure Key Vault, AWS Secrets Manager, HashiCorp Vault)
- Rotate secrets every 90 days

### Step 2: Update appsettings.json

The OAuth configuration is already present in `apps/api/src/Api/appsettings.json`:

```json
{
  "Authentication": {
    "OAuth": {
      "CallbackBaseUrl": "http://localhost:8080",
      "Providers": {
        "Google": {
          "ClientId": "${GOOGLE_OAUTH_CLIENT_ID}",
          "ClientSecret": "${GOOGLE_OAUTH_CLIENT_SECRET}",
          "AuthorizationUrl": "https://accounts.google.com/o/oauth2/v2/auth",
          "TokenUrl": "https://oauth2.googleapis.com/token",
          "UserInfoUrl": "https://www.googleapis.com/oauth2/v2/userinfo",
          "Scope": "openid profile email"
        },
        "Discord": {
          "ClientId": "${DISCORD_OAUTH_CLIENT_ID}",
          "ClientSecret": "${DISCORD_OAUTH_CLIENT_SECRET}",
          "AuthorizationUrl": "https://discord.com/api/oauth2/authorize",
          "TokenUrl": "https://discord.com/api/oauth2/token",
          "UserInfoUrl": "https://discord.com/api/users/@me",
          "Scope": "identify email"
        },
        "GitHub": {
          "ClientId": "${GITHUB_OAUTH_CLIENT_ID}",
          "ClientSecret": "${GITHUB_OAUTH_CLIENT_SECRET}",
          "AuthorizationUrl": "https://github.com/login/oauth/authorize",
          "TokenUrl": "https://github.com/login/oauth/access_token",
          "UserInfoUrl": "https://api.github.com/user",
          "Scope": "read:user user:email"
        }
      }
    }
  }
}
```

**Production** (`apps/api/src/Api/appsettings.Production.json`):
```json
{
  "Authentication": {
    "OAuth": {
      "CallbackBaseUrl": "https://api.meepleai.com"
    }
  }
}
```

### Step 3: Configure Data Protection Keys (Production)

OAuth tokens are encrypted using ASP.NET Core Data Protection API. For production, configure persistent key storage:

**Option 1: Azure Key Vault** (Recommended for Azure deployments)
```csharp
// apps/api/src/Api/Program.cs
builder.Services.AddDataProtection()
    .PersistKeysToAzureBlobStorage(new Uri("https://myaccount.blob.core.windows.net/keys/keys.xml"))
    .ProtectKeysWithAzureKeyVault(new Uri("https://myvault.vault.azure.net/keys/dataprotection"), credential);
```

**Option 2: Redis** (Recommended for multi-instance)
```csharp
builder.Services.AddDataProtection()
    .PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys");
```

**Option 3: File System** (Single-instance only)
```csharp
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/var/lib/meepleai/keys"));
```

---

## Frontend Configuration

### Step 1: Update Environment Variables

**Development** (`apps/web/.env.local`):
```bash
NEXT_PUBLIC_API_BASE=http://localhost:8080
```

**Production** (`apps/web/.env.production`):
```bash
NEXT_PUBLIC_API_BASE=https://api.meepleai.com
```

### Step 2: Verify OAuth Components

OAuth components are already implemented in the frontend:

**Files**:
- `apps/web/src/components/auth/OAuthButtons.tsx` - Social login buttons
- `apps/web/src/pages/auth/callback.tsx` - OAuth callback handler
- `apps/web/src/pages/login.tsx` - Login page with OAuth buttons

**No additional configuration required** for frontend.

---

## Testing OAuth Flow

### Local Development Testing

1. **Start Services**:
```bash
# Terminal 1: Start database
cd infra && docker compose up postgres redis

# Terminal 2: Start API
cd apps/api/src/Api && dotnet run

# Terminal 3: Start frontend
cd apps/web && pnpm dev
```

2. **Navigate to Login Page**:
```
http://localhost:3000/login
```

3. **Test OAuth Flow**:
   - Click **Continue with Google** (or Discord/GitHub)
   - Authorize application on provider page
   - Verify redirect to `http://localhost:3000/auth/callback?success=true`
   - Verify session created (check browser cookies: `X-Session-Token`)

4. **Verify Database**:
```sql
-- Check user created
SELECT * FROM users WHERE email = 'your-test-email@example.com';

-- Check OAuth account linked
SELECT * FROM oauth_accounts WHERE user_id = '<user-id-from-above>';

-- Verify token encrypted
SELECT access_token_encrypted FROM oauth_accounts;  -- Should be encrypted string
```

### Manual API Testing

**Step 1: Initiate OAuth Flow**
```bash
curl -i http://localhost:8080/api/v1/auth/oauth/google/login
```

Expected response:
```
HTTP/1.1 302 Found
Location: https://accounts.google.com/o/oauth2/v2/auth?response_type=code&client_id=...&state=...
```

**Step 2: Complete OAuth Flow**
1. Copy the `Location` URL from response
2. Open in browser
3. Authorize application
4. Browser redirects to callback URL
5. Verify session cookie set

**Step 3: Verify Session**
```bash
curl -i -b cookies.txt http://localhost:8080/api/v1/auth/me
```

Expected response:
```json
{
  "user": {
    "id": "...",
    "email": "test@example.com",
    "displayName": "Test User",
    "role": "User"
  },
  "expiresAt": "2025-10-28T12:00:00Z"
}
```

---

## Production Deployment

### Pre-Deployment Checklist

- [ ] **OAuth Apps Registered**: All 3 providers (Google, Discord, GitHub)
- [ ] **Callback URLs**: Updated to production domain (HTTPS)
- [ ] **Environment Variables**: Secrets configured in production secrets manager
- [ ] **Data Protection Keys**: Persistent storage configured (Redis/Azure Key Vault)
- [ ] **CallbackBaseUrl**: Set to production API domain with HTTPS
- [ ] **FrontendUrl**: Set to production frontend domain
- [ ] **HTTPS Enabled**: SSL/TLS certificate valid
- [ ] **Rate Limiting**: Configured in `appsettings.Production.json`
- [ ] **Logging**: Seq/Jaeger/Application Insights configured

### Deployment Steps

1. **Update OAuth Callback URLs**:
   - Google Cloud Console: Add production callback URL
   - Discord Developer Portal: Add production callback URL
   - GitHub OAuth Apps: Update callback URL

2. **Configure Production Environment**:
```bash
# Set environment variables in deployment platform
export GOOGLE_OAUTH_CLIENT_ID=<production-client-id>
export GOOGLE_OAUTH_CLIENT_SECRET=<production-client-secret>
# ... (repeat for Discord, GitHub)
```

3. **Deploy Backend**:
```bash
cd apps/api
dotnet publish -c Release -o ./publish
# Deploy to production server/container
```

4. **Deploy Frontend**:
```bash
cd apps/web
pnpm build
# Deploy to production hosting (Vercel, Netlify, etc.)
```

5. **Run Database Migrations**:
```bash
# Migrations auto-apply on startup, or run manually:
cd apps/api/src/Api
dotnet ef database update --connection "<production-connection-string>"
```

6. **Verify Production OAuth Flow**:
   - Navigate to `https://app.meepleai.com/login`
   - Test all 3 OAuth providers
   - Verify HTTPS callback URLs
   - Check logs for any errors

### Post-Deployment Validation

```bash
# Test OAuth login endpoint
curl -i https://api.meepleai.com/api/v1/auth/oauth/google/login

# Expected: 302 redirect to Google OAuth

# Verify HTTPS enforcement
curl -i http://api.meepleai.com/api/v1/auth/oauth/google/login
# Expected: Redirect to HTTPS or 400 error
```

---

## Troubleshooting

### Common Issues

#### 1. "Invalid OAuth client" Error

**Symptom**: Error message during OAuth authorization

**Causes**:
- Client ID mismatch
- OAuth app not published (Google)
- Incorrect redirect URI

**Solutions**:
```bash
# Verify Client ID in environment
echo $GOOGLE_OAUTH_CLIENT_ID

# Check redirect URI matches exactly
# Google: https://console.cloud.google.com/apis/credentials
# Discord: https://discord.com/developers/applications
# GitHub: https://github.com/settings/developers
```

#### 2. "Redirect URI mismatch" Error

**Symptom**: Error after clicking "Authorize" on provider page

**Cause**: Callback URL not registered in OAuth app

**Solution**:
1. Check configured callback URL in provider console
2. Verify `CallbackBaseUrl` in appsettings.json
3. Ensure exact match (including HTTP/HTTPS, port, trailing slash)

**Example**:
```
Registered: http://localhost:8080/api/v1/auth/oauth/google/callback
Actual:     http://localhost:8080/api/v1/auth/oauth/google/callback/
                                                                    ^^^ MISMATCH
```

#### 3. "Invalid state parameter" Error

**Symptom**: Error on callback: `error=oauth_failed`

**Causes**:
- App restarted during OAuth flow (in-memory state lost)
- State expired (>10 minutes)
- CSRF attack detected

**Solutions**:
```bash
# Check backend logs
docker logs meepleai-api | grep "Invalid OAuth state"

# If app restarted, retry OAuth flow
# If persistent, check for clock skew
date  # Verify server time
```

#### 4. "Rate limit exceeded" Error

**Symptom**: HTTP 429 Too Many Requests

**Cause**: >10 OAuth requests per minute from same IP

**Solution**:
```bash
# Wait 60 seconds and retry
sleep 60

# Or increase rate limit in appsettings.json
"RateLimit": {
  "OAuth": {
    "MaxTokens": 20,  // Increased from 10
    "RefillRate": 0.33333  // 20 tokens per minute
  }
}
```

#### 5. Tokens Not Encrypted in Database

**Symptom**: `access_token_encrypted` column contains plaintext JSON

**Cause**: Data Protection API not configured

**Solution**:
```csharp
// Verify DI registration in Program.cs
builder.Services.AddDataProtection();

// Check encryption service registered
builder.Services.AddScoped<IEncryptionService, EncryptionService>();

// Restart application
dotnet run
```

### Debug Logging

Enable detailed OAuth logging:

**appsettings.Development.json**:
```json
{
  "Logging": {
    "LogLevel": {
      "Api.Services.OAuthService": "Debug",
      "Api.Services.EncryptionService": "Debug"
    }
  }
}
```

**Restart API** and check logs:
```bash
cd apps/api/src/Api && dotnet run | grep -i oauth
```

---

## Additional Resources

### Provider Documentation

- [Google OAuth 2.0 Setup](https://developers.google.com/identity/protocols/oauth2)
- [Discord OAuth 2.0 Setup](https://discord.com/developers/docs/topics/oauth2)
- [GitHub OAuth Setup](https://docs.github.com/en/apps/oauth-apps/building-oauth-apps)

### Security Best Practices

- [OAuth 2.0 Security Best Practices (RFC 8252)](https://datatracker.ietf.org/doc/html/rfc8252)
- [OWASP OAuth Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/OAuth2_Cheat_Sheet.html)

### MeepleAI Documentation

- [OAuth Security Documentation](../security/oauth-security.md)
- [OAuth User Guide](oauth-user-guide.md)
- [CLAUDE.md](../../CLAUDE.md) - Complete API reference

---

**Guide Status**: ✅ COMPLETE
**Last Tested**: 2025-10-27
**Next Review**: 2026-01-27 (Quarterly)

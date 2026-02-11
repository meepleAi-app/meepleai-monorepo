# BoardGameGeek API Token Setup

## Overview

**As of January 2026**, BoardGameGeek requires authentication for all XML API v2 requests. This guide explains how to obtain and configure your BGG API token.

## Background

BoardGameGeek announced mandatory authentication for the XML API (referred to as "XML APIcalypse") to manage API usage and prevent abuse. All applications using the BGG XML API must now register and use authentication tokens.

## Obtaining Your BGG API Token

### Step 1: Register Your Application

1. **Visit BGG API Registration**:
   - Go to: https://boardgamegeek.com/using_the_xml_api
   - Or: https://boardgamegeek.com/applications (if available)

2. **Create an Application**:
   - Click "Register New Application"
   - Fill in application details:
     - **Name**: MeepleAI
     - **Description**: AI-powered board game rules assistant
     - **Website**: https://meepleai.dev (or your deployment URL)
     - **Callback URL**: Not required for XML API

3. **Submit Registration**:
   - Submit the form
   - Wait for approval (usually instant for development)

4. **Copy Your Token**:
   - Once approved, you'll receive an API token
   - Copy the token (format: long alphanumeric string)

### Step 2: Configure in Local Development

```bash
# Navigate to secrets directory
cd infra/secrets

# Replace placeholder with your actual token
echo "your-actual-bgg-token-here" > bgg-api-token.txt
```

### Step 3: Restart API Container

```bash
cd infra
docker compose restart api

# Verify API is healthy
docker compose ps api
```

### Step 4: Verify Token Works

```bash
# Test BGG search endpoint (after logging in)
curl "http://localhost:8080/api/v1/bgg/search?q=Catan" \
  -H "Cookie: meepleai_session=your-session-cookie"

# Should return search results, not 401/500 errors
```

## Environment Variables

The BGG token is loaded automatically from Docker Secrets:

**Docker Compose**:
```yaml
services:
  api:
    environment:
      BGG_API_TOKEN_FILE: /run/secrets/bgg-api-token
    secrets:
      - bgg-api-token

secrets:
  bgg-api-token:
    file: ./secrets/bgg-api-token.txt
```

**Direct .NET Run** (without Docker):
```bash
# Set environment variable
export BGG_API_TOKEN="your-token-here"

# Or use secret file
export BGG_API_TOKEN_FILE="/path/to/bgg-api-token.txt"
```

## Configuration Details

### BggConfiguration (appsettings.json)

```json
{
  "Bgg": {
    "BaseUrl": "https://boardgamegeek.com/xmlapi2",
    "ApiToken": null,
    "CacheTtlDays": 7,
    "MaxRequestsPerSecond": 2.0,
    "RetryCount": 3,
    "RetryDelaySeconds": 2,
    "TimeoutSeconds": 30
  }
}
```

### Authorization Header

The token is automatically added to all BGG API requests:

```http
GET /xmlapi2/search?query=Catan HTTP/1.1
Host: boardgamegeek.com
User-Agent: MeepleAI/1.0 (https://meepleai.dev)
Authorization: Bearer your-actual-token-here
```

## Troubleshooting

### 401 Unauthorized Errors

**Symptoms**:
```
HTTP 401 Unauthorized
BoardGameGeek API is currently unavailable
```

**Solutions**:
1. **Verify token file exists**:
   ```bash
   cat infra/secrets/bgg-api-token.txt
   ```

2. **Check token is loaded**:
   ```bash
   docker compose exec api env | grep BGG
   ```

3. **Verify token is valid**:
   - Test directly: `curl "https://boardgamegeek.com/xmlapi2/search?query=Catan" -H "Authorization: Bearer your-token"`
   - Should return XML, not 401

4. **Regenerate token** if expired:
   - Tokens may have expiration periods
   - Regenerate at https://boardgamegeek.com/using_the_xml_api

### Rate Limiting

BGG enforces strict rate limits:
- **Max 2 requests/second**
- **5-second wait between requests** (recommendation)

MeepleAI includes automatic rate limiting and caching:
- Cache TTL: 7 days (configurable)
- Rate limit: 2 req/s with token bucket algorithm
- Automatic retry with exponential backoff

### Token Security

**DO**:
- ✅ Store token in `infra/secrets/` (gitignored)
- ✅ Use Docker Secrets in production
- ✅ Rotate tokens periodically

**DON'T**:
- ❌ Commit token to git
- ❌ Share token publicly
- ❌ Use same token for multiple environments

## Production Deployment

For production environments:

1. **Store token securely**:
   ```bash
   # Kubernetes Secrets
   kubectl create secret generic bgg-api-token \
     --from-literal=token=your-token-here

   # Docker Swarm Secrets
   echo "your-token-here" | docker secret create bgg-api-token -
   ```

2. **Environment-specific tokens**:
   - Development: `bgg-api-token-dev`
   - Staging: `bgg-api-token-staging`
   - Production: `bgg-api-token-prod`

3. **Monitor usage**:
   - BGG may provide usage dashboards
   - Monitor for rate limit warnings in application logs

## References

- [BGG XML API Registration](https://boardgamegeek.com/using_the_xml_api)
- [XML API Discussion Thread](https://boardgamegeek.com/thread/3492262/registration-and-authorization-coming-to-the-xml-a)
- [BGG Developer Guild](https://boardgamegeek.com/guild/1229)

---

**Last Updated**: 2026-01-14
**Issue**: AI-13 BGG API Integration
**Related**: Issue #2152 (PostgreSQL credentials), SEC-708 (Docker Secrets)

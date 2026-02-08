# Feature Flags Tier-Based Access Matrix

**Issue**: #3674 - Feature Flag Tier-Based Access Verification
**Parent Epic**: #3327
**Last Updated**: 2026-02-06

## Overview

MeepleAI uses a tier-based feature flag system to gate functionality by user subscription level. Feature access is determined by combining **role-based** and **tier-based** checks - both must allow access.

## Feature Matrix

| Feature | Key | Free | Normal | Premium | Admin |
|---------|-----|:----:|:------:|:-------:|:-----:|
| Basic Chat | `basic_chat` | Yes | Yes | Yes | Yes |
| Chat Export | `chat_export` | Yes | Yes | Yes | Yes |
| Custom Collections | `custom_collections` | Yes | Yes | Yes | Yes |
| Advanced RAG | `advanced_rag` | No | Yes | Yes | Yes |
| PDF OCR Processing | `pdf_ocr` | No | Yes | Yes | Yes |
| Advanced Search | `advanced_search` | No | Yes | Yes | Yes |
| Game Recommendations | `game_recommendations` | No | Yes | Yes | Yes |
| Multi-Agent AI | `multi_agent` | No | No | Yes | Yes |
| Bulk Import | `bulk_import` | No | No | Yes | Yes |
| API Access | `api_access` | No | No | Yes | Yes |

## Tier Hierarchy

```
Admin (unrestricted) > Premium > Normal > Free
```

- **Free**: Basic features only - chat, export, collections
- **Normal**: Core features - adds RAG, PDF, search, recommendations
- **Premium**: Full access - adds multi-agent, bulk import, API access
- **Admin**: All features, bypasses tier restrictions

## Architecture

### Access Control Flow

```
Request → Session Filter → Feature Filter → Endpoint
                              |
                    FeatureFlagService
                    .CanAccessFeatureAsync()
                              |
                    Role Check AND Tier Check
                              |
                    Both must be true → Access granted
```

### Configuration Storage

Feature flags are stored as `SystemConfiguration` entries with category `"FeatureFlags"`:

| Config Key Format | Example | Description |
|-------------------|---------|-------------|
| `{feature}` | `basic_chat` | Global flag |
| `{feature}.{Role}` | `basic_chat.Admin` | Role-specific override |
| `{feature}.Tier.{tier}` | `advanced_rag.Tier.free` | Tier-specific flag |

**Hierarchy**: Tier-specific > Global > Default (true for backward compat)

### Key Components

| Component | File | Purpose |
|-----------|------|---------|
| Service Interface | `Services/IFeatureFlagService.cs` | Feature flag operations |
| Service Impl | `Services/FeatureFlagService.cs` | Role + tier checking logic |
| Endpoint Filter | `Filters/RequireFeatureFilter.cs` | `[RequireFeature]` gate |
| Extension | `Extensions/EndpointFilterExtensions.cs` | `.RequireFeature()` fluent API |
| User Endpoint | `Routing/UserProfileEndpoints.cs` | `GET /users/me/features` |
| Admin Endpoints | `Routing/FeatureFlagEndpoints.cs` | CRUD + tier enable/disable |
| Seed Data | `Infrastructure/Seeders/FeatureFlagSeeder.cs` | Default configuration |
| Query | `GetUserAvailableFeaturesQuery.cs` | User feature access query |

### API Endpoints

**Admin Management**:
- `GET /admin/feature-flags` - List all flags
- `GET /admin/feature-flags/{key}` - Get specific flag
- `POST /admin/feature-flags` - Create flag
- `PUT /admin/feature-flags/{key}` - Update with role/tier
- `POST /admin/feature-flags/{key}/toggle` - Toggle on/off
- `POST /admin/feature-flags/{key}/tier/{tier}/enable` - Enable for tier
- `POST /admin/feature-flags/{key}/tier/{tier}/disable` - Disable for tier

**User Access**:
- `GET /users/me/features` - Get current user's available features

## Usage

### Gating an Endpoint

```csharp
group.MapGet("/advanced-search", handler)
    .RequireSession()
    .RequireFeature("advanced_search");
```

### Checking in Code

```csharp
// Via IFeatureFlagService
var hasAccess = await featureFlagService.CanAccessFeatureAsync(user, "multi_agent");

// Via CQRS query
var features = await mediator.Send(new GetUserAvailableFeaturesQuery { UserId = userId });
```

### Frontend Integration

```typescript
// Fetch user features
const response = await fetch('/api/v1/users/me/features');
const features: UserFeatureDto[] = await response.json();

// Conditional rendering
const canUseMultiAgent = features.find(f => f.key === 'multi_agent')?.hasAccess;
```

## Adding New Features

1. Add seed data in `FeatureFlagSeeder.DefaultFeatureFlags`
2. Update this matrix documentation
3. Optionally gate endpoints with `.RequireFeature("feature_name")`
4. Admin can also create flags at runtime via `POST /admin/feature-flags`

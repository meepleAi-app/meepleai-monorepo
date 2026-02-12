# Issue #4060: Permission Data Model & Schema

**Epic**: #4068 - MeepleCard Enhancements
**Area**: Permission System (1/3)
**Estimate**: 3-4 giorni
**Priority**: P1-High (Critical Path)

---

## 📋 Acceptance Criteria

### AC1: Tier Schema Definition
- [ ] TypeScript enum `UserTier` con valori: `Free`, `Normal`, `Pro`, `Enterprise`
- [ ] Zod schema `TierSchema` per validazione
- [ ] Tier hierarchy mapping: `Free < Normal < Pro < Enterprise`
- [ ] Helper `hasMinimumTier(userTier, requiredTier): boolean`

### AC2: Role Schema Definition
- [ ] TypeScript enum `UserRole` con valori: `user`, `editor`, `creator`, `admin`, `superAdmin`
- [ ] Zod schema `RoleSchema` per validazione
- [ ] Role hierarchy mapping per permission inheritance
- [ ] Helper `hasMinimumRole(userRole, requiredRole): boolean`

### AC3: State Schema Definition
- [ ] `GamePublicationState`: `draft` | `published` | `archived`
- [ ] `CollectionVisibility`: `private` | `shared` | `public`
- [ ] `DocumentProcessingState`: `pending` | `processing` | `ready` | `failed`
- [ ] `UserAccountStatus`: `active` | `suspended` | `banned`

### AC4: Permission Model
- [ ] Interface `Permission` con campi:
  - `featureName: string` (es: "wishlist", "bulk-select", "drag-drop")
  - `requiredTier?: UserTier` (opzionale)
  - `requiredRole?: UserRole` (opzionale)
  - `allowedStates?: EntityState[]` (opzionale)
  - `logic: 'OR' | 'AND'` (default: 'OR')
- [ ] Feature permission registry (map feature → permission config)

### AC5: Database Schema
- [ ] `User` table: add `tier` e `role` columns
- [ ] Migration script con default values (Free tier, user role)
- [ ] Index su `tier` e `role` per query performance

### AC6: API Endpoints
- [ ] `GET /api/v1/auth/me/permissions` → current user permissions
- [ ] `GET /api/v1/permissions/check?feature={name}` → check access
- [ ] Response format:
  ```json
  {
    "hasAccess": true,
    "reason": "tier_sufficient | role_sufficient | both_required",
    "userTier": "Pro",
    "userRole": "admin",
    "required": {
      "tier": "Normal",
      "role": "user",
      "logic": "OR"
    }
  }
  ```

---

## 🔧 API Contract

### Permission Check Request
```typescript
POST /api/v1/permissions/check
{
  "featureName": "wishlist",
  "resourceId": "game-123",  // optional
  "resourceType": "game",    // optional
  "resourceState": "published" // optional
}
```

### Permission Check Response
```typescript
{
  "hasAccess": boolean,
  "reason": "tier_sufficient" | "role_sufficient" | "state_allowed" | "denied",
  "details": {
    "userTier": "Pro",
    "userRole": "admin",
    "userStatus": "active",
    "required": {
      "tier": "Normal",
      "role": "user",
      "states": ["published", "draft"]
    },
    "logic": "OR"
  }
}
```

### Feature Permission Registry
```typescript
const FEATURE_PERMISSIONS: Record<string, Permission> = {
  "wishlist": {
    requiredTier: "Free",  // tutti
    requiredRole: "user",
    logic: "OR"
  },
  "bulk-select": {
    requiredTier: "Pro",
    requiredRole: "editor",
    logic: "OR"
  },
  "drag-drop": {
    requiredTier: "Normal",
    requiredRole: "user",
    logic: "OR"
  },
  "quick-actions.delete": {
    requiredTier: undefined,  // tier non rilevante
    requiredRole: "admin",
    logic: "AND"  // DEVE essere admin
  }
};
```

---

## 📊 Data Model

```typescript
// Enums
enum UserTier {
  Free = 'Free',
  Normal = 'Normal',
  Pro = 'Pro',
  Enterprise = 'Enterprise'
}

enum UserRole {
  User = 'user',
  Editor = 'editor',
  Creator = 'creator',
  Admin = 'admin',
  SuperAdmin = 'superAdmin'
}

enum GamePublicationState {
  Draft = 'draft',
  Published = 'published',
  Archived = 'archived'
}

enum CollectionVisibility {
  Private = 'private',
  Shared = 'shared',
  Public = 'public'
}

enum DocumentProcessingState {
  Pending = 'pending',
  Processing = 'processing',
  Ready = 'ready',
  Failed = 'failed'
}

enum UserAccountStatus {
  Active = 'active',
  Suspended = 'suspended',
  Banned = 'banned'
}

// Permission Model
interface Permission {
  featureName: string;
  requiredTier?: UserTier;
  requiredRole?: UserRole;
  allowedStates?: string[];
  logic: 'OR' | 'AND';
}

interface PermissionCheckContext {
  userTier: UserTier;
  userRole: UserRole;
  userStatus: UserAccountStatus;
  resourceState?: string;
}

interface PermissionCheckResult {
  hasAccess: boolean;
  reason: 'tier_sufficient' | 'role_sufficient' | 'state_allowed' | 'denied';
  details: {
    userTier: UserTier;
    userRole: UserRole;
    required: {
      tier?: UserTier;
      role?: UserRole;
      states?: string[];
    };
    logic: 'OR' | 'AND';
  };
}
```

---

## ✅ Testing Checklist

### Unit Tests
- [ ] Tier hierarchy comparison (Free < Normal < Pro < Enterprise)
- [ ] Role hierarchy comparison
- [ ] Permission logic OR (pass se tier OR role sufficiente)
- [ ] Permission logic AND (pass solo se tier AND role sufficienti)
- [ ] State-based filtering (draft accessible solo a creator)

### Integration Tests
- [ ] API endpoint `/permissions/check` con vari scenari
- [ ] Database migration successful
- [ ] Default values applicati correttamente

### Edge Cases
- [ ] User senza tier (fallback a Free)
- [ ] User senza role (fallback a user)
- [ ] Feature non in registry (deny by default)
- [ ] Banned user (deny always)

---

## 🔗 Dependencies

**Blocked by**: Nessuna (foundation layer)
**Blocks**: #4061 (Permission Hooks), #4062 (MeepleCard Integration)

---

## 📚 Resources

- **Zod Docs**: https://zod.dev/
- **CASL (Authorization Library)**: https://casl.js.org/v6/en/
- **Prisma Enums**: https://www.prisma.io/docs/concepts/components/prisma-schema/data-model#defining-enums

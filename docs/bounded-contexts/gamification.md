# Gamification Bounded Context

**Achievements, badges, progress tracking, rule-based evaluation**

---

## Responsibilities

**Achievement System**:
- Achievement definition management (code, name, description, icon, points)
- Rarity classification (Common, Uncommon, Rare, Epic, Legendary)
- Category grouping (Collection, Session, Social, Exploration, Mastery)
- Threshold-based rule evaluation

**User Progress**:
- Per-user achievement tracking (0-100% progress)
- Automatic unlock when progress reaches 100%
- Recent achievements listing
- Achievement history

**Evaluation Engine**:
- Rule-based achievement evaluation (`AchievementRuleEvaluator`)
- Scheduled evaluation job (`AchievementEvaluationJob`)
- Activity-driven progress updates

---

## Domain Model

| Aggregate | Key Fields | Purpose |
|-----------|-----------|---------|
| **Achievement** | Code, Name, Description, IconUrl, Points, Rarity, Category, Threshold, IsActive | Achievement definition |
| **UserAchievement** | UserId, AchievementId, Progress, UnlockedAt | Per-user tracking |

**Enums**: AchievementRarity (Common/Uncommon/Rare/Epic/Legendary), AchievementCategory (Collection/Session/Social/Exploration/Mastery)

**Domain Methods**:
- `Achievement.Create()` - Factory method with validation
- `Achievement.Activate()` / `Achievement.Deactivate()` - Toggle availability
- `UserAchievement.Create()` - Start tracking for a user
- `UserAchievement.UpdateProgress(int)` - Update progress, auto-unlock at 100%
- `UserAchievement.IsUnlocked` - Check unlock status

---

## CQRS Operations

### Queries
| Query | Handler | Description |
|-------|---------|-------------|
| `GetAchievementsQuery` | `GetAchievementsQueryHandler` | List all achievements (with filters) |
| `GetRecentAchievementsQuery` | `GetRecentAchievementsQueryHandler` | Recent user achievements |

### Validators
- `GetAchievementsQueryValidator` - Input validation for listing
- `GetRecentAchievementsQueryValidator` - Input validation for recent query

---

## Scheduled Jobs

| Job | Description |
|-----|-------------|
| `AchievementEvaluationJob` | Periodic rule evaluation across all users |

---

## Infrastructure

### Repositories
- `IAchievementRepository` → `AchievementRepository`
- `IUserAchievementRepository` → `UserAchievementRepository`

### Services
- `IAchievementRuleEvaluator` → `AchievementRuleEvaluator` (evaluation engine)

### DI Registration
- `GamificationServiceExtensions.AddGamification()`

---

## Dependencies

- **Upstream**: Authentication (user identity), UserLibrary (collection data), SessionTracking (session data), GameManagement (game data)
- **Downstream**: UserNotifications (unlock notifications)
- **Events**: Achievement unlock events may trigger notifications

---

## Related Issues

- Issue #3922: Achievement System and Badge Engine

---

**Last Updated**: 2026-02-18
**Status**: Production
**Code**: `apps/api/src/Api/BoundedContexts/Gamification/`

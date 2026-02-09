# Dashboard Hub - Documentation Index

**Central index for all Dashboard Hub documentation**

---

## 📖 Documentation Structure

### Planning & Specifications
| Document | Description | Status |
|----------|-------------|--------|
| [Epic Details](./epics/epic-dashboard-hub-core.md) | Epic #3901 breakdown with issues and timeline | ✅ Complete |
| [Dashboard Overview Spec](./dashboard-overview-hub.md) | Detailed layout specification and design | ✅ Complete |
| [Implementation Plan](./dashboard-hub-implementation-plan.md) | Technical implementation roadmap | ✅ Complete |
| [Quick Reference](./DASHBOARD-HUB-QUICK-REFERENCE.md) | Developer quick reference guide | ✅ Complete |

### Related Epics
| Epic | Description | Link |
|------|-------------|------|
| **Epic 1** | Dashboard Hub Core (MVP) | [#3901](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3901) |
| **Epic 2** | AI Insights & Recommendations | [#3902](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3902) |
| **Epic 3** | Gamification & Advanced Features | [#3906](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3906) |

---

## 🎯 Epic #3901 Sub-Issues

### Backend (3 issues - 7 SP)
| Issue | Title | SP | Status |
|-------|-------|----|----|
| TBD | Backend: Dashboard Aggregated API Endpoint | 3 | 🔲 To Create |
| TBD | Backend: Activity Timeline Aggregation Service | 2 | 🔲 To Create |
| TBD | Backend: Cache Invalidation Strategy | 2 | 🔲 To Create |

### Frontend (5 issues - 11 SP)
| Issue | Title | SP | Status |
|-------|-------|----|----|
| TBD | Frontend: Dashboard Hub Layout Refactoring + Cleanup Legacy | 3 | 🔲 To Create |
| TBD | Frontend: Enhanced Activity Feed Timeline Component | 3 | 🔲 To Create |
| TBD | Frontend: Library Snapshot Component | 2 | 🔲 To Create |
| TBD | Frontend: Quick Actions Grid Enhancement | 2 | 🔲 To Create |
| TBD | Frontend: Responsive Layout Mobile/Desktop | 3 | 🔲 To Create |

### Testing (1 issue - 3 SP)
| Issue | Title | SP | Status |
|-------|-------|----|----|
| TBD | Testing: Dashboard Hub Integration & E2E Tests | 3 | 🔲 To Create |

**Total**: 8 issues, 21 story points

---

## 📐 Layout Sections

### Phase 1 (MVP - Current Sprint)
| Section | Component | Status | Depends On |
|---------|-----------|--------|------------|
| Hero + Stats | `HeroSection.tsx` | ✅ Existing | - |
| Active Sessions | `ActiveSessionsWidget.tsx` | ✅ Existing (Issue #2617) | - |
| Library Snapshot | `LibrarySnapshot.tsx` | 🔲 To Build | Backend API |
| Activity Feed | `ActivityFeed.tsx` | 🔲 To Build | Activity Service |
| Chat History | `ChatHistory.tsx` | ✅ Existing | - |
| Quick Actions | `QuickActionsGrid.tsx` | ✅ Existing | - |

### Phase 2 (Post-MVP)
| Section | Component | Status | Epic |
|---------|-----------|--------|------|
| AI Insights | `AIInsightsWidget.tsx` | 🔄 Future | Epic 2 |
| Wishlist Highlights | `WishlistHighlights.tsx` | 🔄 Future | Epic 2 |
| Catalog Trending | `CatalogTrending.tsx` | 🔄 Future | Epic 2 |

### Phase 3 (Enhancement)
| Section | Component | Status | Epic |
|---------|-----------|--------|------|
| Achievements | `AchievementsWidget.tsx` | 🔄 Future | Epic 3 |
| Advanced Timeline | `AdvancedTimeline.tsx` | 🔄 Future | Epic 3 |
| Personalized Recs | `PersonalizedRecs.tsx` | 🔄 Future | Epic 3 |

---

## 🔌 API Endpoints

### Dashboard Core (Epic 1)
| Endpoint | Method | Purpose | Cache | Performance |
|----------|--------|---------|-------|-------------|
| `/api/v1/dashboard` | GET | Aggregated dashboard data | 5 min | < 500ms |

### Future Endpoints (Epic 2 & 3)
| Endpoint | Method | Purpose | Epic |
|----------|--------|---------|------|
| `/api/v1/dashboard/insights` | GET | AI recommendations | Epic 2 |
| `/api/v1/dashboard/wishlist` | GET | Wishlist highlights | Epic 2 |
| `/api/v1/dashboard/trending` | GET | Catalog trending | Epic 2 |
| `/api/v1/dashboard/achievements` | GET | User achievements | Epic 3 |

---

## 📊 Data Model

### DashboardData Interface
```typescript
interface DashboardData {
  user: User;
  stats: StatsOverview;
  activeSessions: GameSession[];
  librarySnapshot: LibrarySnapshot;
  recentActivity: Activity[];
  chatHistory: ChatThread[];
}

interface StatsOverview {
  libraryCount: number;
  playedLast30Days: number;
  chatCount: number;
  wishlistCount: number;
  currentStreak: number;
}

interface LibrarySnapshot {
  quota: { used: number; total: number };
  topGames: Game[];
}

type Activity =
  | { type: 'game_added', gameId: string, gameName: string, timestamp: Date }
  | { type: 'session_completed', sessionId: string, gameName: string, timestamp: Date }
  | { type: 'chat_saved', chatId: string, topic: string, timestamp: Date }
  | { type: 'wishlist_added', gameId: string, gameName: string, timestamp: Date };
```

---

## 🧪 Testing Strategy

### Unit Tests (Vitest)
| Component | Coverage Target | Status |
|-----------|----------------|--------|
| `DashboardHub.tsx` | > 85% | 🔲 Pending |
| `LibrarySnapshot.tsx` | > 85% | 🔲 Pending |
| `ActivityFeed.tsx` | > 85% | 🔲 Pending |
| `QuickActionsGrid.tsx` | > 85% | ✅ Existing |

### Integration Tests
| Test Suite | Coverage | Status |
|------------|----------|--------|
| Dashboard Data Flow | API → Components | 🔲 Pending |
| Navigation Links | All sections | 🔲 Pending |
| Empty States | All components | 🔲 Pending |

### E2E Tests (Playwright)
| User Flow | Description | Status |
|-----------|-------------|--------|
| Login → Dashboard | Stats visible, all sections load | 🔲 Pending |
| Active Session Click | Navigate to session page | 🔲 Pending |
| Activity Event Click | Navigate to linked entity | 🔲 Pending |
| Quick Action Click | Navigate to correct page | 🔲 Pending |
| Mobile Responsive | All sections on mobile | 🔲 Pending |

### Visual Regression (Chromatic)
| Viewport | State | Status |
|----------|-------|--------|
| Mobile (375px) | Populated | 🔲 Pending |
| Tablet (768px) | Populated | 🔲 Pending |
| Desktop (1440px) | Populated | 🔲 Pending |
| Desktop (1440px) | Loading | 🔲 Pending |
| Desktop (1440px) | Empty | 🔲 Pending |
| Desktop (1440px) | Error | 🔲 Pending |

---

## 🎨 Design System

### Color Palette
| Category | Color | Hex | Usage |
|----------|-------|-----|-------|
| Collezione | Amber | `#F59E0B` | Library stats, cards |
| Sessioni | Emerald | `#10B981` | Active sessions |
| Chat AI | Blue | `#3B82F6` | Chat history |
| Wishlist | Purple | `#8B5CF6` | Wishlist highlights |
| Insights | Yellow | `#FBBF24` | AI suggestions |
| Achievements | Gold | `#D97706` | Badges, gamification |

### Typography
| Element | Font | Size | Weight |
|---------|------|------|--------|
| Section Title | Inter | 1.5rem | 600 |
| Card Title | Inter | 1.125rem | 500 |
| Body Text | Inter | 1rem | 400 |
| Stats | Inter | 2rem | 700 |

### Spacing
| Breakpoint | Gap | Padding |
|------------|-----|---------|
| Mobile | 1rem | 1rem |
| Tablet | 1.5rem | 1.5rem |
| Desktop | 2rem | 2rem |

---

## 🚀 Timeline

### Sprint N+1 (Weeks 1-2)
- [ ] Backend: Dashboard Aggregated API (#1)
- [ ] Backend: Activity Timeline Service (#2)
- [ ] Frontend: Start Layout Refactoring (#3)

### Sprint N+2 (Weeks 3-4)
- [ ] Frontend: Complete Layout Refactoring (#3)
- [ ] Frontend: Activity Feed Component (#4)
- [ ] Frontend: Library Snapshot Component (#5)
- [ ] Frontend: Quick Actions Enhancement (#6)
- [ ] QA: Start testing

### Sprint N+3 (Weeks 5-6)
- [ ] Frontend: Responsive Layout (#7)
- [ ] QA: Integration & E2E Tests (#8)
- [ ] Deploy to staging with feature flag
- [ ] Stakeholder demo

---

## 📈 Success Metrics

### Development Metrics
| Metric | Target | Current |
|--------|--------|---------|
| Code Coverage | > 85% | TBD |
| API Response (p99) | < 500ms | TBD |
| Bundle Size Increase | < 50KB | TBD |
| Lighthouse Performance | > 90 | TBD |
| Lighthouse Accessibility | > 95 | TBD |

### User Metrics (Post-Launch)
| Metric | Target | Current |
|--------|--------|---------|
| Dashboard Load Time | < 1.5s | TBD |
| CTR (Stats → Pages) | > 40% | TBD |
| Time on Dashboard | > 2 min | TBD |
| Mobile Bounce Rate | < 15% | TBD |

---

## 🗑️ Legacy Code Cleanup

### Files to Remove
```
apps/web/src/components/dashboard/
├── UserDashboard.tsx (1137 lines) - ⚠️ Remove after migration
├── UserDashboardCompact.tsx - ⚠️ Remove after migration
└── (various sub-components) - ⚠️ Remove after migration

apps/web/src/app/(authenticated)/dashboard/
└── dashboard-client.tsx (legacy) - ⚠️ Remove after migration
```

### Validation Checklist
- [ ] `grep -r "UserDashboard" apps/web/src/` returns 0 results
- [ ] `grep -r "dashboard-client-legacy" apps/web/src/` returns 0 results
- [ ] All imports updated to new components
- [ ] All tests passing after cleanup
- [ ] No broken links or references

---

## 🔗 External References

### GitHub Issues
- [Epic #3901 - Dashboard Hub Core (MVP)](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3901)
- [Issue #2617 - Active Sessions Widget](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2617)
- [Issue #2445 - Library Quota Section](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2445)
- [Issue #2612 - Recent Activity Feed](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2612)

### Design References
- GitHub Dashboard Activity Feed
- Notion Workspace Overview
- Linear Project Dashboard

### Tech Stack
- [Next.js 16 App Router](https://nextjs.org/docs)
- [React 19](https://react.dev/)
- [TanStack Query](https://tanstack.com/query)
- [Tailwind CSS 4](https://tailwindcss.com/)
- [shadcn/ui](https://ui.shadcn.com/)
- [Framer Motion](https://www.framer.com/motion/)

---

## 📞 Contact & Ownership

| Role | Owner | Contact |
|------|-------|---------|
| Epic Owner | Frontend Team Lead | TBD |
| Backend Lead | Backend Team Lead | TBD |
| QA Lead | QA Team Lead | TBD |

---

**Last Updated**: 2026-02-09
**Epic**: [#3901](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3901)
**Status**: Planning Phase
**Next Review**: Sprint Planning N+1

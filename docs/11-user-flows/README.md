# MeepleAI User Flows Documentation

> Complete documentation of user interactions, flows, and journeys across all roles.

## Overview

This documentation covers all user flows for MeepleAI, organized by role:

- **[User Role](./user-role/)** - End-user journeys (authentication, library, chat, sessions)
- **[Editor Role](./editor-role/)** - Content management flows (game creation, documents, FAQ)
- **[Admin Role](./admin-role/)** - Administrative flows (approvals, users, configuration)
- **[Diagrams](./diagrams/)** - Sequence diagrams for complex flows

## Role Hierarchy

```
Admin (Full Access)
  └── Editor (Content Management)
        └── User (Consumer Features)
```

| Role | Description | Key Capabilities |
|------|-------------|------------------|
| **User** | End users consuming content | Library management, AI chat, game sessions |
| **Editor** | Content creators | Game catalog CRUD, PDF processing, FAQ/Errata |
| **Admin** | System administrators | User management, approvals, configuration |

## Tier System

User tiers affect quotas and limits (configurable via Admin):

| Tier | Game Library | PDF Upload (Day/Week) | Sessions | Features |
|------|--------------|----------------------|----------|----------|
| **Free** | 5 games | 5/20 | Unlimited* | Basic |
| **Normal** | 20 games | 20/100 | Unlimited* | Standard |
| **Premium** | 50 games | 100/500 | Unlimited* | All Features |

*Session limits not currently enforced but configurable.

## Document Structure

Each flow document contains:

1. **User Stories** - Acceptance criteria format
2. **Screen Flow** - Page-to-page navigation
3. **Sequence Diagram** - Technical interaction flow
4. **API Flow** - Endpoint chain for the action
5. **Implementation Status** - Existing vs gaps

## Quick Links by Flow Type

### Authentication & Onboarding
- [User Registration](./user-role/01-authentication.md#registration)
- [User Login](./user-role/01-authentication.md#login)
- [OAuth Flow](./user-role/01-authentication.md#oauth)
- [2FA Setup](./user-role/01-authentication.md#two-factor-authentication)

### Game Discovery & Library
- [Browse Catalog](./user-role/02-game-discovery.md#browse-catalog)
- [Search Games](./user-role/02-game-discovery.md#search-games)
- [Game Details](./user-role/02-game-discovery.md#game-details)
- [Library Management](./user-role/03-library-management.md)

### AI Chat & Knowledge
- [Start Chat Session](./user-role/04-ai-chat.md#start-chat)
- [Ask Questions](./user-role/04-ai-chat.md#ask-question)
- [Chat History](./user-role/04-ai-chat.md#chat-history)
- [Export Chat](./user-role/04-ai-chat.md#export-chat)

### Game Sessions
- [Create Session](./user-role/05-game-sessions.md#create-session)
- [Track Game State](./user-role/05-game-sessions.md#game-state)
- [Player Mode](./user-role/05-game-sessions.md#player-mode)

### Editor Workflows
- [Create Game](./editor-role/01-game-management.md#create-game)
- [Manage Documents](./editor-role/02-document-management.md)
- [FAQ & Errata](./editor-role/03-content-management.md)
- [Publication Workflow](./editor-role/04-publication-workflow.md)

### Admin Operations
- [Approval Queue](./admin-role/01-approval-workflow.md)
- [User Management](./admin-role/02-user-management.md)
- [System Configuration](./admin-role/03-system-configuration.md)
- [Monitoring](./admin-role/04-monitoring.md)

## Testing & Gap Analysis

### Test Coverage Documentation
- **[complete-test-flows.md](./complete-test-flows.md)** - Comprehensive list of all 105 user flows for E2E testing
- **[Gap Analysis](./gap-analysis.md)** - Implementation status and missing features

### Test Flow Categories

| Category | Existing | Missing | Total | Coverage |
|----------|----------|---------|-------|----------|
| Authentication | 8 | 6 | 14 | 57% |
| Game Discovery | 5 | 3 | 8 | 63% |
| Library Management | 6 | 4 | 10 | 60% |
| AI Chat & RAG | 7 | 5 | 12 | 58% |
| Game Sessions | 6 | 5 | 11 | 55% |
| Editor Flows | 8 | 4 | 12 | 67% |
| Admin Flows | 10 | 8 | 18 | 56% |
| Error & Edge Cases | 5 | 15 | 20 | 25% |
| **TOTAL** | **55** | **50** | **105** | **52%** |

### Priority Flows (P0)

Must be tested before any release:

1. **AUTH-09**: Email Verification
2. **AUTH-10**: Account Lockout
3. **LIB-07**: Session Quota Display
4. **SESS-07**: Session Limits Enforcement
5. **ADM-11**: Session Limits Configuration
6. **ERR-06**: Rate Limiting (429)
7. **ERR-14**: Quota Exceeded Actions

## Related Documentation

- [Product Specification](../01-architecture/overview/product-specification.md)
- [API Reference](../03-api/README.md)
- [Bounded Contexts](../09-bounded-contexts/)
- [E2E Test Guide](../05-testing/e2e/e2-e-test-guide.md)

---

*Last Updated: 2026-01-26*

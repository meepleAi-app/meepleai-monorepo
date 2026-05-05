# Agents Backend Endpoint Audit — 2026-05-03

> Verifies the 14 `@todo BACKEND MISSING` annotations in `apps/web/src/lib/api/clients/agentsClient.ts` from the original audit `2026-04-15`.

## Context

Spec-panel critique 2026-05-03 (with `verification-before-completion` gate) confirmed that the Agents management sub-system is largely backend-missing. Only INVOCATION endpoints exist; CRUD/management endpoints do not.

## Endpoint inventory

### Existing routes (registered backend)

| Path | Method | Backend file:line | Frontend consumer |
|---|---|---|---|
| `/agents` | GET | `AgentsEndpoints.cs` | `useAgents` (RESOLVED by #641 / PR #643) |
| `/agents/qa` | POST | `AiEndpoints.cs:63` | `useAskAgent` (chat) |
| `/agents/explain` | POST | `AiEndpoints.cs:76` | `useAskExplain` |
| `/agents/setup` | POST | `AiEndpoints.cs:84` | `agentsClient.generateSetupGuide` |
| `/agents/feedback` | POST | `AiEndpoints.cs:90` | feedback collection |
| `/agents/player-mode/suggest` | POST | `AiEndpoints.cs:98` | `agentsClient.suggestPlayerMove` |
| `/agents/explain/stream` | POST | `AiEndpoints.cs:112` | streaming explain |
| `/agents/qa/stream` | POST | `AiEndpoints.cs:119` | streaming QA |
| `/agents/arbitro/validate` | POST | `ArbitroAgentEndpoints.cs:44` | arbitro |
| `/agents/arbitro/feedback` | POST | `ArbitroAgentEndpoints.cs:124` | arbitro |
| `/agents/estimate-cost` | POST | `AdminKnowledgeBaseEndpoints.cs:75` | `useEstimateAgentCost` |
| `/games/{id}/agents` | GET | `GameEndpoints.cs:91` | `agentsClient.getUserAgentsForGame` |

### Missing routes (BACKEND MISSING) — followup tracked individually

| # | Path | Method | agentsClient:line | Consumer | Affected feature |
|---|---|---|---|---|---|
| 1 | `/agents` | GET | 65 | `useAgents` | Wave B.2 `/agents` list — RESOLVED by #641 |
| 2 | `/agents/{id}` | GET | 111 | `agentsClient.getById` | (futura agent detail) |
| 3 | `/agents/{id}/status` | GET | 121 | `useAgentStatus` | agent readiness widget |
| 4 | `/agent-typologies` | GET | 157 | `useAgentConfigModal` | Wave B.2 modal create |
| 5 | `/agents/recent` | GET | 183 | `useRecentAgents` | dashboard widget |
| 6 | `/agents/{id}/invoke` | POST | 200 | `agentsClient.invoke` | legacy chat |
| 7 | `/agents` | POST | 220 | `agentsClient.create` | admin create |
| 8 | `/agents/{id}/configure` | PUT | 237 | `agentsClient.configure` | admin config |
| 9 | `/agents/user` | POST | 396 | AgentCreationSheet | Wave B.2 modal save (user-flow) |
| 10 | `/agents/create-with-setup` | POST | 461 | `useCreateAgentFlow` | Wave B.2 modal save (setup-flow) |
| 11 | `/agents/{id}/user` | PUT | 526 | (user agent edit) | edit user agent |
| 12 | `/agents/{id}/configuration` | GET | 640 | `useModels` | model config view |
| 13 | `/agents/{id}/configuration` | PATCH | 654 | `useModels` | model config edit |
| 14 | `/agents/quick-create` | POST | 693 | (quick onboarding) | onboarding |

## Resolution status

| # | Path | Resolution status | Linked issue |
|---|---|---|---|
| 1 | `GET /agents` | RESOLVED | #641 (Wave B.2 hotfix, PR #643) |
| 2-14 | (others) | Followup | Issues filed by this PR |

## Alternative approaches

### Option A — Implement individual endpoints (default recommendation)

Each missing endpoint as separate PR. **Pros**: low blast radius, parallelizable. **Cons**: 13 review cycles.

### Option B — Alias `/admin/agent-definitions` for non-admin

`AdminAgentDefinitionEndpoints.cs:65` already exposes list/get/create/update/delete on `/admin/agent-definitions`. Could be aliased for non-admin with scope filter `(userId == currentUser.Id || isPublic)`. **Pros**: 1 PR resolves 8 endpoints (rows 1, 2, 7, 8, 11, 12, 13, 14). **Cons**: scope filter security review needed; DTO mismatch (`AgentDefinitionDto` ≠ `AgentDto`).

### Option C — Spec dedicata Agents BC user-facing (long-term)

Full design of Agents management user-facing as new BC. **Pros**: clean architecture. **Cons**: 1-2 sprint, blocks Wave B.2 fixes.

**Recommendation**: Option A for routes already consumed in production (rows 1, 4, 9, 10) — those that block Wave B.2. Option B as separate spec for the rest.

## References

- Previous audit: `2026-04-15` (in agentsClient.ts JSDoc comments)
- Memory: `project_wave-b2-agents-backend-missing.md`
- Spec-panel critique: 2026-05-03
- Plan: `docs/superpowers/plans/2026-05-03-wave-b2-agents-hotfix-and-followups.md`
- Wave B.2 hotfix issue: #641
- Wave B.2 hotfix PR: #643 (merged 2026-05-03 squash `a0415c7fc`)

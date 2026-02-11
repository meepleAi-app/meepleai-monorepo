# Quality Validation Documentation

**Test Coverage & Quality Assurance**

---

## Active Quality Initiatives

### RAG Quality Validation (#3192)
**Epic**: [#3174 - AI Agent System](https://github.com/your-org/meepleai-monorepo/issues/3174)
**Status**: 🔴 **BLOCKED by #3231** (RAG endpoints ResponseEnded error)
**Target**: 90%+ accuracy on 20 sample questions

**Documents**:
- [AGT-018 Final Steps](AGT-018-FINAL-STEPS.md) - Implementation roadmap
- [RAG Validation 20Q](rag-validation-20q.md) - Latest test results

**Validation Metrics**:
- **Accuracy**: Answer contains expected keywords (target >90%)
- **Confidence**: Score ≥ min_confidence (target >90% pass rate)
- **Citations**: Valid PDF references present (target >95%)
- **Hallucination**: No forbidden keywords (target 0%)
- **Latency**: Response time <5s (target >95% within SLA)

**Current Status** (2026-01-31):
```
Accuracy:           0% (0/20) - ❌ FAIL (target: ≥90%)
Avg Confidence:     0.00       - ❌ FAIL (target: ≥0.70)
Citation Rate:      0% (0/20)  - ❌ FAIL (target: ≥95%)
Hallucination Rate: 0% (0/20)  - ✅ PASS (target: <3%)
Latency <5s Rate:   100%       - ✅ PASS (target: ≥95%)
```

**Blocker**: #3231 - All RAG endpoints crash with ResponseEnded error. Debug needed in `AskQuestionQueryHandler`.

---

## Test Coverage Targets

**Backend** (xUnit + Testcontainers):
- **Target**: 90%+ coverage
- **Current**: [See FULL_SUITE_TEST_REPORT](../05-testing/FULL_SUITE_TEST_REPORT_2026-01-18.md)
- **Epic**: [#3005 - Test Coverage](https://github.com/your-org/meepleai-monorepo/issues/3005)

**Frontend** (Vitest + Playwright):
- **Target**: 85%+ coverage
- **Current**: [See Frontend Test Plan](../05-testing/frontend/week4-frontend-component-test-plan.md)
- **Epic**: [#3005 - Test Coverage](https://github.com/your-org/meepleai-monorepo/issues/3005)

**E2E** (Playwright):
- **Target**: 50 critical user flows
- **Current**: [See E2E Test Guide](../05-testing/e2e/e2-e-test-guide.md)
- **Issue**: [#3082 - E2E Test Flows](https://github.com/your-org/meepleai-monorepo/issues/3082)

---

## Quality Tools

**Validation Scripts**:
```bash
# RAG Quality Validation (20 questions)
pwsh tools/run-rag-validation-20q.ps1

# Backend Coverage
cd apps/api/src/Api && dotnet test /p:CollectCoverage=true

# Frontend Coverage
cd apps/web && pnpm test:coverage

# E2E Tests
cd apps/web && pnpm test:e2e
```

**Reports Location**:
- RAG Validation: `docs/quality/rag-validation-20q.md`
- Backend Coverage: `apps/api/coverage/`
- Frontend Coverage: `apps/web/coverage/`
- E2E Reports: `apps/web/playwright-report/`

---

## Related Documentation

- [Testing Guide](../05-testing/README.md) - Complete testing strategy
- [Backend Testing](../05-testing/backend/) - xUnit + Testcontainers
- [Frontend Testing](../05-testing/frontend/) - Vitest patterns
- [E2E Testing](../05-testing/e2e/) - Playwright guides
- [Accessibility Guidelines](./accessibility-guidelines.md) - WCAG 2.1 AA compliance

---

**Last Updated**: 2026-01-31
**Maintainer**: QA Team

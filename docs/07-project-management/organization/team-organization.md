# Team Organization

**Consolidated team structure, roles, and organization chart**

---

## Team Structure

MeepleAI is currently a **solo developer project** in alpha phase, designed to scale to a small team (3-5 people) in beta/production.

### Current Team (Alpha - Solo)

**Engineering Lead** (All roles):
- Backend development (ASP.NET, PostgreSQL, Qdrant)
- Frontend development (Next.js, React, Tailwind)
- DevOps/Infrastructure (Docker, CI/CD)
- QA/Testing
- Product management
- Documentation

### Target Team Structure (Beta - 3-5 people)

```
┌─────────────────────────────────────┐
│         Product Owner               │
│  - Roadmap, priorities, stakeholders│
└──────────────┬──────────────────────┘
               │
      ┌────────┴────────┐
      │                 │
┌─────▼──────┐    ┌────▼─────────┐
│  Backend   │    │   Frontend   │
│  Engineer  │    │   Engineer   │
│            │    │              │
│ - API      │    │ - Next.js    │
│ - RAG      │    │ - UI/UX      │
│ - DB       │    │ - Testing    │
└────────────┘    └──────────────┘
      │                 │
      └────────┬────────┘
               │
        ┌──────▼──────┐
        │   QA/DevOps │
        │             │
        │ - Testing   │
        │ - CI/CD     │
        │ - Monitoring│
        └─────────────┘
```

---

## Roles and Responsibilities

### Product Owner
- Define product vision and roadmap
- Prioritize features and bug fixes
- Interface with stakeholders and users
- Make go/no-go decisions for releases

### Backend Engineer
- Develop ASP.NET Core API
- Implement RAG pipeline (Qdrant, OpenAI/Anthropic)
- Maintain database schema and migrations
- Optimize query performance
- Implement background services

### Frontend Engineer
- Develop Next.js/React UI
- Implement responsive design (mobile, tablet, desktop)
- Integrate with backend API
- Implement accessibility standards (WCAG 2.1 AA)
- Optimize performance (Core Web Vitals)

### QA/DevOps Engineer
- Write and maintain automated tests (unit, integration, E2E)
- Manage CI/CD pipelines (GitHub Actions)
- Monitor production health (Seq, Jaeger, Prometheus)
- Manage Docker infrastructure
- Incident response and troubleshooting

---

## Communication & Collaboration

### Daily Workflow (Solo Developer)
- Morning: Review GitHub issues, check production health
- Development: Focus blocks (2-3 hours uninterrupted)
- Testing: Run tests before commits
- Documentation: Update docs as code changes
- Evening: Plan next day's priorities

### Team Workflow (Future)
- **Daily standup** (15 min): What I did, what I'll do, blockers
- **Weekly planning** (1 hour): Review backlog, assign tasks
- **Biweekly retrospective** (1 hour): What went well, what to improve
- **Monthly roadmap review**: Adjust priorities based on user feedback

### Tools
- **GitHub**: Issues, PRs, projects
- **Slack**: Team communication (future)
- **Notion/Linear**: Product roadmap and specs (future)
- **Figma**: Design mockups (future)

---

## Onboarding Guide

New team members should follow this onboarding checklist:

### Week 1: Setup & Familiarization
- [ ] Access granted: GitHub, Docker Hub, production servers
- [ ] Local environment setup: Docker Compose stack running
- [ ] Read: `README.md`, `CLAUDE.md`, `docs/INDEX.md`
- [ ] Review: System architecture, API spec, testing strategy
- [ ] Run: Full test suite (backend + frontend)
- [ ] Deploy: To local environment, verify all services healthy

### Week 2: First Contributions
- [ ] Pick "good first issue" from backlog
- [ ] Submit first PR (code review by lead)
- [ ] Fix any review feedback
- [ ] PR merged → celebrate! 🎉

### Week 3: Domain Knowledge
- [ ] Learn board game domain: Read 2-3 rulebooks
- [ ] Understand RAG pipeline: Trace a query end-to-end
- [ ] Understand hybrid search: Vector + keyword fusion
- [ ] Review quality metrics: Accuracy, hallucination, confidence

### Week 4: Independence
- [ ] Pick medium-complexity issue independently
- [ ] Write tests first (TDD)
- [ ] Submit PR with comprehensive description
- [ ] Participate in code review for other PRs

---

## Escalation Paths

### Technical Issues
1. Check documentation (`docs/`)
2. Search GitHub issues (closed and open)
3. Ask in team Slack (future)
4. Escalate to Engineering Lead

### Product Decisions
1. Review product roadmap
2. Discuss in weekly planning
3. Escalate to Product Owner if unclear

### Incidents
1. Check monitoring (Seq, Jaeger)
2. Follow runbook (`docs/05-operations/runbooks/`)
3. If critical: Page on-call engineer
4. Post-mortem after resolution

---

## Performance Reviews (Future)

**Quarterly reviews** will assess:
- Code quality: Test coverage, adherence to standards
- Velocity: Story points completed
- Collaboration: PR reviews, helping teammates
- Initiative: Proactive improvements, documentation
- Domain knowledge: Understanding board games, RAG, AI

---

## See Also

- **Project Prioritization**: `project-prioritization-2025.md`
- **Execution Plan**: `solo-developer-execution-plan.md`
- **Sprint Overview**: `board-game-ai-sprint-overview.md`
- **Execution Calendar**: `board-game-ai-execution-calendar.md`

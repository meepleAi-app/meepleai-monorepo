# Issue #3811: Visual Strategy Editor - Implementation Plan

## Scope
Visual editor for defining RAG strategies (retrieval, reranking, generation).

## Components

### Backend (2-3 hours)
1. **StrategyDefinition Entity** (Domain/Entities/)
   - Properties: Id, Name, Description, Steps (JSON)
   - Methods: AddStep, RemoveStep, UpdateStep, Validate

2. **CQRS Commands/Queries** (Application/)
   - CreateStrategyCommand, UpdateStrategyCommand, DeleteStrategyCommand
   - GetStrategyByIdQuery, GetAllStrategiesQuery

3. **REST Endpoints** (Routing/)
   - POST /admin/strategies
   - GET /admin/strategies
   - GET /admin/strategies/:id
   - PUT /admin/strategies/:id
   - DELETE /admin/strategies/:id

### Frontend (5-6 hours)
1. **Strategy Form** (components/admin/strategies/)
   - StrategyForm.tsx: Name, description, steps configuration
   - StepConfigurator.tsx: Type selector + parameter inputs
   - StepPreview.tsx: Visual representation of step

2. **Template Library**
   - PredefinedStrategies.ts: FAST, BALANCED, PRECISE templates
   - TemplateSelector.tsx: Choose from library or custom

3. **Pages** (app/admin/strategies/)
   - page.tsx: List strategies
   - create/page.tsx: Create new
   - [id]/edit/page.tsx: Edit existing

4. **API + Schemas** (lib/api/)
   - strategies.api.ts: HTTP client
   - strategies.schemas.ts: Zod validation

### Testing (2-3 hours)
- Backend: 25+ tests (entity, handlers, repo)
- Frontend: 15+ tests (form, preview, templates)
- E2E: Create → Edit → Delete workflow

## Dependencies
- #3808: AgentDefinition (CLOSED)
- #3850: Backend types (IN MERGE)

## Estimate
Total: 9-12 hours (compatible with 4-5 days scope)

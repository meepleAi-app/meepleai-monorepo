# MeepleAI Documentation

This directory contains technical documentation for the MeepleAI monorepo.

## Documentation Index

### Development & Testing

- **[Code Coverage Guide](./code-coverage.md)** - Comprehensive guide for measuring, tracking, and monitoring test coverage
  - How to run coverage locally
  - Coverage tools and formats
  - CI/CD integration options
  - Best practices and troubleshooting

- **[Coverage Baseline Estimate](./coverage-baseline-estimate.md)** - Current test suite analysis and coverage baseline
  - 313 tests across 39 test files
  - Estimated 75-85% line coverage
  - Measurement plan and recommendations
  - Next steps for actual baseline

- **[Codecov Setup Guide](./codecov-setup.md)** - Step-by-step instructions for Codecov integration
  - Account creation and repository setup
  - GitHub secrets configuration
  - Coverage badge and monitoring
  - Troubleshooting common issues

- **[Setup Checklist](./SETUP-CHECKLIST.md)** - Quick checklist to activate Codecov (5-10 minutes)
  - Completed steps overview
  - Remaining steps with time estimates
  - Expected results and success criteria
  - Troubleshooting quick reference

### Architecture & Design

- **[AI-01: Embeddings & Qdrant](./AI-01-embeddings-qdrant.md)** - Vector search and embeddings implementation
  - Text chunking strategy
  - Qdrant collection setup
  - Semantic search pipeline

- **[Tenant Reference Audit](./tenant-reference-audit.md)** - Analysis of tenant/partition references in codebase
  - Legacy multi-tenancy review
  - Migration to shared context

### Project Structure

- **[Epic Structure](./meepleai_epic_structure.md)** - High-level project organization and epic breakdown

## Quick Links

### For Developers

- **Getting Started**: See [CLAUDE.md](../CLAUDE.md) in repository root
- **Running Tests**: `dotnet test` (API) or `pnpm test` (Web)
- **Measuring Coverage**: `pwsh tools/measure-coverage.ps1`

### For Architects

- **System Overview**: [CLAUDE.md - Architecture](../CLAUDE.md#architecture)
- **Service Layer**: [CLAUDE.md - Backend Service Layer](../CLAUDE.md#backend-service-layer)
- **Database Schema**: [CLAUDE.md - Database Layer](../CLAUDE.md#database-layer)

### For QA/Testing

- **Test Strategy**: [CLAUDE.md - Testing](../CLAUDE.md#testing)
- **Coverage Reports**: [code-coverage.md](./code-coverage.md)
- **CI/CD Pipeline**: [CLAUDE.md - CI/CD](../CLAUDE.md#cicd)

## Contributing Documentation

When adding new documentation:

1. Create a descriptive filename using kebab-case (e.g., `feature-name-guide.md`)
2. Add an entry to this README under the appropriate category
3. Include a clear purpose/summary for the document
4. Follow the existing documentation structure and tone
5. Add cross-references to related docs where relevant

## Documentation Standards

- **Format**: GitHub-flavored Markdown
- **Style**: Clear, concise, with practical examples
- **Code Blocks**: Always specify language for syntax highlighting
- **Structure**: Use hierarchical headings (H1 → H6)
- **Links**: Use relative paths for internal docs
- **Examples**: Include both good and bad examples where helpful

## Last Updated

This index was last updated: 2025-10-09

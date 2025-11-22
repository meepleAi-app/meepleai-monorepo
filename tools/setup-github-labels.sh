#!/bin/bash

set -e

echo "🏷️  Creating GitHub Labels..."

# Sprint labels
gh label create sprint-1 --color "0E8A16" --description "Sprint 1: Authentication & Settings" || true
gh label create sprint-2 --color "1D76DB" --description "Sprint 2: Game Library Foundation" || true
gh label create sprint-3 --color "5319E7" --description "Sprint 3: Chat Enhancement" || true
gh label create sprint-4 --color "E99695" --description "Sprint 4: Game Sessions MVP" || true
gh label create sprint-5 --color "F9D0C4" --description "Sprint 5: Agents Foundation" || true

# Component labels
gh label create backend --color "D93F0B" --description "Backend (ASP.NET Core)" || true
gh label create frontend --color "FBCA04" --description "Frontend (Next.js/React)" || true
gh label create database --color "006B75" --description "Database/Migrations" || true
gh label create testing --color "BFD4F2" --description "Testing/QA" || true
gh label create ci-cd --color "C2E0C6" --description "CI/CD/DevOps" || true

# Priority labels
gh label create high-priority --color "D73A4A" --description "High priority" || true
gh label create medium-priority --color "FBCA04" --description "Medium priority" || true
gh label create low-priority --color "0E8A16" --description "Low priority" || true

# Feature labels
gh label create authentication --color "5319E7" --description "Authentication/Authorization" || true
gh label create chat --color "1D76DB" --description "Chat/RAG" || true
gh label create ai --color "E99695" --description "AI/ML/LLM" || true
gh label create pdf --color "F9D0C4" --description "PDF Processing" || true
gh label create performance --color "FEF2C0" --description "Performance Optimization" || true
gh label create security --color "D93F0B" --description "Security" || true

echo "✅ Labels created successfully!"
echo ""

echo "📅 Creating Milestones..."

gh api repos/:owner/:repo/milestones -f title="MVP Sprint 1" -f description="Authentication & Settings (2 weeks)" -f due_on="2025-02-15T00:00:00Z" || true
gh api repos/:owner/:repo/milestones -f title="MVP Sprint 2" -f description="Game Library Foundation (2 weeks)" -f due_on="2025-03-01T00:00:00Z" || true
gh api repos/:owner/:repo/milestones -f title="MVP Sprint 3" -f description="Chat Enhancement (2 weeks)" -f due_on="2025-03-15T00:00:00Z" || true
gh api repos/:owner/:repo/milestones -f title="MVP Sprint 4" -f description="Game Sessions MVP (3 weeks)" -f due_on="2025-04-05T00:00:00Z" || true
gh api repos/:owner/:repo/milestones -f title="MVP Sprint 5" -f description="Agents Foundation (2 weeks)" -f due_on="2025-04-19T00:00:00Z" || true

echo "✅ Milestones created successfully!"
echo ""
echo "🎉 Setup complete! Ready to create issues."

# Microsoft Amplifier - Architecture Overview

## What is Amplifier

> **"Automate complex workflows by describing how you think through them."**

Transform your development expertise into reusable AI tools without writing code.

### Core Concept
```
Expert describes thinking process → Amplifier converts to executable tool
                                  → Tool becomes slash command
                                  → Command reusable forever
```

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────┐
│              Microsoft Amplifier                     │
│  ┌───────────────────────────────────────────────┐ │
│  │        Claude Code (AI Assistant)             │ │
│  └───────────────┬───────────────────────────────┘ │
│                  │                                   │
│  ┌───────────────▼───────────────────────────────┐ │
│  │    Metacognitive Recipe Engine                │ │
│  │  • User describes thinking process            │ │
│  │  • Converts to executable tool                │ │
│  │  • Tool becomes slash command                 │ │
│  └───────────────┬───────────────────────────────┘ │
│                  │                                   │
│  ┌───────────────▼───────────────────────────────┐ │
│  │         Tool Library                          │ │
│  │  /ddd:1-plan  /designer  /your-tool          │ │
│  └───────────────┬───────────────────────────────┘ │
│                  │                                   │
│  ┌───────────────▼───────────────────────────────┐ │
│  │    Project Context (AGENTS.md)                │ │
│  │  Architecture patterns, code conventions      │ │
│  └───────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
```

---

## Key Concepts

### 1. Metacognitive Recipes

**Definition**: Step-by-step description of expert thinking process

**Structure**:
```yaml
recipe:
  - step: 1
    thought: "What does this need to do?"
    action: "Gather requirements"
    validation: "Requirements clear?"

  - step: 2
    thought: "What are dependencies?"
    action: "Identify services/repos"
    validation: "All available in DI?"
```

**Outcome**: Amplifier converts recipe → `/command` → Reusable forever

### 2. Document-Driven Development (DDD)

**Workflow**:
```
Phase 1: PLAN    → docs/issue/FEATURE.md → Human approval
Phase 2: IMPL    → Code from docs → Tests pass
Phase 3: CLEAN   → Update docs → Remove TODOs → PR ready
```

**Benefits**: Docs always synced, design review upfront, no doc drift

### 3. Design Intelligence (7 Agents)

| Agent | Focus | Output |
|-------|-------|--------|
| **art-director** | Visual strategy | Palette, typography |
| **component-designer** | React components | Buttons, cards, modals |
| **layout-architect** | Info architecture | Dashboard layouts |
| **responsive-strategist** | Device adaptation | Breakpoints, fluid layouts |
| **animation-choreographer** | Motion design | Transitions, micro-interactions |
| **voice-strategist** | UX copy | Labels, errors, tone |
| **design-system-architect** | Design systems | Tokens, patterns |

**Framework**: 9 dimensions (purpose, hierarchy, color, typography, spacing, responsive, a11y, motion, voice) × 4 layers (foundational, structural, behavioral, experiential)

---

## MeepleAI Integration Examples

### Feature Development Time Comparison

| Task | Manual | With Amplifier | Saving |
|------|--------|----------------|--------|
| **New Service** | 30min | 2min | 93% |
| **New Endpoint** | 15min | 1min | 93% |
| **UI Component** | 45min | 5min | 89% |
| **Small Feature** | 4h | 1h | 75% |
| **Large Feature** | 2d | 6h | 75% |

### Complete Feature Flow

**Without Amplifier** (7.5h):
```
1. Create docs (30min)
2. Create branch (1min)
3. Design API (45min)
4. Implement service (2h)
5. Create endpoints (1h)
6. Write tests (1.5h)
7. Frontend component (1h)
8. Update docs (30min)
9. Code review prep (30min)
```

**With Amplifier** (1h 7min):
```
/meepleai:feature-start user-preferences        # 2min
/ddd:1-plan → Human review + approval           # 25min
/ddd:2-impl → Generate all + fix errors         # 35min
/ddd:3-clean → Cleanup + docs                   # 5min
```

---

## Amplifier Components

### Project Structure
```
amplifier/
├── .claude/
│   ├── agents/              # 7 design + custom agents
│   ├── commands/            # Slash command definitions
│   └── tools/               # Helper scripts
├── projects/
│   └── meepleai/           # Git submodule
│       ├── AGENTS.md       # Project context
│       └── apps/
├── ai_context/             # Shared knowledge
└── scenarios/              # Scenario tools
```

### Transcript System
```
Before compaction → Export conversation
                  → Save to .data/transcripts/YYYY-MM-DD_HH-MM-SS.json
                  → Searchable: make transcripts-grep pattern="keyword"
```

**Use Cases**: Recover context, find past solutions, learn patterns, share knowledge

---

## Command Library Pattern

### Week-by-Week Build
```bash
Week 1: /meepleai:new-service
Week 2: /meepleai:new-endpoint
Week 3: /meepleai:new-entity
Week 4: /meepleai:ui-component

# After 1 month → Complete library → +50-75% velocity
```

### Creating Custom Commands
```
1. Identify repetitive task
2. Describe thinking process
3. Use /ultrathink-task to convert
4. Test and refine
5. Document in AGENTS.md
6. Share with team (git commit)
```

---

## ROI Calculator

```python
# Setup time
setup_hours = 5

# Monthly savings
services_per_month = 4 * 0.47h         # 1.88h
endpoints_per_month = 8 * 0.23h        # 1.84h
ui_components_per_month = 6 * 0.67h   # 4.02h
features_per_month = 2 * 3.0h          # 6.00h

monthly_savings = 13.74 hours
break_even = 0.36 months (2 weeks)
yearly_savings = 165 hours (1 month of work)
```

---

## Amplifier vs Alternatives

### vs GitHub Copilot
| Aspect | Amplifier | Copilot |
|--------|-----------|---------|
| **Scope** | Workflow automation | Code completion |
| **Reusability** | High (slash commands) | Low (per-use) |
| **Context** | Project-aware (AGENTS.md) | File-aware |
| **Learning** | Compounds over time | Static |

**Use Both**: Copilot for inline, Amplifier for workflows

### vs Agent Lightning
| Aspect | Amplifier | Agent Lightning |
|--------|-----------|-----------------|
| **Purpose** | Dev workflow | AI training |
| **Target** | Developers | AI systems |
| **Output** | Tools/commands | Trained models |
| **Runtime** | Development | Development |
| **Production** | ❌ No | ✅ Yes (artifacts) |

**For MeepleAI**: Amplifier = faster dev | Agent Lightning = better AI

---

## When to Use Amplifier

### ✅ Worth It If
- Full-time on MeepleAI (>20h/week)
- Repetitive tasks (>2x/week)
- Small team needs velocity boost
- Focus on quality + consistency
- Long-term project (>6 months)

### ❌ Skip If
- Occasional contributions (<few hours/month)
- Tasks always different (low repetition)
- Prefer complete manual control
- Immediate time pressure (setup takes hours)

---

## Limitations

### Development-Only Tool
```
❌ NOT a runtime framework
❌ NOT for production integration
✅ Development workflow accelerator
✅ Output (code, docs) goes to production
```

### Requires Claude Code
- Designed specifically for Claude Code
- Doesn't work with Copilot, Cursor, others
- Alternative: Use concepts (metacognitive recipes) with other tools

### Learning Investment
```
Setup: 4-5h
Build library: 10-15h
Break-even: 2-3 weeks
ROI positive: 1 month
ROI significant: 3 months
```

---

## Development Commands

```bash
# Quality
make format lint typecheck test

# Context
make rebuild-context

# Worktrees (parallel experiments)
make worktree name=exp branch=feat/exp
make worktree-list
make worktree-remove name=exp

# Transcripts
make transcripts-list
make transcripts-grep pattern="keyword"
```

---

## Best Practices

### Start Small
```bash
Week 1: Create 1 command (/meepleai:new-service)
Week 2: Add if useful (/meepleai:new-endpoint)
# Don't create 10 commands immediately
```

### Iterate on Commands
```
First attempt: 80% correct → Refine
Second attempt: 95% correct → Continue refining
Final: Perfect for your workflow
```

### Security
```yaml
✅ Safe in AGENTS.md:
  - Architecture patterns
  - Code conventions
  - Public API structure

❌ Never in AGENTS.md:
  - API keys, passwords
  - User data
  - Production URLs (use placeholders)
```

---

**Next Steps**: See `amplifier-developer-workflow-guide.md` for setup and practical examples

**Remember**: Amplifier is for **development workflow**, not production. For optimizing **MeepleAI AI agents**, use **Agent Lightning**.

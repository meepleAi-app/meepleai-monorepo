# Documentation Archive

This directory contains archived documentation for completed epics and old session notes.

---

## Structure

```
archive/
├── epics/           # Completed epic documentation
│   └── 3901-dashboard-hub/  # Dashboard Hub MVP (closed 2026-02-12)
└── sessions/        # Old session notes (> 30 days)
```

---

## Archive Policy

### When to Archive

| Content Type | Archive Trigger | Retention |
|--------------|----------------|-----------|
| **Epic Documentation** | Epic closed + all tasks complete | Permanent |
| **Session Notes** | Age > 30 days | 90 days |
| **Implementation Plans** | Feature shipped to production | Permanent |
| **Analysis Reports** | Superseded by newer analysis | 90 days |

### Archive Process

1. Move files to appropriate `archive/` subdirectory
2. Create `README.md` in archive folder with:
   - Epic/session summary
   - Completion date
   - Key deliverables
   - Links to related ADRs
3. Update links in active documentation
4. Verify no broken references

---

## Accessing Archived Docs

Archived documentation remains searchable via:
- Repository grep/search
- GitHub code search
- IDE workspace search

For frequently accessed archives, consider:
- Creating ADRs summarizing key decisions
- Updating active docs with condensed references

---

## Active Documentation

For current project documentation, see:
- [Main Documentation Index](../INDEX.md)
- [Current Epics](../04-features/admin-dashboard-enterprise/EPICS-AND-ISSUES.md)
- [Roadmap](../ROADMAP-GUIDE.md)

---

**Last Updated**: 2026-02-13

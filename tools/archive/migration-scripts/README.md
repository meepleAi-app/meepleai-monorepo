# Migration Scripts Archive

This directory contains legacy migration scripts that were used during various refactoring phases of the MeepleAI project. These scripts are preserved for historical reference but are **not intended for active use**.

## Archived Scripts

### Frontend Migrations
- **`migrate-api.sh`** - FE-IMP-005: Migrated from legacy API structure to modular SDK (completed)
- **`migrate-components-phase2.sh`** - UI refactoring: Moved components to feature-based modules (completed)
- **`migrate-ui-components-phase3.sh`** - UI refactoring: Reorganized `components/ui/` with categorized structure (completed)
- **`migrate-inline-styles.js`** - Converted inline styles to Tailwind CSS classes (completed)
- **`remove-style-assertions.py`** - Removed `.toHaveStyle()` assertions from tests for Shadcn/UI compatibility (completed)

### Backend Migrations
- **`fix_null_coalescing.py`** - Added null-coalescing validation to C# handler constructors (completed)

## Notes

- These scripts contain hardcoded paths and may not work without modifications
- They were designed for one-time migrations during specific development phases
- For current project structure, see `docs/01-architecture/overview/system-architecture.md`

## Last Updated
2025-11-22

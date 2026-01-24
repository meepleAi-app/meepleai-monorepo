# Epic: User Management - Admin User Control

## Overview
Admin user management table with search, filters, role assignment, bulk operations, and account actions.

## Design Mockup
`docs/design-proposals/meepleai-style/user-management.html`

## Key Features
- User data table (8 columns: checkbox, avatar, name, email, role, status, last active, library size, actions)
- Search by name/email
- Filters: Role (Admin/Editor/User), Status (Active/Inactive/Suspended)
- Bulk selection with floating action bar
- Actions: Change Role, Export CSV, Suspend, Delete
- Per-user menu: View details, Edit role, Reset password, Send email, Delete

## Implementation Issues (7 total)

### Backend (3)
1. GetUsersQuery (search, filters, pagination)
2. UpdateUserRoleCommand
3. SuspendUserCommand / UnsuspendUserCommand

### Frontend (3)
4. User management table with TanStack Table
5. Bulk selection mode + floating action bar
6. User detail modal/page

### Testing (1)
7. User management E2E (search, filter, role change, bulk operations)

## Timeline
2 weeks | Priority: MEDIUM (Admin tool)

## Labels
`epic`, `user-management`, `admin`, `frontend`, `backend`

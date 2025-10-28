# N8N-05 Frontend Specification

**Status**: Backend Complete, Frontend Spec Ready for Implementation
**Backend API**: ✅ Fully Implemented and Tested

---

## Frontend Page: `/admin/workflow-errors`

### Component Structure

**File**: `apps/web/src/pages/admin/workflow-errors.tsx`

**Pattern**: Follow `admin/users.tsx` and `admin/analytics.tsx` patterns

### Features

1. **Error List Table**
   - Columns: Workflow ID, Execution ID, Error Message (truncated), Node Name, Retry Count, Created At
   - Sortable by: Created At (default: descending)
   - Pagination: 20 items per page
   - Row click: Expand to show full error details + stack trace

2. **Filters** (Top of page)
   - Workflow ID dropdown (populated from unique workflow_ids in data)
   - Date range picker (From Date, To Date)
   - Quick filters: Last 24h, Last 7d, Last 30d
   - Clear filters button

3. **Error Detail Modal**
   - Full error message
   - Stack trace (collapsible, monospace font)
   - All metadata (workflow_id, execution_id, node_name, retry_count, created_at)
   - Copy to clipboard button

### API Client Methods

**File**: `apps/web/src/lib/api.ts`

Add to `api` object:

```typescript
workflowErrors: {
  async list(params?: {
    workflowId?: string;
    fromDate?: string;
    toDate?: string;
    page?: number;
    limit?: number;
  }) {
    const query = new URLSearchParams();
    if (params?.workflowId) query.set('workflowId', params.workflowId);
    if (params?.fromDate) query.set('fromDate', params.fromDate);
    if (params?.toDate) query.set('toDate', params.toDate);
    if (params?.page) query.set('page', params.page.toString());
    if (params?.limit) query.set('limit', params.limit.toString());

    const url = `/api/v1/admin/workflows/errors${query.toString() ? `?${query}` : ''}`;
    return api.get<PagedResult<WorkflowErrorDto>>(url);
  },

  async getById(id: string) {
    return api.get<WorkflowErrorDto>(`/api/v1/admin/workflows/errors/${id}`);
  }
}
```

### TypeScript Types

**File**: `apps/web/src/types/index.ts` (or inline in component)

```typescript
export interface WorkflowErrorDto {
  id: string;
  workflowId: string;
  executionId: string;
  errorMessage: string;
  nodeName: string | null;
  retryCount: number;
  stackTrace: string | null;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}
```

### UI Components

**Table**:
- Use `<table>` with Tailwind CSS classes (consistent with admin/users)
- Sticky header
- Hover effects on rows
- Loading skeleton while fetching

**Filters**:
- Workflow ID: `<select>` dropdown
- Date range: Native `<input type="date">` or date picker library
- Quick filters: Button group

**Pagination**:
- Previous/Next buttons
- Page number display
- Jump to page input

**Error Detail Modal**:
- Full-screen overlay
- Close button (X)
- Scrollable content
- Copy button for error message and stack trace

### State Management

```typescript
const [errors, setErrors] = useState<WorkflowErrorDto[]>([]);
const [total, setTotal] = useState(0);
const [page, setPage] = useState(1);
const [loading, setLoading] = useState(false);
const [filters, setFilters] = useState({
  workflowId: '',
  fromDate: '',
  toDate: ''
});
const [selectedError, setSelectedError] = useState<WorkflowErrorDto | null>(null);
```

### Data Fetching

```typescript
useEffect(() => {
  const fetchErrors = async () => {
    setLoading(true);
    try {
      const result = await api.workflowErrors.list({
        ...filters,
        page,
        limit: 20
      });
      setErrors(result.items);
      setTotal(result.total);
    } catch (error) {
      toast.error('Failed to load workflow errors');
    } finally {
      setLoading(false);
    }
  };

  fetchErrors();
}, [page, filters]);
```

### Testing

**Unit Tests** (`__tests__/admin/workflow-errors.test.tsx`):
- Renders error list table
- Displays pagination controls
- Filters by workflow ID
- Filters by date range
- Opens error detail modal
- Copies error to clipboard
- Handles loading state
- Handles error state

**E2E Tests** (`e2e/admin-workflow-errors.spec.ts`):
- Admin can view workflow errors list
- Admin can filter errors by workflow ID
- Admin can filter errors by date range
- Admin can paginate through errors
- Admin can view error details
- Non-admin user cannot access page (403)
- Unauthenticated user redirected to login

### Implementation Priority

1. **High**: Error list table with basic display
2. **High**: Pagination
3. **Medium**: Workflow ID filter
4. **Medium**: Date range filters
5. **Low**: Error detail modal
6. **Low**: Quick filter buttons
7. **Low**: Copy to clipboard

### Success Criteria

- ✅ Admin can view paginated list of workflow errors
- ✅ Admin can filter by workflow ID
- ✅ Admin can filter by date range
- ✅ Errors display with all metadata
- ✅ 90%+ test coverage (unit + E2E)
- ✅ Responsive design (mobile + desktop)
- ✅ Accessible (keyboard navigation, ARIA labels)

---

**Implementation Notes**:
- Follow existing admin page patterns for consistency
- Use Tailwind CSS for styling (existing theme)
- Leverage existing toast notification system
- Reuse pagination component if available
- Follow Next.js 14 best practices

**Estimated Effort**: 4-6 hours (1 developer)

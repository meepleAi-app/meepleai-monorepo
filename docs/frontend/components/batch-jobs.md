# Batch Jobs Components

**Issue**: #3693 - Batch Job System
**Location**: `apps/web/src/components/admin/enterprise/batch-jobs/`
**Status**: Completed

## Overview

React components for managing background batch jobs in the Enterprise Admin section. Provides a complete UI for viewing, creating, monitoring, and managing asynchronous processing tasks.

## Architecture

### Component Hierarchy

```
BatchJobsTab (Container)
├── BatchJobQueueViewer (Table)
│   └── Row actions (Cancel, Retry, Delete)
├── JobDetailModal (Detail View)
│   ├── Job info display
│   ├── Parameters section
│   ├── Results section
│   ├── Logs section (real-time)
│   └── Action buttons
└── CreateJobModal (Creation Form)
    ├── Job type selector
    ├── Parameters JSON editor
    └── Validation
```

## Components

### 1. BatchJobsTab.tsx

**Purpose**: Main container component for batch job management.

**Features**:
- Fetches jobs from API with pagination
- Status filtering (All, Queued, Running, Completed, Failed, Cancelled)
- Auto-refresh every 5s for running/queued jobs
- Manual refresh button
- Create job button
- Pagination controls

**API Integration**:
```typescript
api.admin.getAllBatchJobs({ status?, page, pageSize })
api.admin.getBatchJob(id)
```

**State Management**:
- Jobs list with pagination
- Status filter
- Loading states
- Selected job for detail view
- Create modal visibility

### 2. BatchJobQueueViewer.tsx

**Purpose**: Table component displaying batch jobs with status and actions.

**Columns**:
| Column | Description |
|--------|-------------|
| Type | Job type (ResourceForecast, CostAnalysis, etc.) |
| Status | Badge with color coding (Queued, Running, Completed, Failed, Cancelled) |
| Progress | Progress bar (0-100%) for running jobs |
| Duration | Time taken (formatted: Xs, Xm Ys, Xh Ym) |
| Created | Creation timestamp |
| Actions | Cancel, Retry, Delete buttons |

**Status Badges**:
- **Queued**: Gray badge with clock icon
- **Running**: Blue badge with play icon (animated)
- **Completed**: Green badge with check icon
- **Failed**: Red badge with alert icon
- **Cancelled**: Yellow badge with X icon

**Actions**:
- **Cancel**: Available for Queued/Running jobs
- **Retry**: Available for Failed jobs
- **Delete**: Available for all jobs (with confirmation)

**Interactions**:
- Row click opens JobDetailModal
- Action buttons stop event propagation
- Loading states for async actions

### 3. JobDetailModal.tsx

**Purpose**: Detailed view of a single batch job with comprehensive information.

**Sections**:

#### Job Info
- Status with animated icon
- Duration (formatted)
- Created timestamp
- Started timestamp
- Completed timestamp
- Progress percentage

#### Parameters
- JSON pretty-print of job configuration
- Collapsible pre-formatted code block

#### Results (Completed jobs only)
- JSON pretty-print of job results
- Download results button
- Summary information

#### Error Details (Failed jobs only)
- Error message display
- Red-highlighted error box
- Stack trace (if available)

#### Logs
- Real-time log streaming for running jobs
- Color-coded log levels (INFO, ERROR)
- Timestamp formatting
- Auto-scroll to latest
- Mock implementation (ready for real-time integration)

**Actions**:
- **Retry**: For failed jobs (creates new job)
- **Cancel**: For queued/running jobs
- **Close**: Close modal

**Auto-Refresh**:
- Polls every 3s for running jobs
- Automatically updates status changes
- Stops polling when job completes

### 4. CreateJobModal.tsx

**Purpose**: Form for creating new batch jobs with type selection and parameter configuration.

**Job Types**:
| Type | Label | Description |
|------|-------|-------------|
| ResourceForecast | Resource Forecast | Predict future resource usage and requirements |
| CostAnalysis | Cost Analysis | Analyze cost trends and optimization opportunities |
| DataCleanup | Data Cleanup | Remove old or unused data to optimize storage |
| BggSync | BGG Sync | Synchronize data with BoardGameGeek catalog |
| AgentBenchmark | Agent Benchmark | Run performance benchmarks for AI agents |

**Form Fields**:

#### Job Type Selector
- Dropdown with all available job types
- Shows description on selection
- Required field

#### Parameters Editor
- JSON textarea with syntax validation
- Real-time validation
- Error highlighting
- Optional field (defaults to empty)
- Example parameters shown below

**Validation**:
- Job type required
- Parameters must be valid JSON if provided
- Shows error messages inline
- Disables submit button when invalid

**Example Parameters**:
```json
{
  "daysAhead": 30,
  "includeMetrics": ["tokens", "cost"],
  "format": "detailed"
}
```

## API Client Integration

### Added to adminClient.ts

```typescript
// Get all batch jobs with filters
async getAllBatchJobs(params?: {
  status?: string;
  page?: number;
  pageSize?: number;
}): Promise<BatchJobList>

// Get single batch job
async getBatchJob(id: string): Promise<BatchJobDto>

// Create new batch job
async createBatchJob(request: CreateBatchJobRequest): Promise<{ id: string }>

// Cancel running/queued job
async cancelBatchJob(id: string): Promise<void>

// Retry failed job
async retryBatchJob(id: string): Promise<void>

// Delete job
async deleteBatchJob(id: string): Promise<void>
```

## Schema Definitions

### Added to admin.schemas.ts

```typescript
// Enums
BatchJobTypeSchema = z.enum([
  'ResourceForecast',
  'CostAnalysis',
  'DataCleanup',
  'BggSync',
  'AgentBenchmark',
])

BatchJobStatusSchema = z.enum([
  'Queued',
  'Running',
  'Completed',
  'Failed',
  'Cancelled',
])

// DTOs
BatchJobDtoSchema = z.object({
  id: z.string().uuid(),
  type: BatchJobTypeSchema,
  status: BatchJobStatusSchema,
  parameters: z.record(z.any()).nullable(),
  results: z.record(z.any()).nullable(),
  errorMessage: z.string().nullable(),
  progress: z.number().min(0).max(100),
  createdAt: z.string().datetime(),
  startedAt: z.string().datetime().nullable(),
  completedAt: z.string().datetime().nullable(),
  duration: z.number().nullable(),
})

BatchJobListSchema = z.object({
  jobs: z.array(BatchJobDtoSchema),
  total: z.number().int().nonnegative(),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
})

CreateBatchJobRequestSchema = z.object({
  type: BatchJobTypeSchema,
  parameters: z.record(z.any()).optional(),
})
```

## Usage Example

### Integration in Enterprise Admin

```tsx
import { BatchJobsTab } from '@/components/admin/enterprise/batch-jobs';

export function ResourcesPage() {
  const [activeTab, setActiveTab] = useState('overview');

  return (
    <EnterpriseTabSystem
      tabs={[
        { id: 'overview', label: 'Overview', icon: LayoutDashboard },
        { id: 'tokens', label: 'Token Management', icon: Coins },
        { id: 'batch-jobs', label: 'Batch Jobs', icon: Activity },
      ]}
      activeTab={activeTab}
    >
      {activeTab === 'batch-jobs' && <BatchJobsTab />}
    </EnterpriseTabSystem>
  );
}
```

## UI Components Used

### shadcn/ui
- **Table**: Data display with sorting
- **Badge**: Status indicators with variants
- **Progress**: Progress bar for running jobs
- **Dialog**: Modal overlays
- **Button**: Actions and navigation
- **Select**: Dropdown menus
- **Label**: Form labels
- **Textarea**: JSON parameter input
- **ScrollArea**: Scrollable content areas

### lucide-react Icons
- `PlusIcon`: Create job
- `RefreshCwIcon`: Refresh jobs
- `ClockIcon`: Queued status
- `PlayCircleIcon`: Running status, retry action
- `CheckCircleIcon`: Completed status
- `AlertCircleIcon`: Failed status
- `StopCircleIcon`: Cancel action
- `XCircleIcon`: Cancelled status, close action
- `TrashIcon`: Delete action
- `DownloadIcon`: Download results

## Features

### Real-Time Updates
- Auto-refresh every 5s when running jobs exist
- JobDetailModal polls every 3s for running job
- Manual refresh button with loading state
- Optimistic UI updates

### Error Handling
- Toast notifications for all actions
- Inline validation for JSON parameters
- Error messages displayed in detail modal
- Graceful fallback for API failures

### Accessibility
- Keyboard navigation for tables
- ARIA labels and roles
- Focus management in modals
- Screen reader friendly status indicators

### Responsive Design
- Mobile-friendly table overflow
- Responsive dialog sizes
- Flexible column widths
- Touch-friendly action buttons

## Performance Considerations

### Optimizations
- Conditional auto-refresh (only when needed)
- Debounced parameter validation
- Memoized format functions
- Lazy loading of job details
- Efficient state updates

### Resource Management
- Cleanup intervals on unmount
- Stop polling when modal closes
- Limit log display (max 100 lines)
- Paginated job list

## Testing Recommendations

### Unit Tests
- Status badge rendering for all statuses
- Duration formatting edge cases
- JSON validation in CreateJobModal
- Action button visibility logic

### Integration Tests
- API calls with correct parameters
- Pagination navigation
- Filter application
- Modal open/close behavior

### E2E Tests
- Create job workflow
- Cancel running job
- Retry failed job
- View job details
- Real-time updates

## Future Enhancements

### Planned Features
1. **Real-time Log Streaming**: Replace mock logs with SSE/WebSocket
2. **Bulk Actions**: Select multiple jobs for batch operations
3. **Advanced Filtering**: Date range, duration range, job type
4. **Job Templates**: Save frequently used job configurations
5. **Scheduled Jobs**: Cron-like scheduling interface
6. **Job Dependencies**: Chain jobs with dependencies
7. **Export Results**: CSV/JSON export for completed jobs
8. **Job History**: Historical view with analytics

### Improvements
- Virtual scrolling for large job lists
- Advanced JSON editor with syntax highlighting
- Job comparison view
- Performance metrics dashboard
- Cost tracking per job type

## Related Issues

- **#3693**: Batch Job System (this implementation)
- **#3689**: Layout Base & Navigation System (EnterpriseTabSystem)
- **#3691**: Audit Log System (similar table patterns)
- **#3692**: Token Management (related resource management)

## File Structure

```
apps/web/src/components/admin/enterprise/batch-jobs/
├── index.tsx                    # Exports all components
├── BatchJobsTab.tsx            # Main container (190 lines)
├── BatchJobQueueViewer.tsx     # Table component (240 lines)
├── JobDetailModal.tsx          # Detail view (280 lines)
└── CreateJobModal.tsx          # Creation form (220 lines)

apps/web/src/lib/api/
├── clients/adminClient.ts       # Added 6 batch job methods
└── schemas/admin.schemas.ts     # Added batch job schemas

Total: ~930 lines of component code + API integration
```

## Dependencies

### Required Packages
- `react` (^18.0.0)
- `lucide-react` (^0.263.1)
- `sonner` (^1.0.0) - Toast notifications
- `zod` (^3.22.0) - Schema validation
- `@radix-ui/react-progress` (shadcn/ui)
- `@radix-ui/react-dialog` (shadcn/ui)
- `@radix-ui/react-select` (shadcn/ui)

### Type Safety
- Full TypeScript strict mode
- Zod schema validation
- Type-safe API client methods
- Properly typed component props

---

**Last Updated**: 2026-02-06
**Author**: Frontend Architect
**Status**: ✅ Ready for Integration

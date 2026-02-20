# RAG Debug Console Page

**Location**: `/admin/agents?tab=debug`

## Overview

Live execution monitoring dashboard for RAG pipeline debugging with real-time polling, advanced filtering, and detailed execution analysis.

## Features

### 1. Auto-Refresh Polling
- Configurable intervals: 5s, 10s, 30s
- Live dot animation when active
- Toggle ON/OFF without losing state
- Automatic table refresh with newest executions at top

### 2. Execution Table
- **Columns**:
  - Time (HH:MM:SS format)
  - Status (✓/✗/⚡ icons)
  - Query (truncated to max-width)
  - Strategy badge (POC, SingleModel, MultiModelConsensus, HybridRAG)
  - Latency (color-coded: <100ms green, 100-500ms amber, >500ms red)
  - Confidence (0.00-1.00 with color coding)
  - Agent name

- **Interactions**:
  - Click any row to view detailed execution breakdown
  - Selected row highlighted with amber background
  - Hover effects for better UX

### 3. Execution Detail Panel
- **Appears below table when row clicked**
- **Left Column**:
  - User Query (monospace font)
  - Execution Details:
    - Status, Model, Provider
    - Total Tokens, Cost
    - Cache Hit indicator
    - Error message (if failed)

- **Right Column**:
  - Call Trace Waterfall Chart
  - Visual timeline of execution steps
  - Duration bars with color coding by type
  - Precise timing in milliseconds

### 4. Filters Panel (Right Sidebar)
- **Strategy Filter**: Checkboxes for all 4 strategies
- **Status Filter**: Radio buttons (All, Success, Error, Cached)
- **Agent Dropdown**: Filter by specific agent
- **Confidence Slider**: Min confidence threshold (0.00-1.00)
- **Latency Slider**: Max latency filter (0-5000ms)
- **Date Range**: From/To date pickers
- **Apply Filters Button**: Executes filter query and resets pagination

### 5. Pagination
- Initial load: 20 executions
- "Load More" button shows remaining count
- Preserves existing results when loading more
- Reset on filter change

## Data Fetching

### API Endpoints
- `GET /api/v1/admin/rag-executions` - List executions with filters
- `GET /api/v1/admin/rag-executions/{id}` - Detail with trace

### Query Parameters
```typescript
{
  skip: number;           // Pagination offset
  take: number;           // Page size (20)
  strategy?: string;      // Comma-separated strategies
  status?: string;        // 'ok' | 'error' | 'cache'
  minConfidence?: number; // 0.0-1.0
  maxLatencyMs?: number;  // Max latency filter
  dateFrom?: string;      // ISO date
  dateTo?: string;        // ISO date
}
```

## Component Structure

```
DebugConsolePage (Client Component)
├── Header (Title + Description)
├── Main Grid (70% / 30%)
│   ├── Left Panel - Execution Table
│   │   ├── Live Header (auto-refresh controls)
│   │   ├── Table (thead + tbody)
│   │   ├── Load More Button
│   │   └── Detail Panel (conditional)
│   │       ├── Execution Details
│   │       └── Waterfall Chart
│   └── Right Panel - Filters
│       ├── Strategy Checkboxes
│       ├── Status Radios
│       ├── Agent Dropdown
│       ├── Confidence Slider
│       ├── Latency Slider
│       ├── Date Range Pickers
│       └── Apply Filters Button
```

## State Management

```typescript
// Execution data
const [executions, setExecutions] = useState<RagExecutionListItem[]>([]);
const [selectedExecution, setSelectedExecution] = useState<RagExecutionDetail | null>(null);
const [totalCount, setTotalCount] = useState(0);
const [skip, setSkip] = useState(0);

// Loading states
const [isLoading, setIsLoading] = useState(true);
const [isLoadingMore, setIsLoadingMore] = useState(false);
const [isLoadingDetail, setIsLoadingDetail] = useState(false);

// Auto-refresh
const [autoRefresh, setAutoRefresh] = useState(true);
const [refreshInterval, setRefreshInterval] = useState<RefreshInterval>(10000);

// Filters
const [filters, setFilters] = useState<FilterState>({...});
```

## Shared Components Used

- `StrategyBadge` - Color-coded strategy labels
- `ConfidenceBadge` - Confidence score with color
- `WaterfallChart` - Call trace visualization
- `Skeleton` - Loading placeholders

## Styling

- **Glassmorphism**: `bg-white/70 backdrop-blur-md` cards
- **Amber Accents**: Primary brand color throughout
- **Dark Mode**: Full dark theme support
- **Responsive**: Grid layout adapts to screen size
- **Animations**: Smooth transitions, pulsing live dot

## Performance Considerations

1. **Polling Optimization**:
   - Only polls when `autoRefresh` is true
   - Cleanup interval on unmount
   - Efficient state updates (replace on refresh, append on load more)

2. **Data Loading**:
   - Skeleton loaders for perceived performance
   - Separate loading states for different operations
   - Pagination prevents large initial payload

3. **Re-renders**:
   - `useCallback` for fetch functions
   - Minimal dependencies in effects
   - Conditional rendering for detail panel

## Future Enhancements

- [ ] Real-time WebSocket updates (vs polling)
- [ ] Export executions to CSV/JSON
- [ ] Saved filter presets
- [ ] Execution comparison view
- [ ] Performance metrics aggregation
- [ ] Advanced search (full-text query search)
- [ ] Execution replay/debugging mode

## Related Files

- `/agents/pipeline/page.tsx` - Pipeline Explorer (visualization)
- `/agents/strategy/page.tsx` - Strategy Config (settings)
- `/components/admin/rag/` - Shared RAG components
- `/lib/api/clients/adminClient.ts` - API client types

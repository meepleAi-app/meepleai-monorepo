# BGG Import Queue Service Implementation

**Issue**: #3541
**Status**: Core implementation complete
**Date**: 2026-02-04

## Overview

Implemented a global rate-limited queue service for BGG (BoardGameGeek) game imports with PostgreSQL persistence, background processing, and admin API endpoints.

## Architecture

### Components

1. **Database Entity**: `BggImportQueueEntity`
   - Tracks: BggId, GameName, Status, Position, RetryCount, ErrorMessage, ProcessedAt, CreatedGameId
   - Indexes for: Status+Position lookup, BggId duplicate detection, cleanup queries

2. **Service Layer**: `IBggImportQueueService` / `BggImportQueueService`
   - Queue operations: Enqueue (single/batch), GetStatus, Cancel, Retry
   - Worker methods: GetNextQueued, MarkAsProcessing/Completed/Failed
   - Position management: Auto-recalculation after state changes
   - Auto-cleanup: Remove old completed/failed jobs

3. **Background Worker**: `BggImportQueueBackgroundService`
   - Rate limiting: 1 request/second (BGG API constraint)
   - Retry logic: Max 3 attempts with exponential backoff (2s, 4s, 8s)
   - Integration: Calls `ImportGameFromBggCommand` via MediatR
   - Auto-cleanup: Runs when queue empty

4. **API Endpoints**: `BggImportQueueEndpoints` (Admin-only)
   - `GET /api/v1/admin/bgg-queue/status` - Current queue status
   - `POST /api/v1/admin/bgg-queue/enqueue` - Enqueue single BGG ID
   - `POST /api/v1/admin/bgg-queue/batch` - Enqueue multiple BGG IDs
   - `DELETE /api/v1/admin/bgg-queue/{id}` - Cancel queued import
   - `POST /api/v1/admin/bgg-queue/{id}/retry` - Retry failed import
   - `GET /api/v1/admin/bgg-queue/{bggId}` - Get queue entry by BGG ID

## Configuration

```json
{
  "BggImportQueue": {
    "Enabled": true,
    "InitialDelayMinutes": 1,
    "ProcessingIntervalSeconds": 1.0,
    "MaxRetryAttempts": 3,
    "BaseRetryDelaySeconds": 2,
    "AutoCleanupDays": 7,
    "MaxConcurrentJobs": 1
  }
}
```

## Database Migration

**Migration**: `20260204133152_AddBggImportQueueTable`

**Indexes Created**:
- `IX_BggImportQueue_Status_Position` (filtered: Status = Queued) - Next item lookup
- `IX_BggImportQueue_BggId` - Duplicate detection
- `IX_BggImportQueue_Status_ProcessedAt` (filtered: Completed/Failed) - Cleanup queries
- `IX_BggImportQueue_CreatedGameId` (filtered: NOT NULL) - Game tracking

## Key Features

### Rate Limiting
- **BGG API Constraint**: 1 request/second maximum
- **Implementation**: `ProcessingIntervalSeconds` configurable delay between requests
- **Global Queue**: Singleton background service ensures sequential processing

### Retry Logic
- **Max Attempts**: 3 (configurable)
- **Exponential Backoff**: `BaseRetryDelaySeconds * 2^RetryCount`
  - Attempt 1 fails → wait 2s
  - Attempt 2 fails → wait 4s
  - Attempt 3 fails → marked Failed permanently
- **Auto-Retry**: Failed jobs automatically re-queued with incremented retry count
- **Manual Retry**: Admins can retry Failed jobs via API

### Position Management
- **Auto-Assignment**: New items get `max(Position) + 1`
- **Auto-Recalculation**: Positions renumbered after completion/cancellation
- **Sequential Processing**: Worker always processes lowest position first

### Duplicate Detection
- **BggId Index**: Fast duplicate lookups
- **Batch Enqueue**: Automatically filters out existing BggIds
- **Status Filter**: Only checks Queued/Processing/Completed (allows retry of Failed)

## Integration Points

### CQRS Integration
```csharp
// Background worker calls existing command
var command = new ImportGameFromBggCommand(queueItem.BggId, Guid.Empty);
var createdGameId = await mediator.Send(command, cancellationToken);
```

### Error Handling
- **Service Errors**: Caught by worker, job marked Failed with error message
- **Retry Logic**: Automatic retry until max attempts
- **Permanent Failures**: Status set to Failed, position removed
- **Background Service**: Generic catch prevents service crash

## Files Created/Modified

### Created
- `Infrastructure/Entities/BggImportQueueEntity.cs`
- `Infrastructure/Services/IBggImportQueueService.cs`
- `Infrastructure/Services/BggImportQueueService.cs`
- `Infrastructure/BackgroundServices/BggImportQueueBackgroundService.cs`
- `Routing/BggImportQueueEndpoints.cs`
- `Models/BggImportQueueConfiguration.cs`
- `Infrastructure/Migrations/20260204133152_AddBggImportQueueTable.cs`

### Modified
- `Infrastructure/MeepleAiDbContext.cs` - Added `BggImportQueue` DbSet
- `Program.cs` - Added configuration, endpoint mapping
- `Extensions/InfrastructureServiceExtensions.cs` - Registered service and background worker
- `appsettings.json` - Added `BggImportQueue` configuration section

## Testing Requirements (Pending)

### Unit Tests
- `BggImportQueueServiceTests`
  - Enqueue single/batch operations
  - Duplicate detection
  - Position recalculation
  - Retry logic
  - Cleanup operations

### Integration Tests
- `BggImportQueueBackgroundServiceTests`
  - End-to-end queue processing
  - Rate limiting verification
  - Retry exponential backoff
  - Error handling and recovery

### API Tests
- `BggImportQueueEndpointsTests`
  - Authorization (Admin-only)
  - CRUD operations
  - Status queries
  - Error responses

## Usage Examples

### Enqueue Single Game
```bash
POST /api/v1/admin/bgg-queue/enqueue
{
  "bggId": 174430,
  "gameName": "Gloomhaven"
}
```

### Enqueue Batch
```bash
POST /api/v1/admin/bgg-queue/batch
{
  "bggIds": [174430, 161936, 167791]
}
```

### Check Queue Status
```bash
GET /api/v1/admin/bgg-queue/status
```

Response:
```json
{
  "totalQueued": 3,
  "totalProcessing": 1,
  "items": [
    {
      "id": "...",
      "bggId": 174430,
      "gameName": "Gloomhaven",
      "status": 1,
      "position": 1,
      "retryCount": 0,
      "createdAt": "2026-02-04T13:00:00Z"
    }
  ]
}
```

## Next Steps

1. ~~Apply database migration~~
2. **Write unit tests** (Task #8)
3. **Write integration tests** (Task #8)
4. **Implement SSE streaming** for real-time queue progress (Task #7) - Optional enhancement
5. **Frontend integration** - Display queue status in admin panel

## Performance Considerations

- **PostgreSQL Indexes**: Optimized for frequent position lookups
- **Filtered Indexes**: Reduce index size for status-specific queries
- **Auto-Cleanup**: Prevents table bloat from old completed jobs
- **Scoped Services**: Uses `IServiceScopeFactory` for background worker

## Security

- **Admin-Only Endpoints**: `RequireAuthorization(policy => policy.RequireRole("Admin"))`
- **Input Validation**: BggId must be positive integer
- **Rate Limiting**: Prevents BGG API abuse
- **Error Logging**: Full error messages logged for debugging

## Known Limitations

1. **Single Worker**: Only one background service instance (acceptable for 1 req/sec rate limit)
2. **System User ID**: Background imports use `Guid.Empty` (not tied to specific user)
3. **No Prioritization**: FIFO queue only (could add priority field in future)
4. **No Cancellation of Processing**: Can only cancel Queued jobs

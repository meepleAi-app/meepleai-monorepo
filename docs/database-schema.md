# Database Schema Documentation

> **Last Updated**: 2025-10-09
> **Schema Version**: InitialGlobalSchema (Migration: 20251010153000)
> **Database**: PostgreSQL 16.4
> **ORM**: Entity Framework Core 8.0

## Overview

MeepleAI uses a **single-tenant** database architecture with PostgreSQL as the primary data store. The schema supports user management, game metadata, rule specifications, AI agents, chat sessions, and comprehensive logging/auditing.

## Architecture Model

### Single-Tenant Design

The system operates in **single-tenant mode** without tenant partitioning:
- No `tenants` table required
- All users share a single application instance
- Row-Level Security (RLS) enforced via user roles
- See `docs/tenant-reference-audit.md` for historical context

### Entity Framework Core

**DbContext**: `MeepleAiDbContext` in `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`

**Entities Location**: `apps/api/src/Api/Infrastructure/Entities/`

**Migrations Location**: `apps/api/src/Api/Migrations/`

## Core Tables

### 1. Users (`users`)

User accounts with role-based access control.

**Columns**:
- `Id` (PK, varchar(64)) - Unique user identifier
- `Email` (varchar(256), unique, indexed) - User email address
- `DisplayName` (varchar(128), nullable) - User display name
- `PasswordHash` (text) - Hashed password
- `Role` (varchar(32)) - User role: `Admin`, `Editor`, `User`
- `CreatedAt` (timestamptz) - Account creation timestamp

**Relationships**:
- One-to-Many → `user_sessions` (CASCADE)
- One-to-Many → `pdf_documents` (RESTRICT)
- One-to-Many → `rule_specs` (SET NULL)
- One-to-Many → `n8n_configs` (RESTRICT)

**Indexes**:
- Unique on `Email`

---

### 2. Games (`games`)

Board game metadata.

**Columns**:
- `Id` (PK, varchar(64)) - Game identifier (e.g., `catan`, `chess`)
- `Name` (varchar(128), unique, indexed) - Game display name
- `CreatedAt` (timestamptz) - Game registration timestamp

**Relationships**:
- One-to-Many → `agents` (CASCADE)
- One-to-Many → `rule_specs` (CASCADE)
- One-to-Many → `pdf_documents` (CASCADE)
- One-to-Many → `chats` (CASCADE)
- One-to-Many → `vector_documents` (CASCADE)

**Indexes**:
- Unique on `Name`

---

### 3. Rule Specifications (`rule_specs`, `rule_atoms`)

Formal game rule definitions following RuleSpec v0 schema.

#### `rule_specs`

**Columns**:
- `Id` (PK, uuid) - Rule specification identifier
- `GameId` (FK, varchar(64)) - Associated game
- `Version` (varchar(32)) - Version string (e.g., `v1.0`)
- `CreatedAt` (timestamptz) - Creation timestamp
- `CreatedByUserId` (FK, varchar(64), nullable) - Creator user

**Relationships**:
- Many-to-One → `games` (CASCADE)
- Many-to-One → `users` (SET NULL)
- One-to-Many → `rule_atoms` (CASCADE)

**Indexes**:
- Unique composite on `(GameId, Version)`
- Index on `GameId`
- Index on `CreatedByUserId`

#### `rule_atoms`

Atomic rule components extracted from PDFs.

**Columns**:
- `Id` (PK, uuid) - Atom identifier
- `RuleSpecId` (FK, uuid) - Parent rule spec
- `Key` (varchar(32)) - Rule atom key
- `Text` (text) - Rule text content
- `Section` (varchar(128), nullable) - Section reference
- `PageNumber` (int, nullable) - PDF page number
- `LineNumber` (int, nullable) - PDF line number
- `SortOrder` (int) - Display order

**Relationships**:
- Many-to-One → `rule_specs` (CASCADE)

**Indexes**:
- Index on `RuleSpecId`
- Composite index on `(RuleSpecId, SortOrder)`

---

### 4. Agents (`agents`)

Meeple AI agents specialized per game.

**Columns**:
- `Id` (PK, varchar(64)) - Agent identifier
- `GameId` (FK, varchar(64)) - Associated game
- `Name` (varchar(128)) - Agent display name
- `Kind` (varchar(32)) - Agent type: `explain`, `qa`, `setup`
- `CreatedAt` (timestamptz) - Agent creation timestamp

**Relationships**:
- Many-to-One → `games` (CASCADE)
- One-to-Many → `chats` (CASCADE)
- One-to-Many → `agent_feedback` (CASCADE)

**Indexes**:
- Index on `GameId`
- Composite index on `(GameId, Name)`

---

### 5. Chats & Logs (`chats`, `chat_logs`)

Chat sessions between users and AI agents.

#### `chats`

**Columns**:
- `Id` (PK, uuid) - Chat session identifier
- `GameId` (FK, varchar(64)) - Associated game
- `AgentId` (FK, varchar(64)) - Associated agent
- `UserId` (FK, varchar(64), nullable) - User who started chat (added in migration 20251009115743)
- `StartedAt` (timestamptz) - Session start time
- `LastMessageAt` (timestamptz, nullable) - Last message timestamp (added in migration 20251009115743)

**Relationships**:
- Many-to-One → `games` (CASCADE)
- Many-to-One → `agents` (CASCADE)
- Many-to-One → `users` (CASCADE)
- One-to-Many → `chat_logs` (CASCADE)

**Indexes**:
- Index on `GameId`
- Index on `AgentId`
- Composite index on `(GameId, StartedAt)`

#### `chat_logs`

Individual messages within a chat session.

**Columns**:
- `Id` (PK, uuid) - Log entry identifier
- `ChatId` (FK, uuid) - Parent chat session
- `Level` (varchar(16)) - Log level: `user`, `assistant`, `system`
- `Message` (text) - Message content
- `MetadataJson` (varchar(2048), nullable) - Additional metadata (JSON)
- `CreatedAt` (timestamptz) - Message timestamp

**Relationships**:
- Many-to-One → `chats` (CASCADE)

**Indexes**:
- Index on `ChatId`
- Composite index on `(ChatId, CreatedAt)`

---

## Logging & Auditing Tables

### 6. Audit Logs (`audit_logs`)

Comprehensive audit trail for user actions.

**Columns**:
- `Id` (PK, varchar(64)) - Log entry identifier
- `UserId` (FK, varchar(64), nullable) - User performing action
- `Action` (varchar(64)) - Action type (e.g., `create`, `update`, `delete`)
- `Resource` (varchar(128)) - Resource type (e.g., `game`, `rule_spec`)
- `ResourceId` (varchar(64), nullable) - Resource identifier
- `Result` (varchar(32)) - Outcome: `success`, `failure`
- `Details` (varchar(1024), nullable) - Additional context
- `IpAddress` (varchar(64), nullable) - Client IP address
- `UserAgent` (varchar(256), nullable) - Client user agent
- `CreatedAt` (timestamptz) - Action timestamp

**Indexes**:
- Index on `UserId`
- Index on `CreatedAt`

---

### 7. AI Request Logs (`ai_request_logs`)

Tracks all AI API requests for monitoring and optimization.

**Columns**:
- `Id` (PK, varchar(64)) - Log entry identifier
- `UserId` (FK, varchar(64), nullable) - User making request
- `GameId` (FK, varchar(64), nullable) - Associated game
- `Endpoint` (varchar(32)) - API endpoint called
- `Query` (varchar(2048), nullable) - User query text
- `ResponseSnippet` (varchar(1024), nullable) - Response preview
- `LatencyMs` (int) - Request latency in milliseconds
- `TokenCount` (int, nullable) - LLM token count
- `PromptTokens` (int, nullable) - Prompt tokens (added in migration 20251101000000)
- `CompletionTokens` (int, nullable) - Completion tokens (added in migration 20251101000000)
- `ModelUsed` (varchar(128), nullable) - LLM model identifier (added in migration 20251101000000)
- `Confidence` (double, nullable) - Response confidence score
- `Status` (varchar(32)) - Request status: `success`, `error`
- `ErrorMessage` (varchar(1024), nullable) - Error details
- `IpAddress` (varchar(64), nullable) - Client IP
- `UserAgent` (varchar(256), nullable) - Client user agent
- `CreatedAt` (timestamptz) - Request timestamp

**Indexes**:
- Index on `UserId`
- Index on `GameId`
- Index on `Endpoint`
- Index on `CreatedAt`

---

## Document Management Tables

### 8. PDF Documents (`pdf_documents`)

Uploaded rulebook PDFs with extracted content.

**Columns**:
- `Id` (PK, varchar(64)) - Document identifier
- `GameId` (FK, varchar(64)) - Associated game
- `FileName` (varchar(256)) - Original filename
- `FilePath` (varchar(1024)) - Storage path
- `FileSizeBytes` (bigint) - File size
- `ContentType` (varchar(128)) - MIME type
- `UploadedByUserId` (FK, varchar(64)) - Uploader user
- `UploadedAt` (timestamptz) - Upload timestamp
- `Metadata` (varchar(2048), nullable) - Additional metadata (JSON)
- `ExtractedText` (text, nullable) - Full extracted text
- `ProcessingStatus` (varchar(32)) - Status: `pending`, `completed`, `failed`
- `ProcessedAt` (timestamptz, nullable) - Processing completion time
- `PageCount` (int, nullable) - PDF page count
- `CharacterCount` (int, nullable) - Total characters extracted
- `ProcessingError` (varchar(1024), nullable) - Error details
- `ExtractedTables` (varchar(8192), nullable) - Extracted table data (JSON)
- `ExtractedDiagrams` (varchar(8192), nullable) - Diagram metadata (JSON)
- `AtomicRules` (varchar(8192), nullable) - Atomic rule references (JSON)
- `TableCount` (int, nullable) - Number of tables
- `DiagramCount` (int, nullable) - Number of diagrams
- `AtomicRuleCount` (int, nullable) - Number of atomic rules

**Relationships**:
- Many-to-One → `games` (CASCADE)
- Many-to-One → `users` (RESTRICT)
- One-to-One → `vector_documents` (CASCADE)

**Indexes**:
- Index on `GameId`
- Index on `UploadedByUserId`
- Composite index on `(GameId, UploadedAt)`

---

### 9. Vector Documents (`vector_documents`)

Vector embeddings for semantic search (Qdrant integration).

**Columns**:
- `Id` (PK, varchar(64)) - Vector document identifier
- `GameId` (FK, varchar(64)) - Associated game
- `PdfDocumentId` (FK, varchar(64), unique) - Source PDF document
- `ChunkCount` (int) - Number of text chunks
- `TotalCharacters` (int) - Total characters indexed
- `IndexingStatus` (varchar(32)) - Status: `pending`, `indexed`, `failed`
- `IndexedAt` (timestamptz, nullable) - Indexing completion time
- `IndexingError` (text, nullable) - Error details
- `EmbeddingModel` (varchar(128)) - Model used (e.g., `text-embedding-3-small`)
- `EmbeddingDimensions` (int) - Vector dimensions (e.g., 1536)

**Relationships**:
- Many-to-One → `games` (CASCADE)
- One-to-One → `pdf_documents` (CASCADE)

**Indexes**:
- Index on `GameId`
- Unique index on `PdfDocumentId`

---

## Authentication & Configuration Tables

### 10. User Sessions (`user_sessions`)

Active user sessions for cookie-based authentication.

**Columns**:
- `Id` (PK, varchar(64)) - Session identifier
- `UserId` (FK, varchar(64)) - Associated user
- `TokenHash` (varchar(128), unique, indexed) - Hashed session token
- `UserAgent` (varchar(256), nullable) - Client user agent
- `IpAddress` (varchar(64), nullable) - Client IP
- `CreatedAt` (timestamptz) - Session creation time
- `ExpiresAt` (timestamptz) - Session expiration time
- `LastSeenAt` (timestamptz, nullable) - Last activity timestamp (added in migration 20251015000000)
- `RevokedAt` (timestamptz, nullable) - Manual revocation time (added in migration 20251015000000)

**Relationships**:
- Many-to-One → `users` (CASCADE)

**Indexes**:
- Index on `UserId`
- Unique index on `TokenHash`

---

### 11. n8n Configurations (`n8n_configs`)

n8n workflow engine integration settings.

**Columns**:
- `Id` (PK, varchar(64)) - Config identifier
- `Name` (varchar(128), unique, indexed) - Configuration name
- `BaseUrl` (varchar(512)) - n8n instance URL
- `ApiKeyEncrypted` (varchar(512)) - Encrypted API key
- `WebhookUrl` (varchar(512), nullable) - Webhook endpoint
- `IsActive` (bool) - Active status
- `LastTestedAt` (timestamptz, nullable) - Last connection test
- `LastTestResult` (varchar(512), nullable) - Test result
- `CreatedAt` (timestamptz) - Creation timestamp
- `UpdatedAt` (timestamptz) - Last update timestamp
- `CreatedByUserId` (FK, varchar(64)) - Creator user

**Relationships**:
- Many-to-One → `users` (RESTRICT)

**Indexes**:
- Unique on `Name`
- Index on `CreatedByUserId`

---

### 12. Agent Feedback (`agent_feedback`)

User feedback for AI agent interactions (added in migration 20251020123000).

**Columns**:
- `Id` (PK, varchar(64)) - Feedback identifier
- `ChatId` (FK, uuid) - Associated chat session
- `AgentId` (FK, varchar(64)) - Agent that received feedback
- `UserId` (FK, varchar(64)) - User providing feedback
- `Rating` (int) - Numerical rating (1-5)
- `Comment` (text, nullable) - Optional feedback comment
- `CreatedAt` (timestamptz) - Feedback timestamp

**Relationships**:
- Many-to-One → `chats` (CASCADE)
- Many-to-One → `agents` (CASCADE)
- Many-to-One → `users` (CASCADE)

---

## Applied Migrations

| Migration | Date | Description |
|-----------|------|-------------|
| `20251010153000_InitialGlobalSchema` | 2025-10-10 | Initial database schema creation |
| `20251009115743_AddUserIdAndLastMessageAtToChats` | 2025-10-09 | Add user tracking to chat sessions |
| `20251015000000_AddLastSeenAtAndRevokedAtToUserSessions` | 2025-10-15 | Enhanced session management |
| `20251020123000_AddAgentFeedback` | 2025-10-20 | Agent feedback system |
| `20251101000000_AddLlmUsageMetadataToAiRequestLogs` | 2025-11-01 | Enhanced AI request logging with token metrics |

---

## Entity Relationships Diagram

```
users
  ├── user_sessions (1:N, CASCADE)
  ├── pdf_documents (1:N, RESTRICT)
  ├── rule_specs (1:N, SET NULL)
  ├── n8n_configs (1:N, RESTRICT)
  ├── chats (1:N, CASCADE)
  └── agent_feedback (1:N, CASCADE)

games
  ├── agents (1:N, CASCADE)
  ├── rule_specs (1:N, CASCADE)
  ├── pdf_documents (1:N, CASCADE)
  ├── chats (1:N, CASCADE)
  └── vector_documents (1:N, CASCADE)

agents
  ├── chats (1:N, CASCADE)
  └── agent_feedback (1:N, CASCADE)

chats
  ├── chat_logs (1:N, CASCADE)
  └── agent_feedback (1:N, CASCADE)

rule_specs
  └── rule_atoms (1:N, CASCADE)

pdf_documents
  └── vector_documents (1:1, CASCADE)
```

---

## Row-Level Security (RLS)

The database does NOT use PostgreSQL RLS policies. Access control is enforced at the **application layer** via:

1. **ASP.NET Core Authentication** (`apps/api/src/Api/Program.cs:226-248`)
2. **Role-Based Authorization**:
   - `Admin`: Full access
   - `Editor`: Create/edit games and rule specs
   - `User`: Read-only access
3. **Service Layer Validation** (e.g., `AuditService`, `AuthService`)

---

## Connection String Format

```
Host=localhost;Port=5432;Database=meepleai;Username=meeple;Password=<password>;Maximum Pool Size=100
```

**Environment Variable**: `ConnectionStrings__Postgres`

---

## Backup & Maintenance

### Recommended Practices

1. **Automated Backups**: Daily pg_dump backups with 30-day retention
2. **Index Maintenance**: Monthly REINDEX on high-traffic tables
3. **Vacuum**: Auto-vacuum enabled (default PostgreSQL settings)
4. **Monitoring**: Track table sizes, query performance, connection pool usage

### Critical Tables for Backup Priority

1. `users` - User accounts
2. `games` - Game metadata
3. `rule_specs` + `rule_atoms` - Rule specifications
4. `pdf_documents` - Uploaded documents (file system backup also required)
5. `audit_logs` - Compliance and audit trail

---

## Migration Management

### Creating New Migrations

```bash
cd apps/api/src/Api
dotnet ef migrations add <MigrationName>
```

### Applying Migrations

Migrations are **auto-applied on startup** unless `CI=true` (test mode).

**Manual Application**:
```bash
dotnet ef database update
```

### Rollback

```bash
dotnet ef database update <PreviousMigrationName>
```

---

## Related Documentation

- **Entity Classes**: `apps/api/src/Api/Infrastructure/Entities/*.cs`
- **DbContext**: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`
- **Migrations**: `apps/api/src/Api/Migrations/`
- **RuleSpec Schema**: `schemas/rulespec.v0.schema.json`
- **Tenant Audit**: `docs/tenant-reference-audit.md`
- **CLAUDE.md**: General development guide

---

## Questions & Support

For schema changes or database issues, see:
- Issue tracker: `area/db` label
- Database entity definitions in EF Core models
- Migration history in `__EFMigrationsHistory` table

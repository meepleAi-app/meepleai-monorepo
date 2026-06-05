/**
 * Issue #1903 M8.1 — TypeScript shapes for the admin catalog seed pipeline.
 *
 * Mirror the BE DTOs documented in
 * `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/DTOs/CatalogSeedDraftDto.cs`
 * and the request/result records in `AdminCatalogSeedEndpoints.cs`.
 *
 * Field names follow the API's camelCase serialisation (`PropertyNamingPolicy.CamelCase`).
 */

export type CatalogSeedStatus = 'Pending' | 'Fetched' | 'FetchFailed' | 'Approved' | 'Rejected';

/**
 * Read model for a single catalog seed draft row.
 * 1:1 with `CatalogSeedDraftDto` (BE DTO).
 */
export interface CatalogSeedDraftDto {
  id: string;
  bggId: number | null;
  wikidataQid: string | null;
  searchTermInput: string | null;
  status: CatalogSeedStatus;
  errorMessage: string | null;
  resultingSharedGameId: string | null;
  createdByUserId: string;
  createdAt: string;
  fetchedAt: string | null;
  approvedAt: string | null;
  approvedByUserId: string | null;
}

/** Request body for `POST /api/v1/admin/catalog/seeds`. */
export interface EnqueueCatalogSeedRequest {
  bggId?: number | null;
  wikidataQid?: string | null;
  searchTermInput?: string | null;
}

/** Result of `POST /api/v1/admin/catalog/seeds`. */
export interface EnqueueCatalogSeedResult {
  id: string;
  bggId: number | null;
  status: CatalogSeedStatus;
  isDuplicate: boolean;
}

/** Request body for `POST /api/v1/admin/catalog/seeds/bulk`. */
export interface BulkEnqueueRequest {
  bggIds: number[];
}

/** Result of `POST /api/v1/admin/catalog/seeds/bulk`. */
export interface BulkEnqueueResult {
  total: number;
  enqueued: number;
  duplicates: number;
  newDraftIds: string[];
}

/** Result of `GET /api/v1/admin/catalog/seeds`. */
export interface ListCatalogSeedsResult {
  total: number;
  skip: number;
  take: number;
  items: CatalogSeedDraftDto[];
}

/** Result of `POST /api/v1/admin/catalog/seeds/{id}/approve`. */
export interface ApproveCatalogSeedResult {
  draftId: string;
  resultingSharedGameId: string;
}

/**
 * Per-field provenance metadata used by Wikidata search response.
 * Mirrors `FieldProvenance` value object on BE.
 */
export interface FieldProvenance {
  provider: string;
  sourceUrl: string;
  sourceField: string;
  fetchedAt: string;
  value: unknown;
}

/** Result of `POST /api/v1/admin/catalog/seeds/wikidata-search`. */
export interface WikidataSearchResult {
  fields: Record<string, FieldProvenance>;
  errorMessage: string | null;
}

/**
 * SSE event envelope emitted by `GET /api/v1/admin/catalog/seeds/stream`.
 * The BE wraps each event with `event: <eventType>` + JSON `data:` payload;
 * we capture both pieces here for the LogStream renderer.
 */
export interface SeedStreamEvent {
  eventType: string;
  occurredAt: string;
  [k: string]: unknown;
}

/**
 * Discriminated payloads documented in §6 of the design spec. Each event includes
 * `eventType` + `occurredAt`; the remaining fields are folded into the JSON
 * envelope so this interface only enumerates the well-known ones for downstream
 * consumers who want narrow typing.
 */
export type SeedBatchStartedEvent = SeedStreamEvent & {
  eventType: 'batch.started';
  batchId: string;
  draftIds: string[];
};

export type SeedFetchedEvent = SeedStreamEvent & {
  eventType: 'seed.fetched';
  draftId: string;
  bggId: number | null;
  providerUsed: string;
  durationMs: number;
};

export type SeedFailedEvent = SeedStreamEvent & {
  eventType: 'seed.failed';
  draftId: string;
  error: string;
  retryAttempt: number;
};

export type SeedBatchCompletedEvent = SeedStreamEvent & {
  eventType: 'batch.completed';
  batchId: string;
  succeeded: number;
  failed: number;
  totalDurationMs: number;
};

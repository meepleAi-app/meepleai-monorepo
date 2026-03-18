export type TunnelState = 'Closed' | 'Opening' | 'Open' | 'Error';
export type SyncDirection = 'LocalToStaging' | 'StagingToLocal';

export interface TunnelStatusResult {
  status: TunnelState;
  uptimeSeconds: number;
  message: string | null;
}

export interface MigrationInfo {
  migrationId: string;
  productVersion: string;
  appliedOn: string | null;
}

export interface SchemaDiffResult {
  common: MigrationInfo[];
  localOnly: MigrationInfo[];
  stagingOnly: MigrationInfo[];
}

export interface TableInfo {
  tableName: string;
  schemaName: string;
  localRowCount: number;
  stagingRowCount: number;
  boundedContext: string | null;
}

export interface ColumnDiff {
  column: string;
  localValue: string | null;
  stagingValue: string | null;
}

export interface RowDiff {
  primaryKey: Record<string, string | null>;
  differences: ColumnDiff[];
}

export interface DataDiffResult {
  tableName: string;
  localRowCount: number;
  stagingRowCount: number;
  identicalCount: number;
  modified: RowDiff[];
  localOnly: Record<string, string | null>[];
  stagingOnly: Record<string, string | null>[];
}

export interface SyncResult {
  success: boolean;
  inserted: number;
  updated: number;
  operationId: string;
  errorMessage: string | null;
}

export interface SyncHistoryEntry {
  action: string;
  resource: string;
  resourceId: string | null;
  result: string;
  details: string | null;
  createdAt: string;
  userId: string | null;
}

export interface ApplyMigrationsRequest {
  direction: SyncDirection;
  confirmation: string;
}

export interface SyncTableDataRequest {
  direction: SyncDirection;
  confirmation: string;
}

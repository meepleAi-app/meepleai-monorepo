/**
 * Admin Bulk User Operations API
 * Issue #3947: Bulk User Actions Modal
 *
 * API endpoints for multi-user operations
 */

export interface BulkPasswordResetRequest {
  userIds: string[];
  sendEmail?: boolean;
  message?: string;
}

export interface BulkRoleChangeRequest {
  userIds: string[];
  newRole: string;
}

export interface BulkOperationResult {
  successCount: number;
  failureCount: number;
  errors: string[];
}

/**
 * Reset passwords for multiple users
 */
export async function bulkPasswordReset(request: BulkPasswordResetRequest): Promise<BulkOperationResult> {
  const response = await fetch('/api/v1/admin/users/bulk/password-reset', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new Error('Failed to reset passwords');
  }

  return response.json();
}

/**
 * Change roles for multiple users
 */
export async function bulkRoleChange(request: BulkRoleChangeRequest): Promise<BulkOperationResult> {
  const response = await fetch('/api/v1/admin/users/bulk/role-change', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new Error('Failed to change roles');
  }

  return response.json();
}

/**
 * Import users from CSV
 */
export async function bulkImportUsers(csvContent: string): Promise<BulkOperationResult> {
  const response = await fetch('/api/v1/admin/users/bulk/import', {
    method: 'POST',
    headers: { 'Content-Type': 'text/csv' },
    body: csvContent,
  });

  if (!response.ok) {
    throw new Error('Failed to import users');
  }

  return response.json();
}

/**
 * Export users to CSV
 */
export async function bulkExportUsers(userIds?: string[]): Promise<Blob> {
  const params = userIds ? `?userIds=${userIds.join(',')}` : '';
  const response = await fetch(`/api/v1/admin/users/bulk/export${params}`, {
    method: 'GET',
  });

  if (!response.ok) {
    throw new Error('Failed to export users');
  }

  return response.blob();
}

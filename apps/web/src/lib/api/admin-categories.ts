/**
 * Admin Categories API client.
 * Issue #1440 — Phase 2 BE wiring for the SharedGames categories CRUD surface.
 *
 * Endpoints exposed by `AdminCategoriesEndpoints.cs`:
 *   GET    /api/v1/admin/categories
 *   POST   /api/v1/admin/categories
 *   PUT    /api/v1/admin/categories/{id}
 *   DELETE /api/v1/admin/categories/{id}
 *
 * All endpoints require an admin session (RequireAdminSession server-side).
 * Cookies travel via `credentials: 'include'`.
 */

export interface CategoryDto {
  id: string;
  name: string;
  slug: string;
  emoji: string | null;
  color: string | null;
  gameCount: number | null;
}

export interface CreateCategoryRequest {
  name: string;
  slug: string;
  emoji?: string | null;
  color?: string | null;
}

export type UpdateCategoryRequest = CreateCategoryRequest;

/**
 * Error thrown by the admin-categories client. Carries HTTP status so the
 * UI can branch on 409 (conflict — name/slug already taken, or delete
 * forbidden because gameCount > 0) without parsing the body twice.
 */
export class AdminCategoriesApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly serverMessage: string
  ) {
    super(serverMessage || `HTTP ${status}`);
    this.name = 'AdminCategoriesApiError';
  }
}

async function parseResponse(response: Response, fallback: string): Promise<never> {
  let serverMessage = fallback;
  try {
    const body = (await response.json()) as { message?: string; title?: string };
    if (typeof body.message === 'string' && body.message.length > 0) {
      serverMessage = body.message;
    } else if (typeof body.title === 'string' && body.title.length > 0) {
      serverMessage = body.title;
    }
  } catch {
    // Non-JSON body — fall through with the fallback message.
  }
  throw new AdminCategoriesApiError(response.status, serverMessage);
}

export async function listAdminCategories(): Promise<CategoryDto[]> {
  const response = await fetch('/api/v1/admin/categories', {
    credentials: 'include',
  });
  if (!response.ok) {
    await parseResponse(response, 'Failed to list categories');
  }
  return (await response.json()) as CategoryDto[];
}

export async function createAdminCategory(payload: CreateCategoryRequest): Promise<CategoryDto> {
  const response = await fetch('/api/v1/admin/categories', {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });
  if (!response.ok) {
    await parseResponse(response, 'Failed to create category');
  }
  return (await response.json()) as CategoryDto;
}

export async function updateAdminCategory(
  id: string,
  payload: UpdateCategoryRequest
): Promise<CategoryDto> {
  const response = await fetch(`/api/v1/admin/categories/${encodeURIComponent(id)}`, {
    method: 'PUT',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });
  if (!response.ok) {
    await parseResponse(response, 'Failed to update category');
  }
  return (await response.json()) as CategoryDto;
}

export async function deleteAdminCategory(id: string): Promise<void> {
  const response = await fetch(`/api/v1/admin/categories/${encodeURIComponent(id)}`, {
    method: 'DELETE',
    credentials: 'include',
  });
  if (!response.ok) {
    await parseResponse(response, 'Failed to delete category');
  }
}

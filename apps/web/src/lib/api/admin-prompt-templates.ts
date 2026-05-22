/**
 * Admin Prompt Templates API client.
 * Issue #1442 Phase 1b — wire `system-prompts-section.tsx` to existing BE.
 *
 * BE source:
 *   apps/api/src/Api/Routing/PromptManagementEndpoints.cs
 *   apps/api/src/Api/Models/PromptManagementDto.cs (PromptTemplateDto)
 *
 * Endpoint used in this client (read-only for Phase 1b):
 *   GET /api/v1/admin/prompts — paginated list (returns PagedResult<PromptTemplateDto>)
 *
 * Out of scope for Phase 1b:
 *   POST/GET-by-id/versions/activate/evaluate — mockup only shows the card grid,
 *   the View/Edit actions remain stub buttons until a dedicated editor surface
 *   ships in a follow-up epic.
 */

export interface PromptTemplateDto {
  id: string;
  name: string;
  description: string | null;
  category: string | null;
  createdByUserId: string;
  createdByEmail: string | null;
  createdAt: string;
  versionCount: number;
  activeVersionNumber: number | null;
}

export interface PagedPromptTemplatesDto {
  items: PromptTemplateDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export class AdminPromptTemplatesApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly serverMessage: string
  ) {
    super(serverMessage || `HTTP ${status}`);
    this.name = 'AdminPromptTemplatesApiError';
  }
}

async function rejectAs(response: Response, fallback: string): Promise<never> {
  let message = fallback;
  try {
    const body = (await response.json()) as { message?: string; title?: string };
    if (typeof body.message === 'string' && body.message.length > 0) {
      message = body.message;
    } else if (typeof body.title === 'string' && body.title.length > 0) {
      message = body.title;
    }
  } catch {
    // Non-JSON body — keep fallback.
  }
  throw new AdminPromptTemplatesApiError(response.status, message);
}

/**
 * Fetch the prompt templates. The endpoint returns a generic `PagedResult`
 * wrapper (`{ items, totalCount, page, pageSize }`). The card grid renders
 * a small, bounded set so we request `pageSize=200` and ignore pagination
 * controls for Phase 1b.
 */
export async function listAdminPromptTemplates(): Promise<PromptTemplateDto[]> {
  const response = await fetch('/api/v1/admin/prompts?page=1&limit=200', {
    credentials: 'include',
  });
  if (!response.ok) {
    await rejectAs(response, 'Failed to list prompt templates');
  }
  const payload = (await response.json()) as PagedPromptTemplatesDto;
  return payload.items;
}

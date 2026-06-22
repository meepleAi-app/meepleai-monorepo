/**
 * Admin AI Models API client.
 * Issue #1442 Phase 1a — wire `models-table.tsx` to existing BE endpoints.
 *
 * BE source:
 *   apps/api/src/Api/Routing/AiModelAdminEndpoints.cs
 *   apps/api/src/Api/BoundedContexts/SystemConfiguration/Application/DTOs/AiModelDto.cs
 *
 * Endpoints used in this client:
 *   GET   /api/v1/admin/ai-models           — paginated list
 *   PATCH /api/v1/admin/ai-models/{id}/toggle — flip IsActive
 *
 * Out of scope for this client (Phase 2):
 *   POST/PUT/DELETE/PATCH-priority/tier-routing — the mockup doesn't surface them.
 */

export interface AiModelPricing {
  inputPricePerMillion: number;
  outputPricePerMillion: number;
  currency: string;
}

export interface AiModelSettings {
  maxTokens: number;
  temperature: number;
  pricing: AiModelPricing;
}

export interface AiModelUsage {
  totalRequests: number;
  totalInputTokens: number;
  totalOutputTokens: number;
  totalTokensUsed: number;
  totalCostUsd: number;
  lastUsedAt: string | null;
}

export interface AiModelDto {
  id: string;
  modelId: string;
  displayName: string;
  provider: string;
  priority: number;
  isActive: boolean;
  isPrimary: boolean;
  createdAt: string;
  updatedAt: string | null;
  settings: AiModelSettings;
  usage: AiModelUsage;
}

export interface AiModelListDto {
  models: AiModelDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export class AdminAiModelsApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly serverMessage: string
  ) {
    super(serverMessage || `HTTP ${status}`);
    this.name = 'AdminAiModelsApiError';
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
  throw new AdminAiModelsApiError(response.status, message);
}

/**
 * Fetch the list of AI models. The mockup shows up to ~5 rows at a time;
 * we request a large page size to render the full set without pagination
 * controls (out of scope for Phase 1a).
 */
export async function listAdminAiModels(): Promise<AiModelDto[]> {
  const response = await fetch('/api/v1/admin/ai-models?page=1&pageSize=200', {
    credentials: 'include',
  });
  if (!response.ok) {
    await rejectAs(response, 'Failed to list AI models');
  }
  const list = (await response.json()) as AiModelListDto;
  return list.models;
}

/**
 * Toggle an AI model's `isActive` flag. The BE rejects toggling a primary
 * model off with 409 Conflict (preserved as-is in the error surface).
 */
export async function toggleAdminAiModel(id: string): Promise<AiModelDto> {
  const response = await fetch(`/api/v1/admin/ai-models/${encodeURIComponent(id)}/toggle`, {
    method: 'PATCH',
    credentials: 'include',
  });
  if (!response.ok) {
    await rejectAs(response, 'Failed to toggle AI model');
  }
  return (await response.json()) as AiModelDto;
}

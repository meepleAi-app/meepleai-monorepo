/**
 * Toolbox API Client
 *
 * Endpoints for the GameToolbox bounded context.
 * Epic #412 — Game Toolbox.
 */

import {
  ToolboxSchema,
  CardDrawResultSchema,
  ToolboxToolSchema,
  PhaseSchema,
  SharedContextSchema,
  ToolboxTemplateSchema,
  AvailableToolSchema,
  type ToolboxDto,
  type ToolboxToolDto,
  type PhaseDto,
  type SharedContext,
  type CardDrawResultDto,
  type ToolboxTemplateDto,
  type AvailableToolDto,
  type CreateToolboxRequest,
  type AddToolRequest,
  type UpdateSharedContextRequest,
  type AddPhaseRequest,
  type CreateCardDeckRequest,
} from '../schemas/toolbox.schemas';

import type { HttpClient } from '../core/httpClient';

const BASE = '/api/v1/toolboxes';
const TEMPLATES_BASE = '/api/v1/toolbox-templates';

export interface ToolboxClient {
  // Queries
  getToolbox(id: string): Promise<ToolboxDto | null>;
  getToolboxByGame(gameId: string): Promise<ToolboxDto | null>;
  getAvailableTools(): Promise<AvailableToolDto[]>;
  getTemplates(gameId?: string): Promise<ToolboxTemplateDto[]>;

  // Toolbox CRUD
  createToolbox(req: CreateToolboxRequest): Promise<ToolboxDto>;
  updateMode(id: string, mode: 'Freeform' | 'Phased'): Promise<ToolboxDto>;

  // Tool management
  addTool(toolboxId: string, req: AddToolRequest): Promise<ToolboxToolDto>;
  removeTool(toolboxId: string, toolId: string): Promise<void>;
  reorderTools(toolboxId: string, orderedIds: string[]): Promise<void>;

  // Shared context
  updateSharedContext(toolboxId: string, req: UpdateSharedContextRequest): Promise<SharedContext>;

  // Card deck
  createCardDeck(toolboxId: string, req: CreateCardDeckRequest): Promise<ToolboxToolDto>;
  shuffleDeck(toolboxId: string, deckId: string): Promise<void>;
  drawCards(toolboxId: string, deckId: string, count: number): Promise<CardDrawResultDto>;
  resetDeck(toolboxId: string, deckId: string): Promise<void>;

  // Phases
  addPhase(toolboxId: string, req: AddPhaseRequest): Promise<PhaseDto>;
  removePhase(toolboxId: string, phaseId: string): Promise<void>;
  reorderPhases(toolboxId: string, orderedIds: string[]): Promise<void>;
  advancePhase(toolboxId: string): Promise<PhaseDto>;

  // Templates
  applyTemplate(templateId: string, gameId?: string): Promise<ToolboxDto>;
}

export function createToolboxClient({ httpClient }: { httpClient: HttpClient }): ToolboxClient {
  return {
    // Queries
    async getToolbox(id) {
      const res = await httpClient.get<ToolboxDto>(`${BASE}/${id}`);
      return res ? ToolboxSchema.parse(res) : null;
    },

    async getToolboxByGame(gameId) {
      const res = await httpClient.get<ToolboxDto>(`${BASE}/by-game/${gameId}`);
      return res ? ToolboxSchema.parse(res) : null;
    },

    async getAvailableTools() {
      const res = await httpClient.get<AvailableToolDto[]>(`${BASE}/available-tools`);
      return (res ?? []).map(t => AvailableToolSchema.parse(t));
    },

    async getTemplates(gameId) {
      const params = gameId ? `?gameId=${encodeURIComponent(gameId)}` : '';
      const res = await httpClient.get<ToolboxTemplateDto[]>(`${TEMPLATES_BASE}/${params}`);
      return (res ?? []).map(t => ToolboxTemplateSchema.parse(t));
    },

    // Toolbox CRUD
    async createToolbox(req) {
      const res = await httpClient.post<ToolboxDto>(BASE, req);
      return ToolboxSchema.parse(res);
    },

    async updateMode(id, mode) {
      const res = await httpClient.put<ToolboxDto>(`${BASE}/${id}/mode?mode=${mode}`, {});
      return ToolboxSchema.parse(res);
    },

    // Tool management
    async addTool(toolboxId, req) {
      const res = await httpClient.post<ToolboxToolDto>(`${BASE}/${toolboxId}/tools`, req);
      return ToolboxToolSchema.parse(res);
    },

    async removeTool(toolboxId, toolId) {
      await httpClient.delete(`${BASE}/${toolboxId}/tools/${toolId}`);
    },

    async reorderTools(toolboxId, orderedIds) {
      await httpClient.put(`${BASE}/${toolboxId}/tools/reorder`, { orderedIds });
    },

    // Shared context
    async updateSharedContext(toolboxId, req) {
      const res = await httpClient.put<SharedContext>(`${BASE}/${toolboxId}/shared-context`, req);
      return SharedContextSchema.parse(res);
    },

    // Card deck
    async createCardDeck(toolboxId, req) {
      const res = await httpClient.post<ToolboxToolDto>(`${BASE}/${toolboxId}/card-decks`, req);
      return ToolboxToolSchema.parse(res);
    },

    async shuffleDeck(toolboxId, deckId) {
      await httpClient.post(`${BASE}/${toolboxId}/card-decks/${deckId}/shuffle`, {});
    },

    async drawCards(toolboxId, deckId, count) {
      const res = await httpClient.post<CardDrawResultDto>(
        `${BASE}/${toolboxId}/card-decks/${deckId}/draw?count=${count}`,
        {}
      );
      return CardDrawResultSchema.parse(res);
    },

    async resetDeck(toolboxId, deckId) {
      await httpClient.post(`${BASE}/${toolboxId}/card-decks/${deckId}/reset`, {});
    },

    // Phases
    async addPhase(toolboxId, req) {
      const res = await httpClient.post<PhaseDto>(`${BASE}/${toolboxId}/phases`, req);
      return PhaseSchema.parse(res);
    },

    async removePhase(toolboxId, phaseId) {
      await httpClient.delete(`${BASE}/${toolboxId}/phases/${phaseId}`);
    },

    async reorderPhases(toolboxId, orderedIds) {
      await httpClient.put(`${BASE}/${toolboxId}/phases/reorder`, { orderedIds });
    },

    async advancePhase(toolboxId) {
      const res = await httpClient.post<PhaseDto>(`${BASE}/${toolboxId}/phases/advance`, {});
      return PhaseSchema.parse(res);
    },

    // Templates
    async applyTemplate(templateId, gameId) {
      const res = await httpClient.post<ToolboxDto>(`${TEMPLATES_BASE}/${templateId}/apply`, {
        templateId,
        gameId,
      });
      return ToolboxSchema.parse(res);
    },
  };
}

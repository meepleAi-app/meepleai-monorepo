/**
 * agent-config.schemas — BE-aligned contract tests (Issue #2727, BUG A)
 *
 * The PUT /api/v1/library/games/{gameId}/agent-config endpoint binds
 * `AgentConfigDto(string LlmModel, double Temperature, int MaxTokens,
 * string Personality, string DetailLevel, string? PersonalNotes)` and its
 * FluentValidation validator only accepts the canonical Italian personality /
 * detail-level values. The FE contract must match the BE, otherwise the save
 * fails with HTTP 400/500 (see ConfigureGameAgentCommandValidator).
 */
import { describe, it, expect } from 'vitest';

import {
  UpdateAgentConfigRequestSchema,
  AgentConfigDtoSchema,
  DEFAULT_AGENT_CONFIG,
} from '../agent-config.schemas';

describe('agent-config.schemas — BE-aligned contract (#2727 BUG A)', () => {
  it('request uses BE field names (llmModel, personalNotes)', () => {
    const parsed = UpdateAgentConfigRequestSchema.parse({
      llmModel: 'llama-3.3-70b-free',
      temperature: 0.7,
      maxTokens: 4096,
      personality: 'Amichevole',
      detailLevel: 'Normale',
      personalNotes: 'gioco spesso in 4',
    });

    expect(parsed.llmModel).toBe('llama-3.3-70b-free');
    expect(parsed.personalNotes).toBe('gioco spesso in 4');
  });

  it('personality/detailLevel accept BE canonical values and reject legacy English', () => {
    expect(() =>
      UpdateAgentConfigRequestSchema.parse({
        llmModel: 'llama-3.3-70b-free',
        temperature: 0.7,
        maxTokens: 4096,
        personality: 'Professionale',
        detailLevel: 'Esaustivo',
      })
    ).not.toThrow();

    expect(() =>
      UpdateAgentConfigRequestSchema.parse({
        llmModel: 'llama-3.3-70b-free',
        temperature: 0.7,
        maxTokens: 4096,
        personality: 'friendly',
        detailLevel: 'normal',
      })
    ).toThrow();
  });

  it('response mirrors BE AgentConfigDto shape (no id/userId/gameId/createdAt)', () => {
    const parsed = AgentConfigDtoSchema.parse({
      llmModel: 'google-gemini-pro',
      temperature: 0.5,
      maxTokens: 2048,
      personality: 'Conciso',
      detailLevel: 'Breve',
      personalNotes: null,
    });

    expect(parsed.llmModel).toBe('google-gemini-pro');
    expect(parsed.personality).toBe('Conciso');
  });

  it('DEFAULT_AGENT_CONFIG uses BE-aligned defaults', () => {
    expect(DEFAULT_AGENT_CONFIG.llmModel).toBeDefined();
    expect(DEFAULT_AGENT_CONFIG.personality).toBe('Amichevole');
    expect(DEFAULT_AGENT_CONFIG.detailLevel).toBe('Normale');
  });
});

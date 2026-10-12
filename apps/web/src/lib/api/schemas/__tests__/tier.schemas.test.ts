/**
 * tier.schemas — transition contract for the retired per-user agent limit (Issue #4138)
 *
 * The FE stopped reading `agents` / `agentsMax` (usage) and `limits.maxAgents` (admin tiers)
 * before the BE drops them. Until the BE slice ships, its payloads still carry those fields:
 * the schemas must keep parsing them, and must strip them.
 */

import { describe, expect, it } from 'vitest';

import { TierDefinitionSchema, UsageSnapshotSchema } from '../tier.schemas';

const usage = {
  privateGames: 1,
  privateGamesMax: 3,
  pdfThisMonth: 0,
  pdfThisMonthMax: 3,
  agentQueriesToday: 2,
  agentQueriesTodayMax: 20,
  sessionQueries: 0,
  sessionQueriesMax: 30,
  photosThisSession: 0,
  photosThisSessionMax: 5,
  sessionSaveEnabled: false,
  catalogProposalsThisWeek: 0,
  catalogProposalsThisWeekMax: 1,
};

const limits = {
  maxPrivateGames: 3,
  maxPdfUploadsPerMonth: 3,
  maxPdfSizeBytes: 52428800,
  maxAgentQueriesPerDay: 20,
  maxSessionQueries: 30,
  maxSessionPlayers: 6,
  maxPhotosPerSession: 5,
  sessionSaveEnabled: false,
  maxCatalogProposalsPerWeek: 1,
  maxGamebookTranslationsPerMonth: 50,
};

const tier = {
  id: 'a1b2c3d4-1111-4111-8111-111111111111',
  name: 'free',
  displayName: 'Free',
  llmModelTier: 'free',
  isDefault: true,
  createdAt: '2026-10-10T00:00:00Z',
  updatedAt: '2026-10-10T00:00:00Z',
};

describe('UsageSnapshotSchema', () => {
  it('parses a usage snapshot without agent fields', () => {
    expect(() => UsageSnapshotSchema.parse(usage)).not.toThrow();
  });

  it('still parses a BE payload that carries agents/agentsMax, and drops them', () => {
    const parsed = UsageSnapshotSchema.parse({ ...usage, agents: 0, agentsMax: 1 });
    expect(parsed).not.toHaveProperty('agents');
    expect(parsed).not.toHaveProperty('agentsMax');
  });
});

describe('TierDefinitionSchema', () => {
  it('parses tier limits without maxAgents', () => {
    expect(() => TierDefinitionSchema.parse({ ...tier, limits })).not.toThrow();
  });

  it('still parses a BE payload whose limits carry maxAgents, and drops it', () => {
    const parsed = TierDefinitionSchema.parse({ ...tier, limits: { ...limits, maxAgents: 1 } });
    expect(parsed.limits).not.toHaveProperty('maxAgents');
  });
});

/**
 * Issue #4212. Zod strips the keys a schema does not name. The limits schema did not name the
 * gamebook quota, so the admin page never held it and could not send it back: every save zeroed
 * it. The library limit and the RAPTOR flag are new in the payload: they are kept when present
 * and tolerated when absent, so this build still reads a backend that does not send them yet.
 */
describe('TierDefinitionSchema — limits the admin page must send back (#4212)', () => {
  it('keeps the gamebook quota, the library limit and the RAPTOR flag', () => {
    const parsed = TierDefinitionSchema.parse({
      ...tier,
      limits: { ...limits, maxLibraryGames: 5, raptorRebuildEnabled: false },
    });

    expect(parsed.limits).toMatchObject({
      maxGamebookTranslationsPerMonth: 50,
      maxLibraryGames: 5,
      raptorRebuildEnabled: false,
    });
  });

  it('still parses a backend payload without the two new fields', () => {
    expect(() => TierDefinitionSchema.parse({ ...tier, limits })).not.toThrow();
  });
});

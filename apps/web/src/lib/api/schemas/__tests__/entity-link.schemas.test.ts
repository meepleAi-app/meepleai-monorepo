/**
 * EntityLink Schemas Tests (Issue #5142 — Epic A EntityRelationships)
 *
 * Validates Zod schemas parse .NET JsonStringEnumConverter output correctly.
 */

import { describe, it, expect } from 'vitest';
import {
  MeepleEntityTypeSchema,
  EntityLinkTypeSchema,
  EntityLinkScopeSchema,
  EntityLinkDtoSchema,
  EntityLinkCountResponseSchema,
  ImportBggExpansionsResponseSchema,
} from '../entity-link.schemas';

// ── MeepleEntityTypeSchema ──────────────────────────────────────────────────

describe('MeepleEntityTypeSchema', () => {
  it('accepts all valid entity types', () => {
    const valid = [
      'Game',
      'Player',
      'Session',
      'Agent',
      'Document',
      'ChatSession',
      'Event',
      'Toolkit',
    ];
    valid.forEach(v => {
      expect(MeepleEntityTypeSchema.safeParse(v).success, `"${v}" should be valid`).toBe(true);
    });
  });

  it('rejects unknown entity types', () => {
    const invalid = ['game', 'GAME', 'Unknown', '', null, 123];
    invalid.forEach(v => {
      expect(MeepleEntityTypeSchema.safeParse(v).success, `"${v}" should be invalid`).toBe(false);
    });
  });
});

// ── EntityLinkTypeSchema ────────────────────────────────────────────────────

describe('EntityLinkTypeSchema', () => {
  it('accepts all valid link types (PascalCase from JsonStringEnumConverter)', () => {
    const valid = [
      'ExpansionOf',
      'SequelOf',
      'Reimplements',
      'CompanionTo',
      'RelatedTo',
      'PartOf',
      'CollaboratesWith',
      'SpecializedBy',
    ];
    valid.forEach(v => {
      expect(EntityLinkTypeSchema.safeParse(v).success, `"${v}" should be valid`).toBe(true);
    });
  });

  it('rejects lowercase and snake_case variants', () => {
    const invalid = ['expansion_of', 'expansionOf', 'EXPANSION_OF', 'related_to', '', null];
    invalid.forEach(v => {
      expect(EntityLinkTypeSchema.safeParse(v).success, `"${v}" should be invalid`).toBe(false);
    });
  });
});

// ── EntityLinkScopeSchema ───────────────────────────────────────────────────

describe('EntityLinkScopeSchema', () => {
  it('accepts User and Shared', () => {
    expect(EntityLinkScopeSchema.safeParse('User').success).toBe(true);
    expect(EntityLinkScopeSchema.safeParse('Shared').success).toBe(true);
  });

  it('rejects other values', () => {
    ['user', 'SHARED', 'Public', '', null].forEach(v => {
      expect(EntityLinkScopeSchema.safeParse(v).success, `"${v}" should be invalid`).toBe(false);
    });
  });
});

// ── EntityLinkDtoSchema ─────────────────────────────────────────────────────

const validEntityLinkDto = {
  id: '550e8400-e29b-41d4-a716-446655440000',
  sourceEntityType: 'Game',
  sourceEntityId: '550e8400-e29b-41d4-a716-446655440001',
  targetEntityType: 'Game',
  targetEntityId: '550e8400-e29b-41d4-a716-446655440002',
  linkType: 'ExpansionOf',
  isBidirectional: false,
  scope: 'Shared',
  ownerUserId: '550e8400-e29b-41d4-a716-446655440003',
  metadata: null,
  isAdminApproved: true,
  isBggImported: true,
  createdAt: '2026-02-01T10:00:00Z',
  updatedAt: '2026-02-01T10:00:00Z',
};

describe('EntityLinkDtoSchema', () => {
  it('parses a valid BGG-imported EntityLink DTO', () => {
    const result = EntityLinkDtoSchema.safeParse(validEntityLinkDto);
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.linkType).toBe('ExpansionOf');
      expect(result.data.isBggImported).toBe(true);
      expect(result.data.isAdminApproved).toBe(true);
      expect(result.data.isOwner).toBe(false); // defaults to false
    }
  });

  it('defaults isOwner to false when omitted', () => {
    const result = EntityLinkDtoSchema.safeParse({ ...validEntityLinkDto });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.isOwner).toBe(false);
    }
  });

  it('parses isOwner=true when provided', () => {
    const result = EntityLinkDtoSchema.safeParse({ ...validEntityLinkDto, isOwner: true });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.isOwner).toBe(true);
    }
  });

  it('accepts nullable metadata', () => {
    const withMetadata = { ...validEntityLinkDto, metadata: '{"bgg_source_id":1234}' };
    const result = EntityLinkDtoSchema.safeParse(withMetadata);
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.metadata).toBe('{"bgg_source_id":1234}');
    }
  });

  it('accepts all MeepleEntityType values in source/target', () => {
    const types = [
      'Game',
      'Player',
      'Session',
      'Agent',
      'Document',
      'ChatSession',
      'Event',
      'Toolkit',
    ];
    types.forEach(t => {
      const result = EntityLinkDtoSchema.safeParse({
        ...validEntityLinkDto,
        sourceEntityType: t,
        targetEntityType: t,
      });
      expect(result.success, `EntityType "${t}" should be valid`).toBe(true);
    });
  });

  it('rejects invalid UUID for id', () => {
    const result = EntityLinkDtoSchema.safeParse({ ...validEntityLinkDto, id: 'not-a-uuid' });
    expect(result.success).toBe(false);
  });

  it('rejects missing required fields', () => {
    const { id: _id, ...withoutId } = validEntityLinkDto;
    const result = EntityLinkDtoSchema.safeParse(withoutId);
    expect(result.success).toBe(false);
  });

  it('parses all EntityLinkType values', () => {
    const linkTypes = [
      'ExpansionOf',
      'SequelOf',
      'Reimplements',
      'CompanionTo',
      'RelatedTo',
      'PartOf',
      'CollaboratesWith',
      'SpecializedBy',
    ];
    linkTypes.forEach(lt => {
      const result = EntityLinkDtoSchema.safeParse({ ...validEntityLinkDto, linkType: lt });
      expect(result.success, `linkType "${lt}" should be valid`).toBe(true);
    });
  });

  it('parses user-scoped non-BGG-imported link', () => {
    const userLink = {
      ...validEntityLinkDto,
      scope: 'User',
      isAdminApproved: true,
      isBggImported: false,
      isOwner: true,
    };
    const result = EntityLinkDtoSchema.safeParse(userLink);
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.scope).toBe('User');
      expect(result.data.isBggImported).toBe(false);
    }
  });
});

// ── EntityLinkCountResponseSchema ───────────────────────────────────────────

describe('EntityLinkCountResponseSchema', () => {
  it('parses valid count response', () => {
    const result = EntityLinkCountResponseSchema.safeParse({ count: 5 });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.count).toBe(5);
    }
  });

  it('accepts count of zero', () => {
    const result = EntityLinkCountResponseSchema.safeParse({ count: 0 });
    expect(result.success).toBe(true);
  });

  it('rejects negative count', () => {
    const result = EntityLinkCountResponseSchema.safeParse({ count: -1 });
    expect(result.success).toBe(false);
  });

  it('rejects non-integer count', () => {
    const result = EntityLinkCountResponseSchema.safeParse({ count: 1.5 });
    expect(result.success).toBe(false);
  });
});

// ── ImportBggExpansionsResponseSchema ───────────────────────────────────────

describe('ImportBggExpansionsResponseSchema', () => {
  it('parses valid import response', () => {
    const result = ImportBggExpansionsResponseSchema.safeParse({ created: 3 });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.created).toBe(3);
    }
  });

  it('accepts zero created', () => {
    const result = ImportBggExpansionsResponseSchema.safeParse({ created: 0 });
    expect(result.success).toBe(true);
  });

  it('rejects negative created', () => {
    const result = ImportBggExpansionsResponseSchema.safeParse({ created: -1 });
    expect(result.success).toBe(false);
  });
});

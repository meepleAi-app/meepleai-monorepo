/**
 * Admin Cover Picker Schemas Tests (Epic #3470 — Slice 1d-c)
 *
 * Validates the Zod schemas parse the merged backend contract:
 * - enums serialize as PascalCase names (JsonStringEnumConverter, no naming policy)
 * - PropertyNamingPolicy=CamelCase, no DefaultIgnoreCondition → nulls ARE emitted
 * - previewUrl/source/gameId/candidates/assignments/focalX/focalY required;
 *   license/attribution/sourceUrl + assignments.card/hero/social nullable.
 */

import { describe, it, expect } from 'vitest';

import {
  CoverAssignmentSourceSchema,
  CoverContextSchema,
  CoverCandidateSchema,
  CoverCandidatesSchema,
  CoverAssignmentSchema,
  AssignCoverRequestSchema,
} from '../admin/admin-cover.schemas';

// ── CoverAssignmentSourceSchema ─────────────────────────────────────────────

describe('CoverAssignmentSourceSchema', () => {
  it('accepts the 4 materialized sources (PascalCase from JsonStringEnumConverter)', () => {
    ['Pdf', 'Bgg', 'Wikidata', 'Manual'].forEach(v => {
      expect(CoverAssignmentSourceSchema.safeParse(v).success, `"${v}" should be valid`).toBe(true);
    });
  });

  it('rejects lowercase, unknown, and non-string values', () => {
    ['pdf', 'BGG', 'wikidata', 'Custom', 'User', '', null, 0].forEach(v => {
      expect(CoverAssignmentSourceSchema.safeParse(v).success, `"${v}" should be invalid`).toBe(
        false
      );
    });
  });
});

// ── CoverContextSchema ──────────────────────────────────────────────────────

describe('CoverContextSchema', () => {
  it('accepts the 3 UI contexts (Card/Hero/Social)', () => {
    ['Card', 'Hero', 'Social'].forEach(v => {
      expect(CoverContextSchema.safeParse(v).success, `"${v}" should be valid`).toBe(true);
    });
  });

  it('rejects lowercase and unknown contexts', () => {
    ['card', 'HERO', 'Thumbnail', 'og', '', null].forEach(v => {
      expect(CoverContextSchema.safeParse(v).success, `"${v}" should be invalid`).toBe(false);
    });
  });
});

// ── CoverCandidateSchema ────────────────────────────────────────────────────

const validCandidate = {
  source: 'Wikidata',
  previewUrl: 'https://r2.example/cover-preview.webp',
  license: 'CC BY-SA 4.0',
  attribution: 'Jane Artist',
  sourceUrl: 'https://commons.wikimedia.org/wiki/File:x.jpg',
};

describe('CoverCandidateSchema', () => {
  it('parses a fully-populated candidate', () => {
    const result = CoverCandidateSchema.safeParse(validCandidate);
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.source).toBe('Wikidata');
      expect(result.data.previewUrl).toBe('https://r2.example/cover-preview.webp');
    }
  });

  it('accepts null license/attribution/sourceUrl (BE emits null, not omitted)', () => {
    const result = CoverCandidateSchema.safeParse({
      source: 'Pdf',
      previewUrl: 'https://r2.example/pdf.webp',
      license: null,
      attribution: null,
      sourceUrl: null,
    });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.license).toBeNull();
    }
  });

  it('accepts omitted optional metadata fields', () => {
    const result = CoverCandidateSchema.safeParse({
      source: 'Bgg',
      previewUrl: 'https://r2.example/bgg.webp',
    });
    expect(result.success).toBe(true);
  });

  it('rejects a missing source', () => {
    const { source: _source, ...withoutSource } = validCandidate;
    expect(CoverCandidateSchema.safeParse(withoutSource).success).toBe(false);
  });

  it('rejects a missing previewUrl (required, non-null in BE)', () => {
    const { previewUrl: _previewUrl, ...withoutPreview } = validCandidate;
    expect(CoverCandidateSchema.safeParse(withoutPreview).success).toBe(false);
  });

  it('rejects a null previewUrl', () => {
    expect(CoverCandidateSchema.safeParse({ ...validCandidate, previewUrl: null }).success).toBe(
      false
    );
  });
});

// ── CoverCandidatesSchema (the GET read shape) ──────────────────────────────

const validCandidatesDto = {
  gameId: '550e8400-e29b-41d4-a716-446655440000',
  candidates: [validCandidate],
  assignments: {
    card: 'Wikidata',
    hero: null,
    social: null,
  },
};

describe('CoverCandidatesSchema', () => {
  it('parses the full read shape with a mix of assigned and null contexts', () => {
    const result = CoverCandidatesSchema.safeParse(validCandidatesDto);
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.assignments.card).toBe('Wikidata');
      expect(result.data.assignments.hero).toBeNull();
      expect(result.data.candidates).toHaveLength(1);
    }
  });

  it('accepts an empty candidates array', () => {
    const result = CoverCandidatesSchema.safeParse({ ...validCandidatesDto, candidates: [] });
    expect(result.success).toBe(true);
  });

  it('accepts assignments with all contexts null (no explicit admin pin)', () => {
    const result = CoverCandidatesSchema.safeParse({
      ...validCandidatesDto,
      assignments: { card: null, hero: null, social: null },
    });
    expect(result.success).toBe(true);
  });

  it('rejects a missing assignments object', () => {
    const { assignments: _assignments, ...withoutAssignments } = validCandidatesDto;
    expect(CoverCandidatesSchema.safeParse(withoutAssignments).success).toBe(false);
  });

  it('rejects a non-uuid gameId', () => {
    expect(CoverCandidatesSchema.safeParse({ ...validCandidatesDto, gameId: 'nope' }).success).toBe(
      false
    );
  });

  it('rejects an invalid enum inside assignments', () => {
    const result = CoverCandidatesSchema.safeParse({
      ...validCandidatesDto,
      assignments: { card: 'custom', hero: null, social: null },
    });
    expect(result.success).toBe(false);
  });
});

// ── CoverAssignmentSchema (the PUT response) ────────────────────────────────

describe('CoverAssignmentSchema', () => {
  it('parses the persisted assignment returned by the write command', () => {
    const result = CoverAssignmentSchema.safeParse({
      context: 'Hero',
      source: 'Pdf',
      focalX: 0.42,
      focalY: 0.5,
    });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.context).toBe('Hero');
      expect(result.data.focalX).toBe(0.42);
    }
  });

  it('rejects a missing focal coordinate', () => {
    expect(
      CoverAssignmentSchema.safeParse({ context: 'Card', source: 'Pdf', focalX: 0.5 }).success
    ).toBe(false);
  });
});

// ── AssignCoverRequestSchema (the PUT body) ─────────────────────────────────

describe('AssignCoverRequestSchema', () => {
  it('parses a valid pin request with in-range focal point', () => {
    const result = AssignCoverRequestSchema.safeParse({
      source: 'Manual',
      focalX: 0,
      focalY: 1,
    });
    expect(result.success).toBe(true);
  });

  it('rejects a focal coordinate outside 0..1', () => {
    expect(
      AssignCoverRequestSchema.safeParse({ source: 'Pdf', focalX: 1.5, focalY: 0.5 }).success
    ).toBe(false);
    expect(
      AssignCoverRequestSchema.safeParse({ source: 'Pdf', focalX: 0.5, focalY: -0.1 }).success
    ).toBe(false);
  });

  it('rejects a missing source', () => {
    expect(AssignCoverRequestSchema.safeParse({ focalX: 0.5, focalY: 0.5 }).success).toBe(false);
  });
});

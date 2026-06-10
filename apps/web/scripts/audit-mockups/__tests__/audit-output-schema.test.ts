import { describe, it, expect } from 'vitest';
import {
  MockupClassificationSchema,
  ClusterOutputSchema,
  type MockupClassification,
} from '../audit-output-schema.mjs';

describe('MockupClassificationSchema', () => {
  const valid: MockupClassification = {
    mockup_path: 'admin-mockups/design_files/sp4-library.html',
    design_intent: 'current',
    confidence: 0.85,
    reasoning: 'No markers found. Codebase route /library matches.',
    sub_components: ['LibraryHub', 'GameCard'],
    pair_disagreement: false,
    suggested_tracking_issue: null,
  };

  it('accepts valid current classification', () => {
    expect(() => MockupClassificationSchema.parse(valid)).not.toThrow();
  });

  it('accepts forward-refactor-obsolete with tracking issue', () => {
    const obsolete: MockupClassification = {
      ...valid,
      design_intent: 'forward-refactor-obsolete',
      suggested_tracking_issue: { title: 'X', body: 'Y' },
    };
    expect(() => MockupClassificationSchema.parse(obsolete)).not.toThrow();
  });

  it('rejects missing design_intent', () => {
    const { design_intent: _ignored, ...invalid } = valid;
    expect(() => MockupClassificationSchema.parse(invalid)).toThrow(/design_intent/);
  });

  it('rejects invalid design_intent enum', () => {
    const invalid = { ...valid, design_intent: 'obsolete' };
    expect(() => MockupClassificationSchema.parse(invalid)).toThrow();
  });

  it('rejects confidence > 1', () => {
    const invalid = { ...valid, confidence: 1.5 };
    expect(() => MockupClassificationSchema.parse(invalid)).toThrow();
  });

  it('rejects confidence < 0', () => {
    const invalid = { ...valid, confidence: -0.1 };
    expect(() => MockupClassificationSchema.parse(invalid)).toThrow();
  });

  it('requires suggested_tracking_issue when design_intent=forward-refactor-obsolete', () => {
    const invalid = {
      ...valid,
      design_intent: 'forward-refactor-obsolete',
      suggested_tracking_issue: null,
    };
    expect(() => MockupClassificationSchema.parse(invalid)).toThrow(/tracking/i);
  });
});

describe('ClusterOutputSchema', () => {
  const sample: MockupClassification = {
    mockup_path: 'admin-mockups/design_files/auth.html',
    design_intent: 'current',
    confidence: 0.9,
    reasoning: 'OK',
    sub_components: [],
    pair_disagreement: false,
    suggested_tracking_issue: null,
  };

  it('accepts array of classifications', () => {
    expect(() => ClusterOutputSchema.parse([sample, sample])).not.toThrow();
  });

  it('rejects empty array', () => {
    expect(() => ClusterOutputSchema.parse([])).toThrow();
  });
});

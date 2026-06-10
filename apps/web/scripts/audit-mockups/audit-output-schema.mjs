/**
 * audit-output-schema.mjs — zod schema for mockup audit output (DS-17 Phase B).
 *
 * Single source of truth for the JSON structure each cluster auditor agent emits.
 * Used by: master orchestrator (validate agent output) + generate-deliverables.mjs
 * (load aggregated audit before generating fidelity.json + queue + drafts).
 *
 * Refs:
 *   Spec: docs/superpowers/specs/2026-06-10-ds-17-phase-b-mockup-audit-design.md
 *   Umbrella: #2063
 *   Sub-issue: #2127
 */

import { z } from 'zod';

const DesignIntent = z.enum(['current', 'forward-refactor', 'forward-refactor-obsolete']);

const TrackingIssue = z.object({
  title: z.string().min(1),
  body: z.string().min(1),
});

export const MockupClassificationSchema = z
  .object({
    mockup_path: z.string().min(1),
    design_intent: DesignIntent,
    confidence: z.number().min(0).max(1),
    reasoning: z.string().min(1),
    sub_components: z.array(z.string()),
    pair_disagreement: z.boolean(),
    suggested_tracking_issue: TrackingIssue.nullable(),
  })
  .refine(
    (data) =>
      data.design_intent !== 'forward-refactor-obsolete' ||
      data.suggested_tracking_issue !== null,
    {
      message: 'suggested_tracking_issue required when design_intent=forward-refactor-obsolete',
      path: ['suggested_tracking_issue'],
    }
  );

export const ClusterOutputSchema = z.array(MockupClassificationSchema).min(1);

/** @typedef {z.infer<typeof MockupClassificationSchema>} MockupClassification */
/** @typedef {z.infer<typeof ClusterOutputSchema>} ClusterOutput */

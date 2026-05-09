/**
 * ESLint rule: no inline ENTITY HSL/HSLA in v2 components.
 *
 * Disallows hsl()/hsla() literals where the hue matches one of the 9 entity
 * color signatures (game 25, player 262, session 240, agent 38, kb 174/210,
 * chat 220, event 350, toolkit 142, tool 195). Non-entity hues (custom
 * decorative palette like purple 270, grey 220 low-sat) are allowed.
 *
 * Forces use of entity-tokens helpers from
 * `apps/web/src/components/ui/v2/entity-tokens.ts` (getEntityToken) or
 * Tailwind utilities (text-entity-game, bg-entity-event/10).
 *
 * Scope: src/components/v2/** (feature compositions).
 * For UI primitives (src/components/ui/v2/**) see: local/no-hardcoded-hex.
 *
 * Refs P2 plan Task 9, design spec §3.5, issue #807
 */

'use strict';

// Entity hue signatures: hue ± tolerance, saturation must be in declared range.
// Values derived from tokens.ts entityColors and entity-tokens.ts.
const ENTITY_HUE_RANGES = [
  // game: hue ~25 (orange-amber), high saturation
  { entity: 'game', hMin: 20, hMax: 32, sMin: 80, sMax: 100 },
  // player: hue ~262 (violet), mid-high saturation
  { entity: 'player', hMin: 256, hMax: 268, sMin: 70, sMax: 95 },
  // session: hue ~240 (blue-indigo), mid saturation
  { entity: 'session', hMin: 234, hMax: 246, sMin: 55, sMax: 75 },
  // agent: hue ~38 (amber), high saturation
  { entity: 'agent', hMin: 33, hMax: 44, sMin: 80, sMax: 100 },
  // kb-teal: hue ~174 (teal), mid saturation
  { entity: 'kb-teal', hMin: 169, hMax: 179, sMin: 45, sMax: 70 },
  // kb-slate: hue ~210 (slate-blue), lower-mid saturation
  { entity: 'kb-slate', hMin: 204, hMax: 216, sMin: 35, sMax: 60 },
  // chat: hue ~220 (indigo-blue), mid-high saturation
  { entity: 'chat', hMin: 215, hMax: 226, sMin: 65, sMax: 95 },
  // event: hue ~350 (crimson/rose), high saturation
  { entity: 'event', hMin: 344, hMax: 360, sMin: 75, sMax: 100 },
  // toolkit: hue ~142 (green), mid-high saturation
  { entity: 'toolkit', hMin: 137, hMax: 148, sMin: 60, sMax: 80 },
  // tool: hue ~195 (cyan-teal), mid-high saturation
  { entity: 'tool', hMin: 190, hMax: 201, sMin: 65, sMax: 95 },
];

// Matches hsl(N, N%, N%) and hsla(N, N%, N%, alpha) — comma-separated form only.
// Does NOT match hsl(var(--...)) — that form has no digit as first token.
const HSL_LITERAL_REGEX = /hsla?\(\s*(\d+)\s*,\s*(\d+)%\s*,\s*(\d+)%(?:\s*,\s*([\d.]+))?\s*\)/i;

/**
 * Return the entity name if (h, s) matches a known entity hue signature,
 * or null if it is a non-entity / decorative hue.
 */
function matchesEntity(h, s) {
  for (const range of ENTITY_HUE_RANGES) {
    if (h >= range.hMin && h <= range.hMax && s >= range.sMin && s <= range.sMax) {
      return range.entity;
    }
  }
  return null;
}

module.exports = {
  meta: {
    type: 'problem',
    docs: {
      description:
        'Disallow inline hsl()/hsla() with entity hue signatures in v2 feature compositions — ' +
        'use Tailwind entity utilities (text-entity-game, bg-entity-event/10) or ' +
        'getEntityToken() helper from @/components/ui/v2/entity-tokens instead',
      category: 'Design System',
      recommended: false,
    },
    schema: [],
    messages: {
      entityHsl:
        'Inline {{kind}} matches {{entity}} entity color. ' +
        'Use Tailwind utility (e.g. text-entity-{{entityKey}}, bg-entity-{{entityKey}}/10) or ' +
        'getEntityToken() helper from @/components/ui/v2/entity-tokens. ' +
        'For unavoidable JS style props (gradients with alpha stops) add: ' +
        '// eslint-disable-next-line meepleai/no-inline-hsl-v2 -- <reason>. ' +
        'See docs/for-developers/frontend/v2-token-system.md.',
    },
  },

  create(context) {
    function checkString(node, value) {
      if (typeof value !== 'string') return;
      const match = value.match(HSL_LITERAL_REGEX);
      if (!match) return;
      const h = parseInt(match[1], 10);
      const s = parseInt(match[2], 10);
      const entity = matchesEntity(h, s);
      if (!entity) return; // non-entity hue — allowed

      // Map internal entity name to Tailwind class key.
      // kb-teal and kb-slate both map to 'document' in Tailwind utilities.
      const entityKey = entity.startsWith('kb') ? 'document' : entity;
      const kind = /^hsla/i.test(match[0]) ? 'hsla()' : 'hsl()';

      context.report({
        node,
        messageId: 'entityHsl',
        data: { kind, entity, entityKey },
      });
    }

    return {
      Literal(node) {
        checkString(node, node.value);
      },
      TemplateElement(node) {
        // Detect entity HSL inside template literals (e.g. gradient strings).
        checkString(node, node.value && node.value.cooked);
      },
    };
  },
};

/**
 * Tests for ESLint custom rule: no-inline-hsl-v2
 *
 * P2 issue #807 Task 9: guards v2 feature compositions (src/components/v2/**)
 * against inline entity HSL/HSLA literals.
 *
 * Run directly: node apps/web/eslint-rules/no-inline-hsl-v2.test.js
 */
'use strict';

const { RuleTester } = require('eslint');
const rule = require('./no-inline-hsl-v2');

const ruleTester = new RuleTester({
  languageOptions: {
    ecmaVersion: 'latest',
    sourceType: 'module',
    parserOptions: {
      ecmaFeatures: { jsx: true },
    },
  },
});

ruleTester.run('no-inline-hsl-v2', rule, {
  valid: [
    // Tailwind entity utility usage — the prescribed pattern
    { code: `const x = "text-entity-event";` },
    { code: `const x = "bg-entity-event/10";` },
    { code: `const x = "text-entity-game";` },
    // CSS var reference — generated at runtime, not a literal
    { code: `const color = "var(--c-event)";` },
    { code: `const color = "hsl(var(--c-game))";` },
    { code: `const color = "hsl(var(--c-agent) / 0.1)";` },
    // Non-entity purple 270 (decorative, hue outside all entity ranges)
    { code: `const c = "hsl(270, 80%, 50%)";` },
    { code: `const c = "hsla(270, 80%, 60%, 0.18)";` },
    // Non-entity grey (low saturation — below all sMin thresholds)
    { code: `const c = "hsl(220, 8%, 46%)";` },
    { code: `const c = "hsl(215, 16%, 92%)";` },
    // Non-entity near-black (low saturation)
    { code: `const c = "hsl(0, 0%, 4%)";` },
    { code: `const c = "hsl(28, 30%, 12%)";` },
    // Non-entity mid-hue with low saturation
    { code: `const c = "hsl(180, 50%, 50%)";` },
    // Decorative error/danger (hue 0-5 with mid sat, outside event range)
    { code: `const c = "hsl(0, 72%, 51%)";` },
    // Non-string literals
    { code: `const n = 42;` },
    { code: `const b = true;` },
    // Empty / whitespace
    { code: `const s = '';` },
  ],

  invalid: [
    // --- event entity (hue ~350, high sat) ---
    {
      code: `const c = "hsl(350, 89%, 48%)";`,
      errors: [{ messageId: 'entityHsl' }],
    },
    {
      code: `const c = "hsla(350, 89%, 48%, 0.10)";`,
      errors: [{ messageId: 'entityHsl' }],
    },
    {
      code: `const c = "hsla(350, 89%, 60%, 0.14)";`,
      errors: [{ messageId: 'entityHsl' }],
    },

    // --- game entity (hue ~25, high sat) ---
    {
      code: `const c = "hsl(25, 95%, 45%)";`,
      errors: [{ messageId: 'entityHsl' }],
    },
    {
      code: `const c = "hsla(25, 95%, 45%, 0.18)";`,
      errors: [{ messageId: 'entityHsl' }],
    },
    {
      code: `const c = "hsl(28, 80%, 38%)";`,
      errors: [{ messageId: 'entityHsl' }],
    },

    // --- agent entity (hue ~38, high sat) ---
    {
      code: `const c = "hsl(38, 92%, 33%)";`,
      errors: [{ messageId: 'entityHsl' }],
    },
    {
      code: `const c = "hsla(38, 92%, 60%, 1)";`,
      errors: [{ messageId: 'entityHsl' }],
    },

    // --- toolkit entity (hue ~142, mid-high sat) ---
    {
      code: `const c = "hsl(142, 70%, 31%)";`,
      errors: [{ messageId: 'entityHsl' }],
    },
    {
      code: `const c = "hsla(142, 70%, 31%, 0.85)";`,
      errors: [{ messageId: 'entityHsl' }],
    },

    // --- session entity (hue ~240, mid sat) ---
    {
      code: `const c = "hsl(240, 60%, 45%)";`,
      errors: [{ messageId: 'entityHsl' }],
    },
    {
      code: `const c = "hsla(240, 60%, 55%, 0.22)";`,
      errors: [{ messageId: 'entityHsl' }],
    },

    // --- player entity (hue ~262, mid-high sat) ---
    {
      code: `const c = "hsla(262, 83%, 58%, 0.1)";`,
      errors: [{ messageId: 'entityHsl' }],
    },

    // --- chat entity (hue ~220, mid-high sat) ---
    {
      code: `const c = "hsl(220, 80%, 55%)";`,
      errors: [{ messageId: 'entityHsl' }],
    },
    {
      code: `const c = "hsla(220, 80%, 55%, 0.1)";`,
      errors: [{ messageId: 'entityHsl' }],
    },

    // --- kb-slate entity (hue ~210, lower-mid sat) ---
    {
      code: `const c = "hsl(210, 50%, 28%)";`,
      errors: [{ messageId: 'entityHsl' }],
    },
    {
      code: `const c = "hsla(210, 40%, 48%, 0.3)";`,
      errors: [{ messageId: 'entityHsl' }],
    },

    // --- tool entity (hue ~195, mid-high sat) ---
    {
      code: `const c = "hsl(195, 80%, 50%)";`,
      errors: [{ messageId: 'entityHsl' }],
    },

    // --- template literal with entity hue ---
    {
      code: 'const c = `hsl(350, 89%, 48%)`;',
      errors: [{ messageId: 'entityHsl' }],
    },
    {
      code: 'const c = `hsla(25,95%,45%,0.18)`;',
      errors: [{ messageId: 'entityHsl' }],
    },

    // --- JSX style prop with entity hue ---
    {
      code: `const J = () => <div style={{ color: "hsl(350, 89%, 48%)" }} />;`,
      errors: [{ messageId: 'entityHsl' }],
    },
  ],
});

console.log('no-inline-hsl-v2: All tests passed');

/**
 * Tests for ESLint custom rule: no-hardcoded-hex
 *
 * Issue #572: V2 Phase 0 — guards v2 component code against hex/rgb/hsl
 * literals. Run with `pnpm test:eslint-rules` (or directly via vitest).
 */
const { RuleTester } = require('eslint');
const rule = require('./no-hardcoded-hex.js');

const ruleTester = new RuleTester({
  languageOptions: {
    ecmaVersion: 'latest',
    sourceType: 'module',
    parserOptions: { ecmaFeatures: { jsx: true } },
  },
});

ruleTester.run('no-hardcoded-hex', rule, {
  valid: [
    // CSS var references — the prescribed pattern
    { code: "const c = 'hsl(var(--c-game))';" },
    { code: "const c = 'hsl(var(--c-agent) / 0.1)';" },
    { code: 'const c = `hsl(var(--c-${entity}))`;' },
    { code: 'const c = `hsl(var(--c-${entity}) / ${alpha})`;' },
    // Non-color strings that look hex-ish
    { code: "const id = 'abc123';" }, // not # prefixed
    { code: "const v = '#tag-anchor';" }, // # but not hex chars
    { code: "const v = 'hsla matched no number';" }, // hsl-like but no numbers
    // Non-string literals
    { code: 'const n = 42;' },
    { code: 'const b = true;' },
    // Empty / whitespace strings
    { code: "const s = '';" },
    { code: "const s = '   ';" },
  ],
  invalid: [
    // Hardcoded hsl with commas
    {
      code: "const c = 'hsl(38, 92%, 50%)';",
      errors: [{ messageId: 'hardcodedHsl' }],
    },
    // Hardcoded hsl space-separated (CSS Color 4 syntax)
    {
      code: "const c = 'hsl(38 92% 50%)';",
      errors: [{ messageId: 'hardcodedHsl' }],
    },
    // Hardcoded hsl with alpha
    {
      code: "const c = 'hsl(38, 92%, 50%, 0.5)';",
      errors: [{ messageId: 'hardcodedHsl' }],
    },
    // 6-digit hex
    {
      code: "const c = '#7c3aed';",
      errors: [{ messageId: 'hardcodedHex' }],
    },
    // 3-digit hex
    {
      code: "const c = '#abc';",
      errors: [{ messageId: 'hardcodedHex' }],
    },
    // 8-digit hex with alpha
    {
      code: "const c = '#7c3aedff';",
      errors: [{ messageId: 'hardcodedHex' }],
    },
    // rgb()
    {
      code: "const c = 'rgb(124, 58, 237)';",
      errors: [{ messageId: 'hardcodedRgb' }],
    },
    // rgba()
    {
      code: "const c = 'rgba(124, 58, 237, 0.5)';",
      errors: [{ messageId: 'hardcodedRgb' }],
    },
    // Hardcoded hex in JSX style attribute
    {
      code: "const J = () => <div style={{ color: '#7c3aed' }} />;",
      errors: [{ messageId: 'hardcodedHex' }],
    },
    // Hardcoded hsl in template literal (the wave 1 review pattern)
    {
      code: 'const c = `hsl(140, 60%, 45%)`;',
      errors: [{ messageId: 'hardcodedHsl' }],
    },
  ],
});

console.log('no-hardcoded-hex: all tests passed');

/**
 * check-pnpm-settings-location.test.ts — unit test del gate di #3891.
 *
 * Run: pnpm vitest run scripts/__tests__/check-pnpm-settings-location.test.ts
 *
 * Ogni caso negativo qui e' uno stato reale osservato: il lockfile senza
 * `overrides` e' letteralmente quello che Dependabot ha prodotto su #3721,
 * #3723, #3724 e #3860.
 *
 * Refs:
 *   - Issue: #3891
 *   - Pattern: scripts/__tests__/lint-bgg-mockups.test.ts
 */

import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join, resolve } from 'node:path';

import {
  parseOverrides,
  checkPnpmSettings,
  checkWebRoot,
} from '../check-pnpm-settings-location.mjs';

const WEB_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');

const WS_YAML = [
  '# commento',
  'overrides:',
  "  '@babel/runtime': '>=7.26.10'",
  "  axios: '>=1.18.0'",
  '  eslint>ajv: ^6.12.6',
  '  pdfjs-dist: 6.2.108',
  '',
].join('\n');

const LOCK_YAML = [
  "lockfileVersion: '9.0'",
  '',
  'settings:',
  '  autoInstallPeers: true',
  '  excludeLinksFromLockfile: false',
  '',
  'overrides:',
  "  '@babel/runtime': '>=7.26.10'",
  "  axios: '>=1.18.0'",
  '  eslint>ajv: ^6.12.6',
  '  pdfjs-dist: 6.2.108',
  '',
  'importers:',
  '',
  '  .:',
  '    dependencies:',
  '      axios:',
  "        specifier: '>=1.18.0'",
  '        version: 1.19.0',
  '',
].join('\n');

// Il lockfile che pnpm 11 produce leggendo `pnpm.overrides` da package.json:
// la sezione `overrides` semplicemente non c'e'.
const LOCK_YAML_SENZA_OVERRIDES = [
  "lockfileVersion: '9.0'",
  '',
  'settings:',
  '  autoInstallPeers: true',
  '  excludeLinksFromLockfile: false',
  '',
  'importers:',
  '',
  '  .:',
  '    dependencies:',
  '      axios:',
  "        specifier: '>=1.18.0'",
  '        version: 1.19.0',
  '',
].join('\n');

const PKG_OK = JSON.stringify({ name: '@meepleai/web', private: true }, null, 2);
const PKG_CON_PNPM = JSON.stringify(
  { name: '@meepleai/web', private: true, pnpm: { overrides: { axios: '>=1.18.0' } } },
  null,
  2
);

describe('parseOverrides', () => {
  it('legge le chiavi quotate, quelle con `>` e i valori non quotati', () => {
    expect(parseOverrides(WS_YAML)).toEqual({
      '@babel/runtime': '>=7.26.10',
      axios: '>=1.18.0',
      'eslint>ajv': '^6.12.6',
      'pdfjs-dist': '6.2.108',
    });
  });

  it('si ferma alla chiave di primo livello successiva', () => {
    // `importers:` non deve finire dentro gli overrides.
    expect(Object.keys(parseOverrides(LOCK_YAML) ?? {})).toEqual([
      '@babel/runtime',
      'axios',
      'eslint>ajv',
      'pdfjs-dist',
    ]);
  });

  it('restituisce null quando la sezione non esiste', () => {
    expect(parseOverrides(LOCK_YAML_SENZA_OVERRIDES)).toBeNull();
    expect(parseOverrides(null)).toBeNull();
  });
});

describe('checkPnpmSettings', () => {
  it('non segnala nulla quando config e lockfile coincidono', () => {
    expect(
      checkPnpmSettings({ packageJson: PKG_OK, workspaceYaml: WS_YAML, lockYaml: LOCK_YAML })
    ).toEqual([]);
  });

  it('segnala la chiave `pnpm` tornata in package.json', () => {
    const errors = checkPnpmSettings({
      packageJson: PKG_CON_PNPM,
      workspaceYaml: WS_YAML,
      lockYaml: LOCK_YAML,
    });
    expect(errors).toHaveLength(1);
    expect(errors[0]).toContain('contiene la chiave "pnpm"');
  });

  it('segnala pnpm-workspace.yaml assente', () => {
    const errors = checkPnpmSettings({
      packageJson: PKG_OK,
      workspaceYaml: null,
      lockYaml: LOCK_YAML,
    });
    expect(errors).toHaveLength(1);
    expect(errors[0]).toContain('non esiste');
  });

  it('segnala gli `overrides` svuotati', () => {
    const errors = checkPnpmSettings({
      packageJson: PKG_OK,
      workspaceYaml: 'overrides:\n',
      lockYaml: LOCK_YAML,
    });
    expect(errors).toHaveLength(1);
    expect(errors[0]).toContain('non dichiara nessun `overrides`');
  });

  it('riconosce il lockfile rigenerato senza overrides (il difetto di #3891)', () => {
    const errors = checkPnpmSettings({
      packageJson: PKG_OK,
      workspaceYaml: WS_YAML,
      lockYaml: LOCK_YAML_SENZA_OVERRIDES,
    });
    expect(errors).toHaveLength(1);
    expect(errors[0]).toContain('non ha la sezione `overrides`');
    expect(errors[0]).toContain('4');
  });

  it('elenca la voce che diverge fra config e lockfile', () => {
    const lockDivergente = LOCK_YAML.replace("  axios: '>=1.18.0'", "  axios: '>=1.0.0'");
    const errors = checkPnpmSettings({
      packageJson: PKG_OK,
      workspaceYaml: WS_YAML,
      lockYaml: lockDivergente,
    });
    expect(errors).toHaveLength(1);
    expect(errors[0]).toContain('valore diverso: axios');
  });

  it('elenca la voce presente in config ma non nel lockfile', () => {
    const lockParziale = LOCK_YAML.replace("  axios: '>=1.18.0'\n", '');
    const errors = checkPnpmSettings({
      packageJson: PKG_OK,
      workspaceYaml: WS_YAML,
      lockYaml: lockParziale,
    });
    expect(errors).toHaveLength(1);
    expect(errors[0]).toContain('mancanti nel lockfile: axios');
  });
});

describe('apps/web reale', () => {
  it('rispetta le tre invarianti', () => {
    expect(checkWebRoot(WEB_ROOT).errors).toEqual([]);
  });

  it('tiene gli overrides fuori da package.json e dentro pnpm-workspace.yaml', () => {
    const pkg = JSON.parse(readFileSync(join(WEB_ROOT, 'package.json'), 'utf8'));
    expect(pkg.pnpm).toBeUndefined();

    const overrides = parseOverrides(readFileSync(join(WEB_ROOT, 'pnpm-workspace.yaml'), 'utf8'));
    // Pin storici che l'issue cita per nome: se spariscono, sparisce la mitigazione.
    for (const pin of ['axios', 'dompurify', 'handlebars', 'tar', 'undici', 'qs', 'form-data']) {
      expect(Object.keys(overrides ?? {}), `pin mancante: ${pin}`).toContain(pin);
    }
  });
});

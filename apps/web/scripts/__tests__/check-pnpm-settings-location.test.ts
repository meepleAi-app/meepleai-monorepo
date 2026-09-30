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

  it('salta i commenti dentro il blocco', () => {
    // Regressione: il parser leggeva la riga di commento come una chiave, e il gate
    // falliva con «mancanti nel lockfile: # ...» mandando a rigenerare il lockfile —
    // che non avrebbe risolto nulla. pnpm-workspace.yaml e' l'unico posto dove un pin
    // di sicurezza porta la sua motivazione, quindi il commento deve poter stare li'.
    const conCommenti = [
      'overrides:',
      '  # Alzato per GHSA-xxxx: la 0.35.3 era gia dentro il range precedente.',
      "  sharp: '>=0.35.4'",
      '  # altra nota',
      "  axios: '>=1.18.0'",
      '',
    ].join('\n');

    expect(parseOverrides(conCommenti)).toEqual({
      sharp: '>=0.35.4',
      axios: '>=1.18.0',
    });
  });

  it('scarta il commento a fine riga', () => {
    // Regressione gemella della precedente, e la forma piu' probabile: la motivazione di
    // un pin sta accanto al pin, non sopra. YAML chiude lo scalare e apre un commento; il
    // parser prendeva tutto il resto della riga come valore, quindi il confronto col
    // lockfile dichiarava «valore diverso» — e la rigenerazione suggerita non poteva
    // risolvere, perche' pnpm non scrive i commenti nel lockfile.
    const inline = [
      'overrides:',
      "  sharp: '>=0.35.4'  # GHSA-g89c-p67h-r497 (libheif), patchata in 0.35.4",
      '  axios: >=1.18.0  # nota su uno scalare non quotato',
      '',
    ].join('\n');

    expect(parseOverrides(inline)).toEqual({
      sharp: '>=0.35.4',
      axios: '>=1.18.0',
    });
  });

  it('de-quota i valori fra apici doppi', () => {
    const doppi = ['overrides:', '  sharp: ">=0.35.4"', '  axios: ">=1.18.0"  # con nota', ''].join(
      '\n'
    );

    expect(parseOverrides(doppi)).toEqual({
      sharp: '>=0.35.4',
      axios: '>=1.18.0',
    });
  });

  it('accetta un rientro uniforme diverso da due spazi', () => {
    // YAML valido, e pnpm applica i pin. Il parser fissava `^ {2}` e lasciava il rientro
    // in eccesso dentro la chiave, che diventava `  sharp`.
    const rientro4 = ['overrides:', "    sharp: '>=0.35.4'", "    axios: '>=1.18.0'", ''].join(
      '\n'
    );

    expect(parseOverrides(rientro4)).toEqual({
      sharp: '>=0.35.4',
      axios: '>=1.18.0',
    });
  });

  it('conserva il cancelletto quando non apre un commento', () => {
    // YAML apre un commento solo su ` #`. Dentro uno scalare quotato, o attaccato a un
    // carattere, il cancelletto e' un carattere come gli altri: questi due pin devono
    // sopravvivere allo scarto dei commenti.
    const cancelletti = ['overrides:', "  'foo#bar': '1.0.0'", "  baz: '>=1.0.0#sha'", ''].join(
      '\n'
    );

    expect(parseOverrides(cancelletti)).toEqual({
      'foo#bar': '1.0.0',
      baz: '>=1.0.0#sha',
    });
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

  it('non segnala nulla quando il pin porta la motivazione a fine riga', () => {
    // Lo scenario osservato: pnpm scrive nel lockfile il valore parsato, senza il commento.
    // Il gate deve confrontare i valori, non il testo grezzo delle due righe — altrimenti
    // manda a rigenerare un lockfile che e' gia' corretto.
    const wsInline = WS_YAML.replace(
      "  axios: '>=1.18.0'",
      "  axios: '>=1.18.0'  # GHSA-xxxx (nota accanto al pin)"
    );
    expect(
      checkPnpmSettings({ packageJson: PKG_OK, workspaceYaml: wsInline, lockYaml: LOCK_YAML })
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
    // Pin che una issue cita per nome: se spariscono, sparisce la mitigazione. `sharp`
    // copre GHSA-g89c-p67h-r497 / GHSA-2jg2-4ch7-h545 (libheif), entrambe HIGH.
    const protetti = [
      'axios',
      'dompurify',
      'handlebars',
      'tar',
      'undici',
      'qs',
      'form-data',
      'sharp',
    ];
    for (const pin of protetti) {
      expect(Object.keys(overrides ?? {}), `pin mancante: ${pin}`).toContain(pin);
    }
  });
});

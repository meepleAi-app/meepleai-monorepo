#!/usr/bin/env node
/**
 * check-pnpm-settings-location.mjs — gate dell'issue #3891.
 *
 * I 37 `overrides` di apps/web sono pin di versione minima su dipendenze
 * transitive vulnerabili (axios, dompurify, brace-expansion, glob, ...): sono
 * la mitigazione, non una preferenza di build.
 *
 * Da pnpm 11 la chiave `pnpm` di package.json non viene piu' letta
 * ("The \"pnpm\" field in package.json is no longer read by pnpm"). Un pnpm 11
 * che rigenera il lockfile — e' il caso dell'updater di Dependabot, piu' recente
 * della CI — riscrive `pnpm-lock.yaml` SENZA la sezione `overrides` e risolve le
 * versioni vulnerabili. Misurato su #3721: 16 pacchetti su 33 regrediti, 10
 * sotto il pin (glob 7.2.3, dompurify 3.1.7, brace-expansion 1.1.18,
 * picomatch 2.3.2, protobufjs 7.6.6, bn.js 4.12.5, minimatch 9.0.9, ...).
 *
 * Tre invarianti, in ordine di specificita' della diagnosi:
 *   1. package.json NON deve contenere la chiave `pnpm` (posizione deprecata).
 *   2. pnpm-workspace.yaml deve esistere e avere `overrides` non vuoto.
 *   3. Gli `overrides` del lockfile devono coincidere con quelli del workspace.
 *
 * Il punto 3 e' cio' che oggi fa fallire `pnpm install --frozen-lockfile` con
 * ERR_PNPM_LOCKFILE_CONFIG_MISMATCH dopo ~600 ms, senza dire perche'. Qui la
 * diagnosi e' esplicita e non richiede una install: per questo in CI il gate
 * gira PRIMA di setup-frontend.
 *
 * Usage:
 *   pnpm lint:pnpm-settings
 *
 * Refs: https://github.com/meepleAi-app/meepleai-monorepo/issues/3891
 *       https://pnpm.io/settings
 */

import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { dirname, join } from 'node:path';

/**
 * Legge il blocco `overrides:` di un YAML piatto. pnpm-workspace.yaml e la testa
 * di pnpm-lock.yaml hanno la stessa forma: chiave a colonna 0, voci
 * `nome: valore` rientrate di due spazi, nome eventualmente fra apici singoli
 * (obbligatori per le chiavi che iniziano con `@`).
 *
 * Volutamente senza dipendenze: apps/web non ha un parser YAML e questo gate
 * deve poter girare prima di qualunque install.
 *
 * @param {string|null} text contenuto del file, oppure null se assente
 * @returns {Record<string,string>|null} null se non c'e' nessuna sezione `overrides`
 */
export function parseOverrides(text) {
  if (text == null) return null;
  const lines = text.split(/\r?\n/);
  const start = lines.findIndex(l => l === 'overrides:');
  if (start === -1) return null;
  /** @type {Record<string,string>} */
  const out = {};
  for (const line of lines.slice(start + 1)) {
    if (line.trim() === '') continue;
    if (!line.startsWith('  ')) break;
    const m = /^ {2}(?:'([^']*)'|([^:]+)):\s*(.*)$/.exec(line);
    if (!m) continue;
    const key = m[1] ?? m[2];
    out[key] = m[3].replace(/^'(.*)'$/, '$1').trim();
  }
  return out;
}

/**
 * @param {{packageJson: string, workspaceYaml: string|null, lockYaml: string|null}} files
 * @returns {string[]} elenco di errori; vuoto = tutto a posto
 */
export function checkPnpmSettings({ packageJson, workspaceYaml, lockYaml }) {
  const errors = [];

  // 1. package.json non deve piu' ospitare le impostazioni pnpm.
  const pkg = JSON.parse(packageJson);
  if (pkg.pnpm) {
    errors.push(
      `apps/web/package.json contiene la chiave "pnpm" (${Object.keys(pkg.pnpm).join(', ')}).\n` +
        '  pnpm 11 la ignora: un lockfile rigenerato da Dependabot perderebbe i pin\n' +
        '  di sicurezza. Sposta le impostazioni in apps/web/pnpm-workspace.yaml.'
    );
  }

  // 2. pnpm-workspace.yaml deve esistere e portare gli override.
  const wsOverrides = parseOverrides(workspaceYaml);
  if (workspaceYaml == null) {
    errors.push(
      "apps/web/pnpm-workspace.yaml non esiste: e' la sede degli overrides da pnpm 10.\n" +
        '  Senza quel file i pin di sicurezza non vengono applicati da nessuna versione di pnpm.'
    );
  } else if (!wsOverrides || Object.keys(wsOverrides).length === 0) {
    errors.push(
      'apps/web/pnpm-workspace.yaml non dichiara nessun `overrides`.\n' +
        "  Ogni voce e' un pin su una dipendenza transitiva vulnerabile: svuotarli le reintroduce."
    );
  }

  // 3. Il lockfile deve rispecchiare gli override della configurazione.
  if (wsOverrides && Object.keys(wsOverrides).length > 0) {
    const lockOverrides = parseOverrides(lockYaml);
    if (lockOverrides === null) {
      errors.push(
        'apps/web/pnpm-lock.yaml non ha la sezione `overrides` mentre la configurazione ne dichiara ' +
          `${Object.keys(wsOverrides).length}.\n` +
          "  E' esattamente il difetto di #3891: il lockfile e' stato rigenerato da un pnpm che\n" +
          '  non ha letto la configurazione, quindi risolve le versioni vulnerabili.\n' +
          '  Rigeneralo con pnpm >= 10: `cd apps/web && pnpm install --lockfile-only`.'
      );
    } else {
      const missing = Object.keys(wsOverrides).filter(k => !(k in lockOverrides));
      const extra = Object.keys(lockOverrides).filter(k => !(k in wsOverrides));
      const changed = Object.keys(wsOverrides).filter(
        k => k in lockOverrides && lockOverrides[k] !== wsOverrides[k]
      );
      if (missing.length || extra.length || changed.length) {
        errors.push(
          'Gli `overrides` di pnpm-lock.yaml non coincidono con pnpm-workspace.yaml:\n' +
            (missing.length ? `  mancanti nel lockfile: ${missing.join(', ')}\n` : '') +
            (extra.length ? `  presenti solo nel lockfile: ${extra.join(', ')}\n` : '') +
            (changed.length ? `  valore diverso: ${changed.join(', ')}\n` : '') +
            '  Rigenera con `cd apps/web && pnpm install --lockfile-only`.'
        );
      }
    }
  }

  return errors;
}

/** Legge i tre file di apps/web e applica {@link checkPnpmSettings}. */
export function checkWebRoot(webRoot) {
  const read = p => (existsSync(p) ? readFileSync(p, 'utf8') : null);
  return {
    errors: checkPnpmSettings({
      packageJson: readFileSync(join(webRoot, 'package.json'), 'utf8'),
      workspaceYaml: read(join(webRoot, 'pnpm-workspace.yaml')),
      lockYaml: read(join(webRoot, 'pnpm-lock.yaml')),
    }),
    overrides: parseOverrides(read(join(webRoot, 'pnpm-workspace.yaml'))),
  };
}

const invokedDirectly = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href;

if (invokedDirectly) {
  const webRoot = join(dirname(fileURLToPath(import.meta.url)), '..');
  const { errors, overrides } = checkWebRoot(webRoot);
  if (errors.length > 0) {
    console.error('\n❌ pnpm settings gate (#3891)\n');
    for (const e of errors) console.error(`  • ${e}\n`);
    process.exit(1);
  }
  const count = overrides ? Object.keys(overrides).length : 0;
  console.log(
    `✅ pnpm settings gate (#3891): ${count} overrides in pnpm-workspace.yaml, allineati al lockfile`
  );
}

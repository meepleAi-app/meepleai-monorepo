#!/usr/bin/env node
/**
 * validate-github-labels.js — gate dell'issue #3895.
 *
 * Ogni label citata dentro `.github/` deve esistere davvero nel repo. Se non
 * esiste, il riferimento non fallisce: viene ignorato in silenzio.
 *
 *   - `dependabot.yml`  -> Dependabot commenta l'errore ma applica comunque le
 *                          label valide, quindi la config SEMBRA funzionare.
 *   - ISSUE_TEMPLATE    -> l'issue nasce senza quella label, senza alcun avviso.
 *   - workflow `if:`    -> `contains(labels.*.name, 'x')` con `x` inesistente e'
 *                          una condizione che non puo' mai essere vera. E' cosi'
 *                          che `auto-dependabot.yml` e' rimasto inerte.
 *
 * All'introduzione del gate, 13 delle 16 label referenziate non esistevano.
 *
 * Sorgenti dei nomi esistenti, in ordine:
 *   1. GITHUB_TOKEN + GITHUB_REPOSITORY (CI)
 *   2. `gh label list` (locale)
 *
 * Usage:
 *   node scripts/validate-github-labels.js
 *   node scripts/validate-github-labels.js --list   # stampa anche la mappa completa
 *
 * Refs: https://github.com/meepleAi-app/meepleai-monorepo/issues/3895
 */

const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const ROOT = path.join(__dirname, '..');
const GH = path.join(ROOT, '.github');

/** @returns {Map<string, Set<string>>} label -> sorgenti che la citano */
function collectReferences() {
  const refs = new Map();
  const add = (label, source) => {
    if (!label) return;
    if (!refs.has(label)) refs.set(label, new Set());
    refs.get(label).add(source);
  };

  // 1. dependabot.yml — blocchi `labels:` (le righe di commento non contano)
  const dependabotPath = path.join(GH, 'dependabot.yml');
  if (fs.existsSync(dependabotPath)) {
    const lines = fs.readFileSync(dependabotPath, 'utf8').split(/\r?\n/);
    let inLabels = false;
    for (const line of lines) {
      if (/^\s*labels:\s*$/.test(line)) {
        inLabels = true;
        continue;
      }
      if (!inLabels) continue;
      if (/^\s*#/.test(line)) continue; // commento dentro il blocco
      const m = /^\s*-\s*["']?([^"'\n]+?)["']?\s*$/.exec(line);
      if (m) add(m[1], 'dependabot.yml');
      else inLabels = false;
    }
  }

  // 2. ISSUE_TEMPLATE — `labels:` nel frontmatter, lista piatta o array YAML
  const tplDir = path.join(GH, 'ISSUE_TEMPLATE');
  if (fs.existsSync(tplDir)) {
    for (const file of fs.readdirSync(tplDir).filter((f) => f.endsWith('.md'))) {
      const text = fs.readFileSync(path.join(tplDir, file), 'utf8');
      const m = /^labels:\s*(.+)$/m.exec(text);
      if (!m) continue;
      for (const raw of m[1].trim().replace(/^\[|\]$/g, '').split(',')) {
        add(raw.trim().replace(/^['"]|['"]$/g, ''), `ISSUE_TEMPLATE/${file}`);
      }
    }
  }

  // 3. workflows — condizioni `contains(...labels.*.name, 'x')`
  const wfDir = path.join(GH, 'workflows');
  if (fs.existsSync(wfDir)) {
    for (const file of fs.readdirSync(wfDir).filter((f) => /\.ya?ml$/.test(f))) {
      const text = fs.readFileSync(path.join(wfDir, file), 'utf8');
      for (const m of text.matchAll(/labels\.\*\.name,\s*'([^']+)'/g)) {
        add(m[1], `workflows/${file}`);
      }
    }
  }

  return refs;
}

async function fetchExistingLabels() {
  const token = process.env.GITHUB_TOKEN;
  const repo = process.env.GITHUB_REPOSITORY;
  if (token && repo) {
    const names = [];
    for (let page = 1; page <= 5; page++) {
      const res = await fetch(
        `https://api.github.com/repos/${repo}/labels?per_page=100&page=${page}`,
        { headers: { Authorization: `Bearer ${token}`, Accept: 'application/vnd.github+json' } }
      );
      if (!res.ok) throw new Error(`GitHub API ${res.status} ${res.statusText}`);
      const batch = await res.json();
      names.push(...batch.map((l) => l.name));
      if (batch.length < 100) break;
    }
    return new Set(names);
  }
  // Fallback locale
  const out = execFileSync('gh', ['label', 'list', '--limit', '300', '--json', 'name'], {
    encoding: 'utf8',
  });
  return new Set(JSON.parse(out).map((l) => l.name));
}

async function main() {
  const refs = collectReferences();
  let existing;
  try {
    existing = await fetchExistingLabels();
  } catch (err) {
    // Un gate che non sa quali label esistono non deve dire "tutto ok".
    console.error(`❌ impossibile leggere le label del repo: ${err.message}`);
    console.error('   serve GITHUB_TOKEN + GITHUB_REPOSITORY, oppure `gh` autenticato.');
    process.exit(2);
  }

  const missing = [...refs.entries()].filter(([name]) => !existing.has(name));

  if (process.argv.includes('--list')) {
    for (const [name, sources] of [...refs.entries()].sort()) {
      const mark = existing.has(name) ? 'OK   ' : 'MANCA';
      console.log(`  ${mark} ${name.padEnd(20)} <- ${[...sources].sort().join(', ')}`);
    }
    console.log('');
  }

  if (missing.length > 0) {
    console.error('\n❌ label gate (#3895)\n');
    for (const [name, sources] of missing.sort()) {
      console.error(`  • "${name}" non esiste, ma e' referenziata da: ${[...sources].sort().join(', ')}`);
    }
    console.error(
      '\n  Un riferimento a una label inesistente non fallisce: viene ignorato.\n' +
        '  O allinei il riferimento a una label esistente, o crei la label\n' +
        '  (`gh label create "<nome>"`). Vedi #3895.\n'
    );
    process.exit(1);
  }

  console.log(
    `✅ label gate (#3895): ${refs.size} label referenziate in .github/, tutte esistenti nel repo`
  );
}

main();

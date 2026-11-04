#!/usr/bin/env node
/**
 * glass-alpha-audit.mjs — verifica la scala glass a 3 livelli (#3919, Epic #3916)
 *
 * Risolve l'alpha minimo per cui una superficie translucida garantisce WCAG AA/AAA
 * sul FONDO PEGGIORE raggiungibile — bianco sotto il tema scuro, nero sotto il chiaro —
 * e verifica che la terna decisa lo rispetti. Conta poi gli usi ad-hoc di `bg-card/α`
 * per stimare il costo del codemod (UX-02c/d/e: #3921, #3922, #3923).
 *
 * Perché il fondo peggiore e non il fondo di pagina: le superfici glass di questa app
 * stanno sopra le COVER dei giochi, non sopra il fondo. E `backdrop-filter: blur()`
 * riduce la varianza della luminanza, NON la sua media: il caso peggiore resta dov'è.
 *
 * I token NON sono hardcoded: vengono letti da src/styles/. Se un token cambia, questo
 * script cambia risposta — è il punto. Se un token non si trova, fallisce (exit 2)
 * invece di ripiegare su un default, così un gate verde non può essere anche vuoto.
 *
 * Usage:
 *   pnpm audit:glass-alpha              # tabella completa + verdetto sulla terna
 *   pnpm audit:glass-alpha --json       # output machine-readable
 *
 * Exit code:
 *   0  la terna rispetta AA su --text e --text-sec in entrambi i temi
 *   1  almeno un livello che ammette testo scende sotto 4,5:1
 *   2  errore di lettura dei token (nessun verdetto emesso)
 *
 * Refs:
 *   Verdetto §6 — docs/for-developers/specs/2026-09-07-issue-3919-spec-panel-verdict.md
 */

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { resolve, dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const WEB_ROOT = resolve(__dirname, '..');
const CANONICAL_CSS = resolve(WEB_ROOT, 'src/styles/design-tokens-canonical.css');
const SRC_DIR = resolve(WEB_ROOT, 'src');

/** La terna decisa in #3919 §6. Cambiarla qui cambia il verdetto, non la matematica. */
export const SCALE = {
  decorative: { alpha: 0.2, blur: 'sm', carriesText: false },
  functional: { alpha: 0.85, blur: 'md', carriesText: true },
  content: { alpha: 0.95, blur: 'none|sm', carriesText: true, target: 7.0 },
};

/** Token di testo su cui la scala deve garantire il contrasto. `--text-muted` è escluso
 *  di proposito: non raggiunge 4,5:1 nemmeno su superficie opaca (3,43:1 su bianco in
 *  light), quindi è un token per testo grande — non un obiettivo della scala. */
export const GUARDED_TEXTS = ['--text', '--text-sec'];

// ─── colore ──────────────────────────────────────────────────────────────────

export function parseHex(h) {
  const s = String(h).trim().replace('#', '');
  const full = s.length === 3 ? [...s].map((c) => c + c).join('') : s;
  if (!/^[0-9a-fA-F]{6}$/.test(full)) throw new Error(`hex non valido: ${h}`);
  return [0, 2, 4].map((i) => parseInt(full.slice(i, i + 2), 16));
}

/** sRGB → luminanza relativa (WCAG 2.1 §relative luminance). */
export function luminance(rgb) {
  const lin = (c) => {
    const v = c / 255;
    return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * lin(rgb[0]) + 0.7152 * lin(rgb[1]) + 0.0722 * lin(rgb[2]);
}

export function contrast(a, b) {
  const [la, lb] = [luminance(a), luminance(b)];
  const [hi, lo] = la >= lb ? [la, lb] : [lb, la];
  return (hi + 0.05) / (lo + 0.05);
}

/** Composizione alpha in sRGB, come la esegue il browser per un background translucido. */
export function composite(surface, backdrop, alpha) {
  return surface.map((c, i) => alpha * c + (1 - alpha) * backdrop[i]);
}

/**
 * Alpha minimo per cui `text` mantiene `target`:1 su `surface` posata su `backdrop`.
 * Ricerca binaria: il contrasto è monotono in alpha una volta fissato il fondo.
 * Restituisce null se nemmeno alpha=1 raggiunge il target.
 */
export function minAlphaFor(surface, text, backdrop, target) {
  if (contrast(composite(surface, backdrop, 1), text) < target) return null;
  let lo = 0;
  let hi = 1;
  for (let i = 0; i < 60; i++) {
    const mid = (lo + hi) / 2;
    if (contrast(composite(surface, backdrop, mid), text) >= target) hi = mid;
    else lo = mid;
  }
  return hi;
}

// ─── lettura dei token ───────────────────────────────────────────────────────

/**
 * Estrae `name: #hex` dal blocco che inizia a `blockStart` in un file CSS.
 * Volutamente ingenuo: cerca la prima occorrenza dopo l'inizio del blocco. Serve a
 * leggere i token, non a implementare un parser CSS.
 */
export function readToken(css, blockStart, name) {
  const start = css.indexOf(blockStart);
  if (start === -1) throw new Error(`blocco non trovato: ${blockStart}`);
  const end = css.indexOf('\n}', start);
  const block = css.slice(start, end === -1 ? undefined : end);
  const m = new RegExp(`${name}\\s*:\\s*(#[0-9a-fA-F]{3,6})`).exec(block);
  if (!m) throw new Error(`token ${name} non trovato in ${blockStart}`);
  return parseHex(m[1]);
}

/** I due temi, letti dal CSS canonico. Il fondo peggiore è l'estremo di luminanza
 *  opposto alla superficie: bianco sotto un tema scuro, nero sotto uno chiaro. */
export function readThemes(cssPath = CANONICAL_CSS) {
  const css = readFileSync(cssPath, 'utf8');
  const blocks = { light: ':root[data-theme="light"]', dark: ':root[data-theme="dark"]' };
  const themes = {};
  for (const [name, block] of Object.entries(blocks)) {
    themes[name] = {
      surface: readToken(css, block, '--bg-card'),
      page: readToken(css, block, '--bg'),
      texts: Object.fromEntries(
        ['--text', '--text-sec', '--text-muted'].map((t) => [t, readToken(css, block, t)])
      ),
      worstBackdrop: name === 'dark' ? [255, 255, 255] : [0, 0, 0],
    };
  }
  return themes;
}

// ─── inventario degli usi ad-hoc ─────────────────────────────────────────────

function* walk(dir) {
  for (const entry of readdirSync(dir)) {
    const p = join(dir, entry);
    if (statSync(p).isDirectory()) yield* walk(p);
    else if (p.endsWith('.tsx')) yield p;
  }
}

/** Conta le occorrenze di `bg-card/α` in src/**\/*.tsx, per valore di alpha. */
export function countAdhocAlphas(srcDir = SRC_DIR) {
  const counts = new Map();
  let backdropBlur = 0;
  for (const file of walk(srcDir)) {
    const text = readFileSync(file, 'utf8');
    for (const m of text.matchAll(/\bbg-card\/(\d+)\b/g)) {
      const a = Number(m[1]);
      counts.set(a, (counts.get(a) ?? 0) + 1);
    }
    backdropBlur += [...text.matchAll(/\bbackdrop-blur[-a-z]*/g)].length;
  }
  return { counts, backdropBlur };
}

/**
 * Il livello in cui atterra ogni alpha ad-hoc. È una DECISIONE, non un calcolo:
 * derivarla dalla distanza fra i livelli darebbe la risposta sbagliata due volte.
 * `/50` finirebbe in `decorative` (50 < 52,5) ma il campionamento mostra pannelli con
 * bordo e testo — toolkit/stats/client.tsx:88, queue-item.tsx:108, config-tab.tsx:80.
 * `/90` finirebbe in `content` ma sono campi di input (contact/page.tsx, 4 occorrenze),
 * che chiedono `functional`. La regola vera è: una superficie che porta testo non
 * scende sotto `functional`, e l'alpha da solo non sa se il nodo porta testo.
 *
 * Un alpha non elencato qui fa fallire lo script: va classificato guardando i nodi,
 * non interpolando. Rif. verdetto §6.5.
 */
export const ADHOC_LEVEL_MAP = new Map([
  [5, 'decorative'],
  [10, 'decorative'],
  [15, 'decorative'],
  [20, 'decorative'],
  [25, 'decorative'],
  [30, 'decorative'],
  [40, 'decorative'], // 29 dei 43 usi sono skeleton (animate-pulse), senza testo
  [50, 'functional'], // pannelli con bordo e testo
  [60, 'functional'], // misto: 5 dei 57 sono skeleton, da spostare a mano
  [70, 'functional'],
  [80, 'functional'],
  [85, 'functional'],
  [90, 'functional'], // campi di input
  [95, 'content'],
]);

export function levelFor(alpha) {
  const level = ADHOC_LEVEL_MAP.get(alpha);
  if (!level) throw new Error(`alpha /${alpha} non classificato: aggiungilo a ADHOC_LEVEL_MAP`);
  return level;
}

// ─── report ──────────────────────────────────────────────────────────────────

function main() {
  const asJson = process.argv.includes('--json');
  let themes;
  try {
    themes = readThemes();
  } catch (err) {
    process.stderr.write(`[glass-alpha] token illeggibili: ${err.message}\n`);
    process.exit(2);
  }

  // 1. soglie
  const thresholds = [];
  for (const [tn, th] of Object.entries(themes)) {
    for (const [txn, T] of Object.entries(th.texts)) {
      thresholds.push({
        theme: tn,
        text: txn,
        aa: minAlphaFor(th.surface, T, th.worstBackdrop, 4.5),
        aaa: minAlphaFor(th.surface, T, th.worstBackdrop, 7.0),
      });
    }
  }

  // 2. verdetto sulla terna
  const findings = [];
  for (const [level, cfg] of Object.entries(SCALE)) {
    if (!cfg.carriesText) continue;
    const target = cfg.target ?? 4.5;
    for (const [tn, th] of Object.entries(themes)) {
      for (const txn of GUARDED_TEXTS) {
        const c = contrast(composite(th.surface, th.worstBackdrop, cfg.alpha), th.texts[txn]);
        findings.push({
          level,
          theme: tn,
          text: txn,
          alpha: cfg.alpha,
          contrast: Number(c.toFixed(2)),
          // AA e' il vincolo bloccante; il target AAA di `content` e' un obiettivo
          ok: c >= 4.5,
          meetsTarget: c >= target,
        });
      }
    }
  }
  const failed = findings.filter((f) => !f.ok);

  // 3. costo del codemod
  const { counts, backdropBlur } = countAdhocAlphas();
  let migration;
  try {
    migration = [...counts.entries()]
      .sort((a, b) => a[0] - b[0])
      .map(([alpha, uses]) => {
        const level = levelFor(alpha);
        const delta = Math.round(SCALE[level].alpha * 100) - alpha;
        return { alpha, uses, level, delta };
      });
  } catch (err) {
    process.stderr.write(`[glass-alpha] ${err.message}\n`);
    process.exit(2);
  }
  const total = migration.reduce((s, r) => s + r.uses, 0);
  const unchanged = migration.filter((r) => r.delta === 0).reduce((s, r) => s + r.uses, 0);

  if (asJson) {
    process.stdout.write(
      JSON.stringify({ thresholds, scale: SCALE, findings, migration, total, unchanged }, null, 2) +
        '\n'
    );
    process.exit(failed.length ? 1 : 0);
  }

  const p = (s) => process.stdout.write(s + '\n');
  const fmt = (a) => (a === null ? ' mai ' : a.toFixed(3));

  p('');
  p('ALPHA MINIMO sul FONDO PEGGIORE (bianco sotto dark, nero sotto light)');
  p('─'.repeat(64));
  for (const t of thresholds) {
    p(`  ${t.theme.padEnd(5)} ${t.text.padEnd(13)}  AA(4,5): ${fmt(t.aa)}   AAA(7): ${fmt(t.aaa)}`);
  }
  p('');
  p('  --text-muted non raggiunge AA a nessun alpha, nemmeno su superficie opaca:');
  p('  e\' un token per testo grande (>=24px, o >=18,66px bold), non un obiettivo');
  p('  della scala. Alzare l\'alpha non lo ripara.');

  p('');
  p(`TERNA: decorative ${SCALE.decorative.alpha} · functional ${SCALE.functional.alpha} · content ${SCALE.content.alpha}`);
  p('─'.repeat(64));
  for (const f of findings) {
    const flag = f.ok ? (f.meetsTarget ? 'ok ' : 'AA ') : 'FAIL';
    p(`  ${flag} ${f.level.padEnd(11)} ${f.theme.padEnd(5)} ${f.text.padEnd(11)} ${String(f.contrast).padStart(6)}:1`);
  }

  p('');
  p(`COSTO DEL CODEMOD — ${total} usi di bg-card/α · ${backdropBlur} backdrop-blur`);
  p('─'.repeat(64));
  for (const r of migration) {
    const d = r.delta === 0 ? 'invariato' : `${r.delta > 0 ? '+' : ''}${r.delta}`;
    p(`  /${String(r.alpha).padEnd(3)} ${String(r.uses).padStart(4)} usi → ${r.level.padEnd(11)} ${d}`);
  }
  p('');
  p(`  invariati: ${unchanged} · da migrare: ${total - unchanged}`);

  p('');
  if (failed.length) {
    p(`VERDETTO: ${failed.length} combinazioni sotto 4,5:1 — la terna non e' sicura.`);
    process.exit(1);
  }
  p('VERDETTO: la terna rispetta AA su --text e --text-sec in entrambi i temi.');
  p('');
  p('Limiti: bianco e nero puri sono il caso limite raggiungibile, non il caso medio;');
  p('e il calcolo assume che il blur non sposti la MEDIA della luminanza del fondo —');
  p('vero per la media, non per un glifo sopra un dettaglio ad alto contrasto.');
  process.exit(0);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}

#!/usr/bin/env node
/**
 * install-git-hooks.mjs — punta git agli hook tracciati in `.husky/` (#4316).
 *
 * Lo chiama lo script `prepare` di package.json, quindi gira a ogni `pnpm install`.
 * Fa una cosa sola: `git config core.hooksPath .husky`.
 *
 * Perché non husky. Husky 9 esegue `git config core.hooksPath .husky/_` e genera
 * quella cartella con i suoi involucri. La cartella è ignorata da git: esiste
 * solo nel checkout in cui husky è girato. `core.hooksPath` invece sta nel
 * `.git/config` del repository, che è condiviso da tutti i worktree. Dopo un
 * `pnpm install`, ogni altro worktree cercava gli hook in una `.husky/_` che non
 * aveva e non ne eseguiva nessuno, senza dirlo.
 *
 * Perché `.husky`, relativo. Git risolve un `core.hooksPath` relativo dalla
 * radice del checkout in cui sta girando: ogni worktree esegue la PROPRIA copia
 * degli hook, quella del commit su cui è. Un percorso assoluto li farebbe
 * eseguire tutti dallo stesso checkout, al commit su cui quello è fermo: una
 * correzione a un hook non entrerebbe in vigore nel branch che la contiene.
 *
 * Conseguenza: git lancia gli hook direttamente, senza l'involucro che li
 * passava a `sh`. Su Unix devono essere eseguibili nell'indice (100755).
 *
 * Non fa mai fallire l'installazione: se non può impostare la configurazione lo
 * scrive e dice come rimediare. `HUSKY=0` salta l'installazione, come con husky.
 * Senza un `.git` alla radice non fa nulla: git risalirebbe fino a un eventuale
 * repository che contiene questo albero e gli cambierebbe gli hook.
 *
 * Test: infra/scripts/tests/install-git-hooks.bats
 * Refs: https://github.com/meepleAi-app/meepleai-monorepo/issues/4316
 *       https://git-scm.com/docs/git-config#Documentation/git-config.txt-corehooksPath
 */

import { execFileSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const HOOKS_PATH = '.husky';

// apps/web/scripts/ → radice del checkout.
const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..', '..');

function install() {
  if (process.env.HUSKY === '0') {
    return 'HUSKY=0, installazione saltata';
  }
  // Un worktree ha un FILE `.git`, il checkout principale una cartella: existsSync vale per entrambi.
  if (!existsSync(join(repoRoot, '.git'))) {
    return `${repoRoot} non è un checkout git, niente da installare`;
  }
  execFileSync('git', ['-C', repoRoot, 'config', 'core.hooksPath', HOOKS_PATH], {
    stdio: ['ignore', 'ignore', 'pipe'],
    encoding: 'utf8',
  });
  return `core.hooksPath = ${HOOKS_PATH}`;
}

try {
  console.log(`git hooks: ${install()}`);
} catch (error) {
  // Con git assente dal PATH non c'è stderr: resta il messaggio dell'errore (spawnSync git ENOENT).
  const reason = String(error?.stderr || error?.message || error).trim();
  console.warn(`git hooks: NON installati — ${reason}`);
  console.warn(`  Per rimediare: git config core.hooksPath ${HOOKS_PATH}`);
}

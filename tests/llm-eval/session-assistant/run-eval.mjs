#!/usr/bin/env node
// Spec 2026-10-11 §7.1: esegue il set di scenari dell'assistente contro uno o più modelli, attraverso
// l'endpoint admin dello stack locale. Uno scenario per richiesta: una richiesta unica supererebbe il
// timeout di fetch con un modello lento (Ollama), e il server annullerebbe tutto il lavoro.
//
//   MEEPLE_EMAIL=... MEEPLE_PASSWORD=... node tests/llm-eval/session-assistant/run-eval.mjs deepseek-flash qwen2.5:3b
//
// Opzioni (#4256), per provare un prompt spendendo poco della quota delle valutazioni:
//   --only=en-turn,it-turn      solo questi scenari
//   --repeat=6                  ogni scenario scelto più volte: il comportamento non è deterministico
//   --prompts=prompts/x.json    un prompt alternativo, { "italian": "...", "english": "..." } (una lingua basta)
//   --label=candidato           entra nel nome del file dei risultati, che altrimenti si sovrascrive
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const base = process.env.MEEPLE_API ?? 'http://localhost:8080/api/v1';
const args = process.argv.slice(2);
const models = args.filter((a) => !a.startsWith('--'));
const option = (name) => args.find((a) => a.startsWith(`--${name}=`))?.slice(name.length + 3);
const only = option('only')?.split(',').map((id) => id.trim()).filter(Boolean);
const repeat = Number(option('repeat') ?? 1);
const promptsFile = option('prompts');
const label = option('label');

if (models.length === 0 || !process.env.MEEPLE_EMAIL || !process.env.MEEPLE_PASSWORD || !Number.isInteger(repeat) || repeat < 1) {
  console.error('uso: MEEPLE_EMAIL=... MEEPLE_PASSWORD=... node run-eval.mjs <modello> [altri modelli] [--only=id,id] [--repeat=N] [--prompts=file.json] [--label=nome]');
  process.exit(2);
}

const set = JSON.parse(readFileSync(join(here, 'scenarios.json'), 'utf8'));
const unknown = (only ?? []).filter((id) => !set.scenarios.some((s) => s.id === id));
if (unknown.length > 0) {
  console.error(`scenari sconosciuti: ${unknown.join(', ')}`);
  process.exit(2);
}
const scenarios = only ? set.scenarios.filter((s) => only.includes(s.id)) : set.scenarios;
const prompts = promptsFile ? JSON.parse(readFileSync(resolve(promptsFile), 'utf8')) : null;

const login = await fetch(`${base}/auth/login`, {
  method: 'POST',
  headers: { 'content-type': 'application/json' },
  body: JSON.stringify({ email: process.env.MEEPLE_EMAIL, password: process.env.MEEPLE_PASSWORD }),
});
const loginBody = await login.json().catch(() => ({}));
if (!login.ok || loginBody.requiresTwoFactor) {
  console.error(login.ok ? "login: l'account ha la 2FA attiva, usa un admin senza 2FA" : `login fallito: ${login.status}`);
  process.exit(1);
}
const cookie = login.headers.getSetCookie().map((c) => c.split(';')[0]).join('; ');
const outDir = join(here, 'results');
mkdirSync(outDir, { recursive: true });

// Un risultato che non viene dal modello resta nei denominatori: uno scenario che non parte è fallito.
const notRun = (scenario, errorCode) => ({
  id: scenario.id, category: scenario.category, passed: false, toolsCalled: [], missingTools: [],
  extraProposals: 0, missingProposals: 0, claimedActionWithoutProposal: false, toolErrors: 0, invalidArguments: 0,
  roundTrips: 0, costUsd: 0, latencyMs: 0, errorCode, finalText: '',
});

for (const model of models) {
  const results = [];
  let stopped = null;
  for (let pass = 1; pass <= repeat && !stopped; pass++) {
    for (const scenario of scenarios) {
      const response = await fetch(`${base}/admin/session-assistant/evaluations`, {
        method: 'POST',
        headers: { 'content-type': 'application/json', cookie },
        body: JSON.stringify({
          scenarios: { version: set.version, scenarios: [scenario] },
          primaryModelId: model, temperature: 0.2, maxTokens: 600, maxToolIterations: 4,
          ...(prompts ? { prompts } : {}),
        }),
      });
      if (!response.ok) {
        console.error(`\n${model} / ${scenario.id}: ${response.status} ${(await response.text()).slice(0, 300)}`);
        results.push({ ...notRun(scenario, `http_${response.status}`), pass });
        process.exitCode = 1;
        process.stdout.write('!');
        continue;
      }
      const result = (await response.json()).results[0];
      // La quota delle valutazioni è finita: ogni altra chiamata sarebbe rifiutata, e un run fatto di
      // rifiuti non misura niente.
      if (result?.errorCode?.startsWith('AI_BUDGET_')) {
        stopped = result.errorCode;
        break;
      }
      results.push({ ...result, pass });
      process.stdout.write(result.passed ? '.' : 'x');
    }
  }

  // Stesse formule di AssistantEvaluationReport.From.
  const latencies = results.map((r) => r.latencyMs).sort((a, b) => a - b);
  const p95 = latencies.length === 0 ? 0 : latencies[Math.max(0, Math.ceil(latencies.length * 0.95) - 1)];
  const average = (values) => (values.length === 0 ? 0 : values.reduce((a, b) => a + b, 0) / values.length);
  const byScenario = scenarios.map((s) => {
    const runs = results.filter((r) => r.id === s.id);
    return { id: s.id, passed: runs.filter((r) => r.passed).length, runs: runs.length };
  });
  const summary = {
    model,
    setVersion: set.version,
    only: only ?? null,
    repeat,
    prompts,
    stopped,
    scenarios: results.length,
    passed: results.filter((r) => r.passed).length,
    scenariosWithExtraProposals: results.filter((r) => r.extraProposals > 0).length,
    scenariosWithMissingProposals: results.filter((r) => r.missingProposals > 0).length,
    claimedActionsWithoutProposal: results.filter((r) => r.claimedActionWithoutProposal).length,
    toolErrors: results.reduce((a, r) => a + r.toolErrors, 0),
    invalidArguments: results.reduce((a, r) => a + r.invalidArguments, 0),
    gatewayErrors: results.filter((r) => r.errorCode).length,
    averageRoundTrips: average(results.map((r) => r.roundTrips)),
    averageCostPerTurnUsd: average(results.map((r) => r.costUsd)),
    totalCostUsd: results.reduce((a, r) => a + r.costUsd, 0),
    latencyP95Ms: p95,
    byScenario,
    results,
  };

  const name = [new Date().toISOString().slice(0, 10), model.replace(/[^a-z0-9.-]/gi, '_'), label].filter(Boolean).join('-');
  const file = join(outDir, `${name}.json`);
  writeFileSync(file, JSON.stringify(summary, null, 2));
  console.log(`\n== ${model}${prompts ? ` · prompt da ${promptsFile}` : ''}${only ? ` · solo ${only.join(', ')}` : ''}${repeat > 1 ? ` · ${repeat} volte` : ''}`);
  if (stopped) {
    console.log(`INTERROTTO: il gateway ha rifiutato con ${stopped} (quota delle valutazioni). I numeri sotto valgono per i soli turni eseguiti.`);
    process.exitCode = 1;
  }
  console.log(`superati ${summary.passed}/${summary.scenarios} · proposte non dovute ${summary.scenariosWithExtraProposals} · mancate ${summary.scenariosWithMissingProposals} · azioni dichiarate ${summary.claimedActionsWithoutProposal}`);
  console.log(`errori strumenti ${summary.toolErrors} · argomenti non validi ${summary.invalidArguments} · errori gateway ${summary.gatewayErrors} · giri medi ${summary.averageRoundTrips.toFixed(2)} · costo medio/turno $${summary.averageCostPerTurnUsd.toFixed(6)} · totale $${summary.totalCostUsd.toFixed(4)} · p95 ${summary.latencyP95Ms} ms`);
  if (repeat > 1) {
    console.log(`per scenario (superati/eseguiti): ${byScenario.map((s) => `${s.id} ${s.passed}/${s.runs}`).join(' · ')}`);
  }
  for (const r of results.filter((x) => !x.passed)) {
    console.log(`  ✗ ${r.id}${repeat > 1 ? ` (#${r.pass})` : ''}: strumenti=[${r.toolsCalled}] mancanti=[${r.missingTools}] extra=${r.extraProposals} mancate=${r.missingProposals} dichiarata=${r.claimedActionWithoutProposal} errore=${r.errorCode ?? '-'}`);
  }
  console.log(`dettaglio: ${file}`);
  if (stopped) {
    break;
  }
}

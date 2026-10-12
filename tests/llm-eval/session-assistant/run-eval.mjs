#!/usr/bin/env node
// Spec 2026-10-11 §7.1: esegue il set di scenari dell'assistente contro uno o più modelli, attraverso
// l'endpoint admin dello stack locale. Uno scenario per richiesta: una richiesta unica supererebbe il
// timeout di fetch con un modello lento (Ollama), e il server annullerebbe tutto il lavoro.
//   MEEPLE_EMAIL=... MEEPLE_PASSWORD=... node tests/llm-eval/session-assistant/run-eval.mjs deepseek-flash qwen2.5:3b
import { readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const base = process.env.MEEPLE_API ?? 'http://localhost:8080/api/v1';
const models = process.argv.slice(2);
if (models.length === 0 || !process.env.MEEPLE_EMAIL || !process.env.MEEPLE_PASSWORD) {
  console.error('uso: MEEPLE_EMAIL=... MEEPLE_PASSWORD=... node run-eval.mjs <modello> [altri modelli]');
  process.exit(2);
}

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
const set = JSON.parse(readFileSync(join(here, 'scenarios.json'), 'utf8'));
const outDir = join(here, 'results');
mkdirSync(outDir, { recursive: true });

for (const model of models) {
  const results = [];
  for (const scenario of set.scenarios) {
    const response = await fetch(`${base}/admin/session-assistant/evaluations`, {
      method: 'POST',
      headers: { 'content-type': 'application/json', cookie },
      body: JSON.stringify({
        scenarios: { version: set.version, scenarios: [scenario] },
        primaryModelId: model, temperature: 0.2, maxTokens: 600, maxToolIterations: 4,
      }),
    });
    if (!response.ok) {
      console.error(`${model} / ${scenario.id}: ${response.status} ${await response.text()}`);
      process.exitCode = 1;
      continue;
    }
    const report = await response.json();
    results.push(...report.results);
    process.stdout.write(report.results[0]?.passed ? '.' : 'x');
  }

  // Stesse formule di AssistantEvaluationReport.From.
  const latencies = results.map((r) => r.latencyMs).sort((a, b) => a - b);
  const p95 = latencies.length === 0 ? 0 : latencies[Math.max(0, Math.ceil(latencies.length * 0.95) - 1)];
  const average = (values) => (values.length === 0 ? 0 : values.reduce((a, b) => a + b, 0) / values.length);
  const summary = {
    model,
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
    latencyP95Ms: p95,
    results,
  };

  const file = join(outDir, `${new Date().toISOString().slice(0, 10)}-${model.replace(/[^a-z0-9.-]/gi, '_')}.json`);
  writeFileSync(file, JSON.stringify(summary, null, 2));
  console.log(`\n== ${model}`);
  console.log(`superati ${summary.passed}/${summary.scenarios} · proposte non dovute ${summary.scenariosWithExtraProposals} · mancate ${summary.scenariosWithMissingProposals} · azioni dichiarate ${summary.claimedActionsWithoutProposal}`);
  console.log(`errori strumenti ${summary.toolErrors} · argomenti non validi ${summary.invalidArguments} · errori gateway ${summary.gatewayErrors} · giri medi ${summary.averageRoundTrips.toFixed(2)} · costo medio/turno $${summary.averageCostPerTurnUsd.toFixed(6)} · p95 ${summary.latencyP95Ms} ms`);
  for (const r of results.filter((x) => !x.passed)) {
    console.log(`  ✗ ${r.id}: strumenti=[${r.toolsCalled}] mancanti=[${r.missingTools}] extra=${r.extraProposals} mancate=${r.missingProposals} dichiarata=${r.claimedActionWithoutProposal} errore=${r.errorCode ?? '-'}`);
  }
  console.log(`dettaglio: ${file}`);
}

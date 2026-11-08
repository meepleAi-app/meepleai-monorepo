/**
 * Benchmark informativo per le utility Prometheus — NON un gate.
 *
 * Questo file vive sotto il pattern `*.performance.test.ts`, che `vitest.config.ts`
 * esclude quando `CI` è impostata (#1951): misura e riporta, non promuove né boccia.
 *
 * 🔴 NON reintrodurre `expect(elapsed).toBeLessThan(N)` qui né in
 * `prometheusUtils.test.ts`. Le tre soglie che stavano nel file bloccante
 * (`0.1` / `50` / `10` ms) sono state RIMOSSE, non alzate, chiudendo #3953:
 * su runner condiviso `performance.now()` misura la contesa di CPU del runner,
 * non il costo dell'algoritmo, quindi una soglia assoluta non garantisce nulla di
 * verificabile — fallisce quando il runner è carico e passa quando il codice è
 * lento su una macchina scarica. La soglia da `10` ms era già stata alzata una
 * volta (da `5`, «to reduce flakiness») e il flaky è tornato: la terza cifra non
 * avrebbe cambiato esito. Se serve difendere il costo di queste funzioni, serve
 * un benchmark con baseline storica e confronto relativo, non un `toBeLessThan`.
 *
 * Le asserzioni rimaste verificano la CORRETTEZZA dell'output sotto carico —
 * l'unica cosa che un runner condiviso può garantire.
 */

import { describe, it, expect } from 'vitest';
import { escapePrometheusLabelValue, formatPrometheusMetric } from '../prometheusUtils';

describe('Prometheus utils: benchmark informativo', () => {
  it('escapes strings correctly under repeated calls', () => {
    const typicalString = '/api/v1/users?query="test"&page=1';
    const iterations = 1000;

    const start = performance.now();
    for (let i = 0; i < iterations; i++) {
      escapePrometheusLabelValue(typicalString);
    }
    const end = performance.now();

    const avgTime = (end - start) / iterations;

    // Diagnostica (visibile in output verbose), nessun gate.
    console.info(
      `[benchmark] escapePrometheusLabelValue: ${avgTime.toFixed(4)}ms/chiamata su ${iterations} iterazioni`
    );

    // Garanzia verificabile: l'escape resta corretto e stabile.
    const escaped = escapePrometheusLabelValue(typicalString);
    expect(escaped).toContain('page=1');
    expect(escaped).toBe(escapePrometheusLabelValue(typicalString));
  });

  it('formats high-volume metrics correctly', () => {
    const endpoints = [
      '/api/v1/users',
      '/api/v1/games',
      '/api/v1/chat',
      '/api/v1/auth',
      '/api/v1/admin',
    ];

    const iterations = 100;
    let lastLine = '';

    const start = performance.now();
    for (let i = 0; i < iterations; i++) {
      endpoints.forEach(endpoint => {
        lastLine = formatPrometheusMetric(
          'http_requests_total',
          { endpoint, method: 'GET', status: '200' },
          i
        );
      });
    }
    const end = performance.now();

    const totalTime = end - start;
    const count = iterations * endpoints.length;
    console.info(
      `[benchmark] formatPrometheusMetric: ${count} metriche in ${totalTime.toFixed(2)}ms ` +
        `(${(count / (totalTime / 1000)).toFixed(0)} metriche/sec)`
    );

    // Garanzia verificabile: l'ultima riga prodotta sotto carico è ben formata.
    expect(lastLine).toContain('http_requests_total');
    expect(lastLine).toContain('/api/v1/admin');
    expect(lastLine).toContain('status="200"');
  });

  it('produces a well-formed export under a realistic workload', () => {
    const metricCount = 50;
    const lines: string[] = [];

    const start = performance.now();

    lines.push('# HELP http_requests_total Total HTTP requests');
    lines.push('# TYPE http_requests_total counter');

    for (let i = 0; i < metricCount; i++) {
      const endpoint = `/api/v${i % 3}/endpoint${i}`;
      lines.push(formatPrometheusMetric('http_requests_total', { endpoint, method: 'GET' }, i));
    }

    const output = lines.join('\n') + '\n';
    const end = performance.now();

    console.info(
      `[benchmark] export di ${metricCount} metriche generato in ${(end - start).toFixed(2)}ms`
    );

    // Garanzia verificabile: 2 righe di header + metricCount righe + la riga vuota finale.
    expect(output.split('\n')).toHaveLength(metricCount + 3);
    expect(output).toContain('# TYPE http_requests_total counter');
    expect(output.endsWith('\n')).toBe(true);
  });
});

import { env } from './onboarding-environment';

interface HealthCheckResult {
  service: string;
  healthy: boolean;
  error?: string;
}

/**
 * Pre-flight health check for external services required by flow tests.
 * Call in test.beforeAll() to set failureReason if critical services are down.
 *
 * @param services - Which services to check
 * @returns Array of health check results
 */
export async function checkFlowPrerequisites(
  services: ('api' | 'embedding' | 'frontend')[] = ['api', 'frontend']
): Promise<HealthCheckResult[]> {
  const results: HealthCheckResult[] = [];

  for (const service of services) {
    try {
      const url = getHealthUrl(service);
      const resp = await fetch(url, {
        signal: AbortSignal.timeout(10_000),
      });
      results.push({
        service,
        healthy: resp.ok,
        error: resp.ok ? undefined : `HTTP ${resp.status}`,
      });
    } catch (e) {
      results.push({
        service,
        healthy: false,
        error: e instanceof Error ? e.message : String(e),
      });
    }
  }

  return results;
}

/**
 * Resolve health URL per service.
 * Uses env-based URLs — never derive ports via string replacement.
 */
function getHealthUrl(service: string): string {
  switch (service) {
    case 'api':
      return `${env.apiURL}/health`;
    case 'embedding':
      return (
        process.env.E2E_EMBEDDING_URL ??
        (env.name === 'staging' ? `${env.apiURL}/health` : 'http://localhost:8000/health')
      );
    case 'frontend':
      return `${env.baseURL}`;
    default:
      return 'http://localhost:3000';
  }
}

/**
 * Format health results for skip message.
 */
export function formatHealthResults(results: HealthCheckResult[]): string {
  return results
    .filter(r => !r.healthy)
    .map(r => `${r.service}: ${r.error}`)
    .join(', ');
}

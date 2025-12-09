/**
 * Server health check utilities for E2E tests
 * Issue #2007: E2E Server Stability - Phase 1, Batch 2
 */

export async function waitForServerHealth(
  url: string = 'http://localhost:3000',
  maxAttempts: number = 30,
  delayMs: number = 1000
): Promise<void> {
  for (let i = 0; i < maxAttempts; i++) {
    try {
      const response = await fetch(`${url}/health`, {
        signal: AbortSignal.timeout(5000),
      });

      if (response.ok) {
        const data = await response.json();
        console.log(`✓ Server health OK (uptime: ${data.uptime}s)`);
        return;
      }
    } catch (error) {
      if (i === maxAttempts - 1) {
        throw new Error(`Server health check failed after ${maxAttempts} attempts: ${error}`);
      }
      await new Promise(resolve => setTimeout(resolve, delayMs));
    }
  }
}

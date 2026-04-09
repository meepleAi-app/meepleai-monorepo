import { describe, it, expect } from 'vitest';
import { readFileSync, readdirSync, statSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const BASELINE_PATH = join(process.cwd(), 'bundle-size-baseline.json');
const CHUNKS_DIR = join(process.cwd(), '.next/static/chunks');

describe('bundle-size', () => {
  it.skipIf(!existsSync(CHUNKS_DIR))(
    'total JS chunk size does not exceed baseline + tolerance',
    () => {
      const baseline = JSON.parse(readFileSync(BASELINE_PATH, 'utf8')) as {
        totalBytes: number;
        toleranceBytes: number;
      };

      // If baseline is 0 (placeholder), skip the assertion
      if (baseline.totalBytes === 0) {
        return;
      }

      const files = readdirSync(CHUNKS_DIR).filter(f => f.endsWith('.js'));
      const total = files.reduce((acc, f) => acc + statSync(join(CHUNKS_DIR, f)).size, 0);

      const maxAllowed = baseline.totalBytes + baseline.toleranceBytes;
      expect(total).toBeLessThanOrEqual(maxAllowed);
    }
  );
});

import { readFileSync } from 'fs';
import { resolve } from 'path';

function readSecretFile(filename: string): Record<string, string> {
  try {
    const secretPath = resolve(__dirname, '../../../../infra/secrets', filename);
    const content = readFileSync(secretPath, 'utf-8');
    const entries: Record<string, string> = {};
    for (const line of content.split('\n')) {
      const trimmed = line.trim();
      if (!trimmed || trimmed.startsWith('#')) continue;
      const eqIndex = trimmed.indexOf('=');
      if (eqIndex > 0) {
        entries[trimmed.slice(0, eqIndex)] = trimmed.slice(eqIndex + 1);
      }
    }
    return entries;
  } catch {
    return {};
  }
}

export interface OnboardingEnvironment {
  name: 'local' | 'staging';
  baseURL: string;
  apiURL: string;
  admin: { email: string; password: string };
  email: {
    strategy: 'api-intercept' | 'mailosaur';
    mailosaurApiKey?: string;
    mailosaurServerId?: string;
  };
  seedGameName: string;
  timeouts: {
    email: number;
    agentReady: number;
    chatResponse: number;
  };
}

function resolveEnvironment(): OnboardingEnvironment {
  const isStaging = process.env.E2E_ENV === 'staging';

  const e2eSecret = readSecretFile('e2e.secret');
  const adminSecret = readSecretFile('admin.secret');

  if (isStaging) {
    return {
      name: 'staging',
      baseURL: 'https://meepleai.app',
      apiURL: 'https://api.meepleai.app',
      admin: {
        email:
          process.env.E2E_ADMIN_EMAIL ??
          e2eSecret['E2E_ADMIN_EMAIL'] ??
          adminSecret['ADMIN_EMAIL'] ??
          'admin@meepleai.app',
        password:
          process.env.E2E_ADMIN_PASSWORD ??
          e2eSecret['E2E_ADMIN_PASSWORD'] ??
          adminSecret['ADMIN_PASSWORD'] ??
          'changeme',
      },
      email: {
        strategy: 'mailosaur',
        mailosaurApiKey: process.env.E2E_MAILOSAUR_API_KEY ?? e2eSecret['E2E_MAILOSAUR_API_KEY']!,
        mailosaurServerId:
          process.env.E2E_MAILOSAUR_SERVER_ID ?? e2eSecret['E2E_MAILOSAUR_SERVER_ID']!,
      },
      seedGameName: 'Catan',
      timeouts: { email: 30_000, agentReady: 30_000, chatResponse: 60_000 },
    };
  }

  return {
    name: 'local',
    baseURL: 'http://localhost:3000',
    apiURL: 'http://localhost:8080',
    admin: {
      email: process.env.E2E_ADMIN_EMAIL ?? adminSecret['ADMIN_EMAIL'] ?? 'admin@meepleai.app',
      password: process.env.E2E_ADMIN_PASSWORD ?? adminSecret['ADMIN_PASSWORD'] ?? 'changeme',
    },
    email: { strategy: 'api-intercept' },
    seedGameName: 'Pandemic',
    timeouts: { email: 5_000, agentReady: 30_000, chatResponse: 60_000 },
  };
}

export const env = resolveEnvironment();

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

  if (isStaging) {
    return {
      name: 'staging',
      baseURL: 'https://meepleai.app',
      apiURL: 'https://api.meepleai.app',
      admin: {
        email: process.env.E2E_ADMIN_EMAIL!,
        password: process.env.E2E_ADMIN_PASSWORD!,
      },
      email: {
        strategy: 'mailosaur',
        mailosaurApiKey: process.env.E2E_MAILOSAUR_API_KEY!,
        mailosaurServerId: process.env.E2E_MAILOSAUR_SERVER_ID!,
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
      email: process.env.E2E_ADMIN_EMAIL ?? 'admin@meepleai.dev',
      password: process.env.E2E_ADMIN_PASSWORD ?? 'changeme', // Set E2E_ADMIN_PASSWORD env var
    },
    email: { strategy: 'api-intercept' },
    seedGameName: 'Pandemic',
    timeouts: { email: 5_000, agentReady: 30_000, chatResponse: 60_000 },
  };
}

export const env = resolveEnvironment();

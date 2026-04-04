import { HttpClient } from '../core/httpClient';

const httpClient = new HttpClient({});

export interface SecretEntry {
  key: string;
  maskedValue: string;
  hasValue: boolean;
  isPlaceholder: boolean;
}

export interface SecretFile {
  fileName: string;
  category: string;
  isInfra: boolean;
  entries: SecretEntry[];
}

export interface SecretsResponse {
  secretsDirectory: string;
  files: SecretFile[];
}

export interface SecretUpdate {
  fileName: string;
  key: string;
  value: string;
}

export const adminSecretsClient = {
  getSecrets: (reveal = false) =>
    httpClient.get<SecretsResponse>(`/api/v1/admin/secrets${reveal ? '?reveal=true' : ''}`),

  updateSecrets: (updates: SecretUpdate[]) =>
    httpClient.put<{ updatedFiles: string[]; updatedKeys: number }>('/api/v1/admin/secrets', {
      updates,
    }),

  restartApi: () =>
    httpClient.post<{ message: string; restartedAt: string }>('/api/v1/admin/secrets/restart'),
};

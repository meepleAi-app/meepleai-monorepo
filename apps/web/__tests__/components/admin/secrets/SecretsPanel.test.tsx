import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { SecretsPanel } from '@/components/admin/secrets/SecretsPanel';

// ---------- mocks ----------

const mockGetSecrets = vi.fn();
const mockUpdateSecrets = vi.fn();
const mockRestartApi = vi.fn();

vi.mock('@/lib/api/clients/adminSecretsClient', () => ({
  adminSecretsClient: {
    getSecrets: (...args: unknown[]) => mockGetSecrets(...args),
    updateSecrets: (...args: unknown[]) => mockUpdateSecrets(...args),
    restartApi: (...args: unknown[]) => mockRestartApi(...args),
  },
}));

const mockToast = vi.fn();
vi.mock('@/hooks/useToast', () => ({
  useToast: () => ({ toast: mockToast }),
}));

// ---------- helpers ----------

const MOCK_SECRETS = {
  secretsDirectory: '/app/secrets/dev',
  files: [
    {
      fileName: 'database.secret',
      category: 'Database',
      isInfra: false,
      entries: [
        { key: 'DB_HOST', maskedValue: 'loc****', hasValue: true, isPlaceholder: false },
        { key: 'DB_PASSWORD', maskedValue: '', hasValue: false, isPlaceholder: true },
      ],
    },
    {
      fileName: 'redis.secret',
      category: 'Redis',
      isInfra: true,
      entries: [{ key: 'REDIS_URL', maskedValue: 'red****', hasValue: true, isPlaceholder: false }],
    },
  ],
};

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

// ---------- tests ----------

describe('SecretsPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetSecrets.mockResolvedValue(MOCK_SECRETS);
  });

  describe('Loading & Error States', () => {
    it('shows loading spinner while fetching', () => {
      mockGetSecrets.mockReturnValue(new Promise(() => {})); // never resolves
      render(<SecretsPanel />, { wrapper });

      expect(screen.getByText('Loading secrets...')).toBeInTheDocument();
    });

    it('shows error message when fetch fails', async () => {
      mockGetSecrets.mockRejectedValue(new Error('Network error'));
      render(<SecretsPanel />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText(/impossibile caricare i secret/i)).toBeInTheDocument();
      });
    });
  });

  describe('Rendering Secrets', () => {
    it('renders header with secrets directory', async () => {
      render(<SecretsPanel />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('Secrets Management')).toBeInTheDocument();
      });
      expect(screen.getByText('/app/secrets/dev')).toBeInTheDocument();
    });

    it('renders all secret files with categories', async () => {
      render(<SecretsPanel />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('Database')).toBeInTheDocument();
      });
      expect(screen.getByText('Redis')).toBeInTheDocument();
    });

    it('renders file name labels', async () => {
      render(<SecretsPanel />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('database.secret')).toBeInTheDocument();
      });
      expect(screen.getByText('redis.secret')).toBeInTheDocument();
    });

    it('renders secret entry keys', async () => {
      render(<SecretsPanel />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('DB_HOST')).toBeInTheDocument();
      });
      expect(screen.getByText('DB_PASSWORD')).toBeInTheDocument();
      expect(screen.getByText('REDIS_URL')).toBeInTheDocument();
    });

    it('shows Infra badge for infrastructure files', async () => {
      render(<SecretsPanel />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('Infra')).toBeInTheDocument();
      });
    });

    it('shows placeholder warning icon for empty secrets', async () => {
      render(<SecretsPanel />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('DB_PASSWORD')).toBeInTheDocument();
      });
      // DB_PASSWORD has isPlaceholder=true, should show AlertTriangle
      expect(screen.getByTitle('Placeholder value')).toBeInTheDocument();
    });
  });

  describe('Editing & Saving', () => {
    it('does not show save button when no changes', async () => {
      render(<SecretsPanel />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('Secrets Management')).toBeInTheDocument();
      });
      expect(screen.queryByText(/salva modifiche/i)).not.toBeInTheDocument();
    });

    it('shows save button with count after editing a field', async () => {
      const user = userEvent.setup();
      render(<SecretsPanel />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('DB_HOST')).toBeInTheDocument();
      });

      // Input type="password" has no implicit ARIA role — query by CSS
      const inputs = document.querySelectorAll<HTMLInputElement>('input');
      await user.click(inputs[0]);
      await user.type(inputs[0], 'newhost');

      expect(screen.getByText(/salva modifiche \(1 campo\)/i)).toBeInTheDocument();
    });

    it('calls updateSecrets and shows toast on save', async () => {
      const user = userEvent.setup();
      mockUpdateSecrets.mockResolvedValue({ updatedFiles: ['database.secret'], updatedKeys: 1 });
      render(<SecretsPanel />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('DB_HOST')).toBeInTheDocument();
      });

      const inputs = document.querySelectorAll<HTMLInputElement>('input');
      await user.click(inputs[0]);
      await user.type(inputs[0], 'newvalue');

      const saveBtn = screen.getByText(/salva modifiche/i);
      await user.click(saveBtn);

      await waitFor(() => {
        expect(mockUpdateSecrets).toHaveBeenCalledWith([
          { fileName: 'database.secret', key: 'DB_HOST', value: 'newvalue' },
        ]);
      });
      expect(mockToast).toHaveBeenCalledWith({ title: '1 secret aggiornati' });
    });

    it('shows error toast when save fails', async () => {
      const user = userEvent.setup();
      mockUpdateSecrets.mockRejectedValue(new Error('Save failed'));
      render(<SecretsPanel />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('DB_HOST')).toBeInTheDocument();
      });

      const inputs = document.querySelectorAll<HTMLInputElement>('input');
      await user.click(inputs[0]);
      await user.type(inputs[0], 'x');

      await user.click(screen.getByText(/salva modifiche/i));

      await waitFor(() => {
        expect(mockToast).toHaveBeenCalledWith({
          title: 'Errore durante il salvataggio',
          variant: 'destructive',
        });
      });
    });
  });

  describe('Restart Banner', () => {
    it('shows restart banner after successful save', async () => {
      const user = userEvent.setup();
      mockUpdateSecrets.mockResolvedValue({ updatedFiles: ['database.secret'], updatedKeys: 1 });
      render(<SecretsPanel />, { wrapper });

      await waitFor(() => {
        expect(screen.getByText('DB_HOST')).toBeInTheDocument();
      });

      const inputs = document.querySelectorAll<HTMLInputElement>('input');
      await user.click(inputs[0]);
      await user.type(inputs[0], 'v');

      await user.click(screen.getByText(/salva modifiche/i));

      await waitFor(() => {
        expect(screen.getByText(/riavvia l'api per applicare/i)).toBeInTheDocument();
      });
      expect(screen.getByText('Riavvia ora')).toBeInTheDocument();
    });
  });
});

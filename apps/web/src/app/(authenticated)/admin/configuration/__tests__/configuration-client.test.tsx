import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AdminPageClient } from '../client';
import { AuthProvider } from '@/components/auth/AuthProvider';
import type { AuthUser } from '@/types/auth';
import { api } from '@/lib/api';

vi.mock('@/lib/api', () => ({
  api: {
    config: {
      getConfigurations: vi.fn(),
      getCategories: vi.fn(),
      updateConfiguration: vi.fn(),
      invalidateCache: vi.fn(),
    },
  },
}));

vi.mock('@/components/auth/AuthProvider', async () => {
  const actual = await vi.importActual('@/components/auth/AuthProvider');
  return {
    ...actual,
    useAuthUser: () => ({
      user: mockUser,
      loading: false,
    }),
  };
});

const mockUser: AuthUser = {
  id: 'admin-id',
  email: 'admin@meepleai.com',
  displayName: 'Admin User',
  role: 'Admin',
  createdAt: new Date().toISOString(),
  isTwoFactorEnabled: false,
};

const mockConfigurationsData = {
  items: [
    {
      id: '1',
      key: 'Features:RagCaching',
      value: 'true',
      description: 'Enable RAG caching',
      category: 'FeatureFlag',
      valueType: 'boolean',
      isActive: true,
      requiresRestart: false,
      version: 1,
      createdAt: '2025-01-01T00:00:00Z',
      updatedAt: '2025-01-01T00:00:00Z',
    },
    {
      id: '2',
      key: 'RateLimiting:AdminLimit',
      value: '100',
      description: 'Admin rate limit',
      category: 'RateLimiting',
      valueType: 'integer',
      isActive: true,
      requiresRestart: true,
      version: 1,
      createdAt: '2025-01-01T00:00:00Z',
      updatedAt: '2025-01-01T00:00:00Z',
    },
    {
      id: '3',
      key: 'AiLlm:Temperature',
      value: '0.7',
      description: 'AI temperature',
      category: 'AiLlm',
      valueType: 'float',
      isActive: true,
      requiresRestart: false,
      version: 1,
      createdAt: '2025-01-01T00:00:00Z',
      updatedAt: '2025-01-01T00:00:00Z',
    },
    {
      id: '4',
      key: 'Rag:TopK',
      value: '5',
      description: 'Top K results',
      category: 'Rag',
      valueType: 'integer',
      isActive: true,
      requiresRestart: false,
      version: 1,
      createdAt: '2025-01-01T00:00:00Z',
      updatedAt: '2025-01-01T00:00:00Z',
    },
  ],
  total: 4,
  page: 1,
  pageSize: 100,
};

const mockCategories = ['FeatureFlag', 'RateLimiting', 'AiLlm', 'Rag'];

describe('Configuration AdminPageClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.config.getConfigurations).mockResolvedValue(mockConfigurationsData);
    vi.mocked(api.config.getCategories).mockResolvedValue(mockCategories);
  });

  it('renders loading state initially', () => {
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    expect(screen.getByText('Loading configurations...')).toBeInTheDocument();
  });

  it('renders configuration page after loading', async () => {
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Configuration Management')).toBeInTheDocument();
    });

    expect(screen.getByText('Manage system configurations and feature flags')).toBeInTheDocument();
  });

  it('displays restart reminder banner', async () => {
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText(/Configuration changes require server restart/i)).toBeInTheDocument();
    });
  });

  it('dismisses restart banner when close button is clicked', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText(/Configuration changes require server restart/i)).toBeInTheDocument();
    });

    const dismissButton = screen.getByRole('button', { name: 'Dismiss banner' });
    await user.click(dismissButton);

    await waitFor(() => {
      expect(
        screen.queryByText(/Configuration changes require server restart/i)
      ).not.toBeInTheDocument();
    });
  });

  it('renders all configuration tabs', async () => {
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Feature Flags')).toBeInTheDocument();
    });

    expect(screen.getByText('Rate Limiting')).toBeInTheDocument();
    expect(screen.getByText('AI / LLM')).toBeInTheDocument();
    expect(screen.getByText('RAG')).toBeInTheDocument();
  });

  it('switches between tabs when clicked', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Feature Flags')).toBeInTheDocument();
    });

    const rateLimitingTab = screen.getByRole('button', { name: /Rate Limiting/i });
    await user.click(rateLimitingTab);

    await waitFor(() => {
      expect(screen.getByText('Rate Limiting Configuration')).toBeInTheDocument();
    });
  });

  it('displays configuration statistics', async () => {
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Total Configurations')).toBeInTheDocument();
    });

    const counts = screen.getAllByText('4');
    expect(counts.length).toBeGreaterThan(0); // Multiple stats showing 4
    expect(screen.getByText('Active')).toBeInTheDocument();
    expect(screen.getByText('Categories')).toBeInTheDocument();
  });

  it('reloads configurations when reload button is clicked', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Configuration Management')).toBeInTheDocument();
    });

    const reloadButton = screen.getByRole('button', { name: /Reload/i });
    await user.click(reloadButton);

    await waitFor(() => {
      expect(api.config.getConfigurations).toHaveBeenCalledTimes(2);
    });
  });

  it('invalidates cache when clear cache button is clicked', async () => {
    const user = userEvent.setup();
    vi.mocked(api.config.invalidateCache).mockResolvedValue(undefined);

    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Configuration Management')).toBeInTheDocument();
    });

    const clearCacheButton = screen.getByRole('button', { name: /Clear Cache/i });
    await user.click(clearCacheButton);

    await waitFor(() => {
      expect(api.config.invalidateCache).toHaveBeenCalled();
    });
  });

  it('displays error message when API call fails', async () => {
    vi.mocked(api.config.getConfigurations).mockRejectedValue(
      new Error('Failed to fetch configurations')
    );

    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Unexpected Error')).toBeInTheDocument();
    });
  });

  it('shows retry button on error', async () => {
    const user = userEvent.setup();
    vi.mocked(api.config.getConfigurations).mockRejectedValueOnce(
      new Error('Failed to fetch configurations')
    );

    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Unexpected Error')).toBeInTheDocument();
    });

    // Reset mock to succeed on retry
    vi.mocked(api.config.getConfigurations).mockResolvedValue(mockConfigurationsData);

    const retryButton = screen.getByRole('button', { name: /Retry/i });
    await user.click(retryButton);

    await waitFor(() => {
      expect(screen.getByText('Configuration Management')).toBeInTheDocument();
    });
  });

  it('renders feature flags tab by default', async () => {
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Configuration Management')).toBeInTheDocument();
    });

    // Feature flags tab should be active (has specific styling)
    const featureFlagsTab = screen.getByRole('button', { name: /Feature Flags/i });
    expect(featureFlagsTab).toHaveClass('border-blue-600');
  });

  it('displays category configuration when switching to rate limiting tab', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Configuration Management')).toBeInTheDocument();
    });

    const rateLimitingTab = screen.getByRole('button', { name: /Rate Limiting/i });
    await user.click(rateLimitingTab);

    await waitFor(() => {
      expect(screen.getByText('Rate Limiting Configuration')).toBeInTheDocument();
      expect(screen.getByText('RateLimiting:AdminLimit')).toBeInTheDocument();
    });
  });

  it('displays AI/LLM configuration when switching to AI tab', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Configuration Management')).toBeInTheDocument();
    });

    const aiTab = screen.getByRole('button', { name: /AI \/ LLM/i });
    await user.click(aiTab);

    await waitFor(() => {
      expect(screen.getByText('AI / LLM Configuration')).toBeInTheDocument();
      expect(screen.getByText('AiLlm:Temperature')).toBeInTheDocument();
    });
  });

  it('displays RAG configuration when switching to RAG tab', async () => {
    const user = userEvent.setup();
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Configuration Management')).toBeInTheDocument();
    });

    const ragTab = screen.getByRole('button', { name: /RAG/i });
    await user.click(ragTab);

    await waitFor(() => {
      expect(screen.getByText('RAG Configuration')).toBeInTheDocument();
      expect(screen.getByText('Rag:TopK')).toBeInTheDocument();
    });
  });

  it('navigates back to admin dashboard', async () => {
    render(
      <AuthProvider>
        <AdminPageClient />
      </AuthProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Configuration Management')).toBeInTheDocument();
    });

    const backButton = screen.getByRole('link', { name: /Back to Admin/i });
    expect(backButton).toHaveAttribute('href', '/admin');
  });
});

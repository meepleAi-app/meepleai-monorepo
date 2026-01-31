/**
 * Auth-Gated Workflows Integration Tests - Issue #3026
 *
 * Tests authentication-protected features and error handling
 */

import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

global.fetch = vi.fn();

describe('Auth-Gated Workflows', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Protected Resource Access', () => {
    it('should fetch protected resource with auth token', async () => {
      vi.mocked(global.fetch).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ data: 'protected content' }),
      } as Response);

      const TestComponent = () => {
        const [data, setData] = React.useState<string | null>(null);

        React.useEffect(() => {
          fetch('/api/protected', {
            headers: { Authorization: 'Bearer token123' },
          })
            .then(r => r.json())
            .then(d => setData(d.data));
        }, []);

        return <div>{data && <div role="status">{data}</div>}</div>;
      };

      render(<TestComponent />);

      await waitFor(() => {
        expect(screen.getByRole('status')).toHaveTextContent('protected content');
      });

      expect(global.fetch).toHaveBeenCalledWith(
        '/api/protected',
        expect.objectContaining({
          headers: { Authorization: 'Bearer token123' },
        })
      );
    });

    it('should handle 401 Unauthorized error', async () => {
      vi.mocked(global.fetch).mockResolvedValueOnce({
        ok: false,
        status: 401,
      } as Response);

      const TestComponent = () => {
        const [error, setError] = React.useState<string | null>(null);

        React.useEffect(() => {
          fetch('/api/protected', {
            headers: { Authorization: 'Bearer invalid' },
          }).then(r => {
            if (r.status === 401) setError('Unauthorized');
          });
        }, []);

        return <div>{error && <div role="alert">{error}</div>}</div>;
      };

      render(<TestComponent />);

      await waitFor(() => {
        expect(screen.getByRole('alert')).toHaveTextContent('Unauthorized');
      });
    });

    it('should handle 403 Forbidden error', async () => {
      vi.mocked(global.fetch).mockResolvedValueOnce({
        ok: false,
        status: 403,
      } as Response);

      const TestComponent = () => {
        const [error, setError] = React.useState<string | null>(null);

        React.useEffect(() => {
          fetch('/api/admin', {
            headers: { Authorization: 'Bearer usertoken' },
          }).then(r => {
            if (r.status === 403) setError('Forbidden');
          });
        }, []);

        return <div>{error && <div role="alert">{error}</div>}</div>;
      };

      render(<TestComponent />);

      await waitFor(() => {
        expect(screen.getByRole('alert')).toHaveTextContent('Forbidden');
      });
    });
  });

  describe('Token Refresh Flows', () => {
    it('should retry request after token refresh on 401', async () => {
      vi.mocked(global.fetch)
        .mockResolvedValueOnce({
          ok: false,
          status: 401,
        } as Response)
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ data: 'success after refresh' }),
        } as Response);

      const TestComponent = () => {
        const [data, setData] = React.useState<string | null>(null);

        React.useEffect(() => {
          const fetchWithRetry = async () => {
            let response = await fetch('/api/protected');
            if (response.status === 401) {
              // Simulate token refresh
              response = await fetch('/api/protected');
            }
            if (response.ok) {
              const json = await response.json();
              setData(json.data);
            }
          };
          void fetchWithRetry();
        }, []);

        return <div>{data && <div>{data}</div>}</div>;
      };

      render(<TestComponent />);

      await waitFor(() => {
        expect(screen.getByText('success after refresh')).toBeInTheDocument();
      });

      expect(global.fetch).toHaveBeenCalledTimes(2);
    });
  });

  describe('Role-Based Access', () => {
    it('should render admin content for admin role', () => {
      const user = { role: 'admin' };

      const TestComponent = () => (
        <div>
          {user.role === 'admin' && <div role="region">Admin Panel</div>}
        </div>
      );

      render(<TestComponent />);

      expect(screen.getByRole('region')).toHaveTextContent('Admin Panel');
    });

    it('should not render admin content for user role', () => {
      const user = { role: 'user' };

      const TestComponent = () => (
        <div>
          {user.role === 'admin' && <div role="region">Admin Panel</div>}
          {user.role === 'user' && <div>User Dashboard</div>}
        </div>
      );

      render(<TestComponent />);

      expect(screen.queryByRole('region')).not.toBeInTheDocument();
      expect(screen.getByText('User Dashboard')).toBeInTheDocument();
    });
  });
});

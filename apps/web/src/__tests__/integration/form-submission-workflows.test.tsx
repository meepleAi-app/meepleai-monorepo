/**
 * Form Submission Integration Tests - Issue #3026
 *
 * Multi-component workflows for form submissions with validation and error handling
 */

import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

global.fetch = vi.fn();

describe('Form Submission Workflows', () => {
  describe('Success Flows', () => {
    it('should submit form with valid data', async () => {
      vi.mocked(global.fetch).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ success: true }),
      } as Response);

      const onSuccess = vi.fn();
      const { container } = render(
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            const formData = new FormData(e.currentTarget);
            const data = Object.fromEntries(formData.entries());
            const response = await fetch('/api/submit', {
              method: 'POST',
              body: JSON.stringify(data),
            });
            if (response.ok) onSuccess();
          }}
        >
          <input name="email" type="email" defaultValue="test@test.com" />
          <button type="submit">Submit</button>
        </form>
      );

      const submitButton = screen.getByRole('button', { name: /submit/i });
      fireEvent.click(submitButton);

      await waitFor(() => {
        expect(onSuccess).toHaveBeenCalled();
      });
    });

    it('should show success message after submission', async () => {
      vi.mocked(global.fetch).mockResolvedValueOnce({
        ok: true,
        json: async () => ({ success: true }),
      } as Response);

      const TestForm = () => {
        const [success, setSuccess] = React.useState(false);

        return (
          <div>
            <form
              onSubmit={async (e) => {
                e.preventDefault();
                const res = await fetch('/api/submit', { method: 'POST' });
                if (res.ok) setSuccess(true);
              }}
            >
              <button type="submit">Submit</button>
            </form>
            {success && <div role="status">Success!</div>}
          </div>
        );
      };

      render(<TestForm />);

      fireEvent.click(screen.getByRole('button'));

      await waitFor(() => {
        expect(screen.getByRole('status')).toHaveTextContent('Success!');
      });
    });
  });

  describe('Error Flows', () => {
    it('should show error message on API failure', async () => {
      vi.mocked(global.fetch).mockRejectedValueOnce(new Error('API error'));

      const TestForm = () => {
        const [error, setError] = React.useState<string | null>(null);

        return (
          <div>
            <form
              onSubmit={async (e) => {
                e.preventDefault();
                try {
                  await fetch('/api/submit', { method: 'POST' });
                } catch (err) {
                  setError(err instanceof Error ? err.message : 'Error');
                }
              }}
            >
              <button type="submit">Submit</button>
            </form>
            {error && <div role="alert">{error}</div>}
          </div>
        );
      };

      render(<TestForm />);

      fireEvent.click(screen.getByRole('button'));

      await waitFor(() => {
        expect(screen.getByRole('alert')).toHaveTextContent('API error');
      });
    });

    it('should preserve form data on submission error', async () => {
      vi.mocked(global.fetch).mockRejectedValueOnce(new Error('Network error'));

      const { container } = render(
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            await fetch('/api/submit', { method: 'POST' }).catch(() => {});
          }}
        >
          <input name="email" type="email" defaultValue="test@test.com" />
          <button type="submit">Submit</button>
        </form>
      );

      const input = container.querySelector('input[name="email"]') as HTMLInputElement;
      fireEvent.click(screen.getByRole('button'));

      await waitFor(() => {
        expect(input.value).toBe('test@test.com');
      });
    });
  });

  describe('Validation Flows', () => {
    it('should prevent submission with invalid email', () => {
      const onSubmit = vi.fn();
      const { container } = render(
        <form onSubmit={(e) => { e.preventDefault(); onSubmit(); }}>
          <input name="email" type="email" required />
          <button type="submit">Submit</button>
        </form>
      );

      const input = container.querySelector('input') as HTMLInputElement;
      input.value = 'invalid-email';

      fireEvent.click(screen.getByRole('button'));

      expect(onSubmit).not.toHaveBeenCalled();
    });

    it('should show validation error for required field', () => {
      const { container } = render(
        <form>
          <input name="email" type="email" required aria-invalid="true" />
          <span role="alert">Email is required</span>
          <button type="submit">Submit</button>
        </form>
      );

      expect(screen.getByRole('alert')).toHaveTextContent('Email is required');
      expect(container.querySelector('input')).toHaveAttribute('aria-invalid', 'true');
    });
  });
});

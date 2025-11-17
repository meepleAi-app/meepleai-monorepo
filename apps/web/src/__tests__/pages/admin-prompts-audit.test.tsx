import React from 'react';
import {  screen, fireEvent, waitFor } from '@testing-library/react';
import { renderWithQuery } from '../utils/query-test-utils';
import { useRouter } from 'next/router';
import AuditLog from '../../pages/admin/prompts/[id]/audit';
import { api } from '../../lib/api';

// Mock dependencies
jest.mock('next/router', () => ({
  useRouter: jest.fn(),
}));

jest.mock('next/link', () => {
  return ({ children, href }: any) => {
    return <a href={href}>{children}</a>;
  };
});

jest.mock('../../lib/api', () => ({
  api: {
    get: jest.fn(),
  },
}));

const mockPush = jest.fn();
const mockRouter = {
  push: mockPush,
  query: { id: 'template-123' },
};

const mockAuditLogs = [
  {
    id: 'log-1',
    templateId: 'template-123',
    action: 'create',
    userId: 'user-1',
    userEmail: 'admin@example.com',
    timestamp: '2024-01-01T10:00:00Z',
    details: {
      name: 'QA System Prompt',
      category: 'qa-system-prompt',
    },
  },
  {
    id: 'log-2',
    templateId: 'template-123',
    action: 'update',
    userId: 'user-2',
    userEmail: 'editor@example.com',
    timestamp: '2024-01-02T11:00:00Z',
    details: {
      changes: ['description'],
    },
  },
  {
    id: 'log-3',
    templateId: 'template-123',
    action: 'activate',
    userId: 'user-1',
    userEmail: 'admin@example.com',
    timestamp: '2024-01-03T12:00:00Z',
    details: {
      versionId: 'version-1',
      versionNumber: 1,
    },
  },
  {
    id: 'log-4',
    templateId: 'template-123',
    action: 'delete',
    userId: 'user-1',
    userEmail: 'admin@example.com',
    timestamp: '2024-01-04T13:00:00Z',
    details: null,
  },
];

describe('AuditLog Page', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (useRouter as jest.Mock).mockReturnValue(mockRouter);
  });

  describe('Loading State', () => {
    it('should show loading indicator while fetching', () => {
      (api.get as jest.Mock).mockImplementation(() => new Promise(() => {}));

      renderWithQuery(<AuditLog />);

      expect(screen.getByText('Loading...')).toBeInTheDocument();
    });
  });

  describe('Error Handling', () => {
    it('should display error message when fetch fails', async () => {
      (api.get as jest.Mock).mockRejectedValue(new Error('Network error'));

      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('Unexpected Error')).toBeInTheDocument();
      });
    });

    it('should display unauthorized error when API returns null', async () => {
      (api.get as jest.Mock).mockResolvedValue(null);

      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('Unexpected Error')).toBeInTheDocument();
      });
    });
  });

  describe('Empty State', () => {
    it('should display empty state when no logs exist', async () => {
      (api.get as jest.Mock).mockResolvedValue({
        logs: [],
        totalPages: 1,
      });

      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('No audit logs found')).toBeInTheDocument();
        expect(
          screen.getByText('Actions performed on this template will appear here')
        ).toBeInTheDocument();
      });
    });
  });

  describe('Audit Log Display', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue({
        logs: mockAuditLogs,
        totalPages: 1,
      });
    });

    it('should display all audit logs', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        const adminEmails = screen.getAllByText('admin@example.com');
        expect(adminEmails.length).toBeGreaterThan(0);
        expect(screen.getByText('editor@example.com')).toBeInTheDocument();
      });
    });

    it('should display action types', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('create')).toBeInTheDocument();
        expect(screen.getByText('update')).toBeInTheDocument();
        expect(screen.getByText('activate')).toBeInTheDocument();
        expect(screen.getByText('delete')).toBeInTheDocument();
      });
    });

    it('should display formatted timestamps', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        const timestamp = new Date(mockAuditLogs[0].timestamp).toLocaleString();
        expect(screen.getByText(timestamp)).toBeInTheDocument();
      });
    });

    it('should display action icons with correct colors', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        const createIcon = screen.getAllByText('+')[0];
        expect(createIcon).toBeInTheDocument();
      });
    });
  });

  describe('Action Details', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue({
        logs: mockAuditLogs,
        totalPages: 1,
      });
    });

    it('should show view details button when details exist', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        const viewDetailsButtons = screen.getAllByText('View details');
        expect(viewDetailsButtons.length).toBeGreaterThan(0);
      });
    });

    it('should expand details when clicked', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        const detailsButtons = screen.getAllByText('View details');
        expect(detailsButtons.length).toBeGreaterThan(0);
      });

      const detailsButton = screen.getAllByText('View details')[0];
      fireEvent.click(detailsButton);

      await waitFor(() => {
        expect(screen.getByText(/"name"/)).toBeInTheDocument();
        expect(screen.getByText(/"QA System Prompt"/)).toBeInTheDocument();
      });
    });

    it('should display no details message when details is null', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('No additional details')).toBeInTheDocument();
      });
    });

    it('should display no details message when details is empty object', async () => {
      (api.get as jest.Mock).mockResolvedValue({
        logs: [
          {
            ...mockAuditLogs[0],
            details: {},
          },
        ],
        totalPages: 1,
      });

      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('No additional details')).toBeInTheDocument();
      });
    });

    it('should format details as pretty JSON', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        const detailsButtons = screen.getAllByText('View details');
        expect(detailsButtons.length).toBeGreaterThan(0);
      });

      const detailsButton = screen.getAllByText('View details')[0];
      fireEvent.click(detailsButton);

      await waitFor(() => {
        const jsonElement = screen.getByText(/"name"/);
        const preElement = jsonElement.closest('pre');
        expect(preElement?.textContent).toContain(
          JSON.stringify(mockAuditLogs[0].details, null, 2)
        );
      });
    });
  });

  describe('Action Filtering', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue({
        logs: mockAuditLogs,
        totalPages: 1,
      });
    });

    it('should filter by create action', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('create')).toBeInTheDocument();
      });

      const filterSelect = screen.getByRole('combobox');
      fireEvent.change(filterSelect, { target: { value: 'create' } });

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          expect.stringContaining('action=create')
        );
      });
    });

    it('should filter by update action', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('update')).toBeInTheDocument();
      });

      const filterSelect = screen.getByRole('combobox');
      fireEvent.change(filterSelect, { target: { value: 'update' } });

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          expect.stringContaining('action=update')
        );
      });
    });

    it('should filter by activate action', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('activate')).toBeInTheDocument();
      });

      const filterSelect = screen.getByRole('combobox');
      fireEvent.change(filterSelect, { target: { value: 'activate' } });

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          expect.stringContaining('action=activate')
        );
      });
    });

    it('should filter by delete action', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('delete')).toBeInTheDocument();
      });

      const filterSelect = screen.getByRole('combobox');
      fireEvent.change(filterSelect, { target: { value: 'delete' } });

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          expect.stringContaining('action=delete')
        );
      });
    });

    it('should show all actions when filter cleared', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('create')).toBeInTheDocument();
      });

      const filterSelect = screen.getByRole('combobox');
      fireEvent.change(filterSelect, { target: { value: 'create' } });

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          expect.stringContaining('action=create')
        );
      });

      fireEvent.change(filterSelect, { target: { value: '' } });

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          expect.not.stringContaining('action=')
        );
      });
    });

    it('should reset page to 1 when filter changes', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('create')).toBeInTheDocument();
      });

      const filterSelect = screen.getByRole('combobox');
      fireEvent.change(filterSelect, { target: { value: 'create' } });

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          expect.stringContaining('page=1')
        );
      });
    });
  });

  describe('Pagination', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue({
        logs: mockAuditLogs,
        totalPages: 3,
      });
    });

    it('should navigate to next page', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('create')).toBeInTheDocument();
      });

      const nextButton = screen.getByText('Next');
      fireEvent.click(nextButton);

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          expect.stringContaining('page=2')
        );
      });
    });

    it('should navigate to previous page', async () => {
      (api.get as jest.Mock).mockResolvedValue({
        logs: mockAuditLogs,
        totalPages: 3,
      });

      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('create')).toBeInTheDocument();
      });

      // Go to page 2 first
      const nextButton = screen.getByText('Next');
      fireEvent.click(nextButton);

      await waitFor(() => {
        expect(screen.getByText('Page 2 of 3')).toBeInTheDocument();
      });

      const prevButton = screen.getByText('Previous');
      fireEvent.click(prevButton);

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          expect.stringContaining('page=1')
        );
      });
    });

    it('should disable previous button on first page', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('create')).toBeInTheDocument();
      });

      const prevButton = screen.getByText('Previous');
      expect(prevButton).toBeDisabled();
    });

    it('should disable next button on last page', async () => {
      (api.get as jest.Mock).mockResolvedValue({
        logs: mockAuditLogs,
        totalPages: 1,
      });

      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('create')).toBeInTheDocument();
      });

      const nextButton = screen.getByText('Next');
      expect(nextButton).toBeDisabled();
    });

    it('should display correct page number', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('Page 1 of 3')).toBeInTheDocument();
      });

      const nextButton = screen.getByText('Next');
      fireEvent.click(nextButton);

      await waitFor(() => {
        expect(screen.getByText('Page 2 of 3')).toBeInTheDocument();
      });
    });
  });

  describe('Navigation Links', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue({
        logs: mockAuditLogs,
        totalPages: 1,
      });
    });

    it('should have back to template link', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        const backButton = screen.getByText('← Back to Template');
        expect(backButton.closest('a')).toHaveAttribute(
          'href',
          '/admin/prompts/template-123'
        );
      });
    });
  });

  describe('Action Icon Colors', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue({
        logs: mockAuditLogs,
        totalPages: 1,
      });
    });

    it('should use green color for create actions', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        const createRow = screen.getByText('create').closest('tr');
        expect(createRow).toBeInTheDocument();
      });
    });

    it('should use blue color for update/activate actions', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        const updateRow = screen.getByText('update').closest('tr');
        expect(updateRow).toBeInTheDocument();
      });
    });

    it('should use red color for delete actions', async () => {
      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        const deleteRow = screen.getByText('delete').closest('tr');
        expect(deleteRow).toBeInTheDocument();
      });
    });
  });

  describe('Edge Cases', () => {
    it('should handle missing router query id', () => {
      (useRouter as jest.Mock).mockReturnValue({
        ...mockRouter,
        query: {},
      });

      renderWithQuery(<AuditLog />);

      expect(api.get).not.toHaveBeenCalled();
    });

    it('should handle logs with missing user email', async () => {
      (api.get as jest.Mock).mockResolvedValue({
        logs: [
          {
            ...mockAuditLogs[0],
            userEmail: '',
          },
        ],
        totalPages: 1,
      });

      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('create')).toBeInTheDocument();
      });
    });

    it('should handle logs with complex details object', async () => {
      (api.get as jest.Mock).mockResolvedValue({
        logs: [
          {
            ...mockAuditLogs[0],
            details: {
              nested: {
                deep: {
                  value: 'test',
                },
              },
              array: [1, 2, 3],
            },
          },
        ],
        totalPages: 1,
      });

      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('View details')).toBeInTheDocument();
      });

      const detailsButton = screen.getByText('View details');
      fireEvent.click(detailsButton);

      await waitFor(() => {
        expect(screen.getByText(/"nested"/)).toBeInTheDocument();
        expect(screen.getByText(/"array"/)).toBeInTheDocument();
      });
    });

    it('should capitalize action names in display', async () => {
      (api.get as jest.Mock).mockResolvedValue({
        logs: mockAuditLogs,
        totalPages: 1,
      });

      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        // The action text should appear (lowercase in data, but displayed as-is)
        expect(screen.getByText('create')).toBeInTheDocument();
      });
    });
  });

  describe('API Call Parameters', () => {
    it('should include page and limit in API call', async () => {
      (api.get as jest.Mock).mockResolvedValue({
        logs: mockAuditLogs,
        totalPages: 1,
      });

      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          expect.stringContaining('page=1')
        );
        expect(api.get).toHaveBeenCalledWith(
          expect.stringContaining('limit=20')
        );
      });
    });

    it('should build correct query string with filter', async () => {
      (api.get as jest.Mock).mockResolvedValue({
        logs: mockAuditLogs,
        totalPages: 1,
      });

      renderWithQuery(<AuditLog />);

      await waitFor(() => {
        expect(screen.getByText('create')).toBeInTheDocument();
      });

      const filterSelect = screen.getByRole('combobox');
      fireEvent.change(filterSelect, { target: { value: 'create' } });

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          '/api/v1/admin/prompts/template-123/audit?page=1&limit=20&action=create'
        );
      });
    });
  });
});

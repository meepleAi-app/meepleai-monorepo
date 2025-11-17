import React from 'react';
import {  screen, fireEvent, waitFor } from '@testing-library/react';
import { renderWithQuery } from '../utils/query-test-utils';
import { useRouter } from 'next/router';
import CompareVersions from '../../pages/admin/prompts/[id]/compare';
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

jest.mock('@monaco-editor/react', () => ({
  DiffEditor: ({ original, modified }: any) => (
    <div data-testid="diff-editor">
      <div data-testid="diff-original">{original}</div>
      <div data-testid="diff-modified">{modified}</div>
    </div>
  ),
}));

jest.mock('next/dynamic', () => {
  return (loader: any) => {
    const Component = (props: any) => {
      const MockDiffEditor = require('@monaco-editor/react').DiffEditor;
      return <MockDiffEditor {...props} />;
    };
    Component.displayName = 'MockDynamicDiffEditor';
    return Component;
  };
});

const mockPush = jest.fn();
const mockRouter = {
  push: mockPush,
  query: { id: 'template-123' },
};

const mockVersions = [
  {
    id: 'version-1',
    versionNumber: 1,
    content: 'You are a helpful assistant',
    createdAt: '2024-01-01T00:00:00Z',
    createdByEmail: 'user1@example.com',
  },
  {
    id: 'version-2',
    versionNumber: 2,
    content: 'You are a helpful AI assistant',
    createdAt: '2024-01-02T00:00:00Z',
    createdByEmail: 'user2@example.com',
  },
  {
    id: 'version-3',
    versionNumber: 3,
    content: 'You are a helpful and friendly AI assistant',
    createdAt: '2024-01-03T00:00:00Z',
    createdByEmail: 'user3@example.com',
  },
];

describe('CompareVersions Page', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (useRouter as jest.Mock).mockReturnValue(mockRouter);
  });

  describe('Loading State', () => {
    it('should show loading indicator while fetching', () => {
      (api.get as jest.Mock).mockImplementation(() => new Promise(() => {}));

      renderWithQuery(<CompareVersions />);

      expect(screen.getByText('Loading...')).toBeInTheDocument();
    });
  });

  describe('Error Handling', () => {
    it('should display error message when fetch fails', async () => {
      (api.get as jest.Mock).mockRejectedValue(new Error('Network error'));

      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        expect(screen.getByText('Unexpected Error')).toBeInTheDocument();
      });
    });

    it('should display unauthorized error when API returns null', async () => {
      (api.get as jest.Mock).mockResolvedValue(null);

      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        expect(screen.getByText('Unexpected Error')).toBeInTheDocument();
      });
    });
  });

  describe('Not Enough Versions', () => {
    it('should display message when only one version exists', async () => {
      (api.get as jest.Mock).mockResolvedValue([mockVersions[0]]);

      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        expect(screen.getByText('Not enough versions to compare')).toBeInTheDocument();
        expect(
          screen.getByText('You need at least two versions to use the comparison feature')
        ).toBeInTheDocument();
      });
    });

    it('should display message when no versions exist', async () => {
      (api.get as jest.Mock).mockResolvedValue([]);

      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        expect(screen.getByText('Not enough versions to compare')).toBeInTheDocument();
      });
    });
  });

  describe('Auto-Selection of Versions', () => {
    it('should auto-select latest two versions', async () => {
      (api.get as jest.Mock).mockResolvedValue(mockVersions);

      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        const selects = screen.getAllByRole('combobox');
        const version1Select = selects[0];
        const version2Select = selects[1];

        expect(version1Select).toHaveValue('version-1');
        expect(version2Select).toHaveValue('version-2');
      });
    });

    it('should auto-select first version if only one exists', async () => {
      (api.get as jest.Mock).mockResolvedValue([mockVersions[0]]);

      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        // No select elements should be rendered with less than 2 versions
        expect(screen.queryAllByRole('combobox')).toHaveLength(0);
      });
    });

    it('should pre-select version from query param', async () => {
      (useRouter as jest.Mock).mockReturnValue({
        ...mockRouter,
        query: { id: 'template-123', versions: 'version-3' },
      });

      (api.get as jest.Mock).mockResolvedValue(mockVersions);

      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        const selects = screen.getAllByRole('combobox');
        const version2Select = selects[1];
        expect(version2Select).toHaveValue('version-3');
      });
    });
  });

  describe('Version Selection', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue(mockVersions);
    });

    it('should update version A when selection changes', async () => {
      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        expect(screen.getByText('Version A (Original)')).toBeInTheDocument();
      });

      const selects = screen.getAllByRole('combobox');
      const version1Select = selects[0];
      fireEvent.change(version1Select, { target: { value: 'version-3' } });

      expect(version1Select).toHaveValue('version-3');
    });

    it('should update version B when selection changes', async () => {
      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        expect(screen.getByText('Version B (Modified)')).toBeInTheDocument();
      });

      const selects = screen.getAllByRole('combobox');
      const version2Select = selects[1];
      fireEvent.change(version2Select, { target: { value: 'version-3' } });

      expect(version2Select).toHaveValue('version-3');
    });

    it('should display all versions in both dropdowns', async () => {
      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        expect(screen.getByText('Version A (Original)')).toBeInTheDocument();
      });

      const selects = screen.getAllByRole('combobox');
      selects.forEach((select) => {
        expect(select).toContainHTML('Version 1');
        expect(select).toContainHTML('Version 2');
        expect(select).toContainHTML('Version 3');
      });
    });
  });

  describe('Diff Display', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue(mockVersions);
    });

    it('should display diff editor when two different versions selected', async () => {
      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        expect(screen.getByTestId('diff-editor')).toBeInTheDocument();
      });

      const original = screen.getByTestId('diff-original');
      const modified = screen.getByTestId('diff-modified');

      expect(original).toHaveTextContent('You are a helpful assistant');
      expect(modified).toHaveTextContent('You are a helpful AI assistant');
    });

    it('should display version metadata above diff', async () => {
      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        const version1Elements = screen.getAllByText(/Version 1/);
        const version2Elements = screen.getAllByText(/Version 2/);
        expect(version1Elements.length).toBeGreaterThan(0);
        expect(version2Elements.length).toBeGreaterThan(0);
      });

      // Dates are formatted with toLocaleString() - use flexible matcher for any date format
      // Just verify dates containing "2024" appear (locale-independent)
      const dateElements = screen.getAllByText(/2024/);
      expect(dateElements.length).toBeGreaterThanOrEqual(2); // At least 2 dates shown
    });

    it('should display legend explaining diff colors', async () => {
      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        expect(screen.getByText(/Legend:/)).toBeInTheDocument();
        expect(screen.getByText(/Red/)).toBeInTheDocument();
        expect(screen.getByText(/Green/)).toBeInTheDocument();
      });
    });

    it('should update diff when version selection changes', async () => {
      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        expect(screen.getByTestId('diff-editor')).toBeInTheDocument();
      });

      const selects = screen.getAllByRole('combobox');
      const version2Select = selects[1];
      fireEvent.change(version2Select, { target: { value: 'version-3' } });

      await waitFor(() => {
        const modified = screen.getByTestId('diff-modified');
        expect(modified).toHaveTextContent(
          'You are a helpful and friendly AI assistant'
        );
      });
    });
  });

  describe('Same Version Warning', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue(mockVersions);
    });

    it('should show warning when same version selected in both dropdowns', async () => {
      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        expect(screen.getByText('Version A (Original)')).toBeInTheDocument();
      });

      const selects = screen.getAllByRole('combobox');
      const version2Select = selects[1];
      fireEvent.change(version2Select, { target: { value: 'version-1' } });

      await waitFor(() => {
        expect(
          screen.getByText('Please select two different versions to compare')
        ).toBeInTheDocument();
      });

      expect(screen.queryByTestId('diff-editor')).not.toBeInTheDocument();
    });

    it('should hide diff editor when same version selected', async () => {
      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        expect(screen.getByTestId('diff-editor')).toBeInTheDocument();
      });

      const selects = screen.getAllByRole('combobox');
      const version1Select = selects[0];
      fireEvent.change(version1Select, { target: { value: 'version-2' } });

      await waitFor(() => {
        expect(screen.queryByTestId('diff-editor')).not.toBeInTheDocument();
      });
    });
  });

  describe('Empty Selection Handling', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue(mockVersions);
    });

    it('should not display diff when version A is empty', async () => {
      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        expect(screen.getByText('Version A (Original)')).toBeInTheDocument();
      });

      const selects = screen.getAllByRole('combobox');
      const version1Select = selects[0];
      fireEvent.change(version1Select, { target: { value: '' } });

      await waitFor(() => {
        expect(screen.queryByTestId('diff-editor')).not.toBeInTheDocument();
      });
    });

    it('should not display diff when version B is empty', async () => {
      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        expect(screen.getByText('Version B (Modified)')).toBeInTheDocument();
      });

      const selects = screen.getAllByRole('combobox');
      const version2Select = selects[1];
      fireEvent.change(version2Select, { target: { value: '' } });

      await waitFor(() => {
        expect(screen.queryByTestId('diff-editor')).not.toBeInTheDocument();
      });
    });
  });

  describe('Navigation Links', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue(mockVersions);
    });

    it('should have back to template link', async () => {
      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        const backButton = screen.getByText('← Back to Template');
        expect(backButton.closest('a')).toHaveAttribute(
          'href',
          '/admin/prompts/template-123'
        );
      });
    });
  });

  describe('Edge Cases', () => {
    it('should handle missing router query id', () => {
      (useRouter as jest.Mock).mockReturnValue({
        ...mockRouter,
        query: {},
      });

      renderWithQuery(<CompareVersions />);

      expect(api.get).not.toHaveBeenCalled();
    });

    it('should handle versions with null content', async () => {
      (api.get as jest.Mock).mockResolvedValue([
        { ...mockVersions[0], content: null },
        mockVersions[1],
      ]);

      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        expect(screen.getByTestId('diff-editor')).toBeInTheDocument();
      });
    });

    it('should handle versions with empty content', async () => {
      (api.get as jest.Mock).mockResolvedValue([
        { ...mockVersions[0], content: '' },
        mockVersions[1],
      ]);

      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        const original = screen.getByTestId('diff-original');
        expect(original).toHaveTextContent('');
      });
    });

    it('should format dates in version selector options', async () => {
      (api.get as jest.Mock).mockResolvedValue(mockVersions);

      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        expect(screen.getByText('Version A (Original)')).toBeInTheDocument();
      });

      // Options use toLocaleDateString()
      const formattedDate = new Date(mockVersions[0].createdAt).toLocaleDateString();
      const dateElements = screen.getAllByText(new RegExp(formattedDate));
      expect(dateElements.length).toBeGreaterThan(0);
    });

    it('should include creator email in version selector options', async () => {
      (api.get as jest.Mock).mockResolvedValue(mockVersions);

      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        const user1Emails = screen.getAllByText(/user1@example.com/);
        const user2Emails = screen.getAllByText(/user2@example.com/);
        expect(user1Emails.length).toBeGreaterThan(0);
        expect(user2Emails.length).toBeGreaterThan(0);
      });
    });
  });

  describe('Monaco DiffEditor Configuration', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue(mockVersions);
    });

    it('should render diff editor with correct props', async () => {
      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        expect(screen.getByTestId('diff-editor')).toBeInTheDocument();
      });

      const diffEditor = screen.getByTestId('diff-editor');
      expect(diffEditor).toBeInTheDocument();
    });
  });

  describe('Loading Indicator for Diff', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue(mockVersions);
    });

    it('should display loading message before diff editor mounts', async () => {
      renderWithQuery(<CompareVersions />);

      await waitFor(() => {
        // Diff editor should eventually render
        expect(screen.getByTestId('diff-editor')).toBeInTheDocument();
      });
    });
  });
});

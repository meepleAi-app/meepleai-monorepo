import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import PromptVersionCard from '../PromptVersionCard';

// Mock Next.js Link component
jest.mock('next/link', () => ({
  __esModule: true,
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

describe('PromptVersionCard', () => {
  const mockOnActivate = jest.fn();
  const mockOnCompare = jest.fn();

  const baseVersion = {
    id: 'version-1',
    templateId: 'template-1',
    versionNumber: 1,
    content: 'This is the prompt content for version 1',
    isActive: false,
    createdById: 'user-1',
    createdByEmail: 'user@example.com',
    createdAt: '2025-01-15T10:00:00Z',
  };

  const activeVersion = {
    ...baseVersion,
    id: 'version-2',
    versionNumber: 2,
    isActive: true,
  };

  const versionWithMetadata = {
    ...baseVersion,
    metadata: {
      key1: 'value1',
      key2: 'value2',
      key3: 'value3',
    },
  };

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render version card with basic information', () => {
      render(<PromptVersionCard version={baseVersion} />);

      expect(screen.getByText('Version 1')).toBeInTheDocument();
      expect(screen.getByText('user@example.com')).toBeInTheDocument();
      // Date format is locale-dependent, just check it's rendered
      expect(screen.getByText('Created at:')).toBeInTheDocument();
    });

    it('should show active badge when version is active', () => {
      render(<PromptVersionCard version={activeVersion} />);

      expect(screen.getByText('Active')).toBeInTheDocument();
    });

    it('should not show active badge when version is inactive', () => {
      render(<PromptVersionCard version={baseVersion} />);

      expect(screen.queryByText('Active')).not.toBeInTheDocument();
    });

    it('should show metadata count when metadata exists', () => {
      render(<PromptVersionCard version={versionWithMetadata} />);

      expect(screen.getByText('Metadata:')).toBeInTheDocument();
      expect(screen.getByText('3 field(s)')).toBeInTheDocument();
    });

    it('should not show metadata section when metadata is empty', () => {
      const versionWithEmptyMetadata = {
        ...baseVersion,
        metadata: {},
      };

      render(<PromptVersionCard version={versionWithEmptyMetadata} />);

      expect(screen.queryByText('Metadata:')).not.toBeInTheDocument();
    });

    it('should not show metadata section when metadata is undefined', () => {
      render(<PromptVersionCard version={baseVersion} />);

      expect(screen.queryByText('Metadata:')).not.toBeInTheDocument();
    });

    it('should truncate long content', () => {
      const longContent = 'A'.repeat(300);
      const versionWithLongContent = {
        ...baseVersion,
        content: longContent,
      };

      render(<PromptVersionCard version={versionWithLongContent} />);

      const truncatedText = screen.getByText(/\.\.\./);
      expect(truncatedText.textContent?.length).toBeLessThan(longContent.length);
    });

    it('should not truncate short content', () => {
      const shortContent = 'Short content';
      const versionWithShortContent = {
        ...baseVersion,
        content: shortContent,
      };

      render(<PromptVersionCard version={versionWithShortContent} />);

      expect(screen.getByText(shortContent)).toBeInTheDocument();
    });
  });

  describe('Action Buttons', () => {
    it('should show activate button for inactive versions when showActions is true', () => {
      render(
        <PromptVersionCard
          version={baseVersion}
          onActivate={mockOnActivate}
          showActions={true}
        />
      );

      expect(screen.getByText('Activate')).toBeInTheDocument();
    });

    it('should not show activate button for active versions', () => {
      render(
        <PromptVersionCard
          version={activeVersion}
          onActivate={mockOnActivate}
          showActions={true}
        />
      );

      expect(screen.queryByText('Activate')).not.toBeInTheDocument();
    });

    it('should show compare button when onCompare is provided', () => {
      render(
        <PromptVersionCard
          version={baseVersion}
          onCompare={mockOnCompare}
          showActions={true}
        />
      );

      expect(screen.getByText('Compare')).toBeInTheDocument();
    });

    it('should show view button', () => {
      render(<PromptVersionCard version={baseVersion} showActions={true} />);

      expect(screen.getByText('View')).toBeInTheDocument();
    });

    it('should hide all action buttons when showActions is false', () => {
      render(
        <PromptVersionCard
          version={baseVersion}
          onActivate={mockOnActivate}
          onCompare={mockOnCompare}
          showActions={false}
        />
      );

      expect(screen.queryByText('Activate')).not.toBeInTheDocument();
      expect(screen.queryByText('Compare')).not.toBeInTheDocument();
      expect(screen.queryByText('View')).not.toBeInTheDocument();
    });

    it('should default showActions to true', () => {
      render(<PromptVersionCard version={baseVersion} onActivate={mockOnActivate} />);

      expect(screen.getByText('Activate')).toBeInTheDocument();
    });
  });

  describe('User Interactions', () => {
    it('should call onActivate when activate button is clicked', async () => {
      const user = userEvent.setup();

      render(
        <PromptVersionCard
          version={baseVersion}
          onActivate={mockOnActivate}
          showActions={true}
        />
      );

      const activateButton = screen.getByText('Activate');
      await user.click(activateButton);

      expect(mockOnActivate).toHaveBeenCalledTimes(1);
    });

    it('should call onCompare when compare button is clicked', async () => {
      const user = userEvent.setup();

      render(
        <PromptVersionCard
          version={baseVersion}
          onCompare={mockOnCompare}
          showActions={true}
        />
      );

      const compareButton = screen.getByText('Compare');
      await user.click(compareButton);

      expect(mockOnCompare).toHaveBeenCalledTimes(1);
    });

    it('should navigate to version detail page when view button is clicked', () => {
      render(<PromptVersionCard version={baseVersion} showActions={true} />);

      const viewButton = screen.getByText('View');
      const link = viewButton.closest('a');

      expect(link).toHaveAttribute(
        'href',
        `/admin/prompts/${baseVersion.templateId}/versions/${baseVersion.id}`
      );
    });
  });

  describe('Content Display', () => {
    it('should display created by information', () => {
      render(<PromptVersionCard version={baseVersion} />);

      expect(screen.getByText('Created by:')).toBeInTheDocument();
      expect(screen.getByText('user@example.com')).toBeInTheDocument();
    });

    it('should display created at timestamp', () => {
      render(<PromptVersionCard version={baseVersion} />);

      expect(screen.getByText('Created at:')).toBeInTheDocument();
      // Date formatting is locale-dependent, just check it exists
      const dateElement = screen.getByText(/Created at:/).nextSibling;
      expect(dateElement).toBeTruthy();
    });

    it('should format date using toLocaleString', () => {
      render(<PromptVersionCard version={baseVersion} />);

      const expectedDate = new Date(baseVersion.createdAt).toLocaleString();
      expect(screen.getByText(expectedDate)).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle version without onActivate callback', async () => {
      const user = userEvent.setup();

      render(<PromptVersionCard version={baseVersion} showActions={true} />);

      // Activate button should not be shown if no callback provided
      expect(screen.queryByText('Activate')).not.toBeInTheDocument();
    });

    it('should handle version without onCompare callback', () => {
      render(<PromptVersionCard version={baseVersion} showActions={true} />);

      // Compare button should not be shown if no callback provided
      expect(screen.queryByText('Compare')).not.toBeInTheDocument();
    });

    it('should handle empty content string', () => {
      const versionWithEmptyContent = {
        ...baseVersion,
        content: '',
      };

      render(<PromptVersionCard version={versionWithEmptyContent} />);

      // Should not crash, component should render
      expect(screen.getByText('Version 1')).toBeInTheDocument();
    });

    it('should handle very high version numbers', () => {
      const highVersionNumber = {
        ...baseVersion,
        versionNumber: 9999,
      };

      render(<PromptVersionCard version={highVersionNumber} />);

      expect(screen.getByText('Version 9999')).toBeInTheDocument();
    });

    it('should apply hover styles to card', () => {
      const { container } = render(<PromptVersionCard version={baseVersion} />);

      const card = container.firstChild as HTMLElement;
      expect(card).toHaveClass('hover:shadow-md');
      expect(card).toHaveClass('transition-shadow');
    });

    it('should truncate content at exactly 150 characters', () => {
      const content = 'A'.repeat(150);
      const versionWithExactLength = {
        ...baseVersion,
        content,
      };

      render(<PromptVersionCard version={versionWithExactLength} />);

      expect(screen.getByText(content)).toBeInTheDocument();
    });

    it('should truncate content at 151 characters', () => {
      const content = 'A'.repeat(151);
      const versionWithLongContent = {
        ...baseVersion,
        content,
      };

      render(<PromptVersionCard version={versionWithLongContent} />);

      const truncatedText = screen.getByText(/\.\.\./);
      expect(truncatedText.textContent).toContain('...');
    });
  });

  describe('Styling', () => {
    it('should apply correct styles for active badge', () => {
      render(<PromptVersionCard version={activeVersion} />);

      const badge = screen.getByText('Active');
      expect(badge).toHaveClass('bg-green-100');
      expect(badge).toHaveClass('text-green-800');
    });

    it('should apply correct button styles for activate button', () => {
      render(
        <PromptVersionCard
          version={baseVersion}
          onActivate={mockOnActivate}
          showActions={true}
        />
      );

      const activateButton = screen.getByText('Activate');
      expect(activateButton).toHaveClass('bg-blue-600');
      expect(activateButton).toHaveClass('text-white');
    });

    it('should apply correct button styles for compare button', () => {
      render(
        <PromptVersionCard
          version={baseVersion}
          onCompare={mockOnCompare}
          showActions={true}
        />
      );

      const compareButton = screen.getByText('Compare');
      expect(compareButton).toHaveClass('bg-gray-200');
      expect(compareButton).toHaveClass('text-gray-700');
    });

    it('should apply correct button styles for view button', () => {
      render(<PromptVersionCard version={baseVersion} showActions={true} />);

      const viewButton = screen.getByText('View');
      expect(viewButton).toHaveClass('bg-gray-100');
      expect(viewButton).toHaveClass('text-gray-700');
    });
  });
});

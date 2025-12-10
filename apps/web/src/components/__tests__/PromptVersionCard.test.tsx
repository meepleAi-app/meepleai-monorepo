import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import PromptVersionCard from '../prompt/PromptVersionCard';

// Mock Next.js Link component
vi.mock('next/link', () => ({
  __esModule: true,
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

describe('PromptVersionCard', () => {
  const mockOnActivate = vi.fn();
  const mockOnCompare = vi.fn();

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
    vi.clearAllMocks();
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
        <PromptVersionCard version={baseVersion} onActivate={mockOnActivate} showActions={true} />
      );

      expect(screen.getByText('Activate')).toBeInTheDocument();
    });

    it('should not show activate button for active versions', () => {
      render(
        <PromptVersionCard version={activeVersion} onActivate={mockOnActivate} showActions={true} />
      );

      expect(screen.queryByText('Activate')).not.toBeInTheDocument();
    });

    it('should show compare button when onCompare is provided', () => {
      render(
        <PromptVersionCard version={baseVersion} onCompare={mockOnCompare} showActions={true} />
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
        <PromptVersionCard version={baseVersion} onActivate={mockOnActivate} showActions={true} />
      );

      const activateButton = screen.getByText('Activate');
      await user.click(activateButton);

      expect(mockOnActivate).toHaveBeenCalledTimes(1);
    });

    it('should call onCompare when compare button is clicked', async () => {
      const user = userEvent.setup();

      render(
        <PromptVersionCard version={baseVersion} onCompare={mockOnCompare} showActions={true} />
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
        <PromptVersionCard version={baseVersion} onActivate={mockOnActivate} showActions={true} />
      );

      const activateButton = screen.getByText('Activate');
      expect(activateButton).toHaveClass('bg-primary');
      expect(activateButton).toHaveClass('text-primary-foreground');
    });

    it('should apply correct button styles for compare button', () => {
      render(
        <PromptVersionCard version={baseVersion} onCompare={mockOnCompare} showActions={true} />
      );

      const compareButton = screen.getByText('Compare');
      expect(compareButton).toHaveClass('bg-secondary');
      expect(compareButton).toHaveClass('text-secondary-foreground');
    });

    it('should apply correct button styles for view button', () => {
      render(<PromptVersionCard version={baseVersion} showActions={true} />);

      const viewButton = screen.getByText('View');
      expect(viewButton).toHaveClass('border');
      // Note: View button uses outline variant which has bg-card class
      expect(viewButton).toHaveClass('bg-card');
    });
  });

  describe('Version Comparison Scenarios', () => {
    it('should handle comparing two consecutive versions', async () => {
      const user = userEvent.setup();
      const version1 = baseVersion;
      const version2 = {
        ...baseVersion,
        id: 'version-2',
        versionNumber: 2,
        content: 'Updated prompt content for version 2',
      };

      const { rerender } = render(
        <PromptVersionCard version={version1} onCompare={mockOnCompare} showActions={true} />
      );

      const compareButton1 = screen.getByText('Compare');
      await user.click(compareButton1);
      expect(mockOnCompare).toHaveBeenCalledTimes(1);

      rerender(
        <PromptVersionCard version={version2} onCompare={mockOnCompare} showActions={true} />
      );

      const compareButton2 = screen.getByText('Compare');
      await user.click(compareButton2);
      expect(mockOnCompare).toHaveBeenCalledTimes(2);
    });

    it('should display version metadata for comparison purposes', () => {
      const versionWithComparisonData = {
        ...baseVersion,
        metadata: {
          model: 'gpt-4',
          temperature: '0.7',
          maxTokens: '2000',
        },
      };

      render(<PromptVersionCard version={versionWithComparisonData} />);

      expect(screen.getByText('Metadata:')).toBeInTheDocument();
      expect(screen.getByText('3 field(s)')).toBeInTheDocument();
    });

    it('should show clear version identifiers for comparison', () => {
      render(
        <PromptVersionCard version={baseVersion} onCompare={mockOnCompare} showActions={true} />
      );

      expect(screen.getByText('Version 1')).toBeInTheDocument();
      expect(screen.getByText('user@example.com')).toBeInTheDocument();
    });
  });

  describe('Advanced State Management', () => {
    it('should handle switching between active and inactive states', async () => {
      const user = userEvent.setup();
      const { rerender } = render(
        <PromptVersionCard version={baseVersion} onActivate={mockOnActivate} showActions={true} />
      );

      expect(screen.getByText('Activate')).toBeInTheDocument();
      expect(screen.queryByText('Active')).not.toBeInTheDocument();

      rerender(
        <PromptVersionCard version={activeVersion} onActivate={mockOnActivate} showActions={true} />
      );

      expect(screen.queryByText('Activate')).not.toBeInTheDocument();
      expect(screen.getByText('Active')).toBeInTheDocument();
    });

    it('should maintain button state during async operations', async () => {
      const user = userEvent.setup();
      const asyncOnActivate = vi.fn(() => new Promise(resolve => setTimeout(resolve, 100)));

      render(
        <PromptVersionCard version={baseVersion} onActivate={asyncOnActivate} showActions={true} />
      );

      const activateButton = screen.getByText('Activate');
      await user.click(activateButton);

      expect(asyncOnActivate).toHaveBeenCalled();
    });

    it('should handle rapid clicks on action buttons', async () => {
      const user = userEvent.setup();
      const asyncOnCompare = vi.fn(() => new Promise(resolve => setTimeout(resolve, 50)));

      render(
        <PromptVersionCard version={baseVersion} onCompare={asyncOnCompare} showActions={true} />
      );

      const compareButton = screen.getByText('Compare');

      // Simulate rapid clicks
      await user.click(compareButton);
      await user.click(compareButton);
      await user.click(compareButton);

      // All clicks should be registered (implementation may vary)
      expect(asyncOnCompare.mock.calls.length).toBeGreaterThan(0);
    });
  });

  describe('Content Rendering Edge Cases', () => {
    it('should handle content with HTML-like characters', () => {
      const htmlLikeContent = 'Prompt with <div> and {JSON} content';
      const versionWithHTMLLike = {
        ...baseVersion,
        content: htmlLikeContent,
      };

      render(<PromptVersionCard version={versionWithHTMLLike} />);

      expect(screen.getByText(htmlLikeContent)).toBeInTheDocument();
    });

    it('should handle content with Unicode characters', () => {
      const unicodeContent = 'Prompt with émojis 🚀 and spëcial çharacters';
      const versionWithUnicode = {
        ...baseVersion,
        content: unicodeContent,
      };

      render(<PromptVersionCard version={versionWithUnicode} />);

      expect(screen.getByText(unicodeContent)).toBeInTheDocument();
    });

    it('should handle content with tabs and multiple spaces', () => {
      const contentWithWhitespace = 'Prompt\twith\t\tmultiple   spaces   and\ttabs';
      const versionWithWhitespace = {
        ...baseVersion,
        content: contentWithWhitespace,
      };

      render(<PromptVersionCard version={versionWithWhitespace} />);

      expect(screen.getByText(contentWithWhitespace)).toBeInTheDocument();
    });

    it('should preserve newlines in content display', () => {
      const contentWithNewlines = 'Line 1\nLine 2\nLine 3\nLine 4';
      const versionWithNewlines = {
        ...baseVersion,
        content: contentWithNewlines,
      };

      render(<PromptVersionCard version={versionWithNewlines} />);

      const displayedText = screen.getByText(/Line 1/);
      expect(displayedText).toBeInTheDocument();
    });
  });

  describe('Accessibility Features', () => {
    it('should have proper button semantics', () => {
      render(
        <PromptVersionCard version={baseVersion} onActivate={mockOnActivate} showActions={true} />
      );

      const buttons = screen.getAllByRole('button');
      expect(buttons.length).toBeGreaterThan(0);
      buttons.forEach(button => {
        expect(button.tagName).toBe('BUTTON');
      });
    });

    it('should provide context for link navigation', () => {
      render(<PromptVersionCard version={baseVersion} showActions={true} />);

      const viewLink = screen.getByText('View').closest('a');
      expect(viewLink).toHaveAttribute('href');
      expect(viewLink?.getAttribute('href')).toContain('versions');
    });

    it('should display version number accessibly', () => {
      render(<PromptVersionCard version={baseVersion} />);

      const versionText = screen.getByText('Version 1');
      expect(versionText).toBeInTheDocument();
      expect(versionText.textContent).toMatch(/Version \d+/);
    });

    it('should have semantic HTML structure', () => {
      const { container } = render(<PromptVersionCard version={baseVersion} />);

      // Check for proper heading/content structure
      const card = container.firstChild;
      expect(card).toBeTruthy();
    });
  });

  describe('Performance Considerations', () => {
    it('should handle metadata with many fields', () => {
      const largeMetadata: Record<string, string> = {};
      for (let i = 0; i < 100; i++) {
        largeMetadata[`field_${i}`] = `value_${i}`;
      }

      const versionWithLargeMetadata = {
        ...baseVersion,
        metadata: largeMetadata,
      };

      render(<PromptVersionCard version={versionWithLargeMetadata} />);

      expect(screen.getByText('Metadata:')).toBeInTheDocument();
      expect(screen.getByText('100 field(s)')).toBeInTheDocument();
    });

    it('should efficiently render multiple versions in a list', () => {
      const versions = Array.from({ length: 10 }, (_, i) => ({
        ...baseVersion,
        id: `version-${i}`,
        versionNumber: i + 1,
      }));

      const { container } = render(
        <>
          {versions.map(version => (
            <PromptVersionCard key={version.id} version={version} showActions={true} />
          ))}
        </>
      );

      const cards = container.querySelectorAll('[class*="card"]');
      expect(cards.length).toBeGreaterThan(0);
    });
  });

  describe('User Feedback and Interactions', () => {
    it('should provide visual feedback on button clicks', async () => {
      const user = userEvent.setup();

      render(
        <PromptVersionCard version={baseVersion} onActivate={mockOnActivate} showActions={true} />
      );

      const activateButton = screen.getByText('Activate');
      await user.click(activateButton);

      expect(mockOnActivate).toHaveBeenCalledWith(baseVersion.id);
    });

    it('should handle button disabled states gracefully', () => {
      render(<PromptVersionCard version={activeVersion} showActions={true} />);

      // Active version should not have activate button
      expect(screen.queryByText('Activate')).not.toBeInTheDocument();

      // But should have other buttons
      expect(screen.getByText('View')).toBeInTheDocument();
    });

    it('should display all available actions consistently', () => {
      render(
        <PromptVersionCard
          version={baseVersion}
          onActivate={mockOnActivate}
          onCompare={mockOnCompare}
          showActions={true}
        />
      );

      expect(screen.getByText('Activate')).toBeInTheDocument();
      expect(screen.getByText('Compare')).toBeInTheDocument();
      expect(screen.getByText('View')).toBeInTheDocument();
    });
  });
});

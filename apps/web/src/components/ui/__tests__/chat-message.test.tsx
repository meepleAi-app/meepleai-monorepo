/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  ChatMessage,
  ConfidenceBadge,
  ChatCitationLink,
  TypingIndicator,
  type Citation,
} from '../meeple/chat-message';

// ============================================================================
// Helper Functions
// ============================================================================

const mockCitation: Citation = {
  id: 'cite-1',
  label: 'Regolamento',
  page: 5,
};

// ============================================================================
// ChatMessage Component Tests
// ============================================================================

describe('ChatMessage', () => {
  describe('Basic Rendering', () => {
    it('renders assistant message with correct layout', () => {
      render(<ChatMessage role="assistant" content="Test AI message" confidence={95} />);

      const article = screen.getByRole('article', { name: /AI message/i });
      expect(article).toBeInTheDocument();
      expect(screen.getByText('Test AI message')).toBeInTheDocument();
    });

    it('renders user message with correct layout', () => {
      render(<ChatMessage role="user" content="Test user message" avatar={{ fallback: 'U' }} />);

      const article = screen.getByRole('article', { name: /User message/i });
      expect(article).toBeInTheDocument();
      expect(screen.getByText('Test user message')).toBeInTheDocument();
    });

    it('renders with custom className', () => {
      const { container } = render(
        <ChatMessage role="assistant" content="Test" className="custom-class" />
      );

      expect(container.firstChild).toHaveClass('custom-class');
    });
  });

  describe('Avatar Rendering', () => {
    it('renders MeepleAvatar for assistant messages', () => {
      render(<ChatMessage role="assistant" content="Test" confidence={90} />);

      const avatar = screen.getByRole('img', { name: /AI assistant/i });
      expect(avatar).toBeInTheDocument();
    });

    it('renders standard Avatar for user messages', () => {
      render(
        <ChatMessage
          role="user"
          content="Test"
          avatar={{ src: 'https://example.com/avatar.jpg', fallback: 'JD' }}
        />
      );

      // Avatar fallback is rendered (image loading fails in test env)
      expect(screen.getByText('JD')).toBeInTheDocument();
    });

    it('renders avatar fallback when no image provided', () => {
      render(<ChatMessage role="user" content="Test" avatar={{ fallback: 'U' }} />);

      expect(screen.getByText('U')).toBeInTheDocument();
    });

    it('renders default "U" fallback when no avatar prop', () => {
      render(<ChatMessage role="user" content="Test" />);

      expect(screen.getByText('U')).toBeInTheDocument();
    });
  });

  describe('Confidence Badge', () => {
    it('renders high confidence badge (green) for ≥85%', () => {
      render(<ChatMessage role="assistant" content="Test" confidence={95} />);

      const badge = screen.getByLabelText('High confidence: 95%');
      expect(badge).toBeInTheDocument();
      expect(badge).toHaveClass('bg-green-500');
      expect(badge).toHaveTextContent('95%');
    });

    it('renders medium confidence badge (yellow) for 70-84%', () => {
      render(<ChatMessage role="assistant" content="Test" confidence={75} />);

      const badge = screen.getByLabelText('Medium confidence: 75%');
      expect(badge).toHaveClass('bg-yellow-500');
      expect(badge).toHaveTextContent('75%');
    });

    it('renders low confidence badge (red) for <70%', () => {
      render(<ChatMessage role="assistant" content="Test" confidence={55} />);

      const badge = screen.getByLabelText('Low confidence: 55%');
      expect(badge).toHaveClass('bg-red-500');
      expect(badge).toHaveTextContent('55%');
    });

    it('does not render confidence badge when not provided', () => {
      render(<ChatMessage role="assistant" content="Test" />);

      expect(screen.queryByLabelText(/Confidence:/)).not.toBeInTheDocument();
    });

    it('does not render confidence badge for user messages', () => {
      render(<ChatMessage role="user" content="Test" avatar={{ fallback: 'U' }} />);

      expect(screen.queryByLabelText(/Confidence:/)).not.toBeInTheDocument();
    });
  });

  describe('Citations', () => {
    it('renders citation links for assistant messages', () => {
      const citations: Citation[] = [
        { id: '1', label: 'Doc A', page: 5 },
        { id: '2', label: 'Doc B', page: 10 },
      ];

      render(<ChatMessage role="assistant" content="Test" confidence={90} citations={citations} />);

      expect(screen.getByLabelText('Citation: Doc A page 5')).toBeInTheDocument();
      expect(screen.getByLabelText('Citation: Doc B page 10')).toBeInTheDocument();
    });

    it('does not render citations when array is empty', () => {
      render(<ChatMessage role="assistant" content="Test" confidence={90} citations={[]} />);

      expect(screen.queryByRole('list')).not.toBeInTheDocument();
    });

    it('calls onCitationClick when citation is clicked', async () => {
      const user = userEvent.setup();
      const handleClick = vi.fn();

      render(
        <ChatMessage
          role="assistant"
          content="Test"
          confidence={90}
          citations={[mockCitation]}
          onCitationClick={handleClick}
        />
      );

      const citationLink = screen.getByLabelText('Citation: Regolamento page 5');
      await user.click(citationLink);

      expect(handleClick).toHaveBeenCalledTimes(1);
      expect(handleClick).toHaveBeenCalledWith('cite-1', 5);
    });

    it('does not render citations for user messages', () => {
      render(
        <ChatMessage
          role="user"
          content="Test"
          avatar={{ fallback: 'U' }}
          // @ts-expect-error - Testing invalid prop
          citations={[mockCitation]}
        />
      );

      expect(screen.queryByRole('list')).not.toBeInTheDocument();
    });
  });

  describe('Typing Indicator', () => {
    it('renders typing indicator when isTyping is true', () => {
      render(<ChatMessage role="assistant" content="" isTyping />);

      expect(screen.getByLabelText('AI is typing')).toBeInTheDocument();
      expect(screen.queryByText('Test')).not.toBeInTheDocument();
    });

    it('does not render content when typing', () => {
      render(<ChatMessage role="assistant" content="This should not show" isTyping />);

      expect(screen.queryByText('This should not show')).not.toBeInTheDocument();
    });

    it('does not render confidence badge when typing', () => {
      render(<ChatMessage role="assistant" content="" confidence={95} isTyping />);

      expect(screen.queryByLabelText(/Confidence:/)).not.toBeInTheDocument();
    });

    it('does not render citations when typing', () => {
      render(<ChatMessage role="assistant" content="" citations={[mockCitation]} isTyping />);

      expect(screen.queryByRole('list')).not.toBeInTheDocument();
    });
  });

  describe('Timestamp', () => {
    it('renders timestamp when provided', () => {
      const timestamp = new Date('2025-11-30T14:30:00');
      render(<ChatMessage role="assistant" content="Test" timestamp={timestamp} />);

      expect(screen.getByText('14:30')).toBeInTheDocument();
    });

    it('renders timestamp from string', () => {
      render(<ChatMessage role="assistant" content="Test" timestamp="2025-11-30T10:15:00" />);

      expect(screen.getByText('10:15')).toBeInTheDocument();
    });

    it('does not render timestamp when not provided', () => {
      render(<ChatMessage role="assistant" content="Test" />);

      expect(screen.queryByText(/\d{2}:\d{2}/)).not.toBeInTheDocument();
    });

    it('does not render timestamp when typing', () => {
      render(<ChatMessage role="assistant" content="" timestamp={new Date()} isTyping />);

      expect(screen.queryByText(/\d{2}:\d{2}/)).not.toBeInTheDocument();
    });
  });

  describe('Content Rendering', () => {
    it('preserves whitespace and line breaks', () => {
      const multilineContent = 'Line 1\nLine 2\nLine 3';
      render(<ChatMessage role="assistant" content={multilineContent} />);

      const contentElement = screen.getByText(/Line 1/);
      expect(contentElement).toHaveClass('whitespace-pre-wrap');
    });

    it('handles long content properly', () => {
      const longContent = 'A'.repeat(500);
      render(<ChatMessage role="assistant" content={longContent} />);

      expect(screen.getByText(longContent)).toBeInTheDocument();
    });

    it('handles empty content', () => {
      render(<ChatMessage role="assistant" content="" />);

      expect(screen.getByRole('article')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has correct ARIA role for message container', () => {
      render(<ChatMessage role="assistant" content="Test" />);

      expect(screen.getByRole('article', { name: /AI message/i })).toBeInTheDocument();
    });

    it('has correct ARIA label for user messages', () => {
      render(<ChatMessage role="user" content="Test" avatar={{ fallback: 'U' }} />);

      expect(screen.getByRole('article', { name: /User message/i })).toBeInTheDocument();
    });

    it('citation links are keyboard accessible', async () => {
      const user = userEvent.setup();
      const handleClick = vi.fn();

      render(
        <ChatMessage
          role="assistant"
          content="Test"
          citations={[mockCitation]}
          onCitationClick={handleClick}
        />
      );

      const citationLink = screen.getByLabelText('Citation: Regolamento page 5');
      citationLink.focus();
      await user.keyboard('{Enter}');

      expect(handleClick).toHaveBeenCalledWith('cite-1', 5);
    });

    it('citation links respond to Space key', async () => {
      const user = userEvent.setup();
      const handleClick = vi.fn();

      render(
        <ChatMessage
          role="assistant"
          content="Test"
          citations={[mockCitation]}
          onCitationClick={handleClick}
        />
      );

      const citationLink = screen.getByLabelText('Citation: Regolamento page 5');
      citationLink.focus();
      await user.keyboard(' ');

      expect(handleClick).toHaveBeenCalledWith('cite-1', 5);
    });
  });
});

// ============================================================================
// ConfidenceBadge Component Tests
// ============================================================================

describe('ConfidenceBadge', () => {
  it('renders high confidence with green color', () => {
    render(<ConfidenceBadge confidence={95} />);

    const badge = screen.getByLabelText('High confidence: 95%');
    expect(badge).toHaveClass('bg-green-500');
    expect(badge).toHaveTextContent('95%');
  });

  it('renders medium confidence with yellow color', () => {
    render(<ConfidenceBadge confidence={75} />);

    const badge = screen.getByLabelText('Medium confidence: 75%');
    expect(badge).toHaveClass('bg-yellow-500');
  });

  it('renders low confidence with red color', () => {
    render(<ConfidenceBadge confidence={55} />);

    const badge = screen.getByLabelText('Low confidence: 55%');
    expect(badge).toHaveClass('bg-red-500');
  });

  it('applies custom className', () => {
    render(<ConfidenceBadge confidence={90} className="custom-badge" />);

    expect(screen.getByLabelText('High confidence: 90%')).toHaveClass('custom-badge');
  });
});

// ============================================================================
// ChatCitationLink Component Tests
// ============================================================================

describe('ChatCitationLink', () => {
  it('renders citation with label and page', () => {
    render(<ChatCitationLink citation={mockCitation} />);

    expect(screen.getByLabelText('Citation: Regolamento page 5')).toBeInTheDocument();
    expect(screen.getByText('Regolamento p.5')).toBeInTheDocument();
  });

  it('renders citation without page number', () => {
    const citation: Citation = { id: '1', label: 'FAQ' };
    render(<ChatCitationLink citation={citation} />);

    expect(screen.getByLabelText('Citation: FAQ')).toBeInTheDocument();
    expect(screen.getByText('FAQ')).toBeInTheDocument();
  });

  it('calls onClick handler when clicked', async () => {
    const user = userEvent.setup();
    const handleClick = vi.fn();

    render(<ChatCitationLink citation={mockCitation} onClick={handleClick} />);

    await user.click(screen.getByLabelText('Citation: Regolamento page 5'));

    expect(handleClick).toHaveBeenCalledWith('cite-1', 5);
  });

  it('handles keyboard Enter key', async () => {
    const user = userEvent.setup();
    const handleClick = vi.fn();

    render(<ChatCitationLink citation={mockCitation} onClick={handleClick} />);

    const link = screen.getByLabelText('Citation: Regolamento page 5');
    link.focus();
    await user.keyboard('{Enter}');

    expect(handleClick).toHaveBeenCalledWith('cite-1', 5);
  });

  it('handles keyboard Space key', async () => {
    const user = userEvent.setup();
    const handleClick = vi.fn();

    render(<ChatCitationLink citation={mockCitation} onClick={handleClick} />);

    const link = screen.getByLabelText('Citation: Regolamento page 5');
    link.focus();
    await user.keyboard(' ');

    expect(handleClick).toHaveBeenCalledWith('cite-1', 5);
  });

  it('applies custom className', () => {
    render(<ChatCitationLink citation={mockCitation} className="custom-link" />);

    expect(screen.getByLabelText('Citation: Regolamento page 5')).toHaveClass('custom-link');
  });

  it('has correct focus styles', () => {
    render(<ChatCitationLink citation={mockCitation} />);

    const link = screen.getByLabelText('Citation: Regolamento page 5');
    expect(link).toHaveClass('focus:ring-2', 'focus:ring-orange-400');
  });
});

// ============================================================================
// TypingIndicator Component Tests
// ============================================================================

describe('TypingIndicator', () => {
  it('renders three animated dots', () => {
    const { container } = render(<TypingIndicator />);

    const dots = container.querySelectorAll('.animate-bounce');
    expect(dots).toHaveLength(3);
  });

  it('has correct ARIA attributes', () => {
    render(<TypingIndicator />);

    const indicator = screen.getByLabelText('AI is typing');
    expect(indicator).toHaveAttribute('aria-live', 'polite');
  });

  it('applies custom className', () => {
    render(<TypingIndicator className="custom-indicator" />);

    expect(screen.getByLabelText('AI is typing')).toHaveClass('custom-indicator');
  });
});

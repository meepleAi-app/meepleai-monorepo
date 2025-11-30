/**
 * Tests for Message component
 * Auto-generated baseline tests - Issue #992
 * NOTE: Comprehensive tests are in __tests__/components/chat/Message.test.tsx
 */

import { render, screen } from '@testing-library/react';
import { Message } from '../Message';
import { Message as MessageType } from '../../../types';

// Mock AuthProvider
vi.mock('../../../components/auth/AuthProvider', () => ({
  AuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useAuth: () => ({
    user: { id: '1', email: 'test@example.com', displayName: 'Test User' },
    isAuthenticated: true,
    isLoading: false,
  }),
}));

// Mock ChatProvider context
const mockUseChatContext = vi.fn();
vi.mock('../../../components/chat/ChatProvider', () => ({
  useChatContext: () => mockUseChatContext(),
}));

const createMessage = (overrides?: Partial<MessageType>): MessageType => ({
  id: 'msg-1',
  role: 'user',
  content: 'Test message',
  timestamp: new Date('2025-01-10T10:00:00Z'),
  isDeleted: false,
  ...overrides,
});

describe('Message', () => {
  beforeEach(() => {
    mockUseChatContext.mockReturnValue({
      editingMessageId: null,
      startEditMessage: vi.fn(),
      deleteMessage: vi.fn(),
      setMessageFeedback: vi.fn(),
      loading: { sending: false, updating: false, deleting: false },
      setInputValue: vi.fn(),
    });
  });

  describe('Rendering', () => {
    it('should render without crashing', () => {
      const message = createMessage();
      render(<Message message={message} isUser={true} />);
      expect(screen.getByLabelText('Your message')).toBeInTheDocument();
    });

    it('should render with default props', () => {
      const message = createMessage();
      const { container } = render(<Message message={message} isUser={true} />);
      expect(container.firstChild).toBeInTheDocument();
    });
  });

  describe('Props', () => {
    it('should accept and render with custom props', () => {
      const message = createMessage({ content: 'Custom content' });
      render(<Message message={message} isUser={false} />);
      expect(screen.getByText('Custom content')).toBeInTheDocument();
    });
  });

  describe('Interactions', () => {
    it('should display message content', () => {
      const message = createMessage({ content: 'Test interaction content' });
      render(<Message message={message} isUser={true} />);
      expect(screen.getByText('Test interaction content')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have accessible label for user messages', () => {
      const message = createMessage();
      render(<Message message={message} isUser={true} />);
      expect(screen.getByLabelText('Your message')).toBeInTheDocument();
    });
  });
});

/**
 * Tests for Message component
 * Auto-generated baseline tests - Issue #992
 * NOTE: Comprehensive tests are in __tests__/components/chat/Message.test.tsx
 */

import { screen } from '@testing-library/react';
import { Message } from '../Message';
import { Message as MessageType } from '../../../types';
import { renderWithChatStore } from '@/__tests__/utils/zustand-test-utils';

// Mock AuthProvider
vi.mock('../../../components/auth/AuthProvider', () => ({
  AuthProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useAuth: () => ({
    user: { id: '1', email: 'test@example.com', displayName: 'Test User' },
    loading: false,
    error: null,
    login: vi.fn(),
    register: vi.fn(),
    logout: vi.fn(),
    refreshUser: vi.fn(),
    clearError: vi.fn(),
  }),
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
  describe('Rendering', () => {
    it('should render without crashing', () => {
      const message = createMessage();
      renderWithChatStore(<Message message={message} isUser={true} />, {
        initialState: {
          editingMessageId: null,
          loading: { sending: false, updating: false, deleting: false },
        },
      });
      expect(screen.getByLabelText('Your message')).toBeInTheDocument();
    });

    it('should render with default props', () => {
      const message = createMessage();
      renderWithChatStore(<Message message={message} isUser={true} />, {
        initialState: {
          editingMessageId: null,
          loading: { sending: false, updating: false, deleting: false },
        },
      });
      expect(screen.getByLabelText('Your message')).toBeInTheDocument();
    });
  });
});

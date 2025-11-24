import type { RuleSpecComment } from '@/lib/api';

// Mock MentionInput component
export interface MockMentionInputProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
}

// Mock dialog hooks
export const mockConfirm = vi.fn();
export const mockAlert = vi.fn();

// Mock modules setup
export function setupMocks() {
  vi.mock('../chat/MentionInput', () => ({
    MentionInput: ({ value, onChange, placeholder, disabled }: MockMentionInputProps) => (
      <textarea
        data-testid="mention-input"
        value={value}
        onChange={e => onChange(e.target.value)}
        placeholder={placeholder}
        disabled={disabled}
      />
    ),
  }));

  vi.mock('@/hooks/useConfirmDialog', () => ({
    useConfirmDialog: () => ({
      confirm: mockConfirm,
      ConfirmDialogComponent: () => null,
    }),
  }));

  vi.mock('@/hooks/useAlertDialog', () => ({
    useAlertDialog: () => ({
      alert: mockAlert,
      AlertDialogComponent: () => null,
    }),
  }));
}

// Mock comment data
export const mockComment: RuleSpecComment = {
  id: 'comment-1',
  gameId: 'chess',
  version: 'v1',
  atomId: 'atom-1',
  lineNumber: null,
  lineContext: null,
  parentCommentId: null,
  replies: [],
  userId: 'user-1',
  userDisplayName: 'John Doe',
  commentText: 'This is a test comment',
  isResolved: false,
  resolvedByUserId: null,
  resolvedByDisplayName: null,
  resolvedAt: null,
  mentionedUserIds: [],
  createdAt: '2025-10-15T12:00:00Z',
  updatedAt: null,
};

// Mock callback functions
export function createMockCallbacks() {
  return {
    mockOnEdit: vi.fn(),
    mockOnDelete: vi.fn(),
    mockOnReply: vi.fn(),
    mockOnResolve: vi.fn(),
    mockOnUnresolve: vi.fn(),
  };
}

// Common setup for beforeEach
export function setupBeforeEach() {
  vi.clearAllMocks();
  mockConfirm.mockResolvedValue(true);
  mockAlert.mockResolvedValue(undefined);
}

// Helper to create comment variants
export function createCommentVariant(overrides: Partial<RuleSpecComment>): RuleSpecComment {
  return {
    ...mockComment,
    ...overrides,
  };
}

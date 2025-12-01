/**
 * Tests for ChatProvider component (DEPRECATED)
 * Auto-generated baseline tests - Issue #992
 *
 * NOTE: ChatProvider is deprecated in favor of Zustand ChatStoreProvider
 * These tests are kept for backwards compatibility but should be removed
 * in a future cleanup. See Issue #1083 for Zustand migration.
 */

import { screen } from '@testing-library/react';
import { ChatProvider } from '../ChatProvider';
import { renderWithChatStore } from '@/__tests__/utils/zustand-test-utils';

// Mock GameProvider dependency
vi.mock('@/components/game/GameProvider', () => ({
  useGame: () => ({
    selectedGameId: null,
    games: [],
    loading: false,
  }),
}));

describe('ChatProvider (DEPRECATED)', () => {
  describe('Rendering', () => {
    it('should render children', () => {
      renderWithChatStore(
        <ChatProvider>
          <div>Test Content</div>
        </ChatProvider>,
        {
          initialState: {},
        }
      );
      expect(screen.getByText('Test Content')).toBeInTheDocument();
    });
  });
});

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useRouter } from 'next/router';
import RuleSpecEditor from '../../pages/editor';
import { api } from '../../pages/../lib/api';
import { getEditorTextarea, waitForEditorReady } from '../fixtures/test-helpers';

jest.mock('../../lib/api', () => ({
  api: {
    get: jest.fn(),
    put: jest.fn(),
  },
}));

jest.mock('next/router', () => ({
  useRouter: jest.fn(),
}));

const mockApi = api as jest.Mocked<typeof api>;
const mockUseRouter = useRouter as jest.MockedFunction<typeof useRouter>;

describe('RuleSpecEditor - Undo & Redo', () => {
  const authResponse = {
    user: { id: 'admin', email: 'admin@test.com', role: 'Admin' },
    expiresAt: new Date().toISOString(),
  };

  const initialSpec = {
    gameId: 'demo-chess',
    version: '1.0.0',
    createdAt: new Date('2024-01-01').toISOString(),
    rules: [{ id: 'rule-1', text: 'Initial rule text.' }],
  };

  beforeEach(() => {
    jest.clearAllMocks();
    mockUseRouter.mockReturnValue({
      pathname: '/editor',
      query: { gameId: 'demo-chess' },
    } as any);

    mockApi.get.mockImplementation(async (path: string) => {
      if (path === '/api/v1/auth/me') {
        return authResponse;
      }
      if (path === '/api/v1/games/demo-chess/rulespec') {
        return initialSpec;
      }
      return null as any;
    });

    mockApi.put.mockResolvedValue({ ...initialSpec, version: '1.1.0' });
  });

  it('keeps undo disabled before any changes', async () => {
    render(<RuleSpecEditor />);
    await waitForEditorReady();

    getEditorTextarea();
    const undoButton = screen.getByRole('button', { name: /annulla/i });
    expect(undoButton).toBeDisabled();
  });

  it('undo reverts the last JSON edit', async () => {
    const user = userEvent.setup();
    render(<RuleSpecEditor />);
    await waitForEditorReady();

    const textarea = getEditorTextarea();
    const updatedSpec = { ...initialSpec, version: '1.2.0' };

    await user.clear(textarea);
    fireEvent.change(textarea, { target: { value: JSON.stringify(updatedSpec, null, 2) } });
    fireEvent.blur(textarea);

    await waitFor(() => {
      expect(JSON.parse(textarea.value).version).toBe('1.2.0');
    });

    const undoButton = screen.getByRole('button', { name: /annulla/i });
    await user.click(undoButton);

    await waitFor(() => {
      expect(JSON.parse(textarea.value).version).toBe('1.0.0');
    });
  });

  it('redo reapplies an undone change', async () => {
    const user = userEvent.setup();
    render(<RuleSpecEditor />);
    await waitForEditorReady();

    const textarea = getEditorTextarea();
    const updatedSpec = { ...initialSpec, version: '1.3.0' };

    await user.clear(textarea);
    fireEvent.change(textarea, { target: { value: JSON.stringify(updatedSpec, null, 2) } });
    fireEvent.blur(textarea);

    const undoButton = screen.getByRole('button', { name: /annulla/i });
    await user.click(undoButton);
    await waitFor(() => expect(textarea.value).toContain('"version": "1.0.0"'));

    const redoButton = screen.getByRole('button', { name: /ripeti/i });
    await user.click(redoButton);

    await waitFor(() => {
      expect(textarea.value).toContain('"version": "1.3.0"');
    });
  });
});

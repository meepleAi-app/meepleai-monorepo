import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import RuleSpecEditor from '../editor';
import { api } from '../../lib/api';
import { useRouter } from 'next/router';

jest.mock('../../lib/api', () => ({
  api: {
    get: jest.fn(),
    post: jest.fn(),
    put: jest.fn()
  }
}));

jest.mock('next/router', () => ({
  useRouter: jest.fn()
}));

const mockApi = api as jest.Mocked<typeof api>;
const mockUseRouter = useRouter as jest.MockedFunction<typeof useRouter>;

const createRouter = (overrides: Partial<ReturnType<typeof useRouter>> = {}) => ({
  route: '/editor',
  pathname: '/editor',
  query: {},
  asPath: '/editor',
  basePath: '',
  push: jest.fn(),
  replace: jest.fn(),
  reload: jest.fn(),
  back: jest.fn(),
  prefetch: jest.fn().mockResolvedValue(undefined),
  beforePopState: jest.fn(),
  isFallback: false,
  isLocaleDomain: false,
  isReady: true,
  isPreview: false,
  forward: jest.fn(),
  events: {
    emit: jest.fn(),
    on: jest.fn(),
    off: jest.fn()
  },
  ...overrides
});

const getEditorTextarea = () => {
  const textarea = screen.queryAllByRole('textbox').find((el) => el.tagName === 'TEXTAREA');
  if (!textarea) {
    throw new Error('Editor textarea not found');
  }
  return textarea as HTMLTextAreaElement;
};

describe('RuleSpecEditor', () => {
  const authResponse = {
    user: {
      id: 'admin-1',
      email: 'admin@example.com',
      role: 'Admin'
    },
    expiresAt: new Date().toISOString()
  };

  beforeEach(() => {
    jest.clearAllMocks();
    mockUseRouter.mockReturnValue(createRouter());
  });

  it('prompts for authentication when no user is returned', async () => {
    mockApi.get.mockResolvedValueOnce(null);

    render(<RuleSpecEditor />);

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/auth/me'));
    expect(screen.getByText(/Devi effettuare l'accesso per utilizzare l'editor/i)).toBeInTheDocument();
  });

  it('blocks access for users without editor permissions', async () => {
    mockUseRouter.mockReturnValue(createRouter({ query: { gameId: 'demo-chess' } }));
    mockApi.get.mockResolvedValueOnce({
      user: { id: 'viewer-1', email: 'viewer@example.com', role: 'Viewer' },
      expiresAt: new Date().toISOString()
    });

    render(<RuleSpecEditor />);

    await screen.findByText(/Non hai i permessi necessari/i);
  });

  it('loads, validates and saves a RuleSpec for authorized users', async () => {
    const user = userEvent.setup();

    const initialSpec = {
      gameId: 'demo-chess',
      version: '1.0.0',
      createdAt: new Date('2024-01-01').toISOString(),
      rules: [
        { id: 'rule-1', text: 'Initial rule text.' }
      ]
    };

    const updatedSpec = {
      ...initialSpec,
      version: '2.0.0'
    };

    mockUseRouter.mockReturnValue(createRouter({ query: { gameId: 'demo-chess' } }));
    mockApi.get.mockImplementation(async (path: string) => {
      if (path === '/auth/me') {
        return authResponse;
      }
      if (path === '/games/demo-chess/rulespec') {
        return initialSpec;
      }
      return null;
    });
    mockApi.put.mockResolvedValueOnce(updatedSpec);

    render(<RuleSpecEditor />);

    await screen.findByText(/Editor RuleSpec/i);

    await waitFor(() =>
      expect(getEditorTextarea().value).toContain('"version": "1.0.0"')
    );
    const textarea = getEditorTextarea();

    const newJson = JSON.stringify(updatedSpec, null, 2);
    await user.clear(textarea);
    fireEvent.change(textarea, { target: { value: newJson } });
    fireEvent.blur(textarea);

    const saveButton = await screen.findByRole('button', { name: /^Salva$/i });
    expect(saveButton).toBeEnabled();

    await user.click(saveButton);

    await waitFor(() =>
      expect(mockApi.put).toHaveBeenCalledWith('/games/demo-chess/rulespec', updatedSpec)
    );
    await screen.findByText(/RuleSpec salvato con successo/i);
  });

  it('surfaces backend errors when saving fails', async () => {
    const user = userEvent.setup();
    const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});

    const initialSpec = {
      gameId: 'demo-risk',
      version: '0.1.0',
      createdAt: new Date('2024-06-15').toISOString(),
      rules: [
        { id: 'r-1', text: 'Rule text' }
      ]
    };

    mockUseRouter.mockReturnValue(createRouter({ query: { gameId: 'demo-risk' } }));
    mockApi.get.mockImplementation(async (path: string) => {
      if (path === '/auth/me') {
        return authResponse;
      }
      if (path === '/games/demo-risk/rulespec') {
        return initialSpec;
      }
      return null;
    });

    mockApi.put.mockRejectedValueOnce(new Error('Save failed'));

    render(<RuleSpecEditor />);

    await screen.findByText(/Editor RuleSpec/i);

    await waitFor(() =>
      expect(getEditorTextarea().value).toContain('"version": "0.1.0"')
    );
    const textarea = getEditorTextarea();
    const updatedSpec = { ...initialSpec, version: '0.2.0' };
    const json = JSON.stringify(updatedSpec, null, 2);

    await user.clear(textarea);
    fireEvent.change(textarea, { target: { value: json } });
    fireEvent.blur(textarea);

    const saveButton = screen.getByRole('button', { name: /^Salva$/i });
    await user.click(saveButton);

    await waitFor(() => expect(mockApi.put).toHaveBeenCalled());
    await screen.findByText('Save failed');

    consoleErrorSpy.mockRestore();
  });

  it('disables saving and shows validation error when JSON is invalid', async () => {
    const user = userEvent.setup();

    const initialSpec = {
      gameId: 'demo-catan',
      version: '1.0.0',
      createdAt: new Date('2024-01-02').toISOString(),
      rules: [
        { id: 'rule-a', text: 'Setup details' }
      ]
    };

    mockUseRouter.mockReturnValue(createRouter({ query: { gameId: 'demo-catan' } }));
    mockApi.get.mockImplementation(async (path: string) => {
      if (path === '/auth/me') {
        return authResponse;
      }
      if (path === '/games/demo-catan/rulespec') {
        return initialSpec;
      }
      return null;
    });

    render(<RuleSpecEditor />);

    await screen.findByText(/Editor RuleSpec/i);

    await waitFor(() =>
      expect(getEditorTextarea().value).toContain('"version": "1.0.0"')
    );
    const textarea = getEditorTextarea();
    await user.clear(textarea);
    fireEvent.change(textarea, { target: { value: '{' } });

    expect(screen.getByText(/Expected property name/i)).toBeInTheDocument();
    const saveButton = screen.getByRole('button', { name: /^Salva$/i });
    expect(saveButton).toBeDisabled();
  });
});

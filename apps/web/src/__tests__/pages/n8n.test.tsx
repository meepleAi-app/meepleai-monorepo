import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';

const initialApiBaseEnv = process.env.NEXT_PUBLIC_API_BASE;
if (!process.env.NEXT_PUBLIC_API_BASE) {
  process.env.NEXT_PUBLIC_API_BASE = 'http://api.test';
}
const N8nWorkflowManagement = require('../../pages/n8n').default;

type Deferred<T> = {
  promise: Promise<T>;
  resolve: (value: T) => void;
  reject: (reason?: unknown) => void;
};

const createDeferred = <T,>(): Deferred<T> => {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
};

const createResponse = (data: unknown, ok = true, status = 200): Response => ({
  ok,
  status,
  json: async () => data
}) as Response;

const getFormInputs = () => ({
  nameInput: screen.getByPlaceholderText('Production n8n') as HTMLInputElement,
  baseUrlInput: screen.getByPlaceholderText('http://localhost:5678') as HTMLInputElement,
  apiKeyInput: screen.getByPlaceholderText('n8n API key') as HTMLInputElement,
  webhookInput: screen.getByPlaceholderText('http://localhost:5678/webhook') as HTMLInputElement
});

describe('N8nWorkflowManagement', () => {
  const originalApiBase = initialApiBaseEnv;
  const originalFetch = global.fetch;
  const originalConfirm = window.confirm;
  const originalAlert = window.alert;

  let fetchMock: jest.Mock;
  let confirmMock: jest.Mock;
  let alertMock: jest.Mock;

  beforeEach(() => {
    fetchMock = jest.fn();
    confirmMock = jest.fn();
    alertMock = jest.fn();

    global.fetch = fetchMock as unknown as typeof fetch;
    window.confirm = confirmMock;
    window.alert = alertMock;

    process.env.NEXT_PUBLIC_API_BASE = 'http://api.test';
  });

  afterEach(() => {
    global.fetch = originalFetch;
    window.confirm = originalConfirm;
    window.alert = originalAlert;
    if (originalApiBase === undefined) {
      delete process.env.NEXT_PUBLIC_API_BASE;
    } else {
      process.env.NEXT_PUBLIC_API_BASE = originalApiBase;
    }
  });

  it('uses default API base when NEXT_PUBLIC_API_BASE is not defined', () => {
    delete process.env.NEXT_PUBLIC_API_BASE;

    jest.isolateModules(() => {
      const { apiBase } = require('../../pages/n8n');
      expect(apiBase).toBe('http://localhost:8080');
    });
  });

  it('shows loading state while fetch is pending', async () => {
    const deferred = createDeferred<Response>();
    fetchMock.mockReturnValue(deferred.promise);

    const { unmount } = render(<N8nWorkflowManagement />);

    expect(screen.getByText(/Loading.../i)).toBeInTheDocument();

    await act(async () => {
      deferred.resolve(createResponse({ configs: [] }));
    });

    await waitFor(() => expect(screen.queryByText(/Loading.../i)).not.toBeInTheDocument());

    unmount();
  });

  it('renders error state when initial fetch fails', async () => {
    fetchMock.mockResolvedValue(createResponse({}, false, 500));

    render(<N8nWorkflowManagement />);

    await waitFor(() => expect(screen.getByText('Error')).toBeInTheDocument());
    expect(screen.getByText('Failed to fetch n8n configurations')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Back to Home/i })).toHaveAttribute('href', '/');
  });

  it('supports toggling, creating and editing configurations', async () => {
    const existingConfig = {
      id: 'cfg-1',
      name: 'Production n8n',
      baseUrl: 'https://n8n.example.com',
      webhookUrl: 'https://n8n.example.com/webhook',
      isActive: true,
      lastTestedAt: null,
      lastTestResult: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    };

    fetchMock.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.toString();
      const method = init?.method ?? 'GET';

      if (url === 'http://api.test/admin/n8n' && method === 'GET') {
        return Promise.resolve(createResponse({ configs: [existingConfig] }));
      }

      if (url === 'http://api.test/admin/n8n' && method === 'POST') {
        return Promise.resolve(createResponse({ config: { ...existingConfig, id: 'cfg-2' } }));
      }

      if (url === `http://api.test/admin/n8n/${existingConfig.id}` && method === 'PUT') {
        return Promise.resolve(createResponse({ config: existingConfig }));
      }

      throw new Error(`Unexpected fetch call: ${method} ${url}`);
    });

    render(<N8nWorkflowManagement />);

    await waitFor(() => expect(screen.getByText(existingConfig.name)).toBeInTheDocument());

    // Toggle add configuration form
    const toggleButton = screen.getByRole('button', { name: /Add Configuration/i });
    fireEvent.click(toggleButton);
    expect(screen.getByText('New Configuration')).toBeInTheDocument();

    fireEvent.click(toggleButton);
    await waitFor(() => expect(screen.queryByText('New Configuration')).not.toBeInTheDocument());

    // Open form again to create a new configuration
    fireEvent.click(toggleButton);
    const { nameInput, baseUrlInput, apiKeyInput, webhookInput } = getFormInputs();

    fireEvent.change(nameInput, { target: { value: 'Staging n8n' } });
    fireEvent.change(baseUrlInput, { target: { value: 'https://staging.example.com' } });
    fireEvent.change(apiKeyInput, { target: { value: 'secret-key' } });
    fireEvent.change(webhookInput, { target: { value: 'https://staging.example.com/webhook' } });

    fetchMock.mockClear();

    fireEvent.submit(screen.getByRole('button', { name: /Create Configuration/i }));

    await waitFor(() => {
      const postCall = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST');
      expect(postCall).toBeDefined();
      const [, init] = postCall!;
      expect(postCall![0]).toBe('http://api.test/admin/n8n');
      expect(JSON.parse(init!.body as string)).toEqual({
        name: 'Staging n8n',
        baseUrl: 'https://staging.example.com',
        apiKey: 'secret-key',
        webhookUrl: 'https://staging.example.com/webhook'
      });
    });

    await waitFor(() => expect(screen.queryByText('New Configuration')).not.toBeInTheDocument());

    await waitFor(() =>
      expect(
        fetchMock.mock.calls.filter(([, init]) => (init?.method ?? 'GET') === 'GET')
      ).toHaveLength(1)
    );

    fetchMock.mockClear();

    // Edit existing configuration
    fireEvent.click(screen.getByRole('button', { name: 'Edit' }));

    expect(screen.getByText('Edit Configuration')).toBeInTheDocument();

    const {
      nameInput: editNameInput,
      baseUrlInput: editBaseUrlInput,
      webhookInput: editWebhookInput
    } = getFormInputs();

    await waitFor(() => expect(editNameInput).toHaveValue(existingConfig.name));
    expect(editBaseUrlInput).toHaveValue(existingConfig.baseUrl);
    expect(editWebhookInput).toHaveValue(existingConfig.webhookUrl);

    fireEvent.change(editNameInput, { target: { value: 'Production n8n Updated' } });
    fireEvent.change(editWebhookInput, { target: { value: '' } });

    fireEvent.submit(screen.getByRole('button', { name: /Update Configuration/i }));

    await waitFor(() => {
      const putCall = fetchMock.mock.calls.find(([, init]) => init?.method === 'PUT');
      expect(putCall).toBeDefined();
      const [url, init] = putCall!;
      expect(url).toBe(`http://api.test/admin/n8n/${existingConfig.id}`);
      const parsedBody = JSON.parse(init!.body as string);
      expect(parsedBody).toEqual({
        name: 'Production n8n Updated',
        webhookUrl: null
      });
      expect(parsedBody).not.toHaveProperty('apiKey');
    });

    await waitFor(() => expect(screen.queryByText('Edit Configuration')).not.toBeInTheDocument());
    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([, init]) => (init?.method ?? 'GET') === 'GET')).toBe(true)
    );
  });

  it('handles card actions for testing, activation and deletion', async () => {
    const existingConfig = {
      id: 'cfg-1',
      name: 'Production n8n',
      baseUrl: 'https://n8n.example.com',
      webhookUrl: null,
      isActive: true,
      lastTestedAt: null,
      lastTestResult: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    };

    const testDeferred = createDeferred<Response>();

    fetchMock.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.toString();
      const method = init?.method ?? 'GET';

      if (url === 'http://api.test/admin/n8n' && method === 'GET') {
        return Promise.resolve(createResponse({ configs: [existingConfig] }));
      }

      if (url === `http://api.test/admin/n8n/${existingConfig.id}/test` && method === 'POST') {
        return testDeferred.promise;
      }

      if (url === `http://api.test/admin/n8n/${existingConfig.id}` && method === 'PUT') {
        return Promise.resolve(createResponse({ config: { ...existingConfig, isActive: !existingConfig.isActive } }));
      }

      if (url === `http://api.test/admin/n8n/${existingConfig.id}` && method === 'DELETE') {
        return Promise.resolve(createResponse({ success: true }));
      }

      throw new Error(`Unexpected fetch call: ${method} ${url}`);
    });

    confirmMock.mockReturnValue(true);

    render(<N8nWorkflowManagement />);

    await waitFor(() => expect(screen.getByText(existingConfig.name)).toBeInTheDocument());

    fetchMock.mockClear();

    // Test button triggers test endpoint and alert
    const testButton = screen.getByRole('button', { name: 'Test' });
    fireEvent.click(testButton);

    expect(testButton).toBeDisabled();
    expect(testButton).toHaveTextContent('Testing...');

    await act(async () => {
      testDeferred.resolve(
        createResponse({ success: true, message: 'Test succeeded', latencyMs: 120 })
      );
    });

    await waitFor(() => {
      const testCall = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST');
      expect(testCall).toBeDefined();
      expect(testCall![0]).toBe(`http://api.test/admin/n8n/${existingConfig.id}/test`);
      expect(alertMock).toHaveBeenCalledWith('Test succeeded');
    });

    await waitFor(() => expect(screen.getByRole('button', { name: 'Test' })).not.toBeDisabled());

    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([, init]) => (init?.method ?? 'GET') === 'GET')).toBe(true)
    );

    fetchMock.mockClear();
    alertMock.mockClear();

    // Toggle activation state
    fireEvent.click(screen.getByRole('button', { name: /Deactivate/i }));

    await waitFor(() => {
      const putCall = fetchMock.mock.calls.find(([, init]) => init?.method === 'PUT');
      expect(putCall).toBeDefined();
      const [, init] = putCall!;
      expect(JSON.parse(init!.body as string)).toEqual({ isActive: false });
    });

    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([, init]) => (init?.method ?? 'GET') === 'GET')).toBe(true)
    );

    fetchMock.mockClear();

    // Delete configuration after confirmation
    fireEvent.click(screen.getByRole('button', { name: 'Delete' }));

    expect(confirmMock).toHaveBeenCalledWith('Are you sure you want to delete this configuration?');

    await waitFor(() => {
      const deleteCall = fetchMock.mock.calls.find(([, init]) => init?.method === 'DELETE');
      expect(deleteCall).toBeDefined();
      expect(deleteCall![0]).toBe(`http://api.test/admin/n8n/${existingConfig.id}`);
    });

    await waitFor(() =>
      expect(fetchMock.mock.calls.some(([, init]) => (init?.method ?? 'GET') === 'GET')).toBe(true)
    );
  });

  it('handles error when creating configuration fails', async () => {
    fetchMock.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.toString();
      const method = init?.method ?? 'GET';

      if (url === 'http://api.test/admin/n8n' && method === 'GET') {
        return Promise.resolve(createResponse({ configs: [] }));
      }

      if (url === 'http://api.test/admin/n8n' && method === 'POST') {
        return Promise.resolve(createResponse({ error: 'Validation failed' }, false, 400));
      }

      throw new Error(`Unexpected fetch call: ${method} ${url}`);
    });

    render(<N8nWorkflowManagement />);

    await waitFor(() => expect(screen.getByRole('button', { name: /Add Configuration/i })).toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: /Add Configuration/i }));

    const { nameInput, baseUrlInput, apiKeyInput, webhookInput } = getFormInputs();
    fireEvent.change(nameInput, { target: { value: 'Test' } });
    fireEvent.change(baseUrlInput, { target: { value: 'http://test.com' } });
    fireEvent.change(apiKeyInput, { target: { value: 'key' } });
    fireEvent.change(webhookInput, { target: { value: 'http://test.com/webhook' } });

    fireEvent.submit(screen.getByRole('button', { name: /Create Configuration/i }));

    await waitFor(() => expect(alertMock).toHaveBeenCalledWith('Validation failed'));
  });

  it('cancels delete when user declines confirmation', async () => {
    const existingConfig = {
      id: 'cfg-1',
      name: 'Production n8n',
      baseUrl: 'https://n8n.example.com',
      webhookUrl: null,
      isActive: true,
      lastTestedAt: null,
      lastTestResult: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    };

    fetchMock.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.toString();
      const method = init?.method ?? 'GET';

      if (url === 'http://api.test/admin/n8n' && method === 'GET') {
        return Promise.resolve(createResponse({ configs: [existingConfig] }));
      }

      throw new Error(`Unexpected fetch call: ${method} ${url}`);
    });

    confirmMock.mockReturnValue(false);

    render(<N8nWorkflowManagement />);

    await waitFor(() => expect(screen.getByText(existingConfig.name)).toBeInTheDocument());

    fetchMock.mockClear();

    fireEvent.click(screen.getByRole('button', { name: 'Delete' }));

    expect(confirmMock).toHaveBeenCalledWith('Are you sure you want to delete this configuration?');
    // Verify no DELETE request was made
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('handles error when deleting configuration fails', async () => {
    const existingConfig = {
      id: 'cfg-1',
      name: 'Production n8n',
      baseUrl: 'https://n8n.example.com',
      webhookUrl: null,
      isActive: true,
      lastTestedAt: null,
      lastTestResult: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    };

    fetchMock.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.toString();
      const method = init?.method ?? 'GET';

      if (url === 'http://api.test/admin/n8n' && method === 'GET') {
        return Promise.resolve(createResponse({ configs: [existingConfig] }));
      }

      if (url === `http://api.test/admin/n8n/${existingConfig.id}` && method === 'DELETE') {
        return Promise.resolve(createResponse({}, false, 500));
      }

      throw new Error(`Unexpected fetch call: ${method} ${url}`);
    });

    confirmMock.mockReturnValue(true);

    render(<N8nWorkflowManagement />);

    await waitFor(() => expect(screen.getByText(existingConfig.name)).toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: 'Delete' }));

    await waitFor(() => expect(alertMock).toHaveBeenCalledWith('Failed to delete configuration'));
  });

  it('handles error when testing configuration fails', async () => {
    const existingConfig = {
      id: 'cfg-1',
      name: 'Production n8n',
      baseUrl: 'https://n8n.example.com',
      webhookUrl: null,
      isActive: true,
      lastTestedAt: null,
      lastTestResult: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    };

    fetchMock.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.toString();
      const method = init?.method ?? 'GET';

      if (url === 'http://api.test/admin/n8n' && method === 'GET') {
        return Promise.resolve(createResponse({ configs: [existingConfig] }));
      }

      if (url === `http://api.test/admin/n8n/${existingConfig.id}/test` && method === 'POST') {
        return Promise.resolve(createResponse({}, false, 500));
      }

      throw new Error(`Unexpected fetch call: ${method} ${url}`);
    });

    render(<N8nWorkflowManagement />);

    await waitFor(() => expect(screen.getByText(existingConfig.name)).toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: 'Test' }));

    await waitFor(() => expect(alertMock).toHaveBeenCalledWith('Failed to test configuration'));
  });

  it('handles error when toggling activation fails', async () => {
    const existingConfig = {
      id: 'cfg-1',
      name: 'Production n8n',
      baseUrl: 'https://n8n.example.com',
      webhookUrl: null,
      isActive: true,
      lastTestedAt: null,
      lastTestResult: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    };

    fetchMock.mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.toString();
      const method = init?.method ?? 'GET';

      if (url === 'http://api.test/admin/n8n' && method === 'GET') {
        return Promise.resolve(createResponse({ configs: [existingConfig] }));
      }

      if (url === `http://api.test/admin/n8n/${existingConfig.id}` && method === 'PUT') {
        return Promise.resolve(createResponse({}, false, 500));
      }

      throw new Error(`Unexpected fetch call: ${method} ${url}`);
    });

    render(<N8nWorkflowManagement />);

    await waitFor(() => expect(screen.getByText(existingConfig.name)).toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: /Deactivate/i }));

    await waitFor(() => expect(alertMock).toHaveBeenCalledWith('Failed to update configuration'));
  });

  it('handles non-Error objects in catch blocks', async () => {
    fetchMock.mockImplementation(() => {
      return Promise.reject('String error');
    });

    render(<N8nWorkflowManagement />);

    await waitFor(() => {
      expect(screen.getByText('An error occurred')).toBeInTheDocument();
    });
  });

});

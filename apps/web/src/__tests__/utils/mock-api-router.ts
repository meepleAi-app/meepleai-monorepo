/**
 * MockApiRouter - Centralized API mocking utility for tests
 *
 * Eliminates duplication of mock setup across test files by providing
 * a fluent API for defining mock routes with pattern matching support.
 *
 * @example
 * ```typescript
 * const router = new MockApiRouter()
 *   .get('/auth/me', () => createJsonResponse({ user: {...} }))
 *   .get('/games/:id', ({ params }) => createJsonResponse({ id: params.id }))
 *   .post('/ingest/pdf', () => createJsonResponse({ documentId: 'pdf-123' }));
 *
 * global.fetch = router.toMockImplementation();
 * ```
 */

export type RouteMethod = 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';

export interface RouteContext {
  params: Record<string, string>;
  url: string;
  method: string;
  init?: RequestInit;
}

export type RouteHandler = (context: RouteContext) => Promise<Response>;

interface RouteDefinition {
  method: RouteMethod;
  pattern: string;
  regex: RegExp;
  paramNames: string[];
  handler: RouteHandler;
}

/**
 * Converts a route pattern with :param syntax to a regex for matching
 * @example
 * '/games/:gameId/pdfs/:pdfId' => /^\/games\/([^\/]+)\/pdfs\/([^\/]+)$/
 */
function patternToRegex(pattern: string): { regex: RegExp; paramNames: string[] } {
  const paramNames: string[] = [];

  // Escape special regex characters except for :param placeholders
  let regexPattern = pattern.replace(/[.+?^${}()|[\]\\]/g, '\\$&');

  // Replace :param with capture groups and extract param names
  regexPattern = regexPattern.replace(/:(\w+)/g, (_, paramName) => {
    paramNames.push(paramName);
    return '([^/]+)';
  });

  // Anchor the pattern to match the full URL path
  const regex = new RegExp(`^${regexPattern}$`);

  return { regex, paramNames };
}

/**
 * Extracts the pathname from a full URL or returns the input if it's already a path
 */
function extractPathname(url: string): string {
  try {
    const urlObj = new URL(url);
    return urlObj.pathname;
  } catch {
    // If URL parsing fails, assume it's already a pathname
    return url;
  }
}

export class MockApiRouter {
  private routes: RouteDefinition[] = [];

  /**
   * Register a GET route
   */
  get(pattern: string, handler: RouteHandler): this {
    this.addRoute('GET', pattern, handler);
    return this;
  }

  /**
   * Register a POST route
   */
  post(pattern: string, handler: RouteHandler): this {
    this.addRoute('POST', pattern, handler);
    return this;
  }

  /**
   * Register a PUT route
   */
  put(pattern: string, handler: RouteHandler): this {
    this.addRoute('PUT', pattern, handler);
    return this;
  }

  /**
   * Register a DELETE route
   */
  delete(pattern: string, handler: RouteHandler): this {
    this.addRoute('DELETE', pattern, handler);
    return this;
  }

  /**
   * Register a PATCH route
   */
  patch(pattern: string, handler: RouteHandler): this {
    this.addRoute('PATCH', pattern, handler);
    return this;
  }

  /**
   * Internal method to add a route definition
   */
  private addRoute(method: RouteMethod, pattern: string, handler: RouteHandler): void {
    const { regex, paramNames } = patternToRegex(pattern);
    this.routes.push({
      method,
      pattern,
      regex,
      paramNames,
      handler
    });
  }

  /**
   * Find a matching route for the given URL and method
   */
  private findRoute(url: string, method: string): { route: RouteDefinition; params: Record<string, string> } | null {
    const pathname = extractPathname(url);
    const upperMethod = method.toUpperCase();

    for (const route of this.routes) {
      if (route.method !== upperMethod) {
        continue;
      }

      const match = pathname.match(route.regex);
      if (match) {
        // Extract parameters from the match groups
        const params: Record<string, string> = {};
        for (let i = 0; i < route.paramNames.length; i++) {
          params[route.paramNames[i]] = match[i + 1];
        }
        return { route, params };
      }
    }

    return null;
  }

  /**
   * Handle a fetch request and return a response
   */
  async handle(url: string, init?: RequestInit): Promise<Response> {
    const method = init?.method ?? 'GET';
    const match = this.findRoute(url, method);

    if (!match) {
      const availableRoutes = this.routes
        .map((r) => `  ${r.method} ${r.pattern}`)
        .join('\n');

      throw new Error(
        `MockApiRouter: No handler for ${method} ${extractPathname(url)}\n\n` +
        `Available routes:\n${availableRoutes || '  (none)'}`
      );
    }

    const context: RouteContext = {
      params: match.params,
      url,
      method,
      init
    };

    return match.route.handler(context);
  }

  /**
   * Convert this router to a Jest mock implementation
   *
   * @example
   * ```typescript
   * const mockFetch = jest.fn();
   * mockFetch.mockImplementation(router.toMockImplementation());
   * global.fetch = mockFetch;
   * ```
   */
  toMockImplementation(): (input: RequestInfo | URL, init?: RequestInit) => Promise<Response> {
    return (input: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof input === 'string' ? input : input.toString();
      return this.handle(url, init);
    };
  }

  /**
   * Get a list of all registered routes (useful for debugging)
   */
  getRoutes(): Array<{ method: RouteMethod; pattern: string }> {
    return this.routes.map((r) => ({
      method: r.method,
      pattern: r.pattern
    }));
  }

  /**
   * Clear all registered routes
   */
  clear(): this {
    this.routes = [];
    return this;
  }
}

/**
 * Helper to create a JSON response (compatible with existing test utilities)
 */
export const createJsonResponse = (data: unknown, status = 200): Promise<Response> =>
  Promise.resolve({
    ok: status >= 200 && status < 300,
    status,
    statusText: status >= 200 && status < 300 ? 'OK' : 'Error',
    json: () => Promise.resolve(data)
  } as Response);

/**
 * Helper to create an error response (compatible with existing test utilities)
 */
export const createErrorResponse = (
  status: number,
  body: unknown = { error: 'Error' },
  statusText?: string
): Promise<Response> =>
  Promise.resolve({
    ok: false,
    status,
    statusText:
      statusText ??
      (status === 401 ? 'Unauthorized' : status === 500 ? 'Internal Server Error' : 'Error'),
    json: () => Promise.resolve(body)
  } as Response);

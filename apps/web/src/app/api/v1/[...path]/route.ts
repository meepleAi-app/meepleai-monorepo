/**
 * API Proxy Route - Forwards /api/v1/* to backend
 *
 * Issue #703: Preserves Set-Cookie headers from backend
 * Issue #2432: Fix JSON body corruption during proxy
 *
 * This catch-all route proxies all /api/v1/* requests to the backend API,
 * ensuring proper cookie handling for authentication.
 */

import { NextRequest, NextResponse } from 'next/server';

// Force dynamic rendering (required for proxying)
export const dynamic = 'force-dynamic';

// Use internal Docker network URL when available (server-side proxy)
// Falls back to NEXT_PUBLIC_API_BASE for development outside Docker
const API_BASE =
  process.env.API_BASE_URL || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Log API_BASE on module load for debugging
// eslint-disable-next-line no-console
console.log('[API Proxy] Module initialized with API_BASE:', API_BASE);

/**
 * Build forwarding headers from the incoming request.
 * Excludes headers that must not be forwarded (host, connection, content-length, expect).
 */
function buildForwardHeaders(request: NextRequest): Headers {
  const headers = new Headers();
  const excludedHeaders = ['host', 'connection', 'content-length', 'expect'];
  request.headers.forEach((value, key) => {
    if (!excludedHeaders.includes(key.toLowerCase())) {
      headers.set(key, value);
    }
  });
  return headers;
}

/**
 * Copy response headers to NextResponse, with special Set-Cookie handling.
 * Issue #2778: getSetCookie() returns array of all Set-Cookie headers.
 */
function copyResponseHeaders(source: Response, target: NextResponse, apiPath: string): void {
  const setCookies = source.headers.getSetCookie();
  if (setCookies.length > 0) {
    // eslint-disable-next-line no-console
    console.log(`[API Proxy] Set-Cookie headers received: ${setCookies.length} cookie(s)`);
    for (const cookie of setCookies) {
      target.headers.append('set-cookie', cookie);
    }
  }

  source.headers.forEach((value, key) => {
    if (key.toLowerCase() !== 'set-cookie') {
      target.headers.set(key, value);
    }
  });

  if (apiPath.includes('/auth/')) {
    // eslint-disable-next-line no-console
    console.log(`[API Proxy] Auth response: ${source.status} ${source.statusText}, Set-Cookie: ${setCookies.length > 0 ? `${setCookies.length} cookie(s)` : 'absent'}`);
  }

  target.headers.set('x-no-compression', '1');
}

/**
 * Check if a response is an SSE stream that should be piped without buffering.
 */
function isStreamingResponse(response: Response): boolean {
  const contentType = response.headers.get('content-type') ?? '';
  return contentType.includes('text/event-stream');
}

async function proxyRequest(request: NextRequest, method: string) {
  try {
    const pathname = request.nextUrl.pathname;
    const apiPath = pathname.replace('/api/v1/', '/api/v1/');
    const targetUrl = `${API_BASE}${apiPath}${request.nextUrl.search}`;
    // eslint-disable-next-line no-console
    console.log(`[API Proxy] ${method} ${targetUrl}`);

    // Issue #2432: Use arrayBuffer() to preserve exact binary content
    let body: BodyInit | null = null;
    if (['POST', 'PUT', 'PATCH'].includes(method)) {
      body = await request.arrayBuffer();
    }

    const headers = buildForwardHeaders(request);

    const response = await fetch(targetUrl, {
      method,
      headers,
      body,
      credentials: 'include',
    });

    // SSE streaming: pipe the response body directly instead of buffering
    // This enables real-time streaming for playground chat and other SSE endpoints
    if (isStreamingResponse(response) && response.body) {
      const nextResponse = new NextResponse(response.body as ReadableStream, {
        status: response.status,
        statusText: response.statusText,
      });
      copyResponseHeaders(response, nextResponse, apiPath);
      return nextResponse;
    }

    // Non-streaming: buffer the full response (original behavior)
    const responseBody = await response.arrayBuffer();
    const nextResponse = new NextResponse(responseBody, {
      status: response.status,
      statusText: response.statusText,
    });
    copyResponseHeaders(response, nextResponse, apiPath);

    return nextResponse;
  } catch (error) {
    console.error('[API Proxy] Error:', error);
    return NextResponse.json(
      { error: 'Proxy error', message: error instanceof Error ? error.message : 'Unknown error' },
      { status: 502 }
    );
  }
}

export async function GET(request: NextRequest) {
  return proxyRequest(request, 'GET');
}

export async function POST(request: NextRequest) {
  return proxyRequest(request, 'POST');
}

export async function PUT(request: NextRequest) {
  return proxyRequest(request, 'PUT');
}

export async function DELETE(request: NextRequest) {
  return proxyRequest(request, 'DELETE');
}

export async function PATCH(request: NextRequest) {
  return proxyRequest(request, 'PATCH');
}

export async function OPTIONS(request: NextRequest) {
  return proxyRequest(request, 'OPTIONS');
}

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

async function proxyRequest(request: NextRequest, method: string) {
  try {
    // Extract path segments after /api/v1/
    const pathname = request.nextUrl.pathname;
    const apiPath = pathname.replace('/api/v1/', '/api/v1/');

    // Build target URL
    const targetUrl = `${API_BASE}${apiPath}${request.nextUrl.search}`;
    // eslint-disable-next-line no-console
    console.log(`[API Proxy] ${method} ${targetUrl}`);

    // Get request body for methods that support it
    // Issue #2432: Use arrayBuffer() instead of text() to preserve exact binary content
    // text() can corrupt special characters in JSON (e.g., '!' in passwords),
    // causing "invalid escapable character" deserialization errors on the backend
    let body: BodyInit | null = null;
    if (['POST', 'PUT', 'PATCH'].includes(method)) {
      body = await request.arrayBuffer();
    }

    // Forward headers (exclude problematic headers)
    // - host: Must match target server
    // - connection: Managed by HTTP client
    // - content-length: Recalculated for body
    // - expect: Node.js fetch doesn't support Expect: 100-continue
    const headers = new Headers();
    const excludedHeaders = ['host', 'connection', 'content-length', 'expect'];
    request.headers.forEach((value, key) => {
      if (!excludedHeaders.includes(key.toLowerCase())) {
        headers.set(key, value);
      }
    });

    // Make proxied request
    const response = await fetch(targetUrl, {
      method,
      headers,
      body,
      credentials: 'include',
    });

    // Get response body as ArrayBuffer to avoid compression issues
    const responseBody = await response.arrayBuffer();

    // Create Next.js response with same status
    const nextResponse = new NextResponse(responseBody, {
      status: response.status,
      statusText: response.statusText,
    });

    // Copy response headers with special handling for Set-Cookie
    // Issue: headers.forEach + headers.set may not handle Set-Cookie correctly in all environments
    // Fix: Handle Set-Cookie explicitly first, then copy other headers
    const setCookieHeader = response.headers.get('set-cookie');
    if (setCookieHeader) {
      // Debug logging for auth troubleshooting
      // eslint-disable-next-line no-console
      console.log(`[API Proxy] Set-Cookie header received: ${setCookieHeader.substring(0, 100)}...`);
      nextResponse.headers.set('set-cookie', setCookieHeader);
    }

    // Copy other response headers (excluding set-cookie which was already handled)
    response.headers.forEach((value, key) => {
      const lowerKey = key.toLowerCase();
      if (lowerKey !== 'set-cookie') {
        nextResponse.headers.set(key, value);
      }
    });

    // Debug: Log response status for auth endpoints
    if (apiPath.includes('/auth/')) {
      // eslint-disable-next-line no-console
      console.log(`[API Proxy] Auth response: ${response.status} ${response.statusText}, Set-Cookie: ${setCookieHeader ? 'present' : 'absent'}`);
    }

    // Disable Next.js compression to prevent ERR_CONTENT_DECODING_FAILED
    nextResponse.headers.set('x-no-compression', '1');

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

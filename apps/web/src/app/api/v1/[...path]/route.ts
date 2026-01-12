/**
 * API Proxy Route - Forwards /api/v1/* to backend
 *
 * Issue #703: Preserves Set-Cookie headers from backend
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

async function proxyRequest(request: NextRequest, method: string) {
  try {
    // Extract path segments after /api/v1/
    const pathname = request.nextUrl.pathname;
    const apiPath = pathname.replace('/api/v1/', '/api/v1/');

    // Build target URL
    const targetUrl = `${API_BASE}${apiPath}${request.nextUrl.search}`;

    // Get request body for methods that support it
    let body: BodyInit | null = null;
    if (['POST', 'PUT', 'PATCH'].includes(method)) {
      body = await request.text();
    }

    // Forward headers (exclude host, connection)
    const headers = new Headers();
    request.headers.forEach((value, key) => {
      if (!['host', 'connection', 'content-length'].includes(key.toLowerCase())) {
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

    // Copy response headers (especially Set-Cookie)
    response.headers.forEach((value, key) => {
      nextResponse.headers.set(key, value);
    });

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

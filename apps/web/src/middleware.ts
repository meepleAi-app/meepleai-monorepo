import { NextRequest, NextResponse } from 'next/server';

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Redirect legacy chat URL to unified chat
  if (pathname === '/board-game-ai/ask' || pathname.startsWith('/board-game-ai/ask/')) {
    const url = request.nextUrl.clone();
    url.pathname = '/chat/new';

    // Preserve query params (e.g., ?gameId=xxx)
    return NextResponse.redirect(url, 301);
  }

  return NextResponse.next();
}

export const config = {
  matcher: ['/board-game-ai/ask/:path*'],
};

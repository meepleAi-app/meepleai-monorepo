import { NextResponse } from 'next/server';

/**
 * Simple health check endpoint used by Playwright and orchestration scripts.
 * Returns 200 if the web front-end is reachable.
 */
export async function GET() {
  return NextResponse.json({ status: 'healthy' });
}

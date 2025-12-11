import { NextResponse } from 'next/server';

const notImplemented = () =>
  NextResponse.json(
    { error: 'Stub API route for static build — backend handles /api/v1/*' },
    { status: 404 }
  );

export const dynamic = 'force-static';

export async function GET() {
  return notImplemented();
}

export async function POST() {
  return notImplemented();
}

export async function PUT() {
  return notImplemented();
}

export async function DELETE() {
  return notImplemented();
}

export async function PATCH() {
  return notImplemented();
}

export async function OPTIONS() {
  return notImplemented();
}

/**
 * Custom 404 Not Found Page
 *
 * Uses force-dynamic to prevent static prerendering which fails
 * due to DOMMatrix not being available in Node.js build workers.
 */
export const dynamic = 'force-dynamic';

import Link from 'next/link';

export default function NotFound() {
  return (
    <div className="flex min-h-[60vh] flex-col items-center justify-center gap-4 px-4 text-center">
      <h1 className="font-quicksand text-6xl font-bold text-amber-600">404</h1>
      <h2 className="text-xl font-semibold text-foreground">Page not found</h2>
      <p className="max-w-md text-muted-foreground">
        The page you&apos;re looking for doesn&apos;t exist or has been moved.
      </p>
      <Link
        href="/"
        className="mt-4 rounded-lg bg-amber-600 px-6 py-2.5 text-sm font-medium text-white transition-colors hover:bg-amber-700"
      >
        Go Home
      </Link>
    </div>
  );
}

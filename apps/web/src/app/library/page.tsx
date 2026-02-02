/**
 * User Library Page (Issue #2464, #2613, #2618)
 * Updated: Issue #3104 - Navigation handled by layout
 * Updated: Protected route - requires authentication
 *
 * Enhanced user library management with search, filtering, and actions.
 *
 * Route: /library (authenticated users only)
 * Features:
 * - Search by game title
 * - Filter by favorites
 * - Sort options (date, title, favorite)
 * - Edit notes modal
 * - Remove confirmation dialog
 * - Quota status bar
 * - Empty state with CTA
 * - Bulk selection mode with floating action bar (Issue #2613)
 * - Framer Motion animations (Issue #2618)
 *
 * Note: Uses next/dynamic with ssr: false to avoid DOMMatrix SSR issues
 * with framer-motion during static page generation. Must be a Client
 * Component to use ssr: false with next/dynamic.
 */

'use client';

import dynamic from 'next/dynamic';

import { RequireRole } from '@/components/auth/RequireRole';
import { Skeleton } from '@/components/ui/feedback/skeleton';

// Dynamically import the client component with SSR disabled
// This prevents framer-motion from being evaluated during SSG
const LibraryPageClient = dynamic(() => import('./LibraryPageClient'), {
  ssr: false,
  loading: () => (
    <div className="container mx-auto px-4 py-8 space-y-6">
      {/* Quota skeleton */}
      <Skeleton className="h-24 w-full" />

      {/* Filters skeleton */}
      <Skeleton className="h-16 w-full" />

      {/* Cards grid skeleton */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {Array.from({ length: 6 }).map((_, i) => (
          <Skeleton key={i} className="h-64 w-full rounded-lg" />
        ))}
      </div>
    </div>
  ),
});

export default function LibraryPage() {
  return (
    <RequireRole allowedRoles={['User', 'Editor', 'Admin']}>
      <LibraryPageClient />
    </RequireRole>
  );
}

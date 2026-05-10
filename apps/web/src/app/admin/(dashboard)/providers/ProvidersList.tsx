'use client';

import { KNOWN_PROVIDERS } from '@/lib/api/schemas/providers';

import { ProviderCard } from './ProviderCard';

/**
 * Issue #936 (G5) — Grid of provider cards. Static list mirrors backend DI registration.
 */
export function ProvidersList() {
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {KNOWN_PROVIDERS.map(name => (
        <ProviderCard key={name} name={name} />
      ))}
    </div>
  );
}

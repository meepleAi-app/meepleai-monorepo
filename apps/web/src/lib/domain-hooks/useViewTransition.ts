'use client';

import { useCallback } from 'react';

import { useRouter } from 'next/navigation';

export function useViewTransition() {
  const router = useRouter();

  const navigateWithTransition = useCallback(
    (href: string) => {
      if (typeof document.startViewTransition !== 'function') {
        router.push(href);
        return;
      }
      document.startViewTransition(() => {
        router.push(href);
      });
    },
    [router]
  );

  return { navigateWithTransition };
}

'use client';

import { useEffect } from 'react';

import { useRouter, useSearchParams } from 'next/navigation';

interface AdminTabPersistenceProps {
  hubName: string;
  defaultTab: string;
}

const STORAGE_PREFIX = 'admin-tab-';

/**
 * Client component that persists the active hub tab in localStorage.
 * - Saves the current tab on every render where ?tab is present.
 * - Redirects to the saved tab when no ?tab param exists (e.g., navigating back).
 * - URL param always takes precedence over localStorage.
 * Renders nothing — purely behavioral.
 */
export function AdminTabPersistence({ hubName, defaultTab }: AdminTabPersistenceProps) {
  const router = useRouter();
  const searchParams = useSearchParams();
  const currentTab = searchParams.get('tab');
  const storageKey = `${STORAGE_PREFIX}${hubName}`;

  useEffect(() => {
    if (currentTab) {
      try {
        localStorage.setItem(storageKey, currentTab);
      } catch {
        // localStorage unavailable — ignore
      }
    } else {
      try {
        const saved = localStorage.getItem(storageKey);
        if (saved && saved !== defaultTab) {
          router.replace(`/admin/${hubName}?tab=${saved}`);
        }
      } catch {
        // localStorage unavailable — ignore
      }
    }
  }, [currentTab, storageKey, defaultTab, hubName, router]);

  return null;
}

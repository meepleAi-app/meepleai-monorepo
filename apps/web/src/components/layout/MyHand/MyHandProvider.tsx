'use client';

import React, { useEffect } from 'react';

import { getMyHand } from '@/lib/api/my-hand';
import { useMyHandStore } from '@/stores/my-hand/store';

export function MyHandProvider({ children }: { children: React.ReactNode }): React.JSX.Element {
  const hydrateFromServer = useMyHandStore(s => s.hydrateFromServer);
  const setLoading = useMyHandStore(s => s.setLoading);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      try {
        const slots = await getMyHand();
        if (!cancelled) hydrateFromServer(slots);
      } catch {
        // Silently fail — store remains empty (default state)
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    load();
    return () => {
      cancelled = true;
    };
  }, [hydrateFromServer, setLoading]);

  return <>{children}</>;
}

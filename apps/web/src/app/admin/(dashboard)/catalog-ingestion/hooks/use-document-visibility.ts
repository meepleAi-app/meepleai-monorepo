'use client';
import { useEffect, useState } from 'react';

export function useDocumentVisibility(): boolean {
  const [isVisible, setIsVisible] = useState<boolean>(() =>
    typeof document === 'undefined' ? true : !document.hidden
  );

  useEffect(() => {
    const handler = () => setIsVisible(!document.hidden);
    document.addEventListener('visibilitychange', handler);
    return () => document.removeEventListener('visibilitychange', handler);
  }, []);

  return isVisible;
}

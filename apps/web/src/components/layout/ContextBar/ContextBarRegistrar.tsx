'use client';

import { useEffect, type ReactNode } from 'react';

import { useContextBarStore } from '@/lib/stores/context-bar-store';

interface ContextBarRegistrarProps {
  children: ReactNode;
  alwaysVisible?: boolean;
}

export function ContextBarRegistrar({ children, alwaysVisible = false }: ContextBarRegistrarProps) {
  const setContent = useContextBarStore(s => s.setContent);
  const setOptions = useContextBarStore(s => s.setOptions);
  const clear = useContextBarStore(s => s.clear);

  useEffect(() => {
    setContent(children);
    if (alwaysVisible) setOptions({ alwaysVisible: true });
    return () => clear();
  }, [children, alwaysVisible, setContent, setOptions, clear]);

  return null;
}

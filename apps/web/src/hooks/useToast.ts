import { useCallback } from 'react';

import { toast as sonnerToast } from 'sonner';

export interface Toast {
  title: string;
  description?: string;
  variant?: 'default' | 'destructive';
}

/**
 * Adattatore sul `Toaster` di `sonner` montato in `app/providers.tsx`.
 *
 * `toast` ha identità stabile per tutta la vita del componente: più consumatori lo mettono
 * nell'array di dipendenze di un `useEffect` che fa un fetch al mount, e se cambiasse a ogni
 * render quel fetch ripartirebbe di continuo.
 */
export function useToast() {
  const toast = useCallback(({ title, description, variant }: Toast) => {
    if (variant === 'destructive') {
      sonnerToast.error(title, { description });
    } else {
      sonnerToast(title, { description });
    }
  }, []);

  return { toast };
}

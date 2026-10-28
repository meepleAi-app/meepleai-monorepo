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
    // Lo stesso testo con lo stesso tipo riusa il toast già a schermo invece di impilarne un altro.
    // Chi interroga un servizio a intervalli (le tab del monitor admin) con il backend giù prima non
    // mostrava niente: ora avrebbe un toast a ogni giro.
    const id = `${variant ?? 'default'}|${title}|${description ?? ''}`;
    if (variant === 'destructive') {
      sonnerToast.error(title, { id, description });
    } else {
      sonnerToast(title, { id, description });
    }
  }, []);

  return { toast };
}

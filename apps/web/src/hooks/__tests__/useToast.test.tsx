/**
 * useToast — contratto osservabile (#4343).
 *
 * L'hook era un segnaposto che scriveva su `console.warn`: i trenta componenti che lo usano
 * (riavvii dei servizi admin, coda dei PDF, notifiche push, serate di gioco) mostravano
 * errori e conferme che nessuno vedeva mai. Il `Toaster` montato in `app/providers.tsx` è
 * quello di `sonner`, che l'hook non usava.
 *
 * I test usano il vero `Toaster` e l'hook vero: ciò che si afferma è che il testo arriva
 * nel DOM, non che una funzione sia stata chiamata.
 */
import { act, render, renderHook, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { toast as sonnerToast } from 'sonner';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { Toaster } from '@/components/ui/feedback/sonner';

import { useToast } from '../useToast';

function Trigger({
  title,
  description,
  variant,
}: {
  title: string;
  description?: string;
  variant?: 'default' | 'destructive';
}): React.JSX.Element {
  const { toast } = useToast();
  return (
    <button type="button" onClick={() => toast({ title, description, variant })}>
      fire
    </button>
  );
}

describe('useToast', () => {
  afterEach(() => {
    act(() => {
      sonnerToast.dismiss();
    });
    vi.restoreAllMocks();
  });

  it('shows the title and description to the user', async () => {
    const user = userEvent.setup();
    render(
      <>
        <Toaster />
        <Trigger title="Salvato" description="Le preferenze sono state aggiornate" />
      </>
    );

    await user.click(screen.getByRole('button', { name: 'fire' }));

    expect(await screen.findByText('Salvato')).toBeInTheDocument();
    expect(screen.getByText('Le preferenze sono state aggiornate')).toBeInTheDocument();
  });

  it('shows a destructive toast as an error', async () => {
    const user = userEvent.setup();
    render(
      <>
        <Toaster />
        <Trigger title="Errore durante il riavvio" variant="destructive" />
      </>
    );

    await user.click(screen.getByRole('button', { name: 'fire' }));

    const message = await screen.findByText('Errore durante il riavvio');
    expect(message.closest('[data-type]')).toHaveAttribute('data-type', 'error');
  });

  it('does not write the toast to the console instead of showing it', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const user = userEvent.setup();
    render(
      <>
        <Toaster />
        <Trigger title="Riavvio API in corso..." />
      </>
    );

    await user.click(screen.getByRole('button', { name: 'fire' }));
    await screen.findByText('Riavvio API in corso...');

    expect(warn.mock.calls.filter(call => call[0] === '[Toast]')).toHaveLength(0);
  });

  it('shows the description of a destructive toast too', async () => {
    const user = userEvent.setup();
    render(
      <>
        <Toaster />
        <Trigger
          title="Errore"
          description="Impossibile caricare le preferenze"
          variant="destructive"
        />
      </>
    );

    await user.click(screen.getByRole('button', { name: 'fire' }));

    expect(await screen.findByText('Impossibile caricare le preferenze')).toBeInTheDocument();
  });

  it('does not show a default toast as an error', async () => {
    const user = userEvent.setup();
    render(
      <>
        <Toaster />
        <Trigger title="Salvato" />
      </>
    );

    await user.click(screen.getByRole('button', { name: 'fire' }));

    const message = await screen.findByText('Salvato');
    // Un toast normale non porta `data-type`: si cerca l'elemento del toast, non l'attributo.
    const toastElement = message.closest('[data-sonner-toast]');
    expect(toastElement).not.toBeNull();
    expect(toastElement?.getAttribute('data-type')).not.toBe('error');
  });

  it('shows the same toast once, however many times it is fired', async () => {
    // Le tab del monitor admin interrogano un servizio a intervalli: a backend giù, lo stesso
    // errore a ogni giro non deve impilare un toast dopo l'altro.
    const user = userEvent.setup();
    render(
      <>
        <Toaster />
        <Trigger title="Errore di caricamento" variant="destructive" />
      </>
    );

    await user.click(screen.getByRole('button', { name: 'fire' }));
    await user.click(screen.getByRole('button', { name: 'fire' }));
    await user.click(screen.getByRole('button', { name: 'fire' }));

    await screen.findByText('Errore di caricamento');
    expect(screen.getAllByText('Errore di caricamento')).toHaveLength(1);
  });

  it('keeps two different toasts apart', async () => {
    const user = userEvent.setup();
    render(
      <>
        <Toaster />
        <Trigger title="Primo" />
        <Trigger title="Secondo" />
      </>
    );

    const [first, second] = screen.getAllByRole('button', { name: 'fire' });
    await user.click(first);
    await user.click(second);

    expect(await screen.findByText('Primo')).toBeInTheDocument();
    expect(await screen.findByText('Secondo')).toBeInTheDocument();
  });

  it('keeps `toast` referentially stable across renders', () => {
    // AiConsentSection e NotificationPreferences hanno `useEffect(..., [toast])` che fa un
    // fetch al mount: se `toast` cambiasse a ogni render, il fetch ripartirebbe di continuo e
    // un errore mostrerebbe un toast a ogni giro.
    const { result, rerender } = renderHook(() => useToast());
    const first = result.current.toast;

    rerender();
    rerender();

    expect(result.current.toast).toBe(first);
  });

  it('does not re-render its host when a toast is fired', async () => {
    // Il segnaposto teneva i toast in uno useState: ogni toast faceva ripartire il componente
    // ospite, due volte (all'invio e dopo tre secondi).
    let renders = 0;
    const { result } = renderHook(() => {
      renders++;
      return useToast();
    });
    const before = renders;

    act(() => {
      result.current.toast({ title: 'Fatto' });
    });

    expect(renders).toBe(before);
  });
});

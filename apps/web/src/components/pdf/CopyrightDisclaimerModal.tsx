'use client';

import { Check, FileText } from 'lucide-react';

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/overlays/alert-dialog-primitives';

interface CopyrightDisclaimerModalProps {
  open: boolean;
  onAccept: () => void;
  onCancel: () => void;
}

const DISCLAIMER_ITEMS = [
  'Possiedo una copia fisica o digitale legittima di questo gioco',
  'Il PDF verrà usato solo come riferimento personale durante le mie sessioni di gioco',
  'Il contenuto non verrà redistribuito',
] as const;

export function CopyrightDisclaimerModal({
  open,
  onAccept,
  onCancel,
}: CopyrightDisclaimerModalProps) {
  return (
    <AlertDialog
      open={open}
      onOpenChange={isOpen => {
        if (!isOpen) onCancel();
      }}
    >
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle className="flex items-center gap-2">
            <FileText className="h-5 w-5 text-amber-500" aria-hidden="true" />
            Caricamento Regolamento
          </AlertDialogTitle>
          <AlertDialogDescription asChild>
            <div className="space-y-3">
              <p>Per utilizzare l&apos;assistente AI con questo gioco, confermo che:</p>
              <ul className="space-y-2">
                {DISCLAIMER_ITEMS.map(item => (
                  <li key={item} className="flex items-start gap-2">
                    <Check
                      className="h-4 w-4 mt-0.5 text-green-500 flex-shrink-0"
                      aria-hidden="true"
                    />
                    <span>{item}</span>
                  </li>
                ))}
              </ul>
              <p className="text-xs text-muted-foreground">
                Il regolamento viene analizzato dall&apos;AI per rispondere alle tue domande sulle
                regole. Il file originale non viene condiviso con altri utenti.
              </p>
            </div>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel aria-label="Annulla">Annulla</AlertDialogCancel>
          <AlertDialogAction onClick={onAccept} aria-label="Confermo e carico">
            Confermo e carico
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

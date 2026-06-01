'use client';

import { useEffect, useId, useState, type ReactElement } from 'react';

import * as Dialog from '@radix-ui/react-dialog';

import { RadioGroup, RadioGroupItem } from '@/components/ui/primitives/radio-group';
import { LANG_CODES_ORDER, LANG_LABELS_IT, type SourceLangCode } from '@/lib/gamebook/lang-codes';

export interface LangOverrideModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Preselected radio option (if any). */
  preselect?: SourceLangCode;
  /** When false, hide the Chiudi button and ignore Escape (forced-choice mode for auto-low). */
  dismissable: boolean;
  /** Called when user confirms with a selected lang. */
  onConfirm: (lang: SourceLangCode) => void;
}

export function LangOverrideModal({
  open,
  onOpenChange,
  preselect,
  dismissable,
  onConfirm,
}: LangOverrideModalProps): ReactElement {
  const [selected, setSelected] = useState<SourceLangCode | undefined>(preselect);
  const titleId = useId();
  const radioName = useId();

  // Sync selected when preselect or open changes (modal reopen resets state).
  useEffect(() => {
    if (open) {
      setSelected(preselect);
    }
  }, [open, preselect]);

  const handleConfirm = () => {
    if (selected) onConfirm(selected);
  };

  return (
    <Dialog.Root
      open={open}
      onOpenChange={next => {
        // Block Escape/outside-click dismiss when not dismissable
        if (!next && !dismissable) return;
        onOpenChange(next);
      }}
    >
      <Dialog.Portal>
        <Dialog.Overlay
          // eslint-disable-next-line local/no-hardcoded-color-utility -- modal scrim canonical pattern, mirrors apps/web/src/components/ui/drawer/drawer.tsx OVERLAY_CLS
          className="fixed inset-0 z-50 bg-black/40 backdrop-blur-sm data-[state=open]:animate-in data-[state=open]:fade-in"
          data-testid="lang-override-scrim"
        />
        <Dialog.Content
          className="fixed left-1/2 top-1/2 z-50 w-full max-w-md -translate-x-1/2 -translate-y-1/2 rounded-lg bg-background p-6 shadow-lg"
          aria-labelledby={titleId}
          aria-modal="true"
          onEscapeKeyDown={e => {
            if (!dismissable) e.preventDefault();
          }}
          onPointerDownOutside={e => {
            if (!dismissable) e.preventDefault();
          }}
        >
          <Dialog.Title id={titleId} className="text-lg font-semibold">
            Seleziona la lingua sorgente
          </Dialog.Title>
          <Dialog.Description className="mt-1 text-sm text-muted-foreground">
            Conferma la lingua originale della pagina per migliorare la traduzione.
          </Dialog.Description>

          <RadioGroup
            value={selected}
            onValueChange={v => setSelected(v as SourceLangCode)}
            name={radioName}
            className="mt-4 gap-3"
          >
            {LANG_CODES_ORDER.map(code => (
              <label
                key={code}
                htmlFor={`${radioName}-${code}`}
                className="flex cursor-pointer items-center gap-2 text-sm"
              >
                <RadioGroupItem id={`${radioName}-${code}`} value={code} />
                <span>{LANG_LABELS_IT[code]}</span>
              </label>
            ))}
          </RadioGroup>

          <div className="mt-6 flex justify-end gap-2">
            {dismissable && (
              <button
                type="button"
                onClick={() => onOpenChange(false)}
                className="rounded-md px-4 py-2 text-sm font-medium text-muted-foreground hover:bg-muted"
              >
                Chiudi
              </button>
            )}
            <button
              type="button"
              onClick={handleConfirm}
              disabled={!selected}
              className="rounded-md bg-[var(--c-agent)] px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
              data-testid="lang-override-confirm"
            >
              Conferma e ritraduci
            </button>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}

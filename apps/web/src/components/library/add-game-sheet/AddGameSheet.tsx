'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { ChevronLeft, ChevronRight, X } from 'lucide-react';

import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetFooter,
} from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/button';
import { StepIndicator } from './StepIndicator';
import {
  useAddGameWizardStore,
  type WizardEntryPoint,
  type SelectedGameData,
} from '@/lib/stores/add-game-wizard-store';

/**
 * Props for the AddGameSheet wizard drawer.
 * Issue #4818: AddGameSheet Drawer + State Machine
 * Epic #4817: User Collection Wizard
 */
export interface AddGameSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  entryPoint: WizardEntryPoint;
  gameData?: SelectedGameData;
  onSuccess?: (libraryEntryId: string) => void;
}

const WIZARD_STEPS = [
  { label: 'Sorgente', description: 'Scegli il gioco' },
  { label: 'PDF', description: 'Knowledge base' },
  { label: 'Info & Salva', description: 'Conferma e salva' },
];

const stepAnimationVariants = {
  enter: (direction: number) => ({
    x: direction > 0 ? 100 : -100,
    opacity: 0,
  }),
  center: {
    x: 0,
    opacity: 1,
  },
  exit: (direction: number) => ({
    x: direction < 0 ? 100 : -100,
    opacity: 0,
  }),
};

/**
 * AddGameSheet - Main wizard drawer for adding games to collection.
 * Issue #4818: AddGameSheet Drawer + State Machine
 */
export function AddGameSheet({
  open,
  onOpenChange,
  entryPoint,
  gameData,
  onSuccess,
}: AddGameSheetProps) {
  // Select only primitive/stable values from the store
  const currentStep = useAddGameWizardStore((s) => s.currentStep);
  const isDirty = useAddGameWizardStore((s) => s.isDirty);
  const initialize = useAddGameWizardStore((s) => s.initialize);
  const canGoNext = useAddGameWizardStore((s) => s.canGoNext);

  const [direction, setDirection] = useState(0);
  const [showCloseConfirm, setShowCloseConfirm] = useState(false);
  const initializedRef = useRef(false);

  // Initialize wizard when first opened (guard against unstable gameData refs)
  useEffect(() => {
    if (open) {
      initializedRef.current = true;
      initialize(entryPoint, gameData);
    } else {
      initializedRef.current = false;
    }
  }, [open, entryPoint]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleClose = useCallback(() => {
    if (useAddGameWizardStore.getState().isDirty) {
      setShowCloseConfirm(true);
      return;
    }
    useAddGameWizardStore.getState().reset();
    onOpenChange(false);
  }, [onOpenChange]);

  const handleConfirmClose = useCallback(() => {
    setShowCloseConfirm(false);
    useAddGameWizardStore.getState().reset();
    onOpenChange(false);
  }, [onOpenChange]);

  const handleNext = useCallback(() => {
    const state = useAddGameWizardStore.getState();
    if (state.canGoNext()) {
      setDirection(1);
      state.goNext();
    }
  }, []);

  const handleBack = useCallback(() => {
    setDirection(-1);
    useAddGameWizardStore.getState().goBack();
  }, []);

  const isFirstStep = entryPoint.type === 'fromGameCard' || entryPoint.type === 'fromSearch'
    ? currentStep === 2
    : currentStep === 1;
  const isLastStep = currentStep === 3;

  return (
    <Sheet open={open} onOpenChange={(o) => !o && handleClose()}>
      <SheetContent
        side="right"
        className="w-full sm:max-w-[520px] lg:max-w-[580px] flex flex-col p-0"
      >
        {/* Header with step indicator */}
        <SheetHeader className="border-b border-slate-800 px-6 pt-6 pb-4 space-y-4">
          <div className="flex items-center justify-between">
            <SheetTitle className="text-lg font-semibold text-slate-100">
              Aggiungi alla Collezione
            </SheetTitle>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8 text-slate-400 hover:text-slate-200"
              onClick={handleClose}
            >
              <X className="h-4 w-4" />
              <span className="sr-only">Chiudi</span>
            </Button>
          </div>
          <StepIndicator currentStep={currentStep} steps={WIZARD_STEPS} />
        </SheetHeader>

        {/* Step content with animation */}
        <div className="flex-1 overflow-y-auto px-6 py-6">
          <AnimatePresence mode="wait" custom={direction}>
            <motion.div
              key={currentStep}
              custom={direction}
              variants={stepAnimationVariants}
              initial="enter"
              animate="center"
              exit="exit"
              transition={{ duration: 0.2, ease: 'easeInOut' }}
            >
              {currentStep === 1 && (
                <StepPlaceholder
                  step={1}
                  title="Scegli Sorgente Gioco"
                  description="Cerca nel catalogo, importa da BGG, o crea manualmente"
                />
              )}
              {currentStep === 2 && (
                <StepPlaceholder
                  step={2}
                  title="Knowledge Base & PDF"
                  description="Visualizza e gestisci i PDF del regolamento"
                />
              )}
              {currentStep === 3 && (
                <StepPlaceholder
                  step={3}
                  title="Informazioni & Salva"
                  description="Rivedi le info del gioco e salva nella collezione"
                />
              )}
            </motion.div>
          </AnimatePresence>
        </div>

        {/* Footer with navigation */}
        <SheetFooter className="border-t border-slate-800 px-6 py-4 flex-row gap-3">
          {!isFirstStep && (
            <Button
              variant="outline"
              onClick={handleBack}
              className="gap-1.5"
            >
              <ChevronLeft className="h-4 w-4" />
              Indietro
            </Button>
          )}
          <div className="flex-1" />
          {!isLastStep && (
            <Button
              onClick={handleNext}
              disabled={!canGoNext()}
              className="gap-1.5"
            >
              Avanti
              <ChevronRight className="h-4 w-4" />
            </Button>
          )}
        </SheetFooter>

        {/* Close confirmation overlay */}
        {showCloseConfirm && (
          <div className="absolute inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm">
            <div className="mx-6 rounded-xl border border-slate-700 bg-slate-900 p-6 shadow-xl max-w-sm">
              <h3 className="text-base font-semibold text-slate-100 mb-2">
                Modifiche non salvate
              </h3>
              <p className="text-sm text-slate-400 mb-5">
                Hai modifiche non salvate. Vuoi chiudere e perdere le modifiche?
              </p>
              <div className="flex gap-3 justify-end">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setShowCloseConfirm(false)}
                >
                  Annulla
                </Button>
                <Button
                  variant="destructive"
                  size="sm"
                  onClick={handleConfirmClose}
                >
                  Chiudi
                </Button>
              </div>
            </div>
          </div>
        )}
      </SheetContent>
    </Sheet>
  );
}

/**
 * Temporary placeholder for wizard steps.
 * Will be replaced by actual step components in issues #4819, #4820, #4821.
 */
function StepPlaceholder({
  step,
  title,
  description,
}: {
  step: number;
  title: string;
  description: string;
}) {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center">
      <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-slate-800 text-2xl font-bold text-teal-400 mb-4">
        {step}
      </div>
      <h3 className="text-lg font-semibold text-slate-200 mb-1">{title}</h3>
      <p className="text-sm text-slate-500 max-w-xs">{description}</p>
    </div>
  );
}

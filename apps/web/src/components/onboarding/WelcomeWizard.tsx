'use client';

import { useState } from 'react';

import { motion, AnimatePresence, useReducedMotion } from 'framer-motion';
import { Library, Bot, Gamepad2, Globe, PartyPopper, Rocket } from 'lucide-react';

import { Dialog, DialogContent, DialogTitle } from '@/components/ui/overlays/dialog';
import { useAuthUser } from '@/hooks/useAuthUser';

import { ONBOARDING_STEPS } from './onboarding-steps';
import { useOnboardingStatus } from './use-onboarding-status';

const FEATURES = [
  {
    icon: Library,
    label: 'Libreria Giochi',
    description: 'Gestisci la tua collezione',
    bg: 'bg-amber-50',
  },
  { icon: Bot, label: 'Assistente AI', description: 'Chiedi regole e strategie', bg: 'bg-blue-50' },
  {
    icon: Gamepad2,
    label: 'Sessioni di Gioco',
    description: 'Traccia le partite',
    bg: 'bg-green-50',
  },
  {
    icon: Globe,
    label: 'Catalogo Community',
    description: 'Scopri nuovi giochi',
    bg: 'bg-purple-50',
  },
] as const;

export function WelcomeWizard() {
  const { showWizard, markWizardSeen } = useOnboardingStatus();
  const { user } = useAuthUser();
  const [step, setStep] = useState(0);
  const shouldReduceMotion = useReducedMotion();

  const displayName = user?.displayName ?? user?.email?.split('@')[0] ?? 'Giocatore';

  function handleClose() {
    markWizardSeen();
  }

  function handleNext() {
    if (step < 2) {
      setStep(s => s + 1);
    } else {
      handleClose();
    }
  }

  const motionProps = shouldReduceMotion
    ? {}
    : {
        initial: { opacity: 0, y: 8 },
        animate: { opacity: 1, y: 0 },
        exit: { opacity: 0, y: -8 },
        transition: { duration: 0.25 },
      };

  return (
    <Dialog
      open={showWizard}
      onOpenChange={open => {
        if (!open) handleClose();
      }}
    >
      <DialogContent
        className="max-w-lg p-0 gap-0 overflow-hidden [&>button]:z-10"
        aria-describedby={undefined}
      >
        <DialogTitle className="sr-only">Benvenuto in MeepleAI</DialogTitle>
        <div className="p-6 sm:p-8">
          <AnimatePresence mode="wait">
            {step === 0 && (
              <motion.div key="step-0" {...motionProps} className="text-center space-y-4">
                <PartyPopper className="w-12 h-12 mx-auto text-amber-500" />
                <h2 className="font-quicksand text-2xl font-bold text-foreground">
                  Ciao {displayName}!
                </h2>
                <p className="font-quicksand text-xl font-semibold text-foreground/80">
                  Benvenuto in MeepleAI
                </p>
                <p className="text-muted-foreground font-nunito text-sm max-w-sm mx-auto">
                  Il tuo assistente AI per i giochi da tavolo. Regole, strategie e organizzazione
                  delle serate — tutto in un posto.
                </p>
              </motion.div>
            )}

            {step === 1 && (
              <motion.div key="step-1" {...motionProps} className="space-y-4">
                <h2 className="font-quicksand text-xl font-bold text-center">
                  Cosa puoi fare con MeepleAI
                </h2>
                <div className="grid grid-cols-2 gap-3">
                  {FEATURES.map(({ icon: Icon, label, description, bg }) => (
                    <div key={label} className={`${bg} rounded-xl p-3 text-center`}>
                      <Icon className="w-6 h-6 mx-auto text-foreground/70" />
                      <p className="font-quicksand font-semibold text-sm mt-1.5">{label}</p>
                      <p className="text-xs text-muted-foreground font-nunito">{description}</p>
                    </div>
                  ))}
                </div>
              </motion.div>
            )}

            {step === 2 && (
              <motion.div key="step-2" {...motionProps} className="text-center space-y-4">
                <Rocket className="w-12 h-12 mx-auto text-amber-500" />
                <h2 className="font-quicksand text-xl font-bold">Tutto pronto!</h2>
                <p className="text-muted-foreground font-nunito text-sm max-w-sm mx-auto">
                  Abbiamo preparato alcuni suggerimenti per iniziare. Li troverai nella tua
                  dashboard.
                </p>
                <div className="bg-muted/50 rounded-xl p-4 text-left max-w-xs mx-auto">
                  <p className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
                    La tua checklist
                  </p>
                  {ONBOARDING_STEPS.map(s => (
                    <div key={s.id} className="flex items-center gap-2 py-1.5 text-sm font-nunito">
                      <span className="w-4 h-4 rounded-full border-2 border-muted-foreground/30 shrink-0" />
                      {s.title}
                    </div>
                  ))}
                </div>
              </motion.div>
            )}
          </AnimatePresence>

          {/* Dots indicator */}
          <div className="flex justify-center gap-1.5 mt-6">
            {[0, 1, 2].map(i => (
              <span
                key={i}
                className={`w-6 h-1 rounded-full transition-colors ${
                  i === step ? 'bg-amber-500' : 'bg-muted-foreground/20'
                }`}
              />
            ))}
          </div>

          {/* CTA */}
          <div className="mt-5 text-center">
            <button
              onClick={handleNext}
              className="inline-flex items-center justify-center px-6 py-2.5 rounded-lg bg-amber-500 text-white font-quicksand font-semibold text-sm hover:bg-amber-600 transition-colors"
            >
              {step === 0 && 'Iniziamo →'}
              {step === 1 && 'Avanti →'}
              {step === 2 && 'Vai alla Dashboard'}
            </button>
            {step < 2 && (
              <p
                onClick={handleClose}
                className="text-xs text-muted-foreground mt-3 cursor-pointer hover:text-foreground transition-colors"
              >
                Salta introduzione
              </p>
            )}
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

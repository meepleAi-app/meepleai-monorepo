'use client';

import { useCallback, useState, type ReactElement } from 'react';

import { Dialog, DialogContent } from '@/components/ui/overlays/dialog';
import { CHECKOUT_PACKS, formatEur, type CheckoutPackId } from '@/lib/gamebook/checkout-packs';

import { CheckoutStepIndicator } from './checkout/CheckoutStepIndicator';
import { Step1QuotaReached, type Step1Labels } from './checkout/Step1QuotaReached';
import { Step2PackPicker, type Step2Labels } from './checkout/Step2PackPicker';
import {
  Step3CheckoutForm,
  type Step3Labels,
  type Step3SubState,
} from './checkout/Step3CheckoutForm';
import { Step4Success, type Step4Labels } from './checkout/Step4Success';

type ModalStep = 1 | 2 | 3 | 4;

export interface CheckoutLabels {
  readonly modalTitle: (step: ModalStep) => string;
  readonly close: string;
  readonly stepIndicator: {
    readonly step1: string;
    readonly step2: string;
    readonly step3: string;
    readonly step4: string;
    readonly ariaCurrent: (step: ModalStep) => string;
  };
  readonly step1: Step1Labels;
  readonly step2: Step2Labels;
  readonly step3: Omit<Step3Labels, 'summary' | 'payCta' | 'recapValues'> & {
    readonly summary: (packName: string, credits: number, eur: string) => string;
    readonly payCta: (eur: string) => string;
  };
  readonly step4: Omit<Step4Labels, 'subtitle' | 'receiptLink'> & {
    readonly subtitle: (credits: number) => string;
    readonly receiptLink: (email: string) => string;
  };
}

export interface CheckoutQuota {
  readonly used: number;
  readonly total: number;
  readonly resetDate: string;
  /**
   * Existing credit balance shown in Step 4 recap as "Crediti precedenti".
   * MVP: always 0 (no balance persistence). Future PR introducing real
   * credit accounting will source this from `UserBudgetStatus.creditBalance`.
   */
  readonly previousCredits: number;
}

export interface CheckoutModalProps {
  readonly open: boolean;
  readonly initialStep: 1 | 2;
  readonly quota: CheckoutQuota;
  readonly userEmail: string;
  readonly labels: CheckoutLabels;
  readonly onClose: () => void;
  readonly onPurchaseSuccess: (packId: CheckoutPackId, creditsAdded: number) => void;
  /**
   * TEST-ONLY deterministic override. When set, the simulated payment latency
   * still runs (2s) but the outcome is forced. Production code never sets this.
   */
  readonly __testPaymentResult?: 'success' | 'failed';
}

const PAYMENT_LATENCY_MS = 2000;

export function CheckoutModal({
  open,
  initialStep,
  quota,
  userEmail,
  labels,
  onClose,
  onPurchaseSuccess,
  __testPaymentResult,
}: CheckoutModalProps): ReactElement {
  const [step, setStep] = useState<ModalStep>(initialStep);
  const [selectedPack, setSelectedPack] = useState<CheckoutPackId>('starter');
  const [paymentSubState, setPaymentSubState] = useState<Step3SubState>('filled');

  const pack = CHECKOUT_PACKS.find((p) => p.id === selectedPack)!;
  const priceEurString = formatEur(pack.priceEur);

  const handleOpenChange = useCallback(
    (next: boolean) => {
      if (!next) {
        setStep(initialStep);
        setPaymentSubState('filled');
        onClose();
      }
    },
    [onClose, initialStep]
  );

  const handlePay = useCallback(() => {
    setPaymentSubState('loading');
    const outcome: 'success' | 'failed' = __testPaymentResult ?? 'success';
    setTimeout(() => {
      if (outcome === 'success') {
        onPurchaseSuccess(pack.id, pack.credits);
        setStep(4);
        setPaymentSubState('filled');
      } else {
        setPaymentSubState('failed');
      }
    }, PAYMENT_LATENCY_MS);
  }, [__testPaymentResult, onPurchaseSuccess, pack.id, pack.credits]);

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent
        data-slot="checkout-modal"
        aria-labelledby="checkout-modal-title"
        className="max-w-[520px] gap-0 overflow-hidden p-0"
      >
        <header className="flex items-center gap-2 p-3.5 pb-0">
          <CheckoutStepIndicator
            currentStep={step}
            labels={{
              step1: labels.stepIndicator.step1,
              step2: labels.stepIndicator.step2,
              step3: labels.stepIndicator.step3,
              step4: labels.stepIndicator.step4,
              ariaCurrent: labels.stepIndicator.ariaCurrent(step),
            }}
          />
        </header>
        <h2 id="checkout-modal-title" className="sr-only">
          {labels.modalTitle(step)}
        </h2>
        <div data-slot="checkout-modal-body" className="max-h-[80vh] overflow-y-auto">
          {step === 1 && (
            <Step1QuotaReached
              used={quota.used}
              total={quota.total}
              labels={labels.step1}
              onPrimaryClick={() => setStep(2)}
              onSecondaryClick={onClose}
            />
          )}
          {step === 2 && (
            <Step2PackPicker
              selectedId={selectedPack}
              labels={labels.step2}
              onSelect={setSelectedPack}
              onContinue={() => setStep(3)}
            />
          )}
          {step === 3 && (
            <Step3CheckoutForm
              subState={paymentSubState}
              labels={{
                ...labels.step3,
                summary: labels.step3.summary(
                  labels.step2.packNames[pack.id],
                  pack.credits,
                  priceEurString
                ),
                payCta: labels.step3.payCta(priceEurString),
                recapValues: { credits: priceEurString, total: priceEurString },
              }}
              onPay={handlePay}
              onBack={() => setStep(2)}
            />
          )}
          {step === 4 && (
            <Step4Success
              previousCredits={quota.previousCredits}
              purchasedCredits={pack.credits}
              freeQuotaUsed={quota.used}
              freeQuotaTotal={quota.total}
              labels={{
                ...labels.step4,
                subtitle: labels.step4.subtitle(pack.credits),
                receiptLink: labels.step4.receiptLink(userEmail),
              }}
              onBackToGame={onClose}
            />
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}

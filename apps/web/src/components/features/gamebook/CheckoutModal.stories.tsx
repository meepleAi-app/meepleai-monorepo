/**
 * @mockup admin-mockups/design_files/librogame-runthrough-quota-credits.html
 *
 * CheckoutModal + SoftWarningCredits argTypes matrix story — DS-17 Phase D-2 (sub-issue #2174).
 *
 * State-driving strategy (STATIC — test-seam props, no play functions):
 *   CheckoutModal manages `step` (1-4) + `paymentSubState` ('filled'|'loading'|'failed')
 *   via internal `useState`. To render each mockup state on first paint without
 *   user interaction, two test-seam props were added to the real component:
 *
 *   1. `__initialStep?: ModalStep` — overrides `initialStep` for step 3 and step 4.
 *      (Steps 1 and 2 are driven via the existing public `initialStep` prop.)
 *
 *   2. `__initialPaymentSubState?: Step3SubState` — pre-seeds `paymentSubState` so
 *      'loading' (spinner) and 'failed' (red banner) render on first paint.
 *
 *   Frame07 (SoftWarningCredits) is rendered separately via a `render` override —
 *   it is a standalone component, not a step inside CheckoutModal.
 *
 * 7 mockup states → 7 story frames:
 *   Frame01_Step1Quota    — initialStep=1 (quota 50/50, fill bar 100%)
 *   Frame02_Step2Picker   — initialStep=2 (Starter selected default)
 *   Frame03_Step3Form     — __initialStep=3, paymentSubState='filled'
 *   Frame04_Step3Loading  — __initialStep=3, __initialPaymentSubState='loading'
 *   Frame05_Step3Failed   — __initialStep=3, __initialPaymentSubState='failed'
 *   Frame06_Step4Success  — __initialStep=4 (confetti + credits recap)
 *   Frame07_SoftWarning   — SoftWarningCredits variant='modal-desktop' (47/50)
 *
 * No MSW handlers required — CheckoutModal and SoftWarningCredits are fully
 * client-side visual-only components (no real Stripe, no API calls).
 *
 * BGG guard: no BGG refs in this file (#2123).
 *
 * Refs: spec docs/superpowers/specs/2026-06-22-ds-17-phase-d-2-librogame-storybook-migration-design.md,
 *       umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import {
  MOCK_CHECKOUT_LABELS,
  MOCK_CHECKOUT_QUOTA_FULL,
  MOCK_CHECKOUT_USER_EMAIL,
  MOCK_SOFT_WARNING_LABELS,
  MOCK_SOFT_WARNING_TOTAL,
  MOCK_SOFT_WARNING_USED,
} from '@/__tests__/fixtures/mockup-pilots/librogame/librogame-quota-credits';

import { CheckoutModal } from './CheckoutModal';
import { SoftWarningCredits } from './SoftWarningCredits';

import type { Meta, StoryObj } from '@storybook/react';

// ---------------------------------------------------------------------------
// Meta
// ---------------------------------------------------------------------------

const meta: Meta<typeof CheckoutModal> = {
  title: 'Pages/Librogame/Quota Credits',
  component: CheckoutModal,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di librogame-runthrough-quota-credits.html 7 stati. ' +
          'CheckoutModal 6 frame (step1→step4 + loading + failed) guidati via test-seam props. ' +
          'Frame07 SoftWarningCredits come render override (componente separato).',
      },
    },
  },
  args: {
    open: true,
    initialStep: 1,
    quota: MOCK_CHECKOUT_QUOTA_FULL,
    userEmail: MOCK_CHECKOUT_USER_EMAIL,
    labels: MOCK_CHECKOUT_LABELS,
    onClose: () => {},
    onPurchaseSuccess: () => {},
  },
  argTypes: {
    initialStep: {
      control: { type: 'select' },
      options: [1, 2],
      description: 'Public starting step (1 or 2). Use __initialStep to reach step 3/4.',
    },
    __initialStep: {
      control: { type: 'select' },
      options: [1, 2, 3, 4],
      description: '[Test-seam] Override step on mount — bypasses initialStep restriction.',
    },
    __initialPaymentSubState: {
      control: { type: 'select' },
      options: ['filled', 'loading', 'failed'],
      description: '[Test-seam] Override paymentSubState on mount (step 3 only).',
    },
    __testPaymentResult: {
      control: { type: 'select' },
      options: ['success', 'failed'],
      description: '[Test-seam] Forces payment outcome after 2s timer fires.',
    },
    open: {
      control: 'boolean',
      description: 'Whether the Dialog is open.',
    },
  },
};
export default meta;

type Story = StoryObj<typeof CheckoutModal>;

// ── Frame 01 · Step 1 — Quota raggiunta (mockup state-01) ──────────────────

export const Frame01_Step1Quota: Story = {
  name: '01 · Step 1 — Quota raggiunta, fill 100% (mockup state-01)',
  args: {
    initialStep: 1,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup stato state-01-step1-quota-raggiunta: quota 50/50, fill bar 100%, ' +
          'primary CTA "Acquista crediti" (entity-event bg), secondary "Continua domani". ' +
          'Driven via public initialStep=1.',
      },
    },
  },
};

// ── Frame 02 · Step 2 — Pack Picker (mockup state-02) ──────────────────────

export const Frame02_Step2Picker: Story = {
  name: '02 · Step 2 — Pack picker, Starter selected (mockup state-02)',
  args: {
    initialStep: 2,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup stato state-02-step2-pack-picker-starter: Step 2 pack picker, ' +
          'Starter radio selected (entity-toolkit ring + shadow), 3 packs visible. ' +
          'Driven via public initialStep=2.',
      },
    },
  },
};

// ── Frame 03 · Step 3 — Checkout form filled (mockup state-03) ─────────────

export const Frame03_Step3Form: Story = {
  name: '03 · Step 3 — Checkout form filled, Paga abilitato (mockup state-03)',
  args: {
    __initialStep: 3,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup stato state-03-step3-checkout-form-filled: test-seam __initialStep=3, ' +
          'paymentSubState=filled (default). Form placeholders visibili (VISA •••• 4242). ' +
          'CTA "Paga 5 €" abilitato. Nessun banner errore.',
      },
    },
  },
};

// ── Frame 04 · Step 3 — Payment loading (mockup state-04) ──────────────────

export const Frame04_Step3Loading: Story = {
  name: '04 · Step 3 — Payment loading, spinner (mockup state-04)',
  args: {
    __initialStep: 3,
    __initialPaymentSubState: 'loading',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup stato state-04-step3-payment-loading: test-seam __initialStep=3 + ' +
          '__initialPaymentSubState=loading. Spinner animate-spin visibile, ' +
          'CTA disabled (cursor-wait), back link disabled.',
      },
    },
  },
};

// ── Frame 05 · Step 3 — Payment failed (mockup state-05) ───────────────────

export const Frame05_Step3Failed: Story = {
  name: '05 · Step 3 — Payment failed, banner rosso (mockup state-05)',
  args: {
    __initialStep: 3,
    __initialPaymentSubState: 'failed',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup stato state-05-step3-payment-failed: test-seam __initialStep=3 + ' +
          '__initialPaymentSubState=failed. Error banner (role=alert, entity-event/10 bg) ' +
          '"Pagamento rifiutato" visibile sopra il form.',
      },
    },
  },
};

// ── Frame 06 · Step 4 — Success (mockup state-06) ──────────────────────────

export const Frame06_Step4Success: Story = {
  name: '06 · Step 4 — Crediti aggiunti, confetti CSS (mockup state-06)',
  args: {
    __initialStep: 4,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mockup stato state-06-step4-success: test-seam __initialStep=4. ' +
          'Confetti CSS-only (motion-reduce:opacity-0 guard) + 🎉 bounce. ' +
          'Recap crediti: precedenti 0 + acquistati 100 = bilancio 100. ' +
          'CTA "Torna al gioco" (entity-session bg).',
      },
    },
  },
};

// ── Frame 07 · Soft Warning — 47/50 crediti (mockup state-07) ──────────────

export const Frame07_SoftWarning: Story = {
  name: '07 · Soft Warning — 47/50 crediti, modal desktop (mockup state-07)',
  render: () => (
    <SoftWarningCredits
      used={MOCK_SOFT_WARNING_USED}
      total={MOCK_SOFT_WARNING_TOTAL}
      variant="modal-desktop"
      labels={MOCK_SOFT_WARNING_LABELS}
      onUpgrade={() => {}}
      onDismiss={() => {}}
    />
  ),
  parameters: {
    docs: {
      description: {
        story:
          'Mockup stato state-07-soft-warning-47-50: componente SoftWarningCredits ' +
          '(separato da CheckoutModal). used=47, total=50, remaining=3. ' +
          'variant=modal-desktop (shadcn Dialog + hideCloseButton + custom X). ' +
          'CTA "Acquista crediti" (entity-toolkit) + "Continua per ora" (card border).',
      },
    },
  },
};

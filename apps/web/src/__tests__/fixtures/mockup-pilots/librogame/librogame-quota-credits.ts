/**
 * Fixtures for CheckoutModal + SoftWarningCredits stories — DS-17 Phase D-2 (sub-issue #2174).
 *
 * State-driving strategy:
 *   CheckoutModal manages step + paymentSubState via `useState`. To render each
 *   mockup state STATICALLY (snapshot harness never runs `play`), we use:
 *
 *   - `initialStep: 1 | 2` (existing public prop) → drives step1 / step2 directly.
 *   - `__initialStep?: ModalStep` (new test-seam) → drives step3 / step4 directly.
 *   - `__initialPaymentSubState?: Step3SubState` (new test-seam) → drives
 *     'loading' and 'failed' sub-states of step 3 on first paint.
 *
 *   SoftWarningCredits is rendered via real props — no seam needed.
 *
 * 7 mockup states → 7 story frames:
 *
 *   Frame01_Step1Quota        — initialStep=1 (quota 50/50, fill 100%)
 *   Frame02_Step2Picker       — initialStep=2 (Starter selected default)
 *   Frame03_Step3Form         — __initialStep=3 + paymentSubState='filled'
 *   Frame04_Step3Loading      — __initialStep=3 + __initialPaymentSubState='loading'
 *   Frame05_Step3Failed       — __initialStep=3 + __initialPaymentSubState='failed'
 *   Frame06_Step4Success      — __initialStep=4 (credits added, confetti)
 *   Frame07_SoftWarning       — SoftWarningCredits variant='modal-desktop', used=47/total=50
 *
 * MSW: No API endpoints are called by CheckoutModal or SoftWarningCredits — these
 *   components are fully client-side (visual-only checkout, no real Stripe). No MSW
 *   handlers are required.
 *
 * BGG guard: no BGG refs in this file (#2123).
 *
 * @mockup admin-mockups/design_files/librogame-runthrough-quota-credits.html
 * Refs: umbrella #2063, sub-issue #2174 (Phase D-2).
 */

import type { CheckoutLabels } from '@/components/features/gamebook/CheckoutModal';
import type { SoftWarningCreditsLabels } from '@/components/features/gamebook/SoftWarningCredits';
import type { CheckoutQuota } from '@/components/features/gamebook/CheckoutModal';

// ---------------------------------------------------------------------------
// Quota — 50/50 fill (hard-stop, mockup state-01)
// ---------------------------------------------------------------------------

export const MOCK_CHECKOUT_QUOTA_FULL: CheckoutQuota = {
  used: 50,
  total: 50,
  resetDate: '1 luglio',
  previousCredits: 0,
};

// ---------------------------------------------------------------------------
// Quota — 47/50 (soft warning, mockup state-07)
// ---------------------------------------------------------------------------

export const MOCK_SOFT_WARNING_USED = 47;
export const MOCK_SOFT_WARNING_TOTAL = 50;

// ---------------------------------------------------------------------------
// User
// ---------------------------------------------------------------------------

export const MOCK_CHECKOUT_USER_EMAIL = 'aaron@meepleai.app';

// ---------------------------------------------------------------------------
// Labels — Italian locale (single-market product)
// ---------------------------------------------------------------------------

export const MOCK_CHECKOUT_LABELS: CheckoutLabels = {
  modalTitle: step => `Checkout · passo ${step} di 4`,
  close: 'Chiudi',
  stepIndicator: {
    step1: 'Quota',
    step2: 'Pacchetto',
    step3: 'Pagamento',
    step4: 'Fatto',
    ariaCurrent: step => `Passo ${step} di 4`,
  },
  step1: {
    heading: 'Quota giornaliera raggiunta',
    subheading: 'Hai tradotto 50 paragrafi oggi. Acquista crediti per continuare subito.',
    quotaLabel: 'Paragrafi oggi',
    resetIn: 'Reset tra 8 ore · domani mattina',
    primaryCta: '💎 Acquista crediti',
    secondaryCta: '⏸️ Continua domani',
    explainLink: 'Cosa sono i crediti?',
  },
  step2: {
    heading: 'Scegli il tuo pacchetto',
    subheading: 'Più paragrafi, miglior prezzo. I crediti non scadono.',
    disclaimer: 'Pagamento simulato (visual-only) — nessun addebito reale.',
    totalLabel: 'Totale',
    continueCta: 'Continua →',
    packBadges: { popular: 'Più popolare', save: 'Risparmia 30%' },
    packNames: { starter: 'Starter', mid: 'Mid', pro: 'Pro' },
    packCreditsSuffix: 'crediti',
    perParagraphSuffix: '/ paragrafo',
  },
  step3: {
    heading: 'Pagamento',
    summary: (packName, credits, eur) => `Pacchetto ${packName} · ${credits} crediti · ${eur}`,
    fieldLabels: {
      card: 'Numero carta',
      expiry: 'Scadenza',
      cvc: 'CVC',
      name: 'Nome sulla carta',
      country: 'Paese',
    },
    trustChips: ['SSL sicuro', 'Dati cifrati', '14 gg rimborso'],
    payCta: eur => `Paga ${eur}`,
    loadingCta: 'Elaborazione…',
    backLink: '← Torna ai pacchetti',
    failedBanner: {
      title: 'Pagamento rifiutato',
      detail: 'Controlla i dati della carta e riprova.',
    },
    recapLabels: { credits: 'Crediti', vat: 'IVA inclusa', total: 'Totale' },
  },
  step4: {
    title: 'Crediti aggiunti! 🎉',
    subtitle: credits => `${credits} crediti disponibili immediatamente.`,
    recapLabels: {
      previous: 'Crediti precedenti',
      purchased: 'Crediti acquistati',
      balance: 'Bilancio crediti',
      freeQuotaTitle: 'Quota free oggi',
      resetIn: 'Reset domani mattina',
      rate: '1 paragrafo = 1 credito',
    },
    backToGameCta: '🎯 Torna al gioco',
    receiptLink: email => `Ricevuta via email · ${email}`,
  },
};

// ---------------------------------------------------------------------------
// SoftWarningCredits labels
// ---------------------------------------------------------------------------

export const MOCK_SOFT_WARNING_LABELS: SoftWarningCreditsLabels = {
  title: 'Stai esaurendo i paragrafi',
  subtitle: (used, total, remaining) =>
    `Hai usato ${used} di ${total} paragrafi gratuiti oggi. Ti restano ${remaining} prima del limite giornaliero.`,
  upgradeCta: '💎 Acquista crediti',
  dismissCta: 'Continua per ora',
  close: 'Chiudi avviso',
};

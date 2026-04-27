/**
 * /join — client body (V2, Wave A.2, Issue #589).
 *
 * Owns:
 *   - i18n resolution (one `useTranslation()` call → all labels resolved upfront,
 *     children stay pure-controlled)
 *   - FSM hook (`useWaitlistSubmit`) wiring into `<JoinForm/>`
 *   - Layout composition (mobile-stack ⇄ desktop two-column at `lg`)
 *   - NODE_ENV-guarded `?state=...` visual-test escape hatch
 *
 * Mockup parity: `admin-mockups/design_files/sp3-join.jsx` (HeroText, JoinForm,
 * JoinMobileScreen, JoinDesktopScreen). Spec: §3.1 layout + §3.4 i18n binding.
 */

'use client';

import { useMemo, type JSX } from 'react';

import { useSearchParams } from 'next/navigation';

import { JoinForm, JoinHero } from '@/components/ui/v2/join';
import type { JoinFormLabels, JoinHeroFeature } from '@/components/ui/v2/join';
import { useTranslation } from '@/hooks/useTranslation';
import { useWaitlistSubmit } from '@/hooks/useWaitlistSubmit';
import type { JoinFormState } from '@/hooks/useWaitlistSubmit';
import { ALPHA_FEATURES, TOP_GAMES } from '@/lib/join/games';
import type { GamePreference } from '@/lib/join/games';

const IS_NON_PROD = process.env.NODE_ENV !== 'production';

const VALID_STATE_OVERRIDES: ReadonlySet<JoinFormState> = new Set([
  'default',
  'submitting',
  'success',
  'error',
  'already-on-list',
]);

function parseStateOverride(raw: string | null): JoinFormState | undefined {
  if (!IS_NON_PROD || !raw) return undefined;
  return VALID_STATE_OVERRIDES.has(raw as JoinFormState) ? (raw as JoinFormState) : undefined;
}

export function JoinPageClient(): JSX.Element {
  const { t, formatMessage } = useTranslation();
  const waitlist = useWaitlistSubmit();

  const searchParams = useSearchParams();
  const stateOverride = parseStateOverride(searchParams?.get('state') ?? null);

  // Resolve ICU plurals using the *real* result if present, otherwise fall back
  // to deterministic placeholders so the override branches still render copy.
  const position = waitlist.result?.position ?? 1247;
  const estimatedWeeks = waitlist.result?.estimatedWeeks ?? 2;

  const games = useMemo<readonly GamePreference[]>(
    () =>
      TOP_GAMES.map(g => ({
        id: g.id,
        label: t(g.labelKey),
        labelKey: g.labelKey,
        emoji: g.emoji,
      })),
    [t]
  );

  const features = useMemo<readonly JoinHeroFeature[]>(
    () =>
      ALPHA_FEATURES.map(f => ({
        entity: f.entity,
        emoji: f.emoji,
        title: t(f.titleKey),
        description: t(f.descKey),
      })),
    [t]
  );

  const labels = useMemo<JoinFormLabels>(
    () => ({
      emailLabel: t('pages.join.form.emailLabel'),
      emailPlaceholder: t('pages.join.form.emailPlaceholder'),
      emailErrorInvalid: t('pages.join.form.emailErrorInvalid'),
      nameLabel: t('pages.join.form.nameLabel'),
      nameOptional: t('pages.join.form.nameOptional'),
      namePlaceholder: t('pages.join.form.namePlaceholder'),
      nameHint: t('pages.join.form.nameHint'),
      gameErrorRequired: t('pages.join.form.gameErrorRequired'),
      gameErrorOtherTooShort: t('pages.join.form.gameErrorOtherTooShort'),
      newsletterLabel: t('pages.join.form.newsletterLabel'),
      submitDefault: t('pages.join.form.submitDefault'),
      submitting: t('pages.join.form.submitting'),
      alreadyHaveInvite: t('pages.join.form.alreadyHaveInvite'),
      loginLink: t('pages.join.form.loginLink'),
      bannerAlreadyOnList: formatMessage(
        { id: 'pages.join.banners.alreadyOnList' },
        { position, weeks: estimatedWeeks }
      ),
      bannerErrorGeneric: t('pages.join.banners.errorGeneric'),
      bannerErrorEmailField: t('pages.join.banners.errorEmailField'),
      bannerAlreadyEmailField: t('pages.join.banners.alreadyEmailField'),
      select: {
        placeholder: t('pages.join.form.gamePlaceholder'),
        otherPlaceholder: t('pages.join.form.gameOtherPlaceholder'),
        listboxAriaLabel: t('pages.join.select.listboxAriaLabel'),
        otherInputAriaLabel: t('pages.join.select.otherInputAriaLabel'),
        fieldLabel: t('pages.join.form.gameLabel'),
      },
      success: {
        heading: t('pages.join.success.heading'),
        subText: t('pages.join.success.subText'),
        subTextSecond: t('pages.join.success.subTextSecond'),
        positionLabel: t('pages.join.success.positionLabel'),
        positionEstimate: formatMessage(
          { id: 'pages.join.success.positionEstimate' },
          { weeks: estimatedWeeks }
        ),
        resetCta: t('pages.join.success.resetCta'),
      },
    }),
    [t, formatMessage, position, estimatedWeeks]
  );

  return (
    <main
      data-testid="join-page"
      className="min-h-screen bg-background"
      style={{
        backgroundImage:
          'radial-gradient(120% 80% at 80% 0%, hsl(var(--c-event) / .07), transparent 60%), radial-gradient(80% 70% at 0% 100%, hsl(var(--c-game) / .06), transparent 70%)',
      }}
    >
      <div className="mx-auto max-w-[1280px] px-4 py-10 sm:px-8 sm:py-14 lg:py-16">
        <div className="grid gap-10 lg:grid-cols-[1.1fr_0.9fr] lg:gap-12">
          {/* Left: hero + features (centered on mobile, left-aligned on desktop) */}
          <div className="lg:pt-3">
            {/* Two hero variants share the slot via CSS responsive utilities.
                Tailwind `hidden`/`lg:hidden` resolve to `display:none`, which
                modern browsers exclude from the accessibility tree — only ONE
                <h1> is exposed to AT at any breakpoint. */}
            <div className="lg:hidden">
              <JoinHero
                compact={false}
                alphaPillLabel={t('pages.join.hero.alphaPill')}
                heading={t('pages.join.hero.tagline')}
                headingHighlight=""
                subTagline={t('pages.join.hero.subTagline')}
                features={features}
              />
            </div>
            <div className="hidden lg:block">
              <JoinHero
                compact
                alphaPillLabel={t('pages.join.hero.alphaPill')}
                heading={t('pages.join.hero.tagline')}
                headingHighlight=""
                subTagline={t('pages.join.hero.subTagline')}
                features={features}
              />
            </div>
          </div>

          {/* Right: form card (sticky on desktop) */}
          <div className="lg:sticky lg:top-20 lg:self-start">
            <div
              data-testid="join-form-card"
              className="rounded-2xl border border-border bg-card px-4 pt-5 pb-4 shadow-lg sm:px-5 sm:pt-6 sm:pb-5"
            >
              <h2 className="m-0 mb-1 font-display text-[18px] font-bold tracking-[-0.01em] text-foreground">
                {t('pages.join.hero.alphaPill')}
              </h2>
              <p className="m-0 mb-4 text-[12px] leading-[1.55] text-[hsl(var(--text-muted))]">
                {t('pages.join.hero.subTagline')}
              </p>
              <JoinForm
                games={games}
                waitlist={waitlist}
                labels={labels}
                loginHref="/login"
                stateOverride={stateOverride}
              />
            </div>
          </div>
        </div>
      </div>
    </main>
  );
}

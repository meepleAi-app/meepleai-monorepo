'use client';

/**
 * StatusBanner — global incident / maintenance banner (Issue #1089).
 *
 * Renders a full-width bar at the top of the user shell when the backend
 * publishes an active status banner via `/api/v1/status-banner`. Behaviour:
 *
 *  - 204 No Content → render nothing
 *  - Info / Warning → dismissible (per-user via LocalStorage, keyed by
 *    deterministic `messageId` — same content stays dismissed across reloads)
 *  - Critical → not dismissible (highest urgency, e.g. data integrity issues)
 *
 * Accessibility:
 *  - Info / Warning use `role="status"` + `aria-live="polite"`
 *  - Critical uses `role="alert"` + `aria-live="assertive"`
 *  - Dismiss button has localized `aria-label`
 */

import { useCallback } from 'react';

import { X, AlertTriangle, AlertOctagon, Info } from 'lucide-react';

import { useStatusBanner } from '@/hooks/queries/useStatusBanner';
import { useTranslation } from '@/hooks/useTranslation';
import type {
  PublicStatusBannerResponse,
  StatusBannerSeverity,
} from '@/lib/api/schemas/status-banner.schemas';
import { useDismissedBannersStore } from '@/lib/stores/dismissed-banners-store';

interface SeverityStyle {
  container: string;
  icon: typeof Info;
  iconClass: string;
  buttonClass: string;
}

const SEVERITY_STYLES: Record<StatusBannerSeverity, SeverityStyle> = {
  Info: {
    container:
      'bg-blue-50 text-blue-950 border-blue-200 dark:bg-blue-950 dark:text-blue-50 dark:border-blue-800',
    icon: Info,
    iconClass: 'text-blue-700 dark:text-blue-200',
    buttonClass:
      'text-blue-900 hover:bg-blue-100 focus-visible:ring-blue-500 dark:text-blue-100 dark:hover:bg-blue-900',
  },
  Warning: {
    container:
      'bg-amber-50 text-amber-950 border-amber-300 dark:bg-amber-950 dark:text-amber-50 dark:border-amber-800',
    icon: AlertTriangle,
    iconClass: 'text-amber-700 dark:text-amber-200',
    buttonClass:
      'text-amber-900 hover:bg-amber-100 focus-visible:ring-amber-500 dark:text-amber-100 dark:hover:bg-amber-900',
  },
  Critical: {
    container:
      'bg-red-600 text-red-50 border-red-700 dark:bg-red-700 dark:text-red-50 dark:border-red-900',
    icon: AlertOctagon,
    iconClass: 'text-red-50',
    buttonClass: '',
  },
};

interface StatusBannerViewProps {
  banner: PublicStatusBannerResponse;
  onDismiss: () => void;
}

/**
 * Pure presentational view — receives a banner + dismiss callback. Extracted
 * so unit tests can render it without the React Query / store wiring.
 */
export function StatusBannerView({ banner, onDismiss }: StatusBannerViewProps) {
  const { t } = useTranslation();
  const style = SEVERITY_STYLES[banner.severity];
  const Icon = style.icon;

  const isCritical = banner.severity === 'Critical';
  const role = isCritical ? 'alert' : 'status';
  const ariaLive = isCritical ? 'assertive' : 'polite';

  const severityKey = banner.severity.toLowerCase() as 'info' | 'warning' | 'critical';
  const severityLabel = t(`common.statusBanner.severityLabel.${severityKey}`);
  const dismissLabel = `${t('common.statusBanner.dismiss')}: ${severityLabel}`;

  return (
    <div
      role={role}
      aria-live={ariaLive}
      data-slot="status-banner"
      data-severity={banner.severity}
      data-message-id={banner.messageId}
      className={`w-full border-b px-4 py-3 sm:px-6 ${style.container}`}
    >
      <div className="mx-auto flex max-w-7xl items-start gap-3">
        <Icon aria-hidden="true" className={`mt-0.5 h-5 w-5 shrink-0 ${style.iconClass}`} />
        <p className="flex-1 text-sm font-medium leading-relaxed sm:text-base">
          <span className="sr-only">{severityLabel}: </span>
          {banner.message}
        </p>
        {!isCritical && (
          <button
            type="button"
            onClick={onDismiss}
            aria-label={dismissLabel}
            data-slot="status-banner-dismiss"
            className={`ml-2 inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-md transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 ${style.buttonClass}`}
          >
            <X aria-hidden="true" className="h-4 w-4" />
          </button>
        )}
      </div>
    </div>
  );
}

/**
 * StatusBanner — connected component. Wires the public banner query + the
 * dismissal store. Renders nothing when there is no active banner or the
 * current banner has already been dismissed by this user.
 */
export function StatusBanner() {
  const { data } = useStatusBanner();
  // Subscribe to the list itself so component re-renders on dismiss.
  // (Selecting the function reference alone would not trigger re-render.)
  const dismissedIds = useDismissedBannersStore(state => state.dismissedIds);
  const dismiss = useDismissedBannersStore(state => state.dismiss);

  const banner = data ?? null;

  const handleDismiss = useCallback(() => {
    if (banner) dismiss(banner.messageId);
  }, [banner, dismiss]);

  if (!banner) return null;
  if (dismissedIds.includes(banner.messageId)) return null;

  return <StatusBannerView banner={banner} onDismiss={handleDismiss} />;
}

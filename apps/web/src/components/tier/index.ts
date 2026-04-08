/**
 * Tier Components
 *
 * PricingCard is awaiting /pricing page integration. The progress bar and
 * upgrade CTA components that previously lived here (UsageMeter, UpgradeCta,
 * UsageSummary) were deleted in the orphan components consolidation (#291) —
 * their production replacements are:
 *   - Progress bars: `QuotaRow` inline in `library/UsageWidget.tsx`
 *   - Upgrade CTA: `UpgradePrompt` in `ui/feedback/upgrade-prompt.tsx`
 *     (consumed by FeatureGate, PermissionGate, TierGate)
 */

export { PricingCard, type PricingCardProps, type PricingFeature } from './PricingCard';

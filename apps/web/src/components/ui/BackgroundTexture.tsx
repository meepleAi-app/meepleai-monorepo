/**
 * BackgroundTexture Component
 *
 * Provides subtle wood/paper texture overlay for MeepleAI warm board game aesthetic.
 * Issue #2847: Background Texture System
 * Epic #2845: MeepleAI Design System Integration
 *
 * Features:
 * - Dual-layer texture: repeating gradients + radial warmth overlay
 * - Fixed positioning behind all content (z-index: 0)
 * - Pointer events disabled (no UI interference)
 * - Performance optimized (CSS-only, GPU accelerated)
 * - Responsive with subtle opacity adjustment
 *
 * Design Reference: docs/design-proposals/meepleai-style/admin-dashboard-v2.html (lines 24-44)
 */

export interface BackgroundTextureProps {
  /**
   * Opacity level for texture visibility
   * @default 0.6
   */
  opacity?: number;

  /**
   * Enable/disable texture overlay
   * @default true
   */
  enabled?: boolean;

  /**
   * Texture intensity (affects gradient opacity multipliers)
   * @default 'normal'
   */
  intensity?: 'subtle' | 'normal' | 'strong';
}

const INTENSITY_MULTIPLIERS = {
  subtle: 0.5,
  normal: 1.0,
  strong: 1.5,
};

export function BackgroundTexture({
  opacity = 0.6,
  enabled = true,
  intensity = 'normal',
}: BackgroundTextureProps) {
  if (!enabled) {
    return null;
  }

  const multiplier = INTENSITY_MULTIPLIERS[intensity];

  // Calculate adjusted opacity values for texture layers
  const textureOpacity1 = 0.015 * multiplier;
  const textureOpacity2 = 0.02 * multiplier;
  const gradientOpacity = 0.03 * multiplier;

  return (
    <>
      {/* Layer 1: Wood/Paper Texture Pattern */}
      <div
        className="fixed inset-0 pointer-events-none"
        style={{
          zIndex: 0,
          opacity,
          backgroundImage: `
            repeating-linear-gradient(
              0deg,
              transparent,
              transparent 2px,
              rgba(210, 105, 30, ${textureOpacity1}) 2px,
              rgba(210, 105, 30, ${textureOpacity1}) 4px
            ),
            repeating-linear-gradient(
              90deg,
              transparent,
              transparent 2px,
              rgba(139, 90, 60, ${textureOpacity2}) 2px,
              rgba(139, 90, 60, ${textureOpacity2}) 4px
            )
          `,
        }}
        aria-hidden="true"
      />

      {/* Layer 2: Warm Radial Gradient Overlay */}
      <div
        className="fixed inset-0 pointer-events-none"
        style={{
          zIndex: 0,
          background: `radial-gradient(
            ellipse at 50% 0%,
            rgba(210, 105, 30, ${gradientOpacity}),
            transparent 60%
          )`,
        }}
        aria-hidden="true"
      />
    </>
  );
}

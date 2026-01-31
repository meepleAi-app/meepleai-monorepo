/**
 * MeepleAI Animations & Transitions - Storybook Documentation
 *
 * Visual reference for animation system: pulse effects, hover states, smooth transitions.
 * Issue #2848: Animation & Transition Library
 * Epic #2845: MeepleAI Design System Integration
 */

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta = {
  title: 'Design System/Animations',
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component:
          'MeepleAI animation system with pulse effects, hover states, and accessibility support (prefers-reduced-motion).',
      },
    },
  },
};

export default meta;
type Story = StoryObj;

/**
 * Pulse Animation for Status Indicators
 * 2s infinite pulse for status dots, health indicators
 */
export const PulseAnimation: Story = {
  render: () => (
    <div className="p-8 space-y-8 bg-meeple-warm-bg">
      <div>
        <h2 className="text-2xl font-bold font-heading text-meeple-dark mb-4">
          Pulse Animation (Status Indicators)
        </h2>
        <p className="text-gray-600 font-body mb-8">
          2s infinite pulse for status dots, service health, and live indicators.
        </p>
      </div>

      <div className="flex items-center gap-8 flex-wrap">
        {/* Healthy Status */}
        <div className="flex items-center gap-3">
          <div className="w-4 h-4 rounded-full bg-green-500 animate-pulse-meeple" />
          <span className="text-sm font-semibold text-green-600">Operativo</span>
        </div>

        {/* Degraded Status */}
        <div className="flex items-center gap-3">
          <div className="w-4 h-4 rounded-full bg-yellow-500 animate-pulse-meeple" />
          <span className="text-sm font-semibold text-yellow-600">Rallentato</span>
        </div>

        {/* Down Status */}
        <div className="flex items-center gap-3">
          <div className="w-4 h-4 rounded-full bg-red-500 animate-pulse-meeple" />
          <span className="text-sm font-semibold text-red-600">Offline</span>
        </div>

        {/* Large Status Indicator */}
        <div className="flex items-center gap-3 bg-white rounded-full px-6 py-3 border border-meeple-border">
          <div className="w-3 h-3 rounded-full bg-green-500 animate-pulse-meeple" />
          <span className="text-sm font-bold font-heading">Sistema Attivo</span>
        </div>
      </div>

      <div className="mt-8 p-4 bg-blue-50 border border-blue-200 rounded-lg">
        <p className="text-sm text-blue-800 font-body">
          <strong>Usage:</strong> <code className="bg-blue-100 px-2 py-1 rounded">className="animate-pulse-meeple"</code>
        </p>
      </div>
    </div>
  ),
};

/**
 * Card Hover Effects
 * Smooth lift + shadow transition (0.3s duration)
 */
export const CardHoverEffects: Story = {
  render: () => (
    <div className="p-8 bg-meeple-warm-bg">
      <h2 className="text-2xl font-bold font-heading text-meeple-dark mb-4">
        Card Hover Effects
      </h2>
      <p className="text-gray-600 font-body mb-8">
        Cards lift slightly on hover with shadow enhancement (300ms smooth transition).
      </p>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        {/* Standard Hover Card */}
        <div className="hover-card bg-white border border-meeple-border rounded-2xl p-6 cursor-pointer">
          <div className="text-sm text-gray-600 font-semibold mb-2">Total Users</div>
          <div className="text-4xl font-bold font-heading text-meeple-dark">2,847</div>
          <div className="text-sm text-green-600 font-bold mt-2">↗ +12%</div>
          <p className="text-xs text-gray-500 mt-4 font-body">
            Hover to see lift + shadow
          </p>
        </div>

        {/* Service Card with Border Accent */}
        <div className="hover-card bg-white border-l-4 border-l-green-500 border border-meeple-border rounded-xl p-6 cursor-pointer">
          <div className="flex items-center gap-2 mb-3">
            <div className="w-3 h-3 rounded-full bg-green-500 animate-pulse-meeple" />
            <span className="text-sm font-bold text-green-600">Operativo</span>
          </div>
          <div className="text-xl font-bold font-heading text-meeple-dark">PostgreSQL</div>
          <div className="text-sm text-green-600 font-semibold">5ms response time</div>
        </div>

        {/* Action Card */}
        <div className="hover-card bg-gradient-to-br from-meeple-orange to-orange-600 text-white rounded-2xl p-6 cursor-pointer">
          <div className="text-3xl mb-2">📊</div>
          <div className="text-lg font-bold font-heading">View Analytics</div>
          <p className="text-sm opacity-90 mt-2 font-body">
            Hover for interaction feedback
          </p>
        </div>
      </div>

      <div className="mt-8 p-4 bg-blue-50 border border-blue-200 rounded-lg">
        <p className="text-sm text-blue-800 font-body">
          <strong>Usage:</strong> <code className="bg-blue-100 px-2 py-1 rounded">className="hover-card"</code>
          {' '}provides lift + shadow on hover
        </p>
      </div>
    </div>
  ),
};

/**
 * Button Hover Effects
 * Quick lift animation (200ms) for buttons and CTAs
 */
export const ButtonHoverEffects: Story = {
  render: () => (
    <div className="p-8 bg-meeple-warm-bg">
      <h2 className="text-2xl font-bold font-heading text-meeple-dark mb-4">
        Button Hover Effects
      </h2>
      <p className="text-gray-600 font-body mb-8">
        Buttons lift slightly on hover with shadow (200ms quick transition).
      </p>

      <div className="flex flex-wrap gap-4">
        <button className="hover-button bg-meeple-orange text-white px-7 py-3 rounded-xl font-bold font-heading shadow-md">
          Primary Action
        </button>

        <button className="hover-button bg-green-500 text-white px-7 py-3 rounded-xl font-bold font-heading shadow-md">
          Success Action
        </button>

        <button className="hover-button bg-meeple-purple text-white px-7 py-3 rounded-xl font-bold font-heading shadow-md">
          Accent Action
        </button>

        <button className="hover-button bg-white text-meeple-dark border-2 border-meeple-border px-7 py-3 rounded-xl font-bold font-heading">
          Secondary
        </button>

        <button className="hover-button bg-red-500 text-white px-7 py-3 rounded-xl font-bold font-heading shadow-md">
          Danger Action
        </button>
      </div>

      <div className="mt-8 p-4 bg-blue-50 border border-blue-200 rounded-lg">
        <p className="text-sm text-blue-800 font-body">
          <strong>Usage:</strong> <code className="bg-blue-100 px-2 py-1 rounded">className="hover-button"</code>
          {' '}provides quick lift + shadow
        </p>
      </div>
    </div>
  ),
};

/**
 * Hover Lift Utilities
 * Small (-1) and Medium (-2) lift variants
 */
export const HoverLiftUtilities: Story = {
  render: () => (
    <div className="p-8 bg-meeple-warm-bg">
      <h2 className="text-2xl font-bold font-heading text-meeple-dark mb-4">
        Hover Lift Utilities
      </h2>
      <p className="text-gray-600 font-body mb-8">
        Granular control over hover lift distance: small (4px) or medium (8px).
      </p>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <div className="hover-lift-sm bg-white border border-meeple-border rounded-xl p-6 cursor-pointer">
          <div className="text-sm text-gray-600 font-semibold mb-2">Lift Small</div>
          <div className="text-3xl font-bold font-heading text-meeple-dark">-4px</div>
          <p className="text-sm text-gray-500 mt-2 font-body">
            Subtle lift for list items, tags
          </p>
        </div>

        <div className="hover-lift-md bg-white border border-meeple-border rounded-xl p-6 cursor-pointer">
          <div className="text-sm text-gray-600 font-semibold mb-2">Lift Medium</div>
          <div className="text-3xl font-bold font-heading text-meeple-dark">-8px</div>
          <p className="text-sm text-gray-500 mt-2 font-body">
            Pronounced lift for cards, features
          </p>
        </div>
      </div>

      <div className="mt-8 p-4 bg-blue-50 border border-blue-200 rounded-lg">
        <p className="text-sm text-blue-800 font-body">
          <strong>Usage:</strong> <code className="bg-blue-100 px-2 py-1 rounded">hover-lift-sm</code>
          {' '}or{' '}
          <code className="bg-blue-100 px-2 py-1 rounded">hover-lift-md</code>
        </p>
      </div>
    </div>
  ),
};

/**
 * Shadow Hover Effect
 * MeepleAI-specific shadow with warm brown tones
 */
export const ShadowHoverEffect: Story = {
  render: () => (
    <div className="p-8 bg-meeple-warm-bg">
      <h2 className="text-2xl font-bold font-heading text-meeple-dark mb-4">
        MeepleAI Shadow Hover
      </h2>
      <p className="text-gray-600 font-body mb-8">
        Warm brown shadow (rgba(139, 90, 60, 0.12)) on hover for brand consistency.
      </p>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        {Array.from({ length: 4 }, (_, i) => (
          <div
            key={i}
            className="hover-shadow-meeple bg-white border border-meeple-border rounded-xl p-4 cursor-pointer"
          >
            <div className="text-2xl font-bold font-heading text-meeple-dark">
              {(i + 1) * 250}
            </div>
            <p className="text-xs text-gray-500 mt-1 font-body">Metric {i + 1}</p>
          </div>
        ))}
      </div>

      <div className="mt-8 p-4 bg-blue-50 border border-blue-200 rounded-lg">
        <p className="text-sm text-blue-800 font-body">
          <strong>Usage:</strong> <code className="bg-blue-100 px-2 py-1 rounded">className="hover-shadow-meeple"</code>
        </p>
      </div>
    </div>
  ),
};

/**
 * Combined Animations Demo
 * Shows pulse + hover effects working together
 */
export const CombinedEffects: Story = {
  render: () => (
    <div className="p-8 bg-meeple-warm-bg">
      <h2 className="text-2xl font-bold font-heading text-meeple-dark mb-4">
        Combined Animations
      </h2>
      <p className="text-gray-600 font-body mb-8">
        Pulse animations combined with hover effects for rich interactions.
      </p>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Service Card with Pulse + Hover */}
        <div className="hover-card hover-shadow-meeple bg-white border-l-4 border-l-green-500 border border-meeple-border rounded-xl p-6 cursor-pointer">
          <div className="flex justify-between items-center mb-4">
            <div className="flex items-center gap-2">
              <div className="w-3 h-3 rounded-full bg-green-500 animate-pulse-meeple" />
              <span className="text-sm font-bold text-green-600">Healthy</span>
            </div>
            <span className="text-xs text-gray-500">10s ago</span>
          </div>
          <div className="text-xl font-bold font-heading text-meeple-dark mb-1">
            Database Service
          </div>
          <div className="text-sm text-green-600 font-semibold">5ms response time</div>
        </div>

        {/* Warning Card with Pulse + Hover */}
        <div className="hover-card hover-shadow-meeple bg-yellow-50 border-l-4 border-l-yellow-500 border border-yellow-200 rounded-xl p-6 cursor-pointer">
          <div className="flex justify-between items-center mb-4">
            <div className="flex items-center gap-2">
              <div className="w-3 h-3 rounded-full bg-yellow-500 animate-pulse-meeple" />
              <span className="text-sm font-bold text-yellow-600">Warning</span>
            </div>
            <span className="text-xs text-gray-500">15s ago</span>
          </div>
          <div className="text-xl font-bold font-heading text-meeple-dark mb-1">
            API Service
          </div>
          <div className="text-sm text-yellow-600 font-semibold">850ms response time</div>
        </div>
      </div>

      <div className="mt-8 p-4 bg-blue-50 border border-blue-200 rounded-lg">
        <p className="text-sm text-blue-800 font-body">
          <strong>Tip:</strong> Combine <code className="bg-blue-100 px-1.5 py-0.5 rounded">animate-pulse-meeple</code>
          {' '}with{' '}
          <code className="bg-blue-100 px-1.5 py-0.5 rounded">hover-card</code> for dynamic status displays.
        </p>
      </div>
    </div>
  ),
};

/**
 * Accessibility - Prefers Reduced Motion
 * Demonstrates motion reduction for users with vestibular disorders
 */
export const AccessibilityReducedMotion: Story = {
  render: () => (
    <div className="p-8 bg-meeple-warm-bg space-y-8">
      <div>
        <h2 className="text-2xl font-bold font-heading text-meeple-dark mb-4">
          Accessibility: Reduced Motion
        </h2>
        <p className="text-gray-600 font-body mb-4">
          When <code className="bg-gray-100 px-2 py-1 rounded">prefers-reduced-motion</code> is enabled,
          all animations are disabled for accessibility.
        </p>
        <p className="text-sm text-gray-500 font-body">
          Test: Enable "Reduce motion" in your OS accessibility settings and reload.
        </p>
      </div>

      <div className="bg-white rounded-2xl border border-meeple-border p-6">
        <h3 className="font-semibold font-heading mb-4">Animation Behavior</h3>
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-gray-200">
              <th className="text-left py-2 font-heading">Animation</th>
              <th className="text-left py-2 font-heading">Normal</th>
              <th className="text-left py-2 font-heading">Reduced Motion</th>
            </tr>
          </thead>
          <tbody className="font-body">
            <tr className="border-b border-gray-100">
              <td className="py-2">Pulse (status dots)</td>
              <td className="py-2 text-green-600">2s infinite</td>
              <td className="py-2 text-gray-500">Disabled</td>
            </tr>
            <tr className="border-b border-gray-100">
              <td className="py-2">Hover lift</td>
              <td className="py-2 text-green-600">300ms transform</td>
              <td className="py-2 text-gray-500">No transform</td>
            </tr>
            <tr className="border-b border-gray-100">
              <td className="py-2">Fade in</td>
              <td className="py-2 text-green-600">500ms opacity</td>
              <td className="py-2 text-gray-500">Instant (0.01ms)</td>
            </tr>
            <tr>
              <td className="py-2">Transitions</td>
              <td className="py-2 text-green-600">200-300ms</td>
              <td className="py-2 text-gray-500">Instant (0.01ms)</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div className="p-4 bg-green-50 border border-green-200 rounded-lg">
        <p className="text-sm text-green-800 font-body">
          <strong>WCAG 2.1 Success Criterion 2.3.3:</strong> All MeepleAI animations respect
          user motion preferences for accessibility compliance.
        </p>
      </div>
    </div>
  ),
};

/**
 * Animation Performance Guide
 * Shows 60fps optimizations and best practices
 */
export const PerformanceGuide: Story = {
  render: () => (
    <div className="p-8 bg-meeple-warm-bg">
      <h2 className="text-2xl font-bold font-heading text-meeple-dark mb-4">
        Animation Performance
      </h2>
      <p className="text-gray-600 font-body mb-8">
        All MeepleAI animations are GPU-accelerated for 60fps smooth performance.
      </p>

      <div className="bg-white rounded-2xl border border-meeple-border p-6 space-y-6">
        <div>
          <h3 className="font-semibold font-heading mb-3">GPU-Accelerated Properties</h3>
          <div className="grid grid-cols-2 gap-4 text-sm font-body">
            <div className="flex items-center gap-2">
              <span className="text-green-500">✓</span>
              <code className="bg-gray-100 px-2 py-1 rounded">transform</code>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-green-500">✓</span>
              <code className="bg-gray-100 px-2 py-1 rounded">opacity</code>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-green-500">✓</span>
              <code className="bg-gray-100 px-2 py-1 rounded">filter</code>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-green-500">✓</span>
              <code className="bg-gray-100 px-2 py-1 rounded">box-shadow</code>
            </div>
          </div>
        </div>

        <div className="border-t border-gray-200 pt-4">
          <h3 className="font-semibold font-heading mb-3">Timing Functions</h3>
          <ul className="space-y-2 text-sm font-body text-gray-600">
            <li><code className="bg-gray-100 px-2 py-1 rounded">ease-in-out</code> - Smooth start and end</li>
            <li><code className="bg-gray-100 px-2 py-1 rounded">ease-out</code> - Quick start, slow end (hover effects)</li>
            <li><code className="bg-gray-100 px-2 py-1 rounded">cubic-bezier(0.4, 0, 0.6, 1)</code> - Custom easing (pulse)</li>
          </ul>
        </div>

        <div className="border-t border-gray-200 pt-4">
          <h3 className="font-semibold font-heading mb-3">Performance Targets</h3>
          <ul className="space-y-2 text-sm font-body text-gray-600">
            <li><strong>60fps:</strong> All animations run at 60fps (16.67ms per frame)</li>
            <li><strong>No jank:</strong> GPU acceleration prevents frame drops</li>
            <li><strong>Smooth:</strong> 200-300ms transitions feel natural</li>
            <li><strong>Accessible:</strong> Disabled for prefers-reduced-motion users</li>
          </ul>
        </div>
      </div>
    </div>
  ),
};

/**
 * Complete Animation Reference
 * All available animations with usage examples
 */
export const CompleteReference: Story = {
  render: () => (
    <div className="p-8 bg-meeple-warm-bg">
      <h2 className="text-3xl font-bold font-heading text-meeple-dark mb-2">
        MeepleAI Animation Library
      </h2>
      <p className="text-gray-600 font-body mb-8">
        Complete reference of all available animations and transitions.
      </p>

      <div className="space-y-8">
        {/* Pulse Animations */}
        <div className="bg-white rounded-2xl border border-meeple-border p-6">
          <h3 className="text-xl font-bold font-heading mb-4">Pulse Animations</h3>
          <div className="grid grid-cols-2 gap-4 text-sm font-body">
            <div>
              <code className="bg-gray-100 px-2 py-1 rounded">animate-pulse-meeple</code>
              <p className="text-xs text-gray-500 mt-1">2s infinite (status dots)</p>
            </div>
            <div>
              <code className="bg-gray-100 px-2 py-1 rounded">animate-pulse-slow</code>
              <p className="text-xs text-gray-500 mt-1">3s infinite (subtle pulse)</p>
            </div>
          </div>
        </div>

        {/* Hover Utilities */}
        <div className="bg-white rounded-2xl border border-meeple-border p-6">
          <h3 className="text-xl font-bold font-heading mb-4">Hover Effects</h3>
          <div className="grid grid-cols-2 gap-4 text-sm font-body">
            <div>
              <code className="bg-gray-100 px-2 py-1 rounded">hover-card</code>
              <p className="text-xs text-gray-500 mt-1">Lift + shadow (300ms)</p>
            </div>
            <div>
              <code className="bg-gray-100 px-2 py-1 rounded">hover-button</code>
              <p className="text-xs text-gray-500 mt-1">Quick lift + shadow (200ms)</p>
            </div>
            <div>
              <code className="bg-gray-100 px-2 py-1 rounded">hover-lift-sm</code>
              <p className="text-xs text-gray-500 mt-1">Small lift -4px</p>
            </div>
            <div>
              <code className="bg-gray-100 px-2 py-1 rounded">hover-lift-md</code>
              <p className="text-xs text-gray-500 mt-1">Medium lift -8px</p>
            </div>
            <div>
              <code className="bg-gray-100 px-2 py-1 rounded">hover-shadow-meeple</code>
              <p className="text-xs text-gray-500 mt-1">Warm brown shadow</p>
            </div>
          </div>
        </div>

        {/* Entry Animations */}
        <div className="bg-white rounded-2xl border border-meeple-border p-6">
          <h3 className="text-xl font-bold font-heading mb-4">Entry Animations</h3>
          <div className="grid grid-cols-2 gap-4 text-sm font-body">
            <div>
              <code className="bg-gray-100 px-2 py-1 rounded">animate-fade-in</code>
              <p className="text-xs text-gray-500 mt-1">Opacity 0 → 1 (500ms)</p>
            </div>
            <div>
              <code className="bg-gray-100 px-2 py-1 rounded">animate-slide-up</code>
              <p className="text-xs text-gray-500 mt-1">Slide from bottom (600ms)</p>
            </div>
            <div>
              <code className="bg-gray-100 px-2 py-1 rounded">animate-scale-in</code>
              <p className="text-xs text-gray-500 mt-1">Scale 0.95 → 1 (400ms)</p>
            </div>
            <div>
              <code className="bg-gray-100 px-2 py-1 rounded">animate-bounce-subtle</code>
              <p className="text-xs text-gray-500 mt-1">Gentle bounce (350ms)</p>
            </div>
          </div>
        </div>
      </div>

      <div className="mt-8 p-4 bg-green-50 border border-green-200 rounded-lg">
        <p className="text-sm text-green-800 font-body">
          <strong>All animations:</strong> GPU-accelerated, 60fps target, accessibility compliant.
        </p>
      </div>
    </div>
  ),
};

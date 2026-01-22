/**
 * MeepleAI Design Tokens - Storybook Documentation
 *
 * Visual reference for MeepleAI brand colors, typography, and design tokens.
 * Issue #2846: MeepleAI Theme Configuration
 */

import type { Meta, StoryObj } from '@storybook/react';

const ColorSwatch = ({ color, name, hex, hsl }: { color: string; name: string; hex: string; hsl: string }) => (
  <div className="flex items-center gap-4 p-4 border border-gray-200 rounded-lg">
    <div
      className="w-20 h-20 rounded-lg shadow-md border border-gray-300"
      style={{ backgroundColor: color }}
    />
    <div className="flex-1">
      <div className="font-bold text-lg">{name}</div>
      <div className="text-sm text-gray-600 font-mono">{hex}</div>
      <div className="text-xs text-gray-500 font-mono">HSL: {hsl}</div>
    </div>
  </div>
);

const FontSample = ({ family, weights, sample }: { family: string; weights: string; sample: string }) => (
  <div className="p-6 border border-gray-200 rounded-lg bg-white">
    <div className="text-sm text-gray-600 mb-2">
      {family} ({weights})
    </div>
    <div className="text-4xl mb-4" style={{ fontFamily: family }}>
      {sample}
    </div>
    <div className="text-sm" style={{ fontFamily: family }}>
      The quick brown fox jumps over the lazy dog
    </div>
    <div className="text-xs text-gray-500 mt-2" style={{ fontFamily: family }}>
      ABCDEFGHIJKLMNOPQRSTUVWXYZ 0123456789
    </div>
  </div>
);

const meta: Meta = {
  title: 'Design System/MeepleAI Tokens',
  parameters: {
    layout: 'padded',
    docs: {
      description: {
        component: 'MeepleAI brand design tokens: colors, typography, and visual identity.',
      },
    },
  },
};

export default meta;
type Story = StoryObj;

/**
 * MeepleAI Brand Color Palette
 * Warm, board-game-themed colors with accessibility compliance
 */
export const ColorPalette: Story = {
  render: () => (
    <div className="space-y-6 p-8 bg-gray-50">
      <div>
        <h2 className="text-3xl font-bold mb-6 font-quicksand">MeepleAI Brand Colors</h2>
        <p className="text-gray-600 mb-8">
          Warm, inviting palette designed for board game enthusiasts. All colors are WCAG 2.1 AA compliant.
        </p>
      </div>

      <div className="space-y-4">
        <h3 className="text-xl font-semibold mb-4 font-quicksand">Primary Colors</h3>
        <ColorSwatch
          color="#d2691e"
          name="Meeple Orange"
          hex="#d2691e"
          hsl="25 85% 45%"
        />
        <ColorSwatch
          color="#8b5cf6"
          name="Meeple Purple"
          hex="#8b5cf6"
          hsl="262 83% 62%"
        />
      </div>

      <div className="space-y-4 mt-8">
        <h3 className="text-xl font-semibold mb-4 font-quicksand">Surface & Text Colors</h3>
        <ColorSwatch
          color="#f8f6f0"
          name="Warm Background"
          hex="#f8f6f0"
          hsl="30 25% 97%"
        />
        <ColorSwatch
          color="#2d2d2d"
          name="Dark Text"
          hex="#2d2d2d"
          hsl="0 0% 18%"
        />
        <ColorSwatch
          color="#e8e4d8"
          name="Border"
          hex="#e8e4d8"
          hsl="30 12% 90%"
        />
        <ColorSwatch
          color="#fef3e2"
          name="Light Orange Highlight"
          hex="#fef3e2"
          hsl="30 100% 94%"
        />
      </div>

      <div className="mt-8 p-6 bg-white rounded-lg border border-gray-200">
        <h3 className="text-xl font-semibold mb-4 font-quicksand">Usage in Tailwind</h3>
        <pre className="text-sm bg-gray-100 p-4 rounded-lg overflow-x-auto">
          <code>{`// Primary orange
<button className="bg-meeple-orange text-white">Button</button>

// Purple accent
<div className="border-meeple-purple">Accent Border</div>

// Warm background
<div className="bg-meeple-warm-bg">Content Area</div>

// Custom component
<div style={{ backgroundColor: 'hsl(var(--color-meeple-orange))' }}>
  Custom styling
</div>`}</code>
        </pre>
      </div>
    </div>
  ),
};

/**
 * Typography System
 * Quicksand for headings (bold, friendly), Nunito for body (readable, warm)
 */
export const Typography: Story = {
  render: () => (
    <div className="space-y-8 p-8 bg-gray-50">
      <div>
        <h2 className="text-3xl font-bold mb-6 font-quicksand">MeepleAI Typography</h2>
        <p className="text-gray-600 mb-8">
          Dual font system: Quicksand for headings (friendly, bold), Nunito for body text (readable, warm).
        </p>
      </div>

      <FontSample
        family="Quicksand"
        weights="400, 500, 600, 700"
        sample="MeepleAI Dashboard"
      />

      <FontSample
        family="Nunito"
        weights="300, 400, 600, 700"
        sample="Your AI board game assistant"
      />

      <div className="mt-8 p-6 bg-white rounded-lg border border-gray-200">
        <h3 className="text-xl font-semibold mb-4 font-quicksand">Typography Guidelines</h3>
        <ul className="space-y-2 text-sm">
          <li className="flex items-start gap-2">
            <span className="font-bold">Headings (h1-h6):</span>
            <span className="text-gray-600">Use Quicksand with weights 600-700 for bold, friendly impact</span>
          </li>
          <li className="flex items-start gap-2">
            <span className="font-bold">Body text:</span>
            <span className="text-gray-600">Use Nunito with weights 300-700 for optimal readability</span>
          </li>
          <li className="flex items-start gap-2">
            <span className="font-bold">Buttons:</span>
            <span className="text-gray-600">Quicksand 700 for strong calls-to-action</span>
          </li>
          <li className="flex items-start gap-2">
            <span className="font-bold">Labels:</span>
            <span className="text-gray-600">Nunito 600 for form labels and UI text</span>
          </li>
        </ul>

        <div className="mt-6">
          <h4 className="font-semibold mb-3 font-quicksand">Tailwind Usage</h4>
          <pre className="text-sm bg-gray-100 p-4 rounded-lg overflow-x-auto">
            <code>{`// Headings (auto-applied to h1-h6)
<h1 className="font-quicksand font-bold">Dashboard</h1>

// Body text (default)
<p className="font-nunito">Description text</p>

// Semantic classes
<h2 className="font-heading">Heading</h2>
<p className="font-body">Body text</p>`}</code>
          </pre>
        </div>
      </div>
    </div>
  ),
};

/**
 * Complete Design System Preview
 * Shows colors, typography, and component patterns together
 */
export const CompleteSystem: Story = {
  render: () => (
    <div className="p-8" style={{ backgroundColor: '#f8f6f0' }}>
      <div className="max-w-4xl mx-auto space-y-8">
        {/* Header */}
        <div>
          <h1 className="text-4xl font-bold mb-2" style={{ fontFamily: 'Quicksand', color: '#2d2d2d' }}>
            🎲 MeepleAI Design System
          </h1>
          <p className="text-lg" style={{ fontFamily: 'Nunito', color: '#666' }}>
            Warm, board-game-themed aesthetic for admin dashboard
          </p>
        </div>

        {/* Card Examples */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div
            className="p-6 rounded-2xl border shadow-sm transition-all hover:shadow-lg"
            style={{
              backgroundColor: 'white',
              borderColor: '#e8e4d8',
            }}
          >
            <div className="flex justify-between items-start mb-4">
              <span className="text-sm font-semibold" style={{ fontFamily: 'Nunito', color: '#666' }}>
                Total Users
              </span>
              <div
                className="w-12 h-12 rounded-lg flex items-center justify-center"
                style={{ backgroundColor: '#fef3e2' }}
              >
                <span className="text-2xl">👥</span>
              </div>
            </div>
            <div className="text-5xl font-bold mb-2" style={{ fontFamily: 'Quicksand', color: '#2d2d2d' }}>
              2,847
            </div>
            <div className="text-sm font-bold" style={{ color: '#16a34a' }}>
              ↗ +12% this week
            </div>
          </div>

          <div
            className="p-6 rounded-2xl border-l-4 shadow-sm"
            style={{
              backgroundColor: 'white',
              borderColor: '#e8e4d8',
              borderLeftColor: '#16a34a',
            }}
          >
            <div className="flex justify-between items-center mb-4">
              <div className="flex items-center gap-2">
                <div
                  className="w-3 h-3 rounded-full"
                  style={{ backgroundColor: '#16a34a' }}
                />
                <span className="text-sm font-bold" style={{ color: '#16a34a' }}>
                  Operativo
                </span>
              </div>
              <span className="text-xs" style={{ color: '#999' }}>
                10s fa
              </span>
            </div>
            <div className="text-xl font-bold mb-1" style={{ fontFamily: 'Quicksand', color: '#2d2d2d' }}>
              PostgreSQL
            </div>
            <div className="text-sm font-bold" style={{ color: '#16a34a' }}>
              5ms response time
            </div>
          </div>
        </div>

        {/* Button Examples */}
        <div className="flex gap-4 flex-wrap">
          <button
            className="px-7 py-3 rounded-xl font-bold transition-all hover:shadow-lg"
            style={{
              fontFamily: 'Quicksand',
              backgroundColor: '#d2691e',
              color: 'white',
            }}
          >
            Primary Action
          </button>
          <button
            className="px-7 py-3 rounded-xl font-bold transition-all hover:shadow-lg"
            style={{
              fontFamily: 'Quicksand',
              backgroundColor: '#16a34a',
              color: 'white',
            }}
          >
            Success Action
          </button>
          <button
            className="px-7 py-3 rounded-xl font-bold transition-all hover:shadow-lg"
            style={{
              fontFamily: 'Quicksand',
              backgroundColor: '#8b5cf6',
              color: 'white',
            }}
          >
            Accent Action
          </button>
        </div>

        {/* Activity Feed Example */}
        <div className="bg-white rounded-2xl border p-6" style={{ borderColor: '#e8e4d8' }}>
          <h3 className="text-xl font-bold mb-4" style={{ fontFamily: 'Quicksand', color: '#2d2d2d' }}>
            Activity Feed
          </h3>
          <div className="space-y-3">
            <div className="p-4 rounded-lg border hover:bg-gray-50 transition-all" style={{ borderColor: '#e8e4d8' }}>
              <div className="flex gap-3">
                <span className="text-2xl">👤</span>
                <div>
                  <div className="font-medium" style={{ fontFamily: 'Nunito', color: '#2d2d2d' }}>
                    New user registered
                  </div>
                  <div className="text-xs" style={{ color: '#999' }}>
                    2 minutes ago
                  </div>
                </div>
              </div>
            </div>

            <div
              className="p-4 rounded-lg border-l-4"
              style={{
                backgroundColor: '#fefce8',
                borderLeftColor: '#eab308',
              }}
            >
              <div className="flex gap-3">
                <span className="text-2xl">⚠️</span>
                <div>
                  <div className="font-medium" style={{ fontFamily: 'Nunito', color: '#2d2d2d' }}>
                    API timeout warning
                  </div>
                  <div className="text-xs" style={{ color: '#999' }}>
                    8 minutes ago
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* CSS Variables Reference */}
        <div className="bg-white rounded-2xl border p-6" style={{ borderColor: '#e8e4d8' }}>
          <h3 className="text-xl font-bold mb-4" style={{ fontFamily: 'Quicksand' }}>
            CSS Variables Reference
          </h3>
          <pre className="text-sm bg-gray-100 p-4 rounded-lg overflow-x-auto">
            <code>{`/* MeepleAI Brand Colors (in design-tokens.css) */
--color-meeple-orange: 25 85% 45%;         /* #d2691e */
--color-meeple-purple: 262 83% 62%;        /* #8b5cf6 */
--color-meeple-warm-bg: 30 25% 97%;        /* #f8f6f0 */
--color-meeple-dark: 0 0% 18%;             /* #2d2d2d */
--color-meeple-border: 30 12% 90%;         /* #e8e4d8 */
--color-meeple-light-orange: 30 100% 94%;  /* #fef3e2 */

/* Semantic mappings */
--meeple-primary: var(--color-meeple-orange);
--meeple-accent: var(--color-meeple-purple);
--meeple-surface: var(--color-meeple-warm-bg);

/* Font families */
--font-heading: var(--font-quicksand);
--font-body: var(--font-nunito);

/* Tailwind usage (in globals.css @theme inline) */
--color-meeple-orange: hsl(var(--color-meeple-orange));
--color-meeple-purple: hsl(var(--color-meeple-purple));

/* Usage in components */
className="bg-meeple-orange text-white"
className="border-meeple-purple"
className="font-heading text-3xl"
className="font-body text-base"`}</code>
          </pre>
        </div>
      </div>
    </div>
  ),
};

/**
 * Dark Mode Compatibility Preview
 * Shows how MeepleAI colors adapt to dark mode
 */
export const DarkModePreview: Story = {
  render: () => (
    <div className="grid grid-cols-1 md:grid-cols-2 gap-8 p-8">
      {/* Light Mode */}
      <div className="space-y-4" style={{ backgroundColor: '#f8f6f0', padding: '2rem', borderRadius: '1rem' }}>
        <h3 className="text-xl font-bold mb-4" style={{ fontFamily: 'Quicksand', color: '#2d2d2d' }}>
          Light Mode
        </h3>
        <div className="bg-white p-4 rounded-lg border" style={{ borderColor: '#e8e4d8' }}>
          <div className="text-sm font-semibold mb-2" style={{ color: '#666' }}>
            Metric Card
          </div>
          <div className="text-3xl font-bold" style={{ fontFamily: 'Quicksand', color: '#2d2d2d' }}>
            1,234
          </div>
        </div>
        <button
          className="w-full px-6 py-3 rounded-xl font-bold"
          style={{
            fontFamily: 'Quicksand',
            backgroundColor: '#d2691e',
            color: 'white',
          }}
        >
          Primary Button
        </button>
      </div>

      {/* Dark Mode Compatible */}
      <div className="space-y-4" style={{ backgroundColor: '#1a1a1a', padding: '2rem', borderRadius: '1rem' }}>
        <h3 className="text-xl font-bold mb-4" style={{ fontFamily: 'Quicksand', color: '#f5f5f5' }}>
          Dark Mode Compatible
        </h3>
        <div className="p-4 rounded-lg border" style={{ backgroundColor: '#2d2d2d', borderColor: '#3d3d3d' }}>
          <div className="text-sm font-semibold mb-2" style={{ color: '#999' }}>
            Metric Card
          </div>
          <div className="text-3xl font-bold" style={{ fontFamily: 'Quicksand', color: '#f5f5f5' }}>
            1,234
          </div>
        </div>
        <button
          className="w-full px-6 py-3 rounded-xl font-bold"
          style={{
            fontFamily: 'Quicksand',
            backgroundColor: '#d2691e',
            color: 'white',
          }}
        >
          Primary Button
        </button>
      </div>

      <div className="col-span-full mt-4 p-4 bg-blue-50 border border-blue-200 rounded-lg">
        <p className="text-sm text-blue-800">
          <strong>Note:</strong> MeepleAI colors are designed to work in both light and dark modes.
          The warm orange (#d2691e) provides consistent brand identity across themes.
        </p>
      </div>
    </div>
  ),
};

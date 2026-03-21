/** @type {import('tailwindcss').Config} */
module.exports = {
  darkMode: ['class'],
  content: [
    './src/pages/**/*.{js,ts,jsx,tsx,mdx}',
    './src/components/**/*.{js,ts,jsx,tsx,mdx}',
    './src/app/**/*.{js,ts,jsx,tsx,mdx}',
  ],
  theme: {
    extend: {
      // Colors now fully managed via CSS variables in globals.css
      // Brand colors (primary, secondary, accent) use HSL format
      // shadcn/ui semantic tokens also use HSL format
      fontFamily: {
        quicksand: ['var(--font-quicksand)', 'sans-serif'],
        inter: ['var(--font-inter)', 'sans-serif'],
        // Backward compat: existing font-nunito classes still work
        nunito: ['var(--font-inter)', 'sans-serif'],
        heading: ['var(--font-quicksand)', 'sans-serif'],
        body: ['var(--font-inter)', 'sans-serif'],
      },
      animation: {
        'fade-in': 'fadeIn 0.5s ease-in',
        'slide-up': 'slideUp 0.6s ease-out',
        'slide-down': 'slideDown 0.6s ease-out',
        'scale-in': 'scaleIn 0.4s ease-out',
        'pulse-slow': 'pulse 3s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        'pulse-meeple': 'pulseMeeple 2s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        shake: 'shake 0.4s ease-in-out',
        'voice-pulse': 'voice-pulse 1.5s ease-in-out infinite',
        'bounce-slow': 'bounce 1s infinite',
        'heart-beat': 'heartBeat 0.6s ease-in-out',
        // MeepleCard v2 animations (Issue #4604)
        'mc-shimmer': 'mc-shimmer 0.8s ease-out forwards',
        'mc-float-up': 'mc-float-up 0.35s ease-out both',
        'mc-slide-in-right': 'mc-slide-in-right 200ms cubic-bezier(0.4,0,0.2,1) both',
        'mc-badge-pulse': 'mc-badge-pulse 2s ease-in-out infinite',
        'mc-pulse-glow': 'mc-pulse-glow 2s ease-in-out infinite',
        'mc-unread-bounce': 'mc-unread-bounce 0.5s ease-out',
        'mc-live-pulse': 'mc-live-pulse 2s ease-in-out infinite',
        'mc-spin-slow': 'mc-spin-slow 8s linear infinite',
        // Neon Holo animations (spec: 2026-03-15)
        'holo-slide': 'holo-slide 4s ease-in-out infinite',
        'holo-rotate': 'holo-rotate 6s linear infinite',
        'entity-pulse': 'entity-pulse 3s ease-in-out infinite',
        'status-blink': 'status-blink 2s ease-in-out infinite',
      },
      keyframes: {
        fadeIn: {
          '0%': { opacity: '0' },
          '100%': { opacity: '1' },
        },
        slideUp: {
          '0%': { transform: 'translateY(20px)', opacity: '0' },
          '100%': { transform: 'translateY(0)', opacity: '1' },
        },
        slideDown: {
          '0%': { transform: 'translateY(-20px)', opacity: '0' },
          '100%': { transform: 'translateY(0)', opacity: '1' },
        },
        scaleIn: {
          '0%': { transform: 'scale(0.95)', opacity: '0' },
          '100%': { transform: 'scale(1)', opacity: '1' },
        },
        shake: {
          '0%, 100%': { transform: 'translateX(0)' },
          '10%, 30%, 50%, 70%, 90%': { transform: 'translateX(-10px)' },
          '20%, 40%, 60%, 80%': { transform: 'translateX(10px)' },
        },
        'voice-pulse': {
          '0%, 100%': { transform: 'scale(1)', opacity: '0.6' },
          '50%': { transform: 'scale(1.3)', opacity: '0' },
        },
        pulseMeeple: {
          '0%, 100%': { opacity: '1' },
          '50%': { opacity: '0.5' },
        },
        heartBeat: {
          '0%, 100%': { transform: 'scale(1)' },
          '15%': { transform: 'scale(1.15)' },
          '30%': { transform: 'scale(1)' },
        },
        // MeepleCard v2 keyframes (Issue #4604)
        'mc-shimmer': {
          '0%': { transform: 'translateX(-100%)' },
          '100%': { transform: 'translateX(100%)' },
        },
        'mc-float-up': {
          '0%': { opacity: '0', transform: 'translateY(12px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        'mc-slide-in-right': {
          from: { opacity: '0', transform: 'translateX(20px)' },
          to: { opacity: '1', transform: 'translateX(0)' },
        },
        'mc-badge-pulse': {
          '0%, 100%': { transform: 'scale(1)' },
          '50%': { transform: 'scale(1.08)' },
        },
        'mc-pulse-glow': {
          '0%, 100%': { boxShadow: '0 0 0 0 currentColor' },
          '50%': { boxShadow: '0 0 12px 4px currentColor' },
        },
        'mc-unread-bounce': {
          '0%, 100%': { transform: 'scale(1)' },
          '30%': { transform: 'scale(1.2)' },
          '60%': { transform: 'scale(0.95)' },
        },
        'mc-live-pulse': {
          '0%, 100%': { opacity: '1' },
          '50%': { opacity: '0.4' },
        },
        'mc-spin-slow': {
          '0%': { transform: 'rotate(0deg)' },
          '100%': { transform: 'rotate(360deg)' },
        },
        // Neon Holo keyframes
        'holo-slide': {
          '0%': { backgroundPosition: '-200% 0' },
          '50%': { backgroundPosition: '200% 0' },
          '100%': { backgroundPosition: '-200% 0' },
        },
        'holo-rotate': {
          '0%': { filter: 'hue-rotate(0deg)' },
          '100%': { filter: 'hue-rotate(360deg)' },
        },
        'entity-pulse': {
          '0%, 100%': {
            boxShadow: '0 4px 20px rgba(0,0,0,0.4), 0 0 15px var(--entity-glow-color, transparent)',
          },
          '50%': {
            boxShadow: '0 4px 20px rgba(0,0,0,0.4), 0 0 25px var(--entity-glow-color, transparent)',
          },
        },
        'status-blink': {
          '0%, 100%': { opacity: '1' },
          '50%': { opacity: '0.5' },
        },
      },
    },
  },
  plugins: [],
};

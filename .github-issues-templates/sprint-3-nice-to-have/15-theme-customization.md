# [FEATURE] Theme Customization System

## 🎯 Objective
Allow users to customize color themes beyond light/dark.

## ✅ Acceptance Criteria
- [ ] Theme picker in settings
- [ ] Preset themes:
  - Default (Blue)
  - Forest (Green)
  - Sunset (Orange/Red)
  - Ocean (Teal)
  - Midnight (Purple)
- [ ] Custom theme builder (advanced)
- [ ] Theme preview before apply
- [ ] Persist theme preference

## 🏗️ Implementation
Use CSS variables to switch themes:
```tsx
const themes = {
  default: {
    primary: '221 83% 53%',
    secondary: '142 76% 36%',
    accent: '36 100% 50%'
  },
  forest: {
    primary: '142 76% 36%',
    secondary: '84 81% 44%',
    accent: '47 89% 61%'
  },
  // ...
};

function applyTheme(themeName) {
  const theme = themes[themeName];
  Object.entries(theme).forEach(([key, value]) => {
    document.documentElement.style.setProperty(`--${key}`, value);
  });
}
```

Store in localStorage and apply on mount.

## ⏱️ Effort: **1 day** | **Sprint 3** | **Priority**: 🟢 Low

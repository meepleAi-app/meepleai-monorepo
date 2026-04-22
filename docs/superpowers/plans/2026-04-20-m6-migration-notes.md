# M6 Migration Notes — Claude Design v1 consumer pages

**Branch**: `redesign/v2` | **Closed**: 2026-04-20

## Summary
M6 migrated consumer pages (Auth, Settings, Notifications, Public) to 9 new Claude Design v1 primitives under `apps/web/src/components/ui/v2/`.

## Primitives delivered (9, 123 tests)
- AuthCard, OAuthButton, StepProgress, SettingsList, SettingsRow, ToggleSwitch, NotificationCard, HeroGradient, PricingCard

## Pages migrated
- `464b736ae` — reset-password flow migrated to AuthCard + Btn (M6 Task 5b)
- `880cd0781` — settings/notifications (15 toggles via SettingsList + ToggleSwitch) + settings/ai-consent + settings/security (SettingsRow with trailing)
- `48165a933` — notifications feed (NotificationCard + Drawer + entity grouping, 18/18 tests)
- `1aa1adced` — landing hero (WelcomeHero → HeroGradient)
- `a23071e4c` — about page (HeroGradient header)
- `bac4c6803` — contact page (submit Btn)

Plus earlier M6 primitives commits (landed on `redesign/v2`):
`7aa13eed0` AuthCard · `ebdc47a60` OAuthButton · `a6af8ad32` StepProgress · `378df4f20` SettingsList · `ac57dc0d1` SettingsRow · `764679188` ToggleSwitch · `19b2d48a4` NotificationCard · `61159d38b` HeroGradient · `4219753fb` PricingCard

## Gaps — Blocked migrations (require refactor of shared components)
- **Login/Register/Verify-email**: pages delegate to shared `AuthModal` / `LoginForm` / `RegisterForm` / `OAuthButtons`. Migrating requires refactoring those shared components, which exceeds the M6 scope constraint "preserve form logic exactly". Follow-up task needed with explicit permission.

## Gaps — Missing pages (design mockup exists, app page absent)
- **Pricing** (`/pricing`) — mockup has 3-tier (Free/Pro/Team), PricingCard primitive ready; needs product decision (CTA targets, Stripe integration)
- **Settings hub** (`/settings/page.tsx`) — landing for settings categories
- **Settings profile** form (displayName/bio/email/avatar)
- **Settings account** (password change, delete)
- **Settings preferences** (theme/lang/timezone/density)
- **Settings API keys**
- **Settings connected services** (Google/Discord/BGG)
- **Onboarding product-tour** flow (Welcome → Games → Agents → Session → Complete). Existing `/onboarding` is a 1-step profile form; `OnboardingWizard` is the invited-user account setup (different semantics). Needs product spec.

## Skipped intentionally
- **Terms / Privacy**: `LegalPageLayout` is typography-safe for long-form content; no v2 primitive fits this use case.

## Next steps
- **M6 follow-up**: refactor shared `AuthModal` + submit login/register/verify migration
- **M6.5**: product spec for missing Settings sub-pages, Pricing, Onboarding tour
- **M7**: Gruppo 2 (Game Night, Play Records, Session Wizard) when Claude Design ships mockups

## Related
- Brief: `docs/superpowers/specs/2026-04-20-claude-design-missing-pages-brief.md`
- Todo (resume with Claude Design): `docs/superpowers/claude_design_todo.md`

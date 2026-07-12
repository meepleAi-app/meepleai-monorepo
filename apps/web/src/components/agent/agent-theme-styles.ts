/**
 * Domain-scoped agent styling (neon-brutalist, issue #3237).
 *
 * Imported by the agent feature components rather than the global root layout so
 * the ~450 LOC of `--agent-*` tokens/classes plus a Google Fonts `@import` don't
 * ship in every page's CSS bundle (issue #2852). Consumers: AgentConfigSheet,
 * TemplateCarousel, TokenQuotaDisplay, LockedSlotCard.
 *
 * Load order matters: theme (custom properties) must precede typography and
 * animations, which consume `--agent-*` values.
 */
import '@/styles/agent-theme.css';
import '@/styles/agent-typography.css';
import '@/styles/agent-animations.css';

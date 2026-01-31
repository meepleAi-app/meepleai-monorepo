# Agent Page Design - Style Correction Summary

**Date**: 2026-01-31
**Reason**: Align with existing MeepleAI brand aesthetic
**Impact**: All 12 GitHub issues (#3237-#3248) need design reference update

---

## Design Change

### ❌ REJECTED: Neon Brutalist Terminal
- Dark mode cyber-punk aesthetic
- JetBrains Mono + Chakra Petch fonts
- Neon cyan/purple glows with intense box-shadows
- Industrial/tech dark theme
- **Reason**: Incompatible with MeepleAI warm, friendly brand identity

### ✅ APPROVED: MeepleAI Warm & Friendly
- Light mode with warm wood/paper texture background
- Quicksand (headings) + Nunito (body) fonts
- Orange (#d2691e) + Purple (#8b5cf6) brand colors
- Soft shadows, glass morphism (desktop only), rounded cards
- **Reason**: Consistent with existing library pages, game cards, admin UI, brand guidelines

---

## Corrected Design Elements

### Colors (MeepleAI Brand Palette)

```css
/* Light Mode (Primary) */
--primary: hsl(25, 95%, 38%);         /* #d2691e - MeepleAI Orange */
--secondary: hsl(142, 76%, 28%);      /* Green */
--accent: hsl(271, 91%, 55%);         /* #8b5cf6 - Purple */
--background: hsl(30, 25%, 97%);      /* #f8f6f0 - Warm beige */
--card: hsl(0, 0%, 100%);             /* White cards */

/* Status Colors */
--warning: hsl(36, 100%, 50%);        /* Amber (for quota warnings) */
--error: hsl(0, 84%, 60%);            /* Red (for quota exceeded) */
--success: hsl(142, 76%, 28%);        /* Green (for active slots) */
```

### Typography (Existing Fonts)

```typescript
// Headings, Buttons
font-family: 'Quicksand', sans-serif;
font-weight: 700 (bold) | 600 (semibold)

// Body Text
font-family: 'Nunito', sans-serif;
font-weight: 400 (regular) | 600 (semibold)
```

### Component Patterns

```typescript
// Cards
<Card className="rounded-xl shadow-sm hover:shadow-md hover:-translate-y-0.5">

// Buttons
<Button variant="default">Primary Orange</Button>
<Button variant="outline">Border + Hover</Button>
<Button variant="secondary">Green Secondary</Button>
<Button variant="ghost">No Background</Button>

// Progress Bars
<Progress value={90} className="h-2" />

// Badges
<Badge variant="default">Status</Badge>
```

### Animations (Subtle, MeepleAI Style)

```typescript
// Use existing Tailwind classes
className="animate-fade-in"              // Fade in (0.5s)
className="animate-slide-up"             // Slide up (0.6s)
className="animate-pulse-meeple"         // Subtle pulse (opacity)
className="hover:animate-bounce-subtle"  // Bounce on hover
```

---

## Mockup Comparison

### Visual Differences

**Neon Brutalist (OLD)**:
- Background: Pure black #0a0a0f
- Cards: Dark gray with glowing borders
- Text: White/cyan on dark
- Effects: Intense neon pulse animations
- Buttons: Uppercase, glowing cyan
- Progress: Cyan/purple/yellow neon bars

**MeepleAI Style (NEW)**:
- Background: Warm beige #f8f6f0 with wood texture
- Cards: White with soft shadows
- Text: Dark brown on light
- Effects: Subtle fade/slide animations
- Buttons: Rounded, orange/purple, soft hover
- Progress: Orange/yellow/red gradient bars

---

## Existing Components Analysis

### QuotaStatusBar.tsx - PERFECT MATCH!

**Already Implements**:
- ✅ Progress bar with color coding (<70% normal, 70-90% yellow, >90% red)
- ✅ Numeric display: "450 / 500"
- ✅ Warning message: "Stai per raggiungere il limite"
- ✅ Upgrade CTA: "Passa a Premium per +X"
- ✅ Tier badge: "Piano Free/Normal/Premium"
- ✅ Reset timer: Not yet (need to add)

**Reuse Strategy**: Extend QuotaStatusBar for agent token display, add countdown timer.

### AgentConfigModal.tsx - 80% REUSABLE!

**Already Implements**:
- ✅ Modal container with Dialog
- ✅ Model selection dropdown (Select component)
- ✅ Temperature slider
- ✅ Personality radio buttons
- ✅ Detail level radio buttons
- ✅ Custom instructions textarea
- ✅ Actions: Save, Test, Reset, Cancel

**Missing for Agent Page**:
- Game selector dropdown (easy add)
- Template carousel (new component)
- Slot display (new component)
- Token quota integration (add QuotaStatusBar)

**Reuse Strategy**: Refactor AgentConfigModal to support both existing config AND new template/slot/quota display.

### UserGameCard.tsx - PATTERN TO FOLLOW

**Patterns to Reuse**:
- ✅ Card layout with border-left color coding
- ✅ Framer Motion staggered entrance
- ✅ Actions dropdown with icons
- ✅ Badge displays (status, tier)
- ✅ Hover effects (translateY, shadow-md)

**Apply to**: ActiveSlotCard component in slot management page.

---

## Updated File Structure

```
apps/web/src/components/agent/
├── config/
│   ├── AgentLaunchSheet.tsx          # NEW: Combines config + template + quota
│   ├── TemplateCarousel.tsx          # NEW: Template selection
│   └── index.ts
│
├── chat/
│   ├── AgentChatSheet.tsx            # NEW: Bottom sheet chat
│   ├── ChatMessageList.tsx           # NEW: Message display
│   ├── ChatInput.tsx                 # NEW: Input + send
│   └── index.ts
│
├── slots/
│   ├── SlotManagementPage.tsx        # NEW: Full manager
│   ├── ActiveSlotCard.tsx            # NEW: Following UserGameCard pattern
│   └── index.ts
│
└── shared/
    ├── AgentActionBar.tsx            # NEW: Contextual action bar
    └── index.ts

# REUSE (DO NOT RECREATE)
apps/web/src/components/library/
├── QuotaStatusBar.tsx                # EXTEND: Add countdown timer
└── AgentConfigModal.tsx              # EXTEND: Add template/slot display
```

---

## GitHub Issues Update Strategy

### Option A: Update Existing Issues (Recommended)
- Edit description of each issue (#3237-#3248)
- Remove "Neon Brutalist" references
- Add "MeepleAI Style" references
- Update color/font specifications
- Change mockup reference to `agent-page-meepleai-style.html`

**Pros**: Keep issue numbers, preserve dependencies, less disruption
**Cons**: 12 manual edits required
**Time**: ~30 minutes

### Option B: Close & Recreate
- Close all 12 issues
- Recreate with corrected design specs
- Re-link dependencies
- Update epic document

**Pros**: Clean slate, correct from start
**Cons**: Lose issue numbers, reset progress tracking, more work
**Time**: ~60 minutes

### Option C: Add Correction Comment
- Add comment to each issue with design correction
- Link to `DESIGN_UPDATE_SUMMARY.md`
- Keep original issue bodies for history

**Pros**: Fastest, preserves history, clear correction trail
**Cons**: Designers need to read comment + original description
**Time**: ~10 minutes

---

## Recommended Action

**OPTION C (Add Correction Comment)**:

```bash
# Add comment to all agent issues
gh issue comment 3237 3238 3239 3240 3241 3242 3243 3244 3245 3246 3247 3248 --body "
⚠️ **Design Update**: Ignore "Neon Brutalist" style references in original description.

**Use MeepleAI Style Instead**:
- Mockup: \`docs/mockups/agent-page-meepleai-style.html\`
- Fonts: Quicksand (headings) + Nunito (body)
- Colors: Orange #d2691e (primary) + Purple #8b5cf6 (accent)
- Background: Warm beige with wood texture
- Effects: Soft shadows, glass morphism (desktop), subtle animations

**Reuse Existing Components**:
- QuotaStatusBar.tsx (token display)
- AgentConfigModal.tsx (config pattern)
- UserGameCard.tsx (card pattern)

**Details**: See \`docs/prd/DESIGN_UPDATE_SUMMARY.md\`
"
```

---

## New Mockup Reference

**File**: `docs/mockups/agent-page-meepleai-style.html`

**View**:
```bash
start docs/mockups/agent-page-meepleai-style.html
```

**Features** (Same as neon version, different aesthetics):
- 3 screen states (Config / Chat / Slots)
- Interactive template carousel
- Working progress bars
- Contextual action bar
- Responsive preview

**Style**: Warm MeepleAI brand colors, friendly rounded design, subtle animations

---

**Next Step**: Choose Option A, B, or C for updating GitHub issues

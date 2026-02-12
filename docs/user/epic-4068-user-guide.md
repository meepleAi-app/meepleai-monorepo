# MeepleAI v1.5: New Features Guide

**Welcome to Epic #4068: Enhanced MeepleCard System**

---

## What's New in v1.5?

### 🎯 Smart Permission System

**Your account now has a tier** (Free, Normal, Pro, Enterprise) that determines which features you can access.

**How to check your tier**:
1. Click your profile icon (top-right)
2. View "Account Settings"
3. See your current tier under "Subscription"

**Tier Comparison**:

| Feature | Free | Normal | Pro | Enterprise |
|---------|------|--------|-----|------------|
| Add to Wishlist | ✅ | ✅ | ✅ | ✅ |
| Collection Capacity | 50 games | 100 games | 500 games | Unlimited |
| Storage for PDFs | 100MB | 500MB | 5GB | Unlimited |
| Drag & Drop Reorder | ❌ | ✅ | ✅ | ✅ |
| Bulk Operations | ❌ | ❌ | ✅ | ✅ |
| Create AI Agents | ❌ | ❌ | ✅ | ✅ |
| Analytics Dashboard | ❌ | ❌ | ✅ | ✅ |
| Advanced Filters | ❌ | ✅ | ✅ | ✅ |

**Upgrade your tier**: Click "Upgrade" button in account settings

---

### 🏷️ Game Tags

**What are tags?**

Tags are small labels that appear on game cards showing important information like:
- **New**: Recently added to catalog
- **Sale**: Currently discounted
- **Owned**: In your collection
- **Wishlist**: On your wishlist
- **Exclusive**: Platform exclusive

**How to see tags**:

Game cards now show up to 3 tags on the left edge:

```
┌────────────────────┐
│█ New  ┌─────────┐ │  ← Tag strip (left edge)
│█      │  Cover  │ │
│█ Sale │  Image  │ │
│█      └─────────┘ │
│█ +2   Game Title  │  ← "+2" = 2 more hidden tags
└────────────────────┘
```

**See all tags**: Hover your mouse over the "+N" badge to see hidden tags in a tooltip.

**On mobile**: Tags show icons only (to save space). Tap to see full label.

---

### 💬 Smart Tooltips

**What changed?**

Tooltips now automatically position themselves to stay on screen:
- Near top of screen → Tooltip appears below
- Near bottom → Tooltip flips above
- Near left/right edges → Adjusts position

**How to use**:
- **Desktop**: Hover any info icon (ℹ️) to see tooltip
- **Mobile**: Tap info icon to show tooltip, tap outside to close
- **Keyboard**: Tab to info icon, press Enter to show, Escape to close

**Accessibility**:
- Screen readers announce tooltip content
- Keyboard navigation fully supported
- High contrast mode compatible

---

### 🤖 Enhanced AI Agent Cards

**New information on agent cards**:

1. **Status Badge**:
   - ● Green: Agent is active and processing
   - ○ Gray: Agent is idle (ready but not running)
   - ◐ Yellow: Agent is being trained
   - ✕ Red: Agent has encountered an error

2. **Model Information**:
   - Shows which AI model the agent uses (GPT-4o-mini, Claude, etc.)
   - Hover to see model parameters (temperature, max tokens)

3. **Usage Statistics**:
   - Invocation count: How many times agent was used
   - Last run: When agent was last executed
   - Average response time (Pro tier only)

**Where to find**: Navigate to "AI Agents" tab (requires Pro tier)

---

### 📊 Collection Limits

**What's new?**

Your collection page now shows:
- **Progress bar**: How full your collection is
- **Color coding**:
  - Green: You're good (<75% full)
  - Yellow: Getting full (75-90%)
  - Red: Almost full (>90%)
- **Warning icon**: Appears when approaching limit

**Example**:

```
Your Collection (Pro Tier)
┌──────────────────────────────┐
│ Games                        │
│ ████████████████░░░ 420/500  │  ← 84% (yellow)
│                              │
│ Storage                      │
│ ████████████░░░░░ 3.5/5 GB   │  ← 70% (green)
└──────────────────────────────┘
```

**What to do if full**:
- Remove games you no longer play
- Upgrade to higher tier for more capacity
- Free up storage by deleting old PDF rulebooks

---

## Frequently Asked Questions

### Q: What happened to my old collection?

**A**: Your collection is safe! All games are still there. We just added limits to show how much capacity you're using.

---

### Q: I see a lock icon (🔒) on some features. Why?

**A**: Some features require higher tiers:
- Normal tier: $4.99/month (100 games, drag & drop, advanced filters)
- Pro tier: $9.99/month (500 games, bulk operations, AI agents, analytics)

Click the "Upgrade" button to unlock these features.

---

### Q: Can I try Pro features before upgrading?

**A**: Yes! We offer a 14-day free trial of Pro tier. Click "Start Free Trial" in account settings.

---

### Q: What's the difference between tiers and roles?

**A**:
- **Tier** (Free/Normal/Pro/Enterprise): Subscription level you pay for
- **Role** (User/Editor/Creator/Admin): Special permissions granted by admins

Most users have "User" role. Editors and Creators have additional privileges even on lower tiers.

---

### Q: How do tags work?

**A**: Tags automatically appear based on game properties:
- "New" = Added to catalog in last 30 days
- "Sale" = Currently discounted
- "Owned" = In your collection
- "Wishlist" = On your wishlist

You don't manually add tags - they're automatic!

---

### Q: The tooltip is in the wrong place. Is this a bug?

**A**: No! Tooltips automatically position themselves:
- If trigger is near bottom of screen, tooltip appears above (not below)
- If trigger is near right edge, tooltip appears to the left

This is intentional to keep tooltips visible.

---

### Q: Can I hide tags if I don't like them?

**A**: Not yet, but we're considering this feedback. Tags are designed to be minimal (32px wide strip) and provide useful information at a glance.

---

### Q: What are AI agents?

**A**: AI agents are custom assistants that help with:
- Answering game rules questions
- Strategy recommendations
- Game recommendations based on your preferences

Creating agents requires Pro tier. Using pre-built agents is available to all tiers.

---

### Q: My collection shows "95% full" but I want to keep adding games. What should I do?

**A**:

**Option 1**: Upgrade tier
- Normal → Pro: 100 → 500 games (5x capacity)
- Pro → Enterprise: Unlimited

**Option 2**: Remove games
- Archive games you no longer play
- Delete games you won't play again

**Option 3**: Request limit increase (Enterprise only)
- Contact support for custom limits

---

## Getting Help

**If you encounter issues**:

1. **Check this guide**: Most questions answered above
2. **Visit Help Center**: https://help.meepleai.com
3. **Contact Support**: support@meepleai.com
4. **Community Forum**: https://community.meepleai.com

**Report a bug**:
- Use "Report Issue" button in app (bottom-right)
- Include steps to reproduce
- Attach screenshot if relevant

---

## Tips & Tricks

### Tip 1: Keyboard Shortcuts

- `Tab`: Navigate between cards
- `Enter`: Open card details
- `Space`: Quick preview
- `Esc`: Close tooltips/modals

### Tip 2: Bulk Operations (Pro Tier)

1. Enable bulk selection mode (checkbox icon, top-right)
2. Select games by clicking checkboxes
3. Click "Bulk Actions" dropdown
4. Choose action (Add to Collection, Export, etc.)

### Tip 3: Filter by Tags (Coming Soon)

- Click a tag on any game card to filter catalog by that tag
- Example: Click "Sale" → See all games on sale

### Tip 4: Agent Recommendations (Pro Tier)

- Go to "AI Agents" tab
- Click "Create Agent" → "Recommendation Agent"
- Train on your collection
- Get personalized game recommendations

---

## Upgrade Benefits

### Normal Tier ($4.99/month)

**Best for**: Casual collectors with small-medium collections

**Benefits**:
- 2x collection capacity (50 → 100 games)
- 5x storage (100MB → 500MB for PDFs)
- Drag & drop to reorder games
- Advanced filters
- Collection management tools

---

### Pro Tier ($9.99/month)

**Best for**: Serious collectors and power users

**Benefits**:
- All Normal tier features
- 10x collection capacity (50 → 500 games)
- 50x storage (100MB → 5GB)
- Bulk operations (multi-select, batch actions)
- Create custom AI agents
- Analytics dashboard
- Priority support

**Most popular!** ⭐

---

### Enterprise Tier (Contact Sales)

**Best for**: Organizations, clubs, content creators

**Benefits**:
- All Pro tier features
- Unlimited collection capacity
- Unlimited storage
- Custom branding
- API access
- Dedicated support
- SLA guarantees

---

## Changelog (v1.5)

**Released**: February 2026

**New Features**:
- Permission system (tier/role-based access)
- Vertical tag strips on game cards
- Smart tooltip positioning
- Enhanced AI agent cards with status/model/stats
- Collection limit indicators with progress bars

**Improvements**:
- Faster game catalog loading
- Better mobile experience
- Improved accessibility (WCAG 2.1 AA)
- Performance optimizations

**Bug Fixes**:
- Fixed tooltip positioning at screen edges
- Fixed tag overflow on small screens
- Fixed permission cache staleness
- Improved error messages

---

## Feedback

**We want to hear from you!**

- What do you think of the new tier system?
- Are tags useful or distracting?
- Any features you'd like to see in Pro tier?

**Submit feedback**: Click "Feedback" button in app or email feedback@meepleai.com

**Thank you for being part of MeepleAI!** 🎲

---

**Version**: 1.5.0 (Epic #4068)
**Last Updated**: February 2026
**Next Release**: v1.6 (March 2026) - Enhanced tag filtering and search

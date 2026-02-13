# Epic #4068: Frequently Asked Questions

**Comprehensive FAQ for permission system, tags, tooltips, agents, and collection limits**

---

## Permission System

### Q1: What's the difference between tier and role?

**A**: Think of it like this:
- **Tier** = What you **pay for** (Free, Normal, Pro, Enterprise)
- **Role** = Special **responsibilities** granted by administrators (User, Editor, Creator, Admin)

**Example**: You might be a "Free tier" user (haven't paid) but have "Editor" role (granted by admin to moderate content). The Editor role gives you access to some features that normally require Pro tier (like bulk selection).

**Most users** have Free/Normal/Pro tier + User role. Roles are special privileges, not common.

---

### Q2: How does "OR" logic work for permissions?

**A**: Many features use "OR" logic meaning you need **either** the tier **or** the role:

**Example: Bulk Selection**
- Requires: Pro tier **OR** Editor role
- Free + User = ❌ Denied (neither condition met)
- Free + Editor = ✅ Allowed (Editor role sufficient)
- Pro + User = ✅ Allowed (Pro tier sufficient)
- Pro + Editor = ✅ Allowed (both conditions met, extra safe!)

**Why OR?**: Flexibility! Admins can grant Editor role to power users without requiring Pro tier payment.

---

### Q3: What happens to my data if I downgrade tiers?

**A**: Your data is safe, but you may lose access to some features:

**Scenario**: You downgrade from Pro (500 games) → Normal (100 games), but you have 350 games in your collection.

**What happens**:
- Option A (Current): You cannot downgrade until you remove 250 games (keeps you below Normal limit)
- Option B (Future): Excess games archived automatically (read-only, can't add more until under limit)

**Recommendation**: Clean up collection before downgrading to avoid issues.

**Data Never Deleted**: We never delete your games automatically. You're always in control.

---

### Q4: Can I get a refund if I don't like Pro tier?

**A**: Yes! We offer:
- **14-day free trial** (no credit card required) - try Pro features risk-free
- **30-day money-back guarantee** - full refund if not satisfied
- **Downgrade anytime** - no cancellation fees

**How to request refund**: Email support@meepleai.com with your account email.

---

### Q5: What's the difference between Creator and Editor roles?

**A**:

**Editor**:
- Moderates public content (flag inappropriate games, suggest edits)
- Bulk selection access (for efficiency)
- Cannot publish new content

**Creator**:
- Everything Editor can do, PLUS:
- Publish verified content to catalog
- Create AI agents (even without Pro tier)
- Edit own published content

**How to become Creator/Editor**: Contact admin or email roles@meepleai.com with your use case.

---

## Tag System

### Q6: Can I customize which tags appear on my game cards?

**A**: Not yet (coming in v1.6!). Currently, tags are automatic:
- "New" = Game added to catalog in last 30 days
- "Sale" = Game currently has active discount
- "Owned" = Game in your collection
- "Wishlist" = Game on your wishlist

**Why automatic?**: Ensures accuracy (tags always reflect current state).

**Future (v1.6)**: You'll be able to:
- Create custom tags (Pro tier)
- Hide specific tag types
- Reorder tag priority

---

### Q7: Why do tags only show 3 at a time?

**A**: **Information hierarchy** and **visual clarity**:

- **Most important tags show first**: New, Sale, Exclusive (time-sensitive) appear before Owned, Wishlist (personal status)
- **Avoids clutter**: 5-10 tags would overwhelm the card
- **Overflow tooltip**: Hover "+N" badge to see all hidden tags

**Research**: User testing showed 3 visible tags optimal (more = overwhelmed, fewer = missed info).

**Adjust max**: In settings, you can set "Max visible tags" to 1-5 (Pro tier, coming v1.6).

---

### Q8: Tags disappeared on mobile. Is this a bug?

**A**: No, it's responsive design!

**Mobile (screens < 768px)**:
- Tag strip: 24px wide (narrower to fit)
- Tag labels: Hidden (icon-only mode)
- **Why**: Small screens need space for game title/info

**Tap any tag** to see full label in tooltip.

**Desktop**: Full labels visible (32px strip)

---

### Q9: Can I filter games by tag (e.g., show only games on sale)?

**A**: Coming in v1.6! Currently, tags are visual only. Next update:
- Click "Sale" tag → Filter catalog to games on sale
- Click "Owned" tag → Show only your collection
- Multiple tags: "Sale" + "Wishlist" = Games on wishlist that are on sale

**Workaround**: Use search bar: Type "sale" to find games with sale tag.

---

## Smart Tooltips

### Q10: Why did tooltip appear above instead of below?

**A**: **Auto-flip positioning**! Tooltips detect viewport edges:

**Example**:
- Button near **bottom** of screen → Tooltip appears **above** (not off-screen below)
- Button near **right** edge → Tooltip appears to **left**
- Button in **center** → Tooltip appears in **preferred position** (usually below)

**Benefit**: You always see the full tooltip, no matter where the trigger is.

---

### Q11: Tooltip disappeared when I moved my mouse. How do I read it?

**A**: Tooltips are **hoverable**!

**How to keep tooltip open**:
1. Hover trigger (tooltip appears)
2. Move mouse slowly to tooltip itself
3. Tooltip stays open while your mouse is over it
4. Move mouse away to close

**Mobile**: Tap trigger to show, tap outside to dismiss (no hovering needed).

**Keyboard**: Tab to trigger, Enter to show, Escape to hide. Tooltip stays open until you press Escape.

---

### Q12: Can I make tooltips stay open permanently?

**A**: Not by default (UX principle: tooltips are temporary supplemental info).

**Workarounds**:
- **Keyboard**: Press Enter to show, tooltip stays until you press Escape
- **Mobile**: Tap to show, stays until you tap outside
- **Future**: "Pin tooltip" feature (Pro tier, v1.7)

**Alternative**: If tooltip contains critical info, we might move it to the card itself (permanent display).

---

### Q13: Tooltip text is hard to read. Can I change font size?

**A**: Yes!

**Browser zoom**: Ctrl/Cmd + Plus/Minus to zoom entire page (tooltips scale too)

**Accessibility settings**: Enable large text in browser accessibility settings - tooltips respect your preference.

**High contrast mode** (Windows): Tooltips adapt to high contrast themes automatically.

**Future**: Custom font size setting (v1.6).

---

## AI Agent Metadata

### Q14: What does "◐ Training" status mean?

**A**: Agent is currently being trained or fine-tuned:

**What's happening**:
- Agent learning from new data (e.g., new rulebooks you uploaded)
- Model parameters being optimized
- Can take minutes to hours depending on data size

**Can I use agent while training?**: No, wait until status changes to "● Active" (green).

**How long?**: Typically 5-30 minutes. You'll receive notification when complete.

---

### Q15: Agent shows "✕ Error" status. What should I do?

**A**: **Error** status means agent encountered a problem:

**Common causes**:
- API key invalid/expired (check model provider settings)
- Model service down (OpenAI, Anthropic outage)
- Configuration error (check agent settings)

**How to fix**:
1. Click agent card → "View Logs" to see error details
2. Check agent settings → Verify API key valid
3. Try "Restart Agent" button
4. If persists, contact support with error message

**Temporary**: Agent in Error state doesn't count toward your invocation quota.

---

### Q16: What's the difference between GPT-4o-mini and GPT-4o?

**A**:

**GPT-4o-mini** (Default for Free/Normal):
- Faster responses (~500ms avg)
- Lower cost per invocation
- Good for simple questions (game rules, FAQ)
- Max 2K tokens per response

**GPT-4o** (Pro tier):
- More accurate, deeper reasoning
- Slower (~1.5s avg)
- Better for complex strategy analysis
- Max 4K tokens per response

**Claude 3 Opus** (Enterprise):
- Highest quality responses
- Best reasoning capability
- Expensive (10x cost vs GPT-4o-mini)
- Max 100K tokens (entire rulebooks in context!)

**How to change model**: Agent settings → "AI Model" dropdown (available models depend on your tier).

---

### Q17: Agent invocation count shows "1.2K". What does that mean?

**A**: **Human-readable formatting**:
- < 1,000: Exact number (e.g., "342")
- 1,000 - 999,999: Thousands with "K" (e.g., "1.2K" = 1,200)
- 1,000,000+: Millions with "M" (e.g., "3.5M" = 3,500,000)

**Why format?**: Easier to read at a glance. "1.2K" clearer than "1,234".

**Exact number**: Hover invocation count to see exact value in tooltip.

---

## Collection Limits

### Q18: I'm at 48/50 games (96%) but don't want to upgrade. What are my options?

**A**: Three approaches:

**Option 1: Remove games** (free)
- Archive games you've finished
- Delete games you won't play
- **Tip**: Sort by "Last Played" → Remove oldest

**Option 2: Use wishlist instead** (free)
- Move non-urgent games to wishlist
- Wishlist has no limit!
- Add to collection when ready to play

**Option 3: Upgrade tier** (paid)
- Normal: $4.99/month (100 games)
- Pro: $9.99/month (500 games)
- **First month 50% off** with code EPIC4068

**Option 4: Request exception** (rare)
- Email support@meepleai.com explaining your situation
- We may grant temporary limit increase for special cases

---

### Q19: What counts toward storage quota?

**A**: **PDF rulebooks and custom images** you upload:

**Counts**:
- ✅ PDF rulebooks you upload
- ✅ Custom game images you upload
- ✅ Scanned documents

**Does NOT count**:
- ❌ Game data from BoardGameGeek (we fetch, not stored in your quota)
- ❌ Cover images from our catalog (shared across users)
- ❌ Your user profile data (negligible size)

**Check usage**: Collection page → "Storage" section shows breakdown.

**Free up space**:
- Delete old PDF rulebooks
- Remove custom images (use catalog images instead)
- Compress PDFs before uploading (free tools available)

---

### Q20: Can I temporarily exceed my limit?

**A**: **Grace period** (yes, briefly):

**How it works**:
- You can add 1-2 games over limit (buffer for edge cases)
- System shows warning: "Over limit, please remove games or upgrade"
- **7-day grace period** to comply
- After 7 days: Can't add more games until under limit

**Why grace period?**: Prevents frustration from strict cutoff. Gives you time to decide: remove games or upgrade.

**Bulk imports**: Special handling (if bulk import would exceed limit, we ask beforehand: "This will exceed your limit. Upgrade or select fewer games?")

---

## Mobile Experience

### Q21: Tags are too small on mobile. Can they be bigger?

**A**: Tags on mobile use **icon-only mode** by design:

**Why**:
- Small screens (375px width) need space for game title
- Full tag labels would make strip 40px+ (too much space)
- Icons are recognizable (✓ = Owned, ❤️ = Wishlist, 🏷️ = Sale)

**See full label**: Tap any tag icon → Tooltip shows full label + description.

**Future**: "Compact mobile view" setting (hides tags entirely, shows on tap) - v1.6.

---

### Q22: How do I use tooltips on mobile (no mouse hover)?

**A**: **Tap instead of hover**!

**Desktop**: Hover info icon → Tooltip appears

**Mobile**: Tap info icon → Tooltip appears → Tap outside or press X to close

**Trick**: Tap and hold (long-press) also works for some tooltips.

**Accessibility**: Mobile tooltips have larger tap targets (44x44px minimum, WCAG AA compliant).

---

## Troubleshooting

### Q23: I upgraded to Pro but don't see Pro features yet. Why?

**A**: **Cache delay** (usually < 1 minute):

**What's happening**:
- Your tier was updated in database ✓
- Frontend cache refreshing (5-minute cache, but invalidated on tier change)
- WebSocket should push update instantly, but fallback is 1-minute poll

**Quick fix**: Refresh page (Ctrl+F5 or Cmd+Shift+R) - forces permission refetch

**Prevent**: We're improving real-time sync (SignalR/WebSocket, deployed v1.5.1).

---

### Q24: Tooltip shows in wrong language. How do I change it?

**A**: Tooltips use your **account language setting**:

1. Account Settings → Language → Select your language
2. Save
3. Refresh page

**Supported languages** (Epic #4068):
- English (en)
- Español (es)
- Français (fr)
- Deutsch (de)
- Italiano (it)
- 日本語 (ja)

**More languages coming**: Vote for your language in community forum!

---

### Q25: Tags disappeared after update. Where did they go?

**A**: **Check your browser zoom level**:

**Problem**: If browser zoom > 150%, tags may be hidden (responsive CSS breakpoint).

**Fix**: Reset zoom (Ctrl/Cmd + 0) to 100%.

**Alternative cause**: JavaScript error (rare). Check browser console (F12) for errors. If present, report bug to support@meepleai.com.

---

### Q26: Agent status stuck on "◐ Training" for hours. Is it broken?

**A**: **Long training expected** for large datasets:

**Typical training times**:
- Small dataset (< 10 pages): 5-10 minutes
- Medium dataset (10-50 pages): 15-30 minutes
- Large dataset (> 50 pages): 30-60 minutes
- Very large (> 100 pages): 1-2 hours

**Check progress**: Agent settings → "Training Log" shows progress percentage.

**If stuck > 2 hours**: Likely error. Click "Cancel Training" and try again. If fails again, contact support.

---

### Q27: Permission error says "Tier or role sufficient" but I don't have access. Bug?

**A**: Check **account status** (Active/Suspended/Banned):

**Suspended or Banned users** have **zero permissions**, even with Pro tier or Admin role.

**Check status**: Account Settings → Account Status section

**If suspended**: Contact support@meepleai.com to resolve issue (usually billing or TOS violation).

**If active but still denied**: Clear browser cache and cookies, logout, login again.

---

## Feature Requests

### Q28: Can I create my own custom tags?

**A**: **Coming in v1.6** (Pro tier feature)!

**Planned features**:
- Create custom tags with your own text, color, icon
- Apply custom tags to games
- Filter by custom tags
- Share custom tag sets with community

**Workaround**: Use "Notes" field on game cards to add custom labels (text only, not visual tags).

---

### Q29: I want "Team" tier (between Normal and Pro). Is this possible?

**A**: **Not currently**, but we're exploring:

**Feedback**: Many users want intermediate tier ($6.99/month)
- 250 games (between Normal's 100 and Pro's 500)
- 2GB storage (between 500MB and 5GB)
- Some Pro features (bulk select) but not all (no agents)

**Vote**: Community forum → Feature Requests → "Team Tier Proposal"

**ETA**: If enough demand, v1.7 (May 2026).

---

### Q30: Can admins see my collection?

**A**: **Privacy controls**:

**By default**:
- Collection visibility: **Private** (only you can see)
- Admins **cannot** see private collections (even Admin role)

**If you set Collection to "Public"**:
- Anyone can view (with share link or in public catalog)
- Still editable only by you

**If you set to "Shared"**:
- Only people with share link can view
- Admins still cannot view (unless you share link with them)

**Exceptions**:
- SuperAdmin (2-3 accounts globally) can view all collections (for moderation/support)
- Logged in audit trail (who viewed what, when)

---

## Billing & Upgrades

### Q31: Do I keep Pro features during free trial?

**A**: **Yes! Full Pro access** for 14 days:

**What you get (trial)**:
- 500 game collection limit (vs 50 Free)
- 5GB storage (vs 100MB)
- Bulk selection
- AI agent creation
- Analytics dashboard
- All Pro features unlocked

**After trial ends**:
- Auto-charge if you provided payment method
- Or downgrade to Free automatically (no charge)

**No credit card**: Trial available without payment method (we trust you!)

---

### Q32: Can I pay annually for discount?

**A**: **Yes! Save 20%** with annual billing:

**Monthly**:
- Normal: $4.99/month = $59.88/year
- Pro: $9.99/month = $119.88/year

**Annual (20% off)**:
- Normal: $47.88/year (save $12)
- Pro: $95.88/year (save $24)

**How to switch**: Account Settings → Billing → "Switch to Annual"

**Enterprise**: Custom pricing (annual contracts available).

---

### Q33: What happens if my payment fails?

**A**: **Grace period + downgrade**:

**Timeline**:
- Day 0: Payment fails (credit card expired, insufficient funds)
- Day 0: Email notification: "Payment failed, update payment method"
- Day 1-7: Grace period (Pro features still work)
- Day 7: If still unpaid, downgrade to Free tier
- Day 7: Email: "Downgraded to Free tier due to payment failure"

**Restore Pro**:
- Update payment method
- Re-subscribe
- Collection/data preserved (nothing deleted)

**Important**: If over Free tier limits (e.g., 300 games), you'll need to remove games or re-subscribe to access collection.

---

## Technical Questions

### Q34: Are my permissions checked on every page load?

**A**: **No, cached for 5 minutes**:

**How it works**:
1. First page load: Fetch permissions from API (~100ms)
2. Cache in browser for 5 minutes
3. Navigate around site: Instant (cached)
4. After 5 minutes: Auto-refetch (background, no loading spinner)

**Real-time updates**: If admin changes your tier/role, WebSocket pushes update instantly (no 5-minute wait).

**Manual refresh**: Account Settings → "Refresh Permissions" button forces immediate refetch.

---

### Q35: Does tag system slow down the site?

**A**: **No! Optimized for performance**:

**Benchmarks**:
- Tag rendering: 6ms per card (negligible)
- 100 cards with tags: ~150ms total (fast!)
- GPU-accelerated animations (smooth 60fps)

**Compared to no tags**: ~120ms vs ~150ms (30ms difference = barely noticeable).

**Lazy loading**: Tags off-screen aren't rendered (virtual scrolling).

---

### Q36: How does tooltip positioning know where I am on screen?

**A**: **JavaScript magic!** (Technical details)

**Algorithm**:
1. Measure trigger position: `getBoundingClientRect()` (where on screen)
2. Measure tooltip size: width, height
3. Calculate available space: above, below, left, right
4. Choose best placement: direction with most space
5. Position tooltip: CSS `top`/`bottom`/`left`/`right` values
6. **All in < 16ms** (60fps requirement)

**On scroll**: Repositions tooltip (debounced to avoid lag)

**On resize**: Repositions tooltip (responsive)

---

### Q37: Can I use MeepleAI API to check permissions programmatically?

**A**: **Yes! API available** (Pro tier):

**Endpoints**:
- `GET /api/v1/permissions/me` - Your permissions
- `GET /api/v1/permissions/check?feature=X` - Check specific feature

**Authentication**: Requires API key (generate in Account Settings → API Keys).

**Rate Limit**: 1000 requests/hour (Enterprise: unlimited).

**Documentation**: https://api.meepleai.com/docs (Scalar OpenAPI docs).

**SDK**: TypeScript/JavaScript SDK available: `npm install @meepleai/sdk`

---

## Feedback & Support

### Q38: I have an idea for improving tags. Where do I suggest it?

**A**: **We love feedback!**

**Options**:
1. **In-app**: Click "Feedback" button (bottom-right) → Select "Feature Request"
2. **Community forum**: https://community.meepleai.com → Feature Requests
3. **Email**: feedback@meepleai.com
4. **Discord**: #feature-requests channel

**We review**: Every Friday, team reviews top-voted feature requests.

**Gets implemented**: Top 3 requests each month get prioritized for next release.

---

### Q39: Found a bug. How do I report it?

**A**: **Use "Report Bug" button** (in-app, bottom-right):

**Include**:
- What you were trying to do
- What you expected to happen
- What actually happened
- Screenshots (if visual bug)
- Browser/device (Chrome on Windows, Safari on iPhone, etc.)

**Priority bugs** (crashes, data loss, security): Email support@meepleai.com immediately.

**Response time**:
- Critical: < 2 hours
- High: < 24 hours
- Medium: < 3 days
- Low: < 1 week

---

### Q40: Will Epic #4068 features ever be free?

**A**: **Some features may move to lower tiers over time**:

**History**: Features start as Pro exclusive, later move to Normal or Free as we add higher-value Pro features.

**Example roadmap**:
- v1.5 (now): Bulk select = Pro tier
- v1.7 (future): Bulk select → Normal tier, Advanced AI = Pro tier
- v2.0 (far future): Advanced filters → Free tier, New premium features = Pro

**Philosophy**: Free tier gets better over time as we add more value to paid tiers.

**Current Premium Features** (likely to stay paid):
- AI agent creation (costs us $$$ in API fees)
- Unlimited collections (database costs scale with usage)
- Analytics (compute-intensive)

---

**Have more questions?** Email faq@meepleai.com or visit https://help.meepleai.com

**Last Updated**: February 2026 (Epic #4068 v1.5.0)

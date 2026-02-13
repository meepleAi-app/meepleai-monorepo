# Share Requests Admin Guide

**Manual for reviewing and approving game share requests**

## Overview

**Admin Responsibilities**:
- ✅ Quality assurance (accuracy, completeness)
- ✅ Content moderation (spam, duplicates, inappropriate)
- ✅ User support (constructive feedback)
- ✅ Workflow management (locks, timelines)

**Access**: Admin Panel → Share Requests

---

## Dashboard Metrics

**Primary KPIs**:
- 🔢 **Pending**: Awaiting review
- 🔵 **In Review**: Active locks (total + yours)
- 🟠 **Changes Requested**: Awaiting user fixes
- ⏰ **Urgent**: >48h waiting

**Your Active Reviews**:
- Current locks
- Time remaining per lock
- Quick links to complete

### Filters
| Filter | Options |
|--------|---------|
| **Status** | Pending, InReview, ChangesRequested, All |
| **Type** | NewGame, AdditionalContent |
| **Date** | Custom range |
| **User** | Username filter |

**Search**: Full-text on game title + user notes
**Sort**: Creation date (oldest first - default), newest first, status, username

> 💡 **Best Practice**: Prioritize requests >48h to maintain fast response times

---

## Review Process

### Workflow
```
1. Select request → 2. Acquire lock → 3. Evaluate → 4. Decide → 5. Lock released
```

### 1. Acquire Lock
**Requirements**:
- Request in Pending or ChangesRequested
- No active lock by another admin
- Admin role

**Behavior**:
- ⏱️ Duration: 30 minutes (auto-release)
- 🔒 Exclusive: 1 admin at a time
- ♻️ Auto-expire: Lock releases if timeout

**If Locked**: Shows `⚠️ In review by [AdminName] - Lock expires: [time]`

### 2. Evaluation Checklist

#### ✅ Title (25% weight)
- [ ] Official Italian name (if available)
- [ ] Correct spelling
- [ ] Consistent format
- [ ] No non-standard abbreviations

```
✅ "I Coloni di Catan - Edizione Italiana"
❌ "Catan ITA"
```

#### ✅ Description (35% weight)
- [ ] ≥100 chars (target: 150-300)
- [ ] Describes main mechanics
- [ ] Indicates target audience
- [ ] Explains appeal
- [ ] No promotional spam
- [ ] No narrative spoilers

**Score**: 5=Excellent | 4=Good | 3=Acceptable | 2=Insufficient | 1=Unacceptable

#### ✅ Metadata (20% weight)
- [ ] Player range realistic (vs BGG)
- [ ] Playtime truthful (not just box)
- [ ] Complexity appropriate (1=party, 5=heavy)

#### ✅ Documents (20% weight)
- [ ] Files readable, not corrupted
- [ ] Content relevant
- [ ] Italian or universal language
- [ ] No copyright violations

**Acceptable**: ✅ Official translations, ✅ User-created references, ❌ Unauthorized scans

#### ✅ Duplicates
- Search catalog before approving
- If exact match exists → Reject with ID reference

---

## Decisions

### ✅ Approve (4-5 rating)
**When**: Complete, correct, all criteria met

**Process**:
1. Click **"Approve"**
2. Add admin notes (min 10 chars, mandatory)
3. Optional: Title/description override
4. Optional: Select document subset
5. Confirm

**Result**: Game created, badges assigned, email sent, lock released

**Example Notes**:
```
✅ "Excellent request, approved without changes. Welcome!"
✅ "Approved. Corrected title to official Italian name."
```

### ✅ Approve with Modifications
**When**: Valid but minor issues (typos, formatting)

**Allowed Overrides**:
- **Title**: Fix typo, use official name
- **Description**: Grammar, clarity improvements
- **Documents**: Publish subset only

**Best Practice**: Explain changes in admin notes

**Example**:
```
Original: "Catam"
Override: "I Coloni di Catan"
Notes: "Approved with title typo correction"
```

### 🔄 Request Changes (2-3 rating)
**When**: Incomplete or fixable issues

**Process**:
1. Click **"Request Changes"**
2. Write detailed feedback (min 20 chars, mandatory)
3. Optional: Admin notes (internal)
4. Confirm

**Result**: Status → ChangesRequested, email to user, lock released

**Quality Feedback Example**:
```
❌ Poor: "Description insufficient"

✅ Good:
"Description too brief. Please add:
1. Main mechanics (e.g., placement, cards, dice)
2. Target audience (families, experts, kids)
3. What makes it special vs similar games

Target: 150-300 chars.
Examples: See games #123, #456 in catalog."
```

### ❌ Reject (1 rating, severe only)
**When**: Spam, copyright violations, exact duplicates, invalid

**Process**:
1. Click **"Reject"**
2. Specify reason (min 20 chars, mandatory)
3. Optional: Admin notes
4. Confirm

**Result**: Status → Rejected, email sent, lock released

**Clear Reasons**:
```
✅ "Rejected: Game 'Catan' exists with ID #12345. Search before sharing."
✅ "Rejected: Attached documents are unauthorized scans (copyright violation)."
❌ "Rejected: not good"
```

> 💡 **Philosophy**: Prefer "Request Changes" over "Reject" to guide users

### 🔓 Release Lock
**When**: Started by mistake, need more time, interrupted

**Effect**: Lock released, request returns to Pending/ChangesRequested

---

## Decision Matrix

| Title | Description | Metadata | Documents | Decision |
|-------|-------------|----------|-----------|----------|
| Pass | 4-5 | Pass | 3-5 | ✅ Approve |
| Pass | 3 | Pass | Any | ✅ or 🔄 Request improvements |
| Pass | 2 | Pass | Any | 🔄 Request changes |
| Fail | Any | Any | Any | ❌ or 🔄 |
| Pass | Any | Fail | Any | 🔄 Fix metadata |
| Pass | Any | Pass | Fail | ❌ Copyright |

---

## Common Scenarios

### Case 1: Perfect Request (5min)
```
Checklist: All ✅
Decision: Approve
Notes: "Excellent request, approved immediately. Thank you!"
```

### Case 2: Typo in Title (7min)
```
Issue: "Catam" → "Catan"
Decision: Approve with override
Override: "I Coloni di Catan"
Notes: "Approved with title typo correction"
```

### Case 3: Brief Description (10min)
```
Issue: Description too short
Decision: Request changes
Feedback: [Use template below]
```

**Template**:
```
Description too brief. Please expand:
1. Main mechanics (placement, cards, etc.)
2. Target audience (families, experts)
3. What makes it special

Target: 150-300 chars. Examples: games #123, #456.
```

### Case 4: Duplicate (8min)
```
Issue: Game exists as #789
Decision: Reject
Reason: "Game exists with ID #789. Use search before sharing."
```

### Case 5: Copyright Issue (10min)
```
Issue: Unauthorized scans
Decision: Reject
Reason: "Documents are unauthorized scans (copyright). Share only:
- Official translations from publisher
- Documents you own rights to
- User-created reference sheets"
```

---

## Rate Limit Management

### View User Limits
**In request detail** → Contributor Profile:
```
Tier: Free
Limit: 3/month
Used: 2/3
Remaining: 1
Resets: 01/02/2026
Override: None
```

### Create Override
**Admin → Rate Limit Management**:
1. Search user by email/username
2. **Add Override**
3. Configure:
   - **New Limit**: Number or -1 (unlimited)
   - **Expires**: Optional date
   - **Reason**: Mandatory
4. Save

**Examples**:
```
✅ "Contest Jan 2026: unlimited until 31/01"
✅ "Expert contributor: 20/month for 3 months"
✅ "Beta tester: unlimited during Jan 15-30"
```

**Audit**: All logged (who, when, why, expiry)

---

## Performance Targets

### Organization KPIs
- ⏱️ **Avg Response**: <48h
- ✅ **Approval Rate**: 60-80% (balance)
- 🔄 **Changes Rate**: 15-25% (healthy)
- ❌ **Rejection Rate**: <10% (severe only)

### Your Metrics
- Reviews completed this month
- Avg decision time
- Decision distribution
- User satisfaction score

### Weekly Report (Monday)
```
📊 Weekly Summary

New: 23
Approved: 15 (65%)
Changes: 6 (26%)
Rejected: 2 (9%)

Your Activity:
- Reviews: 8
- Avg Time: 12min
- Pending: 2

Team:
- Avg Response: 36h ✅
- Backlog: 12
- Oldest: 18h
```

---

## Best Practices

### Efficiency ✅
- Process in batches (5-10)
- Use templates for common feedback
- Prioritize >48h requests
- Complete within 15-20min
- Release lock if interrupted

### Quality ✅
- Specific, actionable feedback
- Cite good examples
- Professional, courteous tone
- Document complex decisions

### Consistency ✅
- Apply uniform standards
- Consult other admins for edge cases
- Update guidelines for recurring patterns

---

## Feedback Templates

### Brief Description
```
Description too brief. Add:
1. Main mechanics
2. Target audience
3. What makes it special

Length: 150-300 chars. Examples: #123, #456.
```

### Metadata Issues
```
Metadata seems inaccurate. Verify:
- Players {min}-{max}: Correct vs BGG?
- Playtime {mins}: Reflects real game duration?
- Complexity {n}/5: Appropriate? (1=party, 5=heavy)

Use actual play experience, not just box data.
```

### Copyright
```
Documents have copyright issues:
- {filename}: {issue}

Share only:
- Official free publisher materials
- Documents you own rights to
- Self-created reference sheets

Remove problematic docs and resubmit.
```

---

## Escalation

### When to Escalate
- Complex copyright cases
- Spam/abuse patterns
- Admin disagreements
- Technical bugs

### Support Channels
- 💬 Slack: #share-requests-admin
- 📧 Email: admin-support@meepleai.com
- 📞 Weekly call: Wednesday 15:00

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| **Lock won't acquire** | Check if another admin has lock, reload page |
| **Can't decide** | Lock expired, reload page, re-acquire |
| **Request stuck InReview** | Wait 1h for auto-release or contact Super Admin |
| **Wrong decision** | Contact Super Admin (not reversible via UI) |

---

**Version**: 1.0
**Last Updated**: 2026-01-23
**Maintainer**: MeepleAI Admin Team

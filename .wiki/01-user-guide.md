# User Guide - MeepleAI

**Audience**: End users who want to use MeepleAI to get help with board game rules.

## 📋 Table of Contents

1. [Getting Started](#getting-started)
2. [Account Setup](#account-setup)
3. [Asking Questions](#asking-questions)
4. [Search Features](#search-features)
5. [Managing Games](#managing-games)
6. [Chat Sessions](#chat-sessions)
7. [Account Settings](#account-settings)
8. [API Access](#api-access)
9. [Tips for Best Results](#tips-for-best-results)
10. [Troubleshooting](#troubleshooting)

## 🚀 Getting Started

### Accessing MeepleAI

**Production**: `https://meepleai.dev`
**Local Development**: `http://localhost:3000`

### First Time Users

1. Visit the website
2. Click "Sign Up" or "Register"
3. Create an account (or use OAuth)
4. Verify your email (if required)
5. Start asking questions!

## 🔐 Account Setup

### Registration Options

#### Option 1: Email & Password
1. Go to `/register`
2. Enter email and password
3. Confirm password
4. Submit registration
5. Check email for verification (if enabled)

**Password Requirements**:
- Minimum 8 characters
- At least one uppercase letter
- At least one lowercase letter
- At least one number
- At least one special character

#### Option 2: OAuth Sign-In
1. Click "Sign in with Google/GitHub/Discord"
2. Authorize MeepleAI
3. Account created automatically

**Supported Providers**:
- ✅ Google
- ✅ GitHub
- ✅ Discord

### Two-Factor Authentication (2FA)

Enhance your account security with TOTP-based 2FA.

**Enable 2FA**:
1. Go to Settings → Privacy
2. Click "Enable Two-Factor Authentication"
3. Scan QR code with authenticator app (Google Authenticator, Authy, etc.)
4. Enter verification code
5. Save backup codes securely

**Login with 2FA**:
1. Enter email and password
2. Enter 6-digit code from authenticator app
3. Click "Verify"

**Backup Codes**:
- Use if you lose access to authenticator app
- Each code is single-use
- Generate new codes in Settings

## 💬 Asking Questions

### How to Ask Questions

1. Navigate to the chat interface
2. Type your question in natural language
3. Press Enter or click "Send"
4. Wait for the AI response

### Example Questions

**Good Questions** ✅:
- "In Catan, can I trade with the bank on any turn?"
- "What happens if I roll a 7 in Settlers of Catan?"
- "How does movement work for the knight in Wingspan?"
- "Can I play an action card during another player's turn in 7 Wonders?"

**Less Effective Questions** ⚠️:
- "How to play?" (too vague)
- "Rules" (not a question)
- "Is this game good?" (subjective, not rules-based)

### Understanding Responses

MeepleAI provides:
- **Answer**: Direct response to your question
- **Confidence**: How confident the AI is (0-100%)
- **Citations**: References to rulebook sections
- **Context**: Additional relevant information

**Confidence Levels**:
- **90-100%**: Very confident, well-documented rule
- **70-89%**: Confident, but may have some ambiguity
- **50-69%**: Moderate confidence, verify with rulebook
- **Below 50%**: Low confidence, check official sources

### Follow-Up Questions

MeepleAI maintains conversation context:

```
You: "How does trading work in Catan?"
AI: [Explains trading rules]

You: "Can I trade with multiple players in one turn?"
AI: [Uses context from previous question]
```

## 🔍 Search Features

### Quick Search

1. Go to Search page
2. Enter keywords or questions
3. View ranked results
4. Click to see full details

### Hybrid Search

MeepleAI uses **Hybrid RAG** (Retrieval-Augmented Generation):
- **Vector Search**: Semantic similarity
- **Keyword Search**: Exact matches
- **RRF Fusion**: Combines both (70% vector, 30% keyword)

### Search Filters

**By Game**:
- Select specific game from dropdown
- Search only that game's rules

**By Confidence**:
- Filter by minimum confidence threshold
- Default: 70%

**By Date**:
- Filter by document upload date
- Useful for finding recent rule updates

## 🎲 Managing Games

### Browse Games

1. Go to Games page
2. Browse available games
3. Click on a game to see details

**Game Information**:
- Title and publisher
- Complexity rating
- Player count
- Play time
- BGG rating (if available)

### Upload Custom Rulebooks

**Note**: Requires appropriate permissions.

1. Go to Games → Upload
2. Select PDF rulebook
3. Enter game metadata:
   - Title
   - Publisher
   - Year
   - Language
4. Click "Upload"
5. Wait for processing (may take 1-5 minutes)

**PDF Requirements**:
- Format: PDF
- Max size: 50MB
- Text-based (not scanned images)
- Language: Italian preferred, English supported

**Processing Stages**:
1. **Upload**: File uploaded to server
2. **Extraction**: Text extracted (3-stage pipeline)
3. **Chunking**: Text divided into sections
4. **Embedding**: Vector representations created
5. **Indexing**: Added to search database

**Quality Score**:
- MeepleAI rates PDF quality (0-100%)
- High quality (>80%): Excellent extraction
- Medium quality (60-79%): Good extraction, minor issues
- Low quality (<60%): May have extraction problems

### Favorite Games

1. Browse to game detail page
2. Click "Add to Favorites"
3. Access favorites from dashboard

## 💬 Chat Sessions

### Create New Session

1. Go to Chat
2. Click "New Session"
3. Optionally select a specific game
4. Start asking questions

### Manage Sessions

**View History**:
- All chat sessions saved automatically
- Access from "Chat History"

**Continue Session**:
- Click on previous session
- Context maintained
- Add follow-up questions

**Delete Session**:
- Click trash icon
- Confirm deletion
- Cannot be undone

**Export Session**:
- Click export icon
- Download as JSON or Markdown
- Includes all messages and metadata

## ⚙️ Account Settings

Access settings at `/settings` (or `/profile` - redirects to settings).

### Profile Tab

**Display Name**:
- Change your display name
- Visible to other users (if sharing enabled)

**Email**:
- View current email
- Change email (requires verification)

**Password**:
- Change your password
- Requires current password

### Preferences Tab

**Language**:
- Italian (default)
- English
- Other languages (coming soon)

**Theme**:
- Light mode
- Dark mode
- System preference

**Notifications**:
- Email notifications
- Weekly digest
- New game alerts

**Data Retention**:
- Delete chat history older than X days
- Auto-delete sessions

### Privacy Tab

**Two-Factor Authentication**:
- Enable/disable 2FA
- Regenerate backup codes
- View 2FA status

**OAuth Connections**:
- Link/unlink Google account
- Link/unlink GitHub account
- Link/unlink Discord account
- Manage connected accounts

**Data Privacy**:
- View data we collect
- Download your data
- Request data deletion

### Advanced Tab

**API Keys**:
- Generate API keys for programmatic access
- Manage existing keys
- Revoke keys

**Active Sessions**:
- View all active login sessions
- See device and location
- Revoke individual sessions

**Account Deletion**:
- Permanently delete account
- Cannot be undone
- Requires password confirmation

## 🔑 API Access

For advanced users and developers.

### Generate API Key

1. Go to Settings → Advanced
2. Click "Generate New API Key"
3. Enter description (e.g., "My Python Script")
4. Copy key immediately (shown only once)
5. Store securely

**API Key Format**: `mpl_{env}_{base64}`
- Example: `mpl_prod_YWJjZGVmZ2hpamtsbW5vcA==`

### Using API Keys

**REST API**:
```bash
curl https://api.meepleai.dev/api/v1/chat \
  -H "X-API-Key: mpl_prod_YOUR_KEY_HERE" \
  -H "Content-Type: application/json" \
  -d '{"question": "How does trading work in Catan?", "gameId": 123}'
```

**See [API Documentation](../docs/03-api/board-game-ai-api-specification.md)** for complete API reference.

### Rate Limits

| Tier | Requests/Hour | Requests/Day |
|------|---------------|--------------|
| **Free** | 60 | 1,000 |
| **Pro** | 600 | 10,000 |
| **Enterprise** | Custom | Custom |

## 💡 Tips for Best Results

### Question Formulation

**DO** ✅:
- Be specific: "In Catan, what happens when I roll a 7?"
- Include game name if relevant
- Ask about rules, not strategy
- Use follow-up questions for clarification

**DON'T** ❌:
- Be vague: "How do I win?"
- Ask about house rules or variants (unless specified)
- Expect strategy advice (rules only)
- Ask multiple unrelated questions at once

### Context Matters

- Start a new session for different games
- Keep related questions in same session
- Reference previous messages: "What about in a 2-player game?"

### Interpreting Results

**High Confidence (>80%)**: Trust the answer, but verify critical rules.

**Medium Confidence (60-80%)**: Good starting point, cross-reference with rulebook.

**Low Confidence (<60%)**: Treat as suggestion only, check official rules.

**Always Check Citations**: MeepleAI provides rulebook page references.

### When to Check the Rulebook

- Critical game-deciding rules
- Tournament play
- Confidence score below 70%
- Conflicting answers
- House rule clarifications

## 🐛 Troubleshooting

### Common Issues

#### "Cannot log in"
- Check email/password spelling
- Reset password if forgotten
- Check 2FA code (if enabled)
- Clear browser cache
- Try incognito/private mode

#### "No results found"
- Check spelling of game name
- Try different keywords
- Ensure game is in database
- Try hybrid search instead of exact match

#### "Low confidence answers"
- Rulebook may not be uploaded
- Question may be ambiguous
- Rule may not exist in uploaded documents
- Try rephrasing question

#### "Slow responses"
- Server may be under load
- Check internet connection
- Try again in a few moments
- Check status page (if available)

#### "PDF upload failed"
- Check file size (<50MB)
- Ensure PDF format
- Check if PDF is text-based (not scanned)
- Try converting scanned PDF to OCR text

### Getting Help

1. **Check FAQ**: [Coming soon]
2. **Search Issues**: [GitHub Issues](https://github.com/DegrassiAaron/meepleai-monorepo/issues)
3. **Post Discussion**: [GitHub Discussions](https://github.com/DegrassiAaron/meepleai-monorepo/discussions)
4. **Contact Support**: support@meepleai.dev (if available)

### Reporting Bugs

When reporting issues, include:
- What you were trying to do
- What happened instead
- Steps to reproduce
- Browser and OS version
- Screenshots (if applicable)
- Error messages (if any)

## 📞 Contact & Support

- **Issues**: [GitHub Issues](https://github.com/DegrassiAaron/meepleai-monorepo/issues)
- **Discussions**: [GitHub Discussions](https://github.com/DegrassiAaron/meepleai-monorepo/discussions)
- **Email**: support@meepleai.dev (if available)

## 🔗 Related Resources

- **[Home](./00-home.md)** - Project overview
- **[API Documentation](../docs/03-api/board-game-ai-api-specification.md)** - For developers
- **[FAQ](./faq.md)** - Frequently asked questions (coming soon)

---

**Version**: 1.0-rc
**Last Updated**: 2025-11-15
**For**: End Users

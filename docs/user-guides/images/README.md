# Share Game Screenshots

**TODO: Manual screenshot creation required**

## Required Screenshots

This directory needs 3 screenshots for the Share Game User Guide:

### 1. share-step1.png

**What to capture**: User library page with "Share with Community" button visible

**Steps**:
1. Login as regular user
2. Navigate to `/library`
3. Locate a game card
4. Ensure "Share with Community" button is visible
5. Take screenshot (full browser viewport or focused on game card)

**Recommended size**: 1200x800 or 800x600

---

### 2. share-step2.png

**What to capture**: Share Game Wizard - Step 1 or 2 (Game Info form)

**Steps**:
1. Click "Share with Community" on a game
2. Wizard opens - Step 1: Game Info
3. Show form fields (title, description) partially filled
4. Take screenshot of wizard modal

**Recommended size**: 1200x900 (wizard modal is tall)

---

### 3. share-step3.png

**What to capture**: Share Game Wizard - Step 4 (Document Upload)

**Steps**:
1. Navigate to Step 4 of wizard
2. Show document upload area (with or without files)
3. Display upload instructions
4. Take screenshot

**Recommended size**: 1200x800

---

## Screenshot Guidelines

### Technical Requirements

- **Format**: PNG (preferred for UI) or JPG
- **Resolution**: Minimum 800px width, prefer 1200-1600px for retina displays
- **Compression**: Optimize with TinyPNG or similar (target < 500KB per file)
- **Browser**: Chrome or Firefox with standard zoom (100%)

### Content Guidelines

- **Language**: Italian UI (match user guide language)
- **Test Data**: Use realistic but clearly fake data
  - Game: "Catan" or "Ticket to Ride" (well-known examples)
  - Username: "Mario Rossi" or "Test User"
  - Avoid real personal information

- **Highlighting**: Add arrows or highlights if needed to draw attention to key elements
  - Tool: Snagit, Skitch, or Figma
  - Color: MeepleAI brand colors (check design system)

- **Consistency**: Same browser chrome, same zoom level, same time of day (avoid dark mode mixed with light mode)

### Accessibility

- **Alt Text**: Add descriptive alt text in markdown references
- **Captions**: Consider adding brief captions below images in the guide
- **High Contrast**: Ensure screenshots are readable for users with visual impairments

---

## How to Add Screenshots

Once created:

1. Save PNG files in this directory with exact names:
   - `share-step1.png`
   - `share-step2.png`
   - `share-step3.png`

2. Verify references in `../share-game-guide.md`:
   ```markdown
   ![Seleziona Gioco](./images/share-step1.png)
   ![Informazioni Base](./images/share-step2.png)
   ![Allega Documenti](./images/share-step3.png)
   ```

3. Commit and push:
   ```bash
   git add docs/user-guides/images/*.png
   git commit -m "docs(user-guide): add share game workflow screenshots"
   git push
   ```

---

## Alternative: Use Placeholders

If screenshots cannot be created immediately, consider using:

1. **Figma Mockups**: Create UI mockups in Figma and export as PNG
2. **Placeholder Images**: Use https://placeholder.com with dimensions
3. **Storybook Screenshots**: If components have Storybook stories, export from there

**Temporary Placeholder**:
```markdown
![Seleziona Gioco](https://via.placeholder.com/1200x800/4A90E2/FFFFFF?text=Share+Game+Step+1)
```

Replace with real screenshots before documentation goes live.

---

**Status**: 🔴 PENDING - Screenshots not yet created
**Priority**: Medium (documentation complete but screenshots enhance UX)
**Assignee**: TBD (design team or documentation team)

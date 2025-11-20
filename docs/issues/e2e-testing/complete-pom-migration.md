# Complete POM Migration

**Issue ID**: E2E-006 | **Priorità**: 🟡 MEDIA | **Effort**: 20-30 ore

---

## 📋 Problem

POM architecture è progettata ma **migrazione è 0% completata**. Solo 2-3 test usano ChatPage/AuthPage. Rimanenti 43 test usano direct page interactions.

```typescript
// ❌ CURRENT: Direct page interactions (43/46 files)
test('chat flow', async ({ page }) => {
  await page.goto('/chat');
  await page.getByRole('textbox').fill('Question');
  await page.getByRole('button').click();
});

// ✅ TARGET: POM pattern (3/46 files)
test('chat flow', async ({ page }) => {
  const chatPage = new ChatPage(page);
  await chatPage.goto();
  await chatPage.askQuestionAndWait('Question');
});
```

**README Status**: "Migration Status: 0/30 test files (0%)"

---

## 🎯 Impact

- **Maintainability**: ⬇️ Locator changes require updates in 43 files
- **Reusability**: ⬇️ No shared methods across tests
- **Readability**: ⬇️ Tests are verbose, low-level

---

## ✅ Solution

### Priority Migration Order

1. **Week 1**: Auth tests (7 files) → Use AuthPage
2. **Week 2**: Chat tests (7 files) → Use ChatPage
3. **Week 3**: Admin tests (6 files) → Create AdminPage, migrate
4. **Week 4**: PDF tests (5 files) → Create UploadPage, migrate

### Implementation Template

```typescript
// 1. Create Page Object (if missing)
export class UploadPage extends BasePage {
  private get fileInput(): Locator {
    return this.page.locator('input[type="file"]');
  }

  async uploadPdf(filePath: string): Promise<void> {
    await this.uploadFile(this.fileInput, filePath);
  }
}

// 2. Migrate Test
import { UploadPage } from './pages/upload/UploadPage';

test('upload PDF', async ({ page }) => {
  const uploadPage = new UploadPage(page);
  await uploadPage.goto();
  await uploadPage.uploadPdf('test.pdf');
  await uploadPage.assertPdfVisible('test.pdf');
});
```

---

## 📝 Checklist

- [ ] Create missing Page Objects (5 pages: Upload, Admin, Editor, Game, User)
- [ ] Migrate auth tests (7 files)
- [ ] Migrate chat tests (7 files)
- [ ] Migrate admin tests (6 files)
- [ ] Migrate PDF tests (5 files)
- [ ] Migrate remaining tests (remaining 5-10 files)
- [ ] Update README: 0% → 70%+

---

**Target**: 70%+ migration (21/30 files) by end of Phase 2

# Add Negative Test Scenarios

**Issue ID**: E2E-008 | **Priorità**: 🟡 MEDIA | **Effort**: 6-8 ore

---

## 📋 Problem

Test suite focalizzata su **happy paths**. Mancano test per scenari di errore, validation, edge cases.

**Esempi di gap**:
- ❌ Upload file non-PDF
- ❌ Upload file troppo grande (>50MB)
- ❌ Form submission con campi invalidi
- ❌ Chat con input vuoto
- ❌ Login con email malformata
- ❌ PDF corrotto
- ❌ Network timeout durante upload

---

## 🎯 Impact

- **Quality**: Bug in error handling non rilevati
- **UX**: Messaggi di errore non verificati
- **Robustness**: Edge cases falliscono in produzione

---

## ✅ Solution

### Categories di Negative Tests

1. **Validation Errors** (5 tests)
   - Invalid email format
   - Password troppo corta
   - Required fields mancanti
   - Invalid file types
   - File size limits

2. **Business Rule Violations** (5 tests)
   - Duplicate upload
   - Invalid game selection
   - Chat message troppo lungo
   - Rate limit exceeded

3. **Edge Cases** (5 tests)
   - Empty input submission
   - Special characters in input
   - Very long text (1000+ chars)
   - Rapid consecutive actions
   - Corrupted file upload

### Example Tests

```typescript
test.describe('Negative Scenarios', () => {
  test('rejects non-PDF file upload', async ({ page }) => {
    const uploadPage = new UploadPage(page);
    await uploadPage.goto();

    await uploadPage.selectFile('test.txt'); // Not a PDF
    await uploadPage.clickUpload();

    await expect(page.getByRole('alert')).toContainText(/only PDF files/i);
  });

  test('shows validation for invalid email', async ({ page }) => {
    const authPage = new AuthPage(page);
    await authPage.goto();

    await authPage.fillEmail('invalid-email'); // Missing @
    await authPage.clickLogin();

    await expect(page.getByText(/valid email/i)).toBeVisible();
  });

  test('handles network timeout gracefully', async ({ page, context }) => {
    await context.route('**/api/v1/chat', async route => {
      await page.waitForTimeout(35000); // Exceed timeout
      await route.fulfill({ status: 408 });
    });

    const chatPage = new ChatPage(page);
    await chatPage.askQuestion('Test');

    await expect(page.getByRole('alert')).toContainText(/timeout|riprova/i);
  });
});
```

---

## 📝 Checklist

- [ ] Add 5 validation error tests
- [ ] Add 5 business rule violation tests
- [ ] Add 5 edge case tests
- [ ] Verify error messages user-friendly
- [ ] Verify recovery flows (retry, back)

---

**Target**: 15+ negative test scenarios

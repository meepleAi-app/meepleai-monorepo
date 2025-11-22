# Add RBAC Authorization Tests

**Issue ID**: E2E-004
**Data**: 2025-11-20
**Priorità**: 🔴 ALTA
**Categoria**: Security & Test Completeness
**Effort Stimato**: 10-14 ore
**Status**: 🔴 Not Started

---

## 📋 Problem Description

La suite E2E **non verifica authorization/RBAC** (Role-Based Access Control). Test attuali verificano solo che utenti autenticati possano accedere a features, ma **non testano che ruoli diversi abbiano accessi diversi**.

**Gap Critici**:
- ❌ Nessun test verifica che `User` non possa accedere `/admin`
- ❌ Nessun test verifica che `Editor` non possa gestire utenti
- ❌ Nessun test verifica 403 Forbidden responses
- ❌ Nessun test per privilege escalation prevention

---

## 🎯 Impact & Risks

### Impatto
- **Security**: Authorization bugs potrebbero raggiungere produzione
- **Compliance**: Violazione principio least privilege
- **Test Coverage**: Gap del ~10% in security testing

### Rischi
🔴 **CRITICO**: User potrebbe accedere a funzioni admin (security breach)
🔴 **CRITICO**: Privilege escalation non rilevato
🟡 **Alto**: Compliance issues (GDPR, data access)

---

## 📊 Current Situation

**Test RBAC Esistenti**: 0
**Ruoli da Testare**: Admin, Editor, User
**Endpoint Critici Protetti**: ~15

```typescript
// ❌ Test attuale: Solo verifica auth, NON authorization
test('admin can access dashboard', async ({ adminPage }) => {
  await adminPage.goto('/admin');
  await expect(adminPage.getByRole('heading', { name: /Admin/i })).toBeVisible();
  // Ma non verifica che User NON possa accedere
});
```

---

## ✅ Implementation Recommendations

### Matrix di Test RBAC

| Route | Admin | Editor | User | Expected |
|-------|-------|--------|------|----------|
| `/chat` | ✅ | ✅ | ✅ | All allowed |
| `/games` | ✅ | ✅ | ✅ | All allowed |
| `/upload` | ✅ | ✅ | ❌ | Editor+ only |
| `/editor` | ✅ | ✅ | ❌ | Editor+ only |
| `/admin` | ✅ | ❌ | ❌ | Admin only |
| `/admin/users` | ✅ | ❌ | ❌ | Admin only |
| `/admin/config` | ✅ | ❌ | ❌ | Admin only |

### Test Template

```typescript
test.describe('RBAC Authorization Tests', () => {
  test.describe('Admin-only Routes', () => {
    test('Admin can access /admin dashboard', async ({ page }) => {
      await setupMockAuth(page, { role: 'Admin' });
      await page.goto('/admin');
      await expect(page).toHaveURL('/admin');
      await expect(page.getByRole('heading', { name: /admin/i })).toBeVisible();
    });

    test('Editor is forbidden from /admin', async ({ page }) => {
      await setupMockAuth(page, { role: 'Editor' });
      await page.goto('/admin');

      // Should redirect to forbidden or home
      await expect(page).not.toHaveURL('/admin');

      // Should show 403 or redirect
      const isForbidden = await page.locator('text=/forbidden|403/i').isVisible().catch(() => false);
      const isRedirected = page.url().includes('/') && !page.url().includes('/admin');
      expect(isForbidden || isRedirected).toBe(true);
    });

    test('User is forbidden from /admin', async ({ page }) => {
      await setupMockAuth(page, { role: 'User' });
      await page.goto('/admin');
      await expect(page).not.toHaveURL('/admin');
    });
  });

  test.describe('Editor+ Routes', () => {
    test('Editor can access /upload', async ({ page }) => {
      await setupMockAuth(page, { role: 'Editor' });
      await page.goto('/upload');
      await expect(page).toHaveURL('/upload');
    });

    test('User is forbidden from /upload', async ({ page }) => {
      await setupMockAuth(page, { role: 'User' });
      await page.goto('/upload');
      await expect(page).not.toHaveURL('/upload');
    });
  });

  test.describe('Public Routes', () => {
    ['/', '/chess', '/games'].forEach(route => {
      test(`All roles can access ${route}`, async ({ page }) => {
        for (const role of ['Admin', 'Editor', 'User'] as const) {
          await setupMockAuth(page, { role });
          await page.goto(route);
          await expect(page).toHaveURL(route);
        }
      });
    });
  });

  test.describe('API Authorization', () => {
    test('User cannot call admin API endpoints', async ({ page }) => {
      await setupMockAuth(page, { role: 'User' });

      const response = await page.request.get('http://localhost:5080/api/v1/admin/users');
      expect([401, 403]).toContain(response.status());
    });

    test('Editor cannot modify system config', async ({ page }) => {
      await setupMockAuth(page, { role: 'Editor' });

      const response = await page.request.post('http://localhost:5080/api/v1/admin/configuration', {
        data: { key: 'test', value: 'value' }
      });
      expect([401, 403]).toContain(response.status());
    });
  });
});
```

---

## 📝 Implementation Checklist

### Phase 1: Define RBAC Matrix (Day 1)
- [ ] Document all protected routes
- [ ] Document all protected API endpoints
- [ ] Define role permissions matrix
- [ ] Review with product team

### Phase 2: Implement Route Tests (Days 2-3)
- [ ] Test Admin-only routes (5 tests)
- [ ] Test Editor+ routes (3 tests)
- [ ] Test public routes (3 tests)
- [ ] Test unauthenticated access (5 tests)

### Phase 3: Implement API Tests (Day 4)
- [ ] Test Admin API endpoints (3 tests)
- [ ] Test Editor API endpoints (2 tests)
- [ ] Test User API endpoints (2 tests)

### Phase 4: Edge Cases (Day 5)
- [ ] Test session expiry during admin action
- [ ] Test role change mid-session
- [ ] Test privilege escalation attempts
- [ ] Test API key authorization

---

## ✅ Acceptance Criteria

### Must Have
- [ ] 15+ RBAC tests implementati
- [ ] Tutti i ruoli testati (Admin, Editor, User)
- [ ] Tutti i protected routes verificati
- [ ] 403 Forbidden verificato per accessi non autorizzati

### Should Have
- [ ] API authorization tests
- [ ] Privilege escalation tests
- [ ] Session-based authorization tests

---

## 📁 Files

### Create
- `apps/web/e2e/rbac-authorization.spec.ts` (new, main file)
- `apps/web/e2e/api/authorization.api.spec.ts` (new, API tests)

### Reference
- Existing auth middleware: `apps/api/src/Api/Middleware/AuthenticationMiddleware.cs`
- Existing RBAC logic: `apps/api/src/Api/BoundedContexts/Authentication/`

---

**Created**: 2025-11-20
**Owner**: QA Team + Security Team

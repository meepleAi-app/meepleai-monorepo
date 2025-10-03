import { test, expect } from '@playwright/test';

test.describe('Home Page', () => {
  test('should load home page successfully', async ({ page }) => {
    await page.goto('/');

    // Check main heading
    await expect(page.locator('h1')).toContainText('MeepleAI');

    // Check navigation links
    await expect(page.getByRole('link', { name: 'Chat' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Upload PDF' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'View Logs' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Editor RuleSpec' })).toBeVisible();
  });

  test('should display registration form', async ({ page }) => {
    await page.goto('/');

    // Check registration form elements
    await expect(page.getByRole('heading', { name: 'Registrazione' })).toBeVisible();
    await expect(page.getByText('Tenant ID')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Crea account' })).toBeVisible();
  });

  test('should display login form', async ({ page }) => {
    await page.goto('/');

    // Check login form elements
    await expect(page.getByRole('heading', { name: 'Accesso' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Entra' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Esci' })).toBeVisible();
  });

  test('should display QA demo section', async ({ page }) => {
    await page.goto('/');

    await expect(page.getByRole('heading', { name: 'Agente QA Demo' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Ping Web' })).toBeVisible();
  });
});

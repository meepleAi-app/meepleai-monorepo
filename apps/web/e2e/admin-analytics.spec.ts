import { test, expect } from "@playwright/test";

test.describe("Analytics Dashboard E2E", () => {
  test.beforeEach(async ({ page }) => {
    // Login as admin
    await page.goto("http://localhost:3000");
    await page.getByRole("button", { name: /login/i }).click();
    await page.fill('input[type="email"]', "admin@meepleai.dev");
    await page.fill('input[type="password"]', "Demo123!");
    await page.getByRole("button", { name: /sign in/i }).click();
    await page.waitForURL("http://localhost:3000/");
  });

  test("should display analytics dashboard with metrics", async ({ page }) => {
    // Navigate to analytics
    await page.goto("http://localhost:3000/admin/analytics");

    // Wait for page to load
    await expect(page.getByText("Analytics Dashboard")).toBeVisible();

    // Check metric cards are visible
    await expect(page.getByText("Total Users")).toBeVisible();
    await expect(page.getByText("Active Sessions")).toBeVisible();
    await expect(page.getByText("API Requests Today")).toBeVisible();
    await expect(page.getByText("Total PDF Documents")).toBeVisible();
    await expect(page.getByText("Total Chat Messages")).toBeVisible();
    await expect(page.getByText("Avg Confidence Score")).toBeVisible();
    await expect(page.getByText("Total RAG Requests")).toBeVisible();
    await expect(page.getByText("Total Tokens Used")).toBeVisible();
  });

  test("should display charts", async ({ page }) => {
    await page.goto("http://localhost:3000/admin/analytics");

    // Wait for dashboard to load
    await expect(page.getByText("Analytics Dashboard")).toBeVisible();

    // Check chart titles are visible
    await expect(page.getByText("User Registrations")).toBeVisible();
    await expect(page.getByText("Session Creations")).toBeVisible();
    await expect(page.getByText("API Requests")).toBeVisible();
    await expect(page.getByText("PDF Uploads")).toBeVisible();
    await expect(page.getByText("Chat Messages")).toBeVisible();
  });

  test("should allow changing time period filter", async ({ page }) => {
    await page.goto("http://localhost:3000/admin/analytics");

    // Wait for initial load
    await expect(page.getByText("Analytics Dashboard")).toBeVisible();

    // Change to 7 days
    await page.selectOption('select', '7');

    // Wait for data to refresh (check that page doesn't error)
    await page.waitForTimeout(1000);
    await expect(page.getByText("Analytics Dashboard")).toBeVisible();
  });

  test("should toggle auto-refresh", async ({ page }) => {
    await page.goto("http://localhost:3000/admin/analytics");

    // Wait for dashboard
    await expect(page.getByText("Analytics Dashboard")).toBeVisible();

    // Auto-refresh should be ON by default
    await expect(page.getByRole("button", { name: /Auto-refresh ON/i })).toBeVisible();

    // Toggle off
    await page.getByRole("button", { name: /Auto-refresh ON/i }).click();
    await expect(page.getByRole("button", { name: /Auto-refresh OFF/i })).toBeVisible();

    // Toggle back on
    await page.getByRole("button", { name: /Auto-refresh OFF/i }).click();
    await expect(page.getByRole("button", { name: /Auto-refresh ON/i })).toBeVisible();
  });

  test("should refresh data when refresh button clicked", async ({ page }) => {
    await page.goto("http://localhost:3000/admin/analytics");

    // Wait for initial load
    await expect(page.getByText("Analytics Dashboard")).toBeVisible();

    // Get initial last updated time
    const initialUpdateText = await page.getByText(/Last updated:/).textContent();

    // Wait a moment
    await page.waitForTimeout(1000);

    // Click refresh
    await page.getByRole("button", { name: /Refresh/i }).click();

    // Wait for refresh to complete
    await page.waitForTimeout(1000);

    // Check that last updated time has changed
    const newUpdateText = await page.getByText(/Last updated:/).textContent();
    expect(newUpdateText).not.toBe(initialUpdateText);
  });

  test("should export CSV", async ({ page }) => {
    await page.goto("http://localhost:3000/admin/analytics");

    // Wait for dashboard
    await expect(page.getByText("Analytics Dashboard")).toBeVisible();

    // Click export CSV button
    const downloadPromise = page.waitForEvent("download");
    await page.getByRole("button", { name: /Export CSV/i }).click();

    // Wait for download
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/analytics-.+\.csv/);

    // Check for success toast
    await expect(page.getByText("Analytics exported as CSV")).toBeVisible();
  });

  test("should export JSON", async ({ page }) => {
    await page.goto("http://localhost:3000/admin/analytics");

    // Wait for dashboard
    await expect(page.getByText("Analytics Dashboard")).toBeVisible();

    // Click export JSON button
    const downloadPromise = page.waitForEvent("download");
    await page.getByRole("button", { name: /Export JSON/i }).click();

    // Wait for download
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/analytics-.+\.json/);

    // Check for success toast
    await expect(page.getByText("Analytics exported as JSON")).toBeVisible();
  });

  test("should have back to users link", async ({ page }) => {
    await page.goto("http://localhost:3000/admin/analytics");

    // Check back link exists
    const backLink = page.getByRole("link", { name: /Back to Users/i });
    await expect(backLink).toBeVisible();

    // Click and verify navigation
    await backLink.click();
    await expect(page).toHaveURL("http://localhost:3000/admin/users");
  });
});

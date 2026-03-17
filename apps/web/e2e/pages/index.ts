/**
 * Page Objects and Helpers Index - CONSOLIDATED
 *
 * Central export point for all page objects, helpers, and base classes.
 * Import from this file to use page objects in tests.
 *
 * @example
 * ```typescript
 * import { LoginPage, AuthHelper, USER_FIXTURES } from './pages';
 *
 * test('login flow', async ({ page }) => {
 *   const loginPage = new LoginPage(page);
 *   const authHelper = new AuthHelper(page);
 *
 *   await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);
 *   await loginPage.navigate();
 * });
 * ```
 */

// Base classes
export { BasePage } from './base/BasePage';

// Helpers (NEW - Comprehensive mock utilities)
export { AuthHelper, USER_FIXTURES, type UserFixture } from './helpers/AuthHelper';
export { AdminHelper } from './helpers/AdminHelper';
export { ChatHelper, type ChatMessage } from './helpers/ChatHelper';
export { GamesHelper, type Game } from './helpers/GamesHelper';

// Real Backend Helpers (Issue #3082 - Real backend integration)
export { RealBackendHelper, createRealBackendHelper } from './helpers/RealBackendHelper';

// Session Helpers (Game Night Improvvisata E2E)
export { SessionHelper } from './helpers/SessionHelper';
export {
  MOCK_SESSION_ID,
  MOCK_GAME_ID,
  MOCK_INVITE_CODE,
  MOCK_PRIVATE_GAME_ID,
  MOCK_LIVE_SESSION,
  MOCK_PLAYERS,
  MOCK_RESUME_CONTEXT,
  MOCK_BGG_SEARCH_RESULTS,
  MOCK_IMPORT_BGG_RESPONSE,
  MOCK_SCORE_PARSE_RECORDED,
  MOCK_SCORE_PARSE_NEEDS_CONFIRM,
  MOCK_SCORE_PARSE_AMBIGUOUS,
  MOCK_SCORE_PARSE_UNRECOGNIZED,
  MOCK_DISPUTE_RESPONSE,
  MOCK_NOTIFICATION_AGENT_READY,
} from './helpers/SessionHelper';

// Pages - Authentication
export { AuthPage } from './auth/AuthPage'; // Existing complex auth page
export { LoginPage } from './auth/LoginPage'; // NEW - Simple login interactions
export { RegisterPage } from './auth/RegisterPage'; // NEW - Registration flow
export { ProfilePage } from './auth/ProfilePage'; // NEW - Profile management

// Pages - Admin
export { AdminPage } from './admin/AdminPage';

// Pages - Chat/RAG
export { ChatPage } from './chat/ChatPage';

// Pages - Games
export { GamePage } from './game/GamePage';
export { SharedGameCatalogPage } from './game/SharedGameCatalogPage';

// Pages - Upload/PDF
export { UploadPage } from './upload/UploadPage';

// Pages - Home
export { HomePage } from './home/HomePage';

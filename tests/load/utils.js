import http from 'k6/http';
import { check } from 'k6';
import { config } from './config.js';

/**
 * Authenticate user and return session cookie
 * @param {string} email - User email
 * @param {string} password - User password
 * @returns {string|null} Session cookie or null if auth failed
 */
export function authenticate(email, password) {
  const loginUrl = `${config.baseUrl}/api/v1/auth/login`;
  const payload = JSON.stringify({ email, password });
  const params = {
    headers: {
      'Content-Type': 'application/json',
    },
  };

  const response = http.post(loginUrl, payload, params);

  const authSuccess = check(response, {
    'auth: status is 200': (r) => r.status === 200,
    'auth: has user data': (r) => r.json('user') !== undefined,
  });

  if (!authSuccess) {
    console.error(`Authentication failed: ${response.status} - ${response.body}`);
    return null;
  }

  // Extract session cookie from response
  const cookieJar = http.cookieJar();
  const cookies = cookieJar.cookiesForURL(response.url);

  // MeepleAI uses 'meeple_session' cookie (from AUTH-03 implementation)
  if (cookies.meeple_session && cookies.meeple_session.length > 0) {
    return cookies.meeple_session[0];
  }

  console.error('No session cookie found in auth response');
  return null;
}

/**
 * Get authentication headers with session cookie
 * @param {string} sessionCookie - Session cookie value
 * @returns {object} Headers object with cookie
 */
export function getAuthHeaders(sessionCookie) {
  return {
    'Cookie': `meeple_session=${sessionCookie}`,
    'Content-Type': 'application/json',
  };
}

/**
 * Fetch available games and return game IDs
 * @returns {object} Object with game IDs
 */
export function fetchGameIds() {
  const gamesUrl = `${config.baseUrl}/api/v1/games`;
  const response = http.get(gamesUrl);

  const success = check(response, {
    'fetch games: status is 200': (r) => r.status === 200,
  });

  if (!success) {
    console.error(`Failed to fetch games: ${response.status}`);
    return {};
  }

  const games = response.json();
  const gameIds = {};

  // Find chess and tic-tac-toe game IDs
  games.forEach(game => {
    if (game.name && game.name.toLowerCase().includes('chess')) {
      gameIds.chess = game.id;
    }
    if (game.name && game.name.toLowerCase().includes('tic-tac-toe')) {
      gameIds.ticTacToe = game.id;
    }
  });

  return gameIds;
}

/**
 * Get random item from array
 * @param {Array} array - Array to pick from
 * @returns {*} Random item
 */
export function randomItem(array) {
  return array[Math.floor(Math.random() * array.length)];
}

/**
 * Create a standardized check for HTTP responses
 * @param {object} response - HTTP response object
 * @param {string} checkName - Name for the check
 * @param {number} expectedStatus - Expected HTTP status code
 * @returns {boolean} Check result
 */
export function checkResponse(response, checkName, expectedStatus = 200) {
  return check(response, {
    [`${checkName}: status is ${expectedStatus}`]: (r) => r.status === expectedStatus,
    [`${checkName}: response time < 5s`]: (r) => r.timings.duration < 5000,
  });
}

/**
 * Setup function to run once per VU at the start
 * Authenticates and fetches game IDs
 * @returns {object} Setup data with session and game IDs
 */
export function setupAuth() {
  const sessionCookie = authenticate(config.testUser.email, config.testUser.password);
  const gameIds = fetchGameIds();

  return {
    sessionCookie,
    gameIds,
  };
}

/**
 * Sleep for a random duration between min and max seconds
 * @param {number} min - Minimum seconds
 * @param {number} max - Maximum seconds
 */
export function randomSleep(min, max) {
  const duration = min + Math.random() * (max - min);
  return duration;
}

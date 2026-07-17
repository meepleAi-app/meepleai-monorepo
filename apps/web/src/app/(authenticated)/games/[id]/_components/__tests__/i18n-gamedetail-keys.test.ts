/**
 * GameDetail i18n key validation (#3103)
 *
 * Ensures the `pages.gameDetail.{stats,chat,houseRules}` subtrees exist in both
 * IT and EN locale files. These are consumed by GameDetailView (Stats tab, chat
 * preview, House Rules panel); when absent, react-intl renders the raw dot-path
 * id to every user.
 */

import enMessages from '@/locales/en.json';
import itMessages from '@/locales/it.json';

const STATS_KEYS = [
  'winRate',
  'timesPlayed',
  'lastPlayed',
  'lastPlayedNever',
  'lastPlayedDaysAgoUnit',
  'leaderboardTitle',
  'leaderboardPlays',
  'leaderboardAvg',
  'leaderboardWins',
  'leaderboardEmpty',
  'communityGateTitle',
  'communityGateDescription',
  'communityGateCta',
];

const CHAT_KEYS = ['title', 'empty', 'openCta', 'userPrefix', 'assistantPrefix'];

const HOUSE_RULES_KEYS = [
  'title',
  'addCta',
  'addPlaceholder',
  'addSubmit',
  'editLabel',
  'editSubmit',
  'cancel',
  'deleteLabel',
  'deleteConfirmTitle',
  'deleteConfirmMessage',
  'deleteConfirm',
  'empty',
];

type Catalog = Record<string, unknown> & {
  pages: { gameDetail: Record<string, Record<string, string>> };
};

const itGameDetail = (itMessages as Catalog).pages.gameDetail;
const enGameDetail = (enMessages as Catalog).pages.gameDetail;

describe('GameDetail i18n key validation (#3103)', () => {
  describe.each([
    ['stats', STATS_KEYS],
    ['chat', CHAT_KEYS],
    ['houseRules', HOUSE_RULES_KEYS],
  ] as const)('pages.gameDetail.%s subtree', (subtree, keys) => {
    it.each(keys)(`IT: pages.gameDetail.${subtree}.%s exists and is non-empty`, key => {
      expect(itGameDetail[subtree]?.[key]).toBeTruthy();
    });

    it.each(keys)(`EN: pages.gameDetail.${subtree}.%s exists and is non-empty`, key => {
      expect(enGameDetail[subtree]?.[key]).toBeTruthy();
    });
  });

  it('IT and EN have the same keys for stats/chat/houseRules (parity)', () => {
    for (const subtree of ['stats', 'chat', 'houseRules'] as const) {
      const itKeys = Object.keys(itGameDetail[subtree] ?? {}).sort();
      const enKeys = Object.keys(enGameDetail[subtree] ?? {}).sort();
      expect(itKeys).toEqual(enKeys);
    }
  });
});

import { z } from 'zod';

/**
 * Game Contributors API Schemas
 * Issue #2746: Frontend - Contributor Display su SharedGame
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 */

// Badge Tier Enum
export const BadgeTierSchema = z.enum(['Bronze', 'Silver', 'Gold', 'Platinum', 'Diamond']);

export type BadgeTier = z.infer<typeof BadgeTierSchema>;

// Badge Summary DTO (for display in contributor cards)
/**
 * #3853 — allineato a `BadgeSummaryDto`
 * (SharedGameCatalog/Application/DTOs/BadgeSummaryDto.cs), proiettato da
 * `GetGameContributorsQueryHandler` come `Code · Name · IconUrl · Tier`.
 *
 * Lo schema pretendeva un `id` uuid che il backend non manda, e ignorava `code` —
 * che e' l'identificatore naturale del badge ("TOP_CONTRIBUTOR"). Ogni contributore
 * con almeno un badge faceva fallire la validazione dell'intera risposta; con
 * `topBadges: []` non si vedeva.
 */
export const BadgeSummaryDtoSchema = z.object({
  code: z.string(),
  name: z.string(),
  tier: BadgeTierSchema,
  iconUrl: z.string().nullable(),
});

export type BadgeSummaryDto = z.infer<typeof BadgeSummaryDtoSchema>;

// Game Contributor DTO
export const GameContributorDtoSchema = z.object({
  userId: z.string().uuid(),
  userName: z.string(),
  avatarUrl: z.string().nullable(),
  isPrimaryContributor: z.boolean(),
  contributionCount: z.number().int().nonnegative(),
  firstContributionAt: z.string().datetime({ offset: true }),
  topBadges: z.array(BadgeSummaryDtoSchema).max(3), // Max 3 badges displayed
});

export type GameContributorDto = z.infer<typeof GameContributorDtoSchema>;

// Game Contributors Response
export const GameContributorsResponseSchema = z.object({
  contributors: z.array(GameContributorDtoSchema),
});

export type GameContributorsResponse = z.infer<typeof GameContributorsResponseSchema>;

/**
 * BoardGameGeek API Types - Issue #4141
 *
 * Type definitions for BGG integration in PDF wizard.
 */

export interface BggSearchResult {
  id: number;
  name: string;
  yearPublished: number;
  thumbnail: string | null;
}

export interface BggGameDetailsDto {
  id: number;
  name: string;
  yearPublished: number;
  minPlayers: number;
  maxPlayers: number;
  playingTime: number;
  minAge: number;
  rating: number;
  thumbnail: string | null;
  description?: string;
  categories?: string[];
  mechanics?: string[];
}

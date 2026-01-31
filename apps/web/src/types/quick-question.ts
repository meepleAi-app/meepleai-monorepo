export enum QuestionCategory {
  Gameplay = 0,
  Rules = 1,
  Winning = 2,
  Setup = 3,
  Strategy = 4,
  Clarifications = 5,
}

export interface QuickQuestion {
  id: string;
  sharedGameId: string;
  text: string;
  emoji: string;
  category: QuestionCategory;
  displayOrder: number;
  isGenerated: boolean;
  createdAt: string;
  isActive: boolean;
}

export interface GenerateQuickQuestionsResult {
  questions: QuickQuestion[];
  confidenceScore: number;
  generatedAt: string;
}

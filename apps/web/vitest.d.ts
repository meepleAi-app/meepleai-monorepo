import '@testing-library/jest-dom/vitest';
/// <reference types="vitest/globals" />

// Re-export Mock and Mocked types globally for test files
declare global {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  type Mock<T = any> = import('vitest').Mock<T>;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  type Mocked<T = any> = import('vitest').MockedObject<T>;
}

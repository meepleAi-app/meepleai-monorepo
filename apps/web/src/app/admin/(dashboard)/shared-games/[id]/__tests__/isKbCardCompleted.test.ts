import { describe, it, expect } from 'vitest';

import { isKbCardCompleted } from '../client';

// #5104: the values GET /admin/shared-games/{id}/kb-cards really sends (vector_documents.IndexingStatus).
describe('isKbCardCompleted', () => {
  it('riconosce lo stato completato come lo scrive il backend', () => {
    expect(isKbCardCompleted('completed')).toBe(true);
  });

  it('non distingue maiuscole e minuscole', () => {
    expect(isKbCardCompleted('Completed')).toBe(true);
  });

  it.each(['pending', 'processing', 'failed', '', null, undefined])(
    '%s non è completato',
    status => {
      expect(isKbCardCompleted(status)).toBe(false);
    }
  );
});

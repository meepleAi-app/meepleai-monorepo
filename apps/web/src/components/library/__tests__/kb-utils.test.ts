import { describe, expect, it } from 'vitest';

import { getStatusIndicator } from '../kb-utils';

describe('getStatusIndicator', () => {
  // #4356: la lista dei PDF di un gioco ora arriva, con gli stati veri del backend. Le fasi
  // intermedie non avevano una voce e finivano su «In attesa», come un PDF mai partito.
  it.each(['Uploading', 'Extracting', 'Chunking', 'Embedding', 'Indexing'])(
    'shows %s as in progress',
    state => {
      expect(getStatusIndicator(state).label).toBe('Elaborazione...');
    }
  );

  it('still shows a pending document as waiting', () => {
    expect(getStatusIndicator('Pending').label).toBe('In attesa');
  });
});

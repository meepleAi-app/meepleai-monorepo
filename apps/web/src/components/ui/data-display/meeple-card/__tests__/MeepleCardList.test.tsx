import { render } from '@testing-library/react';

import { MeepleCardList } from '../variants/MeepleCardList';

describe('MeepleCardList — thumbnail', () => {
  it('thumbnail has fixed dimensions 80x112', () => {
    render(<MeepleCardList entity="game" title="Wingspan" />);
    const thumb = document.querySelector('[data-card-thumbnail]');
    expect(thumb).toHaveStyle({ width: '80px', height: '112px' });
  });
});

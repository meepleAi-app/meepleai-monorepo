import { renderHook } from '@testing-library/react';
import { useEntityDetail } from '../use-entity-detail';

describe('useEntityDetail', () => {
  it('returns null data for unimplemented entity types', () => {
    const { result } = renderHook(() => useEntityDetail('collection', 'test-id'));
    expect(result.current.data).toBeNull();
    expect(result.current.loading).toBe(false);
    expect(result.current.error).toBeNull();
  });

  it('returns null data for group entity type', () => {
    const { result } = renderHook(() => useEntityDetail('group', 'test-id'));
    expect(result.current.data).toBeNull();
    expect(result.current.loading).toBe(false);
  });

  it('returns null data for location entity type', () => {
    const { result } = renderHook(() => useEntityDetail('location', 'test-id'));
    expect(result.current.data).toBeNull();
  });

  it('returns null data for all new entity types', () => {
    const newTypes = [
      'collection',
      'group',
      'location',
      'expansion',
      'achievement',
      'note',
    ] as const;
    newTypes.forEach(type => {
      const { result } = renderHook(() => useEntityDetail(type, 'test-id'));
      expect(result.current.data).toBeNull();
      expect(result.current.loading).toBe(false);
      expect(result.current.error).toBeNull();
    });
  });
});

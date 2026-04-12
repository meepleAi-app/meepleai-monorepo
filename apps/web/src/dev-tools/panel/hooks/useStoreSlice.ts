import { useEffect, useState } from 'react';

import type { StoreApi } from 'zustand/vanilla';

export function useStoreSlice<T, U>(store: StoreApi<T>, selector: (state: T) => U): U {
  const [slice, setSlice] = useState<U>(() => selector(store.getState()));
  useEffect(() => {
    return store.subscribe(state => {
      setSlice(selector(state));
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [store]);
  return slice;
}

import { create } from 'zustand';

interface NavbarHeightState {
  /** Current navbar height in pixels (52 without tabs, 88 with tabs) */
  height: number;
  setHeight: (height: number) => void;
}

export const useNavbarHeightStore = create<NavbarHeightState>(set => ({
  height: 52,
  setHeight: (height: number) => set({ height }),
}));

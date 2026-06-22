/**
 * Calendar grid builder for /game-nights index v2 (Stage 3, refs #1170).
 *
 * Pure helper: builds a 42-cell (6×7) Monday-first month grid. Cells outside
 * the current month are flagged so the renderer can dim them and the click
 * handler can route to the adjacent month.
 */

export interface MonthCell {
  readonly day: number; // 1..31
  readonly otherMonth: boolean;
  readonly monthOffset: -1 | 0 | 1; // -1=prev, 0=current, +1=next
}

/**
 * Build 42-cell Monday-first month grid.
 *
 * @param year  4-digit year
 * @param month 0-indexed month (0=Jan ... 11=Dec) — matches the JS `Date` API
 */
export function buildMonthGrid(year: number, month: number): MonthCell[] {
  const firstOfMonth = new Date(year, month, 1);
  // JS getDay: 0=Sun..6=Sat. Convert to ISO Monday-first offset (0=Mon..6=Sun).
  const offset = (firstOfMonth.getDay() + 6) % 7;

  const daysInCurrent = new Date(year, month + 1, 0).getDate();
  const daysInPrev = new Date(year, month, 0).getDate();

  const cells: MonthCell[] = [];

  // Prev-month tail.
  for (let i = 0; i < offset; i++) {
    const day = daysInPrev - offset + 1 + i;
    cells.push({ day, otherMonth: true, monthOffset: -1 });
  }

  // Current month.
  for (let day = 1; day <= daysInCurrent; day++) {
    cells.push({ day, otherMonth: false, monthOffset: 0 });
  }

  // Next-month padding.
  let nextDay = 1;
  while (cells.length < 42) {
    cells.push({ day: nextDay, otherMonth: true, monthOffset: 1 });
    nextDay++;
  }

  return cells;
}

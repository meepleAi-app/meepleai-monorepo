/* eslint-disable local/no-hardcoded-color-utility -- text-white on the entity-tinted gradient avatar; mockup .user-avatar pattern. */
import React from 'react';

export interface UserCellUser {
  displayName: string | null;
  email: string;
  role?: string;
}

export interface UserCellProps {
  user: UserCellUser;
}

/**
 * Compact user cell for admin tables: gradient avatar (player→chat tokens) +
 * display name + truncated email. Mirrors the `.user-cell` pattern from the
 * SP5 A2 mockup. Presentational only — receives a plain user-shaped object.
 */
export function UserCell({ user }: UserCellProps) {
  const initial = (user.displayName?.charAt(0) ?? user.email.charAt(0) ?? '?').toUpperCase();

  return (
    <div className="flex items-center gap-2 min-w-0" data-testid="user-cell">
      <span
        data-testid="user-cell-avatar"
        className="shrink-0 grid place-items-center w-7 h-7 rounded-full text-white text-[11px] font-bold"
        style={{
          background: 'linear-gradient(135deg, hsl(var(--c-player)), hsl(var(--c-chat)))',
        }}
        aria-hidden="true"
      >
        {initial}
      </span>
      <span className="flex flex-col min-w-0">
        <span className="font-quicksand text-[12.5px] font-bold leading-tight truncate">
          {user.displayName ?? user.email}
        </span>
        {user.displayName && (
          <span className="font-mono text-[10.5px] text-muted-foreground truncate">
            {user.email}
          </span>
        )}
      </span>
    </div>
  );
}

export default UserCell;

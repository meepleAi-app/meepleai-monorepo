'use client';

import { memo } from 'react';

interface Member {
  name: string;
  role?: string;
  avatarUrl?: string;
}

interface MembersBlockProps {
  title: string;
  entityColor: string;
  data: {
    type: 'members';
    members: Member[];
  };
}

function getInitials(name: string): string {
  return name
    .split(' ')
    .map(part => part[0])
    .join('')
    .toUpperCase()
    .slice(0, 2);
}

export const MembersBlock = memo(function MembersBlock({
  title,
  entityColor,
  data,
}: MembersBlockProps) {
  const { members } = data;

  return (
    <div className="flex flex-col gap-2">
      {title && (
        <>
          <h4
            className="text-xs font-semibold uppercase tracking-wide"
            style={{ color: `hsl(${entityColor})` }}
          >
            {title}
          </h4>
          <div className="h-px w-full" style={{ backgroundColor: `hsl(${entityColor} / 0.2)` }} />
        </>
      )}

      {members.length === 0 ? (
        <p className="text-xs text-muted-foreground italic">No data yet</p>
      ) : (
        <ul className="flex flex-col gap-1.5">
          {members.map((member, idx) => (
            <li key={idx} className="flex items-center gap-2 text-xs">
              {member.avatarUrl ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={member.avatarUrl}
                  alt={member.name}
                  className="h-6 w-6 shrink-0 rounded-full object-cover"
                />
              ) : (
                <span
                  className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-[10px] font-bold text-white"
                  style={{ backgroundColor: `hsl(${entityColor})` }}
                  aria-hidden="true"
                >
                  {getInitials(member.name)}
                </span>
              )}
              <span className="flex-1 truncate font-medium text-foreground">{member.name}</span>
              {member.role && (
                <span className="shrink-0 text-[10px] text-muted-foreground">{member.role}</span>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
});

MembersBlock.displayName = 'MembersBlock';

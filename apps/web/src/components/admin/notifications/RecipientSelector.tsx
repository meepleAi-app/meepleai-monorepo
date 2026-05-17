/* eslint-disable local/no-hardcoded-color-utility -- admin tools chrome: text-white / button color on style-prop colored bg or admin-decorative inline gradient. DS-13d admin scope (--admin-* decision deferred to DS-15). */
'use client';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';

export interface RecipientSelection {
  type: 'all' | 'role' | 'users';
  role?: string;
  userIds?: string[];
}

export interface RecipientSelectorProps {
  value: RecipientSelection;
  onChange: (value: RecipientSelection) => void;
}

const ROLES = ['admin', 'editor', 'user'] as const;

export function RecipientSelector({ value, onChange }: RecipientSelectorProps) {
  return (
    <div className="space-y-3">
      <div>
        <Label className="text-sm font-medium">Recipients</Label>
        <Select
          value={value.type}
          onValueChange={(type: string) => onChange({ type: type as RecipientSelection['type'] })}
        >
          <SelectTrigger className="w-full mt-1.5 bg-card/70 dark:bg-zinc-800/70">
            <SelectValue placeholder="Select recipients" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Users</SelectItem>
            <SelectItem value="role">By Role</SelectItem>
            <SelectItem value="users">Specific Users</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {value.type === 'role' && (
        <div>
          <Label className="text-sm font-medium">Role</Label>
          <Select
            value={value.role ?? ''}
            onValueChange={(role: string) => onChange({ ...value, role })}
          >
            <SelectTrigger className="w-full mt-1.5 bg-card/70 dark:bg-zinc-800/70">
              <SelectValue placeholder="Select role" />
            </SelectTrigger>
            <SelectContent>
              {ROLES.map(r => (
                <SelectItem key={r} value={r}>
                  {r.charAt(0).toUpperCase() + r.slice(1)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      )}

      {value.type === 'users' && (
        <div>
          <Label className="text-sm font-medium">User IDs (comma-separated)</Label>
          <Input
            className="mt-1.5 bg-card/70 dark:bg-zinc-800/70"
            placeholder="uuid1, uuid2, ..."
            value={(value.userIds ?? []).join(', ')}
            onChange={e => {
              const ids = e.target.value
                .split(',')
                .map(s => s.trim())
                .filter(Boolean);
              onChange({ ...value, userIds: ids });
            }}
          />
        </div>
      )}
    </div>
  );
}

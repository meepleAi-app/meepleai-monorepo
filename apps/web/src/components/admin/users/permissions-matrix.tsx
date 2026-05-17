/* eslint-disable local/no-hardcoded-color-utility -- admin CRUD chrome: text-white / button color on style-prop colored bg or admin-decorative inline gradient. DS-13c admin scope (--admin-* decision deferred to DS-15). */
'use client';

import { CheckIcon, XIcon } from 'lucide-react';

interface Permission {
  name: string;
  description: string;
  admin: boolean;
  editor: boolean;
  user: boolean;
  anonymous: boolean;
}

const PERMISSIONS: Permission[] = [
  {
    name: 'Manage Users',
    description: 'Create, edit, delete user accounts',
    admin: true,
    editor: false,
    user: false,
    anonymous: false,
  },
  {
    name: 'Approve Games',
    description: 'Approve community game submissions',
    admin: true,
    editor: true,
    user: false,
    anonymous: false,
  },
  {
    name: 'Upload Documents',
    description: 'Upload and process game documents',
    admin: true,
    editor: true,
    user: true,
    anonymous: false,
  },
  {
    name: 'Configure AI Agents',
    description: 'Manage AI models and prompts',
    admin: true,
    editor: true,
    user: false,
    anonymous: false,
  },
  {
    name: 'Manage Categories',
    description: 'Create and organize game categories',
    admin: true,
    editor: true,
    user: false,
    anonymous: false,
  },
  {
    name: 'View Analytics',
    description: 'Access usage stats and reports',
    admin: true,
    editor: true,
    user: false,
    anonymous: false,
  },
  {
    name: 'View Public Catalog',
    description: 'Browse public game catalog',
    admin: true,
    editor: true,
    user: true,
    anonymous: true,
  },
];

function PermissionIcon({ hasPermission }: { hasPermission: boolean }) {
  if (hasPermission) {
    return <CheckIcon className="w-6 h-6 text-green-600 dark:text-green-400 mx-auto" />;
  }
  return <XIcon className="w-6 h-6 text-muted-foreground dark:text-muted-foreground mx-auto" />;
}

interface RoleHeaderProps {
  role: string;
  count: string;
  colorClass: string;
}

function RoleHeader({ role, count, colorClass }: RoleHeaderProps) {
  return (
    <th className={`text-center py-4 px-6 text-sm font-bold ${colorClass} uppercase tracking-wider`}>
      <div className="flex flex-col items-center gap-1">
        <span>{role}</span>
        <span className="text-xs font-normal text-muted-foreground dark:text-muted-foreground normal-case">
          ({count})
        </span>
      </div>
    </th>
  );
}

export function PermissionsMatrix() {
  return (
    <div className="bg-card/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-border/50 dark:border-zinc-700/50 shadow-sm overflow-hidden">
      <div className="p-6 border-b border-border dark:border-zinc-700">
        <h2 className="font-quicksand text-2xl font-bold text-foreground dark:text-zinc-100">
          Permissions Matrix
        </h2>
        <p className="text-muted-foreground dark:text-muted-foreground mt-1">
          Access control for system features and operations
        </p>
      </div>

      <div className="overflow-x-auto">
        <table className="w-full">
          <thead>
            <tr className="bg-muted/50 dark:bg-zinc-900/50 border-b border-border dark:border-zinc-700">
              <th className="text-left py-4 px-6 text-sm font-bold text-foreground dark:text-zinc-300 uppercase tracking-wider">
                Permission
              </th>
              <RoleHeader role="Admin" count="5" colorClass="text-amber-700 dark:text-amber-400" />
              <RoleHeader role="Editor" count="23" colorClass="text-blue-700 dark:text-blue-400" />
              <RoleHeader role="User" count="1,247" colorClass="text-green-700 dark:text-green-400" />
              <RoleHeader role="Anonymous" count="Public" colorClass="text-foreground dark:text-muted-foreground" />
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 dark:divide-zinc-700">
            {PERMISSIONS.map((permission) => (
              <tr
                key={permission.name}
                className="hover:bg-muted/50 dark:hover:bg-zinc-900/50 transition-colors"
              >
                <td className="py-4 px-6 text-foreground dark:text-zinc-100 font-medium">
                  <div className="flex items-center gap-3">
                    <div>
                      <div>{permission.name}</div>
                      <div className="text-xs text-muted-foreground dark:text-muted-foreground">
                        {permission.description}
                      </div>
                    </div>
                  </div>
                </td>
                <td className="text-center py-4 px-6">
                  <PermissionIcon hasPermission={permission.admin} />
                </td>
                <td className="text-center py-4 px-6">
                  <PermissionIcon hasPermission={permission.editor} />
                </td>
                <td className="text-center py-4 px-6">
                  <PermissionIcon hasPermission={permission.user} />
                </td>
                <td className="text-center py-4 px-6">
                  <PermissionIcon hasPermission={permission.anonymous} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

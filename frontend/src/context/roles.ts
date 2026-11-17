import type { UserRole } from './auth-context';

// Khớp claim `role` backend phát ra (Common/Roles.cs): user < janitor < moderator < super_admin.
export const USER_ROLES: UserRole[] = ['user', 'janitor', 'moderator', 'super_admin'];
export const ADMIN_ROLES: UserRole[] = ['janitor', 'moderator', 'super_admin'];
export const MODERATOR_UP: UserRole[] = ['moderator', 'super_admin'];
export const SUPER_ADMIN_ONLY: UserRole[] = ['super_admin'];

export const ROLE_LABELS: Record<string, string> = {
  janitor: 'Janitor',
  moderator: 'Moderator',
  super_admin: 'Super Admin',
};

export function isAdminRole(role?: UserRole | null): boolean {
  return !!role && ADMIN_ROLES.includes(role);
}

export function roleLabel(role?: string | null): string {
  return (role && ROLE_LABELS[role]) || role || '';
}

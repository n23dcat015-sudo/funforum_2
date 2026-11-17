import { createContext } from 'react';

export type UserRole = 'user' | 'janitor' | 'moderator' | 'super_admin';

export type SessionUser = {
  id: number;
  role: UserRole;
};

export type AuthContextValue = {
  user: SessionUser | null;
  token: string | null;
  ready: boolean;
  login: (email: string, password: string, admin?: boolean) => Promise<void>;
  loginWithGoogle: (idToken: string) => Promise<void>;
  register: (payload: { email: string; password: string }) => Promise<void>;
  requestPasswordReset: (email: string) => Promise<void>;
  resetPassword: (token: string, newPassword: string) => Promise<void>;
  logout: () => Promise<void>;
  refreshSession: () => Promise<void>;
};

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);
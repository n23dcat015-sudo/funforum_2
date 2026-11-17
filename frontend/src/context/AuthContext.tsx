import { useEffect, useMemo, useState } from 'react';
import { api, refreshAccessToken, setAccessToken } from '../lib/api';
import { AuthContext, type AuthContextValue, type SessionUser, type UserRole } from './auth-context';
import { USER_ROLES } from './roles';

type AuthResponse = { accessToken: string; expiresAt: string };

function userFromToken(token: string): SessionUser | null {
  try {
    const encodedClaims = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const claims = JSON.parse(atob(encodedClaims)) as { sub?: string; role?: UserRole };
    if (!claims.sub || !USER_ROLES.includes(claims.role as UserRole)) return null;
    return { id: Number(claims.sub), role: claims.role! };
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<SessionUser | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [ready, setReady] = useState(false);

  const acceptToken = (nextToken: string | null) => {
    const nextUser = nextToken ? userFromToken(nextToken) : null;
    setAccessToken(nextToken);
    setToken(nextToken);
    setUser(nextUser);
  };

  const refreshSession = async () => {
    try {
      const nextToken = await refreshAccessToken();
      if (!userFromToken(nextToken)) throw new Error('Invalid access token claims.');
      acceptToken(nextToken);
    } catch {
      acceptToken(null);
    } finally {
      setReady(true);
    }
  };

  useEffect(() => {
    const onRefreshed = (event: Event) => acceptToken((event as CustomEvent<string>).detail);
    const onExpired = () => acceptToken(null);
    window.addEventListener('funforum:session-refreshed', onRefreshed);
    window.addEventListener('funforum:session-expired', onExpired);
    queueMicrotask(() => void refreshSession());
    return () => {
      window.removeEventListener('funforum:session-refreshed', onRefreshed);
      window.removeEventListener('funforum:session-expired', onExpired);
    };
  }, []);

  const login = async (email: string, password: string, admin = false) => {
    // User đăng nhập bằng email; admin vẫn dùng username (AdminLoginRequest).
    const body = admin ? { username: email, password } : { email, password };
    const { data } = await api.post<AuthResponse>(admin ? '/auth/admin/login' : '/auth/login', body);
    if (!userFromToken(data.accessToken)) throw new Error('Login response did not contain valid role claims.');
    acceptToken(data.accessToken);
  };

  // Phase D/E — Google Identity Services trả idToken; backend đổi thành JWT + refresh cookie.
  const loginWithGoogle = async (idToken: string) => {
    const { data } = await api.post<AuthResponse>('/auth/google', { idToken });
    if (!userFromToken(data.accessToken)) throw new Error('Login response did not contain valid role claims.');
    acceptToken(data.accessToken);
  };

  const register = async (payload: { email: string; password: string }) => {
    await api.post('/auth/register', payload);
  };

  // Phase C — quên/đặt lại mật khẩu. forgot-password luôn 204 (chống enumeration).
  const requestPasswordReset = async (email: string) => {
    await api.post('/auth/forgot-password', { email });
  };

  const resetPassword = async (token: string, newPassword: string) => {
    await api.post('/auth/reset-password', { token, newPassword });
  };

  const logout = async () => {
    try {
      await api.post('/auth/logout');
    } finally {
      acceptToken(null);
    }
  };

  const value = useMemo<AuthContextValue>(
    () => ({ user, token, ready, login, loginWithGoogle, register, requestPasswordReset, resetPassword, logout, refreshSession }),
    [user, token, ready],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

import { useEffect, useRef } from 'react';
import { useAuth } from '../context/useAuth';
import { messageFromError } from '../lib/format';

type GoogleCredentialResponse = { credential?: string };
type GoogleAccountsId = {
  initialize: (config: { client_id: string; callback: (response: GoogleCredentialResponse) => void }) => void;
  renderButton: (parent: HTMLElement, options: Record<string, string>) => void;
};

declare global {
  interface Window {
    google?: { accounts: { id: GoogleAccountsId } };
  }
}

// Client ID lấy từ env lúc build (trùng với Google:ClientId phía backend để verify `aud`).
// Đọc thêm thẻ meta google-signin-client_id để linh hoạt khi không muốn rebuild.
function resolveClientId(): string {
  const fromEnv = import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined;
  if (fromEnv?.trim()) return fromEnv.trim();
  return document.querySelector('meta[name="google-signin-client_id"]')?.getAttribute('content')?.trim() ?? '';
}

const clientId = resolveClientId();

/**
 * Phase E — nút "Sign in with Google" dùng Google Identity Services.
 * GIS trả idToken (JWT) → AuthContext.loginWithGoogle → POST /api/auth/google.
 * Nếu thiếu client ID, hiển thị hướng dẫn cấu hình thay vì âm thầm ẩn lựa chọn Google.
 */
export function GoogleSignInButton({ onSuccess, onError, text = 'signin_with' }: {
  onSuccess?: () => void;
  onError?: (message: string) => void;
  text?: 'signin_with' | 'signup_with' | 'continue_with';
}) {
  const { loginWithGoogle } = useAuth();
  const containerRef = useRef<HTMLDivElement | null>(null);
  // Giữ handler mới nhất để effect GIS chỉ khởi tạo một lần vẫn gọi đúng callback.
  const handlers = useRef({ loginWithGoogle, onSuccess, onError });
  useEffect(() => {
    handlers.current = { loginWithGoogle, onSuccess, onError };
  });

  useEffect(() => {
    if (!clientId) return;
    let cancelled = false;

    const render = () => {
      const google = window.google;
      if (cancelled || !google || !containerRef.current) return false;
      google.accounts.id.initialize({
        client_id: clientId,
        callback: async (response) => {
          if (!response.credential) {
            handlers.current.onError?.('Google did not return a credential.');
            return;
          }
          try {
            await handlers.current.loginWithGoogle(response.credential);
            handlers.current.onSuccess?.();
          } catch (error) {
            handlers.current.onError?.(messageFromError(error, 'Google sign-in failed. Please try again.'));
          }
        },
      });
      google.accounts.id.renderButton(containerRef.current, {
        type: 'standard', theme: 'outline', size: 'large', text, width: '320',
      });
      return true;
    };

    if (render()) return () => { cancelled = true; };

    // Script GIS nạp async: chờ tới khi `window.google` sẵn sàng.
    const timeout = window.setTimeout(() => {
      window.clearInterval(interval);
      handlers.current.onError?.('Google sign-in could not load. Check your connection or browser settings and try again.');
    }, 10000);
    const interval = window.setInterval(() => {
      if (render()) {
        window.clearInterval(interval);
        window.clearTimeout(timeout);
      }
    }, 100);
    return () => {
      cancelled = true;
      window.clearInterval(interval);
      window.clearTimeout(timeout);
    };
  }, [text]);

  if (!clientId) {
    return (
      <p className="form-note" role="status">
        Google sign-in is not configured yet. Set the same Web Client ID in
        {' '}<code>frontend/.env.local</code> and backend <code>Google:ClientId</code>,
        then restart both apps.
      </p>
    );
  }
  return <div className="google-signin" ref={containerRef} />;
}

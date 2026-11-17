import { useState } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/useAuth';

type LocationState = { from?: { pathname?: string; search?: string; hash?: string } } | null;

export function AdminLoginPage() {
  const { login, logout, user, ready } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [error, setError] = useState('');

  if (user && user.role !== 'user') return <Navigate to="/admin" replace />;

  if (user) {
    return (
      <>
        <h2>Admin login</h2>
        <p className="notice">You are signed in as a regular user. Log out to use an admin account.</p>
        <div className="inline-actions">
          <button className="secondary-btn" type="button" onClick={() => void logout().catch(() => undefined)}>Log out</button>
          <Link to="/">Back to site</Link>
        </div>
      </>
    );
  }

  return (
    <>
      <h2>Admin login</h2>
      <form
        className="inline-form"
        onSubmit={async (event) => {
          event.preventDefault();
          const formData = new FormData(event.currentTarget);
          setError('');
          try {
            await login(String(formData.get('username') ?? ''), String(formData.get('password') ?? ''), true);
            const from = (location.state as LocationState)?.from;
            const destination = from?.pathname?.startsWith('/admin')
              ? `${from.pathname}${from.search ?? ''}${from.hash ?? ''}`
              : '/admin';
            navigate(destination, { replace: true });
          } catch {
            setError('Unable to sign in. Check your username and password.');
          }
        }}
      >
        <div className="field">
          <label htmlFor="admin-username">Username</label>
          <input id="admin-username" name="username" autoComplete="username" required />
        </div>
        <div className="field">
          <label htmlFor="admin-password">Password</label>
          <input id="admin-password" name="password" type="password" autoComplete="current-password" required />
        </div>
        {error ? <p className="field-error" role="alert">{error}</p> : null}
        <button className="primary-btn" type="submit" disabled={!ready}>{ready ? 'Log in' : 'Checking session…'}</button>
      </form>
      <p className="form-note"><Link to="/">← Back to site</Link></p>
    </>
  );
}

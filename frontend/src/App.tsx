import { lazy, Suspense, useEffect, useState } from 'react';
import { Link, NavLink, Outlet, Route, Routes, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { useAuth } from './context/useAuth';
import { ProtectedRoute } from './components/ProtectedRoute';
import { USER_ROLES } from './context/roles';
import { NotificationBell } from './components/CoreComponents';
import { GoogleSignInButton } from './components/GoogleSignInButton';
import {
  AccountPage,
  AppealPage,
  BoardPage,
  HomePage,
  NewThreadPage,
  NotificationsPage,
  ThreadPage,
} from './pages/UserPages';
import { api } from './lib/api';
import { isBannedStatus } from './lib/format';
import type { BoardDto, MeDto } from './types/api';
import './styles/variables.css';
import './styles/base.css';
import './styles/layout.css';
import './styles/pages.css';

// Khu quản trị tách hẳn khỏi giao diện người dùng và nạp lazy: user thường không tải code/CSS admin.
const AdminApp = lazy(() => import('./admin/AdminApp'));

const navItems = [
  { to: '/', label: 'Home' },
  { to: '/news', label: 'News' },
  { to: '/account', label: 'Account' },
  { to: '/events', label: 'Events' },
  { to: '/support', label: 'Support' },
];

function useBoards() {
  const [boards, setBoards] = useState<BoardDto[]>([]);
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    const controller = new AbortController();
    api.get<BoardDto[]>('/boards', { signal: controller.signal })
      .then(({ data }) => setBoards(data))
      .catch(() => undefined)
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, []);
  return { boards, loading };
}

/**
 * X.5 — nạp hồ sơ người dùng hiện tại (display name) và đưa user đang bị ban về /appeal.
 * Dùng chung cho GlobalLayout và BoardLayout.
 */
function useAccountGuard() {
  const { user, token } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [profile, setProfile] = useState<{ userId: number; displayName: string; status: string } | null>(null);

  useEffect(() => {
    if (!token || user?.role !== 'user') return;
    const controller = new AbortController();
    api.get<MeDto>('/users/me', { signal: controller.signal })
      .then(({ data }) => setProfile({ userId: data.id, displayName: data.displayName, status: data.status }))
      .catch(() => undefined);
    return () => controller.abort();
  }, [token, user?.id, user?.role]);

  useEffect(() => {
    if (user?.role !== 'user' || profile?.userId !== user.id) return;
    if (isBannedStatus(profile.status) && location.pathname !== '/appeal') {
      navigate('/appeal', { replace: true });
    }
  }, [user?.role, user?.id, profile, location.pathname, navigate]);

  return user?.role === 'user' && profile?.userId === user.id ? profile.displayName : '';
}

function GlobalLayout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const displayName = useAccountGuard();

  return (
    <div className="app-shell">
      <h1 className="logo-strip">
        <span>ニャー</span>
      </h1>

      <div className="page-shell">
        <header className="topbar">
          <div className="topbar-inner">
            <nav className="nav" aria-label="Main navigation">
              {navItems.map(({ to, label }) => (
                <NavLink key={to} to={to} className={({ isActive }) => (isActive ? 'active' : '')}>
                  {label}
                </NavLink>
              ))}
            </nav>

            <div className="account-strip">
              {user ? (
                <>
                  <span className="user-pill">{displayName || `${user.role} #${user.id}`}</span>
                  <NotificationBell />
                  <button className="text-button" onClick={() => void logout().catch(() => undefined).finally(() => navigate('/'))} type="button">
                    Logout
                  </button>
                </>
              ) : (
                <>
                  <span className="badge">Guest</span>
                  <NavLink className="secondary-btn" to="/login">Login</NavLink>
                  <NavLink className="text-button" to="/register">Register</NavLink>
                </>
              )}
            </div>
          </div>
        </header>

        <main className="content-stack">
          <Outlet />
        </main>
      </div>

      <footer className="footer">
        <div className="footer-inner">
          <span className="footer-brand">ニャー</span>
          <div className="footer-links">
            <NavLink to="/">Home</NavLink>
            <NavLink to="/support">About</NavLink>
            <NavLink to="/faq">FAQ</NavLink>
            <NavLink to="/contact">Contact</NavLink>
            <NavLink to="/support">Rules</NavLink>
          </div>
        </div>
      </footer>

      <small className="copyright">COPYRIGHT (C) 2001-2006 FUNFORUM. ALL RIGHTS RESERVED.</small>
    </div>
  );
}

function BoardLayout() {
  const { boardSlug: paramSlug } = useParams<{ boardSlug?: string; threadId?: string }>();
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const { boards, loading: boardsLoading } = useBoards();
  const displayName = useAccountGuard();

  const resolvedBoard = paramSlug
    ? boards.find((board) => board.slug.toLowerCase() === paramSlug.toLowerCase())
      ?? { id: 0, slug: paramSlug, name: `/${paramSlug}/`, description: '' }
    : null;

  return (
    <div className="app-shell board-shell">
      <div className="board-topnav">
        <div className="board-topnav-inner">
          <div className="board-slug-links">
            {boardsLoading && !boards.length ? <span>Loading boards…</span> : null}
            {boards.map((board, idx) => (
              <span key={board.id} className="board-link-item">
                <Link to={`/b/${board.slug}`}>{board.slug}</Link>
                {idx < boards.length - 1 ? <span className="board-link-sep"> / </span> : null}
              </span>
            ))}
          </div>
          <div className="board-topnav-right">
            {/* <span className="board-link-item"><Link to="/">[Home]</Link></span> */}
            {user ? (
              <>
                {/* <Link className="badge" to={`/u/${user.id}`}>
                  {username || `${user.role} #${user.id}`}
                </Link> */}
                <Link className="badge" to="/notifications">
                  {displayName || `${user.role} #${user.id}`}
                </Link>
                {/* <NotificationBell /> */}
                <button className="text-button" onClick={() => void logout().catch(() => undefined).finally(() => navigate('/'))} type="button">
                  Logout
                </button>
              </>
            ) : (
              <>
                <span className="badge">Guest</span>
                <Link className="text-button" to="/login">Login</Link>
                <Link className="text-button" to="/register">Register</Link>
              </>
            )}
          </div>
        </div>
      </div>

      <div className="board-page-shell">
        <header className="board-header">
          {resolvedBoard ? (
            <>
              <h1 className="board-title">
                <span className="board-slug">/{resolvedBoard.slug}/</span>
                {resolvedBoard.description ? <span className="board-subtitle"> - {resolvedBoard.description}</span> : null}
              </h1>
              <div className="board-actions-row">
                <Link className="board-newthread-btn" to={`/b/${resolvedBoard.slug}/new`}>[Start a New Thread]</Link>
              </div>
            </>
          ) : null}

          
        </header>

        <main className="board-main">
          <div className="board-quick-links">
            {resolvedBoard ? (
              <div className="board-quick-links-inner">
                <Link to={`/b/${resolvedBoard.slug}`}>[Return]</Link>
                <span> [Catalog] </span>
                <span> [Archive] </span>
                <Link to="/">[Home]</Link>
              </div>
            ) : null}
          </div>
          <Outlet />
        </main>
      </div>

      {/* <footer className="footer">
        <div className="footer-inner">
          <span className="footer-brand">ニャー</span>
          <div className="footer-links">
            <NavLink to="/">Home</NavLink>
            <NavLink to="/support">About</NavLink>
            <NavLink to="/faq">FAQ</NavLink>
            <NavLink to="/contact">Contact</NavLink>
            <NavLink to="/support">Rules</NavLink>
          </div>
        </div>
      </footer>

      <small className="copyright">COPYRIGHT (C) 2001-2006 FUNFORUM. ALL RIGHTS RESERVED.</small> */}
    </div>
  );
}

function NewsPage() {
  return (
    <div className="page-card">
      <h2>News</h2>
      <p className="notice">This is a placeholder page for the future news feed.</p>
    </div>
  );
}

function EventsPage() {
  return (
    <div className="page-card">
      <h2>Events</h2>
      <p className="notice">Event page placeholder for later iteration.</p>
    </div>
  );
}

function SupportPage() {
  return (
    <div className="page-card">
      <h2>Support</h2>
      <div className="list-links">
        <NavLink to="/faq">FAQ</NavLink>
        <NavLink to="/contact">Contact</NavLink>
      </div>
    </div>
  );
}

function FAQPage() {
  const entries = [
    { question: 'How do I register?', answer: 'Use the registration form in the account area with your email address.' },
    { question: 'Can I post images?', answer: 'Yes. Follow the board-specific upload rules and stay within the file-size limit.' },
    { question: 'What is the forum etiquette?', answer: 'Keep discussions respectful, avoid spam, and focus on the thread topic.' },
  ];

  return (
    <div className="page-card faq-page">
      <div className="faq-header">
        <h3 className="faq-title">Frequently Asked Questions</h3>
        <p className="faq-welcome">
          Welcome to Funforum's Frequently Asked Questions page. After reading the FAQ, be sure to
          familiarize yourself with the Rules!
        </p>
      </div>

      <div className="faq-body">
        <aside className="sidebar">
          <div className="question-title">Questions</div>
          <div className="question-content">
            <ul className="question-list">
              {entries.map((entry, index) => <li className="item-lv1" key={entry.question}><a href={`#faq-${index + 1}`}>{entry.question}</a></li>)}
            </ul>
          </div>
        </aside>

        <div className="main-content">
          <div className="answer-title">Answers</div>
          <div className="answer-content">
            {entries.map((entry, index) => (
              <article id={`faq-${index + 1}`} key={entry.question}>
                <h3>{index + 1}. {entry.question}</h3>
                <p>{entry.answer}</p>
              </article>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

function ContactPage() {
  return (
    <div className="page-card">
      <h2>Contact</h2>
      <p className="notice">For support, please review the FAQ and use the contact form in the support center.</p>
    </div>
  );
}

function LoginPage() {
  const { login, user, ready } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [error, setError] = useState('');

  if (user) {
    return (
      <div className="page-card">
        <h2>You are already logged in.</h2>
        <NavLink to={user.role === 'user' ? '/account' : '/admin'}>{user.role === 'user' ? 'Continue to your account' : 'Go to the admin panel'}</NavLink>
      </div>
    );
  }

  const from = (location.state as { from?: { pathname?: string; search?: string; hash?: string } } | null)?.from;
  const destination = from ? `${from.pathname ?? '/'}${from.search ?? ''}${from.hash ?? ''}` : '/account';

  return (
    <div className="page-card">
      <h2>Login</h2>
      <form
        className="inline-form"
        onSubmit={async (event) => {
          event.preventDefault();
          const form = event.currentTarget;
          const formData = new FormData(form);
          const email = String(formData.get('email') ?? '');
          const password = String(formData.get('password') ?? '');
          setError('');
          try {
            await login(email, password);
            navigate(destination, { replace: true });
          } catch {
            setError('Unable to sign in. Check your email and password.');
          }
        }}
      >
        <div className="field">
          <label htmlFor="email">Email</label>
          <input id="email" name="email" type="email" required />
        </div>
        <div className="field">
          <label htmlFor="password">Password</label>
          <input id="password" name="password" type="password" required />
        </div>
        {error ? <p className="field-error" role="alert">{error}</p> : null}
        <button className="primary-btn" type="submit" disabled={!ready}>{ready ? 'Log in' : 'Checking session…'}</button>
      </form>
      <p className="form-note"><NavLink to="/forgot-password">Forgot password?</NavLink></p>
      <GoogleSignInButton onSuccess={() => navigate(destination, { replace: true })} onError={setError} />
      <p className="form-note">New here? <NavLink to="/register" state={location.state}>Create an account</NavLink></p>
    </div>
  );
}

function RegisterPage() {
  const { register } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  return (
    <div className="page-card">
      <h2>Register</h2>
      <form
        className="inline-form"
        onSubmit={async (event) => {
          event.preventDefault();
          const form = event.currentTarget;
          const formData = new FormData(form);
          setError('');
          setSubmitting(true);
          try {
            await register({
              email: String(formData.get('email') ?? ''),
              password: String(formData.get('password') ?? ''),
            });
            navigate('/login', { replace: true, state: location.state });
          } catch {
            setError('Account could not be created. Check the details or use another email.');
          } finally {
            setSubmitting(false);
          }
        }}
      >
        <div className="field">
          <label htmlFor="reg-email">Email</label>
          <input id="reg-email" name="email" type="email" required />
        </div>
        <div className="field">
          <label htmlFor="reg-password">Password</label>
          <input id="reg-password" name="password" type="password" minLength={8} maxLength={64} required />
        </div>
        {error ? <p className="field-error" role="alert">{error}</p> : null}
        <button className="primary-btn" type="submit" disabled={submitting}>{submitting ? 'Creating…' : 'Create account'}</button>
      </form>
      <p className="form-note">Your display name defaults to <strong>Anonymous</strong>; you can change it later in your account.</p>
      <p className="form-note">Full name is optional and can be added later in your account settings.</p>
      <GoogleSignInButton text="signup_with" onSuccess={() => navigate('/account', { replace: true })} onError={setError} />
    </div>
  );
}

function NotFoundPage() {
  return (
    <div className="page-card">
      <h1>404</h1>
      <p>Page not found.</p>
    </div>
  );
}

// Phase C/E — xin link đặt lại mật khẩu. Backend luôn 204 để không lộ email tồn tại,
// nên UI cũng luôn báo "đã gửi" thay vì xác nhận email có tài khoản hay không.
function ForgotPasswordPage() {
  const { requestPasswordReset } = useAuth();
  const [error, setError] = useState('');
  const [sent, setSent] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  return (
    <div className="page-card">
      <h2>Forgot password</h2>
      {sent ? (
        <p className="notice" role="status">If an account exists for that email, we have sent a reset link. Check your inbox.</p>
      ) : (
        <form
          className="inline-form"
          onSubmit={async (event) => {
            event.preventDefault();
            const form = new FormData(event.currentTarget);
            setError('');
            setSubmitting(true);
            try {
              await requestPasswordReset(String(form.get('email') ?? ''));
              setSent(true);
            } catch {
              setError('The request could not be sent. Please try again.');
            } finally {
              setSubmitting(false);
            }
          }}
        >
          <div className="field">
            <label htmlFor="forgot-email">Email</label>
            <input id="forgot-email" name="email" type="email" required />
          </div>
          {error ? <p className="field-error" role="alert">{error}</p> : null}
          <button className="primary-btn" type="submit" disabled={submitting}>{submitting ? 'Sending…' : 'Send reset link'}</button>
        </form>
      )}
      <p className="form-note"><NavLink to="/login">Back to login</NavLink></p>
    </div>
  );
}

// Phase C/E — trang đích của link trong mail reset (?token=...). Token dùng 1 lần, TTL 30 phút.
function ResetPasswordPage() {
  const { resetPassword } = useAuth();
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token') ?? '';
  const [error, setError] = useState('');
  const [done, setDone] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  if (!token) {
    return (
      <div className="page-card">
        <h2>Reset password</h2>
        <p className="field-error" role="alert">This reset link is missing its token.</p>
        <p className="form-note"><NavLink to="/forgot-password">Request a new link</NavLink></p>
      </div>
    );
  }

  return (
    <div className="page-card">
      <h2>Reset password</h2>
      {done ? (
        <>
          <p className="notice" role="status">Your password has been updated. You can now log in.</p>
          <p className="form-note"><NavLink to="/login">Go to login</NavLink></p>
        </>
      ) : (
        <form
          className="inline-form"
          onSubmit={async (event) => {
            event.preventDefault();
            const form = new FormData(event.currentTarget);
            const newPassword = String(form.get('newPassword') ?? '');
            const confirmPassword = String(form.get('confirmPassword') ?? '');
            setError('');
            if (newPassword !== confirmPassword) {
              setError('Passwords do not match.');
              return;
            }
            setSubmitting(true);
            try {
              await resetPassword(token, newPassword);
              setDone(true);
            } catch {
              setError('This reset link is invalid or has expired.');
            } finally {
              setSubmitting(false);
            }
          }}
        >
          <div className="field">
            <label htmlFor="reset-password">New password</label>
            <input id="reset-password" name="newPassword" type="password" minLength={8} maxLength={64} required />
          </div>
          <div className="field">
            <label htmlFor="reset-confirm">Confirm password</label>
            <input id="reset-confirm" name="confirmPassword" type="password" minLength={8} maxLength={64} required />
          </div>
          {error ? <p className="field-error" role="alert">{error}</p> : null}
          <button className="primary-btn" type="submit" disabled={submitting}>{submitting ? 'Resetting…' : 'Reset password'}</button>
        </form>
      )}
    </div>
  );
}

function App() {
  return (
    <AuthProvider>
      <Routes>
        {/* Khu quản trị: layout riêng, không đi qua GlobalLayout/BoardLayout */}
        <Route
          path="/admin/*"
          element={(
            <Suspense fallback={<div className="session-loading" role="status">Loading admin panel…</div>}>
              <AdminApp />
            </Suspense>
          )}
        />

        <Route element={<GlobalLayout />}>
          <Route path="/" element={<HomePage />} />
          <Route path="/news" element={<NewsPage />} />
          <Route path="/events" element={<EventsPage />} />
          <Route path="/support" element={<SupportPage />} />
          <Route path="/faq" element={<FAQPage />} />
          <Route path="/contact" element={<ContactPage />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/forgot-password" element={<ForgotPasswordPage />} />
          <Route path="/reset-password" element={<ResetPasswordPage />} />
          <Route element={<ProtectedRoute roles={['user']} />}>
            <Route path="/account" element={<AccountPage />} />
            <Route path="/appeal" element={<AppealPage />} />
          </Route>
          <Route element={<ProtectedRoute roles={USER_ROLES} />}>
            <Route path="/notifications" element={<NotificationsPage />} />
          </Route>
          <Route path="*" element={<NotFoundPage />} />
        </Route>

        <Route element={<BoardLayout />}>
          <Route element={<ProtectedRoute roles={USER_ROLES} />}>
            <Route path="/b/:boardSlug/new" element={<NewThreadPage />} />
          </Route>
          <Route path="/b/:boardSlug" element={<BoardPage />} />
          <Route path="/b/:boardSlug/thread/:threadId" element={<ThreadPage />} />
        </Route>

        <Route element={<BoardLayout />}>
          <Route path="/thread/:threadId" element={<ThreadPage />} />
        </Route>
      </Routes>
    </AuthProvider>
  );
}

export default App;

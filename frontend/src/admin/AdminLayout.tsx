import {
  useEffect,
  useRef,
  useState,
  useSyncExternalStore,
  type CSSProperties,
  type KeyboardEvent as ReactKeyboardEvent,
  type PointerEvent as ReactPointerEvent,
} from 'react';
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/useAuth';
import { roleLabel } from '../context/roles';

const STORAGE_KEY = 'nyachan.admin.sidebar';
const MIN_WIDTH = 160;
const MAX_WIDTH = 360;
const DEFAULT_WIDTH = 210;
const KEY_STEP = 16;
const MOBILE_QUERY = '(max-width: 760px)';

type SidebarPrefs = { width: number; collapsed: boolean };

const clampWidth = (value: number) => Math.min(MAX_WIDTH, Math.max(MIN_WIDTH, Math.round(value)));

function loadPrefs(): SidebarPrefs {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw) as Partial<SidebarPrefs>;
      return { width: clampWidth(Number(parsed.width) || DEFAULT_WIDTH), collapsed: Boolean(parsed.collapsed) };
    }
  } catch {
    // localStorage bị chặn hoặc dữ liệu hỏng: dùng mặc định.
  }
  return { width: DEFAULT_WIDTH, collapsed: false };
}

function savePrefs(prefs: SidebarPrefs) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(prefs));
  } catch {
    // Bỏ qua: chỉ là tiện ích ghi nhớ giao diện.
  }
}

function useIsMobile() {
  return useSyncExternalStore(
    (onChange) => {
      const query = window.matchMedia(MOBILE_QUERY);
      query.addEventListener('change', onChange);
      return () => query.removeEventListener('change', onChange);
    },
    () => window.matchMedia(MOBILE_QUERY).matches,
    () => false,
  );
}

/**
 * Khung của khu quản trị: thanh trên + sidebar + vùng nội dung. Không dùng chung với layout người dùng.
 * Sidebar: kéo mép phải để đổi độ rộng (hoặc dùng phím ←/→ khi đang focus), nút ☰ để đóng/mở.
 * Trên màn hình hẹp, sidebar là ngăn kéo trượt ra từ bên trái.
 */
export function AdminLayout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const isSuperAdmin = user?.role === 'super_admin';
  const isModeratorUp = user?.role === 'moderator' || isSuperAdmin;
  const isMobile = useIsMobile();

  const layoutRef = useRef<HTMLDivElement>(null);
  const [prefs, setPrefs] = useState<SidebarPrefs>(loadPrefs);
  const [resizing, setResizing] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);

  // Chỉ ghi localStorage khi thả chuột, không ghi liên tục lúc đang kéo.
  useEffect(() => {
    if (!resizing) savePrefs(prefs);
  }, [prefs, resizing]);

  useEffect(() => {
    if (!mobileOpen) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setMobileOpen(false);
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [mobileOpen]);

  const closed = isMobile ? !mobileOpen : prefs.collapsed;
  const expanded = !closed;
  const closeDrawer = () => setMobileOpen(false);
  const closeSidebar = () => {
    if (isMobile) setMobileOpen(false);
    else setPrefs((current) => ({ ...current, collapsed: true }));
  };
  const toggleSidebar = () => {
    if (isMobile) setMobileOpen((open) => !open);
    else setPrefs((current) => ({ ...current, collapsed: !current.collapsed }));
  };
  const setWidth = (width: number) => setPrefs((current) => ({ ...current, width: clampWidth(width) }));

  const startResize = (event: ReactPointerEvent<HTMLDivElement>) => {
    event.preventDefault();
    event.currentTarget.setPointerCapture(event.pointerId);
    setResizing(true);
  };
  const onResize = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (!event.currentTarget.hasPointerCapture(event.pointerId)) return;
    const left = layoutRef.current?.getBoundingClientRect().left ?? 0;
    setWidth(event.clientX - left);
  };
  const endResize = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (event.currentTarget.hasPointerCapture(event.pointerId)) event.currentTarget.releasePointerCapture(event.pointerId);
    setResizing(false);
  };
  const onResizerKeyDown = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    const step = event.shiftKey ? KEY_STEP * 2 : KEY_STEP;
    if (event.key === 'ArrowLeft') setWidth(prefs.width - step);
    else if (event.key === 'ArrowRight') setWidth(prefs.width + step);
    else if (event.key === 'Home') setWidth(MIN_WIDTH);
    else if (event.key === 'End') setWidth(MAX_WIDTH);
    else return;
    event.preventDefault();
  };

  const shellClass = [
    'admin-shell',
    closed ? 'sidebar-closed' : '',
    isMobile && mobileOpen ? 'sidebar-open' : '',
    resizing ? 'is-resizing' : '',
  ].filter(Boolean).join(' ');

  return (
    <div className={shellClass} style={{ '--admin-sidebar-w': `${prefs.width}px` } as CSSProperties}>
      <header className="admin-topbar">
        <div className="admin-topbar-left">
          <button
            className="admin-sidebar-toggle"
            type="button"
            onClick={toggleSidebar}
            aria-controls="admin-sidebar"
            aria-expanded={expanded}
            aria-label={expanded ? 'Close sidebar' : 'Open sidebar'}
            title={expanded ? 'Close sidebar' : 'Open sidebar'}
          >
            ☰
          </button>
          <h1 className="admin-brand">
            <span>ニャー</span>
            <small>Admin</small>
          </h1>
        </div>
        <div className="admin-topbar-right">
          {user ? (
            <span className={`admin-role-badge${isSuperAdmin ? ' superadmin' : ''}`}>
              {roleLabel(user.role)} #{user.id}
            </span>
          ) : null}
          <Link to="/">View site</Link>
          <button
            className="text-button"
            type="button"
            onClick={() => void logout().catch(() => undefined).finally(() => navigate('/admin/login'))}
          >
            Logout
          </button>
        </div>
      </header>

      <div ref={layoutRef} className="admin-layout">
        <aside id="admin-sidebar" className="admin-sidebar">
          <div className="admin-sidebar-head">
            <span className="admin-sidebar-title">Admin Panel</span>
            <button className="admin-sidebar-close" type="button" onClick={closeSidebar} aria-label="Close sidebar" title="Close sidebar">
              ✕
            </button>
          </div>
          <nav className="admin-nav" aria-label="Admin navigation">
            <NavLink to="/admin" end onClick={closeDrawer}>Overview</NavLink>
            <NavLink to="/admin/threads" onClick={closeDrawer}>Threads</NavLink>
            <NavLink to="/admin/reports" onClick={closeDrawer}>Reports</NavLink>
            {isModeratorUp ? <NavLink to="/admin/boards" onClick={closeDrawer}>Boards</NavLink> : null}
            {isModeratorUp ? <NavLink to="/admin/users" onClick={closeDrawer}>Users</NavLink> : null}
            {isModeratorUp ? <NavLink to="/admin/bans" onClick={closeDrawer}>Bans</NavLink> : null}
            {isModeratorUp ? <NavLink to="/admin/appeals" onClick={closeDrawer}>Appeals</NavLink> : null}
            {isSuperAdmin ? <NavLink to="/admin/admins" onClick={closeDrawer}>Admins</NavLink> : null}
            {isSuperAdmin ? <NavLink to="/admin/audit-logs" onClick={closeDrawer}>Audit logs</NavLink> : null}
          </nav>
          <div className="admin-sidebar-footer">
            <Link to="/">← Back to site</Link>
          </div>
          <div
            className="admin-sidebar-resizer"
            role="separator"
            aria-orientation="vertical"
            aria-label="Resize sidebar"
            aria-valuemin={MIN_WIDTH}
            aria-valuemax={MAX_WIDTH}
            aria-valuenow={prefs.width}
            tabIndex={0}
            title="Drag to resize · double-click to reset"
            onPointerDown={startResize}
            onPointerMove={onResize}
            onPointerUp={endResize}
            onPointerCancel={endResize}
            onKeyDown={onResizerKeyDown}
            onDoubleClick={() => setWidth(DEFAULT_WIDTH)}
          />
        </aside>

        <div className="admin-backdrop" aria-hidden="true" onClick={closeDrawer} />

        <section className="admin-main-area">
          <Outlet />
        </section>
      </div>
    </div>
  );
}

/** Khung tối giản cho trang đăng nhập admin (chưa có sidebar/nav). */
export function AdminAuthLayout() {
  return (
    <div className="admin-shell admin-auth">
      <main className="admin-auth-card">
        <h1 className="admin-brand">
          <span>ニャー</span>
          <small>Admin</small>
        </h1>
        <Outlet />
      </main>
    </div>
  );
}
import { useEffect, useState, type FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../lib/api';
import { formatDate, messageFromError, statusFromError } from '../lib/format';
import { useAuth } from '../context/useAuth';
import { ADMIN_ROLES, roleLabel } from '../context/roles';
import { CharacterCountTextarea, ImageUpload } from '../components/CoreComponents';
import { AdminDataTable } from './AdminDataTable';
import type { AdminColumn } from './AdminDataTable';
import type {
  AdminAccountDto,
  AdminReportDto,
  AdminThreadDto,
  AdminUserDto,
  AppealDto,
  AuditLogDto,
  BanDto,
  BoardDto,
  PagedResult,
  ThreadDto,
} from '../types/api';

const AUDIT_ACTIONS = [
  'hide',
  'unhide',
  'delete',
  'ban',
  'unban',
  'role_change',
  'board_create',
  'board_delete',
  'wordfilter_edit',
] as const;

/** datetime-local (local) → ISO UTC cho query `from`/`to`. */
function localInputToIso(value: string): string | undefined {
  if (!value) return undefined;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? undefined : date.toISOString();
}

function formatAuditMetadata(metadata: unknown): string {
  if (metadata == null) return '—';
  try {
    return JSON.stringify(metadata);
  } catch {
    return String(metadata);
  }
}

function useBoardsAll() {
  const [boards, setBoards] = useState<BoardDto[]>([]);
  useEffect(() => {
    const controller = new AbortController();
    api.get<BoardDto[]>('/boards', { signal: controller.signal })
      .then(({ data }) => setBoards(data))
      .catch(() => undefined);
    return () => controller.abort();
  }, []);
  return boards;
}

function ActionFeedback({ message, error }: { message: string; error: string }) {
  if (message) return <p className="admin-notice" role="status">{message}</p>;
  if (error) return <p className="admin-notice error" role="alert">{error}</p>;
  return null;
}

export function AdminDashboardPage() {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === 'super_admin';
  const isModeratorUp = user?.role === 'moderator' || isSuperAdmin;
  const [stats, setStats] = useState<{
    threads: number | null;
    boards: number | null;
    users: number | null;
    admins: number | null;
    auditLogs: number | null;
  }>({
    threads: null,
    boards: null,
    users: null,
    admins: null,
    auditLogs: null,
  });
  const [recentThreads, setRecentThreads] = useState<AdminThreadDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    const controller = new AbortController();
    const { signal } = controller;
    const count = (endpoint: string): Promise<number | null> =>
      api.get<PagedResult<unknown>>(endpoint, { params: { page: 1, pageSize: 1 }, signal })
        .then(({ data }) => data.totalItems)
        .catch(() => null);

    Promise.all([
      count('/admin/threads'),
      isModeratorUp ? count('/admin/boards') : Promise.resolve(null),
      isModeratorUp ? count('/admin/users') : Promise.resolve(null),
      isSuperAdmin ? count('/admin/admins') : Promise.resolve(null),
      isSuperAdmin ? count('/admin/audit-logs') : Promise.resolve(null),
      api.get<PagedResult<AdminThreadDto>>('/admin/threads', {
        params: { page: 1, pageSize: 5, sort: 'lastReplyAt', order: 'desc' },
        signal,
      })
        .then(({ data }) => data.items)
        .catch(() => [] as AdminThreadDto[]),
    ]).then(([threads, boards, users, admins, auditLogs, recent]) => {
      if (signal.aborted) return;
      setStats({ threads, boards, users, admins, auditLogs });
      setRecentThreads(recent);
      setError('');
    }).catch(() => {
      if (!signal.aborted) setError('Dashboard data could not be loaded.');
    }).finally(() => {
      if (!signal.aborted) setLoading(false);
    });
    return () => controller.abort();
  }, [isSuperAdmin, isModeratorUp]);

  return (
    <section className="admin-page admin-dashboard">
      <header className="section-head"><h2>Overview</h2></header>
      {error ? <p className="field-error" role="alert">{error}</p> : null}
      {loading ? <p role="status">Loading dashboard…</p> : (
        <>
          <div className="admin-stat-grid">
            <div className="admin-stat-card"><span className="stat-value">{stats.threads ?? '—'}</span><span className="stat-label">Threads</span></div>
            {isModeratorUp ? <div className="admin-stat-card"><span className="stat-value">{stats.boards ?? '—'}</span><span className="stat-label">Boards</span></div> : null}
            {isModeratorUp ? <div className="admin-stat-card"><span className="stat-value">{stats.users ?? '—'}</span><span className="stat-label">Users</span></div> : null}
            {isSuperAdmin ? <div className="admin-stat-card"><span className="stat-value">{stats.admins ?? '—'}</span><span className="stat-label">Admins</span></div> : null}
            {isSuperAdmin ? (
              <div className="admin-stat-card">
                <span className="stat-value">{stats.auditLogs ?? '—'}</span>
                <span className="stat-label">Audit logs</span>
              </div>
            ) : null}
          </div>

          <div>
            <h3 className="section-title">Recently active threads</h3>
            {recentThreads.length ? (
              <table className="admin-table">
                <thead>
                  <tr>
                    <th>ID</th>
                    <th>Board</th>
                    <th>Title</th>
                    <th>Author</th>
                    <th>Status</th>
                    <th>Last reply</th>
                  </tr>
                </thead>
                <tbody>
                  {recentThreads.map((thread) => (
                    <tr key={thread.id}>
                      <td>{thread.id}</td>
                      <td><Link to={`/b/${thread.boardSlug}`}>/{thread.boardSlug}/</Link></td>
                      <td><Link to={`/b/${thread.boardSlug}/thread/${thread.id}`}>{thread.title}</Link></td>
                      <td>{thread.authorUsername ?? '—'}</td>
                      <td><span className={`status-pill status-${thread.status}`}>{thread.status}</span></td>
                      <td>{formatDate(thread.lastReplyAt)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : <p className="hint">No threads have been posted yet.</p>}
          </div>

          <nav className="admin-quick-links" aria-label="Admin sections">
            <Link className="secondary-btn" to="/admin/threads">Manage threads</Link>
            {isModeratorUp ? <Link className="secondary-btn" to="/admin/boards">Manage boards</Link> : null}
            {isModeratorUp ? <Link className="secondary-btn" to="/admin/users">Manage users</Link> : null}
            {isSuperAdmin ? <Link className="secondary-btn" to="/admin/admins">Manage admins</Link> : null}
            {isSuperAdmin ? <Link className="secondary-btn" to="/admin/audit-logs">View audit logs</Link> : null}
          </nav>
        </>
      )}
    </section>
  );
}

export function AdminThreadsPage() {
  const navigate = useNavigate();
  const boards = useBoardsAll();
  const [showForm, setShowForm] = useState(false);
  const [formData, setFormData] = useState({ boardSlug: '', title: '', content: '', useCapcode: true });
  const [files, setFiles] = useState<File[]>([]);
  const [formError, setFormError] = useState('');
  const [actionMessage, setActionMessage] = useState('');
  const [actionError, setActionError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  // The board list loads asynchronously; derive the effective selection so the
  // selector falls back to the first board without needing an effect.
  const selectedBoardSlug = formData.boardSlug || boards[0]?.slug || '';

  const setStatus = async (threadId: number, next: 'visible' | 'hidden') => {
    setActionMessage('');
    setActionError('');
    try {
      await api.patch(`/admin/threads/${threadId}/status`, { status: next });
      setActionMessage(next === 'hidden' ? `Thread #${threadId} is now hidden.` : `Thread #${threadId} is visible again.`);
      setRefreshKey((key) => key + 1);
    } catch (err) {
      setActionError(messageFromError(err, 'Thread status could not be updated.'));
    }
  };

  const deleteThread = async (thread: AdminThreadDto) => {
    if (!window.confirm(`Delete thread "${thread.title}" and its ${thread.replyCount} replies? This cannot be undone.`)) return;
    setActionMessage('');
    setActionError('');
    try {
      await api.delete(`/admin/threads/${thread.id}`);
      setActionMessage(`Thread #${thread.id} deleted.`);
      setRefreshKey((key) => key + 1);
    } catch (err) {
      setActionError(messageFromError(err, 'Thread could not be deleted.'));
    }
  };

  const submitCreate = async (event: FormEvent) => {
    event.preventDefault();
    setSubmitting(true);
    setFormError('');
    try {
      const fd = new FormData();
      fd.append('boardSlug', selectedBoardSlug);
      fd.append('title', formData.title);
      fd.append('content', formData.content);
      fd.append('useCapcode', String(formData.useCapcode));
      if (files[0]) fd.append('image', files[0]);
      const { data } = await api.post<ThreadDto>('/admin/threads', fd);
      navigate(`/b/${data.boardSlug}/thread/${data.id}`);
    } catch (err) {
      setFormError(messageFromError(err, 'Thread could not be created.'));
    } finally {
      setSubmitting(false);
    }
  };

  const threadColumns: AdminColumn<AdminThreadDto>[] = [
    { key: 'id', label: 'ID', sortable: true },
    { key: 'boardSlug', label: 'Board', render: (t) => <Link to={`/b/${t.boardSlug}`}>/{t.boardSlug}/</Link> },
    { key: 'title', label: 'Title', sortable: true, render: (t) => <Link to={`/b/${t.boardSlug}/thread/${t.id}`}>{t.title}</Link> },
    { key: 'author', label: 'Author', render: (t) => t.authorUsername
        ? `${t.authorUsername}${t.authorAdminId != null ? ` (Admin #${t.authorAdminId})` : t.authorUserId != null ? ` (User #${t.authorUserId})` : ''}`
        : '—' },
    { key: 'capcode', label: 'Capcode', render: (t) => t.capcode ?? '—' },
    { key: 'replyCount', label: 'Replies', sortable: true },
    { key: 'score', label: 'Score', sortable: true },
    { key: 'status', label: 'Status', sortable: true, render: (t) => (
        <span className={`status-pill status-${t.status}`}>{t.status}</span>
    ) },
    { key: 'lastReplyAt', label: 'Last reply', sortable: true, render: (t) => formatDate(t.lastReplyAt) },
  ];

  return (
    <section className="admin-page">
      <header className="section-head">
        <h2>Threads</h2>
        <button type="button" className="primary-btn" onClick={() => setShowForm((prev) => !prev)}>
          {showForm ? 'Cancel' : '+ New thread'}
        </button>
      </header>

      {showForm ? (
        <form className="form-stack admin-form" onSubmit={submitCreate}>
          <div className="field">
            <label htmlFor="admin-thread-board">Board</label>
            <select
              id="admin-thread-board"
              value={selectedBoardSlug}
              onChange={(event) => setFormData((prev) => ({ ...prev, boardSlug: event.target.value }))}
              required
            >
              {boards.length ? (
                boards.map((b) => <option key={b.id} value={b.slug}>/{b.slug}/ — {b.name}</option>)
              ) : (
                <option value="">Loading boards…</option>
              )}
            </select>
          </div>
          <div className="field">
            <label htmlFor="admin-thread-title">Title</label>
            <input
              id="admin-thread-title"
              type="text"
              maxLength={200}
              required
              value={formData.title}
              onChange={(event) => setFormData((prev) => ({ ...prev, title: event.target.value }))}
            />
          </div>
          <div className="field">
            <label htmlFor="admin-thread-content">Content</label>
            <CharacterCountTextarea
              id="admin-thread-content"
              value={formData.content}
              maxLength={10000}
              onChange={(content) => setFormData((prev) => ({ ...prev, content }))}
            />
          </div>
          <ImageUpload files={files} onChange={setFiles} allowMultiple={false} />
          <div className="field inline-field">
            <label className="checkbox-label">
              <input
                type="checkbox"
                checked={formData.useCapcode}
                onChange={(event) => setFormData((prev) => ({ ...prev, useCapcode: event.target.checked }))}
              />
              Post with capcode
            </label>
          </div>
          {formError ? <p className="field-error" role="alert">{formError}</p> : null}
          <button type="submit" className="primary-btn" disabled={submitting || !selectedBoardSlug}>
            {submitting ? 'Creating…' : 'Create thread'}
          </button>
          <hr className="divider" />
        </form>
      ) : null}

      <ActionFeedback message={actionMessage} error={actionError} />

      <AdminDataTable<AdminThreadDto>
        endpoint="/admin/threads"
        initialSort="lastReplyAt"
        initialOrder="desc"
        searchPlaceholder="Search by title or content…"
        columns={threadColumns}
        refreshKey={refreshKey}
        rowActions={(t) => (
          <>
            {t.status === 'visible'
              ? <button type="button" className="text-button danger" onClick={() => void setStatus(t.id, 'hidden')}>Hide</button>
              : <button type="button" className="text-button" onClick={() => void setStatus(t.id, 'visible')}>Unhide</button>}
            <Link className="text-button" to={`/b/${t.boardSlug}/thread/${t.id}`}>View</Link>
            <button type="button" className="text-button danger" onClick={() => void deleteThread(t)}>Delete</button>
          </>
        )}
      />
    </section>
  );
}

export function AdminBoardsPage() {
  const { user } = useAuth();
  const canManageBoards = user?.role === 'super_admin';
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [formData, setFormData] = useState({ slug: '', name: '', description: '', sortOrder: 0 });
  const [formError, setFormError] = useState('');
  const [slugError, setSlugError] = useState('');
  const [actionMessage, setActionMessage] = useState('');
  const [actionError, setActionError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  const openCreate = () => {
    setEditingId(null);
    setFormData({ slug: '', name: '', description: '', sortOrder: 0 });
    setFormError('');
    setSlugError('');
    setShowForm(true);
  };

  const openEdit = (board: BoardDto) => {
    setEditingId(board.id);
    setFormData({ slug: board.slug, name: board.name, description: board.description ?? '', sortOrder: board.sortOrder ?? 0 });
    setFormError('');
    setSlugError('');
    setShowForm(true);
  };

  const closeForm = () => {
    setShowForm(false);
    setEditingId(null);
    setFormError('');
    setSlugError('');
  };

  const submitForm = async (event: FormEvent) => {
    event.preventDefault();
    setSubmitting(true);
    setFormError('');
    setSlugError('');
    setActionMessage('');
    setActionError('');
    try {
      if (editingId === null) {
        if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(formData.slug) || formData.slug.length > 50) {
          setSlugError('Slug must be lowercase letters and numbers separated by single hyphens (max 50 characters).');
          setSubmitting(false);
          return;
        }
        await api.post('/admin/boards', {
          slug: formData.slug,
          name: formData.name,
          description: formData.description,
          sortOrder: formData.sortOrder,
        });
        setActionMessage(`Board /${formData.slug}/ created.`);
      } else {
        await api.put(`/admin/boards/${editingId}`, {
          name: formData.name,
          description: formData.description,
          sortOrder: formData.sortOrder,
        });
        setActionMessage(`Board /${formData.slug}/ updated.`);
      }
      closeForm();
      setRefreshKey((key) => key + 1);
    } catch (err) {
      const status = statusFromError(err);
      const msg = messageFromError(err, 'Board could not be saved.');
      if (editingId === null && (status === 400 || status === 409) && msg.toLowerCase().includes('slug')) setSlugError(msg);
      else setFormError(msg);
    } finally {
      setSubmitting(false);
    }
  };

  const deleteBoard = async (board: BoardDto) => {
    if (!window.confirm(`Delete board /${board.slug}/? Boards that still contain threads (including hidden ones) cannot be deleted.`)) return;
    setActionMessage('');
    setActionError('');
    try {
      await api.delete(`/admin/boards/${board.id}`);
      setActionMessage(`Board /${board.slug}/ deleted.`);
      setRefreshKey((key) => key + 1);
    } catch (err) {
      setActionError(messageFromError(err, 'Board could not be deleted.'));
    }
  };

  const boardColumns: AdminColumn<BoardDto>[] = [
    { key: 'id', label: 'ID', sortable: true },
    { key: 'slug', label: 'Slug', sortable: true, render: (b) => <Link to={`/b/${b.slug}`}>/{b.slug}/</Link> },
    { key: 'name', label: 'Name', sortable: true },
    { key: 'description', label: 'Description', render: (b) => b.description ?? '—' },
    { key: 'createdAt', label: 'Created', sortable: true, render: (b) => (b.createdAt ? formatDate(b.createdAt) : '—') },
  ];

  return (
    <section className="admin-page">
      <header className="section-head">
        <h2>Boards</h2>
        {canManageBoards ? (
          <button type="button" className="primary-btn" onClick={() => (showForm ? closeForm() : openCreate())}>
            {showForm ? 'Cancel' : '+ New board'}
          </button>
        ) : <span className="hint">Read-only. Only Super Admin can create, edit or delete boards.</span>}
      </header>

      {showForm ? (
        <form className="form-stack admin-form" onSubmit={submitForm}>
          <div className="field">
            <label htmlFor="admin-board-slug">
              Slug {editingId === null ? '' : '(read-only after creation)'}
            </label>
            <input
              id="admin-board-slug"
              type="text"
              required
              disabled={editingId !== null}
              maxLength={50}
              value={formData.slug}
              onChange={(event) => setFormData((prev) => ({ ...prev, slug: event.target.value.toLowerCase() }))}
            />
            <small className="hint">Lowercase letters and numbers separated by single hyphens, e.g. "tech" or "anime-general". Cannot be changed later.</small>
            {slugError ? <p className="field-error" role="alert">{slugError}</p> : null}
          </div>
          <div className="field">
            <label htmlFor="admin-board-name">Name</label>
            <input
              id="admin-board-name"
              type="text"
              required
              maxLength={100}
              value={formData.name}
              onChange={(event) => setFormData((prev) => ({ ...prev, name: event.target.value }))}
            />
          </div>
          <div className="field">
            <label htmlFor="admin-board-description">Description</label>
            <textarea
              id="admin-board-description"
              maxLength={500}
              rows={2}
              value={formData.description}
              onChange={(event) => setFormData((prev) => ({ ...prev, description: event.target.value }))}
            />
          </div>
          <div className="field">
            <label htmlFor="admin-board-sort">Sort order (lower = earlier in list)</label>
            <input
              id="admin-board-sort"
              type="number"
              value={formData.sortOrder}
              onChange={(event) => setFormData((prev) => ({ ...prev, sortOrder: Number(event.target.value) || 0 }))}
            />
          </div>
          {formError ? <p className="field-error" role="alert">{formError}</p> : null}
          <button type="submit" className="primary-btn" disabled={submitting}>
            {submitting ? 'Saving…' : editingId === null ? 'Create board' : 'Update board'}
          </button>
          <hr className="divider" />
        </form>
      ) : null}

      <ActionFeedback message={actionMessage} error={actionError} />

      <AdminDataTable<BoardDto>
        endpoint="/admin/boards"
        initialSort="id"
        initialOrder="asc"
        searchPlaceholder="Search boards…"
        columns={boardColumns}
        refreshKey={refreshKey}
        rowActions={(b) => (
          <>
            <Link className="text-button" to={`/b/${b.slug}`}>View</Link>
            {canManageBoards ? <button type="button" className="text-button" onClick={() => openEdit(b)}>Edit</button> : null}
            {canManageBoards ? <button type="button" className="text-button danger" onClick={() => void deleteBoard(b)}>Delete</button> : null}
          </>
        )}
      />
    </section>
  );
}

export function AdminUsersPage() {
  const [editingId, setEditingId] = useState<number | null>(null);
  const [formData, setFormData] = useState<AdminUserDto | null>(null);
  const [formError, setFormError] = useState('');
  const [actionMessage, setActionMessage] = useState('');
  const [actionError, setActionError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  const openEdit = async (user: AdminUserDto) => {
    try {
      const { data } = await api.get<AdminUserDto>(`/admin/users/${user.id}`);
      setEditingId(user.id);
      setFormData(data);
      setFormError('');
    } catch (err) {
      setActionError(messageFromError(err, 'User could not be loaded.'));
    }
  };

  const closeForm = () => {
    setEditingId(null);
    setFormData(null);
    setFormError('');
  };

  const submitEdit = async (event: FormEvent) => {
    event.preventDefault();
    if (!formData) return;
    setSubmitting(true);
    setFormError('');
    setActionMessage('');
    setActionError('');
    try {
      await api.put(`/admin/users/${editingId}`, {
        fullName: formData.fullName ?? null,
        birthYear: formData.birthYear ?? null,
        address: formData.address ?? null,
        phone: formData.phone ?? null,
      });
      setActionMessage(`User ${formData.username} updated.`);
      closeForm();
      setRefreshKey((key) => key + 1);
    } catch (err) {
      setFormError(messageFromError(err, 'User could not be updated.'));
    } finally {
      setSubmitting(false);
    }
  };

  const deleteUser = async (user: AdminUserDto) => {
    if (!window.confirm(`Delete user ${user.username}? Their threads and replies will remain as "[deleted]" and their account will be unrecoverable.`)) return;
    setActionMessage('');
    setActionError('');
    try {
      await api.delete(`/admin/users/${user.id}`);
      setActionMessage(`User ${user.username} deleted.`);
      setRefreshKey((key) => key + 1);
    } catch (err) {
      setActionError(messageFromError(err, 'User could not be deleted.'));
    }
  };

  // X.5 — khóa quyền kháng cáo: users.status = banned_permanently.
  const lockAppeal = async (user: AdminUserDto) => {
    if (!window.confirm(`Lock the appeal privilege for ${user.username}? They will no longer be able to submit a ban appeal.`)) return;
    setActionMessage('');
    setActionError('');
    try {
      await api.post(`/admin/users/${user.id}/lock-appeal`);
      setActionMessage(`Appeal privilege locked for ${user.username}.`);
      setRefreshKey((key) => key + 1);
    } catch (err) {
      setActionError(messageFromError(err, 'Appeal privilege could not be locked.'));
    }
  };

  const userColumns: AdminColumn<AdminUserDto>[] = [
    { key: 'id', label: 'ID', sortable: true },
    { key: 'username', label: 'Username', sortable: true },
    { key: 'email', label: 'Email', sortable: true },
    { key: 'fullName', label: 'Full name', sortable: true, render: (u) => u.fullName ?? '—' },
    { key: 'birthYear', label: 'Birth year', sortable: true, render: (u) => u.birthYear ?? '—' },
    { key: 'status', label: 'Status', render: (u) => <span className={`status-pill status-${u.status}`}>{u.status}</span> },
    { key: 'createdAt', label: 'Created', sortable: true, render: (u) => formatDate(u.createdAt) },
  ];

  return (
    <section className="admin-page">
      <header className="section-head">
        <h2>Users</h2>
        <span className="hint">Deleting a user keeps their threads and replies, shown as "[deleted]".</span>
      </header>

      {editingId !== null && formData ? (
        <form className="form-stack admin-form" onSubmit={submitEdit}>
          <div className="field">
            <label htmlFor="admin-user-username">Username (read-only)</label>
            <input id="admin-user-username" type="text" value={formData.username} disabled />
          </div>
          <div className="field">
            <label htmlFor="admin-user-email">Email (read-only)</label>
            <input id="admin-user-email" type="text" value={formData.email} disabled />
          </div>
          <div className="field">
            <label htmlFor="admin-user-fullname">Full name</label>
            <input
              id="admin-user-fullname"
              type="text"
              maxLength={100}
              value={formData.fullName ?? ''}
              onChange={(event) => setFormData((prev) => prev ? { ...prev, fullName: event.target.value || null } : prev)}
            />
          </div>
          <div className="field">
            <label htmlFor="admin-user-birthyear">Birth year</label>
            <input
              id="admin-user-birthyear"
              type="number"
              min={1900}
              max={new Date().getFullYear()}
              value={formData.birthYear ?? ''}
              onChange={(event) => setFormData((prev) => prev ? { ...prev, birthYear: event.target.value ? Number(event.target.value) : null } : prev)}
            />
          </div>
          <div className="field">
            <label htmlFor="admin-user-address">Address</label>
            <input
              id="admin-user-address"
              type="text"
              maxLength={255}
              value={formData.address ?? ''}
              onChange={(event) => setFormData((prev) => prev ? { ...prev, address: event.target.value || null } : prev)}
            />
          </div>
          <div className="field">
            <label htmlFor="admin-user-phone">Phone</label>
            <input
              id="admin-user-phone"
              type="text"
              maxLength={20}
              value={formData.phone ?? ''}
              onChange={(event) => setFormData((prev) => prev ? { ...prev, phone: event.target.value || null } : prev)}
            />
          </div>
          {formError ? <p className="field-error" role="alert">{formError}</p> : null}
          <div className="inline-actions">
            <button type="submit" className="primary-btn" disabled={submitting}>{submitting ? 'Saving…' : 'Save changes'}</button>
            <button type="button" className="text-button" onClick={closeForm}>Cancel</button>
          </div>
          <hr className="divider" />
        </form>
      ) : null}

      <ActionFeedback message={actionMessage} error={actionError} />

      <AdminDataTable<AdminUserDto>
        endpoint="/admin/users"
        initialSort="id"
        initialOrder="desc"
        searchPlaceholder="Search by username, email, name…"
        columns={userColumns}
        refreshKey={refreshKey}
        rowActions={(u) => (
          <>
            <button type="button" className="text-button" onClick={() => void openEdit(u)}>Edit</button>
            <button
              type="button"
              className="text-button"
              disabled={u.status === 'banned_permanently'}
              onClick={() => void lockAppeal(u)}
            >Lock appeal</button>
            <button type="button" className="text-button danger" onClick={() => void deleteUser(u)}>Delete</button>
          </>
        )}
      />
    </section>
  );
}

export function AdminAdminsPage() {
  const { user } = useAuth();
  const currentAdminId = user && ADMIN_ROLES.includes(user.role) ? user.id : null;
  const [showForm, setShowForm] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [editingRole, setEditingRole] = useState('');
  const [formData, setFormData] = useState({ username: '', email: '', role: 'moderator', password: '' });
  const [formError, setFormError] = useState('');
  const [actionMessage, setActionMessage] = useState('');
  const [actionError, setActionError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  const openCreate = () => {
    setEditingId(null);
    setFormData({ username: '', email: '', role: 'moderator', password: '' });
    setFormError('');
    setShowForm(true);
  };

  const openEdit = (acc: AdminAccountDto) => {
    setEditingId(acc.id);
    setEditingRole(acc.role);
    setFormData({ username: acc.username, email: acc.email, role: acc.role, password: '' });
    setFormError('');
    setShowForm(true);
  };

  const closeForm = () => {
    setShowForm(false);
    setEditingId(null);
    setFormError('');
  };

  const submitForm = async (event: FormEvent) => {
    event.preventDefault();
    setSubmitting(true);
    setFormError('');
    setActionMessage('');
    setActionError('');
    try {
      if (editingId === null) {
        await api.post('/admin/admins', {
          username: formData.username,
          email: formData.email,
          role: formData.role,
          password: formData.password,
        });
        setActionMessage(`Admin account "${formData.username}" created.`);
      } else {
        const payload: { username?: string; email?: string; password?: string } = {};
        if (formData.username.trim()) payload.username = formData.username.trim();
        if (formData.email.trim()) payload.email = formData.email.trim();
        if (formData.password) payload.password = formData.password;
        await api.put(`/admin/admins/${editingId}`, payload);
        // Đổi cấp đi qua endpoint riêng (có guard super admin cuối + audit log).
        if (formData.role !== editingRole) await api.patch(`/admin/admins/${editingId}/role`, { role: formData.role });
        setActionMessage(`Admin account "${formData.username}" updated.`);
      }
      closeForm();
      setRefreshKey((key) => key + 1);
    } catch (err) {
      setFormError(messageFromError(err, 'Admin account could not be saved.'));
    } finally {
      setSubmitting(false);
    }
  };

  const deleteAdmin = async (acc: AdminAccountDto) => {
    if (acc.id === currentAdminId) {
      setActionError('You cannot delete your own admin account. Use another super admin account instead.');
      return;
    }
    if (acc.role === 'super_admin') {
      if (!window.confirm(`Remove super admin ${acc.username}? Note: the last super admin cannot be removed.`)) return;
    } else if (!window.confirm(`Remove admin ${acc.username}? This action cannot be undone.`)) return;
    setActionMessage('');
    setActionError('');
    try {
      await api.delete(`/admin/admins/${acc.id}`);
      setActionMessage(`Admin account "${acc.username}" removed.`);
      setRefreshKey((key) => key + 1);
    } catch (err) {
      setActionError(messageFromError(err, 'Admin account could not be removed.'));
    }
  };

  const adminColumns: AdminColumn<AdminAccountDto>[] = [
    { key: 'id', label: 'ID', sortable: true },
    { key: 'username', label: 'Username', sortable: true },
    { key: 'email', label: 'Email', sortable: true },
    { key: 'role', label: 'Role', sortable: true, render: (a) => (a.role === 'super_admin' ? <span className="capcode-admin">{roleLabel(a.role)}</span> : <span className="badge">{roleLabel(a.role)}</span>) },
    { key: 'createdAt', label: 'Created', sortable: true, render: (a) => formatDate(a.createdAt) },
  ];

  return (
    <section className="admin-page">
      <header className="section-head">
        <h2>Admins</h2>
        <span className="hint">Requires Super Admin. Threads/replies of deleted admins remain visible as "[deleted]".</span>
        <button type="button" className="primary-btn" onClick={() => (showForm ? closeForm() : openCreate())}>
          {showForm ? 'Cancel' : '+ New admin'}
        </button>
      </header>

      {showForm ? (
        <form className="form-stack admin-form" onSubmit={submitForm}>
          <div className="field">
            <label htmlFor="admin-admin-username">Username (3–32 chars)</label>
            <input
              id="admin-admin-username"
              type="text"
              minLength={3}
              maxLength={32}
              required
              value={formData.username}
              onChange={(event) => setFormData((prev) => ({ ...prev, username: event.target.value }))}
            />
          </div>
          <div className="field">
            <label htmlFor="admin-admin-email">Email</label>
            <input
              id="admin-admin-email"
              type="email"
              required
              value={formData.email}
              onChange={(event) => setFormData((prev) => ({ ...prev, email: event.target.value }))}
            />
          </div>
          <div className="field">
            <label htmlFor="admin-admin-role">Role</label>
            <select
              id="admin-admin-role"
              value={formData.role}
              onChange={(event) => setFormData((prev) => ({ ...prev, role: event.target.value }))}
            >
              {ADMIN_ROLES.map((r) => <option key={r} value={r}>{roleLabel(r)}</option>)}
            </select>
          </div>
          <div className="field">
            <label htmlFor="admin-admin-password">
              {editingId === null ? 'Password (8–64 chars)' : 'New password (leave blank to keep current)'}
            </label>
            <input
              id="admin-admin-password"
              type="password"
              minLength={editingId === null ? 8 : undefined}
              maxLength={64}
              required={editingId === null}
              value={formData.password}
              onChange={(event) => setFormData((prev) => ({ ...prev, password: event.target.value }))}
            />
          </div>
          {formError ? <p className="field-error" role="alert">{formError}</p> : null}
          <button type="submit" className="primary-btn" disabled={submitting}>
            {submitting ? 'Saving…' : editingId === null ? 'Create admin' : 'Update admin'}
          </button>
          <hr className="divider" />
        </form>
      ) : null}

      <ActionFeedback message={actionMessage} error={actionError} />

      <AdminDataTable<AdminAccountDto>
        endpoint="/admin/admins"
        initialSort="id"
        initialOrder="asc"
        searchPlaceholder="Search by username or email…"
        columns={adminColumns}
        refreshKey={refreshKey}
        rowActions={(a) => (
          <>
            <button
              type="button"
              className="text-button"
              disabled={editingId === a.id}
              onClick={() => openEdit(a)}
            >Edit</button>
            <button
              type="button"
              className="text-button danger"
              disabled={a.id === currentAdminId}
              onClick={() => void deleteAdmin(a)}
            >Remove</button>
          </>
        )}
      />
    </section>
  );
}

/** X.2 — Super Admin xem dấu vết hành động ghi; filter actorId / action / khoảng thời gian. */
export function AdminAuditLogsPage() {
  const [actorIdInput, setActorIdInput] = useState('');
  const [action, setAction] = useState('');
  const [fromInput, setFromInput] = useState('');
  const [toInput, setToInput] = useState('');
  const [applied, setApplied] = useState<{
    actorId?: number;
    action?: string;
    from?: string;
    to?: string;
  }>({});

  const applyFilters = (event: FormEvent) => {
    event.preventDefault();
    const trimmedActor = actorIdInput.trim();
    const actorId = trimmedActor ? Number(trimmedActor) : undefined;
    setApplied({
      actorId: actorId !== undefined && Number.isFinite(actorId) && actorId > 0 ? actorId : undefined,
      action: action || undefined,
      from: localInputToIso(fromInput),
      to: localInputToIso(toInput),
    });
  };

  const clearFilters = () => {
    setActorIdInput('');
    setAction('');
    setFromInput('');
    setToInput('');
    setApplied({});
  };

  const columns: AdminColumn<AuditLogDto>[] = [
    { key: 'id', label: 'ID', sortable: true },
    {
      key: 'createdAt',
      label: 'When',
      sortable: true,
      render: (log) => formatDate(log.createdAt),
    },
    {
      key: 'actorUsername',
      label: 'Actor',
      render: (log) => (log.actorUsername
        ? `${log.actorUsername} (#${log.actorAdminId})`
        : log.actorAdminId != null
          ? `#${log.actorAdminId}`
          : '[deleted]'),
    },
    {
      key: 'action',
      label: 'Action',
      sortable: true,
      render: (log) => <span className="badge">{log.action}</span>,
    },
    {
      key: 'targetType',
      label: 'Target',
      render: (log) => `${log.targetType} #${log.targetId}`,
    },
    {
      key: 'reason',
      label: 'Reason',
      render: (log) => log.reason || '—',
    },
    {
      key: 'metadata',
      label: 'Metadata',
      render: (log) => (
        <code className="audit-metadata" title={formatAuditMetadata(log.metadata)}>
          {formatAuditMetadata(log.metadata)}
        </code>
      ),
    },
  ];

  return (
    <section className="admin-page">
      <header className="section-head">
        <h2>Audit logs</h2>
        <span className="hint">Super Admin only. Metadata stores pre-change state for future rollback (X.7).</span>
      </header>

      <form className="admin-audit-filters" onSubmit={applyFilters}>
        <label className="control-field">
          <span>Actor admin ID</span>
          <input
            type="number"
            min={1}
            inputMode="numeric"
            value={actorIdInput}
            onChange={(event) => setActorIdInput(event.target.value)}
            placeholder="e.g. 1"
          />
        </label>
        <label className="control-field">
          <span>Action</span>
          <select value={action} onChange={(event) => setAction(event.target.value)}>
            <option value="">All actions</option>
            {AUDIT_ACTIONS.map((name) => (
              <option key={name} value={name}>{name}</option>
            ))}
          </select>
        </label>
        <label className="control-field">
          <span>From</span>
          <input
            type="datetime-local"
            value={fromInput}
            onChange={(event) => setFromInput(event.target.value)}
          />
        </label>
        <label className="control-field">
          <span>To</span>
          <input
            type="datetime-local"
            value={toInput}
            onChange={(event) => setToInput(event.target.value)}
          />
        </label>
        <div className="admin-audit-filter-actions">
          <button type="submit" className="primary-btn">Apply filters</button>
          <button type="button" className="secondary-btn" onClick={clearFilters}>Clear</button>
        </div>
      </form>

      <AdminDataTable<AuditLogDto>
        key={JSON.stringify(applied)}
        endpoint="/admin/audit-logs"
        initialSort="createdAt"
        initialOrder="desc"
        hideSearch
        extraParams={applied}
        columns={columns}
      />
    </section>
  );
}

/** X.3 — Report Queue (Janitor+). Lọc theo ?status; Moderator dùng ?status=escalated. */
export function AdminReportsPage() {
  const [statusFilter, setStatusFilter] = useState('');
  const [targetTypeFilter, setTargetTypeFilter] = useState('');
  const [applied, setApplied] = useState<{ status?: string; targetType?: string }>({});
  const [actionMessage, setActionMessage] = useState('');
  const [actionError, setActionError] = useState('');
  const [refreshKey, setRefreshKey] = useState(0);

  const applyFilters = (event: FormEvent) => {
    event.preventDefault();
    setApplied({ status: statusFilter || undefined, targetType: targetTypeFilter || undefined });
  };

  const clearFilters = () => {
    setStatusFilter('');
    setTargetTypeFilter('');
    setApplied({});
  };

  const escalate = async (report: AdminReportDto) => {
    setActionMessage('');
    setActionError('');
    try {
      await api.patch(`/admin/reports/${report.id}/escalate`);
      setActionMessage(`Report #${report.id} escalated to moderators.`);
      setRefreshKey((key) => key + 1);
    } catch (err) {
      setActionError(messageFromError(err, 'Report could not be escalated.'));
    }
  };

  const resolve = async (report: AdminReportDto) => {
    setActionMessage('');
    setActionError('');
    try {
      await api.patch(`/admin/reports/${report.id}/resolve`);
      setActionMessage(`Report #${report.id} resolved.`);
      setRefreshKey((key) => key + 1);
    } catch (err) {
      setActionError(messageFromError(err, 'Report could not be resolved.'));
    }
  };

  const columns: AdminColumn<AdminReportDto>[] = [
    { key: 'id', label: 'ID', sortable: true },
    { key: 'createdAt', label: 'Reported', sortable: true, render: (r) => formatDate(r.createdAt) },
    {
      key: 'targetType',
      label: 'Target',
      render: (r) => `${r.targetType} #${r.targetId}`,
    },
    {
      key: 'reporter',
      label: 'Reporter',
      render: (r) => (r.reporterUsername
        ? `${r.reporterUsername} (#${r.reporterUserId})`
        : r.reporterUserId != null ? `#${r.reporterUserId}` : 'guest'),
    },
    { key: 'reason', label: 'Reason', render: (r) => r.reason || '—' },
    {
      key: 'status',
      label: 'Status',
      sortable: true,
      render: (r) => <span className={`status-pill status-${r.status}`}>{r.status}</span>,
    },
  ];

  return (
    <section className="admin-page">
      <header className="section-head">
        <h2>Reports</h2>
        <span className="hint">Janitor+. Handle content with the existing hide/delete endpoints; escalating routes a report to moderators.</span>
      </header>

      <form className="admin-audit-filters" onSubmit={applyFilters}>
        <label className="control-field">
          <span>Status</span>
          <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
            <option value="">All statuses</option>
            <option value="pending">pending</option>
            <option value="escalated">escalated</option>
            <option value="resolved">resolved</option>
          </select>
        </label>
        <label className="control-field">
          <span>Target type</span>
          <select value={targetTypeFilter} onChange={(event) => setTargetTypeFilter(event.target.value)}>
            <option value="">All targets</option>
            <option value="thread">thread</option>
            <option value="reply">reply</option>
          </select>
        </label>
        <div className="admin-audit-filter-actions">
          <button type="submit" className="primary-btn">Apply filters</button>
          <button type="button" className="secondary-btn" onClick={clearFilters}>Clear</button>
        </div>
      </form>

      <ActionFeedback message={actionMessage} error={actionError} />

      <AdminDataTable<AdminReportDto>
        key={JSON.stringify(applied)}
        endpoint="/admin/reports"
        initialSort="createdAt"
        initialOrder="desc"
        hideSearch
        extraParams={applied}
        refreshKey={refreshKey}
        columns={columns}
        rowActions={(r) => (
          <>
            <button
              type="button"
              className="text-button"
              disabled={r.status !== 'pending'}
              onClick={() => void escalate(r)}
            >Escalate</button>
            <button
              type="button"
              className="text-button"
              disabled={r.status === 'resolved'}
              onClick={() => void resolve(r)}
            >Resolve</button>
          </>
        )}
      />
    </section>
  );
}

/** X.4 — Ban system (Moderator+). IP lấy từ bài bị report, hoặc nhập tay. */
export function AdminBansPage() {
  const [showForm, setShowForm] = useState(false);
  const [formData, setFormData] = useState({ userId: '', reason: '', threadId: '', replyId: '', bannedIp: '' });
  const [formError, setFormError] = useState('');
  const [actionMessage, setActionMessage] = useState('');
  const [actionError, setActionError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);
  const [statusFilter, setStatusFilter] = useState('');
  const [applied, setApplied] = useState<{ status?: string }>({});

  const openCreate = () => {
    setFormData({ userId: '', reason: '', threadId: '', replyId: '', bannedIp: '' });
    setFormError('');
    setShowForm(true);
  };

  const submitForm = async (event: FormEvent) => {
    event.preventDefault();
    const userId = Number(formData.userId);
    if (!Number.isInteger(userId) || userId < 1) {
      setFormError('A valid user ID is required.');
      return;
    }
    if (!formData.reason.trim()) {
      setFormError('A reason is required.');
      return;
    }
    const payload: { userId: number; reason: string; threadId?: number; replyId?: number; bannedIp?: string } = {
      userId,
      reason: formData.reason.trim(),
    };
    const threadId = Number(formData.threadId);
    if (formData.threadId.trim() && Number.isInteger(threadId) && threadId > 0) payload.threadId = threadId;
    const replyId = Number(formData.replyId);
    if (formData.replyId.trim() && Number.isInteger(replyId) && replyId > 0) payload.replyId = replyId;
    if (formData.bannedIp.trim()) payload.bannedIp = formData.bannedIp.trim();

    setSubmitting(true);
    setFormError('');
    setActionMessage('');
    setActionError('');
    try {
      const { data } = await api.post<BanDto>('/admin/bans', payload);
      setActionMessage(`User #${data.userId} banned${data.bannedIp ? ` (IP ${data.bannedIp})` : ''}.`);
      setShowForm(false);
      setFormData({ userId: '', reason: '', threadId: '', replyId: '', bannedIp: '' });
      setRefreshKey((key) => key + 1);
    } catch (err) {
      setFormError(messageFromError(err, 'Ban could not be created.'));
    } finally {
      setSubmitting(false);
    }
  };

  const unban = async (ban: BanDto) => {
    if (!window.confirm(`Lift ban #${ban.id} for ${ban.username}?`)) return;
    setActionMessage('');
    setActionError('');
    try {
      await api.post(`/admin/bans/${ban.id}/unban`);
      setActionMessage(`Ban #${ban.id} lifted for ${ban.username}.`);
      setRefreshKey((key) => key + 1);
    } catch (err) {
      setActionError(messageFromError(err, 'Ban could not be lifted.'));
    }
  };

  const columns: AdminColumn<BanDto>[] = [
    { key: 'id', label: 'ID', sortable: true },
    { key: 'userId', label: 'User', sortable: true, render: (b) => `${b.username} (#${b.userId})` },
    { key: 'bannedIp', label: 'IP', render: (b) => b.bannedIp ?? '—' },
    { key: 'reason', label: 'Reason', render: (b) => b.reason ?? '—' },
    {
      key: 'bannedByAdminId',
      label: 'Banned by',
      render: (b) => (b.bannedByUsername
        ? `${b.bannedByUsername} (#${b.bannedByAdminId})`
        : b.bannedByAdminId != null ? `#${b.bannedByAdminId}` : '—'),
    },
    {
      key: 'status',
      label: 'Status',
      sortable: true,
      render: (b) => <span className={`status-pill status-${b.status}`}>{b.status}</span>,
    },
    { key: 'createdAt', label: 'When', sortable: true, render: (b) => formatDate(b.createdAt) },
  ];

  return (
    <section className="admin-page">
      <header className="section-head">
        <h2>Bans</h2>
        <button type="button" className="primary-btn" onClick={() => (showForm ? setShowForm(false) : openCreate())}>
          {showForm ? 'Cancel' : '+ New ban'}
        </button>
      </header>

      {showForm ? (
        <form className="form-stack admin-form" onSubmit={submitForm}>
          <div className="field">
            <label htmlFor="admin-ban-user">User ID</label>
            <input
              id="admin-ban-user"
              type="number"
              min={1}
              required
              value={formData.userId}
              onChange={(event) => setFormData((prev) => ({ ...prev, userId: event.target.value }))}
            />
          </div>
          <div className="field">
            <label htmlFor="admin-ban-reason">Reason</label>
            <textarea
              id="admin-ban-reason"
              rows={2}
              maxLength={500}
              required
              value={formData.reason}
              onChange={(event) => setFormData((prev) => ({ ...prev, reason: event.target.value }))}
            />
          </div>
          <div className="field">
            <label htmlFor="admin-ban-thread">Thread ID (optional — captures the poster IP)</label>
            <input
              id="admin-ban-thread"
              type="number"
              min={1}
              value={formData.threadId}
              onChange={(event) => setFormData((prev) => ({ ...prev, threadId: event.target.value }))}
            />
          </div>
          <div className="field">
            <label htmlFor="admin-ban-reply">Reply ID (optional — captures the poster IP)</label>
            <input
              id="admin-ban-reply"
              type="number"
              min={1}
              value={formData.replyId}
              onChange={(event) => setFormData((prev) => ({ ...prev, replyId: event.target.value }))}
            />
          </div>
          <div className="field">
            <label htmlFor="admin-ban-ip">IP to ban (optional, used when there is no post)</label>
            <input
              id="admin-ban-ip"
              type="text"
              maxLength={45}
              value={formData.bannedIp}
              onChange={(event) => setFormData((prev) => ({ ...prev, bannedIp: event.target.value }))}
            />
          </div>
          {formError ? <p className="field-error" role="alert">{formError}</p> : null}
          <button type="submit" className="primary-btn" disabled={submitting}>
            {submitting ? 'Saving…' : 'Create ban'}
          </button>
          <hr className="divider" />
        </form>
      ) : null}

      <ActionFeedback message={actionMessage} error={actionError} />

      <form className="admin-audit-filters" onSubmit={(event) => { event.preventDefault(); setApplied({ status: statusFilter || undefined }); }}>
        <label className="control-field">
          <span>Status</span>
          <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
            <option value="">All statuses</option>
            <option value="active">active</option>
            <option value="lifted">lifted</option>
          </select>
        </label>
        <div className="admin-audit-filter-actions">
          <button type="submit" className="primary-btn">Apply filters</button>
          <button type="button" className="secondary-btn" onClick={() => { setStatusFilter(''); setApplied({}); }}>Clear</button>
        </div>
      </form>

      <AdminDataTable<BanDto>
        key={JSON.stringify(applied)}
        endpoint="/admin/bans"
        initialSort="createdAt"
        initialOrder="desc"
        hideSearch
        extraParams={applied}
        refreshKey={refreshKey}
        columns={columns}
        rowActions={(b) => (
          b.status === 'active'
            ? <button type="button" className="text-button" onClick={() => void unban(b)}>Unban</button>
            : <span className="hint">Lifted</span>
        )}
      />
    </section>
  );
}

/** X.5 — hàng chờ kháng cáo (Moderator+). Approve mở ban ngay; reject giữ nguyên và khóa nộp lại. */
export function AdminAppealsPage() {
  const [statusFilter, setStatusFilter] = useState('');
  const [applied, setApplied] = useState<{ status?: string }>({});
  const [actionMessage, setActionMessage] = useState('');
  const [actionError, setActionError] = useState('');
  const [refreshKey, setRefreshKey] = useState(0);

  const review = async (appeal: AppealDto, decision: 'approve' | 'reject') => {
    const verb = decision === 'approve' ? 'Approve' : 'Reject';
    if (!window.confirm(`${verb} appeal #${appeal.id} from ${appeal.username}?`)) return;
    setActionMessage('');
    setActionError('');
    try {
      await api.patch(`/admin/appeals/${appeal.id}`, { decision });
      setActionMessage(decision === 'approve'
        ? `Appeal #${appeal.id} approved. The ban for ${appeal.username} was lifted.`
        : `Appeal #${appeal.id} rejected.`);
      setRefreshKey((key) => key + 1);
    } catch (err) {
      setActionError(messageFromError(err, 'Appeal could not be reviewed.'));
    }
  };

  const columns: AdminColumn<AppealDto>[] = [
    { key: 'id', label: 'ID', sortable: true },
    { key: 'banId', label: 'Ban', render: (a) => `#${a.banId}` },
    { key: 'username', label: 'User', render: (a) => `${a.username} (#${a.userId})` },
    { key: 'bannedIp', label: 'IP', render: (a) => a.bannedIp ?? '—' },
    { key: 'reasonText', label: 'Reason', render: (a) => a.reasonText || '—' },
    {
      key: 'status',
      label: 'Status',
      sortable: true,
      render: (a) => <span className={`status-pill status-${a.status}`}>{a.status}</span>,
    },
    {
      key: 'reviewedByAdminId',
      label: 'Reviewed by',
      render: (a) => (a.reviewedByUsername
        ? `${a.reviewedByUsername} (#${a.reviewedByAdminId})`
        : a.reviewedByAdminId != null ? `#${a.reviewedByAdminId}` : '—'),
    },
    { key: 'createdAt', label: 'Submitted', sortable: true, render: (a) => formatDate(a.createdAt) },
  ];

  return (
    <section className="admin-page">
      <header className="section-head">
        <h2>Appeals</h2>
        <span className="hint">Moderator+. Approving an appeal lifts the ban and reactivates the account.</span>
      </header>

      <form className="admin-audit-filters" onSubmit={(event) => { event.preventDefault(); setApplied({ status: statusFilter || undefined }); }}>
        <label className="control-field">
          <span>Status</span>
          <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
            <option value="">All statuses</option>
            <option value="pending">pending</option>
            <option value="approved">approved</option>
            <option value="rejected">rejected</option>
          </select>
        </label>
        <div className="admin-audit-filter-actions">
          <button type="submit" className="primary-btn">Apply filters</button>
          <button type="button" className="secondary-btn" onClick={() => { setStatusFilter(''); setApplied({}); }}>Clear</button>
        </div>
      </form>

      <ActionFeedback message={actionMessage} error={actionError} />

      <AdminDataTable<AppealDto>
        key={JSON.stringify(applied)}
        endpoint="/admin/appeals"
        initialSort="id"
        initialOrder="desc"
        hideSearch
        extraParams={applied}
        refreshKey={refreshKey}
        columns={columns}
        rowActions={(a) => (
          a.status === 'pending'
            ? (
              <>
                <button type="button" className="text-button" onClick={() => void review(a, 'approve')}>Approve</button>
                <button type="button" className="text-button danger" onClick={() => void review(a, 'reject')}>Reject</button>
              </>
            )
            : <span className="hint">Reviewed</span>
        )}
      />
    </section>
  );
}

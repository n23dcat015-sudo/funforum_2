import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { Link, NavLink, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { api } from '../lib/api';
import { formatDate, formatFileSize, isBannedStatus, messageFromError } from '../lib/format';
import { useAuth } from '../context/useAuth';
import { isAdminRole } from '../context/roles';
import {
  AttachmentImage,
  AuthorLabel,
  CharacterCountTextarea,
  FilterControls,
  ImageUpload,
  Pagination,
  PlainText,
  ReportButton,
  SearchBar,
  SortDropdown,
  ViewRepliesButton,
  VoteButton,
} from '../components/CoreComponents';
import type {
  BoardDto,
  MeDto,
  MyAppealDto,
  NotificationDto,
  PagedResult,
  ReplyDto,
  ThreadDto,
  VoteResultDto,
} from '../types/api';

const THREAD_SORT_OPTIONS = [
  { value: 'latest', label: 'Latest activity' },
  { value: 'new', label: 'Newest' },
  { value: 'top', label: 'Top' },
  { value: 'hot', label: 'Hot' },
];

const TIME_OPTIONS = [
  { value: '', label: 'All time' },
  { value: 'day', label: 'Past day' },
  { value: 'week', label: 'Past week' },
  { value: 'month', label: 'Past month' },
];

export function HomePage() {
  const [boards, setBoards] = useState<BoardDto[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const controller = new AbortController();
    api.get<BoardDto[]>('/boards', { signal: controller.signal })
      .then(({ data }) => setBoards(data))
      .catch(() => { if (!controller.signal.aborted) setError('Boards could not be loaded.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, []);

  const boardsPerColumn = 20;
  const columnCount = Math.max(1, Math.ceil(boards.length / boardsPerColumn));
  const columns = Array.from({ length: columnCount }, (_, columnIndex) =>
    boards.slice(columnIndex * boardsPerColumn, (columnIndex + 1) * boardsPerColumn));

  return (
    <div className="content-stack home-layout">
      <div className="about"><p>Click a board that interests you.</p></div>
      {error ? <p className="field-error" role="alert">{error}</p> : null}
      {loading ? <p role="status">Loading boards…</p> : null}
      {boards.length ? (
        <table className="board-table">
          <tbody>
            <tr>
              {columns.map((column, index) => (
                <td key={`board-column-${index}`} style={{ width: `${100 / columnCount}%` }}>
                  {column.map((board) => <Link key={board.id} to={`/b/${board.slug}`}>{board.name}</Link>)}
                </td>
              ))}
            </tr>
          </tbody>
        </table>
      ) : null}
      {!loading && !error && boards.length === 0 ? <p>No boards have been created yet.</p> : null}
      <div className="counter">
        <div className="counter-info">
          <span>Visitors online: 1234</span>
          <span>Total visitors: 5678</span>
          <div className="marquee-stream" aria-label="Visitor notice">Welcome to Funforum.</div>
        </div>
      </div>
    </div>
  );
}

function ThreadPreview({ thread }: { thread: ThreadDto }) {
  const { user } = useAuth();
  const [score, setScore] = useState(thread.score);
  const vote = async () => {
    const { data } = await api.put<VoteResultDto>(`/threads/${thread.id}/vote`, { value: 1 });
    setScore(data.score);
  };

  return (
    <article className="thread-preview">
      <div className="thread-preview-main">
        {thread.image ? (
          <AttachmentImage
            url={thread.image.url}
            thumbUrl={thread.image.thumbUrl}
            alt={thread.image.originalName}
            caption={`File: ${thread.image.originalName} (${formatFileSize(thread.image.sizeBytes)}, ${thread.image.width}×${thread.image.height})`}
          />
        ) : null}
        <div className="thread-preview-content">
          <header className="thread-preview-head">
            <AuthorLabel name={thread.authorName} capcode={thread.capcode} />
            <time dateTime={thread.createdAt}>{formatDate(thread.createdAt)}</time>
          </header>
          <h3 className="thread-preview-title"><Link to={`/b/${thread.boardSlug}/thread/${thread.id}`}>{thread.title}</Link></h3>
          <PlainText>{thread.content}</PlainText>
        </div>
      </div>
      {thread.previewReplies?.length ? (
        <div className="preview-replies">
          {thread.previewReplies.map((reply) => (
            <div className="preview-reply" key={reply.id}>
              <AuthorLabel name={reply.authorName} capcode={reply.capcode} />
              <PlainText>{reply.content}</PlainText>
            </div>
          ))}
        </div>
      ) : null}
      <p className="omitted-count">{thread.omittedReplies ?? 0} replies and {thread.omittedImages ?? 0} images omitted</p>
      <footer className="thread-preview-actions">
        <Link to={`/b/${thread.boardSlug}/thread/${thread.id}`}>Open thread · {thread.replyCount} replies</Link>
        {user?.role === 'user' ? <VoteButton score={score} onVote={vote} /> : null}
        <ReportButton targetType="thread" targetId={thread.id} />
      </footer>
    </article>
  );
}

export function BoardPage() {
  const { boardSlug = '' } = useParams();
  const [searchParams, setSearchParams] = useSearchParams();
  const page = Math.max(1, Number(searchParams.get('page') ?? 1));
  const query = searchParams.get('q') ?? '';
  const sort = searchParams.get('sort') ?? 'latest';
  const timeframe = searchParams.get('t') ?? '';
  const [result, setResult] = useState<PagedResult<ThreadDto> | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const controller = new AbortController();
    api.get<PagedResult<ThreadDto>>(`/boards/${encodeURIComponent(boardSlug)}/threads`, {
      params: { page, pageSize: 10, q: query || undefined, sort, t: sort === 'top' ? timeframe || undefined : undefined },
      signal: controller.signal,
    }).then(({ data }) => setResult(data)).catch((loadError: unknown) => {
      if (!controller.signal.aborted) setError(messageFromError(loadError, 'This board could not be loaded.'));
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [boardSlug, page, query, sort, timeframe]);

  const updateParams = (patch: Record<string, string>) => {
    const next = new URLSearchParams(searchParams);
    Object.entries(patch).forEach(([key, value]) => value ? next.set(key, value) : next.delete(key));
    if (!('page' in patch)) next.delete('page');
    setSearchParams(next);
  };

  return (
    <div className="content-stack board-page">
  
      <div className="board-controls">
        <SearchBar key={query} initialValue={query} onSearch={(value) => updateParams({ q: value })} />
        <SortDropdown value={sort} options={THREAD_SORT_OPTIONS} onChange={(value) => updateParams({ sort: value })} />
        {sort === 'top' ? <FilterControls value={timeframe} options={TIME_OPTIONS} onChange={(value) => updateParams({ t: value })} /> : null}
      </div>
      {loading ? <p role="status">Loading threads…</p> : null}
      {error ? <p className="field-error" role="alert">{error}</p> : null}
      {result?.items.map((thread) => <ThreadPreview key={thread.id} thread={thread} />)}
      {result && !result.items.length ? <p>No threads match this board and search.</p> : null}
      {result ? <Pagination page={result.page} totalPages={result.totalPages} onPageChange={(nextPage) => updateParams({ page: String(nextPage) })} /> : null}
    </div>
  );
}

export function NewThreadPage() {
  const { boardSlug = '' } = useParams();
  const { user } = useAuth();
  const navigate = useNavigate();
  const [files, setFiles] = useState<File[]>([]);
  const [content, setContent] = useState('');
  const [title, setTitle] = useState('');
  const [useCapcode, setUseCapcode] = useState(false);
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData();
    form.set('title', title);
    form.set('content', content);
    form.set('useCapcode', String(useCapcode));
    if (files[0]) form.set('image', files[0]);
    setSubmitting(true);
    setError('');
    try {
      const { data } = await api.post<ThreadDto>(`/boards/${encodeURIComponent(boardSlug)}/threads`, form);
      navigate(`/b/${boardSlug}/thread/${data.id}`);
    } catch (submitError) {
      setError(messageFromError(submitError, 'Thread could not be created.'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <section className="page-card">
      <h2>Start a thread in /{boardSlug}/</h2>
      <form className="inline-form" onSubmit={(event) => void submit(event)}>
        <div className="field"><label htmlFor="thread-title">Title</label><input id="thread-title" value={title} maxLength={200} required onChange={(event) => setTitle(event.target.value)} /></div>
        <div className="field"><label htmlFor="thread-content">Message</label><CharacterCountTextarea id="thread-content" value={content} maxLength={10000} onChange={setContent} /></div>
        <ImageUpload files={files} onChange={setFiles} allowMultiple={false} />
        {user && user.role !== 'user' ? <label className="check-field"><input type="checkbox" checked={useCapcode} onChange={(event) => setUseCapcode(event.target.checked)} /> Use role capcode</label> : null}
        {error ? <p className="field-error" role="alert">{error}</p> : null}
        <button className="primary-btn" type="submit" disabled={submitting}>{submitting ? 'Posting…' : 'Post thread'}</button>
      </form>
    </section>
  );
}

function ReplyCard({ reply, thread, isAdmin, onReload, onReplyTo }: {
  reply: ReplyDto;
  thread: ThreadDto;
  isAdmin: boolean;
  onReload: () => Promise<void>;
  onReplyTo: (reply: ReplyDto) => void;
}) {
  const { user } = useAuth();
  const canDelete = reply.canDelete;
  const canManageVisibility = isAdmin || thread.isMine;
  const [score, setScore] = useState(reply.score);
  const [actionError, setActionError] = useState('');
  const vote = async () => {
    const { data } = await api.put<VoteResultDto>(`/replies/${reply.id}/vote`, { value: 1 });
    setScore(data.score);
  };
  const deleteReply = async (target: ReplyDto) => {
    const warning = target.childCount > 0
      ? `Delete this reply and its ${target.childCount} replies visible to you? This cannot be undone.`
      : 'Delete this reply and any replies beneath it? This cannot be undone.';
    if (!window.confirm(warning)) return;
    try {
      await api.delete(`/replies/${target.id}`);
      await onReload();
    } catch (error) { setActionError(messageFromError(error, 'Reply could not be deleted.')); }
  };
  const toggleVisibility = async (target: ReplyDto) => {
    const nextStatus = target.status === 'hidden' ? 'visible' : 'hidden';
    if (!window.confirm(`${nextStatus === 'hidden' ? 'Hide' : 'Show'} this reply branch?`)) return;
    try {
      await api.patch(`/replies/${target.id}/status`, { status: nextStatus });
      await onReload();
    } catch (error) { setActionError(messageFromError(error, 'Reply visibility could not be changed.')); }
  };
  const actionsFor = (target: ReplyDto) => {
    const targetCanDelete = target.canDelete;
    const targetCanManageVisibility = isAdmin || thread.isMine;
    return <>
      {targetCanManageVisibility ? <button className="text-button" type="button" disabled={target.hiddenBy === 'admin' && !isAdmin} onClick={() => void toggleVisibility(target)}>{target.status === 'hidden' ? 'Unhide' : 'Hide'}</button> : null}
      {targetCanDelete ? <button className="text-button danger-action" type="button" onClick={() => void deleteReply(target)}>Delete</button> : null}
      <ReportButton targetType="reply" targetId={target.id} />
    </>;
  };

  return (
    <article id={`reply-${reply.id}`} className={`reply-card${reply.isHidden ? ' reply-hidden' : ''}`}>
      <header className="reply-head">
        <AuthorLabel name={reply.authorName} capcode={reply.capcode} />
        <time dateTime={reply.createdAt}>{formatDate(reply.createdAt)}</time>
        {reply.isHidden ? <span className="hidden-badge">Hidden</span> : null}
      </header>
      {reply.replyToUsername ? <p className="reply-to">@{reply.replyToUsername}</p> : null}
      {reply.content ? <PlainText>{reply.content}</PlainText> : null}
      {reply.image ? <AttachmentImage url={reply.image.url} thumbUrl={reply.image.thumbUrl} alt={reply.image.originalName} caption={reply.image.originalName} /> : null}
      <div className="reply-actions">
        <button className="text-button" type="button" onClick={() => onReplyTo(reply)}>Reply</button>
        {user?.role === 'user' ? <VoteButton score={score} onVote={vote} /> : null}
        {canManageVisibility ? <button className="text-button" type="button" disabled={reply.hiddenBy === 'admin' && !isAdmin} onClick={() => void toggleVisibility(reply)}>{reply.status === 'hidden' ? 'Unhide' : 'Hide'}</button> : null}
        {canDelete ? <button className="text-button danger-action" type="button" onClick={() => void deleteReply(reply)}>Delete</button> : null}
        <ReportButton targetType="reply" targetId={reply.id} />
      </div>
      {reply.previewChildren?.map((child) => (
        <div className="preview-child" key={child.id}>
          <AuthorLabel name={child.authorName} capcode={child.capcode} />
          {child.isHidden ? <span className="hidden-badge">Hidden</span> : null}
          {child.replyToUsername ? <span className="reply-to">@{child.replyToUsername}</span> : null}
          {child.content ? <PlainText>{child.content}</PlainText> : null}
          {child.image ? <AttachmentImage url={child.image.url} thumbUrl={child.image.thumbUrl} alt={child.image.originalName} caption={child.image.originalName} /> : null}
          <div className="reply-actions">
            <button className="text-button" type="button" onClick={() => onReplyTo(child)}>Reply</button>
            {user?.role === 'user' ? <VoteButton score={child.score} onVote={async () => { await api.put(`/replies/${child.id}/vote`, { value: 1 }); await onReload(); }} /> : null}
            {actionsFor(child)}
          </div>
        </div>
      ))}
      {reply.parentReplyId === null && reply.childCount > (reply.previewChildren?.length ?? 0) ? (
        <ViewRepliesButton replyId={reply.id} excludeReplyIds={reply.previewChildren?.map((child) => child.id)} onReplyTo={onReplyTo} renderActions={actionsFor} onVote={user?.role === 'user' ? async (childId) => {
          const { data } = await api.put<VoteResultDto>(`/replies/${childId}/vote`, { value: 1 });
          await onReload();
          return data;
        } : undefined}>
          View {Math.max(0, reply.childCount - (reply.previewChildren?.length ?? 0))} more replies
        </ViewRepliesButton>
      ) : null}
      {actionError ? <p className="field-error" role="alert">{actionError}</p> : null}
    </article>
  );
}

export function ThreadPage() {
  const { threadId: rawThreadId } = useParams();
  const threadId = Number(rawThreadId);
  const { user, token } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const [thread, setThread] = useState<ThreadDto | null>(null);
  const [replies, setReplies] = useState<PagedResult<ReplyDto> | null>(null);
  const [targetReply, setTargetReply] = useState<ReplyDto | null>(null);
  const [page, setPage] = useState(1);
  const [replyTarget, setReplyTarget] = useState<ReplyDto | null>(null);
  const [replyContent, setReplyContent] = useState('');
  const [showReplyForm, setShowReplyForm] = useState(false);
  const [replyFiles, setReplyFiles] = useState<File[]>([]);
  const [useCapcode, setUseCapcode] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  const loadThread = useCallback(async () => {
    if (!Number.isInteger(threadId) || threadId < 1) return;
    try {
      const [threadResponse, repliesResponse] = await Promise.all([
        api.get<ThreadDto>(`/threads/${threadId}`),
        api.get<PagedResult<ReplyDto>>(`/threads/${threadId}/replies`, { params: { page, pageSize: 20 } }),
      ]);
      setThread(threadResponse.data);
      setReplies(repliesResponse.data);
      setError('');
    } catch (loadError) {
      setError(messageFromError(loadError, 'Thread could not be loaded.'));
    } finally {
      setLoading(false);
    }
  }, [threadId, page]);

  useEffect(() => { queueMicrotask(() => void loadThread()); }, [loadThread, token]);
  useEffect(() => {
    const replyId = Number(location.hash.match(/^#reply-(\d+)$/)?.[1]);
    if (!replyId) return;
    api.get<ReplyDto>(`/replies/${replyId}`)
      .then(({ data }) => setTargetReply(data.threadId === threadId ? data : null))
      .catch(() => setTargetReply(null));
  }, [location.hash, threadId]);
  useEffect(() => {
    if (!location.hash || !replies) return;
    document.getElementById(decodeURIComponent(location.hash.slice(1)))?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }, [location.hash, replies, targetReply]);

  const openReplyForm = (target: ReplyDto | null) => {
  setReplyTarget(target);
  setShowReplyForm(true);
};

  const closeReplyForm = () => {
    setReplyTarget(null);
    setShowReplyForm(false);
  };

  useEffect(() => {
    if (!showReplyForm) return;
    const textarea = document.getElementById('reply-content');
    textarea?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    textarea?.focus({ preventScroll: true });
  }, [showReplyForm, replyTarget]);

  const postReply = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!replyContent.trim() && !replyFiles[0]) {
      setError('Add a message or image before replying.');
      return;
    }
    const form = new FormData();
    form.set('content', replyContent);
    form.set('useCapcode', String(useCapcode));
    if (replyTarget) form.set('parentReplyId', String(replyTarget.id));
    if (replyFiles[0]) form.set('image', replyFiles[0]);
    try {
      await api.post(`/threads/${threadId}/replies`, form);
      setReplyTarget(null);
      setShowReplyForm(false);   
      setReplyContent('');
      setReplyFiles([]);
      await loadThread();
    } catch (submitError) {
      setError(messageFromError(submitError, 'Reply could not be posted.'));
    }
  };

  const deleteThread = async () => {
    if (!thread || !window.confirm(`Delete this thread and its ${thread.replyCount} replies? This cannot be undone.`)) return;
    try {
      if (isAdminRole(user?.role)) await api.delete(`/admin/threads/${thread.id}`);
      else await api.delete(`/threads/${thread.id}`);
      navigate(`/b/${thread.boardSlug}`);
    } catch (deleteError) {
      setError(messageFromError(deleteError, 'Thread could not be deleted.'));
    }
  };

  if (loading) return <p role="status">Loading thread…</p>;
  if (error && !thread) return <section className="page-card"><p className="field-error" role="alert">{error}</p></section>;
  if (!thread) return null;

  const isAdmin = isAdminRole(user?.role);
  const canDeleteThread = thread.canDelete;
  const targetAlreadyRendered = !!targetReply && !!replies?.items.some((reply) =>
    reply.id === targetReply.id || reply.previewChildren?.some((child) => child.id === targetReply.id));

  return (
    <div className="content-stack thread-page">
      <article id={`thread-${thread.id}`} className="thread-original">
        <p><Link to={`/b/${thread.boardSlug}`}>/{thread.boardSlug}/</Link></p>
        <div className="thread-original-main">
          {thread.image ? (
            <AttachmentImage
              url={thread.image.url}
              thumbUrl={thread.image.thumbUrl}
              alt={thread.image.originalName}
              caption={`File: ${thread.image.originalName} (${formatFileSize(thread.image.sizeBytes)}, ${thread.image.width}×${thread.image.height})`}
            />
          ) : null}
          <div className="thread-original-content">
            <header className="reply-head"><AuthorLabel name={thread.authorName} capcode={thread.capcode} /><time dateTime={thread.createdAt}>{formatDate(thread.createdAt)}</time></header>
            <h2>{thread.title}</h2>
            <PlainText>{thread.content}</PlainText>
          </div>
        </div>
        <div className="reply-actions">

          {user?.role === 'user' ? <VoteButton score={thread.score} onVote={async () => { await api.put(`/threads/${thread.id}/vote`, { value: 1 }); await loadThread(); }} /> : null}
          {user ? <button className="text-button" type="button" onClick={() => openReplyForm(null)}>Reply</button> : null}
          {user ? <ReportButton targetType="thread" targetId={thread.id} /> : null}
          {canDeleteThread ? <button className="text-button danger-action" type="button" onClick={() => void deleteThread()}>Delete thread</button> : null}
        </div>
      </article>

      <section className="reply-list">
        <header className="section-head"><h2>Replies ({thread.replyCount})</h2><Link to={`/b/${thread.boardSlug}`}>Back to board</Link></header>
        {replies?.items.map((reply) => (
          <ReplyCard key={reply.id} reply={reply} thread={thread} isAdmin={!!isAdmin} onReload={loadThread} onReplyTo={openReplyForm} />
        ))}
        {targetReply && targetReply.threadId === thread.id && !targetAlreadyRendered ? (
          <div className="notification-target">
            <p>Reply linked from your notification</p>
            <ReplyCard reply={targetReply} thread={thread} isAdmin={!!isAdmin} onReload={loadThread} onReplyTo={openReplyForm} />
          </div>
        ) : null}
        {replies && !replies.items.length ? <p>No replies yet.</p> : null}
        {replies ? <Pagination page={replies.page} totalPages={replies.totalPages} onPageChange={setPage} /> : null}
      </section>

      {user ? (showReplyForm ? (
        <form className="page-card inline-form reply-form" onSubmit={(event) => void postReply(event)}>
          <h3>{replyTarget ? `Replying to ${replyTarget.authorName}` : 'Write a reply'}</h3>
          {/* {replyTarget ? <button className="text-button" type="button" onClick={() => setReplyTarget(null)}>Cancel reply</button> : null} */}
          <button className="text-button" type="button" onClick={closeReplyForm}>Cancel reply</button>
          {thread.replyLimitReached ? <p className="notice">This thread has reached its reply limit.</p> : null}
          <div className="field"><label htmlFor="reply-content">Message</label><CharacterCountTextarea id="reply-content" value={replyContent} maxLength={5000} onChange={setReplyContent} /></div>
          {thread.imageLimitReached ? <p className="notice">Image limit reached. Text replies are still available.</p> : <ImageUpload files={replyFiles} onChange={setReplyFiles} allowMultiple={false} />}
          {isAdmin ? <label className="check-field"><input type="checkbox" checked={useCapcode} onChange={(event) => setUseCapcode(event.target.checked)} /> Use role capcode</label> : null}
          {error ? <p className="field-error" role="alert">{error}</p> : null}
          <button className="primary-btn" type="submit" disabled={thread.replyLimitReached}>Post reply</button>
        </form>
      ) : null) : <p className="notice">Sign in to reply. <NavLink to="/login">Log in</NavLink></p>}
    </div>
  );
}

export function AccountPage() {
  const { logout } = useAuth();
  const navigate = useNavigate();
  const [profile, setProfile] = useState<MeDto | null>(null);
  const [profileMessage, setProfileMessage] = useState('');
  const [passwordMessage, setPasswordMessage] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    api.get<MeDto>('/users/me').then(({ data }) => setProfile(data)).catch((loadError) => setError(messageFromError(loadError, 'Your profile could not be loaded.'))).finally(() => setLoading(false));
  }, []);

  const saveProfile = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!profile) return;
    setSaving(true);
    setError('');
    try {
      const { data } = await api.put<MeDto>('/users/me', {
        displayName: profile.displayName,
        fullName: profile.fullName || null,
        birthYear: profile.birthYear || null,
        address: profile.address || null,
        phone: profile.phone || null,
      });
      setProfile(data);
      setProfileMessage('Profile updated.');
    } catch (saveError) {
      setError(messageFromError(saveError, 'Profile could not be updated.'));
    } finally { setSaving(false); }
  };

  const changePassword = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    try {
      await api.put('/users/me/password', {
        currentPassword: String(form.get('currentPassword') ?? ''),
        newPassword: String(form.get('newPassword') ?? ''),
      });
      await logout().catch(() => undefined);
      navigate('/login', { replace: true });
    } catch (passwordError) {
      setPasswordMessage(messageFromError(passwordError, 'Password could not be changed.'));
    }
  };

  if (loading) return <p role="status">Loading account…</p>;
  if (!profile) return <section className="page-card"><p className="field-error" role="alert">{error || 'Profile unavailable.'}</p></section>;

  const banned = isBannedStatus(profile.status);

  return (
    <div className="content-stack account-page">
      <section className="page-card">
        <h2>Account</h2>
        <p><strong>Display name:</strong> {profile.displayName}</p>
        <p><strong>Email:</strong> {profile.email}</p>
        <p><strong>Member since:</strong> {formatDate(profile.createdAt)}</p>
        <p><strong>Status:</strong> {profile.status}</p>
        {banned ? (
          <p className="notice" role="alert">
            Your account is banned, so you cannot edit your profile.
            {' '}<NavLink to="/appeal">Go to your ban appeal</NavLink>.
          </p>
        ) : (
          <form className="inline-form" onSubmit={(event) => void saveProfile(event)}>
            <div className="field"><label htmlFor="display-name">Display name</label><input id="display-name" maxLength={100} value={profile.displayName} onChange={(event) => setProfile({ ...profile, displayName: event.target.value })} /></div>
            <div className="field"><label htmlFor="full-name">Full name</label><input id="full-name" maxLength={100} value={profile.fullName ?? ''} onChange={(event) => setProfile({ ...profile, fullName: event.target.value })} /></div>
            <div className="field"><label htmlFor="birth-year">Birth year</label><input id="birth-year" type="number" min={1900} max={new Date().getFullYear()} value={profile.birthYear ?? ''} onChange={(event) => setProfile({ ...profile, birthYear: event.target.value ? Number(event.target.value) : null })} /></div>
            <div className="field"><label htmlFor="address">Address</label><input id="address" maxLength={255} value={profile.address ?? ''} onChange={(event) => setProfile({ ...profile, address: event.target.value })} /></div>
            <div className="field"><label htmlFor="phone">Phone</label><input id="phone" maxLength={20} value={profile.phone ?? ''} onChange={(event) => setProfile({ ...profile, phone: event.target.value })} /></div>
            {profileMessage ? <p role="status">{profileMessage}</p> : null}
            {error ? <p className="field-error" role="alert">{error}</p> : null}
            <button className="primary-btn" type="submit" disabled={saving}>{saving ? 'Saving…' : 'Save profile'}</button>
          </form>
        )}
      </section>
      <section className="page-card">
        <h2>Change password</h2>
        <form className="inline-form" onSubmit={(event) => void changePassword(event)}>
          <div className="field"><label htmlFor="current-password">Current password</label><input id="current-password" name="currentPassword" type="password" required /></div>
          <div className="field"><label htmlFor="new-password">New password</label><input id="new-password" name="newPassword" type="password" minLength={8} maxLength={64} required /></div>
          {passwordMessage ? <p className="field-error" role="alert">{passwordMessage}</p> : null}
          <button className="primary-btn" type="submit">Change password</button>
        </form>
      </section>
    </div>
  );
}

/**
 * X.5 — trang kháng cáo dành cho user đang bị ban. User bị khóa vĩnh viễn (banned_permanently)
 * chỉ thấy thông báo, không có form; user đã có đơn pending/rejected cho ban hiện tại cũng không nộp lại được.
 */
export function AppealPage() {
  const [appeal, setAppeal] = useState<MyAppealDto | null>(null);
  const [reason, setReason] = useState('');
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  const load = useCallback(async () => {
    try {
      const { data } = await api.get<MyAppealDto>('/appeals/me');
      setAppeal(data);
    } catch (loadError) {
      setError(messageFromError(loadError, 'Your appeal status could not be loaded.'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { queueMicrotask(() => void load()); }, [load]);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!appeal?.banId) return;
    setSubmitting(true);
    setError('');
    setMessage('');
    try {
      await api.post('/appeals', { banId: appeal.banId, reasonText: reason });
      setReason('');
      setMessage('Your appeal has been submitted. A moderator will review it.');
      await load();
    } catch (submitError) {
      setError(messageFromError(submitError, 'Your appeal could not be submitted.'));
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) return <p role="status">Loading appeal…</p>;

  return (
    <div className="content-stack">
      <section className="page-card">
        <h2>Ban appeal</h2>
        {appeal?.appealsLocked ? (
          <p className="notice" role="alert">Appeal privileges are disabled for this account. You cannot submit an appeal.</p>
        ) : !appeal?.banId ? (
          <p className="notice" role="status">Your account is not currently banned. You have nothing to appeal.</p>
        ) : (
          <>
            <p><strong>Ban #{appeal.banId}</strong></p>
            {appeal.banReason ? <p><strong>Reason:</strong> {appeal.banReason}</p> : null}
            {appeal.bannedIp ? <p><strong>Banned IP:</strong> {appeal.bannedIp}</p> : null}
            {appeal.appealStatus ? (
              <p role="status">
                Your last appeal is <strong>{appeal.appealStatus}</strong>
                {appeal.appealReviewedAt ? ` (reviewed ${formatDate(appeal.appealReviewedAt)})` : ''}.
              </p>
            ) : null}
            {appeal.canSubmit ? (
              <form className="inline-form" onSubmit={(event) => void submit(event)}>
                <div className="field">
                  <label htmlFor="appeal-reason">Why should this ban be lifted?</label>
                  <textarea
                    id="appeal-reason"
                    rows={5}
                    maxLength={1000}
                    required
                    value={reason}
                    onChange={(event) => setReason(event.target.value)}
                  />
                </div>
                {message ? <p role="status">{message}</p> : null}
                {error ? <p className="field-error" role="alert">{error}</p> : null}
                <button className="primary-btn" type="submit" disabled={submitting || !reason.trim()}>
                  {submitting ? 'Submitting…' : 'Submit appeal'}
                </button>
              </form>
            ) : <p className="notice">You cannot submit another appeal for this ban.</p>}
          </>
        )}
        {!appeal?.banId && error && !appeal?.appealsLocked ? <p className="field-error" role="alert">{error}</p> : null}
      </section>
    </div>
  );
}

export function NotificationsPage() {
  const navigate = useNavigate();
  const [result, setResult] = useState<PagedResult<NotificationDto> | null>(null);
  const [page, setPage] = useState(1);
  const [error, setError] = useState('');

  const load = useCallback(async () => {
    try {
      const { data } = await api.get<PagedResult<NotificationDto>>('/notifications', { params: { page, pageSize: 20 } });
      setResult(data);
      setError('');
    } catch (loadError) { setError(messageFromError(loadError, 'Notifications could not be loaded.')); }
  }, [page]);

  useEffect(() => { queueMicrotask(() => void load()); }, [load]);

  const openNotification = async (notification: NotificationDto) => {
    try {
      if (!notification.readAt) await api.post(`/notifications/${notification.id}/read`);
      const { data: threadInfo } = await api.get<{ boardSlug: string }>(`/threads/${notification.threadId}`);
      navigate(`/b/${threadInfo.boardSlug}/thread/${notification.threadId}#reply-${notification.replyId}`);
    } catch (openError) { setError(messageFromError(openError, 'Notification could not be opened.')); }
  };

  const markAllRead = async () => {
    try {
      await api.post('/notifications/read-all');
      await load();
    } catch (readError) { setError(messageFromError(readError, 'Notifications could not be marked as read.')); }
  };

  return (
    <section className="page-card notification-page">
      <header className="section-head"><h2>Notifications</h2><button className="text-button" type="button" onClick={() => void markAllRead()}>Mark all read</button></header>
      {error ? <p className="field-error" role="alert">{error}</p> : null}
      {result?.items.map((notification) => (
        <button className={`notification-row${notification.readAt ? '' : ' unread'}`} type="button" key={notification.id} onClick={() => void openNotification(notification)}>
          <span><AuthorLabel name={notification.username} capcode={notification.capcode} /> replied to you</span>
          <time dateTime={notification.createdAt}>{formatDate(notification.createdAt)}</time>
        </button>
      ))}
      {result && !result.items.length ? <p>You have no notifications.</p> : null}
      {result ? <Pagination page={result.page} totalPages={result.totalPages} onPageChange={setPage} /> : null}
    </section>
  );
}

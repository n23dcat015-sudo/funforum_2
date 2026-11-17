import { roleLabel } from '../context/roles';
import { useEffect, useMemo, useState, type ChangeEvent, type FormEvent, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../context/useAuth';
import { api } from '../lib/api';
import type { ReplyDto, UnreadCountDto, VoteResultDto } from '../types/api';

type PaginationProps = {
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
};

export function Pagination({ page, totalPages, onPageChange }: PaginationProps) {
  if (totalPages < 2) return null;
  return (
    <nav className="pagination" aria-label="Pagination">
      <button type="button" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>Previous</button>
      <span>Page {page} of {totalPages}</span>
      <button type="button" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>Next</button>
    </nav>
  );
}

export function SearchBar({ initialValue = '', onSearch }: { initialValue?: string; onSearch: (query: string) => void }) {
  const [query, setQuery] = useState(initialValue);
  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    onSearch(query.trim());
  };
  return (
    <form className="search-bar" role="search" onSubmit={submit}>
      <label className="visually-hidden" htmlFor="board-search">Search</label>
      <input id="board-search" value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Search this board" />
      <button type="submit">Search</button>
    </form>
  );
}

export function SortDropdown({ value, options, onChange }: {
  value: string;
  options: Array<{ value: string; label: string }>;
  onChange: (value: string) => void;
}) {
  return (
    <label className="control-field">
      <span>Sort</span>
      <select value={value} onChange={(event) => onChange(event.target.value)}>
        {options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>
    </label>
  );
}

export function FilterControls({ label = 'Time range', value, options, onChange }: {
  label?: string;
  value: string;
  options: Array<{ value: string; label: string }>;
  onChange: (value: string) => void;
}) {
  return (
    <label className="control-field">
      <span>{label}</span>
      <select value={value} onChange={(event) => onChange(event.target.value)}>
        {options.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>
    </label>
  );
}

export function AuthorLabel({ name, capcode }: { name: string; capcode?: string | null }) {
  return (
    <span className="author-label">
      <strong>{name}</strong>
      {capcode ? <span className="capcode-badge">{roleLabel(capcode)}</span> : null}
    </span>
  );
}

export function PlainText({ children }: { children: string }) {
  return <p className="plain-content">{children}</p>;
}

export function CharacterCountTextarea({ id, value, maxLength, onChange }: {
  id: string;
  value: string;
  maxLength: number;
  onChange: (value: string) => void;
}) {
  return (
    <div className="character-count-field">
      <textarea id={id} value={value} maxLength={maxLength} onChange={(event) => onChange(event.target.value)} />
      <small aria-live="polite">{value.length} / {maxLength}</small>
    </div>
  );
}

export function ImageUpload({ files, onChange, maxFileBytes = 4 * 1024 * 1024, allowMultiple = true }: {
  files: File[];
  onChange: (files: File[]) => void;
  maxFileBytes?: number;
  allowMultiple?: boolean;
}) {
  const [error, setError] = useState('');
  const previews = useMemo(() => files.map((file) => URL.createObjectURL(file)), [files]);

  useEffect(() => {
    return () => previews.forEach((url) => URL.revokeObjectURL(url));
  }, [previews]);

  const selectFiles = (event: ChangeEvent<HTMLInputElement>) => {
    const selected = Array.from(event.target.files ?? []).slice(0, allowMultiple ? undefined : 1);
    const acceptedTypes = ['image/jpeg', 'image/png', 'image/gif'];
    const invalid = selected.find((file) => !acceptedTypes.includes(file.type) || file.size > maxFileBytes);
    if (invalid) {
      setError(`${invalid.name}: choose a JPEG, PNG, or GIF no larger than ${Math.floor(maxFileBytes / 1024 / 1024)} MB.`);
      event.target.value = '';
      return;
    }
    setError('');
    onChange([...files, ...selected]);
    event.target.value = '';
  };

  return (
    <div className="image-upload">
      <label className="file-picker">
        <span>Add images</span>
        <input type="file" accept="image/jpeg,image/png,image/gif" multiple={allowMultiple} onChange={selectFiles} />
      </label>
      {error ? <p className="field-error" role="alert">{error}</p> : null}
      {files.length ? (
        <ul className="upload-previews">
          {files.map((file, index) => (
            <li key={`${file.name}-${file.lastModified}-${index}`}>
              {previews[index] ? <img src={previews[index]} alt={`Preview of ${file.name}`} /> : null}
              <span>{file.name}</span>
              <button type="button" aria-label={`Remove ${file.name}`} onClick={() => onChange(files.filter((_, fileIndex) => fileIndex !== index))}>Remove</button>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}

export function AttachmentImage({ url, thumbUrl, alt, caption }: { url: string; thumbUrl: string; alt: string; caption?: string }) {
  const [expanded, setExpanded] = useState(false);
  return (
    <figure className="attachment-figure">
      <button className={`attachment-image${expanded ? ' expanded' : ''}`} type="button" onClick={() => setExpanded(!expanded)} aria-label={expanded ? 'Show thumbnail' : 'View full image'}>
        <img src={expanded ? url : thumbUrl} alt={alt} />
      </button>
      <figcaption>{caption ?? alt}</figcaption>
    </figure>
  );
}

export function NotificationBell() {
  const [count, setCount] = useState(0);
  useEffect(() => {
    let active = true;
    const loadCount = async () => {
      try {
        const { data } = await api.get<UnreadCountDto>('/notifications/unread-count');
        if (active) setCount(data.count);
      } catch {
        if (active) setCount(0);
      }
    };
    void loadCount();
    const interval = window.setInterval(() => void loadCount(), 60_000);
    return () => {
      active = false;
      window.clearInterval(interval);
    };
  }, []);
  return <Link className="notification-link" to="/notifications">Notifications{count > 0 ? ` (${count})` : ''}</Link>;
}

export function VoteButton({ score, onVote }: { score: number; onVote: () => Promise<void> }) {
  const [pending, setPending] = useState(false);
  const vote = async () => {
    setPending(true);
    try { await onVote(); } finally { setPending(false); }
  };
  return <button type="button" className="vote-button" disabled={pending} onClick={() => void vote()}>▲ {score}</button>;
}

/** X.3 — nút Report trên thread/reply. Chỉ hiện khi đã đăng nhập; POST /reports. */
export function ReportButton({ targetType, targetId }: { targetType: 'thread' | 'reply'; targetId: number }) {
  const { user } = useAuth();
  const [reported, setReported] = useState(false);
  const [error, setError] = useState('');

  if (!user) return null;
  if (reported) return <span className="text-button reported-flag" role="status">Reported</span>;

  const report = async () => {
    const reason = window.prompt('Why are you reporting this?');
    if (reason === null) return;
    const trimmed = reason.trim();
    if (!trimmed) return;
    setError('');
    try {
      await api.post('/reports', { targetType, targetId, reason: trimmed });
      setReported(true);
    } catch {
      setError('Report could not be sent.');
    }
  };

  return (
    <>
      <button className="text-button" type="button" onClick={() => void report()}>Report</button>
      {error ? <span className="field-error" role="alert">{error}</span> : null}
    </>
  );
}

export function ViewRepliesButton({ replyId, children, onReplyTo, onVote, excludeReplyIds = [], renderActions }: {
  replyId: number;
  children?: ReactNode;
  onReplyTo?: (reply: ReplyDto) => void;
  onVote?: (replyId: number) => Promise<VoteResultDto>;
  excludeReplyIds?: number[];
  renderActions?: (reply: ReplyDto) => ReactNode;
}) {
  const [items, setItems] = useState<ReplyDto[]>([]);
  const [scores, setScores] = useState<Record<number, number>>({});
  const [votingId, setVotingId] = useState<number | null>(null);
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(true);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const loadReplies = async () => {
    setLoading(true);
    setError('');
    try {
      const { data } = await api.get<{ items: ReplyDto[]; totalPages: number }>(`/replies/${replyId}/children`, { params: { page } });
      setItems((current) => {
        const seen = new Set([...excludeReplyIds, ...current.map((reply) => reply.id)]);
        return [...current, ...data.items.filter((reply) => !seen.has(reply.id))];
      });
      setHasMore(page < data.totalPages);
      setPage((current) => current + 1);
    } catch {
      setError('Replies could not be loaded. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const voteReply = async (id: number) => {
    if (!onVote || votingId !== null) return;
    setVotingId(id);
    try {
      const result = await onVote(id);
      setScores((current) => ({ ...current, [id]: result.score }));
    } finally {
      setVotingId(null);
    }
  };

  return (
    <div className="nested-replies">
      {items.length ? items.map((reply) => (
        <article id={`reply-${reply.id}`} className={`nested-reply${reply.isHidden ? ' reply-hidden' : ''}`} key={reply.id}>
          <AuthorLabel name={reply.authorName} capcode={reply.capcode} />
          {reply.isHidden ? <span className="hidden-badge">Hidden</span> : null}
          {reply.replyToUsername ? <span className="reply-to">@{reply.replyToUsername}</span> : null}
          <p className="plain-content">{reply.content}</p>
          {reply.image ? <AttachmentImage url={reply.image.url} thumbUrl={reply.image.thumbUrl} alt={reply.image.originalName} caption={reply.image.originalName} /> : null}
          <div className="reply-actions">
            {onReplyTo ? <button type="button" className="text-button" onClick={() => onReplyTo(reply)}>Reply</button> : null}
            {onVote ? <button type="button" className="vote-button" disabled={votingId === reply.id} onClick={() => void voteReply(reply.id)}>▲ {scores[reply.id] ?? reply.score}</button> : null}
            {renderActions?.(reply)}
          </div>
        </article>
      )) : null}
      {error ? <p className="field-error" role="alert">{error}</p> : null}
      {hasMore ? (
        <button type="button" className="text-button" disabled={loading} onClick={() => void loadReplies()}>
          {loading ? 'Loading…' : children ?? 'View replies'}
        </button>
      ) : null}
    </div>
  );
}

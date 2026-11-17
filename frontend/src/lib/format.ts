export function formatDate(value: string) {
  return new Intl.DateTimeFormat('en', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
}

export function formatFileSize(bytes: number) {
  if (!Number.isFinite(bytes) || bytes <= 0) return '0 B';
  if (bytes < 1024) return `${bytes} B`;
  const kilobytes = bytes / 1024;
  if (kilobytes < 1024) return `${Math.round(kilobytes)} KB`;
  return `${(kilobytes / 1024).toFixed(1)} MB`;
}

type ProblemBody = { message?: string; detail?: string; title?: string };

export function messageFromError(error: unknown, fallback: string) {
  if (typeof error === 'object' && error !== null && 'response' in error) {
    const response = (error as { response?: { data?: ProblemBody } }).response;
    const body = response?.data;
    if (body?.message || body?.detail || body?.title) {
      return body.message ?? body.detail ?? body.title ?? fallback;
    }
  }
  return fallback;
}

export function statusFromError(error: unknown): number | null {
  if (typeof error === 'object' && error !== null && 'response' in error) {
    const response = (error as { response?: { status?: number } }).response;
    return response?.status ?? null;
  }
  return null;
}

/** X.5 — trạng thái bị ban (dùng chung cho redirect và hiển thị hồ sơ). */
export function isBannedStatus(status?: string | null): boolean {
  return status === 'banned' || status === 'banned_permanently';
}

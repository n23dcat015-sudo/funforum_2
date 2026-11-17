export type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
};

export type BoardDto = {
  id: number;
  name: string;
  slug: string;
  description?: string | null;
  sortOrder?: number;
  createdAt?: string;
};

export type AttachmentDto = {
  url: string;
  thumbUrl: string;
  originalName: string;
  width: number;
  height: number;
  sizeBytes: number;
};

export type AuthorDto = {
  authorName: string;
  capcode?: string | null;
  /** Phase A: chủ sở hữu so với người xem hiện tại — thay cho so sánh chuỗi authorName. */
  isMine: boolean;
  /** Phase A: quyền xóa tính sẵn ở backend (admin | tác giả | chủ thread). */
  canDelete: boolean;
};

export type ReplyDto = AuthorDto & {
  id: number;
  threadId: number;
  parentReplyId?: number | null;
  rootReplyId?: number | null;
  content: string;
  createdAt: string;
  childCount: number;
  score: number;
  replyToUsername?: string | null;
  previewChildren?: ReplyDto[] | null;
  status: 'visible' | 'hidden';
  hiddenBy?: 'owner' | 'admin' | null;
  isHidden: boolean;
  image?: AttachmentDto | null;
};

export type ThreadDto = AuthorDto & {
  id: number;
  boardId: number;
  boardSlug: string;
  title: string;
  content: string;
  createdAt: string;
  replyCount: number;
  score: number;
  image?: AttachmentDto | null;
  previewReplies?: ReplyDto[];
  omittedReplies?: number;
  omittedImages?: number;
  imagesPosted: number;
  imageLimitReached: boolean;
  replyLimitReached: boolean;
  lastReplyAt: string;
  updatedAt: string;
};

export type NotificationDto = {
  id: number;
  username: string;
  capcode?: string | null;
  threadId: number;
  replyId: number;
  createdAt: string;
  readAt?: string | null;
};

export type UnreadCountDto = { count: number };
export type VoteResultDto = { score: number; myVote: number | null };

export type MeDto = {
  id: number;
  username: string;
  displayName: string;
  email: string;
  fullName?: string | null;
  birthYear?: number | null;
  address?: string | null;
  phone?: string | null;
  /** X.4/X.5 — active | banned | banned_permanently. */
  status: string;
  createdAt: string;
};

export type SortOption = 'latest' | 'new' | 'top' | 'hot';

export type AdminThreadDto = {
  id: number;
  boardId: number;
  boardSlug: string;
  title: string;
  content: string;
  status: 'visible' | 'hidden' | string;
  authorUserId?: number | null;
  authorAdminId?: number | null;
  authorUsername?: string | null;
  capcode?: string | null;
  replyCount: number;
  score: number;
  imageCount: number;
  image?: AttachmentDto | null;
  createdAt: string;
  updatedAt: string;
  lastReplyAt: string;
};

export type AdminUserDto = {
  id: number;
  username: string;
  email: string;
  fullName?: string | null;
  birthYear?: number | null;
  address?: string | null;
  phone?: string | null;
  /** X.4/X.5 — active | banned | banned_permanently. */
  status: string;
  createdAt: string;
  updatedAt: string;
};

export type AdminAccountDto = {
  id: number;
  username: string;
  email: string;
  role: 'janitor' | 'moderator' | 'super_admin' | string;
  createdAt: string;
  updatedAt: string;
};

/** X.3 — khớp AdminReportDto phía backend. */
export type AdminReportDto = {
  id: number;
  targetType: 'thread' | 'reply' | string;
  targetId: number;
  reporterUserId?: number | null;
  reporterUsername?: string | null;
  reason: string;
  status: 'pending' | 'escalated' | 'resolved' | string;
  createdAt: string;
};

/** X.4 — khớp BanDto phía backend. */
export type BanDto = {
  id: number;
  userId: number;
  username: string;
  bannedIp?: string | null;
  reason?: string | null;
  bannedByAdminId?: number | null;
  bannedByUsername?: string | null;
  status: 'active' | 'lifted' | string;
  createdAt: string;
};

/** X.2 — khớp AuditActions / AuditTargetTypes phía backend. */
export type AuditLogDto = {
  id: number;
  actorAdminId?: number | null;
  actorUsername?: string | null;
  action: string;
  targetType: string;
  targetId: number;
  reason?: string | null;
  metadata?: unknown;
  createdAt: string;
};

/** X.5 — khớp MyAppealDto phía backend (GET /api/appeals/me). */
export type MyAppealDto = {
  appealsLocked: boolean;
  banId?: number | null;
  banReason?: string | null;
  bannedIp?: string | null;
  appealStatus?: string | null;
  appealReasonText?: string | null;
  appealCreatedAt?: string | null;
  appealReviewedAt?: string | null;
  canSubmit: boolean;
};

/** X.5 — khớp AppealDto phía backend (GET /api/admin/appeals). */
export type AppealDto = {
  id: number;
  banId: number;
  userId: number;
  username: string;
  bannedIp?: string | null;
  reasonText: string;
  status: 'pending' | 'rejected' | 'approved' | string;
  reviewedByAdminId?: number | null;
  reviewedByUsername?: string | null;
  createdAt: string;
  reviewedAt?: string | null;
};
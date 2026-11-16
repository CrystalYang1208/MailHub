import { reactive } from 'vue';
import { ApiError, createApiClient } from './api/client';
import type { components } from './api/generated';

export type Schema = components['schemas'];
export type Account = Schema['Account'];
export type Folder = Schema['Folder'];
export type Mail = Schema['MessageSummary'];
export type Draft = Schema['Draft'];
export type MailPage = {
  items: Mail[];
  nextCursor: string | null;
  hasMore: boolean;
  coverage?: Schema['Coverage'];
};
let bridgeToken: string | undefined;
let sessionToken: string | undefined;
let sessionGeneration = 0;
const activeBlobUrls = new Set<string>();
const client = createApiClient({
  bridgeToken: () => bridgeToken,
  sessionToken: () => sessionToken,
});
export const state = reactive({
  ready: false,
  initializing: true,
  engineering: false,
  demo: false,
  startupError: '',
  user: null as Schema['User'] | null,
  users: [] as Schema['User'][],
  accounts: [] as Account[],
  folders: [] as Folder[],
  jobs: [] as Schema['Job'][],
  settings: null as Schema['Settings'] | null,
  toasts: [] as { id: number; message: string; type: 'success' | 'error' | 'info' }[],
});
export function toast(message: string, type: 'success' | 'error' | 'info' = 'info') {
  const id = Date.now() + Math.random();
  state.toasts.push({ id, message, type });
  window.setTimeout(() => {
    state.toasts = state.toasts.filter((t) => t.id !== id);
  }, 5000);
}
export function showError(error: unknown) {
  toast(
    error instanceof ApiError
      ? `${error.message}。${error.action}`
      : error instanceof Error
        ? error.message
        : '操作未完成，请稍后重试',
    'error',
  );
}
export function forgetUser() {
  for (const url of activeBlobUrls) URL.revokeObjectURL(url);
  activeBlobUrls.clear();
  sessionGeneration++;
  sessionToken = undefined;
  state.user = null;
  state.accounts = [];
  state.folders = [];
  state.jobs = [];
  state.settings = null;
}
export async function request<T>(
  path: string,
  init: RequestInit = {},
  auth: 'session' | 'bridge' | 'none' = 'session',
): Promise<T> {
  const generation = sessionGeneration;
  try {
    const result = await client<T>('/api/v1' + path, init, auth);
    if (auth === 'session' && generation !== sessionGeneration)
      throw new ApiError('SESSION_REQUIRED', '用户已切换', '请在当前用户中重新操作');
    return result;
  } catch (error) {
    if (error instanceof ApiError && error.code === 'SESSION_REQUIRED' && state.user) forgetUser();
    throw error;
  }
}
export function json<T>(
  path: string,
  method: string,
  body: unknown = {},
  headers: HeadersInit = {},
  auth: 'session' | 'bridge' | 'none' = 'session',
) {
  return request<T>(
    path,
    {
      method,
      headers: { 'Content-Type': 'application/json', ...headers },
      body: JSON.stringify(body),
    },
    auth,
  );
}
export async function initialize(code: string | null) {
  try {
    const health = await fetch('/_health', { credentials: 'omit' }).then((r) => r.json());
    state.demo = !!health.data?.demo;
    state.engineering = !!health.data?.engineering;
    if (state.engineering) {
      state.ready = true;
      return;
    }
    if (!code) throw new Error('启动链接已使用或缺少引导码。请重新双击启动器打开工作台。');
    const access = await json<Schema['BridgeAccess']>(
      '/bridge/bootstrap',
      'POST',
      { code },
      {},
      'none',
    );
    bridgeToken = access.bridgeToken;
    state.users = await request<Schema['User'][]>('/users', {}, 'bridge');
    state.ready = true;
  } catch (error) {
    state.startupError = error instanceof Error ? error.message : '无法连接本地程序';
  } finally {
    state.initializing = false;
  }
}
export async function signIn(userId: string, password: string) {
  const result = await json<Schema['Session']>(
    '/sessions',
    'POST',
    { userId, password },
    {},
    'bridge',
  );
  sessionGeneration++;
  sessionToken = result.sessionToken;
  state.user = result.user;
  await refreshWorkspace();
}
export async function register(displayName: string, password: string) {
  const user = await json<Schema['User']>(
    '/users',
    'POST',
    { displayName, password },
    {},
    'bridge',
  );
  state.users = await request<Schema['User'][]>('/users', {}, 'bridge');
  await signIn(user.userId, password);
}
export async function enterDemo() {
  const password = 'MailHub-Demo-2026!';
  const existing = state.users.find((u) => u.displayName === '林同学');
  if (existing) await signIn(existing.userId, password);
  else await register('林同学', password);
}
export async function lockWorkspace() {
  try {
    await json('/sessions/current', 'DELETE');
  } finally {
    forgetUser();
  }
  state.users = await request<Schema['User'][]>('/users', {}, 'bridge');
}
export async function refreshWorkspace() {
  if (!state.user) return;
  const generation = sessionGeneration;
  const [accounts, jobs, settings] = await Promise.all([
    request<Account[]>('/accounts'),
    request<{ items: Schema['Job'][] }>('/sync/jobs'),
    request<Schema['Settings']>('/settings'),
  ]);
  if (generation !== sessionGeneration) return;
  const folders = (
    await Promise.all(accounts.map((a) => request<Folder[]>(`/accounts/${a.accountId}/folders`)))
  ).flat();
  if (generation !== sessionGeneration) return;
  state.accounts = accounts;
  state.jobs = jobs.items;
  state.settings = settings;
  state.folders = folders;
}
export async function downloadAttachment(id: string, name: string) {
  const generation = sessionGeneration;
  const picker = (
    window as Window & {
      showSaveFilePicker?: (options: { suggestedName: string }) => Promise<FileSystemFileHandle>;
    }
  ).showSaveFilePicker;
  let handle: FileSystemFileHandle | undefined;
  if (picker) {
    try {
      handle = await picker.call(window, { suggestedName: name });
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') return false;
    }
  }
  if (generation !== sessionGeneration) return false;
  const batch = await json<Schema['DownloadBatch']>('/downloads', 'POST', { attachmentIds: [id] });
  if (batch.state !== 'completed') throw new Error('附件尚未下载完成，请检查网络或邮箱状态后重试');
  const response = await fetch(`/api/v1/attachments/${encodeURIComponent(id)}/content`, {
    credentials: 'omit',
    headers: { Authorization: `Bearer ${bridgeToken}`, 'X-MailHub-Session': sessionToken ?? '' },
  });
  if (!response.ok) throw new Error('附件下载失败，请重新登录或重试');
  const blob = await response.blob();
  if (generation !== sessionGeneration) return false;
  if (handle) {
    const writable = await handle.createWritable();
    try {
      await writable.write(blob);
      await writable.close();
    } catch (error) {
      await writable.abort();
      throw error;
    }
    return true;
  }
  const url = URL.createObjectURL(blob);
  activeBlobUrls.add(url);
  const a = document.createElement('a');
  a.href = url;
  a.download = name;
  a.click();
  window.setTimeout(() => {
    URL.revokeObjectURL(url);
    activeBlobUrls.delete(url);
  }, 5000);
  return true;
}
export function providerName(account: Account) {
  if (account.providerKey === 'qq') return 'QQ 邮箱';
  if (account.providerKey === '163') return '163 邮箱';
  if (account.providerKey === 'gmail') return 'Gmail';
  if (account.imap?.host.includes('126.com')) return '126 邮箱';
  if (account.imap?.host.includes('outlook') || account.imap?.host.includes('office365'))
    return 'Outlook';
  return 'IMAP 邮箱';
}
export function providerColor(account?: Account) {
  return account?.providerKey === 'gmail'
    ? '#6e8f86'
    : account?.providerKey === '163'
      ? '#8294ae'
      : '#bc775f';
}
export function bytes(value: number) {
  return value < 1000
    ? `${value} B`
    : value < 1_000_000
      ? `${(value / 1000).toFixed(1)} KB`
      : `${(value / 1_000_000).toFixed(1)} MB`;
}
export const errors: Record<string, string> = {
  NETWORK_UNREACHABLE: '无法连接邮箱，等待重试',
  AUTH_FAILED: '授权失败，请检查授权码',
  AUTH_REQUIRED: '需要重新授权',
  IMAP_DISABLED: '请开启IMAP服务',
  SERVER_CONFIG: '请核对服务器地址与端口',
  TLS_FAILED: '服务器证书验证失败',
  SMTP_REJECTED: '邮件服务器拒绝接收，请检查收件人或内容',
  SMTP_CONFIRMATION_LOST: '提交结果未知，请先核对服务商已发送',
  INTERRUPTED_AFTER_SUBMIT: '程序在提交后中断，请核对已发送',
  DEMO_ADDRESS_REQUIRED: '演示收件人需使用example.invalid地址',
  PROVIDER_SIZE_LIMIT: '超过服务商邮件大小限制',
  SEND_PREPARATION_FAILED: '发送准备失败，草稿已保留',
  TRASH_UNSUPPORTED: '服务商不支持安全移至垃圾箱',
  THREAD_HEADERS_MISSING: '原邮件缺少线程头，回复将保留引用正文',
};

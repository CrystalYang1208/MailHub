<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import AppIcon from '../components/AppIcon.vue';
import SafeBody from '../components/SafeBody.vue';
import AccountDialog from '../components/AccountDialog.vue';
import ComposeDialog from '../components/ComposeDialog.vue';
import AccountsPanel from '../components/AccountsPanel.vue';
import SettingsPanel from '../components/SettingsPanel.vue';
import {
  state,
  request,
  json,
  toast,
  showError,
  refreshWorkspace,
  lockWorkspace,
  downloadAttachment,
  providerName,
  providerColor,
  bytes,
  errors,
  type Schema,
  type Mail,
  type MailPage,
  type Account,
  type Draft,
} from '../workspace';
const route = useRoute();
const router = useRouter();
const page = computed(() => route.path);
const accountsOpen = ref(false);
const reauthAccount = ref<Account | null>(null);
const composeOpen = ref(false);
const editingDraft = ref<Draft | null>(null);
const searchInput = ref<HTMLInputElement | null>(null);
const searchText = ref('');
const activeSearch = ref('');
const accountFilter = ref('');
const folderId = ref('');
const expandedAccounts = ref<string[]>([]);
const onlyUnread = ref(false);
const onlyAttachments = ref(false);
const filtersOpen = ref(false);
const fromDate = ref('');
const toDate = ref('');
const mails = ref<Mail[]>([]);
const nextCursor = ref<string | null>(null);
const hasMore = ref(false);
const coverage = ref<Schema['Coverage'] | null>(null);
const listBusy = ref(false);
const moreBusy = ref(false);
const selectedId = ref('');
const detail = ref<Schema['MessageDetail'] | null>(null);
const body = ref<Schema['Body'] | null>(null);
const reading = ref(false);
const mutationBusy = ref(false);
const downloadBusy = ref('');
const drafts = ref<Draft[]>([]);
const draftNextCursor = ref<string | null>(null);
const draftHasMore = ref(false);
const syncing = ref(false);
const headerExpanded = ref(false);
let pollTimer: ReturnType<typeof setInterval> | undefined;
let polling = false;
let listGeneration = 0;
let readingGeneration = 0;
let stopped = false;
let userClosedReading = false;
let loadedExtra = false;
const unreadTotal = computed(() =>
  state.folders.filter((f) => f.role === 'inbox').reduce((sum, f) => sum + f.unreadCount, 0),
);
const currentFolder = computed(() => state.folders.find((f) => f.folderId === folderId.value));
const folderLabels: Record<string, string> = {
  inbox: '收件箱',
  sent: '已发送',
  drafts: '服务端草稿',
  trash: '垃圾箱',
  other: '其他文件夹',
};
const heading = computed(() =>
  activeSearch.value
    ? '搜索结果'
    : currentFolder.value
      ? (folderLabels[currentFolder.value.role] ?? currentFolder.value.name)
      : '统一收件箱',
);
const currentAccount = computed(() =>
  state.accounts.find((a) => a.accountId === detail.value?.accountId),
);
const activeJobs = computed(() =>
  state.jobs.filter((j) => ['pending', 'running'].includes(j.state)),
);
const offline = computed(() =>
  state.accounts.some((a) => a.status === 'retry' && a.lastErrorCode === 'NETWORK_UNREACHABLE'),
);
const mailCountLabel = computed(() =>
  activeSearch.value
    ? `找到 ${mails.value.length}${hasMore.value ? '+' : ''} 封相关邮件`
    : `${unreadTotal.value} 封未读，慢慢处理就好。`,
);
const allFolders = (id: string) => state.folders.filter((f) => f.accountId === id);
function accountFor(mail: Mail) {
  return state.accounts.find((a) => a.accountId === mail.accountId);
}
function sender(mail: Mail) {
  return mail.from[0]?.name || mail.from[0]?.address.split('@')[0] || '未知发件人';
}
function avatar(mail: Mail) {
  return sender(mail).slice(0, 1).toUpperCase();
}
function shortDate(value: string) {
  const date = new Date(value);
  const today = new Date();
  if (date.toDateString() === today.toDateString())
    return date.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit', hour12: false });
  const yesterday = new Date();
  yesterday.setDate(yesterday.getDate() - 1);
  return date.toDateString() === yesterday.toDateString()
    ? '昨天'
    : date.toLocaleDateString('zh-CN', { month: 'numeric', day: 'numeric' });
}
function longDate(value: string) {
  return new Date(value).toLocaleString('zh-CN', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    weekday: 'short',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  });
}
function clearReading() {
  userClosedReading = true;
  readingGeneration++;
  selectedId.value = '';
  detail.value = null;
  body.value = null;
  reading.value = false;
}
function query(cursor?: string | null) {
  const p = new URLSearchParams({ limit: '50' });
  if (activeSearch.value) p.set('q', activeSearch.value);
  if (accountFilter.value) p.set('accountId', accountFilter.value);
  if (folderId.value && !activeSearch.value) p.set('folderId', folderId.value);
  if (onlyUnread.value) p.set('isRead', 'false');
  if (onlyAttachments.value) p.set('hasAttachments', 'true');
  if (fromDate.value) p.set('from', new Date(fromDate.value + 'T00:00:00').toISOString());
  if (toDate.value) p.set('to', new Date(toDate.value + 'T23:59:59.999').toISOString());
  if (cursor) p.set('cursor', cursor);
  return (activeSearch.value ? '/search?' : '/messages?') + p;
}
async function loadMails(silent = false, more = false) {
  const generation = ++listGeneration;
  if (more) loadedExtra = true;
  else if (!silent) loadedExtra = false;
  if (!silent) listBusy.value = true;
  if (more) moreBusy.value = true;
  try {
    const result = await request<MailPage>(query(more ? nextCursor.value : null));
    if (generation !== listGeneration || stopped || page.value !== '/') return;
    mails.value = more
      ? [
          ...mails.value,
          ...result.items.filter(
            (item) => !mails.value.some((m) => m.messageId === item.messageId),
          ),
        ]
      : result.items;
    nextCursor.value = result.nextCursor;
    hasMore.value = result.hasMore;
    coverage.value = result.coverage ?? null;
    if (!selectedId.value && !userClosedReading && mails.value[0]) await openMail(mails.value[0]);
    else if (selectedId.value && !mails.value.some((m) => m.messageId === selectedId.value)) {
      clearReading();
      if (mails.value[0]) await openMail(mails.value[0]);
    } else if (detail.value) {
      const summary = mails.value.find((m) => m.messageId === detail.value?.messageId);
      if (summary) detail.value = { ...detail.value, ...summary };
    }
  } catch (error) {
    if (!silent) showError(error);
  } finally {
    if (generation === listGeneration) {
      listBusy.value = false;
      moreBusy.value = false;
    }
  }
}
async function openMail(mail: Mail) {
  userClosedReading = false;
  const generation = ++readingGeneration;
  selectedId.value = mail.messageId;
  detail.value = null;
  body.value = null;
  reading.value = true;
  headerExpanded.value = false;
  try {
    const [message, content] = await Promise.all([
      request<Schema['MessageDetail']>(`/messages/${mail.messageId}`),
      request<Schema['Body']>(`/messages/${mail.messageId}/body`),
    ]);
    if (generation !== readingGeneration || stopped) return;
    detail.value = message;
    body.value = content;
  } catch (error) {
    if (generation === readingGeneration) showError(error);
  } finally {
    if (generation === readingGeneration) reading.value = false;
  }
}
async function navigate(path: string) {
  await router.push(path);
}
function inbox() {
  folderId.value = '';
  accountFilter.value = '';
  activeSearch.value = '';
  searchText.value = '';
  onlyUnread.value = false;
  onlyAttachments.value = false;
  fromDate.value = '';
  toDate.value = '';
  clearReading();
  void navigate('/');
  void loadMails();
}
function chooseFolder(id: string, accountId: string) {
  folderId.value = id;
  accountFilter.value = accountId;
  activeSearch.value = '';
  searchText.value = '';
  clearReading();
  void navigate('/');
  void loadMails();
}
function toggleAccount(id: string) {
  expandedAccounts.value = expandedAccounts.value.includes(id)
    ? expandedAccounts.value.filter((x) => x !== id)
    : [...expandedAccounts.value, id];
}
async function search() {
  activeSearch.value = searchText.value.trim();
  folderId.value = '';
  accountFilter.value = '';
  clearReading();
  await navigate('/');
  await loadMails();
}
function filter() {
  clearReading();
  void loadMails();
}
async function sync() {
  syncing.value = true;
  try {
    await json('/sync', 'POST', accountFilter.value ? { accountId: accountFilter.value } : {});
    await refreshWorkspace();
    toast('正在同步，进度可在同步记录查看');
  } catch (error) {
    showError(error);
  } finally {
    syncing.value = false;
  }
}
async function waitJob(job: Schema['Job']) {
  for (let attempt = 0; attempt < 80; attempt++) {
    if (stopped) return;
    await new Promise((resolve) => setTimeout(resolve, 350));
    const result = await request<Schema['Job']>(`/sync/jobs/${job.jobId}`);
    if (result.state === 'completed') return;
    if (['retry', 'failed', 'auth-required', 'canceled', 'paused'].includes(result.state))
      throw new Error(
        errors[result.lastErrorCode ?? ''] ?? '操作尚未得到服务器确认，请查看同步记录',
      );
  }
  throw new Error('操作仍在处理中，请稍后查看同步记录');
}
async function mutate(trash = false) {
  if (!detail.value) return;
  mutationBusy.value = true;
  try {
    const id = detail.value.messageId;
    const job = await json<Schema['Job']>(
      `/messages/${id}/${trash ? 'trash' : 'read'}`,
      trash ? 'POST' : 'PATCH',
      trash ? {} : { isRead: !detail.value.isRead },
    );
    await waitJob(job);
    await refreshWorkspace();
    if (trash) clearReading();
    await loadMails(true);
    toast(trash ? '已移至服务端垃圾箱' : '已读状态已由服务器确认', 'success');
  } catch (error) {
    showError(error);
  } finally {
    mutationBusy.value = false;
  }
}
async function derive(forward: boolean) {
  if (!detail.value) return;
  try {
    editingDraft.value = await json<Draft>(
      `/messages/${detail.value.messageId}/${forward ? 'forward' : 'reply'}`,
      'POST',
      { accountId: detail.value.accountId },
    );
    composeOpen.value = true;
  } catch (error) {
    showError(error);
  }
}
function compose() {
  if (!state.accounts.some((a) => a.enabled)) {
    toast('先添加一个邮箱，再写第一封邮件');
    openAccount();
    return;
  }
  editingDraft.value = null;
  composeOpen.value = true;
}
function openAccount(account?: Account) {
  reauthAccount.value = account ?? null;
  accountsOpen.value = true;
}
async function loadDrafts(more = false) {
  try {
    const result = await request<{ items: Draft[]; nextCursor: string | null; hasMore: boolean }>(
      '/drafts?limit=50' +
        (more && draftNextCursor.value
          ? '&cursor=' + encodeURIComponent(draftNextCursor.value)
          : ''),
    );
    drafts.value = more ? [...drafts.value, ...result.items] : result.items;
    draftNextCursor.value = result.nextCursor;
    draftHasMore.value = result.hasMore;
  } catch (error) {
    showError(error);
  }
}
async function editDraft(draft: Draft) {
  try {
    editingDraft.value = await request<Draft>(`/drafts/${draft.draftId}`);
    composeOpen.value = true;
  } catch (error) {
    showError(error);
  }
}
async function done() {
  await refreshWorkspace();
  if (page.value === '/drafts') await loadDrafts();
  else if (page.value === '/') await loadMails(true);
}
async function download(id: string, name: string) {
  downloadBusy.value = id;
  try {
    if (await downloadAttachment(id, name)) toast('附件已保存', 'success');
  } catch (error) {
    showError(error);
  } finally {
    downloadBusy.value = '';
  }
}
async function retry(id: string) {
  try {
    await json(`/sync/jobs/${id}/retry`, 'POST');
    await refreshWorkspace();
    toast('已重新加入队列');
  } catch (error) {
    showError(error);
  }
}
async function lock() {
  try {
    await lockWorkspace();
  } catch (error) {
    showError(error);
  }
}
function previous(delta: number) {
  const index = mails.value.findIndex((m) => m.messageId === selectedId.value);
  const target = mails.value[index + delta];
  if (target) void openMail(target);
}
function key(event: KeyboardEvent) {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
    event.preventDefault();
    searchInput.value?.focus();
  }
}
watch(page, () => {
  if (page.value === '/drafts') void loadDrafts();
  else if (page.value === '/') void loadMails();
});
onMounted(async () => {
  await refreshWorkspace();
  await loadMails();
  document.addEventListener('keydown', key);
  pollTimer = setInterval(async () => {
    if (polling || stopped || !state.user) return;
    polling = true;
    try {
      await refreshWorkspace();
      if (page.value === '/' && !moreBusy.value && !loadedExtra) await loadMails(true);
    } catch {
      /* Keep the last local snapshot while the host is temporarily unavailable. */
    } finally {
      polling = false;
    }
  }, 2500);
  await nextTick();
});
onUnmounted(() => {
  stopped = true;
  listGeneration++;
  readingGeneration++;
  clearInterval(pollTimer);
  document.removeEventListener('keydown', key);
});
const jobStates: Record<string, string> = {
  pending: '排队中',
  running: '执行中',
  retry: '等待重试',
  paused: '已暂停',
  'auth-required': '需要授权',
  completed: '已完成',
  failed: '未完成',
  canceled: '已取消',
};
const jobNames: Record<string, string> = {
  headers: '邮件同步',
  'body-index': '正文索引',
  flags: '更新已读状态',
  trash: '移至垃圾箱',
  cleanup: '清理数据',
};
</script>
<template>
  <div class="workspace-shell">
    <aside class="sidebar">
      <button class="brand brand-button" @click="inbox">
        <span class="brand-mark"><AppIcon name="mail" :size="24" /></span
        ><span>邮汇 <small>MailHub</small></span>
      </button>
      <p class="brand-caption">一个工作台，所有来信。</p>
      <button class="button primary compose-button" @click="compose">
        <AppIcon name="compose" :size="19" />写邮件
      </button>
      <nav class="primary-nav" aria-label="工作台导航">
        <button :class="{ active: page === '/' && !folderId && !activeSearch }" @click="inbox">
          <AppIcon name="inbox" />统一收件箱<span class="nav-badge">{{ unreadTotal }}</span></button
        ><button :class="{ active: page === '/drafts' }" @click="navigate('/drafts')">
          <AppIcon name="draft" />草稿箱</button
        ><button :class="{ active: page === '/activity' }" @click="navigate('/activity')">
          <AppIcon name="clock" />同步记录<span v-if="activeJobs.length" class="activity-dot" />
        </button>
      </nav>
      <div class="sidebar-section-label">
        <span>我的邮箱</span
        ><button class="icon-button" aria-label="添加邮箱" @click="openAccount()">
          <AppIcon name="plus" :size="16" />
        </button>
      </div>
      <div class="sidebar-accounts">
        <div v-for="account in state.accounts" :key="account.accountId">
          <button
            class="sidebar-account"
            :class="{ expanded: expandedAccounts.includes(account.accountId) }"
            @click="toggleAccount(account.accountId)"
          >
            <i class="provider-dot" :style="{ background: providerColor(account) }" /><span
              ><strong>{{ providerName(account) }}</strong
              ><small>{{ account.address }}</small></span
            ><AppIcon name="down" :size="13" />
          </button>
          <div v-if="expandedAccounts.includes(account.accountId)" class="folder-nav">
            <button
              v-for="folder in allFolders(account.accountId)"
              :key="folder.folderId"
              :class="{ active: folderId === folder.folderId && page === '/' }"
              @click="chooseFolder(folder.folderId, account.accountId)"
            >
              <AppIcon
                :name="
                  folder.role === 'trash'
                    ? 'trash'
                    : folder.role === 'sent'
                      ? 'send'
                      : folder.role === 'drafts'
                        ? 'draft'
                        : 'inbox'
                "
                :size="15"
              /><span>{{ folderLabels[folder.role] ?? folder.name }}</span
              ><small v-if="folder.unreadCount">{{ folder.unreadCount }}</small>
            </button>
          </div>
        </div>
        <button class="add-sidebar-account" @click="openAccount()">
          <AppIcon name="plus" :size="15" />添加邮箱账号
        </button>
      </div>
      <div class="sidebar-bottom">
        <nav class="primary-nav secondary-nav">
          <button :class="{ active: page === '/accounts' }" @click="navigate('/accounts')">
            <AppIcon name="users" :size="19" />管理邮箱</button
          ><button :class="{ active: page === '/settings' }" @click="navigate('/settings')">
            <AppIcon name="settings" :size="19" />设置与隐私
          </button>
        </nav>
        <div class="user-profile">
          <span class="user-avatar">{{ state.user?.displayName.slice(0, 1) }}</span>
          <div>
            <strong>{{ state.user?.displayName }}</strong
            ><small>本地空间 · {{ state.demo ? '合成演示' : '凭据已保护' }}</small>
          </div>
          <button
            class="icon-button"
            title="锁定 / 切换本地用户"
            aria-label="锁定或切换用户"
            @click="lock"
          >
            <AppIcon name="lock" :size="17" />
          </button>
        </div>
      </div>
    </aside>
    <main class="workspace-main">
      <header class="topbar">
        <form class="global-search" @submit.prevent="search">
          <AppIcon name="search" :size="19" /><input
            ref="searchInput"
            v-model="searchText"
            placeholder="搜索所有邮箱中的邮件"
            aria-label="搜索所有邮箱"
          /><button
            v-if="searchText"
            type="button"
            class="icon-button"
            aria-label="清除搜索"
            @click="
              searchText = '';
              activeSearch = '';
              filter();
            "
          >
            <AppIcon name="close" :size="14" /></button
          ><kbd>Ctrl K</kbd>
        </form>
        <div class="topbar-right">
          <span v-if="state.demo" class="demo-badge">合成演示</span
          ><span class="privacy-indicator"
            ><AppIcon name="shield" :size="15" /><span>数据保存在本机</span></span
          >
        </div>
      </header>
      <div v-if="offline" class="offline-bar">
        <AppIcon name="wifi" :size="15" />
        邮箱连接暂时中断，正在显示本地缓存。恢复网络后将自动重试。
      </div>
      <template v-if="page === '/'">
        <section class="inbox-heading">
          <div>
            <div class="heading-line">
              <h1>{{ heading }}</h1>
              <span v-if="!activeSearch" class="count-pill">{{ unreadTotal }}</span>
            </div>
            <p>{{ mailCountLabel }}</p>
          </div>
          <button class="button secondary sync-button" :disabled="syncing" @click="sync">
            <AppIcon
              name="sync"
              :size="16"
              :class="{ spinning: syncing || activeJobs.length > 0 }"
            />{{ syncing || activeJobs.length ? '同步中' : '同步' }}
          </button>
        </section>
        <div v-if="activeSearch && coverage && !coverage.complete" class="index-coverage">
          <AppIcon name="info" :size="14" />正文索引进行中：{{ coverage.bodyIndexed }}/{{
            coverage.totalMessages
          }}，当前结果尚未覆盖所有正文。
        </div>
        <section class="mail-layout" :class="{ 'reading-open': !!selectedId }">
          <div class="mail-list-panel">
            <div class="list-toolbar">
              <div class="segmented-control">
                <button
                  :class="{ selected: !onlyUnread }"
                  @click="
                    onlyUnread = false;
                    filter();
                  "
                >
                  全部</button
                ><button
                  :class="{ selected: onlyUnread }"
                  @click="
                    onlyUnread = true;
                    filter();
                  "
                >
                  未读
                </button>
              </div>
              <span class="spacer" /><span class="list-count"
                >{{ mails.length }}{{ hasMore ? '+' : '' }} 封</span
              ><button
                class="icon-button"
                :class="{ 'filter-active': filtersOpen || onlyAttachments }"
                aria-label="筛选邮件"
                @click="filtersOpen = !filtersOpen"
              >
                <AppIcon name="filter" :size="17" />
              </button>
            </div>
            <div v-if="filtersOpen" class="filter-panel">
              <label
                >账号<select v-model="accountFilter" @change="filter">
                  <option value="">全部邮箱</option>
                  <option
                    v-for="account in state.accounts"
                    :key="account.accountId"
                    :value="account.accountId"
                  >
                    {{ providerName(account) }}
                  </option>
                </select></label
              >
              <div class="two-fields">
                <label>开始日期<input v-model="fromDate" type="date" @change="filter" /></label
                ><label>结束日期<input v-model="toDate" type="date" @change="filter" /></label>
              </div>
              <label class="checkbox-label"
                ><input
                  v-model="onlyAttachments"
                  type="checkbox"
                  @change="filter"
                />仅显示有附件的邮件</label
              >
            </div>
            <div class="mail-list">
              <div v-if="listBusy && !mails.length" class="list-loading">
                <span class="spinner" />正在读取本地邮件…
              </div>
              <button
                v-for="mail in mails"
                :key="mail.messageId"
                class="mail-item"
                :class="{ selected: selectedId === mail.messageId, unread: !mail.isRead }"
                @click="openMail(mail)"
              >
                <span v-if="!mail.isRead" class="unread-dot" />
                <div class="mail-item-top">
                  <span
                    class="sender-avatar"
                    :style="{
                      background: providerColor(accountFor(mail)) + '16',
                      color: providerColor(accountFor(mail)),
                    }"
                    >{{ avatar(mail) }}</span
                  ><strong>{{ sender(mail) }}</strong
                  ><time :datetime="mail.receivedAt">{{ shortDate(mail.receivedAt) }}</time>
                </div>
                <h3>{{ mail.subject || '（无主题）' }}</h3>
                <p>{{ mail.from[0]?.address }}</p>
                <div class="mail-item-bottom">
                  <span class="account-label"
                    ><i
                      class="provider-dot"
                      :style="{ background: providerColor(accountFor(mail)) }"
                    />{{ accountFor(mail) ? providerName(accountFor(mail)!) : '邮箱' }}</span
                  ><span v-if="mail.bodyCached" class="cached-hint" title="正文已缓存，可离线阅读"
                    ><AppIcon name="download" :size="12" /></span
                  ><AppIcon v-if="mail.hasAttachments" name="paperclip" :size="13" />
                </div>
              </button>
              <div v-if="!listBusy && !mails.length" class="empty-state small-empty">
                <span class="empty-icon"
                  ><AppIcon :name="activeSearch ? 'search' : 'inbox'" :size="32"
                /></span>
                <h3>
                  {{
                    activeSearch
                      ? '没有找到相关邮件'
                      : state.accounts.length
                        ? '收件箱暂时没有邮件'
                        : '从第一个邮箱开始'
                  }}
                </h3>
                <p>
                  {{
                    activeSearch
                      ? '试试其他关键词，或调整筛选条件。'
                      : state.accounts.length
                        ? '同步完成后，新来信会出现在这里。'
                        : '添加邮箱，把分散的来信汇到一起。'
                  }}
                </p>
                <button
                  v-if="!state.accounts.length"
                  class="button secondary small"
                  @click="openAccount()"
                >
                  添加邮箱
                </button>
              </div>
              <button
                v-if="hasMore"
                class="load-more"
                :disabled="moreBusy"
                @click="loadMails(false, true)"
              >
                {{ moreBusy ? '正在读取…' : '加载更多邮件' }}<AppIcon name="down" :size="14" />
              </button>
              <div v-else-if="mails.length" class="list-end">已经看到这里的全部来信</div>
            </div>
          </div>
          <article class="reading-panel">
            <div class="reading-toolbar">
              <button
                class="icon-button mobile-back"
                aria-label="返回邮件列表"
                @click="clearReading"
              >
                <AppIcon name="back" />
              </button>
              <div class="reading-actions">
                <button
                  class="icon-button"
                  title="回复"
                  aria-label="回复邮件"
                  :disabled="!detail"
                  @click="derive(false)"
                >
                  <AppIcon name="reply" :size="19" /></button
                ><button
                  class="icon-button"
                  title="转发"
                  aria-label="转发邮件"
                  :disabled="!detail"
                  @click="derive(true)"
                >
                  <AppIcon name="forward" :size="19" /></button
                ><span class="toolbar-divider" /><button
                  class="icon-button"
                  :title="detail?.isRead ? '标记未读' : '标记已读'"
                  aria-label="切换已读状态"
                  :disabled="!detail || mutationBusy"
                  @click="mutate(false)"
                >
                  <AppIcon :name="detail?.isRead ? 'mail' : 'check'" :size="18" /></button
                ><button
                  class="icon-button"
                  title="移至服务端垃圾箱"
                  aria-label="移至垃圾箱"
                  :disabled="!detail || mutationBusy || currentFolder?.role === 'trash'"
                  @click="mutate(true)"
                >
                  <AppIcon name="trash" :size="18" />
                </button>
              </div>
              <span class="spacer" /><span v-if="detail" class="reading-position"
                >{{ mails.findIndex((m) => m.messageId === selectedId) + 1 }} /
                {{ mails.length }}</span
              ><button
                class="icon-button"
                aria-label="上一封邮件"
                :disabled="mails.findIndex((m) => m.messageId === selectedId) <= 0"
                @click="previous(-1)"
              >
                <AppIcon name="back" :size="17" /></button
              ><button
                class="icon-button"
                aria-label="下一封邮件"
                :disabled="
                  !selectedId ||
                  mails.findIndex((m) => m.messageId === selectedId) >= mails.length - 1
                "
                @click="previous(1)"
              >
                <AppIcon name="chevron" :size="17" />
              </button>
            </div>
            <div v-if="reading" class="reading-loading">
              <span class="spinner" />
              <p>正在打开这封来信…</p>
            </div>
            <div v-else-if="detail" class="reading-scroll">
              <div class="message-heading">
                <div class="message-account">
                  <span class="account-label"
                    ><i
                      class="provider-dot"
                      :style="{ background: providerColor(currentAccount) }"
                    />{{ currentAccount ? providerName(currentAccount) : '邮箱' }}</span
                  ><span v-if="!detail.isRead" class="message-unread-label">未读</span>
                </div>
                <h2>{{ detail.subject || '（无主题）' }}</h2>
                <div class="sender-detail">
                  <span
                    class="sender-avatar large"
                    :style="{
                      background: providerColor(currentAccount) + '15',
                      color: providerColor(currentAccount),
                    }"
                    >{{ avatar(detail) }}</span
                  >
                  <div>
                    <strong>{{ sender(detail) }}</strong
                    ><span class="sender-email">&lt;{{ detail.from[0]?.address }}&gt;</span
                    ><button class="recipient-toggle" @click="headerExpanded = !headerExpanded">
                      发送至 {{ detail.to.map((a) => a.name || a.address).join('，') || '我'
                      }}<AppIcon name="down" :size="12" />
                    </button>
                  </div>
                  <time :datetime="detail.receivedAt">{{ longDate(detail.receivedAt) }}</time>
                </div>
                <div v-if="headerExpanded" class="message-extra">
                  <p><span>收件人</span>{{ detail.to.map((a) => a.address).join('; ') }}</p>
                  <p v-if="detail.cc.length">
                    <span>抄送</span>{{ detail.cc.map((a) => a.address).join('; ') }}
                  </p>
                  <p v-if="detail.replyTo.length">
                    <span>回复至</span>{{ detail.replyTo.map((a) => a.address).join('; ') }}
                  </p>
                  <p><span>来源账号</span>{{ currentAccount?.address }}</p>
                </div>
              </div>
              <div class="message-body">
                <SafeBody
                  v-if="body?.state === 'ready'"
                  :key="selectedId"
                  :html="body.html"
                  :text="body.text"
                />
                <div v-else class="body-unavailable">
                  <AppIcon name="wifi" :size="28" />
                  <h3>这封邮件的正文还未缓存</h3>
                  <p>当前无法连接邮箱。已缓存的邮件仍可阅读，<br />恢复连接后重新打开即可。</p>
                  <button class="button secondary small" @click="openMail(detail)">重新加载</button>
                </div>
              </div>
              <section v-if="detail.attachments.length" class="message-attachments">
                <h4>
                  <AppIcon name="paperclip" :size="15" />{{ detail.attachments.length }} 个附件
                </h4>
                <div class="attachment-grid">
                  <button
                    v-for="attachment in detail.attachments"
                    :key="attachment.attachmentId"
                    class="attachment-tile"
                    :disabled="!!downloadBusy"
                    @click="download(attachment.attachmentId, attachment.fileName)"
                  >
                    <span class="attachment-file-icon"><AppIcon name="file" :size="23" /></span
                    ><span
                      ><strong>{{ attachment.fileName }}</strong
                      ><small
                        >{{
                          attachment.declaredBytes != null
                            ? bytes(attachment.declaredBytes)
                            : '大小未知'
                        }}
                        ·
                        {{
                          downloadBusy === attachment.attachmentId ? '正在下载…' : '下载到本机'
                        }}</small
                      ></span
                    ><AppIcon name="download" :size="17" />
                  </button>
                </div>
              </section>
              <div class="message-bottom">
                <button class="button secondary reply-button" @click="derive(false)">
                  <AppIcon name="reply" :size="17" />回复此邮件</button
                ><button class="button ghost" @click="derive(true)">
                  <AppIcon name="forward" :size="17" />转发</button
                ><span class="spacer" /><span v-if="body?.cached" class="body-cache-status"
                  ><AppIcon name="check" :size="13" />已缓存，可离线阅读</span
                >
              </div>
              <p class="reading-privacy">
                <AppIcon name="shield" :size="13" />已屏蔽外部图片，邮件中的脚本不会执行。
              </p>
            </div>
            <div v-else class="empty-state reading-empty">
              <div class="empty-letter"><AppIcon name="mail" :size="56" /></div>
              <h2>给来信一点专注。</h2>
              <p>选择一封邮件，在这里慢慢阅读。</p>
              <span><AppIcon name="shield" :size="14" />你的邮件，只留在本机。</span>
            </div>
          </article>
        </section>
      </template>
      <AccountsPanel
        v-else-if="page === '/accounts'"
        @add="openAccount()"
        @reauthorize="openAccount"
      />
      <SettingsPanel v-else-if="page === '/settings'" />
      <section v-else-if="page === '/drafts'" class="page-content drafts-page">
        <div class="page-heading">
          <div>
            <span class="eyebrow">WORDS CAN WAIT</span>
            <h1>草稿箱</h1>
            <p class="muted">没写完的话，替你好好保存。</p>
          </div>
          <button class="button primary" @click="compose">
            <AppIcon name="compose" :size="17" />写邮件
          </button>
        </div>
        <div v-if="!drafts.length" class="empty-state full-empty">
          <span class="empty-icon"><AppIcon name="draft" :size="36" /></span>
          <h3>这里还没有草稿</h3>
          <p>写邮件时会自动保存，关闭后也能继续。</p>
        </div>
        <div v-else class="draft-list">
          <button
            v-for="draft in drafts"
            :key="draft.draftId"
            class="draft-row"
            @click="editDraft(draft)"
          >
            <span class="draft-icon"><AppIcon name="draft" :size="22" /></span>
            <div>
              <h3>
                {{ draft.subject || '（无主题草稿）'
                }}<span v-if="draft.state === 'failed'" class="draft-status error"
                  >发送失败 · 已保留</span
                ><span v-else-if="draft.state === 'unknown'" class="draft-status error"
                  >发送结果待核对</span
                ><span v-else-if="['queued', 'sending'].includes(draft.state)" class="draft-status"
                  >提交中</span
                >
              </h3>
              <p>
                {{
                  draft.to.length
                    ? '发送至 ' + draft.to.map((a) => a.address).join('，')
                    : '尚未填写收件人'
                }}
              </p>
              <p class="draft-preview">{{ draft.bodyText.slice(0, 100) || '还没有正文内容' }}</p>
            </div>
            <time>{{ shortDate(draft.updatedAt) }}</time
            ><AppIcon name="chevron" :size="17" /></button
          ><button v-if="draftHasMore" class="load-more" @click="loadDrafts(true)">
            加载更多草稿
          </button>
        </div>
      </section>
      <section v-else class="page-content activity-page">
        <div class="page-heading">
          <div>
            <span class="eyebrow">EVERYTHING IN ITS OWN TIME</span>
            <h1>同步记录</h1>
            <p class="muted">每一次汇入，都有迹可循。</p>
          </div>
          <button class="button secondary" :disabled="syncing" @click="sync">
            <AppIcon name="sync" :size="16" />同步全部邮箱
          </button>
        </div>
        <div class="activity-summary">
          <div>
            <span class="status-dot" /><strong>{{ activeJobs.length }}</strong
            >个任务待处理
          </div>
          <p>网络失败按 1 / 2 / 4 / 8 分钟退避重试，手动重试会重置等待。</p>
        </div>
        <div v-if="!state.jobs.length" class="empty-state full-empty">
          <AppIcon name="clock" :size="32" />
          <h3>还没有同步记录</h3>
          <p>添加邮箱后，同步进度会显示在这里。</p>
        </div>
        <div v-else class="jobs-table">
          <div class="jobs-header">
            <span>任务 / 邮箱</span><span>进度</span><span>状态</span><span />
          </div>
          <div v-for="job in state.jobs" :key="job.jobId" class="job-row">
            <div class="job-description">
              <span class="job-icon" :class="{ complete: job.state === 'completed' }"
                ><AppIcon
                  :name="job.state === 'completed' ? 'check' : 'sync'"
                  :size="17"
                  :class="{ spinning: job.state === 'running' }"
              /></span>
              <div>
                <strong>{{ jobNames[job.kind] ?? '邮件任务' }}</strong
                ><small>{{
                  state.accounts.find((a) => a.accountId === job.accountId)?.address ?? '本地工作区'
                }}</small>
                <p v-if="job.lastErrorCode" class="job-error">
                  {{ errors[job.lastErrorCode] ?? '操作暂未完成，请重试或检查邮箱配置' }}
                </p>
              </div>
            </div>
            <div class="job-progress">
              <div>
                <span
                  :style="{
                    width:
                      job.state === 'completed'
                        ? '100%'
                        : Math.min(100, (job.progressDone / (job.progressTotal || 1)) * 100) + '%',
                  }"
                />
              </div>
              <small
                >{{ job.progressDone
                }}{{ job.progressTotal != null ? ' / ' + job.progressTotal : '' }}</small
              >
            </div>
            <div>
              <span class="job-state" :class="job.state">{{ jobStates[job.state] }}</span
              ><small v-if="job.state === 'retry'" class="retry-at"
                >{{ shortDate(job.nextRunAt) }} 重试</small
              >
            </div>
            <button
              v-if="['retry', 'failed', 'paused', 'auth-required'].includes(job.state)"
              class="text-button"
              @click="retry(job.jobId)"
            >
              重试
            </button>
          </div>
        </div>
      </section>
      <footer class="workspace-footer">
        <span
          ><span class="status-dot" />{{
            state.demo ? '合成数据演示 · 不连接真实邮箱' : '本地连接 · 不经过云端中转'
          }}</span
        ><span>邮汇 · 让来信有序</span>
      </footer>
    </main>
    <AccountDialog
      :open="accountsOpen"
      :account="reauthAccount"
      @close="accountsOpen = false"
      @done="done"
    /><ComposeDialog
      :open="composeOpen"
      :draft="editingDraft"
      @close="composeOpen = false"
      @done="done"
    />
  </div>
</template>

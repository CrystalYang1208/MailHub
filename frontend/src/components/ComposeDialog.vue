<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import { ElDialog } from 'element-plus';
import 'element-plus/es/components/dialog/style/css';
import AppIcon from './AppIcon.vue';
import {
  state,
  json,
  request,
  toast,
  showError,
  bytes,
  errors,
  type Draft,
  type Schema,
} from '../workspace';
type DraftWithSubmission = Draft & { activeSubmissionId?: string };
const props = defineProps<{ open: boolean; draft?: DraftWithSubmission | null }>();
const emit = defineEmits<{ close: []; done: [] }>();
const current = ref<DraftWithSubmission | null>(null);
const accountId = ref('');
const to = ref('');
const cc = ref('');
const bcc = ref('');
const subject = ref('');
const body = ref('');
const busy = ref(false);
const saving = ref(false);
const dirty = ref(false);
const moreRecipients = ref(false);
const submission = ref<Schema['Submission'] | null>(null);
const duplicateAcknowledged = ref(false);
let saveTimer: ReturnType<typeof setTimeout> | undefined;
let statusTimer: ReturnType<typeof setInterval> | undefined;
let savePromise: Promise<Draft> | null = null;
let filling = false;
const frozen = computed(
  () => current.value && !['editing', 'failed'].includes(current.value.state),
);
const title = computed(() =>
  current.value?.mode === 'reply'
    ? '回复邮件'
    : current.value?.mode === 'forward'
      ? '转发邮件'
      : '写一封新邮件',
);
function addresses(value: string): Schema['Address'][] {
  return value
    .split(/[,;，；\n]/)
    .map((a) => a.trim())
    .filter(Boolean)
    .map((a) => {
      const match = a.match(/^(.*?)<([^>]+)>$/);
      return match ? { name: match[1].trim(), address: match[2].trim() } : { address: a };
    });
}
function display(value: Schema['Address'][]) {
  return value.map((a) => (a.name ? `${a.name} <${a.address}>` : a.address)).join('; ');
}
function fill(draft: DraftWithSubmission | null) {
  filling = true;
  current.value = draft;
  accountId.value = draft?.accountId ?? state.accounts.find((a) => a.enabled)?.accountId ?? '';
  to.value = draft ? display(draft.to) : '';
  cc.value = draft ? display(draft.cc) : '';
  bcc.value = draft ? display(draft.bcc) : '';
  subject.value = draft?.subject ?? '';
  body.value = draft?.bodyText ?? '';
  moreRecipients.value = !!(cc.value || bcc.value);
  dirty.value = false;
  submission.value = null;
  duplicateAcknowledged.value = false;
  queueMicrotask(() => {
    filling = false;
  });
}
watch(
  () => props.open,
  async (open) => {
    clearTimeout(saveTimer);
    clearInterval(statusTimer);
    if (!open) return;
    fill(props.draft ?? null);
    if (current.value?.activeSubmissionId) {
      try {
        submission.value = await request<Schema['Submission']>(
          `/submissions/${current.value.activeSubmissionId}`,
        );
      } catch (error) {
        showError(error);
      }
    }
  },
);
watch([to, cc, bcc, subject, body], () => {
  if (!props.open || filling || frozen.value) return;
  dirty.value = true;
  clearTimeout(saveTimer);
  saveTimer = setTimeout(() => {
    void save().catch(showError);
  }, 1600);
});
async function save(): Promise<Draft> {
  if (savePromise) {
    await savePromise;
    if (!dirty.value && current.value) return current.value;
  }
  if (!dirty.value && current.value) return current.value;
  const content = {
    to: addresses(to.value),
    cc: addresses(cc.value),
    bcc: addresses(bcc.value),
    subject: subject.value,
    bodyText: body.value,
  };
  if (!accountId.value) throw new Error('请先添加并选择一个发件邮箱');
  saving.value = true;
  dirty.value = false;
  savePromise = current.value
    ? json<Draft>(`/drafts/${current.value.draftId}`, 'PUT', content, {
        'If-Match': `"${current.value.version}"`,
      })
    : json<Draft>('/drafts', 'POST', { accountId: accountId.value, ...content });
  try {
    current.value = await savePromise;
    return current.value;
  } catch (error) {
    dirty.value = true;
    throw error;
  } finally {
    saving.value = false;
    savePromise = null;
  }
}
async function close() {
  clearTimeout(saveTimer);
  if (dirty.value && !frozen.value) {
    try {
      await save();
    } catch (error) {
      showError(error);
      return;
    }
  }
  clearInterval(statusTimer);
  emit('done');
  emit('close');
}
async function send() {
  busy.value = true;
  clearTimeout(saveTimer);
  try {
    const draft = await save();
    submission.value = await json<Schema['Submission']>(
      `/drafts/${draft.draftId}/send`,
      'POST',
      { draftVersion: draft.version },
      { 'Idempotency-Key': crypto.randomUUID() },
    );
    current.value = await request<DraftWithSubmission>(`/drafts/${draft.draftId}`);
    clearInterval(statusTimer);
    statusTimer = setInterval(() => {
      void poll().catch(showError);
    }, 1000);
    await poll();
  } catch (error) {
    showError(error);
    busy.value = false;
  }
}
async function poll() {
  if (!submission.value) return;
  submission.value = await request<Schema['Submission']>(
    `/submissions/${submission.value.submissionId}`,
  );
  if (['accepted', 'failed', 'unknown'].includes(submission.value.state)) {
    clearInterval(statusTimer);
    busy.value = false;
    if (current.value)
      current.value = await request<DraftWithSubmission>(`/drafts/${current.value.draftId}`);
    emit('done');
    if (submission.value.state === 'accepted') {
      toast(
        state.demo ? '合成邮件已提交，未向真实邮箱发送' : '已提交邮件服务器，收件状态需由对端确认',
        'success',
      );
      emit('close');
    }
  }
}
async function resolve(decision: 'confirm-accepted' | 'allow-new-submission') {
  if (!submission.value || !current.value) return;
  busy.value = true;
  try {
    await json(`/submissions/${submission.value.submissionId}/resolve`, 'POST', {
      decision,
      confirmPossibleDuplicate: duplicateAcknowledged.value,
    });
    current.value = await request<DraftWithSubmission>(`/drafts/${current.value.draftId}`);
    if (decision === 'confirm-accepted') {
      emit('done');
      emit('close');
    } else {
      submission.value = null;
      toast('草稿可继续编辑，再次发送需由你主动操作');
    }
  } catch (error) {
    showError(error);
  } finally {
    busy.value = false;
  }
}
async function upload(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) return;
  if (
    file.size + (current.value?.attachments.reduce((sum, f) => sum + f.byteSize, 0) ?? 0) >
    25_000_000
  ) {
    showError(new Error('整封邮件附件总量不能超过25MB'));
    input.value = '';
    return;
  }
  busy.value = true;
  try {
    const draft = await save();
    const form = new FormData();
    form.append('file', file);
    await request(`/drafts/${draft.draftId}/attachments`, {
      method: 'POST',
      headers: { 'If-Match': `"${draft.version}"` },
      body: form,
    });
    current.value = await request<Draft>(`/drafts/${draft.draftId}`);
  } catch (error) {
    showError(error);
  } finally {
    busy.value = false;
    input.value = '';
  }
}
async function removeFile(id: string) {
  if (!current.value) return;
  try {
    await json(
      `/drafts/${current.value.draftId}/attachments/${id}`,
      'DELETE',
      {},
      { 'If-Match': `"${current.value.version}"` },
    );
    current.value = await request<Draft>(`/drafts/${current.value.draftId}`);
  } catch (error) {
    showError(error);
  }
}
async function discard() {
  if (!window.confirm('丢弃这封草稿？此操作无法撤销。')) return;
  clearTimeout(saveTimer);
  try {
    if (current.value)
      await json(
        `/drafts/${current.value.draftId}`,
        'DELETE',
        {},
        { 'If-Match': `"${current.value.version}"` },
      );
    dirty.value = false;
    emit('done');
    emit('close');
  } catch (error) {
    showError(error);
  }
}
onUnmounted(() => {
  clearTimeout(saveTimer);
  clearInterval(statusTimer);
});
</script>
<template>
  <ElDialog
    :model-value="open"
    :title="title"
    width="740px"
    class="compose-dialog"
    :close-on-click-modal="false"
    :close-on-press-escape="false"
    :show-close="false"
  >
    <template #header
      ><div class="compose-title">
        <span>{{ title }}</span
        ><span class="compose-save">{{
          saving ? '正在保存…' : dirty ? '编辑中' : current ? '已保存于本机' : '写下你想说的'
        }}</span
        ><button class="icon-button" aria-label="关闭并保存草稿" :disabled="busy" @click="close">
          <AppIcon name="close" />
        </button></div
    ></template>
    <div v-if="state.demo" class="compose-demo">
      合成演示 · 收件人请使用 name@example.invalid，不会发送真实邮件
    </div>
    <div v-if="submission?.state === 'unknown'" class="notice warning unknown-notice">
      <AppIcon name="info" />
      <div>
        <strong>邮件可能已提交，请先核对服务商的“已发送”</strong>
        <p>服务器确认丢失，系统不会自动重发，以免重复发送。</p>
        <div class="button-row">
          <button class="button secondary small" @click="resolve('confirm-accepted')">
            我已核对：服务器已接受
          </button>
        </div>
        <label class="checkbox-label"
          ><input
            v-model="duplicateAcknowledged"
            type="checkbox"
          />我已核对，理解再次发送可能产生重复邮件</label
        ><button
          class="text-button"
          :disabled="!duplicateAcknowledged || busy"
          @click="resolve('allow-new-submission')"
        >
          允许重新编辑，稍后手动发送
        </button>
      </div>
    </div>
    <div v-else-if="submission?.state === 'failed'" class="notice warning">
      <AppIcon name="info" />
      <p>
        {{ errors[submission.lastErrorCode ?? ''] ?? '发送失败，草稿和附件已保留' }}。可修改后重试。
      </p>
    </div>
    <div v-else-if="current?.lastErrorCode === 'THREAD_HEADERS_MISSING'" class="notice info">
      原邮件缺少有效线程头，将保留引用正文。
    </div>
    <form @submit.prevent="send">
      <div class="compose-fields">
        <label
          ><span>发件人</span
          ><select v-model="accountId" :disabled="!!current || !!frozen">
            <option
              v-for="account in state.accounts.filter((a) => a.enabled)"
              :key="account.accountId"
              :value="account.accountId"
            >
              {{ account.address }}
            </option></select
          ><AppIcon name="down" :size="15" /></label
        ><label
          ><span>收件人</span
          ><input
            v-model="to"
            :disabled="!!frozen"
            placeholder="邮箱地址，多个地址用分号分隔"
            aria-label="收件人"
          /><button class="text-button" type="button" @click="moreRecipients = !moreRecipients">
            抄送 / 密送
          </button></label
        ><template v-if="moreRecipients"
          ><label
            ><span>抄送</span
            ><input
              v-model="cc"
              :disabled="!!frozen"
              placeholder="抄送地址"
              aria-label="抄送" /></label
          ><label
            ><span>密送</span
            ><input
              v-model="bcc"
              :disabled="!!frozen"
              placeholder="密送地址"
              aria-label="密送" /></label></template
        ><label
          ><span>主题</span
          ><input
            v-model="subject"
            :disabled="!!frozen"
            placeholder="给这封邮件起个主题"
            maxlength="998"
            aria-label="邮件主题"
        /></label>
      </div>
      <textarea
        v-model="body"
        class="compose-body"
        :disabled="!!frozen"
        placeholder="你好，&#10;&#10;写下想说的话…"
        aria-label="邮件正文"
      />
      <div v-if="current?.attachments.length" class="compose-attachments">
        <div v-for="file in current.attachments" :key="file.fileId" class="attachment-chip">
          <AppIcon name="file" :size="17" /><span
            >{{ file.fileName }}<small>{{ bytes(file.byteSize) }}</small></span
          ><button
            type="button"
            class="icon-button"
            :disabled="!!frozen || busy"
            aria-label="移除附件"
            @click="removeFile(file.fileId)"
          >
            <AppIcon name="close" :size="14" />
          </button>
        </div>
      </div>
      <div class="compose-footer">
        <button type="submit" class="button primary" :disabled="busy || !!frozen">
          <AppIcon :name="busy ? 'sync' : 'send'" :class="{ spinning: busy }" :size="17" />{{
            busy ? '正在提交…' : '发送邮件'
          }}</button
        ><label class="icon-button attachment-upload" title="添加附件，合计不超过25MB"
          ><AppIcon name="paperclip" /><input
            type="file"
            :disabled="busy || !!frozen"
            @change="upload" /></label
        ><span class="field-note">附件合计 ≤25MB</span><span class="spacer" /><button
          type="button"
          class="icon-button"
          title="丢弃草稿"
          :disabled="busy || !!frozen"
          @click="discard"
        >
          <AppIcon name="trash" /></button
        ><button type="button" class="button ghost" :disabled="busy" @click="close">
          {{ frozen ? '关闭' : '保存并关闭' }}
        </button>
      </div>
    </form>
  </ElDialog>
</template>

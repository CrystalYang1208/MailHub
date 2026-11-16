<script setup lang="ts">
import { ref } from 'vue';
import AppIcon from './AppIcon.vue';
import {
  state,
  json,
  refreshWorkspace,
  showError,
  toast,
  providerName,
  providerColor,
  errors,
  type Account,
} from '../workspace';
defineEmits<{ add: []; reauthorize: [account: Account] }>();
const busy = ref('');
const statuses: Record<string, string> = {
  authorized: '连接正常',
  syncing: '正在同步',
  retry: '等待重试',
  'auth-required': '需要重新授权',
  disabled: '已停用',
  removing: '正在移除',
};
async function toggle(account: Account) {
  busy.value = account.accountId;
  try {
    await json(`/accounts/${account.accountId}`, 'PATCH', { enabled: !account.enabled });
    await refreshWorkspace();
    toast(account.enabled ? '邮箱已停用，配置与缓存保留' : '邮箱已启用', 'success');
  } catch (error) {
    showError(error);
  } finally {
    busy.value = '';
  }
}
async function remove(account: Account) {
  if (
    !window.confirm(
      `移除 ${account.address}？本机的该账号邮件、凭据与草稿将被清除，服务器邮件不受影响。`,
    )
  )
    return;
  busy.value = account.accountId;
  try {
    const result = await json<{ state: string }>(`/accounts/${account.accountId}`, 'DELETE');
    await refreshWorkspace();
    toast(
      result.state === 'done'
        ? '邮箱已移除，本机数据已清除'
        : '邮箱已移除，文件占用解除后将继续清理缓存',
      'success',
    );
  } catch (error) {
    showError(error);
  } finally {
    busy.value = '';
  }
}
async function sync(account: Account) {
  busy.value = account.accountId;
  try {
    await json('/sync', 'POST', { accountId: account.accountId });
    await refreshWorkspace();
    toast('已加入同步队列');
  } catch (error) {
    showError(error);
  } finally {
    busy.value = '';
  }
}
</script>
<template>
  <section class="page-content accounts-page">
    <div class="page-heading">
      <div>
        <span class="eyebrow">ALL YOUR INBOXES, TOGETHER</span>
        <h1>我的邮箱</h1>
        <p class="muted">{{ state.accounts.length }} 个邮箱，一处查看。你始终拥有控制权。</p>
      </div>
      <button class="button primary" @click="$emit('add')">
        <AppIcon name="plus" :size="18" />添加邮箱
      </button>
    </div>
    <div v-if="state.demo" class="notice info">
      <AppIcon name="info" />
      <p>当前为合成演示。下面的账号不对应真实邮箱，所有操作仅在演示数据中完成。</p>
    </div>
    <div class="account-cards">
      <article v-for="account in state.accounts" :key="account.accountId" class="account-card">
        <div class="account-card-header">
          <span
            class="account-provider-icon"
            :style="{ background: providerColor(account) + '15', color: providerColor(account) }"
            >{{
              account.providerKey === 'qq'
                ? 'Q'
                : account.providerKey === '163'
                  ? '163'
                  : providerName(account)[0]
            }}</span
          ><span
            class="connection-status"
            :class="{
              warning: ['retry', 'auth-required'].includes(account.status),
              muted: !account.enabled,
            }"
            ><i class="status-dot" />{{ statuses[account.status] }}</span
          >
        </div>
        <h2>{{ providerName(account) }}</h2>
        <p class="account-address">{{ account.address }}</p>
        <div class="account-meta">
          <span
            ><AppIcon name="lock" :size="13" />{{
              account.authType === 'oauth2' ? 'OAuth 2.0' : '凭据已加密'
            }}</span
          ><span
            >{{
              state.folders
                .filter((f) => f.accountId === account.accountId && f.role === 'inbox')
                .reduce((sum, f) => sum + f.unreadCount, 0)
            }}
            封未读</span
          >
        </div>
        <p v-if="account.lastErrorCode" class="account-error">
          {{ errors[account.lastErrorCode] ?? '连接待恢复，请核对配置或重新授权' }}
        </p>
        <div class="account-card-actions">
          <button class="text-button" :disabled="!!busy || !account.enabled" @click="sync(account)">
            <AppIcon name="sync" :size="15" />同步</button
          ><button class="text-button" :disabled="!!busy" @click="$emit('reauthorize', account)">
            重新授权</button
          ><span class="spacer" /><button
            class="text-button muted"
            :disabled="!!busy"
            @click="toggle(account)"
          >
            {{ account.enabled ? '停用' : '启用' }}</button
          ><button
            class="icon-button danger"
            :disabled="!!busy"
            title="移除账号"
            @click="remove(account)"
          >
            <AppIcon name="trash" :size="16" />
          </button>
        </div>
      </article>
      <button class="account-card add-account-card" @click="$emit('add')">
        <span class="add-account-circle"><AppIcon name="plus" :size="25" /></span
        ><strong>再添加一个邮箱</strong>
        <p>QQ、163、126、Gmail、Outlook<br />以及支持标准IMAP的邮箱</p>
      </button>
    </div>
    <div class="accounts-note">
      <AppIcon name="shield" :size="19" />
      <p>移除邮箱会同时清除本机凭据与缓存。<br />停用邮箱保留配置与缓存，停止后续同步。</p>
    </div>
  </section>
</template>

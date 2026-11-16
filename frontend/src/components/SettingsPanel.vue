<script setup lang="ts">
import { ref, watch } from 'vue';
import AppIcon from './AppIcon.vue';
import {
  state,
  json,
  request,
  toast,
  showError,
  refreshWorkspace,
  forgetUser,
  bytes,
  type Schema,
} from '../workspace';
const minutes = ref(10);
const cacheMb = ref(500);
const busy = ref(false);
const currentPassword = ref('');
const newPassword = ref('');
watch(
  () => state.settings,
  (settings) => {
    if (settings) {
      minutes.value = settings.syncIntervalMinutes;
      cacheMb.value = settings.cacheLimitBytes / 1_000_000;
    }
  },
  { immediate: true },
);
async function save() {
  busy.value = true;
  try {
    state.settings = await json<Schema['Settings']>('/settings', 'PATCH', {
      syncIntervalMinutes: minutes.value,
      cacheLimitBytes: Math.round(cacheMb.value * 1_000_000),
    });
    toast('同步与缓存设置已保存', 'success');
  } catch (error) {
    showError(error);
  } finally {
    busy.value = false;
  }
}
async function clear(all: boolean) {
  if (
    !window.confirm(
      all
        ? '清除本用户已同步的邮件、附件缓存与搜索索引？账号配置和草稿会保留，并暂停自动补拉。'
        : '清除已缓存的阅读正文？邮件头、搜索索引和草稿都会保留，断网时这些正文将无法阅读。',
    )
  )
    return;
  busy.value = true;
  try {
    await json(all ? '/settings/mail-data/clear' : '/settings/body-cache', all ? 'POST' : 'DELETE');
    await refreshWorkspace();
    toast(all ? '本地邮件已清除，自动同步已暂停' : '阅读缓存已清除，搜索索引仍保留', 'success');
  } catch (error) {
    showError(error);
  } finally {
    busy.value = false;
  }
}
async function importClient(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) return;
  busy.value = true;
  try {
    if (file.size > 65536) throw new Error('客户端配置文件应小于64KB');
    const body: unknown = JSON.parse(await file.text());
    await json('/settings/oauth-client', 'PUT', body);
    await refreshWorkspace();
    toast('Google客户端配置已加密保存，不会回显', 'success');
  } catch (error) {
    showError(error);
  } finally {
    input.value = '';
    busy.value = false;
  }
}
async function changePassword() {
  busy.value = true;
  try {
    await json('/users/current/password', 'PUT', {
      currentPassword: currentPassword.value,
      newPassword: newPassword.value,
    });
    currentPassword.value = '';
    newPassword.value = '';
    forgetUser();
    state.users = await request<Schema['User'][]>('/users', {}, 'bridge');
    toast('密码已更新，请使用新密码重新登录', 'success');
  } catch (error) {
    showError(error);
  } finally {
    busy.value = false;
  }
}
async function deleteUser() {
  const password = window.prompt(
    '删除本地用户会清除该用户的账号、草稿和邮件，无法撤销。输入本地密码确认：',
  );
  if (!password) return;
  busy.value = true;
  try {
    await json('/users/current', 'DELETE', { userId: state.user?.userId, password });
    forgetUser();
    state.users = await request<Schema['User'][]>('/users', {}, 'bridge');
    toast('该本地用户及其数据已删除');
  } catch (error) {
    showError(error);
  } finally {
    busy.value = false;
  }
}
</script>
<template>
  <section class="page-content settings-page">
    <div class="page-heading">
      <div>
        <span class="eyebrow">MAKE IT YOURS</span>
        <h1>设置与隐私</h1>
        <p class="muted">让工作台，按你的习惯运行。</p>
      </div>
      <span class="local-pill"
        ><AppIcon name="shield" :size="15" />{{
          state.demo ? '合成演示空间' : '数据仅保存在本机'
        }}</span
      >
    </div>
    <div class="settings-columns">
      <div class="settings-main">
        <section class="settings-card">
          <div class="section-title">
            <AppIcon name="sync" />
            <div>
              <h2>同步与缓存</h2>
              <p>轻一点，也及时一点。</p>
            </div>
          </div>
          <form @submit.prevent="save">
            <div class="setting-row">
              <div>
                <strong>自动同步间隔</strong>
                <p>锁定用户后暂停同步，重新登录后恢复。</p>
              </div>
              <div class="number-input">
                <input
                  v-model.number="minutes"
                  aria-label="自动同步间隔"
                  type="number"
                  min="5"
                  max="60"
                  required
                /><span>分钟</span>
              </div>
            </div>
            <div class="setting-row">
              <div>
                <strong>正文缓存上限</strong>
                <p>按最近阅读时间淘汰正文，保留邮件头和搜索索引。</p>
              </div>
              <div class="number-input">
                <input
                  v-model.number="cacheMb"
                  aria-label="正文缓存上限"
                  type="number"
                  min="0.001"
                  step="0.001"
                  required
                /><span>MB</span>
              </div>
            </div>
            <div class="cache-meter">
              <div>
                <span
                  :style="{
                    width:
                      Math.min(
                        100,
                        ((state.settings?.cacheUsedBytes ?? 0) /
                          (state.settings?.cacheLimitBytes || 1)) *
                          100,
                      ) + '%',
                  }"
                />
              </div>
              <p>
                已用 {{ bytes(state.settings?.cacheUsedBytes ?? 0)
                }}<span>上限 {{ bytes(state.settings?.cacheLimitBytes ?? 500_000_000) }}</span>
              </p>
            </div>
            <div v-if="state.settings?.autoSyncPaused" class="notice info">
              <AppIcon name="info" :size="16" />
              <p>自动同步已暂停。点击工作台的“同步”可恢复补拉。</p>
            </div>
            <button class="button primary small" type="submit" :disabled="busy">保存设置</button>
          </form>
        </section>
        <section class="settings-card">
          <div class="section-title">
            <AppIcon name="shield" />
            <div>
              <h2>Google 授权配置</h2>
              <p>由你自己的Google Cloud项目提供授权。</p>
            </div>
          </div>
          <p class="setting-description">
            在Google
            Cloud创建“桌面应用”OAuth客户端，将测试邮箱加入测试用户，下载JSON后在此导入。随后添加Gmail时选择OAuth授权。配置只加密保存在当前本地用户空间。
          </p>
          <div class="button-row">
            <label
              class="button secondary small file-label"
              :class="{ disabled: state.demo || busy }"
              ><AppIcon name="file" :size="16" />{{
                state.settings?.hasGoogleOAuthClient ? '替换客户端 JSON' : '导入客户端 JSON'
              }}<input
                type="file"
                accept=".json,application/json"
                :disabled="state.demo || busy"
                @change="importClient" /></label
            ><span class="field-note">{{
              state.demo
                ? '真实授权请使用Win11正式模式'
                : state.settings?.hasGoogleOAuthClient
                  ? '已配置 · 不回显内容'
                  : '尚未配置'
            }}</span>
          </div>
        </section>
        <section class="settings-card">
          <div class="section-title">
            <AppIcon name="lock" />
            <div>
              <h2>本地用户密码</h2>
              <p>更换后所有旧会话立即失效。</p>
            </div>
          </div>
          <form class="password-form" @submit.prevent="changePassword">
            <label
              >当前密码<input
                v-model="currentPassword"
                type="password"
                autocomplete="current-password"
                required /></label
            ><label
              >新密码<input
                v-model="newPassword"
                type="password"
                autocomplete="new-password"
                minlength="10"
                maxlength="128"
                placeholder="至少10个字符"
                required /></label
            ><button class="button secondary small" type="submit" :disabled="busy">
              更新密码并锁定
            </button>
          </form>
        </section>
      </div>
      <aside class="settings-aside">
        <section class="privacy-card">
          <div class="privacy-illustration"><AppIcon name="shield" :size="36" /></div>
          <h3>你的来信，只属于你。</h3>
          <p>无需平台云账号，邮件不经过运营方中转，也不会交给云端AI。</p>
          <ul>
            <li><AppIcon name="check" :size="15" /> 每个本地用户独立存储</li>
            <li><AppIcon name="check" :size="15" /> 授权凭据加密保存</li>
            <li><AppIcon name="check" :size="15" /> 外部图片默认屏蔽</li>
          </ul>
        </section>
        <section class="settings-card compact">
          <h3>本机空间占用</h3>
          <dl>
            <div>
              <dt>阅读正文缓存</dt>
              <dd>{{ bytes(state.settings?.cacheUsedBytes ?? 0) }}</dd>
            </div>
            <div>
              <dt>搜索文本载荷</dt>
              <dd>{{ bytes(state.settings?.indexBytes ?? 0) }}</dd>
            </div>
            <div>
              <dt>附件文件</dt>
              <dd>{{ bytes(state.settings?.attachmentBytes ?? 0) }}</dd>
            </div>
            <div>
              <dt>用户数据库</dt>
              <dd>{{ bytes(state.settings?.databaseBytes ?? 0) }}</dd>
            </div>
            <div>
              <dt>数据库日志</dt>
              <dd>{{ bytes(state.settings?.walBytes ?? 0) }}</dd>
            </div>
          </dl>
          <p class="field-note">搜索文本载荷为逻辑大小；数据库包含索引等结构，不与其重复相加。</p>
        </section>
        <section class="settings-card compact data-actions">
          <h3>管理本地数据</h3>
          <button class="text-button" :disabled="busy" @click="clear(false)">清除阅读缓存</button
          ><button class="text-button" :disabled="busy" @click="clear(true)">清除已同步邮件</button
          ><button class="text-button danger" :disabled="busy" @click="deleteUser">
            删除当前本地用户
          </button>
          <p class="field-note">清除操作只影响本机数据，不会删除服务器上的邮件。</p>
        </section>
      </aside>
    </div>
  </section>
</template>

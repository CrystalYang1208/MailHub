<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import { ElDialog } from 'element-plus';
import 'element-plus/es/components/dialog/style/css';
import AppIcon from './AppIcon.vue';
import {
  json,
  request,
  state,
  showError,
  refreshWorkspace,
  type Schema,
  type Account,
} from '../workspace';
const props = defineProps<{ open: boolean; account?: Account | null }>();
const emit = defineEmits<{ close: []; done: [] }>();
const step = ref(0);
const providers = ref<Schema['Provider'][]>([]);
const chosen = ref(0);
const address = ref('');
const secret = ref('');
const auth = ref('authorization-code');
const imap = ref<Schema['ServerConfig']>({ host: '', port: 993, security: 'tls' });
const smtp = ref<Schema['ServerConfig']>({ host: '', port: 465, security: 'tls' });
const busy = ref(false);
const oauthFlow = ref('');
const oauthReady = ref(false);
const oauthBusy = ref(false);
const advanced = ref(false);
const provider = computed(() => providers.value[chosen.value]);
let oauthTimer: ReturnType<typeof setInterval> | undefined;
function choose(index: number) {
  chosen.value = index;
  const p = providers.value[index];
  if (!p) return;
  imap.value = { ...p.imap };
  smtp.value = { ...p.smtp };
  auth.value = p.authTypes[0];
  oauthReady.value = false;
  oauthFlow.value = '';
  if (state.demo) {
    auth.value = 'authorization-code';
    secret.value = 'demo';
    address.value = `${p.providerKey}.new.${crypto.randomUUID().slice(0, 4)}@example.invalid`;
  }
}
watch(
  () => props.open,
  async (open) => {
    if (!open) {
      secret.value = '';
      clearInterval(oauthTimer);
      return;
    }
    step.value = props.account ? 1 : 0;
    secret.value = '';
    address.value = '';
    oauthReady.value = false;
    oauthFlow.value = '';
    busy.value = false;
    try {
      providers.value = await request<Schema['Provider'][]>('/providers');
      choose(0);
      if (props.account) {
        const index = providers.value.findIndex((p) => p.imap.host === props.account?.imap?.host);
        choose(index < 0 ? providers.value.length - 1 : index);
        address.value = props.account.address;
        imap.value = { ...props.account.imap! };
        smtp.value = { ...props.account.smtp! };
        auth.value = props.account.authType;
        secret.value = state.demo ? 'demo' : '';
      }
    } catch (error) {
      showError(error);
    }
  },
);
async function authorize() {
  oauthBusy.value = true;
  try {
    const flow = await json<Schema['OAuthFlow']>('/oauth/flows', 'POST', {
      providerKey: 'gmail',
      address: address.value,
      ...(props.account ? { accountId: props.account.accountId } : {}),
    });
    oauthFlow.value = flow.flowId;
    if (flow.authorizationUrl) window.open(flow.authorizationUrl, '_blank', 'noopener,noreferrer');
    clearInterval(oauthTimer);
    oauthTimer = setInterval(async () => {
      try {
        const result = await request<Schema['OAuthFlow']>(`/oauth/flows/${flow.flowId}`);
        if (result.status === 'authorized') {
          oauthReady.value = true;
          oauthBusy.value = false;
          clearInterval(oauthTimer);
        } else if (result.status !== 'pending') {
          oauthBusy.value = false;
          clearInterval(oauthTimer);
          showError(new Error('Google授权未完成或已过期，请重新尝试'));
        }
      } catch (error) {
        clearInterval(oauthTimer);
        oauthBusy.value = false;
        showError(error);
      }
    }, 1500);
  } catch (error) {
    oauthBusy.value = false;
    showError(error);
  }
}
async function connect() {
  if (!provider.value) return;
  busy.value = true;
  try {
    const credential =
      auth.value === 'oauth2'
        ? { type: 'oauth2', oauthFlowId: oauthFlow.value }
        : { type: auth.value, secret: secret.value };
    if (props.account)
      await json(`/accounts/${props.account.accountId}/reauthorize`, 'POST', credential);
    else {
      const validation = await json<Schema['AccountValidation']>('/accounts/validations', 'POST', {
        providerKey: provider.value.providerKey,
        address: address.value,
        imap: imap.value,
        smtp: smtp.value,
        credential,
      });
      await json('/accounts', 'POST', { validationId: validation.validationId });
    }
    secret.value = '';
    step.value = 2;
    await refreshWorkspace();
  } catch (error) {
    showError(error);
  } finally {
    busy.value = false;
  }
}
onUnmounted(() => clearInterval(oauthTimer));
</script>
<template>
  <ElDialog
    :model-value="open"
    :title="account ? '重新授权邮箱' : '添加一个邮箱'"
    width="640px"
    :close-on-click-modal="false"
    :close-on-press-escape="!busy"
    :show-close="!busy"
    @update:model-value="!$event && emit('close')"
  >
    <div class="stepper">
      <span
        v-for="(label, index) in ['选择邮箱', '授权与连接', '准备就绪']"
        :key="label"
        :class="{ active: step === index, complete: step > index }"
        ><i>{{ step > index ? '✓' : index + 1 }}</i
        >{{ label }}</span
      >
    </div>
    <template v-if="step === 0"
      ><h3 class="dialog-heading">从哪个邮箱开始？</h3>
      <p class="muted">常见邮箱已预填服务器参数，也支持其他IMAP邮箱。</p>
      <div class="provider-grid">
        <button
          v-for="(p, index) in providers"
          :key="p.label"
          :aria-label="p.label"
          class="provider-option"
          :class="{ selected: chosen === index }"
          @click="choose(index)"
        >
          <span aria-hidden="true" class="provider-letter" :class="p.providerKey">{{
            p.label.startsWith('QQ')
              ? 'Q'
              : p.label.startsWith('163')
                ? '163'
                : p.label.startsWith('126')
                  ? '126'
                  : p.label[0]
          }}</span
          >{{ p.label }}<AppIcon v-if="chosen === index" name="check" :size="16" />
        </button></div
    ></template>
    <template v-else-if="step === 1">
      <div v-if="state.demo" class="notice info">
        <AppIcon name="info" :size="17" />
        <p>
          这是合成演示，仅接受 example.invalid 地址，授权码使用
          demo。真实邮箱请从Win11正式模式添加。
        </p>
      </div>
      <div class="guide-box">
        <h4>{{ provider?.label }} · 连接指引</h4>
        <ol>
          <li v-for="guide in provider?.guideSteps" :key="guide">{{ guide }}</li>
        </ol>
      </div>
      <form id="connect-account" class="account-form" @submit.prevent="connect">
        <label
          >邮箱地址<input
            v-model="address"
            type="email"
            autocomplete="off"
            placeholder="你的完整邮箱地址"
            required
            :disabled="!!account" /></label
        ><label v-if="provider && provider.authTypes.length > 1 && !state.demo"
          >认证方式<select v-model="auth">
            <option v-for="type in provider.authTypes" :key="type" :value="type">
              {{
                type === 'oauth2'
                  ? 'Google OAuth 2.0（推荐）'
                  : type === 'app-password'
                    ? '应用专用密码'
                    : type === 'authorization-code'
                      ? '客户端授权码'
                      : '服务商允许的密码'
              }}
            </option>
          </select></label
        >
        <template v-if="auth === 'oauth2' && !state.demo"
          ><button
            class="button secondary"
            type="button"
            :disabled="oauthBusy || oauthReady || !address"
            @click="authorize"
          >
            <AppIcon :name="oauthReady ? 'check' : 'shield'" />{{
              oauthReady
                ? 'Google授权已完成'
                : oauthBusy
                  ? '等待浏览器授权…'
                  : '在浏览器中授权 Google'
            }}
          </button>
          <p class="field-note">需先在设置中导入你自己的Google桌面客户端JSON。</p></template
        >
        <label v-else
          >{{ auth === 'password' ? '邮箱密码' : '授权码 / 应用专用密码'
          }}<input
            v-model="secret"
            type="password"
            autocomplete="new-password"
            placeholder="仅保存在本机，添加后不回显"
            required
            maxlength="4096"
        /></label>
        <button
          v-if="!account"
          class="text-button advanced-toggle"
          type="button"
          @click="advanced = !advanced"
        >
          {{ advanced ? '收起' : '查看 / 修改' }}服务器参数<AppIcon name="down" :size="14" />
        </button>
        <div v-if="advanced && !account" class="server-grid">
          <div v-for="kind in ['imap', 'smtp'] as const" :key="kind">
            <h4>{{ kind.toUpperCase() }}</h4>
            <label
              >服务器<input
                v-model="(kind === 'imap' ? imap : smtp).host"
                required
                placeholder="imap.example.com"
            /></label>
            <div class="two-fields">
              <label
                >端口<input
                  v-model.number="(kind === 'imap' ? imap : smtp).port"
                  type="number"
                  min="1"
                  max="65535"
                  required /></label
              ><label
                >安全连接<select v-model="(kind === 'imap' ? imap : smtp).security">
                  <option value="tls">TLS</option>
                  <option value="starttls">STARTTLS</option>
                </select></label
              >
            </div>
          </div>
        </div>
      </form>
    </template>
    <div v-else class="connection-success">
      <div class="success-circle"><AppIcon name="check" :size="30" /></div>
      <h3>邮箱已连接，来信正在汇入。</h3>
      <p class="muted">{{ address }}</p>
      <p>
        首次同步最近90天的邮件，进度可在同步记录查看。<br />授权凭据已加密保存，随时可以移除账号。
      </p>
    </div>
    <template #footer
      ><div class="dialog-footer">
        <button
          v-if="step === 1 && !account"
          class="button ghost"
          :disabled="busy"
          @click="step = 0"
        >
          上一步</button
        ><span v-else class="small-lock"><AppIcon name="lock" :size="14" /> 本地加密保存</span
        ><button v-if="step === 0" class="button primary" @click="step = 1">
          下一步<AppIcon name="arrow" :size="16" /></button
        ><button
          v-else-if="step === 1"
          class="button primary"
          type="submit"
          form="connect-account"
          :disabled="busy || (auth === 'oauth2' && !state.demo && !oauthReady)"
        >
          <AppIcon v-if="busy" name="sync" class="spinning" :size="16" />{{
            busy ? '正在验证连接…' : '校验并连接'
          }}</button
        ><button
          v-else
          class="button primary"
          @click="
            emit('done');
            emit('close');
          "
        >
          完成，查看邮件<AppIcon name="arrow" :size="16" />
        </button></div
    ></template>
  </ElDialog>
</template>

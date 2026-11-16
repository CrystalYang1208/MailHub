<script setup lang="ts">
import { ref } from 'vue';
import AppIcon from './AppIcon.vue';
import { state, signIn, register, enterDemo, showError } from '../workspace';
const registering = ref(false);
const name = ref('');
const password = ref('');
const selected = ref('');
const busy = ref(false);
async function submit() {
  busy.value = true;
  try {
    if (registering.value) await register(name.value, password.value);
    else await signIn(selected.value || state.users[0]?.userId || '', password.value);
    password.value = '';
  } catch (error) {
    showError(error);
  } finally {
    busy.value = false;
  }
}
async function demo() {
  busy.value = true;
  try {
    await enterDemo();
  } catch (error) {
    showError(error);
  } finally {
    busy.value = false;
  }
}
</script>
<template>
  <main class="auth-layout">
    <section class="auth-story">
      <div class="brand">
        <span class="brand-mark"><AppIcon name="mail" :size="25" /></span
        ><span>邮汇 <small>MailHub</small></span>
      </div>
      <div class="auth-story-content">
        <span class="eyebrow">A LITTLE MORE FOCUS.</span>
        <h1>让每一封来信，<br />都有归处<span>。</span></h1>
        <p>把分散的邮箱，汇成一处从容。<br />看见重要的事，也留一点时间给自己。</p>
        <div class="letter-art">
          <div class="letter-paper">
            <div class="letter-stamp">M</div>
            <span /><span /><span />
            <p>写给专注的你</p>
          </div>
          <div class="letter-envelope"><AppIcon name="mail" :size="64" /></div>
          <span class="art-tag tag-one"
            ><i class="provider-dot" style="background: #bc775f" />QQ 邮箱</span
          ><span class="art-tag tag-two"
            ><i class="provider-dot" style="background: #6e8f86" />Gmail</span
          ><span class="art-tag tag-three"
            ><i class="provider-dot" style="background: #8294ae" />163 邮箱</span
          >
        </div>
      </div>
      <p class="auth-footnote"><AppIcon name="shield" :size="16" /> 本地运行 · 隐私由你掌握</p>
    </section>
    <section class="auth-entry">
      <div class="auth-card">
        <span class="local-pill"
          ><span class="status-dot" />{{
            state.demo ? '独立演示空间 · 合成邮件' : '你的本地邮件工作台'
          }}</span
        >
        <h2>
          {{
            state.startupError
              ? '重新打开工作台'
              : registering || (!state.users.length && !state.demo)
                ? '创建你的本地空间'
                : '欢迎回来'
          }}
        </h2>
        <p class="muted">
          {{ state.startupError ? state.startupError : '账号、邮件与草稿，都留在这台电脑上。' }}
        </p>
        <div v-if="state.initializing" class="auth-loading">
          <span class="spinner" /> 正在连接本地程序…
        </div>
        <template v-else-if="state.ready">
          <template v-if="state.demo && !registering"
            ><button class="button primary demo-enter" :disabled="busy" @click="demo">
              <AppIcon name="arrow" />{{ busy ? '正在准备演示…' : '进入演示工作台' }}
            </button>
            <p class="auth-demo-note">
              无需邮箱账号。使用三个合成邮箱体验完整操作，<br />不会向真实邮箱发送任何邮件。
            </p>
            <div class="or-divider"><span>或使用其他本地身份</span></div></template
          >
          <form @submit.prevent="submit">
            <label v-if="registering || (!state.users.length && !state.demo)"
              >本地用户名<input
                v-model="name"
                autocomplete="nickname"
                maxlength="40"
                placeholder="例如：林同学"
                required
                @input="registering = true"
            /></label>
            <label v-else
              >选择本地用户<select v-model="selected">
                <option v-for="user in state.users" :key="user.userId" :value="user.userId">
                  {{ user.displayName }}
                </option>
                <option v-if="!selected" value="" disabled>选择你的身份</option>
              </select></label
            >
            <label
              >本地用户密码<input
                v-model="password"
                type="password"
                :autocomplete="registering ? 'new-password' : 'current-password'"
                :minlength="registering ? 10 : 1"
                maxlength="128"
                :placeholder="registering ? '至少10个字符，用于保护本地凭据' : '输入你的本地密码'"
                required
            /></label>
            <button
              class="button"
              :class="state.demo ? 'secondary' : 'primary'"
              type="submit"
              :disabled="busy || (!state.users.length && !registering)"
            >
              {{ busy ? '正在解锁…' : registering ? '创建并进入工作台' : '解锁工作台'
              }}<AppIcon name="arrow" :size="17" />
            </button>
          </form>
          <p class="auth-switch">
            {{ registering ? '已有本地身份？' : '第一次使用？'
            }}<button
              class="text-button"
              @click="
                registering = !registering;
                password = '';
              "
            >
              {{ registering ? '返回登录' : '创建本地用户' }}
            </button>
          </p>
        </template>
        <div class="auth-privacy">
          <AppIcon name="lock" :size="17" />
          <p>
            本地密码用于解锁你的空间，与邮箱密码不同。<br />切换身份后，彼此的邮件与凭据互不可见。
          </p>
        </div>
      </div>
      <div class="auth-copyright">邮汇 MailHub <span>让来信有序，让生活从容。</span></div>
    </section>
  </main>
</template>

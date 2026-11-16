<script setup lang="ts">
import { inject, onMounted } from 'vue';
import AuthView from './components/AuthView.vue';
import StatusView from './views/StatusView.vue';
import AppIcon from './components/AppIcon.vue';
import { state, initialize } from './workspace';
import { ElConfigProvider } from 'element-plus';
import zhCn from 'element-plus/es/locale/lang/zh-cn';
const code = inject<string | null>('bootstrapCode', null);
onMounted(() => {
  void initialize(code);
});
</script>
<template>
  <ElConfigProvider :locale="zhCn">
    <StatusView v-if="state.engineering" />
    <RouterView v-else-if="state.user" />
    <AuthView v-else />
    <div class="toast-stack" aria-live="polite">
      <div v-for="item in state.toasts" :key="item.id" class="app-toast" :class="item.type">
        <AppIcon :name="item.type === 'success' ? 'check' : 'info'" :size="18" /><span>{{
          item.message
        }}</span
        ><button
          class="icon-button"
          aria-label="关闭提示"
          @click="state.toasts = state.toasts.filter((t) => t.id !== item.id)"
        >
          <AppIcon name="close" :size="14" />
        </button>
      </div>
    </div>
  </ElConfigProvider>
</template>

<script setup lang="ts">
import { inject, ref } from 'vue';
import { ElButton } from 'element-plus';
import 'element-plus/es/components/button/style/css';
import { ApiError, createApiClient } from '@/api/client';

const code = inject<string | null>('bootstrapCode', null);
let token: string | undefined;
const request = createApiClient({ bridgeToken: () => token, sessionToken: () => undefined });
const rows = ref<{ subject: string }[]>([]);
const status = ref('等待检查');
const detail = ref('');
const busy = ref(false);
async function check() {
  busy.value = true;
  try {
    if (!token) {
      if (!code)
        throw new ApiError(
          'BOOTSTRAP_MISSING',
          '启动链接缺少临时引导码',
          '请运行启动脚本，不要手工刷新旧链接',
        );
      const access = await request<{ bridgeToken: string }>(
        '/_engineering/bootstrap',
        {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ code }),
        },
        'none',
      );
      token = access.bridgeToken;
    }
    const result = await request<{ items: { subject: string }[]; sqliteVersion: string }>(
      '/_engineering/items',
      {},
      'bridge',
    );
    rows.value = result.items;
    detail.value = `SQLite ${result.sqliteVersion} · 仅合成数据`;
    status.value = '工程联通成功';
  } catch (error) {
    status.value = error instanceof ApiError ? error.message : '本地请求失败';
    detail.value = error instanceof ApiError ? error.action : '请查看脱敏检查记录';
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <main>
    <h1>邮汇 MailHub</h1>
    <p>工程基础验证 · 尚未开放邮箱业务功能</p>
    <ElButton type="primary" :loading="busy" @click="check">检查工程联通</ElButton>
    <p role="status">{{ status }}</p>
    <p>{{ detail }}</p>
    <ul>
      <li v-for="row in rows" :key="row.subject">{{ row.subject }}</li>
    </ul>
  </main>
</template>

<style scoped>
main {
  max-width: 48rem;
  margin: 4rem auto;
  padding: 2rem;
  font-family: system-ui, sans-serif;
  color: #17243a;
}
</style>

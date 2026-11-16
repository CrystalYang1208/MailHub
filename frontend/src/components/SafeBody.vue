<script setup lang="ts">
import { computed, ref } from 'vue';
import DOMPurify from 'dompurify';
const props = defineProps<{ html?: string; text?: string }>();
const plain = ref(false);
const source = computed(() => {
  const safe = DOMPurify.sanitize(props.html ?? '', {
    ALLOWED_TAGS: [
      'p',
      'br',
      'strong',
      'b',
      'em',
      'i',
      'u',
      'h1',
      'h2',
      'h3',
      'h4',
      'ul',
      'ol',
      'li',
      'blockquote',
      'pre',
      'code',
      'table',
      'thead',
      'tbody',
      'tr',
      'td',
      'th',
      'hr',
      'span',
      'div',
    ],
    ALLOWED_ATTR: [],
    ALLOW_DATA_ATTR: false,
    ALLOW_ARIA_ATTR: false,
    FORBID_TAGS: [
      'script',
      'iframe',
      'object',
      'embed',
      'form',
      'svg',
      'math',
      'base',
      'meta',
      'link',
      'style',
      'img',
      'audio',
      'video',
      'source',
    ],
  });
  return `<!doctype html><html lang="zh-CN"><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'none'; connect-src 'none'; img-src 'none'; style-src 'none'; frame-src 'none'; base-uri 'none'; form-action 'none'"></head><body>${safe}</body></html>`;
});
</script>
<template>
  <div class="safe-body">
    <button v-if="html && text" class="text-button body-toggle" @click="plain = !plain">
      {{ plain ? '查看格式化正文' : '切换纯文本' }}
    </button>
    <iframe
      v-if="html && !plain"
      title="邮件正文（安全阅读）"
      :srcdoc="source"
      sandbox=""
      referrerpolicy="no-referrer"
      class="mail-frame"
    />
    <div v-else class="plain-body">{{ text || '这封邮件没有可显示的正文。' }}</div>
  </div>
</template>

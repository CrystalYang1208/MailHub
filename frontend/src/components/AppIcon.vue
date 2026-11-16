<script setup lang="ts">
withDefaults(defineProps<{ name: string; size?: number }>(), { size: 20 });
const paths: Record<string, string[]> = {
  inbox: ['M4 4h16l2 12v4H2v-4L4 4Z', 'M2 15h6l2 3h4l2-3h6'],
  mail: ['M3 5h18v14H3z', 'm3 6 9 7 9-7'],
  compose: ['m14 4 6 6', 'M4 16 16 4l4 4L8 20H4v-4Z'],
  plus: ['M12 5v14', 'M5 12h14'],
  search: ['M21 21l-5-5', 'M17 10a7 7 0 1 1-14 0 7 7 0 0 1 14 0'],
  sync: ['M20 7v5h-5', 'M4 17v-5h5', 'M6 7a7 7 0 0 1 12-1l2 3', 'M18 17a7 7 0 0 1-12 1l-2-3'],
  draft: ['M14 2H5v20h14V7l-5-5Z', 'M14 2v6h5', 'M8 12h7', 'M8 16h5'],
  clock: ['M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0', 'M12 7v5l3 2'],
  settings: [
    'm9 3-1 3-3 1-2 3 2 2-1 3 3 2 2-1 3 2 3-2 2 1 3-2-1-3 2-2-2-3-3-1-1-3H9Z',
    'M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0',
  ],
  chevron: ['m9 5 7 7-7 7'],
  down: ['m6 9 6 6 6-6'],
  back: ['m15 5-7 7 7 7'],
  close: ['m6 6 12 12', 'M18 6 6 18'],
  reply: ['m9 5-6 6 6 6', 'M3 11h10a7 7 0 0 1 7 7'],
  forward: ['m15 5 6 6-6 6', 'M21 11H11a7 7 0 0 0-7 7'],
  trash: ['M3 6h18', 'M9 6V3h6v3', 'm5 6 1 15h12l1-15', 'M10 10v7', 'M14 10v7'],
  paperclip: ['m21 11-9 9a6 6 0 0 1-8-8L14 2a4 4 0 0 1 6 6L10 18a2 2 0 0 1-3-3l9-9'],
  send: ['m22 2-7 20-4-9L2 9 22 2Z', 'm11 13 6-6'],
  shield: ['m12 2 9 4v6c0 5-9 10-9 10S3 17 3 12V6l9-4Z', 'm8 12 3 3 5-6'],
  lock: ['M6 10h12v11H6z', 'M8 10V6a4 4 0 0 1 8 0v4', 'M12 15v2'],
  check: ['m5 12 4 4L19 6'],
  filter: ['M4 6h16', 'M7 12h10', 'M10 18h4'],
  download: ['M12 3v12', 'm7 10 5 5 5-5', 'M4 16v5h16v-5'],
  file: ['M14 2H5v20h14V7l-5-5Z', 'M14 2v6h5', 'M8 13h7', 'M8 17h5'],
  users: [
    'M16 21v-3a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v3',
    'M12 6a4 4 0 1 1-8 0 4 4 0 0 1 8 0',
    'M17 3a4 4 0 0 1 0 8',
    'M22 21v-3a4 4 0 0 0-3-4',
  ],
  arrow: ['M4 12h16', 'm14 6 6 6-6 6'],
  info: ['M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0', 'M12 11v6', 'M12 7v1'],
  wifi: ['M2 8a16 16 0 0 1 20 0', 'M5 12a11 11 0 0 1 14 0', 'M8 16a6 6 0 0 1 8 0', 'M12 20h.01'],
  logout: ['M9 3H3v18h6', 'M9 12h12', 'm16 7 5 5-5 5'],
};
</script>
<template>
  <svg
    :width="size"
    :height="size"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="1.65"
    stroke-linecap="round"
    stroke-linejoin="round"
    aria-hidden="true"
  >
    <path v-for="(d, i) in paths[name] ?? paths.mail" :key="i" :d="d" />
  </svg>
</template>

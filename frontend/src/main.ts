import { createApp } from 'vue';
import { createRouter, createWebHashHistory } from 'vue-router';
import App from './App.vue';
import WorkspaceView from './views/WorkspaceView.vue';
import './styles.css';

// Remove the one-use code before installing hash routing; never persist it.
const parameters = new URLSearchParams(location.hash.slice(1));
const code = parameters.get('code');
history.replaceState(null, '', location.pathname + location.search);
const router = createRouter({
  history: createWebHashHistory(),
  routes: ['/', '/accounts', '/drafts', '/activity', '/settings'].map((path) => ({
    path,
    component: WorkspaceView,
  })),
});
createApp(App).provide('bootstrapCode', code).use(router).mount('#app');

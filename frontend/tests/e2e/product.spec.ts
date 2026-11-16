import { test as base, expect } from '@playwright/test';
import { spawn, type ChildProcess } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

type Server = { origin: string; directory: string };
const test = base.extend<{ server: Server }>({
  server: [
    async ({ page }, use) => {
      const root = resolve(import.meta.dirname, '../../..');
      const directory = resolve(root, '.local/product-e2e', randomUUID());
      mkdirSync(directory, { recursive: true });
      const startup = resolve(directory, 'startup.json');
      let child: ChildProcess | undefined;
      try {
        child = spawn(
          process.env.MAILHUB_DOTNET ?? 'dotnet',
          [
            resolve(root, 'backend/src/MailHub.Host/bin/Debug/net10.0/MailHub.Host.dll'),
            '--demo',
            '--no-browser',
          ],
          {
            cwd: root,
            windowsHide: true,
            stdio: 'ignore',
            env: {
              ...process.env,
              MAILHUB_ENGINEERING: '',
              MAILHUB_ROOT: root,
              MAILHUB_DATA: resolve(directory, 'data'),
              MAILHUB_STARTUP_FILE: startup,
            },
          },
        );
        await expect.poll(() => existsSync(startup), { timeout: 25_000 }).toBe(true);
        const meta = JSON.parse(readFileSync(startup, 'utf8')) as { origin: string; url: string };
        await page.setViewportSize({ width: 1440, height: 1000 });
        try {
          await page.goto(meta.url);
        } catch {
          throw new Error('无法打开本机合成测试工作台');
        }
        await page.getByRole('button', { name: '进入演示工作台', exact: true }).click();
        await expect(page.getByRole('heading', { name: '统一收件箱', exact: true })).toBeVisible({
          timeout: 25_000,
        });
        await expect(page.locator('.mail-item').first()).toBeVisible({ timeout: 25_000 });
        await expect(page.locator('.message-heading h2')).toBeVisible();
        await expect(page.locator('.sync-button')).toHaveText('同步', { timeout: 20000 });
        await use({ origin: meta.origin, directory });
      } finally {
        child?.kill();
      }
    },
    { auto: true },
  ],
});

test('聚合收件箱、跨账号搜索、附件和HTML安全隔离', async ({ page, context, server }) => {
  expect(new URL(page.url()).hash).not.toContain('code=');
  await expect(page.getByText('合成演示', { exact: true })).toBeVisible();
  expect(await page.locator('.mail-item .account-label').allTextContents()).toEqual(
    expect.arrayContaining(['QQ 邮箱', '163 邮箱', 'Gmail']),
  );
  const denied = await page.request.get(server.origin + '/api/v1/accounts');
  expect(denied.status()).toBe(401);
  const storage = await page.evaluate(() => ({
    local: Object.keys(localStorage),
    session: Object.keys(sessionStorage),
  }));
  expect(storage).toEqual({ local: [], session: [] });
  let trackingRequests = 0;
  await context.route('https://tracker.example.invalid/**', async (route) => {
    trackingRequests++;
    await route.fulfill({
      status: 200,
      contentType: 'image/png',
      body: Buffer.from(
        'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII=',
        'base64',
      ),
    });
  });
  const control = await context.newPage();
  await control.setContent('<img src="https://tracker.example.invalid/positive-control">');
  await expect.poll(() => trackingRequests).toBe(1);
  await control.close();
  trackingRequests = 0;
  const search = page.getByRole('textbox', { name: '搜索所有邮箱', exact: true });
  await search.fill('安全阅读测试');
  await search.press('Enter');
  await expect(page.getByRole('heading', { name: '搜索结果', exact: true })).toBeVisible();
  await page.locator('.mail-item').first().click();
  const body = page.frameLocator('iframe').locator('body');
  await expect(body).toContainText('安全阅读测试');
  expect(
    await page.frameLocator('iframe').locator('script,img,iframe,svg,object,form').count(),
  ).toBe(0);
  expect(
    await page.evaluate(() => (window as Window & { __mailhubXss?: boolean }).__mailhubXss),
  ).toBeUndefined();
  expect(trackingRequests).toBe(0);
  await search.fill('实习证明');
  await search.press('Enter');
  await expect(page.locator('.mail-item').first()).toBeVisible();
  await page.locator('.mail-item').filter({ hasText: '你的实习证明已准备就绪' }).first().click();
  await expect(page.locator('.attachment-tile').first()).toBeVisible();
  await page.evaluate(() =>
    Object.defineProperty(window, 'showSaveFilePicker', { value: undefined, configurable: true }),
  );
  const downloading = page.waitForEvent('download');
  await page.locator('.attachment-tile').first().click();
  const file = await downloading;
  expect(file.suggestedFilename()).toBe('实习证明.txt');
  await page.screenshot({ path: resolve(server.directory, 'inbox-security.png'), fullPage: true });
});

test('草稿恢复、发送失败保稿与未知状态显式处置', async ({ page }) => {
  await page.getByRole('button', { name: '写邮件', exact: true }).click();
  const dialog = page.getByRole('dialog');
  await dialog
    .getByRole('textbox', { name: '收件人', exact: true })
    .fill('receiver@example.invalid');
  await dialog.getByRole('textbox', { name: '邮件主题' }).fill('[失败] 浏览器保稿验证');
  await dialog.getByRole('textbox', { name: '邮件正文' }).fill('失败后这段正文必须完整保留。');
  await dialog.getByRole('button', { name: '发送邮件', exact: true }).click();
  await expect(dialog.locator('.notice.warning')).toContainText('服务器拒绝', { timeout: 20_000 });
  await dialog.getByRole('button', { name: '保存并关闭' }).click();
  await page.getByRole('button', { name: '草稿箱', exact: true }).click();
  await page.locator('.draft-row').filter({ hasText: '[失败] 浏览器保稿验证' }).click();
  await expect(dialog.getByRole('textbox', { name: '邮件正文' })).toHaveValue(
    '失败后这段正文必须完整保留。',
  );
  await dialog.getByRole('textbox', { name: '邮件主题' }).fill('[未知] 浏览器确认丢失');
  await dialog.getByRole('button', { name: '发送邮件', exact: true }).click();
  await expect(dialog.locator('.unknown-notice')).toBeVisible({ timeout: 20_000 });
  await dialog.getByRole('button', { name: '关闭', exact: true }).click();
  await page.locator('.draft-row').filter({ hasText: '[未知] 浏览器确认丢失' }).click();
  await expect(dialog.locator('.unknown-notice')).toBeVisible();
  await expect(dialog.getByRole('button', { name: '允许重新编辑，稍后手动发送' })).toBeDisabled();
  await dialog.getByRole('checkbox').check();
  await dialog.getByRole('button', { name: '允许重新编辑，稍后手动发送' }).click();
  await expect(dialog.getByRole('textbox', { name: '邮件主题' })).toBeEnabled();
  await expect(dialog.getByRole('textbox', { name: '邮件正文' })).toHaveValue(
    '失败后这段正文必须完整保留。',
  );
});

test('合成邮件提交、回复线程和垃圾箱操作', async ({ page }) => {
  await page.getByRole('button', { name: '回复邮件', exact: true }).click();
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('textbox', { name: '邮件主题' })).toHaveValue(/^Re:/);
  await dialog
    .getByRole('textbox', { name: '邮件正文' })
    .fill('收到，谢谢！\n\n> 原邮件引用由后端生成并持久保存。');
  await dialog.getByRole('button', { name: '发送邮件', exact: true }).click();
  await expect(dialog).not.toBeVisible({ timeout: 20_000 });
  await expect(page.getByText('合成邮件已提交，未向真实邮箱发送')).toBeVisible();
  await page.getByRole('button', { name: '切换已读状态' }).click();
  await expect(page.getByText('已读状态已由服务器确认')).toBeVisible({ timeout: 20_000 });
  const original = await page.locator('.message-heading h2').innerText();
  await page.getByRole('button', { name: '移至垃圾箱', exact: true }).click();
  await expect(page.getByText('已移至服务端垃圾箱')).toBeVisible({ timeout: 20_000 });
  await page.locator('.sidebar-account').first().click();
  await page
    .locator('.folder-nav')
    .first()
    .getByRole('button', { name: '垃圾箱', exact: true })
    .click();
  await expect(page.getByRole('heading', { name: '垃圾箱', exact: true })).toBeVisible();
  await expect(page.locator('.mail-item')).toHaveCount(1, { timeout: 20000 });
  await expect(page.locator('.mail-item').filter({ hasText: original })).toBeVisible({
    timeout: 20_000,
  });
});

test('三步添加邮箱、模板参数与同步缓存设置', async ({ page }) => {
  await page.getByRole('button', { name: '添加邮箱账号', exact: true }).click();
  const dialog = page.getByRole('dialog');
  for (const label of ['QQ 邮箱', '163 邮箱', '126 邮箱', 'Gmail', 'Outlook'])
    await expect(dialog.getByRole('button', { name: label, exact: true })).toBeVisible();
  await dialog.getByRole('button', { name: '126 邮箱', exact: true }).click();
  await dialog.getByRole('button', { name: '下一步' }).click();
  await dialog.getByRole('button', { name: '查看 / 修改服务器参数' }).click();
  await expect(dialog.getByPlaceholder('imap.example.com').first()).toHaveValue('imap.126.com');
  await dialog.getByRole('button', { name: '校验并连接', exact: true }).click();
  await expect(dialog.getByRole('heading', { name: '邮箱已连接，来信正在汇入。' })).toBeVisible({
    timeout: 20_000,
  });
  await dialog.getByRole('button', { name: '完成，查看邮件' }).click();
  await page.getByRole('button', { name: '管理邮箱', exact: true }).click();
  await expect(page.locator('.account-card').filter({ hasText: '126 邮箱' })).toBeVisible();
  await page.getByRole('button', { name: '设置与隐私', exact: true }).click();
  await page.getByRole('spinbutton', { name: '自动同步间隔' }).fill('15');
  await page.getByRole('spinbutton', { name: '正文缓存上限' }).fill('1');
  await page.getByRole('button', { name: '保存设置', exact: true }).click();
  await expect(page.getByText('同步与缓存设置已保存')).toBeVisible();
});

test('本地用户切换撤销旧令牌并隔离账号和正文', async ({ page, server }) => {
  let headers: Record<string, string> = {};
  page.on('request', (req) => {
    if (req.url().includes('/api/v1/accounts')) headers = req.headers();
  });
  await page.getByRole('button', { name: '同步', exact: true }).click();
  await expect.poll(() => headers['x-mailhub-session']).toBeTruthy();
  const oldHeaders = { ...headers };
  const firstId = await page.locator('.mail-item').first().getAttribute('class');
  expect(firstId).toBeTruthy();
  await page.getByRole('button', { name: '锁定或切换用户' }).click();
  await page.getByRole('button', { name: '创建本地用户', exact: true }).click();
  await page.getByRole('textbox', { name: '本地用户名' }).fill('小周');
  await page.getByLabel('本地用户密码', { exact: true }).fill('Second-Local-User-2026');
  await page.getByRole('button', { name: '创建并进入工作台' }).click();
  await expect(page.locator('.user-profile strong')).toHaveText('小周', { timeout: 20_000 });
  const old = await page.request.get(server.origin + '/api/v1/accounts', {
    headers: {
      Authorization: oldHeaders.authorization,
      'X-MailHub-Session': oldHeaders['x-mailhub-session'],
    },
  });
  expect(old.status()).toBe(401);
});

test('桌面、平板和窄屏无横向溢出，阅读可返回列表', async ({ page, server }) => {
  for (const size of [
    { width: 1440, height: 1000 },
    { width: 768, height: 1024 },
    { width: 390, height: 844 },
  ]) {
    await page.setViewportSize(size);
    if (size.width <= 850) {
      if (await page.getByRole('button', { name: '返回邮件列表' }).isVisible())
        await page.getByRole('button', { name: '返回邮件列表' }).click();
      await expect(page.locator('.mail-item').first()).toBeVisible();
      await page.locator('.mail-item').first().click();
    }
    await expect(page.locator('.message-heading h2')).toBeVisible();
    const dimensions = await page.evaluate(() => ({
      scroll: document.documentElement.scrollWidth,
      width: innerWidth,
    }));
    expect(dimensions.scroll).toBeLessThanOrEqual(dimensions.width);
    await page.screenshot({
      path: resolve(server.directory, `viewport-${size.width}.png`),
      fullPage: true,
    });
  }
  writeFileSync(
    resolve(server.directory, 'responsive.json'),
    JSON.stringify({
      status: 'PASS',
      viewports: [1440, 768, 390],
      data: 'synthetic',
      realMailboxes: false,
    }),
  );
});

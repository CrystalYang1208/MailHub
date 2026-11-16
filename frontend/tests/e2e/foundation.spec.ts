import { test, expect } from '@playwright/test';
import { spawn, type ChildProcess } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

test('同源页面展示SQLite合成数据且无令牌请求被拒绝', async ({ page, browser }, info) => {
  const root = resolve(import.meta.dirname, '../../..');
  const local = resolve(root, '.local/stage4/e2e', randomUUID());
  mkdirSync(local, { recursive: true });
  const startup = resolve(local, 'startup.json');
  const dotnet = process.env.MAILHUB_DOTNET ?? 'dotnet';
  const dll = resolve(root, 'backend/src/MailHub.Host/bin/Debug/net10.0/MailHub.Host.dll');
  let child: ChildProcess | undefined;
  try {
    child = spawn(dotnet, [dll], {
      cwd: root,
      windowsHide: true,
      stdio: 'ignore',
      env: {
        ...process.env,
        MAILHUB_ROOT: root,
        MAILHUB_ENGINEERING: '1',
        MAILHUB_ENGINEERING_DATA: resolve(local, 'database'),
        MAILHUB_STARTUP_FILE: startup,
      },
    });
    await expect.poll(() => existsSync(startup), { timeout: 20_000 }).toBe(true);
    const meta = JSON.parse(readFileSync(startup, 'utf8')) as { origin: string; url: string };
    expect(meta.origin).toMatch(/^http:\/\/127\.0\.0\.1:\d+$/);
    await page.route('**/*', async (route) => {
      if (
        route
          .request()
          .url()
          .startsWith(meta.origin + '/')
      )
        await route.continue();
      else await route.abort();
    });
    try {
      await page.goto(meta.url);
    } catch {
      throw new Error('无法打开本地工程页面');
    }
    await expect(page.getByRole('heading', { name: '邮汇 MailHub', exact: true })).toBeVisible();
    expect(new URL(page.url()).hash).not.toContain('code=');
    await page.getByRole('button', { name: '检查工程联通' }).click();
    await expect(page.getByRole('status')).toHaveText('工程联通成功');
    await expect(page.getByText('SQLite 合成数据读取成功', { exact: true })).toBeVisible();
    const denied = await page.request.get(meta.origin + '/_engineering/items');
    expect(denied.status()).toBe(401);
    const error = (await denied.json()) as { ok: boolean; error: { code: string } };
    expect(error.ok).toBe(false);
    expect(error.error.code).toBe('BRIDGE_REQUIRED');
    writeFileSync(
      resolve(root, '.local/stage4', `browser-${info.project.name}.json`),
      JSON.stringify(
        {
          utc: new Date().toISOString(),
          browser: info.project.name,
          version: browser.version(),
          status: 'PASS',
          checks: {
            loopback: true,
            frontendLoaded: true,
            bootstrapCodeRemoved: true,
            databaseDataRendered: true,
            noTokenRejected: true,
            errorEnvelope: true,
          },
          scope:
            'Real installed browser, new stage-four engineering app, synthetic SQLite data; no mailbox',
        },
        null,
        2,
      ),
    );
  } finally {
    child?.kill();
  }
});

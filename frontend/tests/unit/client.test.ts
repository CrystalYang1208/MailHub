import { describe, expect, it, vi } from 'vitest';
import { ApiError, createApiClient } from '../../src/api/client';

describe('API authorization and error boundaries', () => {
  it('does not send a protected request without an application session', async () => {
    const fetcher = vi.fn();
    const request = createApiClient({
      bridgeToken: () => 'synthetic',
      sessionToken: () => undefined,
      fetcher,
    });
    await expect(request('/api/v1/messages')).rejects.toMatchObject({ code: 'SESSION_REQUIRED' });
    expect(fetcher).not.toHaveBeenCalled();
  });
  it('sends both credentials only as headers and unwraps the response', async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValue(
        new Response(
          JSON.stringify({ ok: true, data: { value: 1 }, error: null, requestId: 'synthetic-id' }),
        ),
      );
    const request = createApiClient({
      bridgeToken: () => 'bridge-fixture',
      sessionToken: () => 'session-fixture',
      fetcher,
    });
    expect(await request('/api/v1/messages')).toEqual({ value: 1 });
    const headers = fetcher.mock.calls[0]![1].headers as Headers;
    expect(headers.get('Authorization')).toBe('Bearer bridge-fixture');
    expect(headers.get('X-MailHub-Session')).toBe('session-fixture');
    expect(fetcher.mock.calls[0]![0]).toBe('/api/v1/messages');
  });
  it('preserves the server error code and actionable Chinese message', async () => {
    const fetcher = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          ok: false,
          data: null,
          error: { code: 'NOT_FOUND', message: '资源不存在', action: '请刷新列表' },
          requestId: 'test',
        }),
        { status: 404 },
      ),
    );
    const request = createApiClient({ bridgeToken: () => 'x', sessionToken: () => 'y', fetcher });
    await expect(request('/api/v1/messages')).rejects.toEqual(
      new ApiError('NOT_FOUND', '资源不存在', '请刷新列表', 'test'),
    );
  });
});

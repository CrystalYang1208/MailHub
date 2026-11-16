export interface Envelope<T> {
  ok: boolean;
  data: T;
  error: { code: string; message: string; action: string } | null;
  requestId: string;
}

export class ApiError extends Error {
  constructor(
    public readonly code: string,
    message: string,
    public readonly action: string,
    public readonly requestId?: string,
  ) {
    super(message);
  }
}

export function createApiClient(options: {
  bridgeToken: () => string | undefined;
  sessionToken: () => string | undefined;
  fetcher?: typeof fetch;
}) {
  return async function request<T>(
    path: string,
    init: RequestInit = {},
    auth: 'session' | 'bridge' | 'none' = 'session',
  ): Promise<T> {
    if (!path.startsWith('/') || path.startsWith('//'))
      throw new ApiError('INVALID_PATH', '接口路径不正确', '请重新打开工作台');
    const headers = new Headers(init.headers);
    if (auth !== 'none') {
      const bridge = options.bridgeToken();
      if (!bridge)
        throw new ApiError('BRIDGE_REQUIRED', '本地访问凭证已失效', '请从启动脚本重新打开工作台');
      headers.set('Authorization', `Bearer ${bridge}`);
      if (auth === 'session') {
        const session = options.sessionToken();
        if (!session) throw new ApiError('SESSION_REQUIRED', '尚未登录应用用户', '请登录后再操作');
        headers.set('X-MailHub-Session', session);
      }
    }
    let response: Response;
    try {
      response = await (options.fetcher ?? fetch)(path, { ...init, headers, credentials: 'omit' });
    } catch {
      throw new ApiError('NETWORK_ERROR', '无法连接本地程序', '请检查本地程序是否仍在运行');
    }
    const envelope = (await response.json()) as Envelope<T>;
    if (!response.ok || !envelope.ok) {
      throw new ApiError(
        envelope.error?.code ?? 'API_ERROR',
        envelope.error?.message ?? '请求失败',
        envelope.error?.action ?? '请稍后重试',
        envelope.requestId,
      );
    }
    return envelope.data;
  };
}

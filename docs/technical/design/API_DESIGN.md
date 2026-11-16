# 本地API详细设计

所有路径相对于`/api/v1`。openapi.yaml为机器契约；下表从同一operation注册表生成，避免operationId/需求映射漂移。接口未实现。

## 统一协议

成功：`{ok:true,data:...,error:null,requestId:uuid}`；失败：`{ok:false,data:null,error:{code,message,action,...},requestId:uuid}`。message和action为中文。仅附件内容为带Content-Disposition的二进制例外；错误仍JSON。

权限U=Bridge Bearer与X-MailHub-Session均有效；B=仅Bridge；P=一次性引导码。所有接口仍受Host/Origin约束。无效会话401，跨用户对象404，不通过差异错误泄露他人资源。

游标limit默认50、范围1～200，稳定倒序；HMAC绑定用户/条件/位置/过期。搜索排序固定收信时间倒序，不提供CR-07已取消高亮或会话聚合。若期间有新数据，前端可刷新首屏，cursor不是跨时间不可变快照。

| operationId | 方法 路径 | 请求/参数 | 成功data / HTTP | 权限 | 分页 | 需求/优先级 | 专项错误 |
|---|---|---|---|---|---|---|---|
| bootstrapBridge | POST `/bridge/bootstrap` | BridgeBootstrap | BridgeAccess / 200 | P | 否 | FR-F01 / Must | BOOTSTRAP_EXPIRED, BOOTSTRAP_REPLAY |
| listUsers | GET `/users` | 无body | 结构见YAML / 200 | B | 否 | FR-F01 / Must | 通用错误 |
| registerUser | POST `/users` | RegisterRequest | User / 201 | B | 否 | FR-F01 / Must | USER_LIMIT, NAME_EXISTS |
| login | POST `/sessions` | LoginRequest | Session / 200 | B | 否 | FR-F01 / Must | LOGIN_INVALID, LOGIN_COOLDOWN, KEY_UNAVAILABLE |
| getCurrentSession | GET `/sessions/current` | 无body | User / 200 | U | 否 | FR-F01 / Must | 通用错误 |
| logout | DELETE `/sessions/current` | 无body | Ack / 200 | U | 否 | FR-F01 / Must | 通用错误 |
| switchUser | POST `/sessions/switch` | LoginRequest | Session / 200 | U | 否 | FR-F01 / Must | LOGIN_INVALID, LOGIN_COOLDOWN |
| changePassword | PUT `/users/current/password` | PasswordChange | Ack / 200 | U | 否 | FR-F01 / Must | PASSWORD_INVALID, KEY_UNAVAILABLE |
| deleteUser | DELETE `/users/current` | LoginRequest | Operation / 202 | U | 否 | FR-F01 / Must | LOGIN_INVALID |
| listProviders | GET `/providers` | 无body | 结构见YAML / 200 | U | 否 | FR-A01, FR-A02, FR-A03, FR-A04 / Must | 通用错误 |
| importOAuthClient | PUT `/settings/oauth-client` | OAuthClientImport | Ack / 200 | U | 否 | FR-A03 / Must | OAUTH_CLIENT_INVALID |
| startOAuth | POST `/oauth/flows` | OAuthStart | OAuthFlow / 201 | U | 否 | FR-A03, FR-A07 / Must | OAUTH_CLIENT_MISSING |
| getOAuthFlow | GET `/oauth/flows/{flowId}` | 无body；flowId | OAuthFlow / 200 | U | 否 | FR-A03, FR-A07 / Must | OAUTH_FLOW_EXPIRED |
| validateAccount | POST `/accounts/validations` | AccountValidationRequest | AccountValidation / 200 | U | 否 | FR-A01, FR-A02, FR-A03, FR-A04, FR-A05, FR-A06 / Must | AUTH_FAILED, IMAP_DISABLED, SERVER_CONFIG, NETWORK_UNREACHABLE, AUTH_COOLDOWN, TLS_FAILED, PROVIDER_REJECTED |
| addAccount | POST `/accounts` | AccountCreate | Account / 201 | U | 否 | FR-A01, FR-A06 / Must | ACCOUNT_EXISTS, VALIDATION_EXPIRED |
| listAccounts | GET `/accounts` | 无body | 结构见YAML / 200 | U | 否 | FR-A02, FR-C02 / Must | 通用错误 |
| getAccount | GET `/accounts/{accountId}` | 无body；accountId | Account / 200 | U | 否 | FR-A06 / Must | 通用错误 |
| setAccountEnabled | PATCH `/accounts/{accountId}` | 结构见YAML；accountId | Account / 200 | U | 否 | FR-A09 / Should | ACCOUNT_BUSY |
| reauthorizeAccount | POST `/accounts/{accountId}/reauthorize` | CredentialInput；accountId | Account / 200 | U | 否 | FR-A07 / Must | AUTH_FAILED, AUTH_COOLDOWN |
| removeAccount | DELETE `/accounts/{accountId}` | 无body；accountId | Operation / 202 | U | 否 | FR-A08 / Must | 通用错误 |
| listFolders | GET `/accounts/{accountId}/folders` | 无body；accountId | 结构见YAML / 200 | U | 否 | FR-C07, FR-C08 / Should | 通用错误 |
| listMessages | GET `/messages` | 无body；accountId, folderId, from, to, isRead, hasAttachments, limit, cursor | 结构见YAML / 200 | U | 是 | FR-C01, FR-C02, FR-C07 / Must | 通用错误 |
| getMessage | GET `/messages/{messageId}` | 无body；messageId | MessageDetail / 200 | U | 否 | FR-C03 / Must | 通用错误 |
| getMessageBody | GET `/messages/{messageId}/body` | 无body；messageId | Body / 200 | U | 否 | FR-C03 / Must | STALE_GENERATION |
| setMessageRead | PATCH `/messages/{messageId}/read` | ReadMutation；messageId | Job / 202 | U | 否 | FR-C05 / Must | AUTH_REQUIRED, ACCOUNT_DISABLED |
| moveMessageToTrash | POST `/messages/{messageId}/trash` | 无body；messageId | Job / 202 | U | 否 | FR-C06 / Must | TRASH_UNSUPPORTED, ALREADY_IN_TRASH, STALE_GENERATION |
| createReply | POST `/messages/{messageId}/reply` | DeriveDraft；messageId | Draft / 201 | U | 否 | FR-D02 / Must | 通用错误 |
| createForward | POST `/messages/{messageId}/forward` | DeriveDraft；messageId | Draft / 201 | U | 否 | FR-D02 / Must | 通用错误 |
| createDraft | POST `/drafts` | DraftCreate | Draft / 201 | U | 否 | FR-D01, FR-D03, FR-D04 / Must | 通用错误 |
| listDrafts | GET `/drafts` | 无body；limit, cursor | 结构见YAML / 200 | U | 是 | FR-D03, FR-D04 / Must | 通用错误 |
| getDraft | GET `/drafts/{draftId}` | 无body；draftId | Draft / 200 | U | 否 | FR-D03, FR-D04 / Must | 通用错误 |
| saveDraft | PUT `/drafts/{draftId}` | DraftUpdate；draftId, If-Match | Draft / 200 | U | 否 | FR-D03, FR-D04 / Must | VERSION_CONFLICT, DRAFT_BUSY |
| deleteDraft | DELETE `/drafts/{draftId}` | 无body；draftId, If-Match | Ack / 200 | U | 否 | FR-D03 / Should | DRAFT_BUSY, VERSION_CONFLICT |
| uploadDraftAttachment | POST `/drafts/{draftId}/attachments` | 结构见YAML；draftId, If-Match | DraftFile / 201 | U | 否 | FR-D05 / Should | ATTACHMENT_TOO_LARGE, VERSION_CONFLICT, DRAFT_BUSY |
| removeDraftAttachment | DELETE `/drafts/{draftId}/attachments/{fileId}` | 无body；draftId, fileId, If-Match | Ack / 200 | U | 否 | FR-D05 / Should | DRAFT_BUSY, VERSION_CONFLICT |
| sendDraft | POST `/drafts/{draftId}/send` | SendRequest；draftId, Idempotency-Key | Submission / 202 | U | 否 | FR-D01, FR-D02, FR-D04, FR-D05 / Must | VERSION_CONFLICT, IDEMPOTENCY_CONFLICT, DRAFT_BUSY, ATTACHMENT_TOO_LARGE, AUTH_REQUIRED |
| getSubmission | GET `/submissions/{submissionId}` | 无body；submissionId | Submission / 200 | U | 否 | FR-D04 / Must | 通用错误 |
| resolveSubmission | POST `/submissions/{submissionId}/resolve` | SubmissionResolution；submissionId | Submission / 200 | U | 否 | FR-D04 / Must | SUBMISSION_NOT_UNKNOWN, DUPLICATE_RISK_NOT_CONFIRMED |
| createDownloadBatch | POST `/downloads` | DownloadRequest | DownloadBatch / 202 | U | 否 | FR-C04 / Should | DOWNLOAD_LIMIT, ATTACHMENT_TOO_LARGE |
| getDownloadBatch | GET `/downloads/{batchId}` | 无body；batchId | DownloadBatch / 200 | U | 否 | FR-C04 / Should | 通用错误 |
| getAttachmentContent | GET `/attachments/{attachmentId}/content` | 无body；attachmentId | binary / 200 | U | 否 | FR-C04 / Should | ATTACHMENT_NOT_READY |
| requestSync | POST `/sync` | SyncRequest | SyncAccepted / 202 | U | 否 | FR-B01, FR-B02, FR-B03, FR-B04, FR-B05, FR-B06 / Must | AUTH_REQUIRED, AUTH_COOLDOWN, ACCOUNT_DISABLED |
| listSyncJobs | GET `/sync/jobs` | 无body；accountId, state, limit, cursor | 结构见YAML / 200 | U | 是 | FR-B03, FR-B05, FR-B07, FR-B08 / Must | 通用错误 |
| getSyncJob | GET `/sync/jobs/{jobId}` | 无body；jobId | Job / 200 | U | 否 | FR-B05, FR-B07 / Must | 通用错误 |
| retrySyncJob | POST `/sync/jobs/{jobId}/retry` | 无body；jobId | Job / 202 | U | 否 | FR-B03, FR-B05 / Must | AUTH_COOLDOWN, AUTH_REQUIRED, JOB_NOT_RETRYABLE |
| searchMessages | GET `/search` | 无body；q, accountId, folderId, from, to, isRead, hasAttachments, limit, cursor | 结构见YAML / 200 | U | 是 | FR-E01, FR-E02 / Must | QUERY_INVALID, QUERY_TOO_EXPENSIVE, CURSOR_INVALID |
| getSettings | GET `/settings` | 无body | Settings / 200 | U | 否 | FR-B04, FR-F02, FR-F03 / Must | 通用错误 |
| updateSettings | PATCH `/settings` | SettingsUpdate | Settings / 200 | U | 否 | FR-B04, FR-F02 / Must | CACHE_QUOTA, STORAGE_FULL |
| clearBodyCache | DELETE `/settings/body-cache` | 无body | Ack / 200 | U | 否 | FR-F02 / Must | 通用错误 |
| clearMailData | POST `/settings/mail-data/clear` | 结构见YAML | Operation / 202 | U | 否 | FR-F03 / Should | 通用错误 |
| getOperation | GET `/operations/{operationId}` | 无body；operationId | Operation / 200 | U | 否 | FR-A08, FR-F01, FR-F03 / Must | 通用错误 |

## 异常与交互约定

所有请求有超时/取消；POST同步、发送、清理返回202只代表已持久入队。任务状态在GET接口查询；fetch事件流是可选提示，不作为交付可靠性的唯一通道。账号验证可耗时，前端显示步骤内进度；失败不创建半账号。API错误不透传服务端原文。

`POST /drafts/{draftId}/send`要求Idempotency-Key+draftVersion，同键同请求返回原提交，同键不同内容409；submitting/unknown不得建立第二活跃提交。SMTP accepted不等于收件端收到。草稿更新/附件上传返回新版本；版本冲突重新读取。

所有GET无服务器状态修改；正文GET仅能入队幂等PEEK读取，不能隐式标已读。服务端FLAGS确认前UI显示pending状态，isRead仍是服务器缓存值。垃圾箱操作不得调用永久EXPUNGE。

附件上传/下载限制既检查声明长度，也按实际流式字节计数，未知大小不能绕过上限。浏览器以带令牌fetch接收单附件Blob并触发另存为；不使用携长期令牌的下载URL。批量顺序处理，释放前一个Blob，累计不超200MB。原始绝对路径不会进入API。

OAuth系统浏览器回调使用独立短生命127.0.0.1动态监听器，state+PKCE验证；不使用桥接token、不要求Origin（Google导航无自定义头），不返回邮件令牌给页面。该协议回调不是本OpenAPI的认证业务端点；流程状态由getOAuthFlow读取。

注册、登录、清理的JSON body及token不得记录；身份注册受最多4槽及限速约束。switch先失效旧会话，再认证新用户；失败后保持锁定，UI不能继续使用A会话。changePassword完成后全部会话撤销，返回Ack后重新登录。

请求大小、队列饱和、磁盘不足、取消及服务商能力不足的中文提示/重试规则见ERROR_HANDLING_DESIGN。每个接口继承通用错误与归属检查；专用错误见x-error-codes。Should接口设计不表示首迭代已经实现或提升为Must。

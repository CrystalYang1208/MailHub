# SQL 与 API 字段映射

OpenAPI用camelCase，SQL用snake_case；UUID字符串不传数据库路径，毫秒转换为UTC RFC3339 Z，JSON列解析为强类型数组。HTTP DTO不等同直接序列化数据库实体。此表含派生/敏感例外，防止开发按名字猜测。

可选但非nullable字段在SQL为NULL时省略；显式nullable字段按契约返回null，不能把accountId/jobId等非nullable UUID序列化成null。

| DTO字段 | SQL或来源 | 转换与写入权限 |
|---|---|---|
| User.userId/displayName/state | catalog.LocalUser.user_id/display_name/state | 登录前列表只含这些公开身份选择值 |
| Session.sessionToken/expiresAt/authVersion | 内存会话注册表；LocalUser.auth_version | 仅登录响应；不持久token原文、不进日志 |
| Account.accountId/address/providerKey/authType/enabled/status | MailAccount对应列 | enabled整数↔bool；启停PATCH只接enabled，不支持已取消显示名编辑 |
| Account.imap.host/port/security | imap_host/imap_port/imap_security | 嵌套ServerConfig，TLS模式不得降None |
| Account.smtp.host/port/security | smtp_host/smtp_port/smtp_security | 同上 |
| Account.lastErrorCode/cooldownUntil | last_error_code/auth_cooldown_until_ms | nullable；冷却UTC |
| CredentialInput.secret/oauthFlowId | 临时验证上下文/内存OAuth flow | 只写，不存明文列，不直接当AuthCredential DTO |
| Folder.folderId/accountId/name/role/state/unreadCount | Folder对应列，其中name=remote_name | 展示名可本地decode，不篡改远端标识 |
| MessageSummary.messageId/accountId/folderId | Message.message_id/account_id/folder_id | 不暴露message_row_id作为业务身份 |
| subject/from/receivedAt/sentAt/isRead/hasAttachments | subject/sender_json/internal_date_ms/sent_at_ms/is_read/has_attachments | 时间UTC；from为Address数组，isRead是服务端确认值 |
| bodyCached/bodyIndexState/version | EXISTS(MessageBody)/body_index_state/version | 缓存命中派生，不能前端回写 |
| MessageDetail.to/cc/replyTo | recipients_json中的to/cc；reply_to_json | 地址列表，未知Bcc不从别处猜 |
| rfcMessageId/inReplyTo/references | rfc_message_id/in_reply_to/references_json | 服务端观察值；rfcMessageId不作为唯一主键 |
| Body.state/text/html/cached/jobId | MessageBody或BODY.PEEK任务结果 | UTF8 Blob解码为string；raw html依旧不可信；offline-unavailable不伪装空正文 |
| Attachment.attachmentId/fileName/mimeType/declaredBytes/downloaded | Attachment.attachment_id/file_name/mime_type/declared_bytes；FileObject.ready | 路径不出API；下载标记由真实文件ready判定 |
| Draft.draftId/accountId/mode/sourceMessageId | Draft.draft_id/account_id/mode/source_message_id | 来源只接受当前用户Message，快照持久后源删不丢草稿 |
| Draft.to/cc/bcc/subject/bodyText/bodyHtml/state/version/updatedAt/lastErrorCode | Draft对应列，地址为JSON数组 | If-Match限制写入；提交中不可改冻结版本 |
| Draft.attachments | DraftAttachment JOIN FileObject | 只返回fileId/fileName/mimeType/byteSize，不暴露relative_path |
| Submission.submissionId/draftId/state/lastErrorCode/updatedAt | OutboxSubmission对应列 | accepted与unknown不被HTTP错误覆盖 |
| Submission.serverAccepted/recipientDelivery | state==accepted；恒not-observed | 不以SMTP成功宣称接收方收到 |
| Submission.resolution | OutboxSubmission.resolution/resolved_at_ms | 仅unknown本人显式处置；allow-new-submission不改原unknown事实，只释放再提交占用 |
| Job.jobId/accountId/folderId/kind/state/attempt/nextRunAt/progressDone/progressTotal/lastErrorCode | SyncJob对应列 | 不回payload_json中的内部路径/租约/错误原文 |
| Operation.operationId/state/lastErrorCode | 当前用户CatalogOperation或CleanupOperation；错误来自固定映射 | 用户删除导致会话撤销后UI不继续凭已撤销会话查询私有操作 |
| DownloadBatch.batchId/state/receivedBytes/items | DownloadBatch与DownloadItem | 实际字节计数，不把declared_bytes作为最终总量 |
| Settings.syncIntervalMinutes/autoSyncPaused | ProfileSettings对应列 | F03后暂停，手动sync恢复 |
| Settings.cacheLimitBytes/cacheUsedBytes | CacheQuota.limit_bytes/used_bytes | 降限额同事务淘汰，不能先提交新低限额 |
| Settings.indexBytes | SearchDocument三列UTF8字节合计 | 逻辑索引源载荷，不等于FTS物理大小 |
| Settings.databaseBytes/walBytes | 本用户固定数据库及WAL路径文件长度 | 只读测量，WAL变化可能使统计为近似时点，不对外暴露路径 |
| Settings.attachmentBytes | 本用户ready附件及草稿附件FileObject字节合计 | 不计阅读正文；无去重对象不能重复累计 |
| Settings.hasGoogleOAuthClient | ProfileSettings.google_oauth_client_dpapi IS NOT NULL | 不回显client_secret/JSON |
| Coverage.* | 当前用户Message及body_index_state按过滤范围统计 | 同一读快照，complete=(pending==0且unavailable==0)；不掩盖失败索引 |
| cursor | HMAC签名的排序位置/查询摘要/用户会话epoch | 不是任意SQL或绝对路径；重启/索引代次变化可失效 |

表中所有存储字段将在design-review检查器中与实际SQL元数据核对。机器schemas不直接包含ownerId、credentialCipher、主密钥或文件绝对路径；服务器端内部端口依赖在MODULE_DESIGN定义。

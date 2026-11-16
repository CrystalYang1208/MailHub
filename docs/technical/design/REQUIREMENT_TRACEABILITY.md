# 需求追踪矩阵

基线逐行来自baseline-input.json（原Word表8/13/15/17/19，源文件SHA256在该JSON）。原35条优先级保持Must24/Should11；这里只表示设计覆盖，不表示产品测试已通过。API关联operationId来自openapi.yaml；T编号是待执行产品用例。

| 需求 | 原描述 | 优先级 | 模块 | 数据实体 | API operationId | 核心流程 | 测试 / AC |
|---|---|---|---|---|---|---|---|
| FR-A01 | 支持通用 IMAP 接入：手工填写服务器地址、端口、加密方式与认证方式 | Must | Account | MailAccount,AuthCredential,AccountReservation | listProviders, validateAccount, addAccount | FLOW-ACCOUNT | T-FR-A01 / AC-02 |
| FR-A02 | 内置常见邮箱模板（QQ、163、Gmail），选中后自动填充服务器参数 | Must | Account | MailAccount（模板在ProviderRegistry静态配置） | listProviders, validateAccount, listAccounts | FLOW-ACCOUNT | T-FR-A02 / AC-01 |
| FR-A03 | Gmail 模板优先走 OAuth 2.0，兼容应用专用密码 | Must | Account | AuthCredential,ProfileSettings | listProviders, importOAuthClient, startOAuth, getOAuthFlow, validateAccount | FLOW-ACCOUNT | T-FR-A03 / AC-13补充及专用用例 |
| FR-A04 | 添加账号时展示分步授权指引（开启 IMAP、生成授权码、服务器参数核对） | Must | Account | ProviderRegistry指引（无新增业务表） | listProviders, validateAccount | FLOW-ACCOUNT | T-FR-A04 / AC-01 |
| FR-A05 | 校验凭据有效性，失败时区分授权失败、未开启 IMAP、服务器地址或端口错误、网络不可达 | Must | Account | MailAccount认证计数,短期Validation | validateAccount | FLOW-ACCOUNT | T-FR-A05 / AC-02 |
| FR-A06 | 凭据加密存储于本机，界面与日志不出现明文 | Must | Account | AuthCredential,UserKeyEnvelope | validateAccount, addAccount, getAccount | FLOW-ACCOUNT | T-FR-A06 / AC-03 |
| FR-A07 | 支持重新授权（令牌过期或授权码变更） | Must | Account | AuthCredential,MailAccount.work_epoch | startOAuth, getOAuthFlow, reauthorizeAccount | FLOW-ACCOUNT | T-FR-A07 / 专用用例 |
| FR-A08 | 支持移除账号并清除该账号本地数据 | Must | Account | CleanupOperation,CatalogOperation及账号级联实体 | removeAccount, getOperation | FLOW-ACCOUNT | T-FR-A08 / AC-10 |
| FR-A09 | 支持账号启用/停用，停用保留配置但不参与同步 | Should | Account | MailAccount.enabled,SyncJob | setAccountEnabled | FLOW-ACCOUNT | T-FR-A09 / 专用用例 |
| FR-B01 | 首次接入按最近 90 天做增量同步 | Must | Sync | Folder,Message,SyncCheckpoint | requestSync | FLOW-SYNC | T-FR-B01 / AC-01 |
| FR-B02 | 基于 UID 与 UIDVALIDITY 做增量拉取与断点续传 | Must | Sync | Folder,Message,SyncCheckpoint | requestSync | FLOW-SYNC | T-FR-B02 / AC-05 |
| FR-B03 | 支持手动同步（单账号或全部账号） | Must | Sync | SyncJob | requestSync, listSyncJobs, retrySyncJob | FLOW-SYNC | T-FR-B03 / 专用用例 |
| FR-B04 | 支持定时自动同步，默认 10 分钟，可配置 5～60 分钟 | Should | Sync | ProfileSettings,SyncJob | requestSync, getSettings, updateSettings | FLOW-SYNC | T-FR-B04 / 专用用例 |
| FR-B05 | 同步失败进入待重试队列，按指数退避重试 | Must | Sync | SyncJob.attempt/next_run_at_ms | requestSync, listSyncJobs, getSyncJob, retrySyncJob | FLOW-SYNC | T-FR-B05 / AC-05及专用退避 |
| FR-B06 | UIDVALIDITY 变化时重建该文件夹索引并全量重拉 | Must | Sync | Folder.epoch/uid_validity,Message,SearchDocument | requestSync | FLOW-SYNC | T-FR-B06 / AC-05及专用代次重建 |
| FR-B07 | 同步过程展示进度与结果，失败给出原因 | Should | Sync | SyncJob.progress/last_error_code | listSyncJobs, getSyncJob | FLOW-SYNC | T-FR-B07 / 专用用例 |
| FR-B08 | 控制并发，单账号同步并发不超过 3，全局不超过 5 | Should | Sync | 账号/全局运行时租约（不另加业务表） | listSyncJobs | FLOW-SYNC | T-FR-B08 / 专用用例 |
| FR-C01 | 统一收件箱跨账号聚合，按收信时间倒序 | Must | Mail | Message,Folder,MailAccount | listMessages | FLOW-READ | T-FR-C01 / AC-04 |
| FR-C02 | 邮件列表显示来源账号标识 | Must | Mail | Message.account_id,MailAccount | listAccounts, listMessages | FLOW-READ | T-FR-C02 / AC-04 |
| FR-C03 | 阅读邮件正文，支持 HTML 与纯文本，默认不加载外部图片 | Must | Mail | MessageBody,SearchDocument,SyncJob | getMessage, getMessageBody | FLOW-READ | T-FR-C03 / AC-07 |
| FR-C04 | 下载附件到本地指定目录 | Should | Mail | Attachment,FileObject,DownloadBatch/Item | createDownloadBatch, getDownloadBatch, getAttachmentContent | FLOW-READ | T-FR-C04 / 专用用例 |
| FR-C05 | 标记已读/未读并以服务器状态为准 | Must | Mail | Message,MailMutation,SyncJob | setMessageRead | FLOW-READ | T-FR-C05 / AC-08 |
| FR-C06 | 删除邮件仅移至服务端垃圾箱，不做本地永久删除 | Must | Mail | Folder,Message,MailMutation | moveMessageToTrash | FLOW-READ | T-FR-C06 / AC-09 |
| FR-C07 | 按账号与文件夹切换视图 | Should | Mail | Folder,Message | listFolders, listMessages | FLOW-READ | T-FR-C07 / 专用用例 |
| FR-C08 | 展示各文件夹未读数 | Should | Mail | Folder.unread_count | listFolders | FLOW-READ | T-FR-C08 / 专用用例 |
| FR-D01 | 新建邮件，可选择发件账号、填写收件人、主题、正文 | Must | Compose | Draft,OutboxSubmission,OutboxRecipient | createDraft, sendDraft | FLOW-SEND | T-FR-D01 / AC-13补充 |
| FR-D02 | 回复与转发，正确携带引用内容与线程头 | Must | Compose | Message引用头,Draft.source_headers_json,OutboxSubmission | createReply, createForward, sendDraft | FLOW-SEND | T-FR-D02 / AC-13补充 |
| FR-D03 | 草稿本地保存与恢复 | Should | Compose | Draft,DraftAttachment | createDraft, listDrafts, getDraft, saveDraft, deleteDraft | FLOW-SEND | T-FR-D03 / 专用用例 |
| FR-D04 | 发送失败保留草稿并提示原因 | Must | Compose | Draft,OutboxSubmission,FileObject | createDraft, listDrafts, getDraft, saveDraft, sendDraft, getSubmission, resolveSubmission | FLOW-SEND | T-FR-D04 / AC-13补充 |
| FR-D05 | 添加附件，单封不超过 25MB | Should | Compose | DraftAttachment,FileObject | uploadDraftAttachment, removeDraftAttachment, sendDraft | FLOW-SEND | T-FR-D05 / 专用用例 |
| FR-E01 | 本地全文检索（主题、正文、发件人）覆盖全部已同步账号 | Must | Search | Message,SearchDocument,MessageSearch | searchMessages | FLOW-SEARCH | T-FR-E01 / AC-14补充 |
| FR-E02 | 组合筛选（账号、时间范围、有无附件、已读状态） | Should | Search | Message过滤字段,MailAccount | searchMessages | FLOW-SEARCH | T-FR-E02 / 专用用例 |
| FR-F01 | 本地多用户切换与数据隔离 | Must | Settings/Security | LocalUser,UserKeyEnvelope,Profile及每用户全部实体 | bootstrapBridge, listUsers, registerUser, login, getCurrentSession, logout, switchUser, changePassword, deleteUser, getOperation | FLOW-USER | T-FR-F01 / AC-12 |
| FR-F02 | 设置正文缓存上限，默认 500MB，按 LRU 淘汰 | Must | Settings/Security | MessageBody,CacheQuota | getSettings, updateSettings, clearBodyCache | FLOW-USER | T-FR-F02 / 专用配额用例 |
| FR-F03 | 一键清除本地邮件数据 | Should | Settings/Security | CleanupOperation,ProfileSettings及邮件/索引实体 | getSettings, clearMailData, getOperation | FLOW-USER | T-FR-F03 / 专用用例 |

## 非功能需求

| 编号 | 原指标 | 设计位置 | 测试 |
|---|---|---|---|
| NFR-01 | 缓存可用时首屏 ≤2s | TECHNICAL_DESIGN性能预算 | T-NFR-01：秒表或浏览器计时，重复 5 次取中位数（原方法保留） |
| NFR-02 | 1000 封列表滚动无明显卡顿，交互响应 ≤100ms | TECHNICAL_DESIGN性能预算 | T-NFR-02：人工滚动 + 开发者工具帧率观察（原方法保留） |
| NFR-03 | 单账号增量 200 封邮件头 ≤30s（10Mbps） | TECHNICAL_DESIGN性能预算 | T-NFR-03：构造 200 封新邮件后计时（原方法保留） |
| NFR-04 | 本地检索 1000 封 ≤1s | TECHNICAL_DESIGN性能预算 | T-NFR-04：同一关键词重复检索 3 次计时（原方法保留） |
| NFR-05 | 断网可读缓存，恢复网络后自动补拉 | SYNC_ENGINE_DESIGN/ERROR_HANDLING_DESIGN | T-NFR-05：断开网络后浏览、恢复后观察同步（原方法保留） |
| NFR-06 | 凭据以 Windows DPAPI 加密存储，界面与日志无明文 | SECURITY_DESIGN | T-NFR-06：检索日志与界面截图，核对无明文（原方法保留） |
| NFR-07 | 邮件数据仅存本机，不向第三方上传 | SECURITY_DESIGN | T-NFR-07：抓包观察出网目标，仅允许邮件服务器（原方法保留） |
| NFR-08 | 本地服务仅监听 127.0.0.1 且校验随机令牌 | SECURITY_DESIGN | T-NFR-08：netstat 查看监听地址，无令牌请求应被拒绝（原方法保留） |
| NFR-09 | HTML 邮件外部图片默认不加载，脚本禁止执行 | SECURITY_DESIGN | T-NFR-09：打开含追踪像素的邮件，抓包应无外部请求（原方法保留） |
| NFR-10 | 日志脱敏，不含正文、附件名、完整地址 | SECURITY_DESIGN | T-NFR-10：抽查日志文件内容（原方法保留） |
| NFR-11 | 同步中断不产生半条记录，索引与库一致 | SYNC_ENGINE_DESIGN/ERROR_HANDLING_DESIGN | T-NFR-11：同步中强制断网，恢复后校验条数（原方法保留） |
| NFR-12 | 仅 Windows 10/11，Chrome/Edge 最新版均可运行；CR-08有效范围仅Windows11 | SYSTEM_ARCHITECTURE/CR-08 | T-NFR-12：两浏览器各跑一次主流程（原方法保留） |
| NFR-13 | 全中文界面，添加账号 ≤3 步 | MODULE_DESIGN三步向导 | T-NFR-13：计时并记录操作步数（原方法保留） |
| NFR-14 | 4 个本地用户共用时数据互不可见 | SECURITY_DESIGN | T-NFR-14：切换用户后核对账号与邮件列表（原方法保留） |

## 业务规则

| 规则 | 原文 | 设计/约束 | 测试 |
|---|---|---|---|
| BR-01 | 同一邮箱地址在本地只允许存在一个账号实例 | AccountReservation唯一HMAC及补偿 | T-BR-01 |
| BR-02 | 连续 3 次授权失败后冷却 10 分钟 | MailAccount三次失败与10分钟持久冷却 | T-BR-02 |
| BR-03 | 单账号同步并发 ≤3，全局并发 ≤5 | 账号/全局信号量3/5 | T-BR-03 |
| BR-04 | 单封附件 ≤25MB，批量下载累计 ≤200MB | 流式25MB单项/200MB批次计数 | T-BR-04 |
| BR-05 | UIDVALIDITY 变化时重建该文件夹索引并全量重拉 | Folder.epoch及新代全量任务 | T-BR-05 |
| BR-06 | 已读状态以服务器为准，本地仅作展示缓存 | MailMutation确认后更新FLAGS | T-BR-06 |
| BR-07 | 删除仅在服务器侧移至垃圾箱，不做本地永久删除 | 仅服务端垃圾箱，禁止普通EXPUNGE | T-BR-07 |
| BR-08 | 正文缓存默认 500MB，LRU 淘汰，仅淘汰正文不淘汰邮件头 | CacheQuota CHECK及同事务预淘汰 | T-BR-08 |
| BR-09 | 时间统一以 UTC 存储，按系统时区展示 | SQL *_ms UTC/API RFC3339 Z | T-BR-09 |
| BR-10 | 字符编码统一以 UTF-8 存储，兼容 GB2312/GBK 解码 | MimeKit解码及UTF8正文Blob | T-BR-10 |

## 已接受CR与解释

| 编号 | 内容 | 设计影响 |
|---|---|---|
| CR-01 | 增加“本地多用户切换”，新增 FR-F01 并定为 Must；接受 | F 模块、NFR-14、AC-12，工期约 +2 天；不得以新API恢复已取消功能 |
| CR-02 | Gmail 接入改为 OAuth 2.0 优先，应用专用密码作为兼容路径；接受，需预留 Google Cloud 项目配置 | FR-A03、类 AuthCredential、AC-01 变体；不得以新API恢复已取消功能 |
| CR-03 | 会话合并需求由 Should 降为 Could；接受，该项后于 CR-07 取消 | C 模块范围收缩，验收不含该项；不得以新API恢复已取消功能 |
| CR-04 | 附件不再做在线预览，改为下载后由系统程序打开；接受 | FR-C04、NFR-02 的内存预算；不得以新API恢复已取消功能 |
| CR-05 | 移动端原生应用移出本期范围，列入 Won't；接受 | Won't 清单、交付形态说明；不得以新API恢复已取消功能 |
| CR-06 | 接入范围由“QQ/163/Gmail 三类”改为“通用 IMAP + 常见模板”；接受 | FR-A01、A02、A05，AC-01、AC-02，活动图首步；不得以新API恢复已取消功能 |
| CR-07 | 取消 5 条低优先级需求（修改账号显示名与同步范围、会话合并、搜索高亮、开机自启、配置迁移），功能需求由 40 条收敛为 35 条；接受 | 表 6、表 7、表 8、追踪矩阵；Could 级归零，Should 由 12 条降为 11 条；不得以新API恢复已取消功能 |
| CR-08 | 首版仅Windows11；2026-11-15负责人明确接受；旧标识CR-PLATFORM-001为同一项 | 覆盖原NFR-12的Windows10/11表述；原DOCX保留历史，不再测试Windows10 |

D2密码登录、D3正文索引独立保留、D4四应用用户地址不重复、D5Google认证必要端点按DECISION_LOG和DESIGN_INPUTS纳入实现解释。FR-D02最新解释为“回复关联原邮件、转发独立邮件并携引用”，负责人本轮明确接受，不恢复会话合并。

## 核心流程定位

| 标识 | 文档 | 异常合同 |
|---|---|---|
| FLOW-ACCOUNT | SYSTEM_ARCHITECTURE添加账号时序；EMAIL_INTEGRATION适配 | 认证/端口/网络/冷却/补偿 |
| FLOW-SYNC | SYNC_ENGINE_DESIGN状态机/时序/伪代码 | 断网/重启/UID代次/磁盘/会话失效 |
| FLOW-READ | SYSTEM_ARCHITECTURE数据流；EMAIL_INTEGRATION阅读/操作 | 离线/未缓存/格式坏/能力不足/旧UID |
| FLOW-SEND | EMAIL_INTEGRATION状态机与伪代码 | 明确拒绝/unknown/幂等/草稿冲突 |
| FLOW-SEARCH | SEARCH_STORAGE_DESIGN查询与覆盖 | 空词/短词成本/未索引/取消/重建 |
| FLOW-USER | SECURITY_DESIGN登录/封套/切换 | 错误密码/旧会话/改密中断/密钥坏/清理 |

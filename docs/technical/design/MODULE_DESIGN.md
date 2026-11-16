# 模块详细设计

以下为后续工程边界，不表示这些项目/类已经存在。建议阶段四建立Host、Application、Domain、Infrastructure及Web工程，按Account/Sync/Mail/Compose/Search/Settings垂直目录组织；不拆成网络微服务。

| 模块 | 负责/输入输出 | 数据写入权 | 对外端口与依赖 |
|---|---|---|---|
| Account | 模板与三步引导、配置校验、认证失败冷却、重授权、启停/移除 | MailAccount/AuthCredential及目录预留 | AccountService、IProviderRegistry、ICredentialVault、IJobScheduler |
| Sync | 首次/增量/重建/FLAGS/删除对账、正文建索引任务、租约与退避 | Folder/Message/SearchDocument/SyncJob/Checkpoint/Scan | ISyncScheduler、IMailSessionFactory、IUnitOfWork、IClock |
| Mail | 统一列表、来源标识、详情/BODY.PEEK、已读与垃圾箱操作编排 | MessageBody/Attachment、MailMutation、相关任务 | IMailQueries、IBodyReader、IMutationService、IStorage |
| Compose | 新建/回复/转发草稿、附件、冻结提交、SMTP与unknown状态 | Draft/DraftAttachment/OutboxSubmission/Recipient | IComposer、IOutboxSender、ISmtpSession、ICredentialVault |
| Search | 本用户跨账号主题/发件人/正文检索和过滤，覆盖状态 | 无业务直接写，重建交给Sync/Storage任务 | ISearchQueries、IProfileReadConnection |
| Settings | 间隔/缓存/清邮件/用户设置，用户注册登录界面调用Security | ProfileSettings/CacheQuota/CleanupOperation | ISettingsService、IUserLifecycle、IJobControl |
| Security | 密码验证、会话、属主校验、主密钥与凭据封装、启动引导 | Catalog用户与封套；凭据写入通过Account事务 | IUserSessionService、ICredentialVault、IUserContextAccessor |
| Storage | 单写队列、事务、索引触发器、配额、文件发布/清理、迁移 | 仅在业务用例授权下操作，不自做邮件策略 | IProfileStore、IUnitOfWork、IFileStore、IMigrationRunner |

## 共享契约

```text
UserContext { userId: Guid, sessionId: opaque, authVersion: int, unlockEpoch: long }
AccountLease { userId, accountId, workEpoch, keyLease, CancellationToken }
FolderIdentity { accountId, folderId, uidValidity: uint32, folderEpoch: long }
RemoteMessageKey { FolderIdentity, uid: uint32 }
Clock { UtcNow: Instant } // 生产UTC时钟；测试可控，不在生产任意提前任务
ProviderSession.Connect(ValidatedServerConfig, CredentialLease, ct)
UnitOfWork.CommitHeaderBatch(context, expectedEpochs, headers, rangeCheckpoint, jobLease)
CredentialVault.AcquireForConnection(context, accountId) -> IDisposable decrypted secret lease
```

资源ID都是UUID；Message内部整数rowid仅仓储使用。所有异步工作捕获不可变UserContext和epoch；UI切换不是更改全局userId变量。SQL查询调用前必须拿到经会话解析的Profile连接，不接受前端数据库路径/ownerId。

## 模块协作约定

1. Controller只负责输入、认证、调用、返回；不操作MailKit或SQL。协议适配器只返回领域DTO/分类异常；不直接更新数据库。Storage不发网络请求。
2. 任务合并键为`{kind}:{accountId}:{folderId or messageId}`；相同活跃资源不重复入队。一个IMAP Client命令串行；按资源控制多个租约，绝不同时操作一个Client。
3. 跨模块写入通过应用服务定义的事务命令，不自行拆分仓储各自提交。FTS仅由SearchDocument触发器写；任何模块都不能单独更新虚拟表后忘记游标。
4. 取消只停止未来网络/工作，不中途强行中断已开始的短事务；事务提交前检查会话/账号/文件夹epoch，失效即回滚。已提交结果只属于捕获的原用户。
5. 同一任务所有权用CAS/租约验证，晚到的旧worker不能覆盖新worker。接口返回jobId及可查询状态，前端不以HTTP断连判任务失败。

## 前端责任

Vue功能目录与六业务模块一致；Security的桥接/会话token只在内存，MailStore按session epoch清空。列表虚拟化、服务端游标分页；API缓存key含用户会话epoch与账号/文件夹条件，登出撤销下载Blob URL及阅读iframe。禁止把原邮件HTML放进主应用v-html。

公开 DTO、operationId、错误码以openapi.yaml为准；本文件端口是内部C#实现合同。阶段四可据operationId生成TS客户端，生成物不可手工加字段。

## 独立开发与集成顺序

先冻结Security/UserContext、Storage事务与DTO；Account与Sync可基于协议stub开发，Mail与Compose基于应用端口开发，Search只依赖SQL读契约。团队在阶段四各自创建OpenSpec变更：Account、Sync、Mail、Compose、Search、Settings，Security/Storage由共享契约维护人先行。每次PR列FR/operationId/ADR/迁移编号及测试证据；改合同先更新设计与规范，不能靠联调临时改JSON。

代码评审检查：身份来源、事务边界、无网络长事务、未知SMTP结果、无宽泛EXPUNGE、索引与缓存独立、日志白名单。集成先本地身份/数据层，再接入/同步，再阅读/发送/搜索；Should按资源排第二迭代，安全/配额/并发硬约束不能随Should延期取消。

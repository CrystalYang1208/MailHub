# 数据字典

由SQL字段注释及SQLite元数据生成；SQL为约束权威。所有INTEGER时间列以`_ms`结尾，存UTC Unix毫秒；API输出RFC3339 UTC。TEXT均UTF8；BLOB按字段说明编码。NULL表示未知/不适用，不以0代替未知。

## catalog.AccountReservation

所属与生命周期：当前Windows账户安装目录内目录库；预留至账号彻底清理完成；清理前不能释放唯一占用。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| account_id | TEXT | PK | 无 | 对应用户库MailAccount UUID |
| user_id | TEXT | NOT NULL | 无 | 不对未认证请求公开的属主 |
| address_fingerprint | BLOB | NOT NULL | 无 | 规范地址HMAC-SHA256 |
| state | TEXT | NOT NULL | 无 | 跨库补偿状态 |
| created_at_ms | INTEGER | NOT NULL | 无 | 预留UTC |
| expires_at_ms | INTEGER | NULL | 无 | 未完成预留可清理截止；active为空 |

外键：user_id → LocalUser.user_id（DELETE CASCADE）

索引/唯一约束：IX_Reservation_User(user_id,state)；sqlite_autoindex_AccountReservation_2(address_fingerprint) UNIQUE；sqlite_autoindex_AccountReservation_1(account_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## catalog.CatalogOperation

所属与生命周期：当前Windows账户安装目录内目录库；跨库操作开始至完成，终态保留7日后清理。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| operation_id | TEXT | PK | 无 | 注册与跨库清理操作UUID |
| user_id | TEXT | NOT NULL | 无 | 所属用户 |
| account_id | TEXT | NULL | 无 | 可选账号UUID，不设跨库FK |
| kind | TEXT | NOT NULL | 无 | 操作类型 |
| state | TEXT | NOT NULL | 无 | 补偿流程进度 |
| updated_at_ms | INTEGER | NOT NULL | 无 | 重启恢复排序UTC |

外键：user_id → LocalUser.user_id（DELETE CASCADE）

索引/唯一约束：IX_CatalogOperation_State(state,updated_at_ms)；sqlite_autoindex_CatalogOperation_1(operation_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## catalog.CatalogSecret

所属与生命周期：当前Windows账户安装目录内目录库；安装生命周期，丢失时只能从一致备份恢复或清理后重新接入。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| singleton | INTEGER | PK | 无 | 安装内唯一记录 |
| dpapi_hmac_key | BLOB | NOT NULL | 无 | 邮箱唯一性指纹HMAC密钥，CurrentUser保护 |

外键：无；跨库关系由应用服务与补偿操作维护。

索引/唯一约束：主键/FTS内建索引。
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## catalog.LocalUser

所属与生命周期：当前Windows账户安装目录内目录库；账号注册至彻底清理；删除中占用用户槽。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| user_id | TEXT | PK | 无 | 不可变应用用户UUID |
| slot | INTEGER | NOT NULL | 无 | 最多4个用户槽位 |
| display_name | TEXT | NOT NULL | 无 | 中文可读名称 |
| normalized_name | TEXT | NOT NULL | 无 | NFC及大小写归一后的登录名 |
| password_salt | BLOB | NOT NULL | 无 | 验证密码的独立随机盐 |
| password_verifier | BLOB | NOT NULL | 无 | KDF验证值，不是可逆密码 |
| password_kdf | TEXT | NOT NULL | 无 | 格式版本内的KDF算法 |
| password_iterations | INTEGER | NOT NULL | 无 | 验证KDF迭代数 |
| auth_version | INTEGER | NOT NULL | 1 | 会话失效版本 |
| failed_logins | INTEGER | NOT NULL | 0 | 应用登录失败计数 |
| locked_until_ms | INTEGER | NULL | 无 | 登录冷却截止UTC |
| state | TEXT | NOT NULL | 无 | 注册与删除恢复状态 |
| created_at_ms | INTEGER | NOT NULL | 无 | 创建UTC |
| updated_at_ms | INTEGER | NOT NULL | 无 | 最近修改UTC |

外键：无；跨库关系由应用服务与补偿操作维护。

索引/唯一约束：sqlite_autoindex_LocalUser_3(normalized_name) UNIQUE；sqlite_autoindex_LocalUser_2(slot) UNIQUE；sqlite_autoindex_LocalUser_1(user_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## catalog.UserKeyEnvelope

所属与生命周期：当前Windows账户安装目录内目录库；用户生命周期；改密码原子换封套。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| user_id | TEXT | PK | 无 | 所属用户 |
| key_id | TEXT | NOT NULL | 无 | 随机主密钥标识 |
| envelope_version | INTEGER | NOT NULL | 无 | 封套序列化版本 |
| kek_salt | BLOB | NOT NULL | 无 | 与密码验证盐不同的KEK盐 |
| kek_iterations | INTEGER | NOT NULL | 无 | KEK派生迭代数 |
| dpapi_wrapped_envelope | BLOB | NOT NULL | 无 | DPAPI包裹的AES-GCM主密钥封套 |
| revision | INTEGER | NOT NULL | 1 | 密码变更并发CAS版本 |
| updated_at_ms | INTEGER | NOT NULL | 无 | 最近换封套UTC |

外键：user_id → LocalUser.user_id（DELETE CASCADE）

索引/唯一约束：sqlite_autoindex_UserKeyEnvelope_2(key_id) UNIQUE；sqlite_autoindex_UserKeyEnvelope_1(user_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.Attachment

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；随Message；文件对象另走清理协议。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| attachment_id | TEXT | PK | 无 | API附件UUID |
| message_row_id | INTEGER | NOT NULL | 无 | 所属邮件 |
| part_spec | TEXT | NOT NULL | 无 | IMAP MIME part标识 |
| file_name | TEXT | NOT NULL | 无 | 仅用于展示或下载名，不用于缓存路径 |
| mime_type | TEXT | NOT NULL | 无 | 原始内容类型；下载固定attachment |
| declared_bytes | INTEGER | NULL | 无 | 服务器声明值，可能不可信 |
| file_id | TEXT | NULL | 无 | 可清理下载缓存引用 |

外键：file_id → FileObject.file_id（DELETE SET NULL）；message_row_id → Message.message_row_id（DELETE CASCADE）

索引/唯一约束：sqlite_autoindex_Attachment_2(message_row_id,part_spec) UNIQUE；sqlite_autoindex_Attachment_1(attachment_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.AuthCredential

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；接入至移除；重授权/刷新CAS替换。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| credential_id | TEXT | PK | 无 | 凭据引用UUID |
| account_id | TEXT | NOT NULL | 无 | 一账号一当前凭据 |
| key_id | TEXT | NOT NULL | 无 | catalog封套中的主密钥标识 |
| format_version | INTEGER | NOT NULL | 1 | 密文格式版本 |
| dpapi_cipher | BLOB | NOT NULL | 无 | DPAPI保护的AES-GCM凭据载荷 |
| expires_at_ms | INTEGER | NULL | 无 | OAuth access到期UTC，授权码为空 |
| revision | INTEGER | NOT NULL | 1 | 刷新及重授权CAS版本 |
| updated_at_ms | INTEGER | NOT NULL | 无 | 更新UTC |

外键：account_id → MailAccount.account_id（DELETE CASCADE）

索引/唯一约束：sqlite_autoindex_AuthCredential_2(account_id) UNIQUE；sqlite_autoindex_AuthCredential_1(credential_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.CacheQuota

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；用户生命周期；F03保留限额并归零用量。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| singleton | INTEGER | PK | 无 | 本用户正文配额 |
| limit_bytes | INTEGER | NOT NULL | 500000000 | 阅读正文逻辑字节上限 |
| used_bytes | INTEGER | NOT NULL | 0 | 同事务维护的正文载荷总量 |

外键：无；跨库关系由应用服务与补偿操作维护。

索引/唯一约束：主键/FTS内建索引。
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.CleanupOperation

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；删除实际文件及数据库收敛后done；终态7日清理。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| operation_id | TEXT | PK | 无 | 用户库清理UUID |
| kind | TEXT | NOT NULL | 无 | 清理范围 |
| target_id | TEXT | NULL | 无 | 目标UUID快照，无FK以在目标删除后继续清文件 |
| payload_json | TEXT | NOT NULL | 无 | 生成相对路径/对象ID清单，不能含任意路径 |
| state | TEXT | NOT NULL | 无 | 重启可继续状态 |
| updated_at_ms | INTEGER | NOT NULL | 无 | 最近推进UTC |

外键：无；跨库关系由应用服务与补偿操作维护。

索引/唯一约束：IX_Cleanup_State(state,updated_at_ms)；sqlite_autoindex_CleanupOperation_1(operation_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.DownloadBatch

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；下载活动及结束后7日；文件缓存独立管理。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| batch_id | TEXT | PK | 无 | API批次UUID |
| limit_bytes | INTEGER | NOT NULL | 200000000 | 批量实际下载硬上限 |
| received_bytes | INTEGER | NOT NULL | 0 | 实际接收累计量 |
| state | TEXT | NOT NULL | 无 | 下载状态 |
| created_at_ms | INTEGER | NOT NULL | 无 | 创建UTC |

外键：无；跨库关系由应用服务与补偿操作维护。

索引/唯一约束：sqlite_autoindex_DownloadBatch_1(batch_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.DownloadItem

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；随批次。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| batch_id | TEXT | PK | 无 | 下载批次 |
| attachment_id | TEXT | PK | 无 | 邮件附件 |
| state | TEXT | NOT NULL | 无 | 单项状态 |
| last_error_code | TEXT | NULL | 无 | 中断或大小限制原因 |

外键：attachment_id → Attachment.attachment_id（DELETE CASCADE）；batch_id → DownloadBatch.batch_id（DELETE CASCADE）

索引/唯一约束：sqlite_autoindex_DownloadItem_1(batch_id,attachment_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.Draft

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；用户显式删除/账号移除；发送后标sent保留，不自动丢稿。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| draft_id | TEXT | PK | 无 | API草稿UUID |
| account_id | TEXT | NOT NULL | 无 | 发件账号；移除账号同时清理 |
| mode | TEXT | NOT NULL | 无 | 撰写模式 |
| source_message_id | TEXT | NULL | 无 | 来源本地Message UUID快照，不设FK避免源删除丢草稿 |
| source_headers_json | TEXT | NOT NULL | '{}' | 回复时已收到原邮件头快照 |
| to_json | TEXT | NOT NULL | 无 | 收件人列表 |
| cc_json | TEXT | NOT NULL | '[]' | 抄送列表 |
| bcc_json | TEXT | NOT NULL | '[]' | 密送仅属主可读，不进日志 |
| subject | TEXT | NOT NULL | 无 | UTF8主题 |
| body_text | TEXT | NOT NULL | 无 | UTF8纯文本正文 |
| body_html | TEXT | NULL | 无 | 可选富文本；首版纯文本编辑亦可 |
| state | TEXT | NOT NULL | 无 | 与提交状态分开 |
| version | INTEGER | NOT NULL | 1 | 编辑并发及冻结版本 |
| last_error_code | TEXT | NULL | 无 | 失败原因代码 |
| created_at_ms | INTEGER | NOT NULL | 无 | 创建UTC |
| updated_at_ms | INTEGER | NOT NULL | 无 | 保存UTC |

外键：account_id → MailAccount.account_id（DELETE CASCADE）

索引/唯一约束：IX_Draft_Updated(updated_at_ms)；sqlite_autoindex_Draft_1(draft_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.DraftAttachment

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；随Draft解绑，文件实际清理由引用计数查询决定。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| draft_id | TEXT | PK | 无 | 草稿 |
| file_id | TEXT | PK | 无 | 已完成持久化的用户选择文件 |
| file_name | TEXT | NOT NULL | 无 | 发送时MIME文件名 |
| mime_type | TEXT | NOT NULL | 无 | 附件内容类型 |

外键：file_id → FileObject.file_id（DELETE NO ACTION）；draft_id → Draft.draft_id（DELETE CASCADE）

索引/唯一约束：sqlite_autoindex_DraftAttachment_1(draft_id,file_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.FileObject

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；临时至原子发布；不再引用后持久清理。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| file_id | TEXT | PK | 无 | 文件对象UUID |
| purpose | TEXT | NOT NULL | 无 | 文件用途 |
| relative_path | TEXT | NOT NULL | 无 | 仅生成的相对路径，不接受邮箱附件名 |
| state | TEXT | NOT NULL | 无 | 与文件重命名协调的状态 |
| byte_size | INTEGER | NOT NULL | 无 | 实际文件字节数 |
| sha256 | BLOB | NULL | 无 | 完整性摘要 |
| created_at_ms | INTEGER | NOT NULL | 无 | 创建UTC |

外键：无；跨库关系由应用服务与补偿操作维护。

索引/唯一约束：sqlite_autoindex_FileObject_2(relative_path) UNIQUE；sqlite_autoindex_FileObject_1(file_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.Folder

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；服务端映射；缺失标记后受控清理。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| folder_id | TEXT | PK | 无 | 本地文件夹UUID |
| account_id | TEXT | NOT NULL | 无 | 所属账号 |
| remote_name | TEXT | NOT NULL | 无 | 服务端原始文件夹名 |
| role | TEXT | NOT NULL | 无 | SPECIAL-USE或适配识别 |
| uid_validity | INTEGER | NULL | 无 | 未打开时为空 |
| last_uid | INTEGER | NOT NULL | 0 | 已完整覆盖的UID区间高水位 |
| epoch | INTEGER | NOT NULL | 1 | 重建守卫代号 |
| highest_modseq | TEXT | NULL | 无 | 无符号64位十进制字符串 |
| state | TEXT | NOT NULL | 无 | 同步及可见状态 |
| unread_count | INTEGER | NOT NULL | 0 | 最近服务端确认未读数 |
| reconciled_at_ms | INTEGER | NULL | 无 | 完整状态对账UTC |

外键：account_id → MailAccount.account_id（DELETE CASCADE）

索引/唯一约束：sqlite_autoindex_Folder_3(folder_id,account_id) UNIQUE；sqlite_autoindex_Folder_2(account_id,remote_name) UNIQUE；sqlite_autoindex_Folder_1(folder_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.FolderScan

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；完整对账至提交后删除；未完成可重启或废弃重扫。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| scan_id | TEXT | PK | 无 | UID集合对账UUID |
| folder_id | TEXT | NOT NULL | 无 | 扫描文件夹 |
| expected_epoch | INTEGER | NOT NULL | 无 | 防旧代扫描生效 |
| upper_uid | INTEGER | NOT NULL | 无 | 本次扫描范围 |
| completed | INTEGER | NOT NULL | 0 | 未完整扫描不能判断删除 |
| created_at_ms | INTEGER | NOT NULL | 无 | 扫描UTC |

外键：folder_id → Folder.folder_id（DELETE CASCADE）

索引/唯一约束：sqlite_autoindex_FolderScan_1(scan_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.MailAccount

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；添加至移除；F03保留。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| account_id | TEXT | PK | 无 | API账号UUID |
| owner_id | TEXT | NOT NULL | 无 | 冗余明确属主 |
| address | TEXT | NOT NULL | 无 | 规范邮箱地址，仅属主可见 |
| provider_key | TEXT | NOT NULL | 无 | 适配器键 |
| auth_type | TEXT | NOT NULL | 无 | 认证类型 |
| imap_host | TEXT | NOT NULL | 无 | IMAP主机 |
| imap_port | INTEGER | NOT NULL | 无 | IMAP端口 |
| imap_security | TEXT | NOT NULL | 无 | 强制安全连接 |
| smtp_host | TEXT | NOT NULL | 无 | SMTP主机 |
| smtp_port | INTEGER | NOT NULL | 无 | SMTP端口 |
| smtp_security | TEXT | NOT NULL | 无 | 隐式TLS或强制STARTTLS |
| enabled | INTEGER | NOT NULL | 1 | 停用保留配置 |
| status | TEXT | NOT NULL | 无 | 对外互斥账号状态 |
| auth_failure_count | INTEGER | NOT NULL | 0 | 连续提供商认证失败数 |
| auth_cooldown_until_ms | INTEGER | NULL | 无 | 三次失败后10分钟截止UTC |
| work_epoch | INTEGER | NOT NULL | 1 | 停用重授权移除使旧任务失效 |
| last_error_code | TEXT | NULL | 无 | 稳定脱敏错误码 |
| created_at_ms | INTEGER | NOT NULL | 无 | 创建UTC |
| updated_at_ms | INTEGER | NOT NULL | 无 | 修改UTC |

外键：owner_id → Profile.user_id（DELETE NO ACTION）

索引/唯一约束：sqlite_autoindex_MailAccount_1(account_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.MailMutation

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；服务器确认或失败后随终态任务清理。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| mutation_id | TEXT | PK | 无 | UI操作UUID |
| job_id | TEXT | NOT NULL | 无 | 调度任务 |
| message_row_id | INTEGER | NULL | 无 | 旧邮件可在服务端移动后消失 |
| source_uid_validity | INTEGER | NOT NULL | 无 | 防UID复用操作错邮件 |
| source_uid | INTEGER | NOT NULL | 无 | 原UID快照 |
| kind | TEXT | NOT NULL | 无 | 服务器操作 |
| desired_read | INTEGER | NULL | 无 | read目标 |
| target_folder_id | TEXT | NULL | 无 | 垃圾箱目标 |
| target_uid | INTEGER | NULL | 无 | COPYUID/MOVE返回的新UID |
| state | TEXT | NOT NULL | 无 | 未确认不覆盖服务器缓存 |

外键：target_folder_id → Folder.folder_id（DELETE SET NULL）；message_row_id → Message.message_row_id（DELETE SET NULL）；job_id → SyncJob.job_id（DELETE CASCADE）

索引/唯一约束：sqlite_autoindex_MailMutation_2(job_id) UNIQUE；sqlite_autoindex_MailMutation_1(mutation_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.Message

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；头部不受LRU；服务器删除、代次重建、账号清理时删除。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| message_row_id | INTEGER | PK | 无 | FTS关联内部整数，不作为API身份 |
| message_id | TEXT | NOT NULL | 无 | API稳定本地UUID |
| account_id | TEXT | NOT NULL | 无 | 用于过滤及跨账号查询 |
| folder_id | TEXT | NOT NULL | 无 | 所属文件夹 |
| uid_validity | INTEGER | NOT NULL | 无 | 此记录服务器代次 |
| uid | INTEGER | NOT NULL | 无 | 文件夹内服务器UID |
| subject | TEXT | NOT NULL | 无 | 解码UTF8主题 |
| sender_json | TEXT | NOT NULL | 无 | 发件人地址显示值列表 |
| reply_to_json | TEXT | NOT NULL | '[]' | 回复地址，空时回退From |
| recipients_json | TEXT | NOT NULL | 无 | To及Cc；不从别处推测Bcc |
| rfc_message_id | TEXT | NULL | 无 | 服务端观察到的Message-ID，不唯一 |
| in_reply_to | TEXT | NULL | 无 | 原始回复引用 |
| references_json | TEXT | NOT NULL | '[]' | 有序References数组 |
| internal_date_ms | INTEGER | NOT NULL | 无 | IMAP INTERNALDATE转UTC，列表排序依据 |
| sent_at_ms | INTEGER | NULL | 无 | Date头转UTC，异常时为空 |
| is_read | INTEGER | NOT NULL | 无 | 服务端确认Seen值 |
| flags_json | TEXT | NOT NULL | 无 | 服务端FLAGS |
| size_bytes | INTEGER | NOT NULL | 无 | RFC822.SIZE |
| has_attachments | INTEGER | NOT NULL | 无 | BODYSTRUCTURE推导 |
| body_index_state | TEXT | NOT NULL | 无 | 全文正文覆盖状态 |
| server_state | TEXT | NOT NULL | 无 | 删除对账确认状态 |
| version | INTEGER | NOT NULL | 1 | DTO ETag版本 |

外键：folder_id → Folder.folder_id（DELETE CASCADE）；account_id → Folder.account_id（DELETE CASCADE）

索引/唯一约束：IX_Message_IndexPending(body_index_state,message_row_id)；IX_Message_RfcId(account_id,rfc_message_id)；IX_Message_FolderRead(folder_id,is_read,internal_date_ms)；IX_Message_Account(account_id,internal_date_ms,message_row_id)；IX_Message_Inbox(internal_date_ms,message_row_id) PARTIAL；sqlite_autoindex_Message_2(folder_id,uid_validity,uid) UNIQUE；sqlite_autoindex_Message_1(message_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.MessageBody

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；正文LRU或主动清缓存，不删头/索引。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| message_row_id | INTEGER | PK | 无 | 阅读缓存所属邮件 |
| text_utf8 | BLOB | NOT NULL | 无 | UTF8纯文本载荷 |
| html_utf8 | BLOB | NOT NULL | 无 | UTF8原HTML缓存，不能直接送入主DOM |
| byte_size | INTEGER | NOT NULL | 无 | 精确逻辑字节数 |
| accessed_at_ms | INTEGER | NOT NULL | 无 | LRU最后实际阅读UTC |

外键：message_row_id → Message.message_row_id（DELETE CASCADE）

索引/唯一约束：IX_Body_LRU(accessed_at_ms,message_row_id)
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.MessageSearch

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；SearchDocument触发器维护；可重建。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| subject |  | NULL | 无 | FTS列，语义与SearchDocument同名列一致 |
| sender |  | NULL | 无 | FTS列，语义与SearchDocument同名列一致 |
| body |  | NULL | 无 | FTS列，语义与SearchDocument同名列一致 |

外键：无；跨库关系由应用服务与补偿操作维护。

索引/唯一约束：主键/FTS内建索引。
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.OutboxRecipient

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；随提交；不把地址写日志。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| submission_id | TEXT | PK | 无 | 所属冻结提交 |
| address | TEXT | PK | 无 | 本地信封地址，含Bcc不写MIME头 |
| kind | TEXT | NOT NULL | 无 | 收件类别 |
| state | TEXT | NOT NULL | 无 | RCPT/最终结果 |
| smtp_status | INTEGER | NULL | 无 | 数字响应 |

外键：submission_id → OutboxSubmission.submission_id（DELETE CASCADE）

索引/唯一约束：sqlite_autoindex_OutboxRecipient_1(submission_id,address) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.OutboxSubmission

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；与Draft关联；unknown禁止自动重发；不早于对账完成清理。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| submission_id | TEXT | PK | 无 | API提交UUID |
| draft_id | TEXT | NOT NULL | 无 | 草稿来源 |
| draft_version | INTEGER | NOT NULL | 无 | 冻结发送版本 |
| idempotency_key | TEXT | NOT NULL | 无 | 用户范围内API去重键 |
| request_hash | BLOB | NOT NULL | 无 | 同键不同请求409 |
| mime_file_id | TEXT | NULL | 无 | 冻结MIME文件，信封在OutboxRecipient保存 |
| client_message_id | TEXT | NOT NULL | 无 | 提交前Message-ID，仅对账线索 |
| observed_message_id | TEXT | NULL | 无 | 从Sent或其他真实副本观察到的ID |
| state | TEXT | NOT NULL | 无 | 提交确认状态 |
| last_error_code | TEXT | NULL | 无 | 稳定发送错误码 |
| smtp_status | INTEGER | NULL | 无 | 可选SMTP数字响应，不含原文 |
| resolution | TEXT | NULL | 无 | unknown人工处置，保留原未知事实 |
| resolved_at_ms | INTEGER | NULL | 无 | 用户处置UTC |
| created_at_ms | INTEGER | NOT NULL | 无 | 提交UTC |
| updated_at_ms | INTEGER | NOT NULL | 无 | 状态更新UTC |

外键：mime_file_id → FileObject.file_id（DELETE NO ACTION）；draft_id → Draft.draft_id（DELETE CASCADE）

索引/唯一约束：IX_Outbox_State(state,updated_at_ms)；UX_Outbox_ActiveDraft(draft_id) UNIQUE PARTIAL；sqlite_autoindex_OutboxSubmission_2(idempotency_key) UNIQUE；sqlite_autoindex_OutboxSubmission_1(submission_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.Profile

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；用户库唯一属主。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| user_id | TEXT | PK | 无 | 对应catalog.LocalUser，无跨库FK |
| format_version | INTEGER | NOT NULL | 1 | 用户库格式版本 |
| created_at_ms | INTEGER | NOT NULL | 无 | 创建UTC |

外键：无；跨库关系由应用服务与补偿操作维护。

索引/唯一约束：sqlite_autoindex_Profile_1(user_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.ProfileSettings

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；用户生命周期；F03保留。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| singleton | INTEGER | PK | 无 | 本库唯一设置 |
| sync_interval_minutes | INTEGER | NOT NULL | 10 | 自动同步周期 |
| auto_sync_paused | INTEGER | NOT NULL | 0 | 清除数据后暂停自动补拉 |
| google_oauth_client_dpapi | BLOB | NULL | 无 | 本用户Google桌面客户端配置密文，导入后不回显 |
| updated_at_ms | INTEGER | NOT NULL | 无 | 修改UTC |

外键：无；跨库关系由应用服务与补偿操作维护。

索引/唯一约束：主键/FTS内建索引。
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.ScanUid

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；随FolderScan删除。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| scan_id | TEXT | PK | 无 | 所属扫描 |
| uid | INTEGER | PK | 无 | 本轮服务端存在UID |

外键：scan_id → FolderScan.scan_id（DELETE CASCADE）

索引/唯一约束：sqlite_autoindex_ScanUid_1(scan_id,uid) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.SearchDocument

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；与Message同生灭，正文索引独立保留。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| message_row_id | INTEGER | PK | 无 | 同一邮件共享索引行号 |
| subject | TEXT | NOT NULL | 无 | 规范化主题 |
| sender | TEXT | NOT NULL | 无 | 规范化发件人文本 |
| body | TEXT | NOT NULL | '' | 提取后的正文，独立于阅读缓存 |
| updated_at_ms | INTEGER | NOT NULL | 无 | 最近索引源更新UTC |

外键：message_row_id → Message.message_row_id（DELETE CASCADE）

索引/唯一约束：主键/FTS内建索引。
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.SyncCheckpoint

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；随任务；完成后清理。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| job_id | TEXT | PK | 无 | 每任务一个断点 |
| expected_uid_validity | INTEGER | NOT NULL | 无 | 固定代次 |
| expected_folder_epoch | INTEGER | NOT NULL | 无 | 重建守卫 |
| lower_uid | INTEGER | NOT NULL | 无 | 固定扫描下界 |
| upper_uid | INTEGER | NOT NULL | 无 | 本轮快照上界 |
| next_uid | INTEGER | NOT NULL | 无 | 下一未覆盖区间起点 |
| cutoff_at_ms | INTEGER | NULL | 无 | 首次90天UTC边界；代次变化全量重建为空 |

外键：job_id → SyncJob.job_id（DELETE CASCADE）

索引/唯一约束：sqlite_autoindex_SyncCheckpoint_1(job_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。

## profile.SyncJob

所属与生命周期：仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH；非终态重启可恢复；终态7日后清理。

| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |
|---|---|---|---|---|
| job_id | TEXT | PK | 无 | API任务UUID |
| account_id | TEXT | NULL | 无 | 可空的用户级维护任务 |
| folder_id | TEXT | NULL | 无 | 文件夹任务归属 |
| message_row_id | INTEGER | NULL | 无 | 正文任务目标 |
| kind | TEXT | NOT NULL | 无 | 工作类型 |
| state | TEXT | NOT NULL | 无 | 持久状态 |
| coalesce_key | TEXT | NOT NULL | 无 | 合并同资源重复触发 |
| payload_json | TEXT | NOT NULL | '{}' | 有版本的非凭据命令载荷 |
| attempt | INTEGER | NOT NULL | 0 | 连续失败次数 |
| next_run_at_ms | INTEGER | NOT NULL | 无 | 退避后UTC到期时间 |
| lease_owner | TEXT | NULL | 无 | 当前宿主租约随机代号 |
| lease_expires_at_ms | INTEGER | NULL | 无 | 崩溃后可回收租约UTC |
| expected_account_epoch | INTEGER | NULL | 无 | 防停用或重授权旧任务回写 |
| progress_done | INTEGER | NOT NULL | 0 | 已提交工作量 |
| progress_total | INTEGER | NULL | 无 | 已知工作量，无则为空 |
| last_error_code | TEXT | NULL | 无 | 脱敏错误分类 |
| version | INTEGER | NOT NULL | 1 | 任务CAS版本 |
| created_at_ms | INTEGER | NOT NULL | 无 | 创建UTC |
| updated_at_ms | INTEGER | NOT NULL | 无 | 状态变更UTC |

外键：message_row_id → Message.message_row_id（DELETE CASCADE）；folder_id → Folder.folder_id（DELETE CASCADE）；account_id → MailAccount.account_id（DELETE CASCADE）

索引/唯一约束：IX_Job_Due(state,next_run_at_ms)；UX_Job_Active(coalesce_key) UNIQUE PARTIAL；sqlite_autoindex_SyncJob_1(job_id) UNIQUE
CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。


-- One physical database per LocalUser. Never attach another user's database for queries.
PRAGMA foreign_keys=ON;
PRAGMA journal_mode=WAL;
PRAGMA synchronous=FULL;
PRAGMA busy_timeout=5000;
PRAGMA wal_autocheckpoint=0;
CREATE TABLE Profile (
 user_id TEXT PRIMARY KEY, -- 对应catalog.LocalUser，无跨库FK
 format_version INTEGER NOT NULL DEFAULT 1, -- 用户库格式版本
 created_at_ms INTEGER NOT NULL -- 创建UTC
) STRICT;
CREATE TRIGGER Profile_One BEFORE INSERT ON Profile WHEN EXISTS(SELECT 1 FROM Profile) BEGIN SELECT RAISE(ABORT,'PROFILE_SINGLE_OWNER'); END;
CREATE TABLE ProfileSettings (
 singleton INTEGER PRIMARY KEY CHECK(singleton=1), -- 本库唯一设置
 sync_interval_minutes INTEGER NOT NULL DEFAULT 10 CHECK(sync_interval_minutes BETWEEN 5 AND 60), -- 自动同步周期
 auto_sync_paused INTEGER NOT NULL DEFAULT 0 CHECK(auto_sync_paused IN (0,1)), -- 清除数据后暂停自动补拉
 google_oauth_client_dpapi BLOB, -- 本用户Google桌面客户端配置密文，导入后不回显
 updated_at_ms INTEGER NOT NULL -- 修改UTC
) STRICT;
CREATE TABLE CacheQuota (
 singleton INTEGER PRIMARY KEY CHECK(singleton=1), -- 本用户正文配额
 limit_bytes INTEGER NOT NULL DEFAULT 500000000 CHECK(limit_bytes>0), -- 阅读正文逻辑字节上限
 used_bytes INTEGER NOT NULL DEFAULT 0 CHECK(used_bytes>=0 AND used_bytes<=limit_bytes) -- 同事务维护的正文载荷总量
) STRICT;
CREATE TABLE MailAccount (
 account_id TEXT PRIMARY KEY, -- API账号UUID
 owner_id TEXT NOT NULL REFERENCES Profile(user_id), -- 冗余明确属主
 address TEXT NOT NULL, -- 规范邮箱地址，仅属主可见
 provider_key TEXT NOT NULL CHECK(provider_key IN ('qq','163','gmail','generic')), -- 适配器键
 auth_type TEXT NOT NULL CHECK(auth_type IN ('oauth2','authorization-code','app-password','password')), -- 认证类型
 imap_host TEXT NOT NULL, -- IMAP主机
 imap_port INTEGER NOT NULL CHECK(imap_port BETWEEN 1 AND 65535), -- IMAP端口
 imap_security TEXT NOT NULL CHECK(imap_security IN ('tls','starttls')), -- 强制安全连接
 smtp_host TEXT NOT NULL, -- SMTP主机
 smtp_port INTEGER NOT NULL CHECK(smtp_port BETWEEN 1 AND 65535), -- SMTP端口
 smtp_security TEXT NOT NULL CHECK(smtp_security IN ('tls','starttls')), -- 隐式TLS或强制STARTTLS
 enabled INTEGER NOT NULL DEFAULT 1 CHECK(enabled IN (0,1)), -- 停用保留配置
 status TEXT NOT NULL CHECK(status IN ('authorized','syncing','retry','auth-required','disabled','removing')), -- 对外互斥账号状态
 auth_failure_count INTEGER NOT NULL DEFAULT 0, -- 连续提供商认证失败数
 auth_cooldown_until_ms INTEGER, -- 三次失败后10分钟截止UTC
 work_epoch INTEGER NOT NULL DEFAULT 1, -- 停用重授权移除使旧任务失效
 last_error_code TEXT, -- 稳定脱敏错误码
 created_at_ms INTEGER NOT NULL, -- 创建UTC
 updated_at_ms INTEGER NOT NULL -- 修改UTC
) STRICT;
CREATE TABLE AuthCredential (
 credential_id TEXT PRIMARY KEY, -- 凭据引用UUID
 account_id TEXT NOT NULL UNIQUE REFERENCES MailAccount(account_id) ON DELETE CASCADE, -- 一账号一当前凭据
 key_id TEXT NOT NULL, -- catalog封套中的主密钥标识
 format_version INTEGER NOT NULL DEFAULT 1, -- 密文格式版本
 dpapi_cipher BLOB NOT NULL, -- DPAPI保护的AES-GCM凭据载荷
 expires_at_ms INTEGER, -- OAuth access到期UTC，授权码为空
 revision INTEGER NOT NULL DEFAULT 1, -- 刷新及重授权CAS版本
 updated_at_ms INTEGER NOT NULL -- 更新UTC
) STRICT;
CREATE TABLE Folder (
 folder_id TEXT PRIMARY KEY, -- 本地文件夹UUID
 account_id TEXT NOT NULL REFERENCES MailAccount(account_id) ON DELETE CASCADE, -- 所属账号
 remote_name TEXT NOT NULL, -- 服务端原始文件夹名
 role TEXT NOT NULL CHECK(role IN ('inbox','sent','trash','drafts','other')), -- SPECIAL-USE或适配识别
 uid_validity INTEGER CHECK(uid_validity BETWEEN 1 AND 4294967295), -- 未打开时为空
 last_uid INTEGER NOT NULL DEFAULT 0 CHECK(last_uid BETWEEN 0 AND 4294967295), -- 已完整覆盖的UID区间高水位
 epoch INTEGER NOT NULL DEFAULT 1, -- 重建守卫代号
 highest_modseq TEXT, -- 无符号64位十进制字符串
 state TEXT NOT NULL CHECK(state IN ('active','rebuilding','missing','removing')), -- 同步及可见状态
 unread_count INTEGER NOT NULL DEFAULT 0 CHECK(unread_count>=0), -- 最近服务端确认未读数
 reconciled_at_ms INTEGER, -- 完整状态对账UTC
 UNIQUE(account_id,remote_name),
 UNIQUE(folder_id,account_id)
) STRICT;
CREATE TABLE Message (
 message_row_id INTEGER PRIMARY KEY AUTOINCREMENT, -- FTS关联内部整数，不作为API身份
 message_id TEXT NOT NULL UNIQUE, -- API稳定本地UUID
 account_id TEXT NOT NULL, -- 用于过滤及跨账号查询
 folder_id TEXT NOT NULL, -- 所属文件夹
 uid_validity INTEGER NOT NULL CHECK(uid_validity BETWEEN 1 AND 4294967295), -- 此记录服务器代次
 uid INTEGER NOT NULL CHECK(uid BETWEEN 1 AND 4294967295), -- 文件夹内服务器UID
 subject TEXT NOT NULL, -- 解码UTF8主题
 sender_json TEXT NOT NULL CHECK(json_valid(sender_json)), -- 发件人地址显示值列表
 reply_to_json TEXT NOT NULL DEFAULT '[]' CHECK(json_valid(reply_to_json)), -- 回复地址，空时回退From
 recipients_json TEXT NOT NULL CHECK(json_valid(recipients_json)), -- To及Cc；不从别处推测Bcc
 rfc_message_id TEXT, -- 服务端观察到的Message-ID，不唯一
 in_reply_to TEXT, -- 原始回复引用
 references_json TEXT NOT NULL DEFAULT '[]' CHECK(json_valid(references_json)), -- 有序References数组
 internal_date_ms INTEGER NOT NULL, -- IMAP INTERNALDATE转UTC，列表排序依据
 sent_at_ms INTEGER, -- Date头转UTC，异常时为空
 is_read INTEGER NOT NULL CHECK(is_read IN (0,1)), -- 服务端确认Seen值
 flags_json TEXT NOT NULL CHECK(json_valid(flags_json)), -- 服务端FLAGS
 size_bytes INTEGER NOT NULL CHECK(size_bytes>=0), -- RFC822.SIZE
 has_attachments INTEGER NOT NULL CHECK(has_attachments IN (0,1)), -- BODYSTRUCTURE推导
 body_index_state TEXT NOT NULL CHECK(body_index_state IN ('pending','indexed','unavailable')), -- 全文正文覆盖状态
 server_state TEXT NOT NULL CHECK(server_state IN ('present','missing')), -- 删除对账确认状态
 version INTEGER NOT NULL DEFAULT 1, -- DTO ETag版本
 FOREIGN KEY(folder_id,account_id) REFERENCES Folder(folder_id,account_id) ON DELETE CASCADE,
 UNIQUE(folder_id,uid_validity,uid)
) STRICT;
CREATE TRIGGER Message_ValidGeneration BEFORE INSERT ON Message WHEN NOT EXISTS(SELECT 1 FROM Folder WHERE folder_id=NEW.folder_id AND uid_validity=NEW.uid_validity) BEGIN SELECT RAISE(ABORT,'STALE_GENERATION'); END;
CREATE TRIGGER Message_ValidGenerationUpdate BEFORE UPDATE OF folder_id,uid_validity ON Message WHEN NOT EXISTS(SELECT 1 FROM Folder WHERE folder_id=NEW.folder_id AND uid_validity=NEW.uid_validity) BEGIN SELECT RAISE(ABORT,'STALE_GENERATION'); END;
CREATE TRIGGER Folder_ChangeValidity BEFORE UPDATE OF uid_validity ON Folder WHEN OLD.uid_validity IS NOT NEW.uid_validity AND EXISTS(SELECT 1 FROM Message WHERE folder_id=OLD.folder_id) BEGIN SELECT RAISE(ABORT,'OLD_GENERATION_NOT_PURGED'); END;
CREATE TABLE SearchDocument (
 message_row_id INTEGER PRIMARY KEY REFERENCES Message(message_row_id) ON DELETE CASCADE, -- 同一邮件共享索引行号
 subject TEXT NOT NULL, -- 规范化主题
 sender TEXT NOT NULL, -- 规范化发件人文本
 body TEXT NOT NULL DEFAULT '', -- 提取后的正文，独立于阅读缓存
 updated_at_ms INTEGER NOT NULL -- 最近索引源更新UTC
) STRICT;
CREATE VIRTUAL TABLE MessageSearch USING fts5(subject,sender,body,content='SearchDocument',content_rowid='message_row_id',tokenize='trigram');
CREATE TRIGGER SearchDocument_Insert AFTER INSERT ON SearchDocument BEGIN INSERT INTO MessageSearch(rowid,subject,sender,body) VALUES(NEW.message_row_id,NEW.subject,NEW.sender,NEW.body); END;
CREATE TRIGGER SearchDocument_Delete AFTER DELETE ON SearchDocument BEGIN INSERT INTO MessageSearch(MessageSearch,rowid,subject,sender,body) VALUES('delete',OLD.message_row_id,OLD.subject,OLD.sender,OLD.body); END;
CREATE TRIGGER SearchDocument_Update AFTER UPDATE ON SearchDocument BEGIN INSERT INTO MessageSearch(MessageSearch,rowid,subject,sender,body) VALUES('delete',OLD.message_row_id,OLD.subject,OLD.sender,OLD.body); INSERT INTO MessageSearch(rowid,subject,sender,body) VALUES(NEW.message_row_id,NEW.subject,NEW.sender,NEW.body); END;
CREATE TABLE MessageBody (
 message_row_id INTEGER PRIMARY KEY REFERENCES Message(message_row_id) ON DELETE CASCADE, -- 阅读缓存所属邮件
 text_utf8 BLOB NOT NULL, -- UTF8纯文本载荷
 html_utf8 BLOB NOT NULL, -- UTF8原HTML缓存，不能直接送入主DOM
 byte_size INTEGER NOT NULL CHECK(byte_size=length(text_utf8)+length(html_utf8)), -- 精确逻辑字节数
 accessed_at_ms INTEGER NOT NULL -- LRU最后实际阅读UTC
) STRICT;
CREATE TRIGGER Body_Insert AFTER INSERT ON MessageBody BEGIN UPDATE CacheQuota SET used_bytes=used_bytes+NEW.byte_size WHERE singleton=1; SELECT CASE WHEN changes()<>1 THEN RAISE(ABORT,'CACHE_QUOTA_MISSING') END; END;
CREATE TRIGGER Body_Delete AFTER DELETE ON MessageBody BEGIN UPDATE CacheQuota SET used_bytes=used_bytes-OLD.byte_size WHERE singleton=1; END;
CREATE TRIGGER Body_Update AFTER UPDATE OF byte_size ON MessageBody BEGIN UPDATE CacheQuota SET used_bytes=used_bytes-OLD.byte_size+NEW.byte_size WHERE singleton=1; END;
CREATE TABLE FileObject (
 file_id TEXT PRIMARY KEY, -- 文件对象UUID
 purpose TEXT NOT NULL CHECK(purpose IN ('attachment-cache','draft-attachment','outbox-mime')), -- 文件用途
 relative_path TEXT NOT NULL UNIQUE, -- 仅生成的相对路径，不接受邮箱附件名
 state TEXT NOT NULL CHECK(state IN ('staging','ready','pending-delete')), -- 与文件重命名协调的状态
 byte_size INTEGER NOT NULL CHECK(byte_size>=0), -- 实际文件字节数
 sha256 BLOB CHECK(sha256 IS NULL OR length(sha256)=32), -- 完整性摘要
 created_at_ms INTEGER NOT NULL -- 创建UTC
) STRICT;
CREATE TABLE Attachment (
 attachment_id TEXT PRIMARY KEY, -- API附件UUID
 message_row_id INTEGER NOT NULL REFERENCES Message(message_row_id) ON DELETE CASCADE, -- 所属邮件
 part_spec TEXT NOT NULL, -- IMAP MIME part标识
 file_name TEXT NOT NULL, -- 仅用于展示或下载名，不用于缓存路径
 mime_type TEXT NOT NULL, -- 原始内容类型；下载固定attachment
 declared_bytes INTEGER CHECK(declared_bytes>=0), -- 服务器声明值，可能不可信
 file_id TEXT REFERENCES FileObject(file_id) ON DELETE SET NULL, -- 可清理下载缓存引用
 UNIQUE(message_row_id,part_spec)
) STRICT;
CREATE TABLE SyncJob (
 job_id TEXT PRIMARY KEY, -- API任务UUID
 account_id TEXT REFERENCES MailAccount(account_id) ON DELETE CASCADE, -- 可空的用户级维护任务
 folder_id TEXT REFERENCES Folder(folder_id) ON DELETE CASCADE, -- 文件夹任务归属
 message_row_id INTEGER REFERENCES Message(message_row_id) ON DELETE CASCADE, -- 正文任务目标
 kind TEXT NOT NULL CHECK(kind IN ('headers','body-index','reconcile','flags','trash','cleanup')), -- 工作类型
 state TEXT NOT NULL CHECK(state IN ('pending','running','retry','paused','auth-required','completed','failed','canceled')), -- 持久状态
 coalesce_key TEXT NOT NULL, -- 合并同资源重复触发
 payload_json TEXT NOT NULL DEFAULT '{}' CHECK(json_valid(payload_json)), -- 有版本的非凭据命令载荷
 attempt INTEGER NOT NULL DEFAULT 0 CHECK(attempt>=0), -- 连续失败次数
 next_run_at_ms INTEGER NOT NULL, -- 退避后UTC到期时间
 lease_owner TEXT, -- 当前宿主租约随机代号
 lease_expires_at_ms INTEGER, -- 崩溃后可回收租约UTC
 expected_account_epoch INTEGER, -- 防停用或重授权旧任务回写
 progress_done INTEGER NOT NULL DEFAULT 0, -- 已提交工作量
 progress_total INTEGER, -- 已知工作量，无则为空
 last_error_code TEXT, -- 脱敏错误分类
 version INTEGER NOT NULL DEFAULT 1, -- 任务CAS版本
 created_at_ms INTEGER NOT NULL, -- 创建UTC
 updated_at_ms INTEGER NOT NULL -- 状态变更UTC
) STRICT;
CREATE UNIQUE INDEX UX_Job_Active ON SyncJob(coalesce_key) WHERE state IN ('pending','running','retry','paused','auth-required');
CREATE INDEX IX_Job_Due ON SyncJob(state,next_run_at_ms);
CREATE TABLE SyncCheckpoint (
 job_id TEXT PRIMARY KEY REFERENCES SyncJob(job_id) ON DELETE CASCADE, -- 每任务一个断点
 expected_uid_validity INTEGER NOT NULL, -- 固定代次
 expected_folder_epoch INTEGER NOT NULL, -- 重建守卫
 lower_uid INTEGER NOT NULL CHECK(lower_uid>=1), -- 固定扫描下界
 upper_uid INTEGER NOT NULL CHECK(upper_uid BETWEEN 0 AND 4294967295), -- 本轮快照上界
 next_uid INTEGER NOT NULL CHECK(next_uid BETWEEN 1 AND 4294967296), -- 下一未覆盖区间起点
 cutoff_at_ms INTEGER -- 首次90天UTC边界；代次变化全量重建为空
) STRICT;
CREATE TABLE FolderScan (
 scan_id TEXT PRIMARY KEY, -- UID集合对账UUID
 folder_id TEXT NOT NULL REFERENCES Folder(folder_id) ON DELETE CASCADE, -- 扫描文件夹
 expected_epoch INTEGER NOT NULL, -- 防旧代扫描生效
 upper_uid INTEGER NOT NULL, -- 本次扫描范围
 completed INTEGER NOT NULL DEFAULT 0 CHECK(completed IN (0,1)), -- 未完整扫描不能判断删除
 created_at_ms INTEGER NOT NULL -- 扫描UTC
) STRICT;
CREATE TABLE ScanUid (
 scan_id TEXT NOT NULL REFERENCES FolderScan(scan_id) ON DELETE CASCADE, -- 所属扫描
 uid INTEGER NOT NULL, -- 本轮服务端存在UID
 PRIMARY KEY(scan_id,uid)
) STRICT;
CREATE TABLE Draft (
 draft_id TEXT PRIMARY KEY, -- API草稿UUID
 account_id TEXT NOT NULL REFERENCES MailAccount(account_id) ON DELETE CASCADE, -- 发件账号；移除账号同时清理
 mode TEXT NOT NULL CHECK(mode IN ('new','reply','forward')), -- 撰写模式
 source_message_id TEXT, -- 来源本地Message UUID快照，不设FK避免源删除丢草稿
 source_headers_json TEXT NOT NULL DEFAULT '{}' CHECK(json_valid(source_headers_json)), -- 回复时已收到原邮件头快照
 to_json TEXT NOT NULL CHECK(json_valid(to_json)), -- 收件人列表
 cc_json TEXT NOT NULL DEFAULT '[]' CHECK(json_valid(cc_json)), -- 抄送列表
 bcc_json TEXT NOT NULL DEFAULT '[]' CHECK(json_valid(bcc_json)), -- 密送仅属主可读，不进日志
 subject TEXT NOT NULL, -- UTF8主题
 body_text TEXT NOT NULL, -- UTF8纯文本正文
 body_html TEXT, -- 可选富文本；首版纯文本编辑亦可
 state TEXT NOT NULL CHECK(state IN ('editing','queued','sending','failed','unknown','sent')), -- 与提交状态分开
 version INTEGER NOT NULL DEFAULT 1, -- 编辑并发及冻结版本
 last_error_code TEXT, -- 失败原因代码
 created_at_ms INTEGER NOT NULL, -- 创建UTC
 updated_at_ms INTEGER NOT NULL -- 保存UTC
) STRICT;
CREATE TABLE DraftAttachment (
 draft_id TEXT NOT NULL REFERENCES Draft(draft_id) ON DELETE CASCADE, -- 草稿
 file_id TEXT NOT NULL REFERENCES FileObject(file_id), -- 已完成持久化的用户选择文件
 file_name TEXT NOT NULL, -- 发送时MIME文件名
 mime_type TEXT NOT NULL, -- 附件内容类型
 PRIMARY KEY(draft_id,file_id)
) STRICT;
CREATE TABLE OutboxSubmission (
 submission_id TEXT PRIMARY KEY, -- API提交UUID
 draft_id TEXT NOT NULL REFERENCES Draft(draft_id) ON DELETE CASCADE, -- 草稿来源
 draft_version INTEGER NOT NULL, -- 冻结发送版本
 idempotency_key TEXT NOT NULL UNIQUE, -- 用户范围内API去重键
 request_hash BLOB NOT NULL CHECK(length(request_hash)=32), -- 同键不同请求409
 mime_file_id TEXT REFERENCES FileObject(file_id), -- 冻结MIME文件，信封在OutboxRecipient保存
 client_message_id TEXT NOT NULL, -- 提交前Message-ID，仅对账线索
 observed_message_id TEXT, -- 从Sent或其他真实副本观察到的ID
 state TEXT NOT NULL CHECK(state IN ('queued','preparing','submitting','accepted','failed','unknown')), -- 提交确认状态
 last_error_code TEXT, -- 稳定发送错误码
 smtp_status INTEGER, -- 可选SMTP数字响应，不含原文
 resolution TEXT CHECK(resolution IN ('confirm-accepted','allow-new-submission')), -- unknown人工处置，保留原未知事实
 resolved_at_ms INTEGER, -- 用户处置UTC
 created_at_ms INTEGER NOT NULL, -- 提交UTC
 updated_at_ms INTEGER NOT NULL -- 状态更新UTC
) STRICT;
CREATE UNIQUE INDEX UX_Outbox_ActiveDraft ON OutboxSubmission(draft_id) WHERE state IN ('queued','preparing','submitting') OR (state='unknown' AND resolution IS NULL);
CREATE TABLE OutboxRecipient (
 submission_id TEXT NOT NULL REFERENCES OutboxSubmission(submission_id) ON DELETE CASCADE, -- 所属冻结提交
 address TEXT NOT NULL, -- 本地信封地址，含Bcc不写MIME头
 kind TEXT NOT NULL CHECK(kind IN ('to','cc','bcc')), -- 收件类别
 state TEXT NOT NULL CHECK(state IN ('pending','accepted','rejected','unknown')), -- RCPT/最终结果
 smtp_status INTEGER, -- 数字响应
 PRIMARY KEY(submission_id,address)
) STRICT;
CREATE TABLE MailMutation (
 mutation_id TEXT PRIMARY KEY, -- UI操作UUID
 job_id TEXT NOT NULL UNIQUE REFERENCES SyncJob(job_id) ON DELETE CASCADE, -- 调度任务
 message_row_id INTEGER REFERENCES Message(message_row_id) ON DELETE SET NULL, -- 旧邮件可在服务端移动后消失
 source_uid_validity INTEGER NOT NULL, -- 防UID复用操作错邮件
 source_uid INTEGER NOT NULL, -- 原UID快照
 kind TEXT NOT NULL CHECK(kind IN ('read','trash')), -- 服务器操作
 desired_read INTEGER CHECK(desired_read IN (0,1)), -- read目标
 target_folder_id TEXT REFERENCES Folder(folder_id) ON DELETE SET NULL, -- 垃圾箱目标
 target_uid INTEGER, -- COPYUID/MOVE返回的新UID
 state TEXT NOT NULL CHECK(state IN ('pending','confirmed','failed')), -- 未确认不覆盖服务器缓存
 CHECK((kind='read' AND desired_read IS NOT NULL) OR kind='trash')
) STRICT;
CREATE TABLE DownloadBatch (
 batch_id TEXT PRIMARY KEY, -- API批次UUID
 limit_bytes INTEGER NOT NULL DEFAULT 200000000 CHECK(limit_bytes=200000000), -- 批量实际下载硬上限
 received_bytes INTEGER NOT NULL DEFAULT 0 CHECK(received_bytes BETWEEN 0 AND limit_bytes), -- 实际接收累计量
 state TEXT NOT NULL CHECK(state IN ('pending','running','completed','failed','canceled')), -- 下载状态
 created_at_ms INTEGER NOT NULL -- 创建UTC
) STRICT;
CREATE TABLE DownloadItem (
 batch_id TEXT NOT NULL REFERENCES DownloadBatch(batch_id) ON DELETE CASCADE, -- 下载批次
 attachment_id TEXT NOT NULL REFERENCES Attachment(attachment_id) ON DELETE CASCADE, -- 邮件附件
 state TEXT NOT NULL CHECK(state IN ('pending','running','completed','failed','canceled')), -- 单项状态
 last_error_code TEXT, -- 中断或大小限制原因
 PRIMARY KEY(batch_id,attachment_id)
) STRICT;
CREATE TABLE CleanupOperation (
 operation_id TEXT PRIMARY KEY, -- 用户库清理UUID
 kind TEXT NOT NULL CHECK(kind IN ('account','mail-data','user-files','orphan-file')), -- 清理范围
 target_id TEXT, -- 目标UUID快照，无FK以在目标删除后继续清文件
 payload_json TEXT NOT NULL CHECK(json_valid(payload_json)), -- 生成相对路径/对象ID清单，不能含任意路径
 state TEXT NOT NULL CHECK(state IN ('pending','running','done','failed')), -- 重启可继续状态
 updated_at_ms INTEGER NOT NULL -- 最近推进UTC
) STRICT;
CREATE INDEX IX_Message_Inbox ON Message(internal_date_ms DESC,message_row_id DESC) WHERE server_state='present';
CREATE INDEX IX_Message_Account ON Message(account_id,internal_date_ms DESC,message_row_id DESC);
CREATE INDEX IX_Message_FolderRead ON Message(folder_id,is_read,internal_date_ms DESC);
CREATE INDEX IX_Message_RfcId ON Message(account_id,rfc_message_id);
CREATE INDEX IX_Message_IndexPending ON Message(body_index_state,message_row_id);
CREATE INDEX IX_Body_LRU ON MessageBody(accessed_at_ms,message_row_id);
CREATE INDEX IX_Draft_Updated ON Draft(updated_at_ms DESC);
CREATE INDEX IX_Outbox_State ON OutboxSubmission(state,updated_at_ms);
CREATE INDEX IX_Cleanup_State ON CleanupOperation(state,updated_at_ms);
PRAGMA user_version=1;

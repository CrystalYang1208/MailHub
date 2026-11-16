-- Design contract v1. UTC values are Unix milliseconds. Not a migration of validation databases.
PRAGMA foreign_keys=ON;
PRAGMA journal_mode=WAL;
PRAGMA synchronous=FULL;
PRAGMA busy_timeout=5000;
PRAGMA wal_autocheckpoint=0;
CREATE TABLE LocalUser (
 user_id TEXT PRIMARY KEY, -- 不可变应用用户UUID
 slot INTEGER NOT NULL UNIQUE CHECK(slot BETWEEN 1 AND 4), -- 最多4个用户槽位
 display_name TEXT NOT NULL, -- 中文可读名称
 normalized_name TEXT NOT NULL UNIQUE, -- NFC及大小写归一后的登录名
 password_salt BLOB NOT NULL CHECK(length(password_salt)>=16), -- 验证密码的独立随机盐
 password_verifier BLOB NOT NULL, -- KDF验证值，不是可逆密码
 password_kdf TEXT NOT NULL CHECK(password_kdf='pbkdf2-sha256'), -- 格式版本内的KDF算法
 password_iterations INTEGER NOT NULL CHECK(password_iterations>=600000), -- 验证KDF迭代数
 auth_version INTEGER NOT NULL DEFAULT 1, -- 会话失效版本
 failed_logins INTEGER NOT NULL DEFAULT 0, -- 应用登录失败计数
 locked_until_ms INTEGER, -- 登录冷却截止UTC
 state TEXT NOT NULL CHECK(state IN ('provisioning','active','deleting')), -- 注册与删除恢复状态
 created_at_ms INTEGER NOT NULL, -- 创建UTC
 updated_at_ms INTEGER NOT NULL -- 最近修改UTC
) STRICT;
CREATE TABLE UserKeyEnvelope (
 user_id TEXT PRIMARY KEY REFERENCES LocalUser(user_id) ON DELETE CASCADE, -- 所属用户
 key_id TEXT NOT NULL UNIQUE, -- 随机主密钥标识
 envelope_version INTEGER NOT NULL CHECK(envelope_version=1), -- 封套序列化版本
 kek_salt BLOB NOT NULL CHECK(length(kek_salt)>=16), -- 与密码验证盐不同的KEK盐
 kek_iterations INTEGER NOT NULL CHECK(kek_iterations>=600000), -- KEK派生迭代数
 dpapi_wrapped_envelope BLOB NOT NULL, -- DPAPI包裹的AES-GCM主密钥封套
 revision INTEGER NOT NULL DEFAULT 1, -- 密码变更并发CAS版本
 updated_at_ms INTEGER NOT NULL -- 最近换封套UTC
) STRICT;
CREATE TABLE CatalogSecret (
 singleton INTEGER PRIMARY KEY CHECK(singleton=1), -- 安装内唯一记录
 dpapi_hmac_key BLOB NOT NULL -- 邮箱唯一性指纹HMAC密钥，CurrentUser保护
) STRICT;
CREATE TABLE AccountReservation (
 account_id TEXT PRIMARY KEY, -- 对应用户库MailAccount UUID
 user_id TEXT NOT NULL REFERENCES LocalUser(user_id) ON DELETE CASCADE, -- 不对未认证请求公开的属主
 address_fingerprint BLOB NOT NULL UNIQUE CHECK(length(address_fingerprint)=32), -- 规范地址HMAC-SHA256
 state TEXT NOT NULL CHECK(state IN ('reserved','active','removing')), -- 跨库补偿状态
 created_at_ms INTEGER NOT NULL, -- 预留UTC
 expires_at_ms INTEGER -- 未完成预留可清理截止；active为空
) STRICT;
CREATE TABLE CatalogOperation (
 operation_id TEXT PRIMARY KEY, -- 注册与跨库清理操作UUID
 user_id TEXT NOT NULL REFERENCES LocalUser(user_id) ON DELETE CASCADE, -- 所属用户
 account_id TEXT, -- 可选账号UUID，不设跨库FK
 kind TEXT NOT NULL CHECK(kind IN ('create-user','delete-user','add-account','remove-account')), -- 操作类型
 state TEXT NOT NULL CHECK(state IN ('pending','running','done','failed')), -- 补偿流程进度
 updated_at_ms INTEGER NOT NULL -- 重启恢复排序UTC
) STRICT;
CREATE INDEX IX_Reservation_User ON AccountReservation(user_id,state);
CREATE INDEX IX_CatalogOperation_State ON CatalogOperation(state,updated_at_ms);
PRAGMA user_version=1;

-- 对目标开发/测试库只读执行；不要对用户生产库试验。
PRAGMA foreign_keys;
PRAGMA user_version;
PRAGMA foreign_key_check;
PRAGMA integrity_check;
SELECT version, sha256, applied_at_ms FROM SchemaMigration;
SELECT name, type FROM sqlite_master WHERE type IN ('table','index','trigger') ORDER BY type,name;

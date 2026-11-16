# 迁移约定

V0001 来自已批准阶段三 catalog/profile DDL，按字节导入，不用 ORM 生成覆盖。MigrationRunner 先核对实际 SQLite 版本，满足 WAL 修复底线后，独立配置连接 PRAGMA，再在事务中执行 DDL、外键检查和 SchemaMigration 记录。user_version=1。SQLite SQL中的同一PRAGMA不在DDL事务内重复执行。

同版重复启动核验迁移 SHA256，禁止原地修改已执行迁移；发现无迁移账本的已有业务表时拒绝接管。后续追加 V0002 等必须先批准设计和迁移/恢复方案，并更新执行器注册，不靠目录自动扫描执行任意SQL。

开发库 .local/development，测试库 .local/stage4/tests 及随机子目录；均忽略Git，工程种子使用不可投递的 example.invalid。不得把真实库路径传给工程启动程序。SchemaMigration是技术元数据附加表，不改变批准业务模型。

inspect.sql 只读检查，表字段以执行器为准。迁移没有自动降级：合并前测试空库、既有版本升级、重复执行、失败回滚；正式用户库升级前按设计做一致性备份。

# Design

## Context
见proposal.md。Starter含固定依赖、分层工程、API/SQL合同，未实现业务。用户已确认从需求分析进入完整实现。

## Goals / Non-Goals
**Goals:** 在既有35FR边界实现完整本地工作台，提供可复现合成验收与明确外部验收清单。
**Non-Goals:** 不接云端AI或中转、不实现会话合并/原生移动端/配置迁移、不在云端访问用户真实邮箱。

## Decisions
1. 维持Domain→Application端口←Infrastructure→Host组合根。Host只负责契约/授权/错误映射，应用服务编排协议与事务，Vue使用生成类型。
2. 保留v1DDL；126/Outlook属于generic适配器的预置参数，避免为模板复制协议实现或重建业务表。Outlook能否授权取决于账号策略，不把模板等同OAuth支持。
3. 每用户独立SQLite，密码PBKDF2验证与独立盐KEK，AES-GCM主密钥封套再经DPAPI；会话仅内存且单活跃，切换取消任务并清密钥，生产不提供跨平台DPAPI替代。
4. 固定UIDVALIDITY/快照上界与批事务推进游标，持久SyncJob驱动恢复和指数退避。完整服务器UID扫描成功后才能标记缺失，正文PEEK与独立SearchDocument覆盖，LRU不删除搜索正文。
5. SMTP冻结草稿、幂等键防重复HTTP提交；DATA后失联记录unknown且不自动重发。回复引用收到邮件的真实头，转发独立引用。
6. DOMPurify严格无属性白名单与sandbox/CSP隔离正文；请求校验Host/Origin、桥接与用户双令牌，附件流式限额且内部随机路径。
7. 显式demo启动使用独立标记数据根和合成协议适配器，可在Linux做测试；正式入口拒绝非Win11。演示凭据保护适配器绝不进入正式模式。真实邮箱验收由用户本机完成。

## Risks / Trade-offs
- [仅一天] → 先实现全部业务闭环，再按可执行测试逐项登记，不把未测项勾通过。
- [外部OAuth配置和服务商策略] → 提供本机配置指引，实际OAuth列待外部验收，演示不模拟OAuth成功。
- [Linux无法测试DPAPI/Windows托盘及脚本] → 编译验证与平台独立测试分开，Win11检查单明确标NOT_TESTED。
- [中文短词FTS限制] → 三码点以上trigram，短词参数化LIKE并测通配符字面语义。

## Migration Plan
保留原Starter及v1迁移哈希，在独立工作副本开发；正式/演示数据目录强制区分。交付源码、构建前端、启动脚本与文档；不包含测试库、凭据、缓存或工具链。回退使用原源码和事先备份，不静默覆盖旧数据库。

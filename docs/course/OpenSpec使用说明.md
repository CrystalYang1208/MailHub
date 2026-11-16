# OpenSpec 在本项目中的实际使用

选用官方 https://github.com/Fission-AI/OpenSpec ，版本2.0.0。它提供规范与变更管理，不是邮件协议库、代码质量证明或自动完成所有需求的工具。课程要求中的“Grill Me、Spec-Kit或OpenSpec”按任选一种理解；本项目实际使用OpenSpec。

## 本次记录

1. 需求探索：阅读Word与Starter，识别40FR/35FR、Win10/11、三模板/五模板及AC13/14差异。用户确认35FR、五模板、Win11后开始实施。
2. 查询既有上下文：实际运行`openspec context --json`、`list --json`、`list --specs --json`。Starter有8个能力规范和engineering-foundation变更。
3. 创建变更：`openspec new change deliver-mailhub-course-project`，由CLI创建元数据，没有手工伪造变更目录。
4. 逐项读取`instructions proposal/specs/design/tasks`与`status`，创建提案、模板需求delta、合成演示能力规范、设计和带验证方式的任务。
5. 实施与核对：实际运行`instructions apply --change deliver-mailhub-course-project --json`，把任务映射到代码和测试；只有完成相应工作才勾选。
6. 验证：`openspec validate --all --strict --no-interactive`。本轮现有8规范与2变更共10项严格校验；真实外部验收任务保留未勾选，不归档假装完成。

`proposal.md`解释为什么做，`specs/**/spec.md`描述可观察行为，`design.md`记录方案和权衡，`tasks.md`跟踪实施及验证。主规范描述产品目标，不能当成“已经实现”的证据。

## 重现命令

在MailHub根目录执行：

```powershell
npm ci --ignore-scripts
$env:OPENSPEC_TELEMETRY='0'
npx openspec --version
npx openspec list --json
npx openspec list --specs --json
npx openspec status --change deliver-mailhub-course-project --json
npx openspec instructions apply --change deliver-mailhub-course-project --json
npx openspec validate --all --strict --no-interactive
```

不重复init，不随意upgrade或重生成历史阶段设计。未来新增功能应另建Change；本次真实Win11/邮箱验收完成后再检查sync/archive命令帮助并归档。

## 答辩可展示的链条

需求FR-C05 → mail-reading规范（服务器已读为准） → 本次任务3.1 → Application/WorkspaceService.Sync.cs的持久MailMutation与确认 → MailKit UID STORE/FETCH → 后端集成测试与浏览器已读状态测试 → 验收记录。

需求FR-D04 → mail-compose规范 → 任务3.2 → 冻结MIME/OutboxSubmission → SMTP失败保稿与unknown不自动重发 → 合成故障测试及本机AC13。

需求FR-E01 → mail-search规范 → 任务3.1 → SearchDocument/FTS5与中文短词LIKE → 1000封真实SQLite合成测量 → 本机AC14。

OpenSpec严格校验只验证规范结构；通过它并不等于这三条业务都通过真实邮箱验收。

## 工程补充决定

- 126与Outlook模板走generic适配器，保留v1DDL与既有providerKey枚举。
- 当前API在Draft中新增一个向后兼容的可选`activeSubmissionId`，用于关闭窗口/重启后重新找到未知发送记录。历史设计API保留，契约检查只允许这个已记录差异；51个operationId不变，生成TypeScript同步更新。
- catalog V0002只新增添加账号前的授权失败冷却记录，保存HMAC指纹，v1迁移不改；迁移有版本与SHA256校验。
- 生产凭据只用Windows DPAPI；跨平台演示保护器只处理合成数据，不提供生产替代。
- 构建工具允许Node24.x和npm11.x兼容补丁，业务/测试依赖精确版本仍由锁文件固定。本轮具体环境见验收记录。
- 原设计的托盘菜单未实现；本轮使用后台进程窗口与页面锁定/切换，关闭后台窗口停止。它不替代35FR中的任何Must条目。

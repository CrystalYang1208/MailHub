# mail-sync Specification

## Purpose
本规范定义邮汇MailHub已批准的邮件同步与持久任务能力，作为后续独立开发和验收的稳定行为合同。本规范描述产品目标；当前实现与验证情况以Change任务及docs/course/verification记录为准，真实外部验收与合成验证分别登记。

## Requirements

### Requirement: FR-B01 首次接入按最近 90 天做增量同步
优先级Must。The system SHALL 首次接入按最近 90 天做增量同步。同步条数与服务器端一致。

接口：requestSync；测试：T-FR-B01；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-B01 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 同步条数与服务器端一致

#### Scenario: FR-B01 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-B02 基于 UID 与 UIDVALIDITY 做增量拉取与断点续传
优先级Must。The system SHALL 基于 UID 与 UIDVALIDITY 做增量拉取与断点续传。中断后恢复不重复不遗漏。

接口：requestSync；测试：T-FR-B02；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-B02 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 中断后恢复不重复不遗漏

#### Scenario: FR-B02 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-B03 支持手动同步（单账号或全部账号）
优先级Must。The system SHALL 支持手动同步（单账号或全部账号）。点击后 5 秒内有状态反馈。

接口：requestSync, listSyncJobs, retrySyncJob；测试：T-FR-B03；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-B03 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 点击后 5 秒内有状态反馈

#### Scenario: FR-B03 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-B04 支持定时自动同步，默认 10 分钟，可配置 5～60 分钟
优先级Should，不提升为Must。The system SHALL preserve this scope; when this feature is scheduled, 支持定时自动同步，默认 10 分钟，可配置 5～60 分钟。按设定间隔自动触发。

接口：requestSync, getSettings, updateSettings；测试：T-FR-B04；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-B04 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备，本Should项已安排实施
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 按设定间隔自动触发

#### Scenario: FR-B04 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-B05 同步失败进入待重试队列，按指数退避重试
优先级Must。The system SHALL 失败任务持久化，1/2/4/8分钟退避，进程重启恢复；可控时钟验证，不绕过三次邮箱认证失败10分钟冷却。

接口：requestSync, listSyncJobs, getSyncJob, retrySyncJob；测试：T-FR-B05；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-B05 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 失败任务持久化，1/2/4/8分钟退避，进程重启恢复；可控时钟验证，不绕过三次邮箱认证失败10分钟冷却。

#### Scenario: FR-B05 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-B06 UIDVALIDITY 变化时重建该文件夹索引并全量重拉
优先级Must。The system SHALL UIDVALIDITY变化使旧epoch失效，重建该文件夹索引并全量重拉，不用首次90天过滤限制代次重建。

接口：requestSync；测试：T-FR-B06；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-B06 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** UIDVALIDITY变化使旧epoch失效，重建该文件夹索引并全量重拉，不用首次90天过滤限制代次重建。

#### Scenario: FR-B06 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-B07 同步过程展示进度与结果，失败给出原因
优先级Should，不提升为Must。The system SHALL preserve this scope; when this feature is scheduled, 同步过程展示进度与结果，失败给出原因。有进度与最终结果文案。

接口：listSyncJobs, getSyncJob；测试：T-FR-B07；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-B07 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备，本Should项已安排实施
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 有进度与最终结果文案

#### Scenario: FR-B07 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-B08 控制并发，单账号同步并发不超过 3，全局不超过 5
优先级Should，不提升为Must。The system SHALL preserve this scope; when this feature is scheduled, 本功能优先级仍为Should；但BR-03的单账号<=3、全局<=5安全上限在任何同步实现中始终成立。

接口：listSyncJobs；测试：T-FR-B08；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-B08 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备，本Should项已安排实施
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 本功能优先级仍为Should；但BR-03的单账号<=3、全局<=5安全上限在任何同步实现中始终成立。

#### Scenario: FR-B08 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: NFR-03 性能
The system SHALL 满足：单账号增量 200 封邮件头 ≤30s（10Mbps）。相关原AC及T-NFR-03保持对应，阶段二原型不冒充产品验收。

#### Scenario: NFR-03 规定条件验收
- **GIVEN** 已按原需求及CR-08准备测试环境
- **WHEN** 构造 200 封新邮件后计时
- **THEN** 单账号增量 200 封邮件头 ≤30s（10Mbps）

### Requirement: NFR-05 可靠性
The system SHALL 满足：断网可读缓存，恢复网络后自动补拉。相关原AC及T-NFR-05保持对应，阶段二原型不冒充产品验收。

#### Scenario: NFR-05 规定条件验收
- **GIVEN** 已按原需求及CR-08准备测试环境
- **WHEN** 断开网络后浏览、恢复后观察同步
- **THEN** 断网可读缓存，恢复网络后自动补拉

### Requirement: BR-03 单账号同步并发 ≤3，全局并发 ≤5
The system SHALL 始终遵守：单账号同步并发 ≤3，全局并发 ≤5。理由：控制连接数，降低被限流概率。对应T-BR-03。

#### Scenario: BR-03 边界保持
- **GIVEN** 接近或达到本规则边界，或发生中断/重复请求
- **WHEN** 执行相关操作
- **THEN** 保持本规则，不通过重启、取消或错误降级绕过限制

### Requirement: BR-05 UIDVALIDITY 变化时重建该文件夹索引并全量重拉
The system SHALL 始终遵守：UIDVALIDITY 变化时重建该文件夹索引并全量重拉。理由：序号版本变化后旧 UID 失效，继续增量会错位。对应T-BR-05。

#### Scenario: BR-05 边界保持
- **GIVEN** 接近或达到本规则边界，或发生中断/重复请求
- **WHEN** 执行相关操作
- **THEN** 保持本规则，不通过重启、取消或错误降级绕过限制

## References
- 完整业务/接口/错误合同：docs/technical/design/TECHNICAL_DESIGN.md、openapi.yaml、TEST_PLAN.md。
- 已接受CR-01～CR-08及AC-01～AC-14：docs/requirements/BASELINE_DECISIONS.md；AC原文/补充定义在设计TEST_PLAN.md。
- 实施与代码位置：docs/collaboration/DEVELOPMENT_SLICES.md。

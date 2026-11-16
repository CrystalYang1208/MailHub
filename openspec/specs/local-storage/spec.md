# local-storage Specification

## Purpose
本规范定义邮汇MailHub已批准的本地持久化及缓存能力，作为后续独立开发和验收的稳定行为合同。本规范描述产品目标；当前实现与验证情况以Change任务及docs/course/verification记录为准，真实外部验收与合成验证分别登记。

## Requirements

### Requirement: FR-F02 设置正文缓存上限，默认 500MB，按 LRU 淘汰
优先级Must。The system SHALL 设置正文缓存上限，默认 500MB，按 LRU 淘汰。超限后旧正文被清理，邮件头保留。

接口：getSettings, updateSettings, clearBodyCache；测试：T-FR-F02；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-F02 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 超限后旧正文被清理，邮件头保留

#### Scenario: FR-F02 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-F03 一键清除本地邮件数据
优先级Should，不提升为Must。The system SHALL preserve this scope; when this feature is scheduled, 清除本地邮件及索引，保留账号配置/凭据/草稿和设置，暂停自动补拉直到用户手动同步；不是远程永久删信。

接口：getSettings, clearMailData, getOperation；测试：T-FR-F03；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-F03 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备，本Should项已安排实施
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 清除本地邮件及索引，保留账号配置/凭据/草稿和设置，暂停自动补拉直到用户手动同步；不是远程永久删信。

#### Scenario: FR-F03 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: NFR-11 一致性
The system SHALL 满足：同步中断不产生半条记录，索引与库一致。相关原AC及T-NFR-11保持对应，阶段二原型不冒充产品验收。

#### Scenario: NFR-11 规定条件验收
- **GIVEN** 已按原需求及CR-08准备测试环境
- **WHEN** 同步中强制断网，恢复后校验条数
- **THEN** 同步中断不产生半条记录，索引与库一致

### Requirement: BR-08 正文缓存默认 500MB，LRU 淘汰，仅淘汰正文不淘汰邮件头
The system SHALL 始终遵守：正文缓存默认 500MB，LRU 淘汰，仅淘汰正文不淘汰邮件头。理由：邮件头体积小、检索价值高，正文可重新拉取。对应T-BR-08。

#### Scenario: BR-08 边界保持
- **GIVEN** 接近或达到本规则边界，或发生中断/重复请求
- **WHEN** 执行相关操作
- **THEN** 保持本规则，不通过重启、取消或错误降级绕过限制

### Requirement: BR-09 时间统一以 UTC 存储，按系统时区展示
The system SHALL 始终遵守：时间统一以 UTC 存储，按系统时区展示。理由：跨时区与夏令时下排序与显示才一致。对应T-BR-09。

#### Scenario: BR-09 边界保持
- **GIVEN** 接近或达到本规则边界，或发生中断/重复请求
- **WHEN** 执行相关操作
- **THEN** 保持本规则，不通过重启、取消或错误降级绕过限制

### Requirement: BR-10 字符编码统一以 UTF-8 存储，兼容 GB2312/GBK 解码
The system SHALL 始终遵守：字符编码统一以 UTF-8 存储，兼容 GB2312/GBK 解码。理由：国内老邮件常见 GB 系编码，需要正确解码不乱码。对应T-BR-10。

#### Scenario: BR-10 边界保持
- **GIVEN** 接近或达到本规则边界，或发生中断/重复请求
- **WHEN** 执行相关操作
- **THEN** 保持本规则，不通过重启、取消或错误降级绕过限制

## References
- 完整业务/接口/错误合同：docs/technical/design/TECHNICAL_DESIGN.md、openapi.yaml、TEST_PLAN.md。
- 已接受CR-01～CR-08及AC-01～AC-14：docs/requirements/BASELINE_DECISIONS.md；AC原文/补充定义在设计TEST_PLAN.md。
- 实施与代码位置：docs/collaboration/DEVELOPMENT_SLICES.md。

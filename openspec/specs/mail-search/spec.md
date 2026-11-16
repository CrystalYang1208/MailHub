# mail-search Specification

## Purpose
本规范定义邮汇MailHub已批准的本地跨账号检索能力，作为后续独立开发和验收的稳定行为合同。本规范描述产品目标；当前实现与验证情况以Change任务及docs/course/verification记录为准，真实外部验收与合成验证分别登记。

## Requirements

### Requirement: FR-E01 本地全文检索（主题、正文、发件人）覆盖全部已同步账号
优先级Must。The system SHALL 主题/发件人/正文覆盖本用户全部已同步账号；未读及阅读缓存淘汰后的正文仍可搜索；返回索引覆盖状态，短词使用安全LIKE回退。

接口：searchMessages；测试：T-FR-E01；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-E01 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 主题/发件人/正文覆盖本用户全部已同步账号；未读及阅读缓存淘汰后的正文仍可搜索；返回索引覆盖状态，短词使用安全LIKE回退。

#### Scenario: FR-E01 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-E02 组合筛选（账号、时间范围、有无附件、已读状态）
优先级Should，不提升为Must。The system SHALL preserve this scope; when this feature is scheduled, 组合筛选（账号、时间范围、有无附件、已读状态）。多个条件可叠加生效。

接口：searchMessages；测试：T-FR-E02；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-E02 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备，本Should项已安排实施
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 多个条件可叠加生效

#### Scenario: FR-E02 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: NFR-04 性能
The system SHALL 满足：本地检索 1000 封 ≤1s。相关原AC及T-NFR-04保持对应，阶段二原型不冒充产品验收。

#### Scenario: NFR-04 规定条件验收
- **GIVEN** 已按原需求及CR-08准备测试环境
- **WHEN** 同一关键词重复检索 3 次计时
- **THEN** 本地检索 1000 封 ≤1s

## References
- 完整业务/接口/错误合同：docs/technical/design/TECHNICAL_DESIGN.md、openapi.yaml、TEST_PLAN.md。
- 已接受CR-01～CR-08及AC-01～AC-14：docs/requirements/BASELINE_DECISIONS.md；AC原文/补充定义在设计TEST_PLAN.md。
- 实施与代码位置：docs/collaboration/DEVELOPMENT_SLICES.md。

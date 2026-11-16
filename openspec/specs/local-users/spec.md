# local-users Specification

## Purpose
本规范定义邮汇MailHub已批准的本地应用用户能力，作为后续独立开发和验收的稳定行为合同。本规范描述产品目标；当前实现与验证情况以Change任务及docs/course/verification记录为准，真实外部验收与合成验证分别登记。

## Requirements

### Requirement: FR-F01 本地多用户切换与数据隔离
优先级Must。The system SHALL 同Windows账户下最多四个密码认证应用用户，一次仅一个活跃；切换撤销旧会话和密钥租约，账号/邮件/草稿/搜索/缓存/凭据均隔离；DPAPI alone不构成应用用户隔离。

接口：bootstrapBridge, listUsers, registerUser, login, getCurrentSession, logout, switchUser, changePassword, deleteUser, getOperation；测试：T-FR-F01；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-F01 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 同Windows账户下最多四个密码认证应用用户，一次仅一个活跃；切换撤销旧会话和密钥租约，账号/邮件/草稿/搜索/缓存/凭据均隔离；DPAPI alone不构成应用用户隔离。

#### Scenario: FR-F01 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: NFR-14 易用
The system SHALL 满足：4 个本地用户共用时数据互不可见。相关原AC及T-NFR-14保持对应，阶段二原型不冒充产品验收。

#### Scenario: NFR-14 规定条件验收
- **GIVEN** 已按原需求及CR-08准备测试环境
- **WHEN** 切换用户后核对账号与邮件列表
- **THEN** 4 个本地用户共用时数据互不可见

## References
- 完整业务/接口/错误合同：docs/technical/design/TECHNICAL_DESIGN.md、openapi.yaml、TEST_PLAN.md。
- 已接受CR-01～CR-08及AC-01～AC-14：docs/requirements/BASELINE_DECISIONS.md；AC原文/补充定义在设计TEST_PLAN.md。
- 实施与代码位置：docs/collaboration/DEVELOPMENT_SLICES.md。

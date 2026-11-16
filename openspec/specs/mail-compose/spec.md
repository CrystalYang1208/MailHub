# mail-compose Specification

## Purpose
本规范定义邮汇MailHub已批准的草稿与发送能力，作为后续独立开发和验收的稳定行为合同。本规范描述产品目标；当前实现与验证情况以Change任务及docs/course/verification记录为准，真实外部验收与合成验证分别登记。

## Requirements

### Requirement: FR-D01 新建邮件，可选择发件账号、填写收件人、主题、正文
优先级Must。The system SHALL 新建邮件，可选择发件账号、填写收件人、主题、正文。可从任一已接入账号发出。

接口：createDraft, sendDraft；测试：T-FR-D01；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-D01 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 可从任一已接入账号发出

#### Scenario: FR-D01 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-D02 回复与转发，正确携带引用内容与线程头
优先级Must。The system SHALL 回复使用真实收到原邮件的Message-ID/References关联原会话；转发作为独立新邮件携引用，不带伪造回复头，不提供会话合并UI。

接口：createReply, createForward, sendDraft；测试：T-FR-D02；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-D02 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 回复使用真实收到原邮件的Message-ID/References关联原会话；转发作为独立新邮件携引用，不带伪造回复头，不提供会话合并UI。

#### Scenario: FR-D02 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-D03 草稿本地保存与恢复
优先级Should，不提升为Must。The system SHALL preserve this scope; when this feature is scheduled, 草稿本地保存与恢复。关闭后重新打开草稿内容完整。

接口：createDraft, listDrafts, getDraft, saveDraft, deleteDraft；测试：T-FR-D03；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-D03 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备，本Should项已安排实施
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 关闭后重新打开草稿内容完整

#### Scenario: FR-D03 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-D04 发送失败保留草稿并提示原因
优先级Must。The system SHALL 发送失败保留草稿并提示原因。失败后草稿不丢失。

接口：createDraft, listDrafts, getDraft, saveDraft, sendDraft, getSubmission, resolveSubmission；测试：T-FR-D04；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-D04 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 失败后草稿不丢失

#### Scenario: FR-D04 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-D05 添加附件，单封不超过 25MB
优先级Should，不提升为Must。The system SHALL preserve this scope; when this feature is scheduled, 添加附件，单封不超过 25MB。超限时阻止发送并提示。

接口：uploadDraftAttachment, removeDraftAttachment, sendDraft；测试：T-FR-D05；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-D05 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备，本Should项已安排实施
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 超限时阻止发送并提示

#### Scenario: FR-D05 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

## References
- 完整业务/接口/错误合同：docs/technical/design/TECHNICAL_DESIGN.md、openapi.yaml、TEST_PLAN.md。
- 已接受CR-01～CR-08及AC-01～AC-14：docs/requirements/BASELINE_DECISIONS.md；AC原文/补充定义在设计TEST_PLAN.md。
- 实施与代码位置：docs/collaboration/DEVELOPMENT_SLICES.md。

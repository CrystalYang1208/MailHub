# account-management Specification

## Purpose
本规范定义邮汇MailHub已批准的账号接入与生命周期能力，作为后续独立开发和验收的稳定行为合同。本规范描述产品目标；当前实现与验证情况以Change任务及docs/course/verification记录为准，真实外部验收与合成验证分别登记。

## Requirements

### Requirement: FR-A01 支持通用 IMAP 接入：手工填写服务器地址、端口、加密方式与认证方式
优先级Must。The system SHALL 支持通用 IMAP 接入：手工填写服务器地址、端口、加密方式与认证方式。模板外邮箱也能完成接入。

接口：listProviders, validateAccount, addAccount；测试：T-FR-A01；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-A01 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 模板外邮箱也能完成接入

#### Scenario: FR-A01 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-A02 内置常见邮箱模板（QQ、163、Gmail），选中后自动填充服务器参数
优先级Must。The system SHALL 内置常见邮箱模板（QQ、163、Gmail），选中后自动填充服务器参数。模板选中后地址端口自动填好。

接口：listProviders, validateAccount, listAccounts；测试：T-FR-A02；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-A02 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 模板选中后地址端口自动填好

#### Scenario: FR-A02 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-A03 Gmail 模板优先走 OAuth 2.0，兼容应用专用密码
优先级Must。The system SHALL Gmail 模板优先走 OAuth 2.0，兼容应用专用密码。两种方式均可完成接入。

接口：listProviders, importOAuthClient, startOAuth, getOAuthFlow, validateAccount；测试：T-FR-A03；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-A03 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 两种方式均可完成接入

#### Scenario: FR-A03 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-A04 添加账号时展示分步授权指引（开启 IMAP、生成授权码、服务器参数核对）
优先级Must。The system SHALL 添加账号时展示分步授权指引（开启 IMAP、生成授权码、服务器参数核对）。每步有可执行说明，不需查阅外部文档。

接口：listProviders, validateAccount；测试：T-FR-A04；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-A04 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 每步有可执行说明，不需查阅外部文档

#### Scenario: FR-A04 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-A05 校验凭据有效性，失败时区分授权失败、未开启 IMAP、服务器地址或端口错误、网络不可达
优先级Must。The system SHALL 校验凭据有效性，失败时区分授权失败、未开启 IMAP、服务器地址或端口错误、网络不可达。四类失败提示文案不同且可操作。

接口：validateAccount；测试：T-FR-A05；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-A05 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 四类失败提示文案不同且可操作

#### Scenario: FR-A05 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-A06 凭据加密存储于本机，界面与日志不出现明文
优先级Must。The system SHALL 凭据加密存储于本机，界面与日志不出现明文。抓取界面与日志均无明文。

接口：validateAccount, addAccount, getAccount；测试：T-FR-A06；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-A06 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 抓取界面与日志均无明文

#### Scenario: FR-A06 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-A07 支持重新授权（令牌过期或授权码变更）
优先级Must。The system SHALL 支持重新授权（令牌过期或授权码变更）。重新授权后恢复同步，不需重建账号。

接口：startOAuth, getOAuthFlow, reauthorizeAccount；测试：T-FR-A07；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-A07 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 重新授权后恢复同步，不需重建账号

#### Scenario: FR-A07 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-A08 支持移除账号并清除该账号本地数据
优先级Must。The system SHALL 支持移除账号并清除该账号本地数据。移除后本地库无该账号任何记录。

接口：removeAccount, getOperation；测试：T-FR-A08；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-A08 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 移除后本地库无该账号任何记录

#### Scenario: FR-A08 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-A09 支持账号启用/停用，停用保留配置但不参与同步
优先级Should，不提升为Must。The system SHALL preserve this scope; when this feature is scheduled, 支持账号启用/停用，停用保留配置但不参与同步。停用后不再产生同步请求。

接口：setAccountEnabled；测试：T-FR-A09；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-A09 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备，本Should项已安排实施
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 停用后不再产生同步请求

#### Scenario: FR-A09 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: NFR-13 易用
The system SHALL 满足：全中文界面，添加账号 ≤3 步。相关原AC及T-NFR-13保持对应，阶段二原型不冒充产品验收。

#### Scenario: NFR-13 规定条件验收
- **GIVEN** 已按原需求及CR-08准备测试环境
- **WHEN** 计时并记录操作步数
- **THEN** 全中文界面，添加账号 ≤3 步

### Requirement: BR-01 同一邮箱地址在本地只允许存在一个账号实例
The system SHALL 始终遵守：同一邮箱地址在本地只允许存在一个账号实例。理由：避免同一邮箱被重复同步导致列表重复。对应T-BR-01。

#### Scenario: BR-01 边界保持
- **GIVEN** 接近或达到本规则边界，或发生中断/重复请求
- **WHEN** 执行相关操作
- **THEN** 保持本规则，不通过重启、取消或错误降级绕过限制

### Requirement: BR-02 连续 3 次授权失败后冷却 10 分钟
The system SHALL 始终遵守：连续 3 次授权失败后冷却 10 分钟。理由：防止频繁重试触发服务商风控，也提示用户先检查配置。对应T-BR-02。

#### Scenario: BR-02 边界保持
- **GIVEN** 接近或达到本规则边界，或发生中断/重复请求
- **WHEN** 执行相关操作
- **THEN** 保持本规则，不通过重启、取消或错误降级绕过限制

## References
- 完整业务/接口/错误合同：docs/technical/design/TECHNICAL_DESIGN.md、openapi.yaml、TEST_PLAN.md。
- 已接受CR-01～CR-08及AC-01～AC-14：docs/requirements/BASELINE_DECISIONS.md；AC原文/补充定义在设计TEST_PLAN.md。
- 实施与代码位置：docs/collaboration/DEVELOPMENT_SLICES.md。

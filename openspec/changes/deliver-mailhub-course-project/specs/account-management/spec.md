# Spec Delta

## MODIFIED Requirements

### Requirement: FR-A02 内置常见邮箱模板（QQ、163、Gmail），选中后自动填充服务器参数
优先级Must。The system SHALL 内置QQ、163、126、Gmail、Outlook五种邮箱模板及通用接入，选中后自动填充服务器参数。126和Outlook使用通用协议适配器。模板选中后地址端口自动填好。

接口：listProviders, validateAccount, listAccounts；测试：T-FR-A02；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-A02 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 模板选中后地址端口自动填好

#### Scenario: FR-A02 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录


#### Scenario: 补充模板保持通用契约
- **WHEN** 用户选择126或Outlook模板
- **THEN** 自动填充对应IMAP/SMTP参数，以generic适配器完成校验；不承诺服务商已停用的认证方式。

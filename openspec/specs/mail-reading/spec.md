# mail-reading Specification

## Purpose
本规范定义邮汇MailHub已批准的阅读与服务器整理能力，作为后续独立开发和验收的稳定行为合同。本规范描述产品目标；当前实现与验证情况以Change任务及docs/course/verification记录为准，真实外部验收与合成验证分别登记。

## Requirements

### Requirement: FR-C01 统一收件箱跨账号聚合，按收信时间倒序
优先级Must。The system SHALL 统一收件箱跨账号聚合，按收信时间倒序。3 个账号邮件混合正确排序。

接口：listMessages；测试：T-FR-C01；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-C01 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 3 个账号邮件混合正确排序

#### Scenario: FR-C01 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-C02 邮件列表显示来源账号标识
优先级Must。The system SHALL 邮件列表显示来源账号标识。每条记录可见账号标签。

接口：listAccounts, listMessages；测试：T-FR-C02；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-C02 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 每条记录可见账号标签

#### Scenario: FR-C02 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-C03 阅读邮件正文，支持 HTML 与纯文本，默认不加载外部图片
优先级Must。The system SHALL 阅读邮件正文，支持 HTML 与纯文本，默认不加载外部图片。默认状态下不发外部图片请求。

接口：getMessage, getMessageBody；测试：T-FR-C03；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-C03 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 默认状态下不发外部图片请求

#### Scenario: FR-C03 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-C04 下载附件到本地指定目录
优先级Should，不提升为Must。The system SHALL preserve this scope; when this feature is scheduled, 下载附件到本地指定目录。下载后文件可正常打开。

接口：createDownloadBatch, getDownloadBatch, getAttachmentContent；测试：T-FR-C04；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-C04 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备，本Should项已安排实施
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 下载后文件可正常打开

#### Scenario: FR-C04 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-C05 标记已读/未读并以服务器状态为准
优先级Must。The system SHALL 标记已读/未读并以服务器状态为准。本地标记后服务端状态同步变更。

接口：setMessageRead；测试：T-FR-C05；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-C05 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 本地标记后服务端状态同步变更

#### Scenario: FR-C05 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-C06 删除邮件仅移至服务端垃圾箱，不做本地永久删除
优先级Must。The system SHALL 仅移动到服务器垃圾箱；不支持安全MOVE/UIDPLUS降级时明确失败，不以普通EXPUNGE或永久删除代替。

接口：moveMessageToTrash；测试：T-FR-C06；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-C06 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 仅移动到服务器垃圾箱；不支持安全MOVE/UIDPLUS降级时明确失败，不以普通EXPUNGE或永久删除代替。

#### Scenario: FR-C06 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-C07 按账号与文件夹切换视图
优先级Should，不提升为Must。The system SHALL preserve this scope; when this feature is scheduled, 按账号与文件夹切换视图。切换后列表内容与文件夹一致。

接口：listFolders, listMessages；测试：T-FR-C07；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-C07 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备，本Should项已安排实施
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 切换后列表内容与文件夹一致

#### Scenario: FR-C07 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: FR-C08 展示各文件夹未读数
优先级Should，不提升为Must。The system SHALL preserve this scope; when this feature is scheduled, 展示各文件夹未读数。未读数与服务器一致。

接口：listFolders；测试：T-FR-C08；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。

#### Scenario: FR-C08 成功行为
- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备，本Should项已安排实施
- **WHEN** 用户或已授权后台执行本需求对应操作
- **THEN** 未读数与服务器一致

#### Scenario: FR-C08 授权或运行失败
- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败
- **WHEN** 发起该操作
- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录

### Requirement: NFR-01 性能
The system SHALL 满足：缓存可用时首屏 ≤2s。相关原AC及T-NFR-01保持对应，阶段二原型不冒充产品验收。

#### Scenario: NFR-01 规定条件验收
- **GIVEN** 已按原需求及CR-08准备测试环境
- **WHEN** 秒表或浏览器计时，重复 5 次取中位数
- **THEN** 缓存可用时首屏 ≤2s

### Requirement: NFR-02 性能
The system SHALL 满足：1000 封列表滚动无明显卡顿，交互响应 ≤100ms。相关原AC及T-NFR-02保持对应，阶段二原型不冒充产品验收。

#### Scenario: NFR-02 规定条件验收
- **GIVEN** 已按原需求及CR-08准备测试环境
- **WHEN** 人工滚动 + 开发者工具帧率观察
- **THEN** 1000 封列表滚动无明显卡顿，交互响应 ≤100ms

### Requirement: BR-04 单封附件 ≤25MB，批量下载累计 ≤200MB
The system SHALL 始终遵守：单封附件 ≤25MB，批量下载累计 ≤200MB。理由：多数服务商附件上限在 25MB 左右，批量限制保护内存。对应T-BR-04。

#### Scenario: BR-04 边界保持
- **GIVEN** 接近或达到本规则边界，或发生中断/重复请求
- **WHEN** 执行相关操作
- **THEN** 保持本规则，不通过重启、取消或错误降级绕过限制

### Requirement: BR-06 已读状态以服务器为准，本地仅作展示缓存
The system SHALL 始终遵守：已读状态以服务器为准，本地仅作展示缓存。理由：多端登录时本地状态会被覆盖，以服务器为唯一真相。对应T-BR-06。

#### Scenario: BR-06 边界保持
- **GIVEN** 接近或达到本规则边界，或发生中断/重复请求
- **WHEN** 执行相关操作
- **THEN** 保持本规则，不通过重启、取消或错误降级绕过限制

### Requirement: BR-07 删除仅在服务器侧移至垃圾箱，不做本地永久删除
The system SHALL 始终遵守：删除仅在服务器侧移至垃圾箱，不做本地永久删除。理由：误删可恢复，也符合各服务商的删除语义。对应T-BR-07。

#### Scenario: BR-07 边界保持
- **GIVEN** 接近或达到本规则边界，或发生中断/重复请求
- **WHEN** 执行相关操作
- **THEN** 保持本规则，不通过重启、取消或错误降级绕过限制

## References
- 完整业务/接口/错误合同：docs/technical/design/TECHNICAL_DESIGN.md、openapi.yaml、TEST_PLAN.md。
- 已接受CR-01～CR-08及AC-01～AC-14：docs/requirements/BASELINE_DECISIONS.md；AC原文/补充定义在设计TEST_PLAN.md。
- 实施与代码位置：docs/collaboration/DEVELOPMENT_SLICES.md。

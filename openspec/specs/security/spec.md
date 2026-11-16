# security Specification

## Purpose
本规范定义邮汇MailHub已批准的本地通信与邮件安全能力，作为后续独立开发和验收的稳定行为合同。本规范描述产品目标；当前实现与验证情况以Change任务及docs/course/verification记录为准，真实外部验收与合成验证分别登记。

## Requirements

### Requirement: NFR-06 安全
The system SHALL 满足：凭据以 Windows DPAPI 加密存储，界面与日志无明文。相关原AC及T-NFR-06保持对应，阶段二原型不冒充产品验收。

#### Scenario: NFR-06 规定条件验收
- **GIVEN** 已按原需求及CR-08准备测试环境
- **WHEN** 检索日志与界面截图，核对无明文
- **THEN** 凭据以 Windows DPAPI 加密存储，界面与日志无明文

### Requirement: NFR-07 安全
The system SHALL 满足：邮件数据仅存本机，不向第三方上传。相关原AC及T-NFR-07保持对应，阶段二原型不冒充产品验收。

#### Scenario: NFR-07 规定条件验收
- **GIVEN** 已按原需求及CR-08准备测试环境
- **WHEN** 抓包观察出网目标，仅允许邮件服务器
- **THEN** 邮件数据仅存本机，不向第三方上传

### Requirement: NFR-08 安全
The system SHALL 满足：本地服务仅监听 127.0.0.1 且校验随机令牌。相关原AC及T-NFR-08保持对应，阶段二原型不冒充产品验收。

#### Scenario: NFR-08 规定条件验收
- **GIVEN** 已按原需求及CR-08准备测试环境
- **WHEN** netstat 查看监听地址，无令牌请求应被拒绝
- **THEN** 本地服务仅监听 127.0.0.1 且校验随机令牌

### Requirement: NFR-09 安全
The system SHALL 满足：HTML 邮件外部图片默认不加载，脚本禁止执行。相关原AC及T-NFR-09保持对应，阶段二原型不冒充产品验收。

#### Scenario: NFR-09 规定条件验收
- **GIVEN** 已按原需求及CR-08准备测试环境
- **WHEN** 打开含追踪像素的邮件，抓包应无外部请求
- **THEN** HTML 邮件外部图片默认不加载，脚本禁止执行

### Requirement: NFR-10 安全
The system SHALL 满足：日志脱敏，不含正文、附件名、完整地址。相关原AC及T-NFR-10保持对应，阶段二原型不冒充产品验收。

#### Scenario: NFR-10 规定条件验收
- **GIVEN** 已按原需求及CR-08准备测试环境
- **WHEN** 抽查日志文件内容
- **THEN** 日志脱敏，不含正文、附件名、完整地址

### Requirement: NFR-12 兼容
The system SHALL 满足：首版仅Windows11，Chrome与Edge最新版；CR-08取消Windows10兼容范围。相关原AC及T-NFR-12保持对应，阶段二原型不冒充产品验收。

#### Scenario: NFR-12 规定条件验收
- **GIVEN** 已按原需求及CR-08准备测试环境
- **WHEN** 两浏览器各跑一次主流程
- **THEN** 首版仅Windows11，Chrome与Edge最新版；CR-08取消Windows10兼容范围

## References
- 完整业务/接口/错误合同：docs/technical/design/TECHNICAL_DESIGN.md、openapi.yaml、TEST_PLAN.md。
- 已接受CR-01～CR-08及AC-01～AC-14：docs/requirements/BASELINE_DECISIONS.md；AC原文/补充定义在设计TEST_PLAN.md。
- 实施与代码位置：docs/collaboration/DEVELOPMENT_SLICES.md。

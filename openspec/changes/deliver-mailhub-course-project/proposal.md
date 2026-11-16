# Proposal

## Why
用户已在2026-11-16确认采用35条功能需求、五种邮箱模板、Windows11及OpenSpec，需在11月17日晚前交付前后端完整联通的课程项目。现有Starter只有工程基础，不能完成邮箱操作。

## What Changes
- 实施既有8领域规范的35FR、14NFR与10BR；保留Must/Should及原验收编号，补充编号映射。
- 将QQ/163/Gmail模板补齐为QQ/163/126/Gmail/Outlook，通用协议适配保持不变。
- 完成本地身份与密码密钥封套、独立用户库、持久同步、阅读整理、发送和本地检索、设置。
- 提供隔离的合成数据演示模式，便于无测试邮箱时答辩；生产模式仅Windows11，演示模式不接真实邮箱。
- 补充Windows启动脚本、测试记录、真实邮箱验收指南与OpenSpec课程使用说明；如实区分代码测试与外部验收。

## Capabilities

### New Capabilities
- `demo-workspace`: 独立合成数据演示与真实账号隔离。

### Modified Capabilities
- `account-management`: FR-A02新增126和Outlook模板；其余既有业务规范进入实施。

## Impact
frontend/src及backend/src各分层、后端/浏览器测试、tools启动交付、OpenSpec与课程文档。新增DOMPurify用于HTML净化。既有v1数据库DDL与API保持不变；126与Outlook模板使用generic协议适配器，按label和服务器参数区分。GmailOAuth及Win11DPAPI真实验收需用户在本机执行，不使用用户主力邮箱或在聊天传递凭据。

# Design

## Context

遵循docs/technical/design全部已批准合同及阶段四任务书；原型不直接作为产品工程。根目录为MailHub，父工作区的工具可临时复用但团队环境从锁文件还原。

## Goals / Non-Goals

Goals：可构建工程、迁移、规范/契约检查、实际SQLite版本、最小同源UI通信、团队独立开发入口。

Non-Goals：完整应用用户认证、邮箱接入/同步、邮件管理/编辑器、Repository全集、真实邮件测试、发布/远程设置。

## Decisions

- 分层程序集Domain/Application/Infrastructure/Host与基础测试，Infrastructure下按Persistence/Protocols/Security分目录；不为每个空模块建微服务或过多项目。
- /_health只返回非敏感健康状态；合成数据路由放/_engineering，只有显式工程模式可用，绝不伪装产品/api/v1能力。主契约字节保持不变；未实现产品API明确503及FEATURE_NOT_IMPLEMENTED。
- Host同源提供构建后静态资源；Vite仅用于UI/HMR，不开放跨源代理或放宽后台来源校验；仅127.0.0.1、一次引导码/随机内存令牌、Host/Origin防护。工程模式没有产品用户会话，不能对接真实数据。
- SQL版本脚本按哈希与user_version管理；实际加载SQLite未达官方修复底线即拒绝启用WAL。开发和测试目录分离。
- 八主specs记录已批准目标能力不声称已实现；未来业务Changes作为待实施切片登记，不在本阶段勾选完成。

## Risks / Trade-offs

- 网络/审计源受限→保留准确失败证据，使用工作区缓存；不当作无漏洞。
- 生成Codex集成可能影响旧文件→前置检查、限定项目根、不force、保留父目录Skills。
- 阶段二个人信息证据→默认Git排除整个历史证据目录，公开报告只使用编号摘要。

## Migration Plan

只创建新开发库，不升级原型数据；批准DDL/OpenAPI副本有SHA256核对。工程验证失败只修本变更文件，不回写设计或移除历史结果。

# Proposal

## Why

已批准设计尚无可构建产品工程，团队缺统一构建、契约和协作入口。建立可复现工程基础后再进入业务实现。

## What Changes

- 创建Vue/TS/Vite按需Element Plus最小页及分层.NET10解决方案。
- 导入批准SQL/OpenAPI、锁定依赖并核验实际SQLite运行库修复底线。
- 建立八能力已确认规范、后续功能切片待办、Git/CI及本地检查。
- 用仅本机合成数据的工程专用端点演示通信；不实现产品邮件/登录/同步API。

## Capabilities

### New Capabilities

无产品能力新增，skip_specs=true仅用于本工程工具变更；批准的业务能力规范另导入主specs。

### Modified Capabilities

无业务需求变更，不修改批准OpenAPI或ADR。

## Impact

新增frontend/backend/database/contracts/tools和协作文件；保留validation及design原件。无远程仓库创建、推送或安装包发布。

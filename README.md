# 邮汇 MailHub

**本地多邮箱工作台 · 软件需求与分析期末项目**

Vue 3 + TypeScript + Element Plus 前端，ASP.NET Core / .NET 10 后端，MailKit / MimeKit 邮件协议，SQLite / FTS5 存储与检索，Windows DPAPI 与用户密码密钥封套保护凭据。

本轮在 Developer Starter 的基础上实现邮箱业务。2026-11-16 用户确认采用 **35 FR（24 Must / 11 Should）、五种邮箱模板、Windows 11、OpenSpec**。完整实现范围及真实验收状态见 [需求追踪](docs/course/需求追踪.md) 和 [验收记录](docs/course/验收记录.md)。工程规范存在、合成测试通过，都不能代替真实邮箱验收。

## 最快运行：Windows 11 免安装包

**下载地址：[课程项目 v1.0.0 发布页](https://github.com/CrystalYang1208/MailHub/releases/tag/v1.0.0-course)**

在发布页的 Assets 中下载 `MailHub-Win11-x64.zip`（Win11 免安装运行包），或下载 `MailHub-Complete.zip`（含运行包、原始源码与期末报告）。报告也单独提供。

解压 `MailHub-Win11-x64.zip`，保留 `wwwroot` 目录与程序在一起：

- 双击 **运行演示.cmd**：三个合成邮箱，无需任何真实凭据，可展示完整前后端流程。
- 双击 **运行正式版.cmd**：创建本地用户，再添加自己的测试邮箱。真实邮件只保存在你的电脑。
- 再次运行启动器会打开已运行实例的新链接。直接刷新旧页面会丢失内存令牌，请重新运行启动器。
- 关闭后台程序窗口即可停止服务；关闭浏览器标签不会停止后台。页面左下角可锁定或切换本地用户。

免安装包自带.NET运行时，**不需要安装Node、npm或.NET SDK**。Win11程序由Linux环境交叉编译；本轮未在Windows上实际运行，首次请按 [本机验收清单](docs/course/本机验收清单.md) 检查。

演示和正式模式使用不同的 `.local/demo` 与 `.local/mailhub`，数据根带模式标记，禁止混用。演示采用合成协议适配器，只接受 `example.invalid` 地址，不连接真实邮箱、不模拟Google授权成功。演示快捷用户为“林同学”，固定演示密码 `MailHub-Demo-2026!`，仅适用于无真实数据的演示空间。

## 从源码启动

推荐工具：Node 24.20+（24.x）、npm 11.9+（11.x）、.NET SDK 10.0.404、Git，以及Chrome/Edge。运行正式模式必须Windows11。

```powershell
npm ci --ignore-scripts
dotnet restore backend/MailHub.slnx --locked-mode
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Start-MailHub.ps1 -Demo -Open
# 正式模式：去掉 -Demo
```

前端单独 `npm run dev --workspace frontend` 只用于UI开发。真实联通使用构建后的前端与本地Host同源运行，不开放跨域CORS。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-Product.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Publish-Windows.ps1
```

质量脚本使用合成邮箱，浏览器测试默认检查实际已安装的Chrome和Edge。只测一个浏览器可传 `-Browser chrome` 或 `-Browser edge`；跳过浏览器不是浏览器验收通过。

Linux仅允许合成演示和可移植测试：先构建前端与后端，然后 `MAILHUB_ROOT=<项目绝对路径> dotnet backend/src/MailHub.Host/bin/Debug/net10.0/MailHub.Host.dll --demo --no-browser`，从本机 `.local/demo/startup.json` 读取一次性链接。正式模式拒绝非Win11平台，不能拿演示保护器处理真实邮件。

## 已实现的业务

- 最多四个本地用户，单活跃身份、密码认证、改密、退出、删除；跨用户账号/邮件/搜索/草稿/下载隔离，切换撤销旧会话。
- QQ、163、126、Gmail、Outlook模板与通用IMAP；三步指引、IMAP及SMTP校验、加密凭据、重授权、移除、启停。
- Gmail桌面OAuth：PKCE、随机state、回环回调、离线刷新、single-flight与凭据版本保护；兼容具备资格的应用专用密码。
- 最近90天首次同步，UID/UIDVALIDITY、批事务游标与恢复、完整UID对账、持久任务、5～60分钟定时同步、1/2/4/8分钟退避；每账号最多3个连接、全局最多5个。
- 跨账号统一收件箱、账号/文件夹视图、服务器未读数；正文PEEK、严格HTML净化与沙箱；正文缓存离线阅读、附件按实际字节限额下载、已读确认、移至服务端垃圾箱。
- 新建/回复/独立转发、自动本地草稿、25MB出站附件、冻结MIME、幂等发送、明确失败保稿、未知状态人工核对且不自动重发。
- 本地主题/发件人/正文检索，中文短词、组合筛选、覆盖率与分页；阅读缓存500MB默认LRU，淘汰不删除正文索引；清除邮件保留账号与草稿并暂停自动补拉。

**Outlook边界：** 模板提供IMAP/SMTP参数并走通用适配器。Microsoft多数个人账号已停用基本认证，本项目没有Microsoft OAuth；并不保证所有Outlook账号可实际授权。课程真实收发优先用QQ/163测试邮箱。Gmail OAuth需在你本机配置自己的Google Cloud桌面客户端和测试用户。

不包含：会话合并、修改同步范围/显示名、搜索高亮、开机自启、配置迁移、云端AI、邮件中转、营销群发、移动原生端。本轮后台以进程窗口运行，未实现Windows托盘菜单。

## OpenSpec 使用

项目固定使用官方OpenSpec 2.0.0，不需重复init，也不覆盖现有规范。

```powershell
$env:OPENSPEC_TELEMETRY='0'
npx openspec list --json
npx openspec status --change deliver-mailhub-course-project --json
npx openspec instructions apply --change deliver-mailhub-course-project --json
npx openspec validate --all --strict --no-interactive
```

变更路径：`openspec/changes/deliver-mailhub-course-project`，含proposal、需求delta、design和带验证方式的tasks。课程答辩过程与截图位置见 [OpenSpec使用说明](docs/course/OpenSpec使用说明.md)。真实外部验收任务保持未勾选，不提前归档、不虚报完成。

## 目录

| 路径 | 说明 |
|---|---|
| `frontend/src` | 中文Vue页面、只在内存的令牌、安全阅读组件 |
| `backend/src/MailHub.Domain` | 限额、地址规范、字面搜索和退避规则 |
| `backend/src/MailHub.Application/Workspace` | 身份、账号、同步、邮件、草稿、搜索等用例与端口 |
| `backend/src/MailHub.Infrastructure` | SQLite、DPAPI、MailKit、Google OAuth、合成与限流适配器 |
| `backend/src/MailHub.Host` | 同源静态资源/API、双令牌、请求边界、后台任务与启动入口 |
| `backend/tests` / `frontend/tests` | 单元、真实SQLite集成和真实浏览器合成端到端测试 |
| `contracts` / `database/migrations` | 当前API合同、接口清单、版本化SQL |
| `docs/course` | 本轮需求追踪、测试记录、配置与答辩材料 |
| `docs/technical/design` | Starter保留的阶段三设计历史 |
| `.local` | 本机私有数据/缓存/一次性启动信息；禁止打包或提交 |

根目录旧 `DEVELOPMENT_HANDOFF`、`HANDOFF_*`、`TEAM_*` 以及阶段四报告为Starter历史交接材料，描述的是当时状态；本轮交付以本README及`docs/course`为准。原始v1SQL和历史设计文档未覆盖重写。

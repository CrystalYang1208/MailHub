# 阶段四工程建设报告

交付日期：2026-11-16（Asia/Shanghai）。状态：工程实施完成，提交负责人审核；保留NuGet安全审计网络限制，未进入阶段五。本文只报告新工程运行，不重跑阶段二邮箱验证，不把目标Specs视为业务已完成。

## 1. 工程结果

创建Vue3.5/TypeScript/Element Plus按需组件工程、.NET10四层解决方案与测试工程、版本迁移、批准合同副本、统一脚本、协作规范和CI。完整入口和目录说明在根README。

- Host：DI、仅127.0.0.1动态监听、同源静态资源、一次性引导码、内存令牌、Origin/Host/Sec-Fetch-Site校验、脱敏错误和健康检查。
- Application：合成数据端口和DTO；Domain：程序集边界；Infrastructure：Persistence/Protocols/Security适配边界。没有实现邮箱业务Repository、协议连接、用户认证、同步或编辑器。
- 工程接口 /_engineering/* 与正式 /api/v1 合同分离。正式业务实现清单为空，受保护业务占位返回503明确“尚未开发”，未更改批准API适配Demo。
- 开发/测试数据在.local，example.invalid合成账号；真实profile不会被工程模式自动接管。目录内已有业务表而无迁移账本会被拒绝。

## 2. 环境、来源与锁定

| 项目 | 实际/固定版本 | 来源及锁定 |
|---|---|---|
| OS | Windows11 build26200 x64 | 本机；CR-08，不做Win10 |
| .NET SDK / Runtime | 10.0.404 / 10.0.15 | 工作区既有Microsoft SDK；global.json |
| Node / npm / Git | 24.20.0 / 11.19.0 / 2.33.1 | 本机已有工具；packageManager与engines |
| Vue / TS / Element Plus | 3.5.45 / 5.9.3 / 2.11.8 | npm官方源；根package-lock |
| Vite / Vue plugin | 7.3.8 / 6.0.2 | npm官方源；兼容补丁及锁文件 |
| Vue Router / vue-tsc | 4.5.1 / 3.1.4 | npm官方源及锁文件 |
| ESLint / Prettier / Vitest | 9.39.5 / 3.6.2 / 4.1.12 | 开发检查工具；定向漏洞补丁 |
| OpenSpec / Playwright | 2.0.0 / 1.56.1 | 项目npm依赖，精确锁定 |
| Redocly / openapi-typescript | 2.78.0 / 7.10.1 | 契约工具，精确锁定 |
| MailKit / MimeKit | 4.18.2 / 4.18.2 | NuGet中心管理与5个项目锁文件 |
| Microsoft.Data.Sqlite | 10.0.15 | NuGet包，不代替native版本证明 |
| SQLitePCLRaw.lib.e_sqlite3 | 2.1.13 | 锁定的实际native包 |
| **实际SQLite引擎** | **3.53.3** | .NET进程SELECT sqlite_version()，迁移测试PASS |
| ProtectedData | 10.0.0 | 固定依赖，工程不重新验证DPAPI |
| Chrome / Edge | 157.0.8103.78 / 157.0.4341.60 | 本机已安装浏览器实际运行 |

SQLite 3.53.3满足批准ADR中的WAL修复底线；MigrationRunner每次启动先查实际版本，不达底线拒绝迁移。V0001业务SQL与阶段三原件按字节相同，新增SchemaMigration只作技术账本；DDL/外键检查/版本记录同事务，无ORM自动覆盖。

默认NuGet源保留nuget.org。该源及官方漏洞源在本机TLS连接失败；实际构建使用可选Huawei公开镜像，signatureValidationMode=require，固定Microsoft公布的NuGet.org仓库签名证书，HTTPS/TLS校验未关闭。首次不完整还原导致Host锁图NU1102，重新评估并恢复完整包图后已解决；最终locked-mode还原实际通过。

可核验来源：[SQLite WAL说明](https://sqlite.org/wal.html)、[NuGet配置](https://learn.microsoft.com/en-us/nuget/reference/nuget-config-file)、[Microsoft仓库签名轮换公告](https://devblogs.microsoft.com/dotnet/the-nuget-org-repository-signing-certificate-will-be-updated-as-soon-as-april-8th-2024/)。不因镜像可下载而声称漏洞/吊销服务已正常。

## 3. 实际质量结果

完整命令从MailHub运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-Quality.ps1 -Mirror -BuildOnlyRestore
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Start-Engineering.ps1 -NoBuild
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Stop-Engineering.ps1 -ProcessFile <本次私有process.json>
```

| 检查 | 实际结果 | 本机证据 |
|---|---|---|
| npm ci --ignore-scripts | PASS，干净依赖安装 | .local/stage4/quality-run.log |
| 类型、ESLint、Prettier | PASS，0 lint错误/警告 | 同上 |
| Vitest单元测试 | PASS，3/3 | 同上 |
| Vue生产构建 | PASS；JS约119.5KB/gzip46.2KB，CSS约23.9KB/gzip3.85KB | 同上 |
| OpenAPI3.1 lint | PASS，有2个规则提示，见下 | 同上 |
| 原件/引用/类型/追踪检查 | PASS，51operations、44schemas、35FR | .local/stage4/contracts.json |
| OpenSpec严格校验 | PASS，8specs+1change，9/9 | 同上质量日志 |
| NuGet locked-mode还原 | PASS（明确BuildOnlyRestore） | 同上 |
| dotnet format whitespace与.NET analyzers构建 | PASS，0编译警告/错误 | 同上 |
| 后端迁移及令牌/Origin/Host测试 | PASS，2/2 | .local/stage4/tests/foundation.trx |
| 实际SQLite版本 | PASS，3.53.3、WAL门槛及迁移重复执行 | .local/stage4/sqlite-runtime.json |
| Chrome工程联通 | PASS：页面、SQLite、引导码清除、无令牌401、错误信封 | .local/stage4/browser-chrome.json |
| Edge工程联通 | PASS，同上 | .local/stage4/browser-edge.json |
| 统一启动、健康检查、按身份停止进程 | PASS，无真实凭据 | .local/stage4/launcher.json |
| npm audit | PASS，最终0条已知漏洞（含开发依赖） | .local/stage4/npm-audit.json |
| NuGet官方漏洞审计 | BLOCKED：TLS EOF，NU1900/NU1905 | .local/stage4/dotnet-restore.signed.log |
| NuGet仓库签名核验 | PASS：MailKit4.18.2签名及内容哈希，固定NuGet.org Microsoft证书；不是全依赖漏洞审计 | .local/stage4/nuget-signature-check.log |
| 远程CI | NOT_RUN，无远程仓库 | .github/workflows/engineering.yml |
| 历史保护 | PASS，207个原有文件SHA256未改变 | contracts.json及preexisting-files.json |

所有新工程测试使用合成数据。Chrome/Edge是真实安装浏览器的本机自动测试，不是模拟浏览器；阶段二真实邮件证据不复制入CI。单元/工程测试通过不等于产品AC01-14通过。

OpenAPI两处Redocly提示在not.required（secret/oauthFlowId）局部Schema中，字段定义位于父Schema，是批准的互斥表达；校验结构/引用仍通过。保留批准合同，不为消除提示改写。后端错误测试核对ok/data/error/requestId以及error必需字段；新增业务端点必须另外实现完整请求/响应Schema用例。

初始npm审计发现27项（含high/critical），据实际建议定向升级Vue3.5补丁、Vite7补丁、Redocly、yaml、Vitest并落实typescript-estree的minimatch11.0.1限定覆盖；最终npm ci之后重复审计0条，lint/类型/测试/构建均通过。未运行audit fix --force。ESLint9已显示维护终止提示，后续工具升级走独立工程变更，不更换业务栈。

## 4. OpenSpec建设

在正确MailHub根目录实际运行官方2.0.0 init --tools codex --language zh-CN，生成6项Codex技能，保留父工作区配置。8个主Spec描述已批准目标，35FR维持24Must/11Should，14NFR、10BR及AC01-14/CR01-08引用批准设计和决定。

当前实施engineering-foundation六项任务经apply指令逐项复核已完成（6/6、state=all_done），未归档；它是工程变更，skip_specs=true，不声明业务行为改变；proposal/design/tasks均已读取并由apply指令管理。七个业务功能切片保留PLANNED及未勾选任务，不提前开发。使用方式见OPENSPEC_WORKFLOW；根目录npx openspec instructions apply --change engineering-foundation --json提供实际任务进度。

## 5. Git与协作

在MailHub新建main本地仓库，未重新初始化父目录，未创建远程/提交/推送/发布。排除凭据、数据库、.local、缓存、eml、附件、截图和私有历史证据；git check-ignore验证关键路径，暂存区为空。

Git工作流为main+短期feature/fix/docs分支，Conventional Commits关联Change/FR/Task；公共API/DDL先审核，负责人审核后合并。CONTRIBUTING、PR模板、CODE_REVIEW齐全。GitHub Actions仅配置文件，使用同一本地脚本（默认官方源审计，若NU1900/NU1905将明确阻止门禁，不静默当作安全通过）；无托管权限，不宣称云端CI运行过。

## 6. 问题、修复与剩余工作

本轮已修复：首次静态文件被JSON回退路由遮蔽，改为明确中间件路由顺序后双浏览器通过；NuGet不完整锁图重新评估后锁定还原通过；Windows测试临时目录权限冲突用进程级项目临时目录解决；依赖审计的兼容补丁已实际落地并验证。

| 时点 | 尚需事项 |
|---|---|
| 阶段五启动前 | 负责人审核工程与NuGet审计阻塞风险；恢复官方安全数据源查询或正式接受短期限制。禁止将BuildOnlyRestore变成默认安全结论 |
| 相应功能开发 | 用户密码/密钥封套和会话隔离；协议、同步、Outbox、检索等批准业务；逐端点Schema测试；单写者/检查点/备份策略落实 |
| 集成/发布前 | 全产品AC/NFR与真实邮箱受控联调；安装包和长期稳定性；官方漏洞/吊销数据可达后的完整复查；远程CI与分支保护仅在授权后启用 |

本次完整质量运行后，仅为加强脚本对默认NuGet审计不可达的拒绝和独立测试目录，再执行-SkipInstall -SkipBrowser复核；保留此前已通过且未变化的浏览器记录。证据quality-final-script.log。OpenSpec最终进度见apply-after-3.2.json。

工程具备按统一边界并行开发的基础。阶段四工程工作已完成、待审核；NuGet审计为明确BLOCKED而非无风险。是否接受此限制并启动阶段五由技术负责人决定，本次不自动开始业务开发或归档。

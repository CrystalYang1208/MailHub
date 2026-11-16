> 阶段四Starter历史快照：本页保留原交接背景。本轮业务实现与当前验证状态请看README.md及docs/course/验收记录.md，不以本页的“尚未开发/待分配”判断当前交付。

# MailHub 首次开发交接指南

2026-11-16。阶段四由技术负责人有条件验收，允许课程开发协作；NuGet官方漏洞数据查询暂不可达已接受为短期限制，正式发布前必须补查，不能宣称安全审计通过。本次不是合并，不启动阶段五业务实现。

**当前工程是已验收骨架，正式邮箱业务尚未实现。** 工程页只读取合成SQLite数据；不提供真实账号登录、同步、编辑器或用户认证。不能把工程令牌替代正式应用用户会话。用户数据不上传云服务器、不接云端AI，首版只支持Windows11。

## 1. 从哪里开始

先读本文件→MODULE_OWNERSHIP→本组TEAM_A_TASKS或TEAM_B_TASKS→UNASSIGNED_REQUIREMENTS→INTEGRATION_CHECKLIST。然后阅读docs/requirements/BASELINE_DECISIONS、docs/technical/design/MODULE_DESIGN、API_DESIGN、DATABASE_DESIGN、SECURITY_DESIGN及本模块专项。全部35FR与优先级保留于baseline-input.json；原Word及真实验证材料由负责人本机保管，未混入开发包，原件哈希可追溯。历史设计生成器引用原Word，不是日常开发校验入口，不应重跑重写批准设计。

阶段三整体设计/ADR0004-0011/AC13-14已批准。原文件保留交付时历史“待审”措辞，当前批准状态见BASELINE_DECISIONS和STAGE4_ACCEPTANCE。回复关联实际收到原邮件；转发独立带引用；一个活跃内部用户；锁定暂停同步；F03保留账号草稿并暂停补拉。

## 2. 环境与固定版本

Windows11 x64、Chrome/Edge。安装Node24.20.0、npm11.19.0、.NET SDK10.0.404（含所需运行时），Git。来源使用官方Node/.NET/Git网站；不要安装不明发行包或关闭TLS。Python仅供历史文档生成工具，不是运行/构建必需依赖。

| 工具/依赖 | 固定版本 |
|---|---|
| Vue / TypeScript / Element Plus | 3.5.45 / 5.9.3 / 2.11.8 |
| Vite / Vue Router / vue-tsc | 7.3.8 / 4.5.1 / 3.1.4 |
| .NET SDK / Runtime | 10.0.404 / 10.0.15 |
| MailKit / MimeKit / Microsoft.Data.Sqlite | 4.18.2 / 4.18.2 / 10.0.15 |
| SQLite native包 / 已测实际引擎 | SQLitePCLRaw.lib.e_sqlite3 2.1.13 / 3.53.3 |
| DPAPI ProtectedData | 10.0.0 |
| OpenSpec / Vitest / Playwright | 2.0.0 / 4.1.12 / 1.56.1 |
| ESLint / Prettier | 9.39.5 / 3.6.2 |

其余精确依赖见package-lock和backend/Directory.Packages.props及5个packages.lock.json。不要运行npm update、audit fix --force或随意改锁文件。前端根npm workspace统一锁定，不在frontend另建锁。SQLite以运行时查询为准，未满足ADR修复底线会被拒绝启用WAL。

## 3. 解压与安装

解压到自己的目录，例如D:\Projects\MailHub，以下命令在包含package.json/global.json的MailHub根目录执行。路径可含中文/空格；不存在必须与原作者一致的盘符或用户名。

```powershell
node --version
npm --version
dotnet --info
git --version
npm ci --ignore-scripts
dotnet restore backend/MailHub.slnx --locked-mode --configfile backend/NuGet.Config
```

SDK在PATH中；也可在当前终端设置$env:MAILHUB_DOTNET为自己dotnet.exe的路径后使用工具脚本。脚本默认把CLI用户目录和NuGet缓存放在.local，包中不带任何机器缓存。不要把作者的缓存路径填入自己的配置。

如果官方NuGet TLS或安全数据源失败，可按已批准短期限制执行：

```powershell
# 可选签名校验的公开镜像；保持锁定、TLS及Microsoft仓库签名验证
# 这是构建还原，不是安全审计通过：
dotnet restore backend/MailHub.slnx --locked-mode --configfile backend/NuGet.Mirror.Config -p:NuGetAudit=false
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-Quality.ps1 -Mirror -BuildOnlyRestore
```

不允许为了连接成功关闭证书校验、随意换代理节点或修改全局代理。镜像/官方源都不可达时记录BLOCKED，与负责人协调网络，不改依赖版本“凑通过”。

## 4. 构建、数据库与运行

```powershell
npm run build
dotnet build backend/MailHub.slnx --no-restore
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Start-Engineering.ps1 -NoBuild -Open
```

浏览器点击“检查工程联通”，应显示成功、SQLite版本及“SQLite 合成数据读取成功”。后台动态绑定127.0.0.1，提供dist与API同源。新开发库.local/development在首次启动自动迁移：V0001批准DDL→外键检查→SchemaMigration哈希与版本记录，重复启动不重复建表。无须先手工执行SQL，不用ORM自动建表。开发数据库、单测数据库与浏览器测试数据库相互独立；不使用真实邮箱内容。

临时启动URL只在.local/runs/<id>/startup.json的url字段，不发聊天/不提交。引导码3分钟单次有效，页面消费后清除fragment；刷新旧页面失效应重启获取新链接。用同目录process.json停止：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Stop-Engineering.ps1 -ProcessFile .local/runs/<id>/process.json
```

脚本检查PID可执行路径及MailHub.Host.dll后才停止，不清理其他程序。不要杀死所有dotnet进程。测试库/日志只在自己.local内，不随模块交付。

前端UI/HMR单独运行npm run dev --workspace frontend（127.0.0.1:5173）；这是UI开发，不能完成后台API联通。集成时重新npm run build并使用Host同源。没有为了方便而开放CORS/取消Origin校验的开发捷径。

## 5. 目录与模块边界

frontend/src为Vue页面与API客户端；backend/src分Host、Application、Domain、Infrastructure；后者内有Protocols/Security/Persistence边界；backend/tests为工程测试。contracts为批准OpenAPI；database/migrations为catalog/profile的V0001；openspec含8规范与工程变更；.agents/skills是Codex集成；docs/technical/design是完整批准设计和8ADR；tools为启动/质量入口；.github为PR模板和未来CI。

Host只做HTTP、认证和DI，不写协议/SQL；Application依赖端口，Domain不依赖框架适配器；协议库、DPAPI、SQLite在Infrastructure。实际业务目录按本组任务创建，不能另外搭一套后端工程。

## 6. OpenAPI与OpenSpec

contracts/openapi.yaml是51个业务operationId的合同，正式API基址/api/v1；不要把/_engineering/*当成产品端点。业务未实现，implementedProductOperations为空。错误信封ok/data/error/requestId、中文message/action不变；除合同声明的例外，进程令牌与内部用户会话均须校验。

```powershell
npm run contracts:lint
npm run contracts:check
# 只有批准契约变更后才更新生成物：
npm run contracts:types
$env:OPENSPEC_TELEMETRY='0'
npx openspec list --json
npx openspec validate --all --strict --no-interactive
npx openspec new change <范围明确的变更名>
npx openspec status --change <变更名> --json
npx openspec instructions proposal --change <变更名> --json
# 按status顺序生成design/specs/tasks；完成规划后：
npx openspec instructions apply --change <变更名> --json
```

不重新openspec init，不重复npm create/dotnet new生成工程。Codex可请求“使用openspec-propose为<name>创建本组范围的规划”，再用openspec-apply-change实现。读技能和CLI返回的上下文；主Specs是批准行为，changes是实施过程；按实际测试勾任务。工程变更6/6不能用来代表邮箱业务完成。未经审核不归档或修改主规范。

需求→Spec→计划Change→Design→Task→operationId→测试映射见docs/collaboration/REQUIREMENT_SPEC_TRACE，本次所有权由MODULE_OWNERSHIP细化，历史切片名称不代表所有功能已分配。

## 7. 分支、提交和公共文件

ZIP刻意不带.git及作者Git配置。收到包后先保存原始ZIP和校验值。若负责人已提供共同远程/基线，优先clone该基线，不各自创建重复历史；目前尚无远程。需要立即本地开发时，可在这个全新的解压副本首次建立个人记录（不是重建工程）：

```powershell
git init -b main
# 自己在本仓库配置姓名/邮箱；不要复制作者身份或修改全局设置
git add -- README.md CONTRIBUTING.md package.json package-lock.json global.json .editorconfig .gitignore .npmrc redocly.yaml frontend backend contracts database openspec tools .github .agents docs
# 检查暂存文件；只提交包内已审查基线，.local等必须被忽略
git add -- DEVELOPMENT_HANDOFF.md MODULE_OWNERSHIP.md TEAM_A_TASKS.md TEAM_B_TASKS.md UNASSIGNED_REQUIREMENTS.md INTEGRATION_CHECKLIST.md HANDOFF_VALIDATION_REPORT.md HANDOFF_FILE_MANIFEST.json
git diff --cached --stat
git commit -m "chore: import approved MailHub starter baseline"
git switch -c feature/team-a-account-access
# B使用自己的feature/team-b-compose-outbox等
```

记录此基线commit及原包SHA256，然后再编辑代码。每个PR范围对应一个Change；Conventional Commits正文附FR/Task/Change。无远程时不要自行创建公开仓库或推送；模块交付可提交基于个人基线的git diff --binary <baseline>，后续统一远程后由负责人协调导入，不假设所有个人基线commit相同。

不可擅改：global.json、依赖锁/中心版本、contracts/openapi、V0001/批准DDL、批准ADR/主Specs、安全认证/公共UserContext、公共DTO、共享API客户端和Host的全局安全/DI入口。需要改时提出最小设计变更及影响清单，经负责人批准后一起更新合同、类型、测试和迁移。不要为解决联调临时加JSON字段。

## 8. 自测与模块交付

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-Quality.ps1
# 当前获准短期受限网络环境使用上文-Mirror -BuildOnlyRestore；仍不得报NuGet审计通过
```

需安装Chrome和Edge；浏览器自测仅运行本机合成页，不登录真实邮箱。没有其中浏览器可临时-SkipBrowser，但交付记录标NOT_TESTED。模块单测覆盖本组正常/失败、会话撤销与事务边界；详见TEAM任务及INTEGRATION_CHECKLIST。不要重复阶段二已通过且未受修改影响的真实协议/性能测试。

提交源码/Change及任务、关联FR与operationId、模块单元测试、脱敏模块自测记录、必要契约/迁移批准记录、依赖变化说明和集成注意事项。不要提交node_modules/bin/obj、.local、真实配置、Token、原始邮件或截图日志。

文件交还技术负责人，由其建立集成分支、审查合同/差异、合并后整体联调和验收；本包不自动发送给任何组员、不创建远程、不发布。7条未分配需求及共享底座必须在相应集成前明确实施人员，不能默认归技术负责人。

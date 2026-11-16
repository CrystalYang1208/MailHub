> 阶段四Starter历史快照：本页保留原交接背景。本轮业务实现与当前验证状态请看README.md及docs/course/验收记录.md，不以本页的“尚未开发/待分配”判断当前交付。

# 首次开发交接验证报告

日期：2026-11-16（Asia/Shanghai）。结论：**开发Starter可交付；仅工程骨架，不是邮箱业务成品。** 阶段四有条件验收决定已记录。NuGet官方漏洞审计仍为BLOCKED，负责人已接受课程开发短期限制，正式发布前必须完成补充审计。

## 范围与最终封包方式

使用实际MailHub源码进行白名单打包，156个文件（含29个源码/项目/执行脚本类文件），不是文档包。保留Vue、分层.NET、测试、SQL迁移、批准OpenAPI、8个Specs、6项Codex技能、8ADR与全部49个阶段三设计文件、锁文件、脚本及Git/CI模板。

开发ZIP不含.git、原始Word、validation或历史私有证据。Word对应的35FR/14NFR/BR/原AC/CR逐行基线与原件哈希保留在design/baseline-input.json；批准补充与决定在BASELINE_DECISIONS。不需要原作者私人文件来构建运行工程。原工作区材料没有被删除或清理。

流程：候选ZIP解压到全新目录，空白npm/NuGet/CLI用户缓存运行完整质量脚本；通过后将本报告替换占位记录并重新封包。最终ZIP再次解压到另一个新目录，核对全部清单与已测试源码逐字节相同，再执行安装、锁定还原、构建与基础检查。报告及manifest自身不能记录所在ZIP的最终自引用哈希，最终ZIP大小/SHA256以包外PACKAGE_INFO.json、SHA256SUMS.txt为准。

## 独立验证环境与步骤

本机Windows11 build26200 x64；Node24.20.0/npm11.19.0，.NET SDK10.0.404/Runtime10.0.15，Git2.33.1。是同一电脑的新解压路径及空白缓存，不冒充第二台机器测试。SDK通过PATH提供，未复制原工程node_modules/bin/obj或NuGet包缓存；ZIP不包含SDK或依赖包，开发者需安装工具并联网还原。

```powershell
# 在全新解压的MailHub目录，SDK已加入PATH
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-Quality.ps1 -Mirror -BuildOnlyRestore
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Start-Engineering.ps1 -NoBuild
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Stop-Engineering.ps1 -ProcessFile <该次私有process.json>
```

使用已批准的签名校验镜像和locked-mode；BuildOnlyRestore仅规避已接受的官方安全数据查询阻塞，不关闭TLS、签名或更换锁定版本。没有真实邮箱登录、OAuth或收发操作。

| 验证 | 预期 | 实际结果 | 状态 |
|---|---|---|---|
| ZIP CRC/路径 | 无损坏/路径穿越 | CRC通过，条目均在MailHub/内 | PASS |
| 文件完整性 | 可修改源码及全部配置齐全 | 156文件、前后端/SQL/契约/规范/锁/测试/脚本俱全 | PASS |
| npm ci | 从锁文件创建依赖 | 新目录空白npm缓存安装305包 | PASS |
| 前端类型/lint/格式 | 无错误 | 全部通过 | PASS |
| 前端单测/生产构建 | 3例通过、dist生成 | 3/3通过，Vite构建成功 | PASS |
| NuGet还原 | 锁定包从源还原 | 新缓存24包目录，5项目还原成功 | PASS |
| 后端格式/分析器/构建 | 无编译警告和错误 | 成功，0编译警告/错误 | PASS |
| SQLite版本/迁移 | WAL修复版本、事务版本迁移幂等 | 实际3.53.3；catalog/profile创建和profile重复迁移、外键/索引通过 | PASS |
| 后端测试 | 迁移/API授权边界 | 2/2通过 | PASS |
| OpenAPI | 3.1、引用、唯一ID、生成类型一致 | 51operationId、44schemas通过；2个既有lint提示保留 | PASS |
| OpenSpec | 主规范和工程变更可读且严格校验 | 8specs+1change，9/9 | PASS |
| Chrome联通 | UI显示SQLite数据、无令牌拒绝 | 实际Chrome浏览器通过 | PASS |
| Edge联通 | 同上 | 实际Edge浏览器通过 | PASS |
| 约定启动/停止脚本 | 启动、健康检查、限定PID停止 | 新解压副本实际通过 | PASS |
| npm审计 | 实际查询结果 | 0条已知漏洞 | PASS |
| NuGet官方漏洞审计 | 安全数据查询 | 官方源连接受限；按负责人决定暂缓 | BLOCKED（短期接受） |
| 个人绝对路径依赖 | 代码不引用作者盘符/用户目录/旁置SDK | 29源码/项目/脚本类文件扫描通过，PATH SDK在新路径运行成功 | PASS |
| 需求分配 | 35条且保留优先级 | A23、B5、待分配7；24Must/11Should | PASS |
| 批准设计保护 | 架构/ADR/DDL/API/规范不变 | 49个设计文件原SHA未变；DDL/API精确副本，Specs未改 | PASS |
| 敏感文件排除 | 不含真实凭据/用户数据/私有材料 | 白名单+文件扩展/目录排除+凭据值/非合成邮箱/个人路径扫描通过 | PASS |

Chrome/Edge版本以本机安装版本157.0.8103.78/157.0.4341.60记录。没有第二台电脑、远程CI、真实业务模块的验证主张。模板中的client_secret字段、公开证书指纹、example.invalid合成账号不是秘密值；真实Google客户端配置不在包内。静态扫描不是不存在任何可能秘密的数学证明，本包全部为审查过的文本源码/模板。

## 验证中发现并修复

1. 删除测试入口和环境脚本对作者旁置.validation-dotnet的默认依赖，改为PATH或显式MAILHUB_DOTNET，保持批准SDK/依赖版本。
2. 第一候选ZIP在格式校验失败：便携SDK入口改动后需要Prettier格式化，已修复并重新封包。
3. 第二候选ZIP在Windows PowerShell5.1首次SDK提示写stderr时过早退出：仅对native输出收集恢复Continue，并以实际进程退出码和NU1900/1905检测判定成功/失败；未吞掉真实还原错误。第三候选完整冷启动验证通过。

这是交接可移植性修复，没有重建工程、变更架构、修改已批准Schema或开发邮箱业务。失败候选和本机原始日志保存在独立暂存区，不混入交付包。

## 证据与剩余事项

本机证据位于独立暂存区20261116-third/verify-3/MailHub/.local：handoff/independent-quality.log、handoff/launcher-result.json、stage4/browser-chrome.json、browser-edge.json、backend-data/sqlite-runtime.json、tests/foundation.trx、contracts.json及npm-audit.json。静态记录为暂存区static-validation.json。最终封包复核记录final-verification.json只含脱敏结果，最终源码与已验证载荷按哈希一致。

- 手动：安装固定工具及Chrome/Edge、解压、联网安装依赖、选择本组任务与分支；首次合成页无需任何邮箱凭据或OAuth项目。
- 技术负责人：7条未分配需求中5条Must及共享Security/Storage底座仍需明确人员；这不默认分给负责人，不阻止交付工程包，但相应真实功能集成前必须安排。
- 正式发布前：恢复NuGet官方安全数据源并完成补充审计。ESLint9的维护提示保留，后续工具维护单独变更。
- 未创建远程仓库、未推送、未发送文件给其他成员、未开始阶段五业务开发。当前等待负责人正式发包。

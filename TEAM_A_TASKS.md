> 阶段四Starter历史快照：本页保留原交接背景。本轮业务实现与当前验证状态请看README.md及docs/course/验收记录.md，不以本页的“尚未开发/待分配”判断当前交付。

# 开发组A：邮箱接入与收件

状态：待开发；本文是首次交接任务，不是测试通过记录。先读DEVELOPMENT_HANDOFF和MODULE_OWNERSHIP。

建议按Account→Sync→Read分PR，不做一个覆盖全部23条的大提交。前后端均由A完成：frontend/src/features/account|sync|mail，backend/src各层对应Account/Sync/Mail目录，协议实现放Infrastructure/Protocols。不要新建微服务或把协议/SQL塞入Host。

建议待创建Change：team-a-account-access、team-a-durable-sync、team-a-mail-reading。它们是建议名称，当前未创建或启动。Spec分别引用account-management、mail-sync、mail-reading，设计引用EMAIL_INTEGRATION、SYNC_ENGINE、MODULE、SEARCH_STORAGE及SECURITY专项。

顺序：先合成协议适配器与账号配置/状态接口，再实现UID批次与持久任务，再接列表/正文页面。首轮以Must为主，Should保留原优先级。统一列表按UTC收信时间和稳定游标排序；BODY.PEEK不得意外改已读。

## T-FR-A01 [Must] 支持通用 IMAP 接入：手工填写服务器地址、端口、加密方式与认证方式

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：listProviders, validateAccount, addAccount；公共端点的认证约束不可放宽。
- [ ] 模块自测：模板外邮箱也能完成接入；测试编号T-FR-A01，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-A02 [Must] 内置常见邮箱模板（QQ、163、Gmail），选中后自动填充服务器参数

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：listProviders, validateAccount, listAccounts；公共端点的认证约束不可放宽。
- [ ] 模块自测：模板选中后地址端口自动填好；测试编号T-FR-A02，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-A03 [Must] Gmail 模板优先走 OAuth 2.0，兼容应用专用密码

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：listProviders, importOAuthClient, startOAuth, getOAuthFlow, validateAccount；公共端点的认证约束不可放宽。
- [ ] 模块自测：两种方式均可完成接入；测试编号T-FR-A03，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-A04 [Must] 添加账号时展示分步授权指引（开启 IMAP、生成授权码、服务器参数核对）

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：listProviders, validateAccount；公共端点的认证约束不可放宽。
- [ ] 模块自测：每步有可执行说明，不需查阅外部文档；测试编号T-FR-A04，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-A05 [Must] 校验凭据有效性，失败时区分授权失败、未开启 IMAP、服务器地址或端口错误、网络不可达

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：validateAccount；公共端点的认证约束不可放宽。
- [ ] 模块自测：四类失败提示文案不同且可操作；测试编号T-FR-A05，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-A06 [Must] 凭据加密存储于本机，界面与日志不出现明文

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：validateAccount, addAccount, getAccount；公共端点的认证约束不可放宽。
- [ ] 模块自测：抓取界面与日志均无明文；测试编号T-FR-A06，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-A07 [Must] 支持重新授权（令牌过期或授权码变更）

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：startOAuth, getOAuthFlow, reauthorizeAccount；公共端点的认证约束不可放宽。
- [ ] 模块自测：重新授权后恢复同步，不需重建账号；测试编号T-FR-A07，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-A08 [Must] 支持移除账号并清除该账号本地数据

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：removeAccount, getOperation；公共端点的认证约束不可放宽。
- [ ] 模块自测：移除后本地库无该账号任何记录；测试编号T-FR-A08，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-A09 [Should] 支持账号启用/停用，停用保留配置但不参与同步

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：setAccountEnabled；公共端点的认证约束不可放宽。
- [ ] 模块自测：停用后不再产生同步请求；测试编号T-FR-A09，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-B01 [Must] 首次接入按最近 90 天做增量同步

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：requestSync；公共端点的认证约束不可放宽。
- [ ] 模块自测：同步条数与服务器端一致；测试编号T-FR-B01，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-B02 [Must] 基于 UID 与 UIDVALIDITY 做增量拉取与断点续传

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：requestSync；公共端点的认证约束不可放宽。
- [ ] 模块自测：中断后恢复不重复不遗漏；测试编号T-FR-B02，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-B03 [Must] 支持手动同步（单账号或全部账号）

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：requestSync, listSyncJobs, retrySyncJob；公共端点的认证约束不可放宽。
- [ ] 模块自测：点击后 5 秒内有状态反馈；测试编号T-FR-B03，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-B04 [Should] 支持定时自动同步，默认 10 分钟，可配置 5～60 分钟

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：requestSync, getSettings, updateSettings；公共端点的认证约束不可放宽。
- [ ] 模块自测：按设定间隔自动触发；测试编号T-FR-B04，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-B05 [Must] 同步失败进入待重试队列，按指数退避重试

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：requestSync, listSyncJobs, getSyncJob, retrySyncJob；公共端点的认证约束不可放宽。
- [ ] 模块自测：连续失败按 1/2/4/8 分钟间隔重试；测试编号T-FR-B05，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-B06 [Must] UIDVALIDITY 变化时重建该文件夹索引并全量重拉

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：requestSync；公共端点的认证约束不可放宽。
- [ ] 模块自测：版本变化后不出现错位邮件；测试编号T-FR-B06，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-B07 [Should] 同步过程展示进度与结果，失败给出原因

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：listSyncJobs, getSyncJob；公共端点的认证约束不可放宽。
- [ ] 模块自测：有进度与最终结果文案；测试编号T-FR-B07，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-B08 [Should] 控制并发，单账号同步并发不超过 3，全局不超过 5

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：listSyncJobs；公共端点的认证约束不可放宽。
- [ ] 模块自测：并发计数可观测，不触发限流；测试编号T-FR-B08，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-C01 [Must] 统一收件箱跨账号聚合，按收信时间倒序

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：listMessages；公共端点的认证约束不可放宽。
- [ ] 模块自测：3 个账号邮件混合正确排序；测试编号T-FR-C01，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-C02 [Must] 邮件列表显示来源账号标识

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：listAccounts, listMessages；公共端点的认证约束不可放宽。
- [ ] 模块自测：每条记录可见账号标签；测试编号T-FR-C02，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-C03 [Must] 阅读邮件正文，支持 HTML 与纯文本，默认不加载外部图片

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：getMessage, getMessageBody；公共端点的认证约束不可放宽。
- [ ] 模块自测：默认状态下不发外部图片请求；测试编号T-FR-C03，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-C04 [Should] 下载附件到本地指定目录

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：createDownloadBatch, getDownloadBatch, getAttachmentContent；公共端点的认证约束不可放宽。
- [ ] 模块自测：下载后文件可正常打开；测试编号T-FR-C04，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-C07 [Should] 按账号与文件夹切换视图

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：listFolders, listMessages；公共端点的认证约束不可放宽。
- [ ] 模块自测：切换后列表内容与文件夹一致；测试编号T-FR-C07，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-C08 [Should] 展示各文件夹未读数

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：listFolders；公共端点的认证约束不可放宽。
- [ ] 模块自测：未读数与服务器一致；测试编号T-FR-C08，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## 必测失败边界

认证冷却与重授权、超时/断网、UIDVALIDITY代次失效、批次回滚与游标/索引同事务、重复重放幂等、锁定/切换用户后旧worker撤销、服务端删除/FLAGS对账、HTML脚本与外部请求阻断。按设计用可控时钟验证1/2/4/8分钟，测试串行使用一个IMAP client命令通道；不重复阶段二已通过的全供应商互发。

提交与联调标准见INTEGRATION_CHECKLIST；未分配的共享依赖见UNASSIGNED_REQUIREMENTS，不能临时更改合同绕过。

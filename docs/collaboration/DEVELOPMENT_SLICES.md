# 后续业务开发切片

阶段四不执行下列业务任务。阶段五获批准后先用OpenSpec创建对应Change，再按本表和设计细化proposal/design/tasks及必要delta，不为每条FR创建Change。先共享身份/存储边界，再业务模块并行；公共数据库/API变更必须先评审。

## implement-account-access
状态：PLANNED，尚未创建Change目录/实施。设计：docs/technical/design/EMAIL_INTEGRATION_DESIGN.md；代码目标：backend/src及frontend/src对应模块目录。

- [ ] T-FR-A01 [Must] 支持通用 IMAP 接入：手工填写服务器地址、端口、加密方式与认证方式；验证T-FR-A01与关联AC，先更新契约后实现。
- [ ] T-FR-A02 [Must] 内置常见邮箱模板（QQ、163、Gmail），选中后自动填充服务器参数；验证T-FR-A02与关联AC，先更新契约后实现。
- [ ] T-FR-A03 [Must] Gmail 模板优先走 OAuth 2.0，兼容应用专用密码；验证T-FR-A03与关联AC，先更新契约后实现。
- [ ] T-FR-A04 [Must] 添加账号时展示分步授权指引（开启 IMAP、生成授权码、服务器参数核对）；验证T-FR-A04与关联AC，先更新契约后实现。
- [ ] T-FR-A05 [Must] 校验凭据有效性，失败时区分授权失败、未开启 IMAP、服务器地址或端口错误、网络不可达；验证T-FR-A05与关联AC，先更新契约后实现。
- [ ] T-FR-A06 [Must] 凭据加密存储于本机，界面与日志不出现明文；验证T-FR-A06与关联AC，先更新契约后实现。
- [ ] T-FR-A07 [Must] 支持重新授权（令牌过期或授权码变更）；验证T-FR-A07与关联AC，先更新契约后实现。
- [ ] T-FR-A08 [Must] 支持移除账号并清除该账号本地数据；验证T-FR-A08与关联AC，先更新契约后实现。
- [ ] T-FR-A09 [Should] 支持账号启用/停用，停用保留配置但不参与同步；验证T-FR-A09与关联AC，先更新契约后实现。

## implement-durable-mail-sync
状态：PLANNED，尚未创建Change目录/实施。设计：docs/technical/design/SYNC_ENGINE_DESIGN.md；代码目标：backend/src及frontend/src对应模块目录。

- [ ] T-FR-B01 [Must] 首次接入按最近 90 天做增量同步；验证T-FR-B01与关联AC，先更新契约后实现。
- [ ] T-FR-B02 [Must] 基于 UID 与 UIDVALIDITY 做增量拉取与断点续传；验证T-FR-B02与关联AC，先更新契约后实现。
- [ ] T-FR-B03 [Must] 支持手动同步（单账号或全部账号）；验证T-FR-B03与关联AC，先更新契约后实现。
- [ ] T-FR-B04 [Should] 支持定时自动同步，默认 10 分钟，可配置 5～60 分钟；验证T-FR-B04与关联AC，先更新契约后实现。
- [ ] T-FR-B05 [Must] 同步失败进入待重试队列，按指数退避重试；验证T-FR-B05与关联AC，先更新契约后实现。
- [ ] T-FR-B06 [Must] UIDVALIDITY 变化时重建该文件夹索引并全量重拉；验证T-FR-B06与关联AC，先更新契约后实现。
- [ ] T-FR-B07 [Should] 同步过程展示进度与结果，失败给出原因；验证T-FR-B07与关联AC，先更新契约后实现。
- [ ] T-FR-B08 [Should] 控制并发，单账号同步并发不超过 3，全局不超过 5；验证T-FR-B08与关联AC，先更新契约后实现。

## implement-mail-reading-actions
状态：PLANNED，尚未创建Change目录/实施。设计：docs/technical/design/EMAIL_INTEGRATION_DESIGN.md；代码目标：backend/src及frontend/src对应模块目录。

- [ ] T-FR-C01 [Must] 统一收件箱跨账号聚合，按收信时间倒序；验证T-FR-C01与关联AC，先更新契约后实现。
- [ ] T-FR-C02 [Must] 邮件列表显示来源账号标识；验证T-FR-C02与关联AC，先更新契约后实现。
- [ ] T-FR-C03 [Must] 阅读邮件正文，支持 HTML 与纯文本，默认不加载外部图片；验证T-FR-C03与关联AC，先更新契约后实现。
- [ ] T-FR-C04 [Should] 下载附件到本地指定目录；验证T-FR-C04与关联AC，先更新契约后实现。
- [ ] T-FR-C05 [Must] 标记已读/未读并以服务器状态为准；验证T-FR-C05与关联AC，先更新契约后实现。
- [ ] T-FR-C06 [Must] 删除邮件仅移至服务端垃圾箱，不做本地永久删除；验证T-FR-C06与关联AC，先更新契约后实现。
- [ ] T-FR-C07 [Should] 按账号与文件夹切换视图；验证T-FR-C07与关联AC，先更新契约后实现。
- [ ] T-FR-C08 [Should] 展示各文件夹未读数；验证T-FR-C08与关联AC，先更新契约后实现。

## implement-compose-outbox
状态：PLANNED，尚未创建Change目录/实施。设计：docs/technical/design/EMAIL_INTEGRATION_DESIGN.md；代码目标：backend/src及frontend/src对应模块目录。

- [ ] T-FR-D01 [Must] 新建邮件，可选择发件账号、填写收件人、主题、正文；验证T-FR-D01与关联AC，先更新契约后实现。
- [ ] T-FR-D02 [Must] 回复与转发，正确携带引用内容与线程头；验证T-FR-D02与关联AC，先更新契约后实现。
- [ ] T-FR-D03 [Should] 草稿本地保存与恢复；验证T-FR-D03与关联AC，先更新契约后实现。
- [ ] T-FR-D04 [Must] 发送失败保留草稿并提示原因；验证T-FR-D04与关联AC，先更新契约后实现。
- [ ] T-FR-D05 [Should] 添加附件，单封不超过 25MB；验证T-FR-D05与关联AC，先更新契约后实现。

## implement-local-search
状态：PLANNED，尚未创建Change目录/实施。设计：docs/technical/design/SEARCH_STORAGE_DESIGN.md；代码目标：backend/src及frontend/src对应模块目录。

- [ ] T-FR-E01 [Must] 本地全文检索（主题、正文、发件人）覆盖全部已同步账号；验证T-FR-E01与关联AC，先更新契约后实现。
- [ ] T-FR-E02 [Should] 组合筛选（账号、时间范围、有无附件、已读状态）；验证T-FR-E02与关联AC，先更新契约后实现。

## implement-local-user-vault
状态：PLANNED，尚未创建Change目录/实施。设计：docs/technical/design/SECURITY_DESIGN.md；代码目标：backend/src及frontend/src对应模块目录。

- [ ] T-FR-F01 [Must] 本地多用户切换与数据隔离；验证T-FR-F01与关联AC，先更新契约后实现。
- [ ] T-SEC-VAULT 密码封套持久化、改密、旧会话撤销及任务锁定，验证安全设计指定异常。

## implement-storage-lifecycle
状态：PLANNED，尚未创建Change目录/实施。设计：docs/technical/design/DATABASE_DESIGN.md；代码目标：backend/src及frontend/src对应模块目录。

- [ ] T-FR-F02 [Must] 设置正文缓存上限，默认 500MB，按 LRU 淘汰；验证T-FR-F02与关联AC，先更新契约后实现。
- [ ] T-FR-F03 [Should] 一键清除本地邮件数据；验证T-FR-F03与关联AC，先更新契约后实现。
- [ ] T-STORAGE-TX 完善UnitOfWork、文件发布/清理及迁移恢复，验证事务和崩溃边界。

# 异常与边界设计

本表规范所有模块共用错误字典。API错误始终含稳定code、中文message、中文action、requestId；错误详情不透传第三方原文。后台以相同code持久化，UI不靠解析英文异常名决定重试。操作可能部分完成时返回job/submission状态，不由HTTP超时推断失败。

| 触发条件 | code/HTTP与用户提示 | 系统行为/一致性 | 自动重试 | 人工 |
|---|---|---|---|---|
| 认证拒绝 | AUTH_FAILED/422：授权失败，请核对客户端授权码 | 不创建半账号；失败计数+1；旧有效凭据不被失败重授权覆盖 | 不热重试，3次冷却10分钟 | 检查/重授权 |
| 服务端明确IMAP未开 | IMAP_DISABLED/422：未开启IMAP，请按步骤开启 | 分类提示，不把网络断连误判未开 | 否 | 开服务 |
| 错主机/端口/安全模式 | SERVER_CONFIG/422：服务器配置不正确，请核对参数 | 只读验证无持久账号，校验结果TTL作废 | 否 | 改配置 |
| 无法确定认证原因 | PROVIDER_REJECTED/422：服务器拒绝连接，请检查服务及授权设置 | 提示可能性，不编造确定根因；留阶段/类型无原文 | 不无限 | 按指引 |
| TLS证书错误 | TLS_FAILED/422或任务failed：无法安全验证服务器 | 立即中止，不关闭证书校验、不降明文 | 否 | 检查时钟/合法信任链 |
| 网络断开/超时/服务暂不可用 | NETWORK_UNREACHABLE或SERVER_UNAVAILABLE/503 | 不推进失败批游标；已提交可读；持久retry | 1/2/4/8，锁定/停用除外 | 超预算手动重试 |
| OAuth access过期 | 内部TOKEN_EXPIRED；刷新失败按原因映射 | single-flight刷新，原子CAS保存；不将正常刷新当重授权 | 刷新一次 | invalid_grant重授权 |
| OAuth撤销/refresh失效 | AUTH_REQUIRED/409：授权已失效，请重新授权 | 账号auth-required，任务暂停，其他账号继续 | 否 | 是 |
| 冷却期请求 | AUTH_COOLDOWN/429：请稍后重试 | 持久截止；手动任务也不能绕过 | 到期后才可 | 核对配置 |
| SQLite忙 | STORAGE_BUSY/503：本地存储忙，请稍后 | busy_timeout后短暂可取消重试，事务全部回滚 | 有界本地重试，不套邮箱分钟重试 | 持续则排障 |
| 事务失败/数据库损坏 | STORAGE_FAILED/503：本地数据无法写入/需要恢复 | 回滚，不标完成；坏库只读隔离、不覆盖重建 | 不无限写坏库 | 从一致备份或明确重拉 |
| 磁盘空间不足 | STORAGE_FULL/503：磁盘空间不足，草稿未丢失/本次未保存 | 禁止发送未持久草稿；已持久不删；停止正文/附件写入 | 释放空间后 | 释放空间 |
| 正文超过配额 | CACHE_QUOTA/409或不缓存阅读 | 同事务预淘汰，超大正文不落缓存；保留头和独立索引 | 不重复为同超大项淘汰 | 调整限额可选 |
| 附件单项/批次超限 | ATTACHMENT_TOO_LARGE或DOWNLOAD_LIMIT/413 | 实际字节硬限、关闭流；不发布半文件；保留草稿附件原件 | 否 | 缩小附件 |
| 附件下载中断 | DOWNLOAD_INTERRUPTED（下载状态failed） | tmp不标ready，累计已接收仍记账；重试新任务核对文件 | 有界用户可重试，不伪造Range能力 | 重试 |
| 原文未缓存且离线 | Body.state=offline-unavailable/200 | 头可见，不返回空正文冒充成功；无网络请求风暴 | 网络恢复后入队 | 无 |
| UID代次变化/旧worker回写 | STALE_GENERATION/409 | 事务拒绝旧epoch，Folder重建；不拿新UID读旧缓存 | 新代任务 | 无 |
| 邮件格式/编码异常 | MESSAGE_FORMAT（任务/正文降级） | 单封隔离，保留头及原始错误状态，纯文本安全回退，不中断其他邮件 | 有界，不死循环同封 | 可查看说明 |
| 邮件移垃圾箱能力不足 | TRASH_UNSUPPORTED/409 | 不执行普通EXPUNGE或永久删除，保留原邮件 | 否 | 用服务商客户端 |
| SMTP明确拒绝 | SMTP_REJECTED（submission.failed） | 草稿/冻结MIME保留、原因可见，不标sent | 不自动重复非幂等发送 | 修正后新提交 |
| SMTP DATA后失联/提交中进程退出 | SMTP_RESULT_UNKNOWN（submission.unknown） | 不自动重发；保留候选ID/摘要，后续Sent对账仍有歧义时等用户 | 否 | 明确确认重发风险 |
| SMTP成功后断开失败 | submission.accepted保持 | 保存的最终接受结果不被Disconnect错误撤销 | 不重发 | 无 |
| HTTP幂等键重用但请求变更 | IDEMPOTENCY_CONFLICT/409 | 原提交不动，拒绝重复新发送 | 否 | 新草稿版本/键 |
| 草稿版本冲突/提交中编辑 | VERSION_CONFLICT或DRAFT_BUSY/409 | 不覆盖其他标签/冻结提交；重新读取 | 否 | 合并编辑 |
| 用户切换/会话过期 | SESSION_EXPIRED/401 | 撤销旧会话/密钥/事件/下载，取消网络；提交守卫拒绝旧任务 | 新登录后恢复允许任务 | 登录 |
| 主密钥/凭据解密失败 | KEY_UNAVAILABLE/409 | 不删除或覆盖密文，不把无法解密当空密码重试 | 否 | 原Windows身份/密码/备份 |
| 无令牌或非法Origin/Host | 401/403 | 在业务前拒绝，无数据副作用，不反射跨域头 | 否 | 重新打开本地工作台 |
| 跨用户资源ID | NOT_FOUND/404 | 不透露其他用户是否有该资源 | 否 | 无 |
| 清理中崩溃/文件占用 | CLEANUP_PENDING（operation仍running/failed） | 数据/目录预留按日志收敛，未清完不伪报AC10完成 | 有界重启恢复 | 关闭占用文件 |
| 复杂搜索超预算 | QUERY_TOO_EXPENSIVE/422 | 中断只读查询，不返回不完整零命中；coverage真实 | 不后台无限扫 | 缩小日期/账号 |

429附Retry-After及retryAfterSeconds；503说明是否可重试。程序取消返回任务paused/canceled而不是NETWORK_ERROR。前端token失效首先清理敏感状态，再显示登录；普通邮箱错误不能把整个应用切换到空白错误页。

崩溃点重点：DB提交前后、文件rename前后、刷新响应与CAS之间、SMTP submitting及最终确认之间、密码封套提交之间。只有数据库事务内写操作可声称原子；网络和文件用持久状态/幂等补偿，不能用一个try/catch掩盖不同结果。

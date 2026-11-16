from pathlib import Path
import json
BASE=Path(__file__).resolve().parents[1]
b=json.loads((BASE/'baseline-input.json').read_text(encoding='utf-8'))
ops=json.loads((BASE/'operation-registry.json').read_text(encoding='utf-8'))
entities={
 'A01':'MailAccount,AuthCredential,AccountReservation','A02':'MailAccount（模板在ProviderRegistry静态配置）','A03':'AuthCredential,ProfileSettings','A04':'ProviderRegistry指引（无新增业务表）','A05':'MailAccount认证计数,短期Validation','A06':'AuthCredential,UserKeyEnvelope','A07':'AuthCredential,MailAccount.work_epoch','A08':'CleanupOperation,CatalogOperation及账号级联实体','A09':'MailAccount.enabled,SyncJob',
 'B01':'Folder,Message,SyncCheckpoint','B02':'Folder,Message,SyncCheckpoint','B03':'SyncJob','B04':'ProfileSettings,SyncJob','B05':'SyncJob.attempt/next_run_at_ms','B06':'Folder.epoch/uid_validity,Message,SearchDocument','B07':'SyncJob.progress/last_error_code','B08':'账号/全局运行时租约（不另加业务表）',
 'C01':'Message,Folder,MailAccount','C02':'Message.account_id,MailAccount','C03':'MessageBody,SearchDocument,SyncJob','C04':'Attachment,FileObject,DownloadBatch/Item','C05':'Message,MailMutation,SyncJob','C06':'Folder,Message,MailMutation','C07':'Folder,Message','C08':'Folder.unread_count',
 'D01':'Draft,OutboxSubmission,OutboxRecipient','D02':'Message引用头,Draft.source_headers_json,OutboxSubmission','D03':'Draft,DraftAttachment','D04':'Draft,OutboxSubmission,FileObject','D05':'DraftAttachment,FileObject',
 'E01':'Message,SearchDocument,MessageSearch','E02':'Message过滤字段,MailAccount',
 'F01':'LocalUser,UserKeyEnvelope,Profile及每用户全部实体','F02':'MessageBody,CacheQuota','F03':'CleanupOperation,ProfileSettings及邮件/索引实体'
}
modules={'A':'Account','B':'Sync','C':'Mail','D':'Compose','E':'Search','F':'Settings/Security'}
flows={'A':'FLOW-ACCOUNT','B':'FLOW-SYNC','C':'FLOW-READ','D':'FLOW-SEND','E':'FLOW-SEARCH','F':'FLOW-USER'}
acs={'A01':'AC-02','A02':'AC-01','A03':'AC-13补充及专用用例','A04':'AC-01','A05':'AC-02','A06':'AC-03','A07':'专用用例','A08':'AC-10','B01':'AC-01','B02':'AC-05','B03':'专用用例','B05':'AC-05及专用退避','B06':'AC-05及专用代次重建','C01':'AC-04','C02':'AC-04','C03':'AC-07','C05':'AC-08','C06':'AC-09','D01':'AC-13补充','D02':'AC-13补充','D04':'AC-13补充','E01':'AC-14补充','F01':'AC-12','F02':'专用配额用例'}
t=['# 需求追踪矩阵','','基线逐行来自baseline-input.json（原Word表8/13/15/17/19，源文件SHA256在该JSON）。原35条优先级保持Must24/Should11；这里只表示设计覆盖，不表示产品测试已通过。API关联operationId来自openapi.yaml；T编号是待执行产品用例。','','| 需求 | 原描述 | 优先级 | 模块 | 数据实体 | API operationId | 核心流程 | 测试 / AC |','|---|---|---|---|---|---|---|---|']
tests=[]
for id,desc,priority,criterion in b['fr']:
    short=id.removeprefix('FR-'); related=[o['id'] for o in ops if id in o['fr']]
    assert related, id
    t.append(f"| {id} | {desc} | {priority} | {modules[short[0]]} | {entities[short]} | {', '.join(related)} | {flows[short[0]]} | T-{id} / {acs.get(short,'专用用例')} |")
    tests.append(f"| T-{id} | {priority} | 已登录本地用户且按本需求准备受控数据/能力 | 执行{modules[short[0]]}相应用例；包含成功与对应错误分支 | {criterion} | NOT_TESTED（产品待开发） |")
t += ['','## 非功能需求','','| 编号 | 原指标 | 设计位置 | 测试 |','|---|---|---|---|']
for id,category,desc,method in b['nfr']:
    location='TECHNICAL_DESIGN性能预算' if int(id[-2:])<=4 else 'SECURITY_DESIGN' if id in ['NFR-06','NFR-07','NFR-08','NFR-09','NFR-10','NFR-14'] else 'SYNC_ENGINE_DESIGN/ERROR_HANDLING_DESIGN' if id in ['NFR-05','NFR-11'] else 'SYSTEM_ARCHITECTURE/CR-08' if id=='NFR-12' else 'MODULE_DESIGN三步向导'
    effective=desc+'；CR-08有效范围仅Windows11' if id=='NFR-12' else desc
    t.append(f'| {id} | {effective} | {location} | T-{id}：{method}（原方法保留） |')
t += ['','## 业务规则','','| 规则 | 原文 | 设计/约束 | 测试 |','|---|---|---|---|']
brloc=['AccountReservation唯一HMAC及补偿','MailAccount三次失败与10分钟持久冷却','账号/全局信号量3/5','流式25MB单项/200MB批次计数','Folder.epoch及新代全量任务','MailMutation确认后更新FLAGS','仅服务端垃圾箱，禁止普通EXPUNGE','CacheQuota CHECK及同事务预淘汰','SQL *_ms UTC/API RFC3339 Z','MimeKit解码及UTF8正文Blob']
for (id,rule,reason),loc in zip(b['br'],brloc):t.append(f'| {id} | {rule} | {loc} | T-{id} |')
t += ['','## 已接受CR与解释','','| 编号 | 内容 | 设计影响 |','|---|---|---|']
for id,change,reason,impact,decision in b['cr']:t.append(f'| {id} | {change}；{decision} | {impact}；不得以新API恢复已取消功能 |')
t += ['| CR-08 | 首版仅Windows11；2026-11-15负责人明确接受；旧标识CR-PLATFORM-001为同一项 | 覆盖原NFR-12的Windows10/11表述；原DOCX保留历史，不再测试Windows10 |',
'','D2密码登录、D3正文索引独立保留、D4四应用用户地址不重复、D5Google认证必要端点按DECISION_LOG和DESIGN_INPUTS纳入实现解释。FR-D02最新解释为“回复关联原邮件、转发独立邮件并携引用”，负责人本轮明确接受，不恢复会话合并。',
'','## 核心流程定位','','| 标识 | 文档 | 异常合同 |','|---|---|---|','| FLOW-ACCOUNT | SYSTEM_ARCHITECTURE添加账号时序；EMAIL_INTEGRATION适配 | 认证/端口/网络/冷却/补偿 |','| FLOW-SYNC | SYNC_ENGINE_DESIGN状态机/时序/伪代码 | 断网/重启/UID代次/磁盘/会话失效 |','| FLOW-READ | SYSTEM_ARCHITECTURE数据流；EMAIL_INTEGRATION阅读/操作 | 离线/未缓存/格式坏/能力不足/旧UID |','| FLOW-SEND | EMAIL_INTEGRATION状态机与伪代码 | 明确拒绝/unknown/幂等/草稿冲突 |','| FLOW-SEARCH | SEARCH_STORAGE_DESIGN查询与覆盖 | 空词/短词成本/未索引/取消/重建 |','| FLOW-USER | SECURITY_DESIGN登录/封套/切换 | 错误密码/旧会话/改密中断/密钥坏/清理 |','']
(BASE/'REQUIREMENT_TRACEABILITY.md').write_text('\n'.join(t),encoding='utf-8')
plan=['# 开发及集成测试计划','','本阶段仅检查设计工件；不重跑阶段二邮箱/OAuth/性能。以下是产品实现后的用例，全部NOT_TESTED，不能以设计通过替代功能通过。所有Must必须通过，原安全AC-03/07/11一票否决；Should按既有优先级排期并登记延期，不能偷偷提升或取消。','','## 原AC-01～12原文保留','','| 编号 | 原关联需求 | Given | When | Then | 后续测试 |','|---|---|---|---|---|---|']
for id,frs,g,w,then in b['ac']:plan.append(f'| {id} | {frs} | {g} | {w} | {then} | T-{id}，NOT_TESTED |')
plan += ['','原AC中的100封/三个账号等条件不因阶段二只测20 UID而降低。原AC12两个用户条件保留，NFR14另测四用户。NFR12只有平台范围被已批准CR-08覆盖。','','## AC-13/14验收补充（设计稿，待本阶段审核）','','| 编号 | Given | When | Then |','|---|---|---|---|',
'| AC-13 | 已授权受控发送账号、受控收件箱、合成邮件和已保存草稿 | 新建、回复、独立转发各一次；小合成附件；另制造明确SMTP拒绝及DATA后失联 | 收件端正文/附件一致；回复In-Reply-To/References关联实际收到的原邮件；转发独立引用；明确失败保稿和原因，unknown不自动重发；accepted不冒充收到 |',
'| AC-14 | 同一用户3账号合计1000封、主题/发件人/正文及1～2字词预置命中，包含未读与阅读缓存已淘汰；另用户同词干扰 | 跨账号本地搜索、账号/日期等组合过滤，原NFR04同词三次计时 | 精确命中集合，无跨用户结果；完整索引覆盖时每次<=1秒；正文覆盖未完成明确标示，短词参数化LIKE语义正确 |',
'','这两条补充原先缺口，不改AC-01～12；D02解释已确认，但补充用例本身等待本阶段审核。产品真实证据与阶段二原型证据分开记录。','','## 每条FR用例','','| 测试 | 优先级 | Given | When | Then（基线验收要点） | 状态 |','|---|---|---|---|---|---|']+tests
plan += ['','## 非功能可执行条件','','| 测试 | 指标/前提 | 操作/断言 | 状态 |','|---|---|---|---|']
for id,category,desc,method in b['nfr']:
    if id=='NFR-12':desc='Windows11，Chrome/Edge最新版；CR-08已取消Windows10'
    plan.append(f'| T-{id} | {desc} | {method} | NOT_TESTED（产品） |')
plan += ['','## 业务规则及跨模块故障回归','','| 测试 | 操作与断言 |','|---|---|']
for (id,rule,reason),loc in zip(b['br'],brloc):plan.append(f'| T-{id} | {rule}；针对{loc}验证边界前/中/后与重启，不改原规则 |')
plan += ['','## 关键集成补充','','- 用户隔离：四用户分别写账号/头/正文/索引/草稿/附件；改URL/userId、旧session、切换中下载和旧worker写入均拒绝，索引绝不跨库。','- 密钥：改密提交前后崩溃分别只接受旧/新密码；不同user/account/keyId的AAD不能互换，刷新CAS与移除并发不复活账号；数据库损坏不覆盖空库。','- 同步：首次90天时区边界、空箱/稀疏UID/范围中expunge、固定上界后新入、完整FLAGS/删除扫描失败不删本地；UIDVALIDITY重建全量与旧worker守卫。','- 发送：HTTP同幂等键重复、不同payload冲突、RCPT拒绝禁止DATA、DATA确认丢失unknown、发送时退出及切用户、草稿附件引用直到对账结束。','- 缓存/文件：SQL账本拒绝漏淘汰提交；降低限额同事务；文件rename/DB提交间崩溃、磁盘不足/文件被占用不发布半附件；阅读淘汰不移除搜索命中。','- 安全：真实Chrome/Edge打开合成恶意邮件，观察器正对照先到达，脚本/内嵌iframe/外部资源零请求；Host/Origin/no-token/null-origin在业务前拒绝；日志敏感哨兵全无。',
'','## 测试层级与记录','','单元：领域规则、状态转换、游标区间与密钥封套；集成：真实SQLite/文件/协议stub及事务崩溃；受控邮箱：仅有需要验证的新适配行为时连接；端到端：真实浏览器与本地宿主；验收：AC逐条；回归：依据operationId/FR/ADR/迁移变更选择受影响项，不机械重跑无变化用例。',
'','记录字段：testId、FR/AC、commit或artifactHash、环境及依赖、数据类别（真实邮箱/模拟/合成）、步骤、预期、实际、证据路径、PASS/FAIL/BLOCKED/NOT_TESTED、缺陷及修复复测。记录不含凭据/正文/附件名/完整地址；原始敏感EML不进Git。失败证据保留，只新增修正后结果。','']
(BASE/'TEST_PLAN.md').write_text('\n'.join(plan),encoding='utf-8')
print('Generated complete FR/NFR/BR/AC/CR traceability and future product test plan; no tests executed.')

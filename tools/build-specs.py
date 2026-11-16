"""Import approved target behaviors into OpenSpec; no claim of business implementation."""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[1]
design=ROOT/'docs/technical/design'
b=json.loads((design/'baseline-input.json').read_text(encoding='utf-8'))
operations=json.loads((design/'operation-registry.json').read_text(encoding='utf-8'))
group={'A':'account-management','B':'mail-sync','C':'mail-reading','D':'mail-compose','E':'mail-search','F':'local-users'}
def capability(id):return 'local-storage' if id in ('FR-F02','FR-F03') else group[id[3]]
titles={'account-management':'账号接入与生命周期','mail-sync':'邮件同步与持久任务','mail-reading':'阅读与服务器整理','mail-compose':'草稿与发送','mail-search':'本地跨账号检索','local-users':'本地应用用户','security':'本地通信与邮件安全','local-storage':'本地持久化及缓存'}
documents={}
for key,title in titles.items():
    documents[key]=[f'# {key} Specification','','## Purpose',f'本规范定义邮汇MailHub已批准的{title}能力，作为后续独立开发和验收的稳定行为合同。当前仅工程初始化，规范存在不表示相应产品业务已实现；实施状态由Change任务与实际测试记录确定。','', '## Requirements','']
changes={'account-management':'implement-account-access','mail-sync':'implement-durable-mail-sync','mail-reading':'implement-mail-reading-actions','mail-compose':'implement-compose-outbox','mail-search':'implement-local-search','local-users':'implement-local-user-vault','security':'implement-local-user-vault','local-storage':'implement-storage-lifecycle'}
special={
'FR-D02':'回复使用真实收到原邮件的Message-ID/References关联原会话；转发作为独立新邮件携引用，不带伪造回复头，不提供会话合并UI。',
'FR-F01':'同Windows账户下最多四个密码认证应用用户，一次仅一个活跃；切换撤销旧会话和密钥租约，账号/邮件/草稿/搜索/缓存/凭据均隔离；DPAPI alone不构成应用用户隔离。',
'FR-F03':'清除本地邮件及索引，保留账号配置/凭据/草稿和设置，暂停自动补拉直到用户手动同步；不是远程永久删信。',
'FR-B05':'失败任务持久化，1/2/4/8分钟退避，进程重启恢复；可控时钟验证，不绕过三次邮箱认证失败10分钟冷却。',
'FR-B06':'UIDVALIDITY变化使旧epoch失效，重建该文件夹索引并全量重拉，不用首次90天过滤限制代次重建。',
'FR-B08':'本功能优先级仍为Should；但BR-03的单账号<=3、全局<=5安全上限在任何同步实现中始终成立。',
'FR-C06':'仅移动到服务器垃圾箱；不支持安全MOVE/UIDPLUS降级时明确失败，不以普通EXPUNGE或永久删除代替。',
'FR-E01':'主题/发件人/正文覆盖本用户全部已同步账号；未读及阅读缓存淘汰后的正文仍可搜索；返回索引覆盖状态，短词使用安全LIKE回退。'}
trace=['# FR → Spec → Change → Design → Task → API → Test','','主Specs保存已批准目标能力，非业务完成证明；下列业务Change是计划名称，不是已创建/已执行的变更。阶段四唯一执行变更是engineering-foundation。','','| FR | 优先级 | Spec | 计划Change / Task | 设计 | operationId | 测试 |','|---|---|---|---|---|---|---|']
for id,desc,priority,criterion in b['fr']:
    cap=capability(id); related=[o['id'] for o in operations if id in o['fr']]
    requirement=special.get(id,desc+'。'+criterion+'。')
    if priority=='Should':requirement='优先级Should，不提升为Must。The system SHALL preserve this scope; when this feature is scheduled, '+requirement
    else:requirement='优先级Must。The system SHALL '+requirement
    documents[cap]+=[f'### Requirement: {id} {desc}',requirement,'',f'接口：{", ".join(related)}；测试：T-{id}；批准技术合同：docs/technical/design/REQUIREMENT_TRACEABILITY.md。','',f'#### Scenario: {id} 成功行为', '- **GIVEN** 已认证当前应用用户，相关服务能力及合成/受控数据已准备'+('，本Should项已安排实施' if priority=='Should' else ''),f'- **WHEN** 用户或已授权后台执行本需求对应操作',f'- **THEN** {special.get(id,criterion)}','',f'#### Scenario: {id} 授权或运行失败','- **GIVEN** 无有效会话、对象不属于当前用户，或运行条件失败','- **WHEN** 发起该操作','- **THEN** 按ERROR_HANDLING_DESIGN返回拒绝或明确中文原因，不能泄露其他用户数据、丢失草稿或提交半条记录','']
    trace.append(f'| {id} | {priority} | {cap} | {changes[cap]} / T-{id} | docs/technical/design | {", ".join(related)} | T-{id}；AC映射见设计追踪 |')
nfrcap={'NFR-01':'mail-reading','NFR-02':'mail-reading','NFR-03':'mail-sync','NFR-04':'mail-search','NFR-05':'mail-sync','NFR-06':'security','NFR-07':'security','NFR-08':'security','NFR-09':'security','NFR-10':'security','NFR-11':'local-storage','NFR-12':'security','NFR-13':'account-management','NFR-14':'local-users'}
for id,category,desc,method in b['nfr']:
    if id=='NFR-12':desc='首版仅Windows11，Chrome与Edge最新版；CR-08取消Windows10兼容范围'
    documents[nfrcap[id]] += [f'### Requirement: {id} {category}',f'The system SHALL 满足：{desc}。相关原AC及T-{id}保持对应，阶段二原型不冒充产品验收。','',f'#### Scenario: {id} 规定条件验收',f'- **GIVEN** 已按原需求及CR-08准备测试环境','- **WHEN** '+method,'- **THEN** '+desc,'']
brcap={'BR-01':'account-management','BR-02':'account-management','BR-03':'mail-sync','BR-04':'mail-reading','BR-05':'mail-sync','BR-06':'mail-reading','BR-07':'mail-reading','BR-08':'local-storage','BR-09':'local-storage','BR-10':'local-storage'}
for id,rule,reason in b['br']:
    documents[brcap[id]] += [f'### Requirement: {id} {rule}',f'The system SHALL 始终遵守：{rule}。理由：{reason}。对应T-{id}。','',f'#### Scenario: {id} 边界保持','- **GIVEN** 接近或达到本规则边界，或发生中断/重复请求','- **WHEN** 执行相关操作','- **THEN** 保持本规则，不通过重启、取消或错误降级绕过限制','']
for cap,lines in documents.items():
    lines+=['## References','- 完整业务/接口/错误合同：docs/technical/design/TECHNICAL_DESIGN.md、openapi.yaml、TEST_PLAN.md。','- 已接受CR-01～CR-08及AC-01～AC-14：docs/requirements/BASELINE_DECISIONS.md；AC原文/补充定义在设计TEST_PLAN.md。','- 实施与代码位置：docs/collaboration/DEVELOPMENT_SLICES.md。','']
    folder=ROOT/'openspec/specs'/cap;folder.mkdir(parents=True,exist_ok=True);(folder/'spec.md').write_text('\n'.join(lines),encoding='utf-8')
(ROOT/'docs/collaboration/REQUIREMENT_SPEC_TRACE.md').write_text('\n'.join(trace)+'\n',encoding='utf-8')
lines=['# 后续业务开发切片','','阶段四不执行下列业务任务。阶段五获批准后先用OpenSpec创建对应Change，再按本表和设计细化proposal/design/tasks及必要delta，不为每条FR创建Change。先共享身份/存储边界，再业务模块并行；公共数据库/API变更必须先评审。','']
designs={'implement-local-user-vault':'SECURITY_DESIGN.md','implement-storage-lifecycle':'DATABASE_DESIGN.md','implement-account-access':'EMAIL_INTEGRATION_DESIGN.md','implement-durable-mail-sync':'SYNC_ENGINE_DESIGN.md','implement-mail-reading-actions':'EMAIL_INTEGRATION_DESIGN.md','implement-compose-outbox':'EMAIL_INTEGRATION_DESIGN.md','implement-local-search':'SEARCH_STORAGE_DESIGN.md'}
for change in dict.fromkeys(changes.values()):
    selected=[r for r in b['fr'] if changes[capability(r[0])]==change]
    lines += [f'## {change}',f'状态：PLANNED，尚未创建Change目录/实施。设计：docs/technical/design/{designs[change]}；代码目标：backend/src及frontend/src对应模块目录。','']
    for id,desc,priority,_ in selected:lines.append(f'- [ ] T-{id} [{priority}] {desc}；验证T-{id}与关联AC，先更新契约后实现。')
    if change=='implement-local-user-vault':lines.append('- [ ] T-SEC-VAULT 密码封套持久化、改密、旧会话撤销及任务锁定，验证安全设计指定异常。')
    if change=='implement-storage-lifecycle':lines.append('- [ ] T-STORAGE-TX 完善UnitOfWork、文件发布/清理及迁移恢复，验证事务和崩溃边界。')
    lines+=['']
(ROOT/'docs/collaboration/DEVELOPMENT_SLICES.md').write_text('\n'.join(lines),encoding='utf-8')
req=ROOT/'docs/requirements';req.mkdir(exist_ok=True)
records=['# 有效需求基线与批准记录','','业务原件保留于docs/reference/需求分析与验收报告.docx；不改原件/原AC。阶段四任务书明确批准阶段三整体设计、8ADR、AC13/14及下列默认值。','','## 已接受CR','']
for id,change,reason,impact,decision in b['cr']:records.append(f'- {id}：{change}；{decision}。')
records+=['- CR-08：首版仅Windows11；旧技术标识CR-PLATFORM-001同一决议，不再要求Windows10。','','## 阶段三批准补充','- 单一活跃应用用户；锁定暂停该用户同步，重新认证后从持久状态恢复。','- F03保留账号配置和草稿，暂停自动补拉。','- 回复引用真实收到原邮件，转发独立且携引用；无会话合并UI。','- AC13发送与AC14搜索按docs/technical/design/TEST_PLAN.md补充稿正式采用；AC01～12不改。','- 阶段三ADR0004～0011八份及整体设计已被负责人阶段四任务书正式批准，原文件保留其交付时“待审核”标题以维持历史哈希，当前批准状态以本记录为准。','- SQLite含官方WAL修复的兼容依赖升级已获批准，实际版本以工程运行检查为准。','']
(req/'BASELINE_DECISIONS.md').write_text('\n'.join(records),encoding='utf-8')
print('8 specs, 35 FR links, 14 NFR, 10 BR, AC/CR baseline and 7 planned business slices generated.')

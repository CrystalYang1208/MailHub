from pathlib import Path
BASE=Path(__file__).resolve().parents[1]
decisions=[
('0004-local-host','本地后台架构','Windows本地协议、浏览器UI、DPAPI及无运营方服务器已确定，需要长期任务与明确退出语义。','避免Windows Service身份错配、浏览器标签退出影响后台及业务/协议耦合。',[
('Windows Service','长期驻留','管理员安装、DPAPI与交互身份管理复杂'),('用户级单宿主托盘+同源Kestrel','当前Windows身份直接保护、单进程易维护','浏览器关闭不等于退出，需租约/锁定状态'),('Electron/云端桥接','统一壳或部署集中','改变已定形态或隐私边界，不采用')],
'沿用.NET10用户级单宿主、127.0.0.1动态Kestrel同源托管Vue。模块化分层，单实例，命名管道仅协调打开工作台。锁定/切换暂停该用户任务；关闭标签不暂停，正常退出持久保存状态。',
'满足已接受D1及本地化边界，避免为小型本地系统引入微服务或服务账户。',
'宿主故障影响所有本地模块，需恢复记录；只有解锁用户可同步，不提供锁定用户无密码后台解密。',
'阶段四验证真实托盘生命周期、单实例、退出中SMTP unknown和Windows会话锁定。','SYSTEM_ARCHITECTURE.md'),
('0005-profile-storage','数据库及多用户隔离','最多4个应用用户共用同Windows身份，需要账号、FTS、缓存和草稿全部隔离。','共享库漏owner过滤以及跨库唯一账号与原子性的矛盾。',[
('共享SQLite加owner字段','跨用户统计方便','每个FTS/附件请求都可能漏过滤'),('每用户SQLite加小目录库','连接级隔离，查询自然仅属当前用户','需目录预留和幂等跨库补偿'),('每用户Windows账户或独立数据库服务','操作系统或服务隔离','不符合当前应用用户形态或部署成本')],
'每用户Profile库+catalog身份/封套/AccountReservation；当前会话选择固定库。地址HMAC唯一预留覆盖本安装四用户，跨库采用CatalogOperation补偿，不声称WAL ATTACH全局事务。',
'隔离范围清楚、开发人员不需每次跨库过滤，SQLite路线保持。',
'多个物理库需分别迁移和一致备份；不承诺对同Windows恶意进程的明文邮件静态保密。',
'四用户产品API/文件/事件隔离及跨库注册删除中断恢复。','DATABASE_DESIGN.md'),
('0006-password-envelope','用户密码与密钥管理','DPAPI CurrentUser不能隔离同Windows下的A/B应用用户，M3已证明额外密码层可行。','不能只加登录UI或依赖DPAPI；需要改密码可原子完成而不重加密每封凭据。',[
('仅密码验证+DPAPI','简单且原生','共享Windows身份能解DPAPI，凭据无密码层'),('密码直接加密所有凭据','应用密码层隔离','改密需批量重写、失败复杂'),('随机UMK+密码KEK封套+DPAPI','改密只换封套，用户/账号AAD边界明确','丢密码不可恢复，密钥生命周期需严格实现')],
'采用随机用户UMK、独立盐PBKDF2-SHA256>=600000的验证值与KEK，AES-GCM封套外加DPAPI；凭据AES-GCM绑定用户/账号后再次DPAPI。改密在catalog同事务更新验证/封套/authVersion，退出清密钥。',
'延续密码+DPAPI决策，不加整库加密或第三方密码体系；使用.NET现有算法，明确同Windows边界。',
'KDF需要阶段四校准；不能保证托管字符串绝无残留；没有无损忘记密码找回。',
'持久封套重启、改密崩溃、AAD交换、刷新并发、四用户/旧会话与日志哨兵。','SECURITY_DESIGN.md'),
('0007-body-file-storage','正文缓存与附件存储','原型曾出现已提交510MB窗口；索引必须独立保留；附件需本地文件系统。','阅读缓存配额与DB/WAL物理空间不同，文件和SQL不能共同事务。',[
('提交正文再异步LRU','实现简单','有可见/持久超限，已被原型反证'),('同事务预淘汰+写入+配额账本CHECK','读者看不到超限提交','需要单写队列，降低配额也需淘汰'),('正文也存外部文件','可流式大对象','配额/读取租约及崩溃文件补偿更复杂')],
'阅读正文存MessageBody UTF8 Blob；先同事务LRU预淘汰再写，CacheQuota CHECK硬守卫。附件/草稿文件用FileObject staging/ready/cleanup协议，不占阅读500MB；SearchDocument独立。',
'采纳ADR0003修正方向且便于数据库事务保证，不引入未经需求要求的对象存储。',
'DB/WAL与索引实际空间可能超过500MB；附件不自动永久删除用户已导出文件，SSD不保证安全擦除。',
'新SQL约束检查只是设计核查；产品中再验证磁盘不足、改限额、文件rename/DB提交崩溃与引用清理。','SEARCH_STORAGE_DESIGN.md'),
('0008-fts-chinese','FTS5中文搜索策略','FTS5跨账号合成1000封通过，trigram无法MATCH短于3字符的词。','不能把未索引正文当零命中，也不能以SQLite NOCASE冒充完整Unicode大小写处理。',[
('仅unicode61','低成本','中文未按词划分，短词覆盖差'),('trigram+安全phrase+1/2字符LIKE回退','沿用已验证SQLite方案，可同事务维护','短词扫描有成本，需规范化与查询预算'),('引入Lucene/外部中文分词服务','更丰富排序/词法','新依赖/索引一致性与路线改变，当前无必要')],
'FTS5 external-content SearchDocument、trigram，输入和索引NFC/casefold；长词引用phrase，1～2码点参数化LIKE ESCAPE，反馈coverage和可取消预算。阅读缓存淘汰不删索引。',
'保持SQLite选型，修补已知短词限制，不用合成性能承诺任意库一秒。',
'LIKE ESCAPE不能依赖trigram索引优化，大库短词可能超预算；正文索引增长是明确代价。',
'产品实际1000封基准、中文混合大小写/通配符字面语义、取消、索引重建与覆盖。','SEARCH_STORAGE_DESIGN.md'),
('0009-sync-transactions','同步事务及重试','已验证UID核心算法、真实20-UID网络中断恢复与独立任务持久化。','产品还需固定扫描边界、FLAGS/删除对账、epoch守卫，不能直接照搬原型长网络事务。',[
('仅记最后返回UID','简单','部分FETCH/稀疏UID可能遗漏，无法对账删除'),('固定UID范围+原子批次+持久任务','游标/头/索引同提交、可恢复','需状态与扫描辅助表'),('网络和SQL放同一长事务','写法直观','锁跨网络等待，阻塞单写队列')],
'网络在事务外；短事务检查用户/账号/文件夹epoch及任务lease，提交头/SearchDocument/检查点/进度。初次固定90天，代次变化按原需求全量重建。retry持久，1/2/4/8分钟，可控时钟测试；3/5上限。',
'直接处理阶段二已见风险而不夸大其验证范围，可并行模块开发。',
'远程服务器与本地非分布式事务；用幂等/状态对账而非exactly-once承诺。',
'产品多Folder/多账号、FLAGS删除、范围中expunge、租约失效、崩溃和磁盘满。','SYNC_ENGINE_DESIGN.md'),
('0010-local-security','前后端本地通信与邮件安全','真实Chrome/Edge最小安全主流程通过，运营方服务器被排除。','localhost可能被恶意网页访问，邮件HTML不可信；进程token不能代替用户认证。',[
('只监听回环','简单','仍可被恶意网页盲请求'),('同源+随机BridgeToken+用户session+Host/Origin','可逐层授权并拒绝跨站','多标签页引导、会话切换需管理'),('跨源开发服务器+通配CORS','开发方便','扩大浏览器攻击面，不采用生产配置')],
'采用同源127.0.0.1、一次性fragment引导、内存BridgeToken及用户session双校验、严格Host/Origin/Fetch Metadata，禁止通配CORS。邮件清洗后隔离sandbox空权限+CSP禁止资源，邮件内iframe移除。',
'沿用已验证原型且覆盖明确安全威胁，浏览器无需任何邮箱凭据。',
'富HTML兼容受保守白名单限制；不是防同Windows恶意进程/管理员的隔离。',
'产品登录切换/旧下载、真实邮件复杂格式与浏览器回归，不开展无关完整渗透。','SECURITY_DESIGN.md'),
('0011-provider-adapters','邮箱服务商适配架构','163真实读取曾因缺ID失败并修正；Gmail代理路径与OAuth有特殊性。','厂商判断散落会导致业务层与协议/凭据耦合。',[
('每模块直接MailKit及厂商if','初期快','重复认证/错误策略、难测试'),('ProviderRegistry+能力端口+会话工厂','特殊规则集中且通用邮箱仍可配置','需有限接口和能力矩阵'),('每服务商独立微服务','完全隔离','本地部署过重，不符合产品')],
'模板是配置默认值，特例在ProviderProfile/适配器；163 ID、Gmail XOAUTH2/刷新、特殊文件夹、错误映射与ConnectionPolicy集中实现。一个Client串行；安全TLS、超时/取消一致，业务只消费能力。',
'不推翻MailKit/MimeKit，保留generic IMAP/SMTP，不被三个品牌枚举锁死。',
'不能从所有失败精确推断IMAP未开；能力不足需明确失败，绝不降级永久删除或明文连接。',
'Gmail应用专用密码资格/公开分发、无MOVE服务器安全降级、学校自签CA合法部署与特殊Folder。','EMAIL_INTEGRATION_DESIGN.md'),
]
folder=BASE/'ADR';folder.mkdir(exist_ok=True)
for slug,title,bg,problem,candidates,decision,reason,cost,future,link in decisions:
    lines=[f'# ADR {slug[:4]} {title}','','状态：阶段三设计决策，待本阶段整体审核；已接受技术栈/密码/平台约束保持不变。日期：2026-11-15。先前ADR0001～0003保留在上级ADR目录，不覆盖历史。','','## 背景',bg,'','## 问题',problem,'','## 候选比较','','| 方案 | 优点 | 缺点与适用性 |','|---|---|---|']
    lines += ['| '+' | '.join(row)+' |' for row in candidates]
    lines += ['','## 最终推荐决策',decision,'','## 决策理由',reason,'','## 代价与风险',cost,'','## 后续验证与设计合同',future,f'完整实施合同：[设计文档](../{link})。后续验证为产品/工程阶段计划，不表示本阶段已执行或重启阶段二。','']
    (folder/(slug+'.md')).write_text('\n'.join(lines),encoding='utf-8')
print('Generated 8 design ADRs without overwriting previously accepted records.')

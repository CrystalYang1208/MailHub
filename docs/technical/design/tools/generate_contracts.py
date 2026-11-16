"""Design authoring utilities: derive traceability and dictionaries; no product/network code."""
from pathlib import Path
from collections import Counter
import hashlib, json, re, sqlite3, tempfile, xml.etree.ElementTree as ET, zipfile
import yaml

BASE = Path(__file__).resolve().parents[1]
PROJECT = BASE.parents[2]
NS = {'w': 'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
SOURCE = PROJECT / 'docs/reference/需求分析与验收报告.docx'
with zipfile.ZipFile(SOURCE) as archive:
    xml = ET.fromstring(archive.read('word/document.xml'))
text = lambda e: ''.join(t.text or '' for t in e.findall('.//w:t', NS))
tables = [[[text(c) for c in row.findall('w:tc', NS)] for row in table.findall('w:tr', NS)] for table in xml.findall('.//w:tbl', NS)]
rows = lambda prefix: [row for table in tables for row in table if row and re.fullmatch(prefix + r'-[A-Z]?\d{2}', row[0])]
fr = [r for r in rows('FR') if len(r) == 4]
nfr = [r for r in rows('NFR') if len(r) == 4]
br = [r for r in rows('BR') if len(r) == 3]
ac = [r for r in rows('AC') if len(r) == 5]
cr = [r for r in rows('CR') if len(r) == 5]
assert len(fr) == 35 and Counter(r[2] for r in fr) == {'Must': 24, 'Should': 11}
assert (len(nfr),len(br),len(ac),len(cr)) == (14,10,12,7)
(BASE/'baseline-input.json').write_text(json.dumps({'source':'../../reference/需求分析与验收报告.docx','sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'fr':fr,'nfr':nfr,'br':br,'ac':ac,'cr':cr},ensure_ascii=False,indent=2),encoding='utf-8')

LIFECYCLE = {
 'LocalUser':'账号注册至彻底清理；删除中占用用户槽', 'UserKeyEnvelope':'用户生命周期；改密码原子换封套',
 'CatalogSecret':'安装生命周期，丢失时只能从一致备份恢复或清理后重新接入',
 'AccountReservation':'预留至账号彻底清理完成；清理前不能释放唯一占用',
 'CatalogOperation':'跨库操作开始至完成，终态保留7日后清理',
 'Profile':'用户库唯一属主', 'ProfileSettings':'用户生命周期；F03保留', 'CacheQuota':'用户生命周期；F03保留限额并归零用量',
 'MailAccount':'添加至移除；F03保留', 'AuthCredential':'接入至移除；重授权/刷新CAS替换',
 'Folder':'服务端映射；缺失标记后受控清理', 'Message':'头部不受LRU；服务器删除、代次重建、账号清理时删除',
 'SearchDocument':'与Message同生灭，正文索引独立保留', 'MessageSearch':'SearchDocument触发器维护；可重建',
 'MessageBody':'正文LRU或主动清缓存，不删头/索引', 'FileObject':'临时至原子发布；不再引用后持久清理',
 'Attachment':'随Message；文件对象另走清理协议', 'SyncJob':'非终态重启可恢复；终态7日后清理',
 'SyncCheckpoint':'随任务；完成后清理', 'FolderScan':'完整对账至提交后删除；未完成可重启或废弃重扫',
 'ScanUid':'随FolderScan删除', 'Draft':'用户显式删除/账号移除；发送后标sent保留，不自动丢稿',
 'DraftAttachment':'随Draft解绑，文件实际清理由引用计数查询决定',
 'OutboxSubmission':'与Draft关联；unknown禁止自动重发；不早于对账完成清理',
 'OutboxRecipient':'随提交；不把地址写日志', 'MailMutation':'服务器确认或失败后随终态任务清理',
 'DownloadBatch':'下载活动及结束后7日；文件缓存独立管理', 'DownloadItem':'随批次',
 'CleanupOperation':'删除实际文件及数据库收敛后done；终态7日清理',
}
dictionary = ['# 数据字典','', '由SQL字段注释及SQLite元数据生成；SQL为约束权威。所有INTEGER时间列以`_ms`结尾，存UTC Unix毫秒；API输出RFC3339 UTC。TEXT均UTF8；BLOB按字段说明编码。NULL表示未知/不适用，不以0代替未知。','']
mermaid = ['erDiagram']
for dbname in ('catalog','profile'):
    sql=(BASE/'sql'/f'{dbname}.sql').read_text(encoding='utf-8')
    with sqlite3.connect(':memory:') as conn:
        conn.executescript(sql)
        names=[r[0] for r in conn.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name") if r[0] in LIFECYCLE]
        for name in names:
            scope='当前Windows账户安装目录内目录库' if dbname=='catalog' else '仅当前Profile用户；通过会话选库，其他用户库绝不ATTACH'
            dictionary += [f'## {dbname}.{name}', '',f'所属与生命周期：{scope}；{LIFECYCLE[name]}。','', '| 字段 | SQLite类型 | 空值/主键 | 默认值 | 含义 |','|---|---|---|---|---|']
            match=re.search(r'CREATE TABLE '+re.escape(name)+r'\s*\((.*?)\n\) STRICT;',sql,re.S)
            comments={}
            if match:
                for line in match[1].splitlines():
                    col=re.match(r'\s*(\w+)\s+\w+.*?--\s*(.*)',line)
                    if col: comments[col[1]]=col[2]
            columns=list(conn.execute(f'PRAGMA table_info("{name}")'))
            for _,col,typ,nonnull,default,pk in columns:
                dictionary.append(f'| {col} | {typ} | '+('PK' if pk else 'NOT NULL' if nonnull else 'NULL')+f' | {default if default is not None else "无"} | {comments.get(col,"FTS列，语义与SearchDocument同名列一致")} |')
            fks=list(conn.execute(f'PRAGMA foreign_key_list("{name}")'))
            dictionary += ['', '外键：'+ ('；'.join(f'{r[3]} → {r[2]}.{r[4]}（DELETE {r[6]}）' for r in fks) or '无；跨库关系由应用服务与补偿操作维护。')]
            indexes=list(conn.execute(f'PRAGMA index_list("{name}")'))
            details=[]
            for idx in indexes:
                cols=','.join(str(r[2]) for r in conn.execute(f'PRAGMA index_info("{idx[1]}")'))
                details.append(f'{idx[1]}({cols})'+(' UNIQUE' if idx[2] else '')+(' PARTIAL' if idx[4] else ''))
            dictionary += ['','索引/唯一约束：'+('；'.join(details) or '主键/FTS内建索引。'),'CHECK、过滤索引及触发器完整定义见SQL；不要从本表手工重建约束。','']
            mermaid += [f'  {name} {{']
            for _,col,typ,nonnull,default,pk in columns:
                key=' PK' if pk else ' FK' if any(r[3]==col for r in fks) else ''
                mermaid += [f'    {typ.lower()} {col}{key}']
            mermaid += ['  }']
            for fk in fks:
                if fk[1]==0: mermaid.append(f'  {fk[2]} ||--o{{ {name} : "{fk[3]}"')
(BASE/'DATA_DICTIONARY.md').write_text('\n'.join(dictionary)+'\n',encoding='utf-8')
(BASE/'diagrams').mkdir(exist_ok=True)
(BASE/'diagrams/er.mmd').write_text('\n'.join(mermaid)+'\n',encoding='utf-8')
print('Baseline verified: 35 FR (24 Must / 11 Should), 14 NFR, 10 BR, 12 original AC, 7 original CR. Both SQL files parse; dictionary and ER derived.')

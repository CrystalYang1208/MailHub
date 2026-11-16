"""Static/design contract checks only; never connects to a mailbox or reruns benchmarks."""
from pathlib import Path
from collections import Counter
from datetime import datetime, timezone
import hashlib,json,re,sqlite3
import yaml,jsonschema
BASE=Path(__file__).resolve().parents[1]
baseline=json.loads((BASE/'baseline-input.json').read_text(encoding='utf-8'))
api=yaml.safe_load((BASE/'openapi.yaml').read_text(encoding='utf-8'))
results=[]
def record(name,fn):
    try: details=fn();results.append({'check':name,'status':'PASS','details':details})
    except Exception as exc:results.append({'check':name,'status':'FAIL','errorType':type(exc).__name__,'detail':str(exc)[:300]})
def ensure(value,message):
    if not value:raise AssertionError(message)
def base_check():
    src=BASE.parents[2]/'docs/reference/需求分析与验收报告.docx'
    ensure(hashlib.sha256(src.read_bytes()).hexdigest()==baseline['sha256'],'Original requirement source changed')
    ensure(Counter(r[2] for r in baseline['fr'])=={'Must':24,'Should':11},'Priority count')
    return {'fr':35,'must':24,'should':11,'nfr':len(baseline['nfr']),'br':len(baseline['br']),'originalAC':len(baseline['ac']),'originalCR':len(baseline['cr']),'sourceUnchanged':True}
record('baseline-source-priorities',base_check)

operations=[(path,method,op) for path,item in api['paths'].items() for method,op in item.items()]
def api_check():
    ensure(api['openapi'].startswith('3.1.'),'OpenAPI version')
    ids=[o['operationId'] for _,_,o in operations];ensure(len(ids)==len(set(ids)),'Duplicate operationId')
    seen=set()
    def walk(value):
        if isinstance(value,dict):
            if '$ref' in value:
                ref=value['$ref'];ensure(ref.startswith('#/'),'Nonlocal ref');x=api
                for part in ref[2:].split('/'):x=x[part.replace('~1','/').replace('~0','~')]
                seen.add(ref)
            for x in value.values():walk(x)
        elif isinstance(value,list):
            for x in value:walk(x)
    walk(api)
    for path,method,op in operations:
        names=set(re.findall(r'\{(\w+)\}',path));params={p['name'] for p in op.get('parameters',[]) if p['in']=='path'}
        ensure(names==params,'Path parameters '+op['operationId'])
        ensure(bool(op['responses']) and 'x-requirements' in op,'Missing response/trace')
        ensure(set(op['x-requirements'])<={r[0] for r in baseline['fr']},'Unknown FR')
    for name,schema in api['components']['schemas'].items():jsonschema.Draft202012Validator.check_schema(schema)
    return {'operations':len(operations),'schemas':len(api['components']['schemas']),'resolvedLocalReferences':len(seen),'pathParametersValid':True,'scope':'OAS structure and JSON Schema checks; not third-party full OAS linter or running API'}
record('openapi-local-refs-and-schemas',api_check)
def example_check():
    def validator(name):return jsonschema.Draft202012Validator({'$ref':'#/components/schemas/'+name,'components':api['components']},format_checker=jsonschema.FormatChecker())
    cred=validator('CredentialInput')
    for valid in ({'type':'oauth2','oauthFlowId':'00000000-0000-4000-8000-000000000001'},{'type':'authorization-code','secret':'synthetic-only'}):ensure(not list(cred.iter_errors(valid)),'Valid credential DTO rejected')
    for invalid in ({'type':'oauth2'},{'type':'oauth2','secret':'synthetic-only','oauthFlowId':'00000000-0000-4000-8000-000000000001'},{'type':'password'}):ensure(bool(list(cred.iter_errors(invalid))),'Invalid credential combination accepted')
    resolution=validator('SubmissionResolution')
    ensure(not list(resolution.iter_errors({'decision':'allow-new-submission','confirmPossibleDuplicate':True})),'Confirmed resolution rejected')
    ensure(bool(list(resolution.iter_errors({'decision':'allow-new-submission','confirmPossibleDuplicate':False}))),'Unconfirmed duplicate risk accepted')
    search=api['paths']['/search']['get'];ensure(next(p for p in search['parameters'] if p['name']=='q')['required'],'Search q not required')
    return {'credentialOneOf':True,'unknownResolutionRequiresExplicitRisk':True,'searchQueryRequired':True,'scope':'Schema examples only; no credentials or API requests'}
record('schema-input-boundaries',example_check)
def trace_check():
    text=(BASE/'REQUIREMENT_TRACEABILITY.md').read_text(encoding='utf-8');test=(BASE/'TEST_PLAN.md').read_text(encoding='utf-8')
    for fr,desc,prio,criterion in baseline['fr']:
        ensure(f'| {fr} | {desc} | {prio} |' in text,'FR row changed '+fr)
        ensure(any(fr in o['x-requirements'] for _,_,o in operations),'FR no API '+fr)
        ensure('T-'+fr in test,'FR no test '+fr)
    for ac,fr,g,w,t in baseline['ac']:ensure(f'| {ac} | {fr} | {g} | {w} | {t} |' in test,'Original AC altered '+ac)
    for nfr,*_ in baseline['nfr']:ensure('T-'+nfr in test,'No NFR test')
    for br,*_ in baseline['br']:ensure('T-'+br in test,'No BR test')
    for i in range(1,9):ensure(f'CR-{i:02}' in text,'No CR')
    return {'all35FRToAPIAndTest':True,'original12ACVerbatim':True,'all14NFRAnd10BRMapped':True,'cr01to08Present':True}
record('requirement-traceability',trace_check)

def sql_check():
    counts={}
    for kind in ('catalog','profile'):
        c=sqlite3.connect(':memory:',isolation_level=None);c.executescript((BASE/'sql'/f'{kind}.sql').read_text(encoding='utf-8'))
        counts[kind]=c.execute("select count(*) from sqlite_master where type='table' and name not like 'sqlite_%'").fetchone()[0]
        ensure(not c.execute('PRAGMA foreign_key_check').fetchall(),'FK check')
        if kind=='catalog':
            for i in range(1,5):c.execute("INSERT INTO LocalUser(user_id,slot,display_name,normalized_name,password_salt,password_verifier,password_kdf,password_iterations,state,created_at_ms,updated_at_ms) VALUES(?,?,?,?,?,?,'pbkdf2-sha256',600000,'active',0,0)",(str(i),i,str(i),str(i),b'x'*32,b'x'*32))
            denied=False
            try:c.execute("INSERT INTO LocalUser(user_id,slot,display_name,normalized_name,password_salt,password_verifier,password_kdf,password_iterations,state,created_at_ms,updated_at_ms) VALUES('5',5,'5','5',?,?,'pbkdf2-sha256',600000,'active',0,0)",(b'x'*32,b'x'*32))
            except sqlite3.IntegrityError:denied=True
            ensure(denied,'Fifth user not blocked')
            c.execute("INSERT INTO AccountReservation VALUES('a','1',?,'active',0,NULL)",(b'a'*32,))
            denied=False
            try:c.execute("INSERT INTO AccountReservation VALUES('b','2',?,'active',0,NULL)",(b'a'*32,))
            except sqlite3.IntegrityError:denied=True
            ensure(denied,'Duplicate reservation not blocked')
        else:
            c.execute("INSERT INTO Profile VALUES('user',1,0)");c.execute('INSERT INTO CacheQuota VALUES(1,100,0)')
            c.execute("INSERT INTO ProfileSettings(singleton,updated_at_ms) VALUES(1,0)")
            c.execute("INSERT INTO MailAccount(account_id,owner_id,address,provider_key,auth_type,imap_host,imap_port,imap_security,smtp_host,smtp_port,smtp_security,status,created_at_ms,updated_at_ms) VALUES('a','user','synthetic@example.invalid','generic','password','imap.example.invalid',993,'tls','smtp.example.invalid',465,'tls','authorized',0,0)")
            c.execute("INSERT INTO Folder(folder_id,account_id,remote_name,role,uid_validity,state) VALUES('f','a','INBOX','inbox',1,'active')")
            for i in (1,2):
                c.execute("INSERT INTO Message(message_id,account_id,folder_id,uid_validity,uid,subject,sender_json,recipients_json,internal_date_ms,is_read,flags_json,size_bytes,has_attachments,body_index_state,server_state) VALUES(?,'a','f',1,?,'synthetic','[]','{}',0,0,'[]',0,0,'pending','present')",(str(i),i))
                c.execute("INSERT INTO SearchDocument VALUES(?,'synthetic','fixture','needle',0)",(i,))
            ensure(c.execute("SELECT count(*) FROM MessageSearch WHERE MessageSearch MATCH 'needle'").fetchone()[0]==2,'FTS insert trigger')
            c.execute("INSERT INTO MessageBody VALUES(1,?,?,60,0)",(b'x'*60,b''))
            denied=False
            try:c.execute('INSERT INTO MessageBody VALUES(2,?,?,50,0)',(b'x'*50,b''))
            except sqlite3.IntegrityError:denied=True
            ensure(denied and c.execute('SELECT used_bytes FROM CacheQuota').fetchone()[0]==60,'Quota guard not atomic')
            c.execute('BEGIN IMMEDIATE');c.execute('DELETE FROM MessageBody WHERE message_row_id=1');c.execute('INSERT INTO MessageBody VALUES(2,?,?,50,0)',(b'x'*50,b''));c.execute('COMMIT')
            ensure(c.execute('SELECT used_bytes FROM CacheQuota').fetchone()[0]==50,'Quota trigger ledger')
            c.execute("INSERT INTO Draft(draft_id,account_id,mode,to_json,subject,body_text,state,created_at_ms,updated_at_ms) VALUES('d','a','new','[]','synthetic','synthetic','unknown',0,0)")
            def submission(id,key):c.execute("INSERT INTO OutboxSubmission(submission_id,draft_id,draft_version,idempotency_key,request_hash,client_message_id,state,created_at_ms,updated_at_ms) VALUES(?,'d',1,?,?,?,'unknown',0,0)",(id,key,b'x'*32,id+'@example.invalid'))
            submission('s1','k1');denied=False
            try:submission('s2','k2')
            except sqlite3.IntegrityError:denied=True
            ensure(denied,'Unresolved unknown allowed duplicate active submission')
            c.execute("UPDATE OutboxSubmission SET resolution='allow-new-submission',resolved_at_ms=1 WHERE submission_id='s1'");submission('s2','k2')
            ensure(c.execute("SELECT count(*) FROM OutboxSubmission WHERE submission_id='s1' AND state='unknown'").fetchone()[0]==1,'Unknown history overwritten')
            denied=False
            try:c.execute("UPDATE Folder SET uid_validity=2 WHERE folder_id='f'")
            except sqlite3.IntegrityError:denied=True
            ensure(denied,'Old messages retagged by folder validity update')
            c.execute("DELETE FROM MailAccount WHERE account_id='a'")
            ensure(c.execute('SELECT used_bytes FROM CacheQuota').fetchone()[0]==0,'Cascade quota release')
            ensure(c.execute("SELECT count(*) FROM MessageSearch WHERE MessageSearch MATCH 'needle'").fetchone()[0]==0,'FTS cascade delete trigger')
            ensure(not c.execute('PRAGMA foreign_key_check').fetchall(),'Profile FK violations')
        c.close()
    return {'tablesIncludingFTSInternal':counts,'fourSlotAndUniqueAddressConstraints':True,'newDDLQuotaTriggerAndFTSCascade':True,'unknownResolutionConstraint':True,'generationChangeGuard':True,'scope':'Tiny in-memory SQL constraint sanity; no pressure/performance/UIDVALIDITY algorithm/mailbox tests; WAL runtime not tested here','checkerSQLite':sqlite3.sqlite_version}
record('new-sql-contract-sanity',sql_check)

def columns_check():
    expected={'MailAccount':['account_id','owner_id','address','provider_key','auth_type','imap_host','imap_port','imap_security','smtp_host','smtp_port','smtp_security','enabled','status','last_error_code','auth_cooldown_until_ms'],
    'Folder':['folder_id','account_id','remote_name','role','state','unread_count','uid_validity','last_uid','epoch'],
    'Message':['message_row_id','message_id','account_id','folder_id','subject','sender_json','reply_to_json','recipients_json','internal_date_ms','sent_at_ms','is_read','has_attachments','body_index_state','version','rfc_message_id','in_reply_to','references_json'],
    'Draft':['draft_id','account_id','mode','source_message_id','source_headers_json','to_json','cc_json','bcc_json','subject','body_text','body_html','state','version','updated_at_ms','last_error_code'],
    'ProfileSettings':['sync_interval_minutes','auto_sync_paused','google_oauth_client_dpapi'],
    'CacheQuota':['limit_bytes','used_bytes'],'OutboxSubmission':['submission_id','draft_id','state','client_message_id','observed_message_id','last_error_code','updated_at_ms'],
    'SyncJob':['job_id','account_id','folder_id','kind','state','attempt','next_run_at_ms','progress_done','progress_total','last_error_code']}
    with sqlite3.connect(':memory:') as c:
        c.executescript((BASE/'sql/profile.sql').read_text(encoding='utf-8'))
        for table,names in expected.items():ensure(set(names)<={r[1] for r in c.execute(f'PRAGMA table_info({table})')},'DTO map nonexistent column '+table)
    return {'mappedCoreTables':len(expected),'coreMappingColumnsExist':True,'scope':'Explicit DTO mapping column check; business semantics additionally reviewed in docs'}
record('dto-to-sql-fields',columns_check)
def links_check():
    required=['TECHNICAL_DESIGN.md','SYSTEM_ARCHITECTURE.md','MODULE_DESIGN.md','DATABASE_DESIGN.md','API_DESIGN.md','SYNC_ENGINE_DESIGN.md','EMAIL_INTEGRATION_DESIGN.md','SECURITY_DESIGN.md','SEARCH_STORAGE_DESIGN.md','ERROR_HANDLING_DESIGN.md','REQUIREMENT_TRACEABILITY.md','TEST_PLAN.md','STAGE3_DESIGN_REVIEW.md']
    ensure(all((BASE/p).is_file() for p in required),'Missing requested document')
    ensure(len(list((BASE/'ADR').glob('*.md')))==8,'ADR count')
    diagrams=0;links=0
    for p in BASE.rglob('*.md'):
        txt=p.read_text(encoding='utf-8');ensure(txt.count('```')%2==0,'Unbalanced code fence '+p.name)
        for target in re.findall(r'\[[^\]]+\]\(([^)]+)\)',txt):
            if re.match(r'(https?://|#|/|[A-Za-z]:)',target):continue
            target=target.split('#',1)[0]
            if target:ensure((p.parent/target).exists(),'Broken link '+p.name+': '+target);links+=1
        if p.parent==BASE:
            for i,block in enumerate(re.findall(r'```mermaid\s*\n(.*?)```',txt,re.S),1):
                ensure(block.strip().startswith(('flowchart','sequenceDiagram','stateDiagram-v2','erDiagram')),'Unknown diagram type')
                (BASE/'diagrams'/f'{p.stem.lower()}-{i}.mmd').write_text(block.rstrip()+'\n',encoding='utf-8');diagrams+=1
    return {'requiredDocuments':len(required),'adrs':8,'linksChecked':links,'extractedMermaidBlocks':diagrams,'mermaidScope':'Source/fence/type review and exported .mmd; no external Mermaid renderer run'}
record('deliverables-links-and-diagram-sources',links_check)
report={'utc':datetime.now(timezone.utc).isoformat(),'scope':'Stage-three design checks only; not product functional acceptance; no prior stage benchmarks rerun','results':results,'status':'PASS' if all(r['status']=='PASS' for r in results) else 'FAIL'}
(BASE/'design-checks.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2))
raise SystemExit(0 if report['status']=='PASS' else 1)

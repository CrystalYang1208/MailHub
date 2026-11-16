"""Generate OpenAPI 3.1 and human inventory from a single operation registry."""
from pathlib import Path
import json, re, yaml
BASE=Path(__file__).resolve().parents[1]
S={}
def ref(name): return {'$ref':'#/components/schemas/'+name}
def string(**kw): return {'type':'string',**kw}
def integer(**kw): return {'type':'integer',**kw}
def array(item): return {'type':'array','items':item}
def obj(props,required=()): return {'type':'object','properties':props,'required':list(required),'additionalProperties':False}
def enum(*values): return string(enum=list(values))
def add(name,props,required=()): S[name]=obj(props,required)
ID=string(format='uuid'); UTC=string(format='date-time',description='RFC3339 UTC，必须以Z结尾')
BOOL={'type':'boolean'}; NONNEG=integer(minimum=0); VERSION=integer(minimum=1)
add('Address',{'name':string(maxLength=256),'address':string(format='email',maxLength=320)},['address'])
ADDRESSES=array(ref('Address')); ADDRESSES['maxItems']=100
add('User',{'userId':ID,'displayName':string(),'state':enum('provisioning','active','deleting')},['userId','displayName','state'])
add('Session',{'user':ref('User'),'sessionToken':string(writeOnly=False,description='一次返回，仅浏览器内存；不写日志'),'expiresAt':UTC,'authVersion':VERSION},['user','sessionToken','expiresAt','authVersion'])
add('RegisterRequest',{'displayName':string(minLength=1,maxLength=40),'password':string(minLength=10,maxLength=128,writeOnly=True)},['displayName','password'])
add('LoginRequest',{'userId':ID,'password':string(minLength=1,maxLength=128,writeOnly=True)},['userId','password'])
add('PasswordChange',{'currentPassword':string(writeOnly=True,minLength=1),'newPassword':string(writeOnly=True,minLength=10,maxLength=128)},['currentPassword','newPassword'])
add('BridgeBootstrap',{'code':string(minLength=32,writeOnly=True)},['code'])
add('BridgeAccess',{'bridgeToken':string(),'expiresAt':UTC},['bridgeToken','expiresAt'])
add('ServerConfig',{'host':string(minLength=1,maxLength=253),'port':integer(minimum=1,maximum=65535),'security':enum('tls','starttls')},['host','port','security'])
add('CredentialInput',{'type':enum('oauth2','authorization-code','app-password','password'),'secret':string(writeOnly=True,maxLength=4096),'oauthFlowId':ID},['type'])
S['CredentialInput']['description']='oauth2必须oauthFlowId，其他方式必须secret；互斥；服务端校验，永不回显。'
S['CredentialInput']['oneOf']=[{'properties':{'type':{'const':'oauth2'}},'required':['oauthFlowId'],'not':{'required':['secret']}},{'properties':{'type':{'enum':['authorization-code','app-password','password']}},'required':['secret'],'not':{'required':['oauthFlowId']}}]
add('AccountValidationRequest',{'providerKey':enum('qq','163','gmail','generic'),'address':string(format='email'),'imap':ref('ServerConfig'),'smtp':ref('ServerConfig'),'credential':ref('CredentialInput')},['providerKey','address','imap','smtp','credential'])
add('AccountValidation',{'validationId':ID,'expiresAt':UTC,'capabilities':array(string())},['validationId','expiresAt','capabilities'])
add('AccountCreate',{'validationId':ID},['validationId'])
add('Account',{'accountId':ID,'address':string(format='email'),'providerKey':string(),'authType':string(),'imap':ref('ServerConfig'),'smtp':ref('ServerConfig'),'enabled':BOOL,'status':enum('authorized','syncing','retry','auth-required','disabled','removing'),'lastErrorCode':{'type':['string','null']},'cooldownUntil':{'type':['string','null'],'format':'date-time'}},['accountId','address','providerKey','authType','enabled','status'])
add('Provider',{'providerKey':string(),'label':string(),'imap':ref('ServerConfig'),'smtp':ref('ServerConfig'),'authTypes':array(string()),'guideSteps':array(string())},['providerKey','label','imap','smtp','authTypes','guideSteps'])
add('OAuthStart',{'providerKey':enum('gmail'),'address':string(format='email'),'accountId':ID},['providerKey','address'])
add('OAuthFlow',{'flowId':ID,'status':enum('pending','authorized','failed','expired'),'authorizationUrl':string(format='uri'),'expiresAt':UTC,'errorCode':{'type':['string','null']}},['flowId','status','expiresAt'])
add('OAuthClientImport',{'installed':obj({'client_id':string(),'client_secret':string(writeOnly=True),'project_id':string(),'auth_uri':string(),'token_uri':string(),'auth_provider_x509_cert_url':string(),'redirect_uris':array(string())},['client_id','client_secret'])},['installed'])
S['OAuthClientImport']['description']='本用户Google桌面客户端JSON；拒绝Web客户端及自定义认证/令牌端点；密文保存，不回显配置。'
add('Folder',{'folderId':ID,'accountId':ID,'name':string(),'role':enum('inbox','sent','trash','drafts','other'),'state':enum('active','rebuilding','missing','removing'),'unreadCount':NONNEG},['folderId','accountId','name','role','state','unreadCount'])
add('MessageSummary',{'messageId':ID,'accountId':ID,'folderId':ID,'subject':string(),'from':ADDRESSES,'receivedAt':UTC,'sentAt':{'type':['string','null'],'format':'date-time'},'isRead':BOOL,'hasAttachments':BOOL,'bodyCached':BOOL,'bodyIndexState':enum('pending','indexed','unavailable'),'version':VERSION},['messageId','accountId','folderId','subject','from','receivedAt','isRead','hasAttachments','bodyCached','bodyIndexState','version'])
add('Attachment',{'attachmentId':ID,'fileName':string(),'mimeType':string(),'declaredBytes':{'type':['integer','null'],'minimum':0},'downloaded':BOOL},['attachmentId','fileName','mimeType','downloaded'])
add('MessageDetail',{**S['MessageSummary']['properties'],'to':ADDRESSES,'cc':ADDRESSES,'replyTo':ADDRESSES,'attachments':array(ref('Attachment')),'rfcMessageId':{'type':['string','null']},'inReplyTo':{'type':['string','null']},'references':array(string())},S['MessageSummary']['required']+['to','cc','replyTo','attachments','references'])
add('Body',{'state':enum('ready','pending','offline-unavailable'),'text':string(),'html':string(description='未经信任的HTML；仅交安全阅读组件，不能放主DOM'),'cached':BOOL,'jobId':ID},['state','cached'])
add('Draft',{'draftId':ID,'accountId':ID,'mode':enum('new','reply','forward'),'sourceMessageId':ID,'to':ADDRESSES,'cc':ADDRESSES,'bcc':ADDRESSES,'subject':string(maxLength=998),'bodyText':string(),'bodyHtml':{'type':['string','null']},'state':enum('editing','queued','sending','failed','unknown','sent'),'version':VERSION,'updatedAt':UTC,'lastErrorCode':{'type':['string','null']},'attachments':array(ref('DraftFile'))},['draftId','accountId','mode','to','cc','bcc','subject','bodyText','state','version','updatedAt','attachments'])
add('DraftFile',{'fileId':ID,'fileName':string(),'mimeType':string(),'byteSize':integer(minimum=0,maximum=25000000)},['fileId','fileName','mimeType','byteSize'])
add('DraftCreate',{'accountId':ID,'to':ADDRESSES,'cc':ADDRESSES,'bcc':ADDRESSES,'subject':string(maxLength=998),'bodyText':string()},['accountId'])
add('DraftUpdate',{'to':ADDRESSES,'cc':ADDRESSES,'bcc':ADDRESSES,'subject':string(maxLength=998),'bodyText':string(),'bodyHtml':{'type':['string','null']}},['to','cc','bcc','subject','bodyText'])
add('DeriveDraft',{'accountId':ID,'comment':string()},['accountId'])
add('SendRequest',{'draftVersion':VERSION},['draftVersion'])
add('Submission',{'submissionId':ID,'draftId':ID,'state':enum('queued','preparing','submitting','accepted','failed','unknown'),'lastErrorCode':{'type':['string','null']},'serverAccepted':BOOL,'recipientDelivery':enum('not-observed'),'updatedAt':UTC},['submissionId','draftId','state','serverAccepted','recipientDelivery','updatedAt'])
S['Submission']['properties']['resolution']={'type':['string','null'],'enum':['confirm-accepted','allow-new-submission',None]}
add('SubmissionResolution',{'decision':enum('confirm-accepted','allow-new-submission'),'confirmPossibleDuplicate':BOOL},['decision'])
S['SubmissionResolution']['allOf']=[{'if':{'properties':{'decision':{'const':'allow-new-submission'}}},'then':{'properties':{'confirmPossibleDuplicate':{'const':True}},'required':['confirmPossibleDuplicate']}}]
add('Job',{'jobId':ID,'accountId':ID,'folderId':ID,'kind':string(),'state':enum('pending','running','retry','paused','auth-required','completed','failed','canceled'),'attempt':NONNEG,'nextRunAt':UTC,'progressDone':NONNEG,'progressTotal':{'type':['integer','null'],'minimum':0},'lastErrorCode':{'type':['string','null']}},['jobId','kind','state','attempt','nextRunAt','progressDone','progressTotal','lastErrorCode'])
add('Operation',{'operationId':ID,'state':enum('pending','running','done','failed'),'lastErrorCode':{'type':['string','null']}},['operationId','state'])
add('SyncRequest',{'accountId':ID,'folderId':ID},[])
S['SyncRequest']['description']='无accountId为当前用户全部启用账号；folderId必须属于accountId；清数据暂停后手动调用恢复自动补拉。'
add('SyncAccepted',{'jobs':array(ref('Job')),'coalesced':BOOL},['jobs','coalesced'])
add('ReadMutation',{'isRead':BOOL},['isRead'])
add('DownloadRequest',{'attachmentIds':{'type':'array','items':ID,'minItems':1,'maxItems':100}},['attachmentIds'])
add('DownloadBatch',{'batchId':ID,'state':enum('pending','running','completed','failed','canceled'),'receivedBytes':integer(minimum=0,maximum=200000000),'items':array(obj({'attachmentId':ID,'state':string(),'errorCode':{'type':['string','null']}},['attachmentId','state']))},['batchId','state','receivedBytes','items'])
add('Settings',{'syncIntervalMinutes':integer(minimum=5,maximum=60),'autoSyncPaused':BOOL,'cacheLimitBytes':integer(minimum=1),'cacheUsedBytes':NONNEG,'indexBytes':NONNEG,'attachmentBytes':NONNEG,'hasGoogleOAuthClient':BOOL},['syncIntervalMinutes','autoSyncPaused','cacheLimitBytes','cacheUsedBytes','indexBytes','attachmentBytes','hasGoogleOAuthClient'])
S['Settings']['properties']['indexBytes']={**NONNEG,'description':'SearchDocument UTF8逻辑载荷字节数，不冒充FTS物理大小或总盘占用'}
S['Settings']['properties'].update({'databaseBytes':NONNEG,'walBytes':NONNEG})
S['Settings']['required']+=['databaseBytes','walBytes']
add('SettingsUpdate',{'syncIntervalMinutes':integer(minimum=5,maximum=60),'cacheLimitBytes':integer(minimum=1)},[])
S['SettingsUpdate']['minProperties']=1
add('Coverage',{'totalMessages':NONNEG,'bodyIndexed':NONNEG,'bodyPending':NONNEG,'bodyUnavailable':NONNEG,'complete':BOOL},['totalMessages','bodyIndexed','bodyPending','bodyUnavailable','complete'])
add('Ack',{'done':BOOL},['done'])
add('ErrorInfo',{'code':string(),'message':string(description='中文明确原因'),'action':string(description='中文下一步操作'),'retryAfterSeconds':NONNEG,'fieldErrors':array(obj({'field':string(),'code':string()},['field','code']))},['code','message','action'])
S['ErrorEnvelope']=obj({'ok':{'const':False},'data':{'type':'null'},'error':ref('ErrorInfo'),'requestId':ID},['ok','data','error','requestId'])
def envelope(data):return obj({'ok':{'const':True},'data':data,'error':{'type':'null'},'requestId':ID},['ok','data','error','requestId'])
def page(item,search=False):
    props={'items':array(ref(item)),'nextCursor':{'type':['string','null']},'hasMore':BOOL}
    if search:props['coverage']=ref('Coverage')
    return obj(props,list(props))
OPS=[]
def op(method,path,id,module,fr,summary,request=None,result='Ack',status=200,bridge_only=False,public=False,paging=False,extra=(),errors=(),priority=None,binary=False,multipart=False):
    OPS.append(dict(method=method,path=path,id=id,module=module,fr=fr.split(),summary=summary,request=request,result=result,status=status,bridge_only=bridge_only,public=public,paging=paging,extra=list(extra),errors=list(errors),priority=priority,binary=binary,multipart=multipart))
def query(name,schema,desc):return {'name':name,'in':'query','required':False,'schema':schema,'description':desc}
def header(name,schema,desc):return {'name':name,'in':'header','required':True,'schema':schema,'description':desc}
FILTERS=[query('accountId',ID,'当前用户账号'),query('folderId',ID,'文件夹必须属于所选账号'),query('from',UTC,'收信时间下界（含）'),query('to',UTC,'收信时间上界（不含）'),query('isRead',BOOL,'服务端已确认的已读值'),query('hasAttachments',BOOL,'附件过滤')]
op('post','/bridge/bootstrap','bootstrapBridge','Security','FR-F01','一次性启动引导码换进程令牌','BridgeBootstrap','BridgeAccess',public=True,errors=['BOOTSTRAP_EXPIRED','BOOTSTRAP_REPLAY'])
op('get','/users','listUsers','Settings','FR-F01','列出最多四个本地用户名称，不返回密钥/账号',result=array(ref('User')),bridge_only=True)
op('post','/users','registerUser','Settings','FR-F01','注册本地用户并初始化密钥与用户库','RegisterRequest','User',201,bridge_only=True,errors=['USER_LIMIT','NAME_EXISTS'])
op('post','/sessions','login','Security','FR-F01','密码认证并解封主密钥','LoginRequest','Session',bridge_only=True,errors=['LOGIN_INVALID','LOGIN_COOLDOWN','KEY_UNAVAILABLE'])
op('get','/sessions/current','getCurrentSession','Security','FR-F01','当前用户信息',result='User')
op('delete','/sessions/current','logout','Security','FR-F01','撤销会话、取消任务并清密钥')
op('post','/sessions/switch','switchUser','Security','FR-F01','先退出旧身份再验证目标密码；失败保持锁定','LoginRequest','Session',errors=['LOGIN_INVALID','LOGIN_COOLDOWN'])
op('put','/users/current/password','changePassword','Security','FR-F01','原子更换验证值和密钥封套并注销全部会话','PasswordChange',errors=['PASSWORD_INVALID','KEY_UNAVAILABLE'])
op('delete','/users/current','deleteUser','Settings','FR-F01','本人再次密码确认后清本地数据，非远程删信','LoginRequest','Operation',202,errors=['LOGIN_INVALID'])
op('get','/providers','listProviders','Account','FR-A01 FR-A02 FR-A03 FR-A04','模板与三步中文授权指引',result=array(ref('Provider')))
op('put','/settings/oauth-client','importOAuthClient','Account','FR-A03','导入本用户Google桌面客户端配置','OAuthClientImport',errors=['OAUTH_CLIENT_INVALID'])
op('post','/oauth/flows','startOAuth','Account','FR-A03 FR-A07','创建桌面PKCE授权流程','OAuthStart','OAuthFlow',201,errors=['OAUTH_CLIENT_MISSING'])
op('get','/oauth/flows/{flowId}','getOAuthFlow','Account','FR-A03 FR-A07','查询授权状态，不返回邮箱令牌',result='OAuthFlow',errors=['OAUTH_FLOW_EXPIRED'])
op('post','/accounts/validations','validateAccount','Account','FR-A01 FR-A02 FR-A03 FR-A04 FR-A05 FR-A06','校验配置、授权及文件夹，返回短期绑定验证结果','AccountValidationRequest','AccountValidation',errors=['AUTH_FAILED','IMAP_DISABLED','SERVER_CONFIG','NETWORK_UNREACHABLE','AUTH_COOLDOWN','TLS_FAILED','PROVIDER_REJECTED'])
op('post','/accounts','addAccount','Account','FR-A01 FR-A06','消费验证结果并原子保存账号密文','AccountCreate','Account',201,errors=['ACCOUNT_EXISTS','VALIDATION_EXPIRED'])
op('get','/accounts','listAccounts','Account','FR-A02 FR-C02','本用户账号列表',result=array(ref('Account')))
op('get','/accounts/{accountId}','getAccount','Account','FR-A06','账号详情，绝不返回凭据',result='Account')
op('patch','/accounts/{accountId}','setAccountEnabled','Account','FR-A09','仅修改启停，不恢复已取消显示名/同步范围功能',obj({'enabled':BOOL},['enabled']),'Account',errors=['ACCOUNT_BUSY'])
op('post','/accounts/{accountId}/reauthorize','reauthorizeAccount','Account','FR-A07','验证并替换凭据，保留账号及邮件','CredentialInput','Account',errors=['AUTH_FAILED','AUTH_COOLDOWN'])
op('delete','/accounts/{accountId}','removeAccount','Account','FR-A08','取消任务并清账号、凭据、邮件、索引和关联文件',result='Operation',status=202)
op('get','/accounts/{accountId}/folders','listFolders','Mail','FR-C07 FR-C08','文件夹及服务器确认未读数',result=array(ref('Folder')))
op('get','/messages','listMessages','Mail','FR-C01 FR-C02 FR-C07','统一收件箱默认聚合inbox，按收信时间倒序',result=page('MessageSummary'),paging=True,extra=FILTERS)
op('get','/messages/{messageId}','getMessage','Mail','FR-C03','头部详情和附件元数据',result='MessageDetail')
op('get','/messages/{messageId}/body','getMessageBody','Mail','FR-C03','缓存读取或入队BODY.PEEK，阅读不自动置已读',result='Body',errors=['STALE_GENERATION'])
op('patch','/messages/{messageId}/read','setMessageRead','Mail','FR-C05','排队修改服务器FLAGS，确认前保持服务器缓存值','ReadMutation','Job',202,errors=['AUTH_REQUIRED','ACCOUNT_DISABLED'])
op('post','/messages/{messageId}/trash','moveMessageToTrash','Mail','FR-C06','只移服务端垃圾箱，禁止永久删除',result='Job',status=202,errors=['TRASH_UNSUPPORTED','ALREADY_IN_TRASH','STALE_GENERATION'])
op('post','/messages/{messageId}/reply','createReply','Compose','FR-D02','按真实收到原邮件头派生回复草稿','DeriveDraft','Draft',201)
op('post','/messages/{messageId}/forward','createForward','Compose','FR-D02','派生独立转发草稿并携带引用，不合并会话','DeriveDraft','Draft',201)
op('post','/drafts','createDraft','Compose','FR-D01 FR-D03 FR-D04','创建独立本地草稿','DraftCreate','Draft',201)
op('get','/drafts','listDrafts','Compose','FR-D03 FR-D04','当前用户草稿列表',result=page('Draft'),paging=True)
op('get','/drafts/{draftId}','getDraft','Compose','FR-D03 FR-D04','恢复完整草稿',result='Draft')
op('put','/drafts/{draftId}','saveDraft','Compose','FR-D03 FR-D04','按版本保存草稿','DraftUpdate','Draft',extra=[header('If-Match',string(),'草稿ETag，例双引号内版本数字')],errors=['VERSION_CONFLICT','DRAFT_BUSY'])
op('delete','/drafts/{draftId}','deleteDraft','Compose','FR-D03','删除本人草稿；unknown提交必须先对账',extra=[header('If-Match',string(),'当前草稿版本')],errors=['DRAFT_BUSY','VERSION_CONFLICT'])
op('post','/drafts/{draftId}/attachments','uploadDraftAttachment','Compose','FR-D05','上传并持久化草稿附件；发送总解码附件<=25MB',obj({'file':string(format='binary')},['file']),'DraftFile',201,multipart=True,extra=[header('If-Match',string(),'当前草稿版本')],errors=['ATTACHMENT_TOO_LARGE','VERSION_CONFLICT','DRAFT_BUSY'])
op('delete','/drafts/{draftId}/attachments/{fileId}','removeDraftAttachment','Compose','FR-D05','解绑草稿附件并排队清理无引用对象',extra=[header('If-Match',string(),'当前草稿版本')],errors=['DRAFT_BUSY','VERSION_CONFLICT'])
op('post','/drafts/{draftId}/send','sendDraft','Compose','FR-D01 FR-D02 FR-D04 FR-D05','冻结草稿并建立幂等发送提交','SendRequest','Submission',202,extra=[header('Idempotency-Key',string(minLength=16,maxLength=128),'同一冻结版本重试HTTP请求必须复用')],errors=['VERSION_CONFLICT','IDEMPOTENCY_CONFLICT','DRAFT_BUSY','ATTACHMENT_TOO_LARGE','AUTH_REQUIRED'])
op('get','/submissions/{submissionId}','getSubmission','Compose','FR-D04','查询SMTP接受/明确失败/结果未知，不冒充收件端送达',result='Submission')
op('post','/submissions/{submissionId}/resolve','resolveSubmission','Compose','FR-D04','仅本人处置unknown；允许新提交必须确认可能重复，原unknown事实保留','SubmissionResolution','Submission',errors=['SUBMISSION_NOT_UNKNOWN','DUPLICATE_RISK_NOT_CONFIRMED'])
op('post','/downloads','createDownloadBatch','Mail','FR-C04','创建单个或批量附件下载；25MB单项、200MB累计','DownloadRequest','DownloadBatch',202,errors=['DOWNLOAD_LIMIT','ATTACHMENT_TOO_LARGE'])
op('get','/downloads/{batchId}','getDownloadBatch','Mail','FR-C04','查询下载及中断原因',result='DownloadBatch')
op('get','/attachments/{attachmentId}/content','getAttachmentContent','Mail','FR-C04','下载已就绪附件，浏览器选择保存目录',binary=True,errors=['ATTACHMENT_NOT_READY'])
op('post','/sync','requestSync','Sync','FR-B01 FR-B02 FR-B03 FR-B04 FR-B05 FR-B06','单账号/全部手动同步，5秒内返回任务','SyncRequest','SyncAccepted',202,errors=['AUTH_REQUIRED','AUTH_COOLDOWN','ACCOUNT_DISABLED'])
op('get','/sync/jobs','listSyncJobs','Sync','FR-B03 FR-B05 FR-B07 FR-B08','任务状态、进度及脱敏错误',result=page('Job'),paging=True,extra=[query('accountId',ID,'按账号'),query('state',string(),'按状态')])
op('get','/sync/jobs/{jobId}','getSyncJob','Sync','FR-B05 FR-B07','任务状态与最后错误',result='Job')
op('post','/sync/jobs/{jobId}/retry','retrySyncJob','Sync','FR-B03 FR-B05','手动重置网络退避；不绕过认证冷却',result='Job',status=202,errors=['AUTH_COOLDOWN','AUTH_REQUIRED','JOB_NOT_RETRYABLE'])
op('get','/search','searchMessages','Search','FR-E01 FR-E02','当前用户跨账号本地搜索，返回正文覆盖状态',result=page('MessageSummary',True),paging=True,extra=[{**query('q',string(minLength=1,maxLength=256),'NFC、trim后的字面搜索词，拒绝空串'),'required':True}]+FILTERS,errors=['QUERY_INVALID','QUERY_TOO_EXPENSIVE','CURSOR_INVALID'])
op('get','/settings','getSettings','Settings','FR-B04 FR-F02 FR-F03','用户设置及缓存/索引/附件容量',result='Settings')
op('patch','/settings','updateSettings','Settings','FR-B04 FR-F02','更新同步间隔或在同事务淘汰后降低配额','SettingsUpdate','Settings',errors=['CACHE_QUOTA','STORAGE_FULL'])
op('delete','/settings/body-cache','clearBodyCache','Settings','FR-F02','只清阅读正文，保留头/索引/草稿')
op('post','/settings/mail-data/clear','clearMailData','Settings','FR-F03','清本地邮件和索引，保留账号/凭据/草稿并暂停自动补拉',obj({'confirm':{'const':True}},['confirm']),'Operation',202)
op('get','/operations/{operationId}','getOperation','Settings','FR-A08 FR-F01 FR-F03','查询当前用户异步清理状态',result='Operation')

baseline=json.loads((BASE/'baseline-input.json').read_text(encoding='utf-8')); priorities={r[0]:r[2] for r in baseline['fr']}
doc={'openapi':'3.1.1','info':{'title':'MailHub 本地桥接API设计合同','version':'1.0.0-design','description':'设计草案；127.0.0.1动态端口、同源、全中文错误。除bootstrap/登录前用户接口外，进程令牌和用户会话必须同时验证。不是现有原型API实现。'},'servers':[{'url':'http://127.0.0.1:{port}/api/v1','variables':{'port':{'default':'49152','description':'实际启动动态端口；不是固定生产端口'}}}],'security':[{'BridgeToken':[],'UserSession':[]}],'paths':{},'components':{'securitySchemes':{'BridgeToken':{'type':'http','scheme':'bearer','description':'进程级随机访问令牌，仅内存'},'UserSession':{'type':'apiKey','in':'header','name':'X-MailHub-Session','description':'用户密码认证后签发，服务端绑定身份，切换后失效'}},'schemas':S,'responses':{'Error':{'description':'统一错误；无资源归属统一404；校验失败422；会话失效401；Origin/Host拒绝403。','content':{'application/json':{'schema':ref('ErrorEnvelope')}}}}}}
inventory=['# 本地API详细设计','','所有路径相对于`/api/v1`。openapi.yaml为机器契约；下表从同一operation注册表生成，避免operationId/需求映射漂移。接口未实现。','','## 统一协议','','成功：`{ok:true,data:...,error:null,requestId:uuid}`；失败：`{ok:false,data:null,error:{code,message,action,...},requestId:uuid}`。message和action为中文。仅附件内容为带Content-Disposition的二进制例外；错误仍JSON。','','权限U=Bridge Bearer与X-MailHub-Session均有效；B=仅Bridge；P=一次性引导码。所有接口仍受Host/Origin约束。无效会话401，跨用户对象404，不通过差异错误泄露他人资源。','','游标limit默认50、范围1～200，稳定倒序；HMAC绑定用户/条件/位置/过期。搜索排序固定收信时间倒序，不提供CR-07已取消高亮或会话聚合。若期间有新数据，前端可刷新首屏，cursor不是跨时间不可变快照。','','| operationId | 方法 路径 | 请求/参数 | 成功data / HTTP | 权限 | 分页 | 需求/优先级 | 专项错误 |','|---|---|---|---|---|---|---|---|']
for o in OPS:
    params=[]
    for name in re.findall(r'\{(\w+)\}',o['path']): params.append({'name':name,'in':'path','required':True,'schema':ID,'description':'仅当前用户归属资源'})
    params+=o['extra']
    if o['paging']:params += [query('limit',integer(minimum=1,maximum=200,default=50),'每页条数'),query('cursor',string(),'不透明分页游标')]
    result=ref(o['result']) if isinstance(o['result'],str) else o['result']
    response={'description':o['summary'],'content':{'application/octet-stream':{'schema':string(format='binary')}} if o['binary'] else {'application/json':{'schema':envelope(result)}}}
    if o['binary']:response['headers']={'Content-Disposition':{'schema':string(),'description':'attachment；UTF8文件名安全编码，禁止inline'},'Cache-Control':{'schema':string(),'description':'no-store'}}
    operation={'operationId':o['id'],'tags':[o['module']],'summary':o['summary'],'x-requirements':o['fr'],'x-priority':'Must' if any(priorities[x]=='Must' for x in o['fr']) else 'Should','x-error-codes':o['errors'],'responses':{str(o['status']):response,'400':{'$ref':'#/components/responses/Error'},'401':{'$ref':'#/components/responses/Error'},'403':{'$ref':'#/components/responses/Error'},'404':{'$ref':'#/components/responses/Error'},'409':{'$ref':'#/components/responses/Error'},'413':{'$ref':'#/components/responses/Error'},'422':{'$ref':'#/components/responses/Error'},'429':{'$ref':'#/components/responses/Error'},'503':{'$ref':'#/components/responses/Error'}}}
    if params:operation['parameters']=params
    if o['public']:operation['security']=[]
    elif o['bridge_only']:operation['security']=[{'BridgeToken':[]}]
    if o['request']:
        schema=ref(o['request']) if isinstance(o['request'],str) else o['request']
        operation['requestBody']={'required':True,'content':{'multipart/form-data' if o['multipart'] else 'application/json':{'schema':schema}}}
    if o['id'] in ('saveDraft','getDraft'): response['headers']={'ETag':{'schema':string(),'description':'草稿version对应强ETag，修改时If-Match'}}
    doc['paths'].setdefault(o['path'],{})[o['method']]=operation
    request=o['request'] if isinstance(o['request'],str) else '结构见YAML' if o['request'] else '无body'
    request+='；'+', '.join(p['name'] for p in params) if params else ''
    resultname=o['result'] if isinstance(o['result'],str) else '结构见YAML'
    inventory.append(f"| {o['id']} | {o['method'].upper()} `{o['path']}` | {request} | {'binary' if o['binary'] else resultname} / {o['status']} | {'P' if o['public'] else 'B' if o['bridge_only'] else 'U'} | {'是' if o['paging'] else '否'} | {', '.join(o['fr'])} / {operation['x-priority']} | {', '.join(o['errors']) or '通用错误'} |")
inventory += ['','## 异常与交互约定','','所有请求有超时/取消；POST同步、发送、清理返回202只代表已持久入队。任务状态在GET接口查询；fetch事件流是可选提示，不作为交付可靠性的唯一通道。账号验证可耗时，前端显示步骤内进度；失败不创建半账号。API错误不透传服务端原文。','','`POST /drafts/{draftId}/send`要求Idempotency-Key+draftVersion，同键同请求返回原提交，同键不同内容409；submitting/unknown不得建立第二活跃提交。SMTP accepted不等于收件端收到。草稿更新/附件上传返回新版本；版本冲突重新读取。','','所有GET无服务器状态修改；正文GET仅能入队幂等PEEK读取，不能隐式标已读。服务端FLAGS确认前UI显示pending状态，isRead仍是服务器缓存值。垃圾箱操作不得调用永久EXPUNGE。','','附件上传/下载限制既检查声明长度，也按实际流式字节计数，未知大小不能绕过上限。浏览器以带令牌fetch接收单附件Blob并触发另存为；不使用携长期令牌的下载URL。批量顺序处理，释放前一个Blob，累计不超200MB。原始绝对路径不会进入API。','','OAuth系统浏览器回调使用独立短生命127.0.0.1动态监听器，state+PKCE验证；不使用桥接token、不要求Origin（Google导航无自定义头），不返回邮件令牌给页面。该协议回调不是本OpenAPI的认证业务端点；流程状态由getOAuthFlow读取。','','注册、登录、清理的JSON body及token不得记录；身份注册受最多4槽及限速约束。switch先失效旧会话，再认证新用户；失败后保持锁定，UI不能继续使用A会话。changePassword完成后全部会话撤销，返回Ack后重新登录。','','请求大小、队列饱和、磁盘不足、取消及服务商能力不足的中文提示/重试规则见ERROR_HANDLING_DESIGN。每个接口继承通用错误与归属检查；专用错误见x-error-codes。Should接口设计不表示首迭代已经实现或提升为Must。','']
(BASE/'openapi.yaml').write_text(yaml.safe_dump(doc,allow_unicode=True,sort_keys=False,width=110),encoding='utf-8')
(BASE/'API_DESIGN.md').write_text('\n'.join(inventory),encoding='utf-8')
(BASE/'operation-registry.json').write_text(json.dumps(OPS,ensure_ascii=False,indent=2),encoding='utf-8')
print(f'Generated {len(OPS)} operations and {len(S)} schemas, no HTTP requests executed.')

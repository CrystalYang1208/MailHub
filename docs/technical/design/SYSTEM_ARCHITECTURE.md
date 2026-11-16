# 系统总体架构

MailHub是Windows11用户级单宿主的模块化本地应用。Chrome/Edge访问宿主同时提供的静态Vue资源和API；浏览器无IMAP/SMTP或DPAPI能力，也不持有邮箱凭据。启动路径为快捷方式→宿主单实例检查→绑定127.0.0.1动态端口→生成一次性引导链接→系统浏览器→应用用户密码登录。

## 总体边界

```mermaid
flowchart LR
  Browser[Chrome / Edge 中文Vue界面] -->|同源HTTP JSON / fetch事件流| Host[本地桥接 127.0.0.1]
  Host --> Security[会话授权与用户密钥租约]
  Host --> App[Account / Sync / Mail / Compose / Search / Settings]
  App --> Ports[协议 仓储 凭据 时钟 文件接口]
  Ports --> MailKit[MailKit / MimeKit适配器]
  MailKit -->|严格TLS IMAP / SMTP| Provider[QQ / 163 / Gmail / 标准邮箱]
  Ports --> OAuth[Google桌面OAuth / 刷新]
  OAuth --> Google[Google必要认证端点]
  Ports --> Sqlite[每应用用户SQLite / FTS5]
  Ports --> Files[每应用用户文件对象与临时目录]
  Security --> Catalog[目录库 用户 密钥封套 唯一地址预留]
  Security --> DPAPI[Windows DPAPI CurrentUser]
```

邮件协议及必要Google认证是用户授权后的出网例外；不向运营方上传数据，不使用CDN/云AI/遥测。账号凭据解密只在建立协议连接或OAuth认证请求的短生命周期中发生；用户登录只解封主密钥，不解密所有凭据。

## 分层与模块依赖

```mermaid
flowchart TB
  UI[展示层 Vue组件与状态] --> Web[桥接层 Endpoint DTO 验证 错误映射]
  Web --> UseCase[应用层 用例与事务编排]
  UseCase --> Domain[领域层 邮件身份 状态机 业务规则]
  UseCase --> Port[端口 接口与不可变UserContext]
  Infra[协议接入与数据访问实现] --> Port
  Infra --> Persist[持久化 SQLite 文件 DPAPI]
  Root[Host组合根与后台任务] --> Web
  Root --> UseCase
  Root --> Infra
```

```mermaid
flowchart LR
 Account --> Security
 Account --> Storage
 Account --> MailGateway
 Sync --> AccountPort[Account只读状态端口]
 Sync --> Security
 Sync --> MailGateway
 Sync --> Storage
 Mail --> Storage
 Mail --> SyncPort[Sync命令入队端口]
 Compose --> Security
 Compose --> Storage
 Compose --> MailGateway
 Search --> Storage
 Settings --> Security
 Settings --> Storage
 Settings --> JobControl[任务取消与租约端口]
 Security --> CatalogStore[目录库与DPAPI适配]
```

业务模块不直接引用其他模块的私有仓储，不互相调用Controller。事务协调通过Application的UnitOfWork把头/索引/游标一次提交；Search既不调用邮件服务器也不以搜索请求偷偷拉正文。Host是唯一DI组合根，Domain不依赖MailKit、HTTP、SQLite或Vue。

## 核心数据流

```mermaid
flowchart TD
 A[认证会话得到UserContext] --> B[查询本用户邮件头]
 B --> C[先呈现缓存列表]
 A --> D[同步调度取得账号连接租约]
 D --> E[IMAP UID与邮件头 网络阶段]
 E --> F[短事务 头 SearchDocument 游标 Job]
 F --> G[发布不含正文的进度事件]
 C --> H[阅读正文]
 H --> I{正文缓存命中}
 I -->|是| J[脱离数据库快照的正文缓冲]
 I -->|否且在线| K[BODY.PEEK 拉取 MIME解码]
 K --> L[同事务LRU与缓存写入 / 索引更新]
 L --> J
 J --> M[DOMPurify清洗 sandbox空权限 CSP]
 D --> N[后台低优先级全文正文建索引]
 N --> F
```

## 生命周期和失效边界

- 按Windows SID命名的互斥量维持单宿主，锁文件不含长期令牌。第二次启动通过仅当前SID可访问的命名管道请求已有宿主生成新引导码，不再开第二写宿主。命名管道消息仅OpenWorkspace/Shutdown，不传邮件。
- 不是Windows Service，无管理员安装前提，无开机自启。浏览器关闭不终止宿主；托盘“退出/锁定”取消任务、撤销会话、清密钥。一次活跃应用用户的约束适用于所有标签页，切换使旧会话全失效。
- 正常退出：停止接单→取消可取消网络I/O→等待短DB提交→释放租约→将未完成任务持久化→清密钥→停止Kestrel。SMTP submitting无法安全判定时持久化unknown，不因退出自动重发。
- 异常退出：重启先锁定；用户登录后回收过期任务租约、核对清理日志、恢复retry。锁定用户不解密凭据、不后台同步；只作不需用户密钥的目录级恢复。
- 事件流只是进度提示，断连不丢任务；客户端重新GET状态。服务端提交后才发事件，事件不含地址、正文或令牌。可退化成2秒状态轮询，避免把UI通信变成核心任务依赖。

## 添加账号流程

```mermaid
sequenceDiagram
 participant UI as 中文三步向导
 participant A as Account应用服务
 participant C as Catalog
 participant P as Provider适配器
 participant D as 用户数据库
 UI->>A: 模板/通用配置与凭据（当前会话）
 A->>C: 原子预留地址指纹
 A->>P: 验证TLS 认证 文件夹能力
 alt 失败
 P-->>A: 分类错误
 A->>C: 释放本次预留或保持冷却记录
 A-->>UI: 中文原因和下一步
 else 成功
 A->>D: 单事务写账号和加密凭据
 A->>C: 预留置active
 A->>D: 幂等创建首次同步任务
 A-->>UI: 账号与jobId（同步状态≤5秒反馈）
 end
```

目录库与用户库不采用跨库“全局事务”。每步有CatalogOperation持久状态，按账号UUID幂等补偿；reserved地址不能被另一个用户抢用，过期清理必须先确认用户库无对应账号。完整SQL及恢复策略见DATABASE_DESIGN。

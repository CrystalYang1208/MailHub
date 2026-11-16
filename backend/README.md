> 以下为Starter工程说明。当前业务已实现，运行与验证以项目根README和docs/course为准。

# 后端工程

按Host→Application、Infrastructure→Application→Domain单向引用。协议、安全、持久化适配器置于Infrastructure内，当前只有工程迁移与合成数据读取；不创建完整业务Repository。MailKit/MimeKit仅依赖与程序集边界核对，不连接邮箱。

Host仅127.0.0.1动态监听；默认工程模式关闭，工程路由需MAILHUB_ENGINEERING=1。真实生产认证、解锁、同步等留给阶段五。无令牌401，Host/Origin不可信403；只记录异常类型，统一错误信封符合合同结构。/_health无敏感数据。

迁移见database/README。dotnet build开启.NET analyzers并把编译警告视为错误；网络/吊销/漏洞数据源不可达警告保留并单独评估，不抑制已知漏洞警告。

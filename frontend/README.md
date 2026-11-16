> 以下为Starter工程说明。当前业务已实现，运行与验证以项目根README和docs/course为准。

# 前端

从MailHub根目录npm ci --ignore-scripts。npm run dev --workspace frontend仅UI/HMR；真实工程联通使用根目录tools/Start-Engineering.ps1构建后同源提供静态资源与API。Element Plus按需引入Button及对应CSS。

API客户端默认要求进程令牌与用户会话；工程路由显式使用仅进程令牌模式。令牌只存内存，引导码先从fragment取出并清除，再安装hash路由。前端不能直连邮箱。生成类型勿手改；修改批准契约必须先有设计审查。

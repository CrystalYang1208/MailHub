# 开发环境与运行

目标Windows11（build >=22000），最新版Chrome/Edge。Node24.20.0/npm11.19.0；SDK10.0.404由global.json锁定，运行时10.0.15。Git当前2.33.1。npm锁文件位于项目根目录，覆盖frontend工作区；NuGet使用Central Package Management与各项目packages.lock.json。不要在frontend再生成第二套锁。

## 安装与检查

```powershell
node --version
npm --version
dotnet --info
git --version
npm ci --ignore-scripts
dotnet restore backend/MailHub.slnx --locked-mode
```

默认npm源为registry.npmjs.org，本项目.npmrc缓存仅在项目.npm-cache。官方NuGet在当前电脑存在TLS EOF，备用方案：

```powershell
dotnet restore backend/MailHub.slnx --configfile backend/NuGet.Mirror.Config --locked-mode
```

该备用源为Huawei公开镜像，强制NuGet.org仓库签名并固定Microsoft公布的证书指纹，未关闭TLS和证书检查。网络导致官方漏洞数据/吊销状态不可达时，警告必须保留。不要修改全局代理或证书。仅为了构建可使用Test-Quality的 -Mirror -BuildOnlyRestore；这明确跳过本次还原的漏洞查询，不能记作NuGet安全审计通过。常规CI仍用官方源及审计。

## 开发、集成与停止

```powershell
npm run dev --workspace frontend
# 上面仅Vite UI/HMR，不放宽后台Origin/Host，不能完成同源API联通。
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Start-Engineering.ps1 -Open
# 已构建时加 -NoBuild。Chrome或Edge打开同一启动链接即可；单次码只能被一个页面消费。
```

脚本构建dist和Host，后台隐藏启动，以动态127.0.0.1端口同源服务。启动信息写 .local/runs/<id>/startup.json，其中url是私有短期链接，不复制到聊天/Git。进程记录process.json用于停止：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Stop-Engineering.ps1 -ProcessFile .local/runs/<id>/process.json
```

只停止记录中经路径和命令行核对的MailHub Host；不停止其他dotnet/代理程序。页面未显示时检查同目录stdout/stderr，本阶段日志不含邮箱内容。

## 质量门禁

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-Quality.ps1
# 当前受限网络电脑的已知替代：
powershell -NoProfile -ExecutionPolicy Bypass -File tools/Test-Quality.ps1 -Mirror -BuildOnlyRestore
```

默认npm ci；复核本机现有安装可加-SkipInstall；未安装浏览器可临时-SkipBrowser但必须将浏览器项标记NOT_TESTED。测试证据 .local/stage4；无任何私有数据自动上传。Windows临时目录锁问题通过为测试进程设置项目.local/temp解决，不修改系统TEMP。

开发库.local/development，后端测试随机库，浏览器每例独立随机库。绝不指向真实profile。MigrationRunner先实际查询sqlite_version()并阻止未修复WAL版本，然后应用批准迁移；无需重复阶段二性能测试。

## API契约约定

npm run contracts:lint 校验OpenAPI3.1；npm run contracts:types生成类型；npm run contracts:check复核来源字节、引用、51operationId唯一、35FR及生成类型。后端测试覆盖统一错误信封。/_engineering/*仅为工程验证；正式/api/v1/*尚未实现，清单implementedProductOperations为空。后续每个真实端点加入清单、对应operationId及请求/响应Schema测试后，才能计入业务完成。

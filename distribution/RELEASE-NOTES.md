MailHub 多邮箱聚合平台课程交付：Vue 3 + TypeScript + Element Plus 前端，.NET 10 后端，SQLite 本地存储，含 OpenSpec 需求、设计、任务及验收材料。

## 下载与运行

- **只想运行：下载 MailHub-Win11-x64.zip**，解压后双击“运行演示.cmd”。需要 Windows 11 x64，无需安装 Node 或 .NET SDK，演示不需要真实邮箱。
- MailHub-Source.zip 是原始交付源码；仓库 main 包含相同源码，并补充 GitHub 下载入口。
- MailHub-Course-Report.docx 为可编辑报告。SHA256SUMS.txt 用于完整性校验。

## 验证范围

交付时的合成测试记录：后端 26 项、前端单元 3 项、浏览器 7 项通过，详见 docs/course/verification。此次上传另行校验了交付压缩包 SHA256 和 ZIP 完整性，不表示重新运行了全部业务测试。

Windows 11 本机运行、DPAPI、真实测试邮箱收发与 Gmail 外部 OAuth 仍需按本机验收清单验证。Outlook 提供通用 IMAP/SMTP 模板，未实现 Microsoft OAuth。OpenSpec 中真实外部验收任务保留未完成状态。

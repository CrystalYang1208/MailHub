# 阶段四执行前检查

依据：负责人已批准阶段三整体设计、8ADR、AC13/14及三个设计默认值。当前阶段只搭建工程，不重新设计或开发邮箱业务。

| 检查 | 实际状态 |
|---|---|
| 工作区/项目根 | 软件需求分析大作业 / MailHub |
| Git | 两级均无.git；无法区分历史未提交修改，因此先记录现有文件SHA256，不把现有文件当可覆盖生成物 |
| 前后端 | 无正式frontend/backend；只有validation独立原型 |
| 设计 | design目录全部合同可读；阶段三manifest核对，不改原文 |
| 原型/证据 | validation、技术验证记录保留；207个非私有原有文件哈希保存于.local/stage4/preexisting-files.json |
| 私有数据 | validation/private存在；不读秘密、不迁移、不纳入Git；历史PNG/证据默认排除 |
| OpenSpec | 父工作区2.0.0已有空规范骨架；MailHub/openspec为空，不移动父目录配置 |
| Codex | 父工作区已有6项OpenSpec Skills；项目内原先无Skills/提示词；检查无需要清理的旧全局OpenSpec提示词 |
| 工具 | Node24.20.0/npm11.19.0/Git2.33.1；工作区SDK10.0.404，Runtime10.0.15 |

## 已告知的文件操作清单

新增frontend、backend、database、contracts、tools、项目内OpenSpec、.github及协作报告；将批准SQL/OpenAPI作为字节一致副本导入，不移动原设计。补写原本为空的README，新增Git忽略/质量配置。仅在MailHub初始化Git，不创建远程、不push、不提交未经安全审查的现有证据。工程数据/日志/运行链接位于.local且被忽略。

OpenSpec使用官方2.0.0本地CLI及--tools codex，不使用--force，不删除父目录或全局文件；后续脚本设置OPENSPEC_TELEMETRY=0。初次CLI路径相对目录错误仅导致MODULE_NOT_FOUND，修正路径后初始化成功；第一次npm元数据查询因系统缓存目录不可写失败，改工作区缓存，未修改系统ACL或全局npm配置。

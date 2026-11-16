# 开发协作

先阅读 docs/requirements/BASELINE_DECISIONS.md、docs/technical/design/MODULE_DESIGN.md、接口/数据库/安全专项设计及本模块Spec。阶段三设计标题中的“待审”是保留历史文本，批准记录以BASELINE_DECISIONS及本次任务书为准。

技术负责人负责架构、接口、迁移与合并审核；Account、Sync、Mail、Compose、Search、Settings负责人按应用端口和批准契约独立开发。Infrastructure中的适配器不得反向依赖Host，Domain不得引用数据访问和协议库。跨模块依赖通过Application端口；公共文件修改先在变更design说明影响并协调，不直接改其他模块内部实现。

每个功能切片使用自己的 feature/* 分支和 OpenSpec Change；开发计划见 docs/collaboration/DEVELOPMENT_SLICES.md。不可把整个平台作为一个变更，也不为每条FR机械建一个变更。Should 保留优先级。

公共API先更新OpenAPI并经负责人审查，再生成前端类型、更新实现清单和端点契约测试。数据库变更先审核追加迁移与恢复策略。批准的V0001不可原地修改。

合并前执行 tools/Test-Quality.ps1；提交只选择明确文件，检查 git diff --cached，禁止批量加入历史私有数据。按PR模板说明Change/FR/任务/接口/迁移/测试；实验性业务实现不得直接合入main。详见 GIT_WORKFLOW、OPENSPEC_WORKFLOW、CODE_REVIEW。

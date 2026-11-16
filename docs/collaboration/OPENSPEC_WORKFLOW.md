# OpenSpec 2.0.0与Codex

本项目根目录MailHub已运行官方init，启用Codex中文集成。项目.agents/skills中保留CLI生成的六项技能；父工作区旧配置不改。命令从MailHub执行；OPENSPEC_TELEMETRY=0禁用匿名遥测。

```powershell
$env:OPENSPEC_TELEMETRY='0'
npx openspec --version
npx openspec list --json
npx openspec validate --all --strict --no-interactive
npx openspec status --change engineering-foundation --json
npx openspec instructions apply --change engineering-foundation --json
```

specs包含account-management、mail-sync、mail-reading、mail-compose、mail-search、local-users、security、local-storage。规范是批准目标能力，不代表业务实现。35FR保持24Must/11Should；NFR/BR和AC01-14、CR01-08引用原设计及批准记录。

changes/engineering-foundation只追踪本次工程任务，skip_specs=true因为不改变业务行为。proposal解释动机与范围，design记录工程方案，tasks只在实际验证后勾选。阶段四审核后再归档。

后续按DEVELOPMENT_SLICES创建可实施业务切片，未开始的七个名称仅为待办，不是假装已实施的变更。CLI基础步骤：

```powershell
npx openspec new change <name>
npx openspec status --change <name> --json
npx openspec instructions proposal --change <name> --json
# 按status就绪顺序读取specs/design/tasks指令，填写相应产物
npx openspec instructions apply --change <name> --json
npx openspec validate --all --strict --no-interactive
```

Codex中可直接要求“使用 openspec-propose 为 implement-account-access 生成变更”；实施时“使用 openspec-apply-change implement-account-access”；修订规划用openspec-update-change；审核通过再openspec-sync-specs与openspec-archive-change。Codex先读适用SKILL.md和CLI指令，不把提案生成当成实施完成，也不使用过时/臆造的命令。任何归档CLI具体参数先查--help。

FR→Spec→计划Change/Task→阶段三Design→operationId→测试在REQUIREMENT_SPEC_TRACE；具体代码文件/测试结果由相应Change在实施时追加，当前不得伪造业务代码对应关系。

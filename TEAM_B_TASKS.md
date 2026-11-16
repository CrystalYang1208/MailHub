> 阶段四Starter历史快照：本页保留原交接背景。本轮业务实现与当前验证状态请看README.md及docs/course/验收记录.md，不以本页的“尚未开发/待分配”判断当前交付。

# 开发组B：发件与邮件操作

状态：待开发；本文是首次交接任务，不是测试通过记录。先读DEVELOPMENT_HANDOFF和MODULE_OWNERSHIP。

建议按草稿/编辑器基础→冻结提交与失败状态→回复转发/附件分PR。前后端均由B完成：frontend/src/features/compose，backend各层Compose目录，SMTP适配器放Infrastructure/Protocols。发送逻辑不写在Controller。

建议待创建Change：team-b-compose-outbox（按评审大小再拆分）。Spec mail-compose；设计引用EMAIL_INTEGRATION_DESIGN、ERROR_HANDLING_DESIGN、MODULE_DESIGN、DATABASE_DESIGN及SECURITY_DESIGN。该名称目前未创建或启动。

先基于合成IMailQueries/凭据租约/SMTP stub完成本模块，不要求A先完成真实同步。回复从实际源Message-ID/References生成关联；转发独立新邮件带引用，不伪造回复线程头。发送前冻结内容与收件人状态；明确拒绝保稿；DATA后失联属于unknown，绝不自动重发。服务器accepted不能报告收件端已收到；Sent副本按已批准设计处理。

## T-FR-D01 [Must] 新建邮件，可选择发件账号、填写收件人、主题、正文

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：createDraft, sendDraft；公共端点的认证约束不可放宽。
- [ ] 模块自测：可从任一已接入账号发出；测试编号T-FR-D01，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-D02 [Must] 回复与转发，正确携带引用内容与线程头

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：createReply, createForward, sendDraft；公共端点的认证约束不可放宽。
- [ ] 模块自测：回复真实源头关联；转发独立新邮件且引用完整（批准解释）；测试编号T-FR-D02，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-D03 [Should] 草稿本地保存与恢复

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：createDraft, listDrafts, getDraft, saveDraft, deleteDraft；公共端点的认证约束不可放宽。
- [ ] 模块自测：关闭后重新打开草稿内容完整；测试编号T-FR-D03，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-D04 [Must] 发送失败保留草稿并提示原因

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：createDraft, listDrafts, getDraft, saveDraft, sendDraft, getSubmission, resolveSubmission；公共端点的认证约束不可放宽。
- [ ] 模块自测：失败后草稿不丢失；测试编号T-FR-D04，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## T-FR-D05 [Should] 添加附件，单封不超过 25MB

- [ ] 前端交互、应用用例与协议/存储适配按批准设计实现。
- [ ] 使用原operationId：uploadDraftAttachment, removeDraftAttachment, sendDraft；公共端点的认证约束不可放宽。
- [ ] 模块自测：超限时阻止发送并提示；测试编号T-FR-D05，相关AC见设计追踪。
- [ ] 提交Change任务、源码、可复现测试与脱敏结果，不以本任务清单作为完成证明。

## 必测失败边界

明确SMTP拒绝与草稿恢复、DATA后连接丢失unknown、部分收件人接受、重复点击幂等、回复来源头缺失、转发引用与附件选择、超限和文件消失、会话锁定/切换取消租约。不要求为了模块自测让所有供应商重复互发；修改影响到真实协议时再申请受控一次验证。

提交与联调标准见INTEGRATION_CHECKLIST；未分配的共享依赖见UNASSIGNED_REQUIREMENTS，不能临时更改合同绕过。

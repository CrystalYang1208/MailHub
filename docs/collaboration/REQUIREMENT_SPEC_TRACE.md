# FR → Spec → Change → Design → Task → API → Test

主Specs保存已批准目标能力，非业务完成证明；下列业务Change是计划名称，不是已创建/已执行的变更。阶段四唯一执行变更是engineering-foundation。

| FR | 优先级 | Spec | 计划Change / Task | 设计 | operationId | 测试 |
|---|---|---|---|---|---|---|
| FR-A01 | Must | account-management | implement-account-access / T-FR-A01 | docs/technical/design | listProviders, validateAccount, addAccount | T-FR-A01；AC映射见设计追踪 |
| FR-A02 | Must | account-management | implement-account-access / T-FR-A02 | docs/technical/design | listProviders, validateAccount, listAccounts | T-FR-A02；AC映射见设计追踪 |
| FR-A03 | Must | account-management | implement-account-access / T-FR-A03 | docs/technical/design | listProviders, importOAuthClient, startOAuth, getOAuthFlow, validateAccount | T-FR-A03；AC映射见设计追踪 |
| FR-A04 | Must | account-management | implement-account-access / T-FR-A04 | docs/technical/design | listProviders, validateAccount | T-FR-A04；AC映射见设计追踪 |
| FR-A05 | Must | account-management | implement-account-access / T-FR-A05 | docs/technical/design | validateAccount | T-FR-A05；AC映射见设计追踪 |
| FR-A06 | Must | account-management | implement-account-access / T-FR-A06 | docs/technical/design | validateAccount, addAccount, getAccount | T-FR-A06；AC映射见设计追踪 |
| FR-A07 | Must | account-management | implement-account-access / T-FR-A07 | docs/technical/design | startOAuth, getOAuthFlow, reauthorizeAccount | T-FR-A07；AC映射见设计追踪 |
| FR-A08 | Must | account-management | implement-account-access / T-FR-A08 | docs/technical/design | removeAccount, getOperation | T-FR-A08；AC映射见设计追踪 |
| FR-A09 | Should | account-management | implement-account-access / T-FR-A09 | docs/technical/design | setAccountEnabled | T-FR-A09；AC映射见设计追踪 |
| FR-B01 | Must | mail-sync | implement-durable-mail-sync / T-FR-B01 | docs/technical/design | requestSync | T-FR-B01；AC映射见设计追踪 |
| FR-B02 | Must | mail-sync | implement-durable-mail-sync / T-FR-B02 | docs/technical/design | requestSync | T-FR-B02；AC映射见设计追踪 |
| FR-B03 | Must | mail-sync | implement-durable-mail-sync / T-FR-B03 | docs/technical/design | requestSync, listSyncJobs, retrySyncJob | T-FR-B03；AC映射见设计追踪 |
| FR-B04 | Should | mail-sync | implement-durable-mail-sync / T-FR-B04 | docs/technical/design | requestSync, getSettings, updateSettings | T-FR-B04；AC映射见设计追踪 |
| FR-B05 | Must | mail-sync | implement-durable-mail-sync / T-FR-B05 | docs/technical/design | requestSync, listSyncJobs, getSyncJob, retrySyncJob | T-FR-B05；AC映射见设计追踪 |
| FR-B06 | Must | mail-sync | implement-durable-mail-sync / T-FR-B06 | docs/technical/design | requestSync | T-FR-B06；AC映射见设计追踪 |
| FR-B07 | Should | mail-sync | implement-durable-mail-sync / T-FR-B07 | docs/technical/design | listSyncJobs, getSyncJob | T-FR-B07；AC映射见设计追踪 |
| FR-B08 | Should | mail-sync | implement-durable-mail-sync / T-FR-B08 | docs/technical/design | listSyncJobs | T-FR-B08；AC映射见设计追踪 |
| FR-C01 | Must | mail-reading | implement-mail-reading-actions / T-FR-C01 | docs/technical/design | listMessages | T-FR-C01；AC映射见设计追踪 |
| FR-C02 | Must | mail-reading | implement-mail-reading-actions / T-FR-C02 | docs/technical/design | listAccounts, listMessages | T-FR-C02；AC映射见设计追踪 |
| FR-C03 | Must | mail-reading | implement-mail-reading-actions / T-FR-C03 | docs/technical/design | getMessage, getMessageBody | T-FR-C03；AC映射见设计追踪 |
| FR-C04 | Should | mail-reading | implement-mail-reading-actions / T-FR-C04 | docs/technical/design | createDownloadBatch, getDownloadBatch, getAttachmentContent | T-FR-C04；AC映射见设计追踪 |
| FR-C05 | Must | mail-reading | implement-mail-reading-actions / T-FR-C05 | docs/technical/design | setMessageRead | T-FR-C05；AC映射见设计追踪 |
| FR-C06 | Must | mail-reading | implement-mail-reading-actions / T-FR-C06 | docs/technical/design | moveMessageToTrash | T-FR-C06；AC映射见设计追踪 |
| FR-C07 | Should | mail-reading | implement-mail-reading-actions / T-FR-C07 | docs/technical/design | listFolders, listMessages | T-FR-C07；AC映射见设计追踪 |
| FR-C08 | Should | mail-reading | implement-mail-reading-actions / T-FR-C08 | docs/technical/design | listFolders | T-FR-C08；AC映射见设计追踪 |
| FR-D01 | Must | mail-compose | implement-compose-outbox / T-FR-D01 | docs/technical/design | createDraft, sendDraft | T-FR-D01；AC映射见设计追踪 |
| FR-D02 | Must | mail-compose | implement-compose-outbox / T-FR-D02 | docs/technical/design | createReply, createForward, sendDraft | T-FR-D02；AC映射见设计追踪 |
| FR-D03 | Should | mail-compose | implement-compose-outbox / T-FR-D03 | docs/technical/design | createDraft, listDrafts, getDraft, saveDraft, deleteDraft | T-FR-D03；AC映射见设计追踪 |
| FR-D04 | Must | mail-compose | implement-compose-outbox / T-FR-D04 | docs/technical/design | createDraft, listDrafts, getDraft, saveDraft, sendDraft, getSubmission, resolveSubmission | T-FR-D04；AC映射见设计追踪 |
| FR-D05 | Should | mail-compose | implement-compose-outbox / T-FR-D05 | docs/technical/design | uploadDraftAttachment, removeDraftAttachment, sendDraft | T-FR-D05；AC映射见设计追踪 |
| FR-E01 | Must | mail-search | implement-local-search / T-FR-E01 | docs/technical/design | searchMessages | T-FR-E01；AC映射见设计追踪 |
| FR-E02 | Should | mail-search | implement-local-search / T-FR-E02 | docs/technical/design | searchMessages | T-FR-E02；AC映射见设计追踪 |
| FR-F01 | Must | local-users | implement-local-user-vault / T-FR-F01 | docs/technical/design | bootstrapBridge, listUsers, registerUser, login, getCurrentSession, logout, switchUser, changePassword, deleteUser, getOperation | T-FR-F01；AC映射见设计追踪 |
| FR-F02 | Must | local-storage | implement-storage-lifecycle / T-FR-F02 | docs/technical/design | getSettings, updateSettings, clearBodyCache | T-FR-F02；AC映射见设计追踪 |
| FR-F03 | Should | local-storage | implement-storage-lifecycle / T-FR-F03 | docs/technical/design | getSettings, clearMailData, getOperation | T-FR-F03；AC映射见设计追踪 |

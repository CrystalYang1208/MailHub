# Git流程

MailHub是独立仓库，main为稳定分支。feature/<change>、fix/<issue>、docs/<topic>短期分支，不引入develop或Git Flow。当前仅初始化，无远程、无推送、无自动提交。

Conventional Commits示例：feat(account): add provider templates；提交正文包含OpenSpec: implement-account-access、FR: FR-A02、Task: T-FR-A02。工程提交使用chore(engineering)。

只按路径git add，先git check-ignore验证私有路径，再git diff --cached --stat与完整diff检查。真实邮箱地址、正文、授权码、令牌、原始eml、截图、用户数据库和历史证据不可提交。ignore不能替代已跟踪文件审查。阶段二历史目录不应直接git add整个目录；仅选审查后的脱敏文档/源码。

PR填写模板并附本地检查结果；若未来配置GitHub，CI通过及技术负责人审核后squash合并。公共API/DDL/安全变更先审查设计；main不接收未经确认的实验业务代码。远程与分支保护需要负责人另行授权后配置。

冲突由作者更新目标分支后本地解决；接口/DDL冲突先协调负责人，不选择整侧覆盖；重新运行受影响检查。已合入缺陷用git revert产生审计明确的回滚，不重写共享历史。数据迁移回滚需一致性备份与专门恢复流程，不能仅revert SQL代码。

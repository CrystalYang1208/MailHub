# Spec Delta

## Purpose
提供独立于真实邮箱的合成邮件工作区，让课程展示和自动测试能在无外部账号、无邮件服务器时执行前后端完整流程，同时明确标识演示性质并防止把模拟结果当成真实收发验收。

## ADDED Requirements

### Requirement: 隔离的合成工作区
The system SHALL 通过显式演示启动选项使用独立数据目录及合成协议适配器，界面持续显示演示标识，只允许example.invalid测试地址，不能向外部邮箱发送。

#### Scenario: 无外部账号展示
- **WHEN** 用户启动演示模式并创建本地身份
- **THEN** 系统提供三个合成账号和可搜索阅读的邮件，所有操作通过本地API及SQLite完成。

#### Scenario: 演示拒绝真实邮箱
- **WHEN** 演示用户填写非example.invalid地址
- **THEN** 系统明确拒绝，指引使用Windows11正式模式。

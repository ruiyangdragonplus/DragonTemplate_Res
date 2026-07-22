# ⚠️ 框架只读区

`Assets/Framework/` 下所有内容由 DragonTemplate 模板统一管理（`Tools/framework_sync.sh` 同步范围）。

- **子项目禁止修改本目录任何文件**。改了会被下次 framework_sync pull 覆盖，且 commit-msg hook / Jenkins Preflight 会告警。
- 发现框架 bug：紧急时允许修改，但 commit message 必须带 `[framework-hotfix]` 标签，随后用 `Tools/framework_sync.sh diff` 导出 patch 提交 DragonTemplate 仓库 PR 回流。
- 项目定制需求请写在 `Assets/Game/Ext/`（扩展层）——继承/替换框架实现，不改框架本体。

当前框架版本见仓库根 `FRAMEWORK_VERSION.md`。

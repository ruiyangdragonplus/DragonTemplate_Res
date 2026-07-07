# Res 资源工程（git submodule 占位）

派生项目中，本目录是独立仓库 `<Project>_Res.git` 的 submodule（第二个 Unity 工程，美术在此迭代，避免撑爆主仓）。

## 固定目录结构（所有项目一致，禁止发散）

```
Res/
├── Assets/
│   ├── Art/
│   │   └── Modules/<ModuleName>/   # 模块资源按此归组（YooAsset Collector 每模块一个 Group）
│   ├── Audio/
│   ├── UI/
│   ├── Configs/                    # Luban 输出 JSON（生成物，进 YooAsset 包）
│   └── Localization/
├── Luban/
│   ├── Tables/<ModuleName>.xlsx    # 源表按模块分文件（配置的唯一源头）
│   ├── luban.conf
│   └── gen.sh                      # 唯一生成入口 → JSON 进 Assets/Configs/，partial C# 进主工程 Config/Generated/
└── res_manifest.json               # Res→Game 声明式同步映射（Tools/sync_res.sh 与 -task SyncRes 共用）
```

## 归属判据（FRAMEWORK_RULES.md 第 6 条）

- 运行时经 `IAssetSystem` 加载的一切 → Res 工程
- AOT 随包场景（Launcher、隐私弹窗）与 Settings 资产 → Game 工程

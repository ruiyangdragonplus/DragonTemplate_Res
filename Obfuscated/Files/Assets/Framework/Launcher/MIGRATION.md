# 迁移清单：Dragon.Launcher（阶段 0/3）

从 ColorBean1 搬运：

| 源（ColorBean1/Assets/Scripts/） | 目标 | 改造点 |
|---|---|---|
| `Launcher/Launcher.cs` | `Launcher.cs` | 原样 |
| `Launcher/GlobalState/`（LauncherStateManager + 7 状态） | `GlobalState/` | 原样保留状态机结构 |
| `Launcher/GlobalState/State/SetupState.cs` | 同名 | **InstallSDK() 硬编码顺序 → 读 GameComposition.asset + ProviderRegistry.InstallAll()**（阶段 3 核心改点） |
| `Launcher/GlobalState/State/ResInit/ResUpdate*/ResDownload` 各状态 | 同名 | **直呼 YooAsset → 改走 IAssetProvider / IHotUpdateProvider**；Provider 报告"无热更"时三个 ResUpdate 态 Skip |
| `Launcher/GlobalState/State/StartGameState.cs` | 同名 | HybridCLR/Obfuz 加载逻辑 → IHotUpdateProvider.LoadHotfixAssemblies() |
| `Launcher.Attribute/` | 并入本程序集 | — |

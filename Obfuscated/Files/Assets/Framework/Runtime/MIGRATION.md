# 迁移清单：Dragon.Framework（阶段 0/2）

从 ColorBean1（DragonCD3 基线）搬运以下内容到本目录，命名空间 `DragonCD3` → `Dragon.Framework`：

| 源（ColorBean1/Assets/Scripts/） | 目标 | 说明 |
|---|---|---|
| `Framework/Runtime/Runtime/GameManager.cs` | `Runtime/GameManager.cs` | System 列表改读 SystemManifest.asset（阶段 3） |
| `Framework/Runtime/Runtime/GameSystem.cs / GameStage.cs / GameFeature.cs` | `Runtime/` | 原样保留 |
| `Framework/Runtime/Runtime/<模块>/I*System.cs` | 接口上移到 `Core/Systems/` | 本目录只留通用实现骨架 |
| `Framework/Runtime/Implements/<模块>/Standard/` | `Systems/<模块>/` | 与 SDK 无关的实现留此；碰 SDK 的部分拆到 Providers/ |
| `Framework/Runtime/Runtime/UI/`（PanelDescriptor/PopupDescriptor/ViewGroup 11 层） | `UI/` | 原样保留 |
| `Framework/Runtime/Runtime/Event/EventSystem.cs` | `Systems/Event/` | 原样保留 |
| `ApolloFramework/UserSegmentation/` | 拆到 Providers/RemoteConfig 相关或独立目录 | 阶段 2 处理 |

**拆分原则**：文件里 `using YooAsset` / `using DragonU3DSDK` / `using AppLovin` 等具体 SDK 的部分 → 对应 `Providers/<Xxx>/`；纯逻辑留本目录。

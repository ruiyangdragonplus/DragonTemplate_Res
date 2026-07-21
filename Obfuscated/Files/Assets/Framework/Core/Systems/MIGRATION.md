# 迁移清单：System 接口上移（阶段 0/2）

DragonCD3 已有的 `I*System` 接口（ColorBean1 `Assets/Scripts/Framework/Runtime/Runtime/<模块>/I*System.cs`）
整体搬到本目录，命名空间改 `Dragon.Core`，共约 19 个：

IAccountSystem, IActivitySystem, IAdvertiseSystem, IAssetSystem, IAudioSystem, IConfigSystem,
ICoroutineSystem, IEventSystem, IGuideSystem, IIAPSystem, ILocalizationSystem, IModelSystem(ISaveSystem),
INetworkSystem, INotificationSystem, IRedPointSystem, IRemoteConfigSystem, ITaskListSystem, IUISystem, ITouchMaskSystem

搬运时的接口收敛动作：
1. 接口内直接暴露 YooAsset/Max 等具体类型的成员 → 改为 Dragon.Core 中的中立类型（见 Providers/ 下各接口的参数风格）
2. 依赖具体 SDK 的默认实现留在 `Framework/Runtime/`（不碰 SDK 的部分）或 `Framework/Providers/<Xxx>/`（碰 SDK 的部分）
3. `I*Helper` 业务粘合接口（IAdvertiseHelper/IAccountHelper/IRedPointHelper/ITaskListHelper/ILocalizationHelper 等）
   一并上移到本目录 `Helpers/` 子目录，Game.Ext 实现，GameManager.Initialize 经 HelperRegistry 注入

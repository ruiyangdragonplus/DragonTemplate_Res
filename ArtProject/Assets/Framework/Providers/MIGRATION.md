# 迁移清单：Providers（阶段 2，按风险从低到高）

## 状态（2026-07-08）

| Provider | 状态 | 说明 |
|---|---|---|
| Analytics.Debug / Analytics.Firebase | ✅ 冒烟验证 | 多 sink 广播 + 异常隔离；Firebase 初始化仍由 Legacy TrackingModule |
| Ads.Max | ✅ 冒烟验证 | 惰性委托 SDK<DragonPlus.Ad.IAdProvider>；OnAdRevenue 待接 MaxSdkCallbacks |
| IAP.UnityPurchasing | ✅ 冒烟验证 | 委托 SDK<DragonPlus.InAppPurchasing.IAP>；补单校验保留 |
| Analytics.Adjust / Analytics.DragonBI | ⬜ 占位 | Adjust 需事件 token 映射表；DragonBI 是强类型 protobuf 事件，需通用映射设计 |
| HotUpdate.HybridCLR | ⬜ 占位 | 需抽 StartGameState.LoadHybrid → Provider（改 Legacy，需 Play 回归护航） |
| Asset.YooAsset | ⬜ 占位，最重 | Launcher ResInit/ResUpdate 三态 + AssetSystem 全链改接口 |
| Config.Luban / Network.DragonSDK | ⬜ 占位 | 待有模板内调用方时收编 |
| Save.DragonStorage | ⬜ 占位 | 真实接入面在业务 Model 基类（StorageModel，未入模板）；随 Model 层收编 |
| Notification.DragonSDK | ❌ 判定不需要 | Legacy NotificationsSystem 是纯框架实现（Unity Mobile Notifications 直连），归"无 Provider"类 |


每个 Provider 文件夹 = 一个可整体删除/替换的工具适配器。迁移顺序与来源：

| 顺序 | Provider | 来源（ColorBean1/Assets/Scripts/） | 说明 |
|---|---|---|---|
| 1 | Analytics.Firebase / Adjust / DragonBI | `Framework/Runtime/Implements/` 埋点相关 + BiUtil 调用点 | 纯旁路，风险最低；三个 sink 并存注册 |
| 2 | Ads.Max | `Implements/Advertise/Standard/` 中碰 MaxModule 的部分 | IAdProvider |
| 3 | IAP.UnityPurchasing | `Implements/IAP/` | IStoreProvider |
| 4 | Notification.DragonSDK / Save.DragonStorage | `Implements/Notification/`、Model 层 StorageModule 调用点 | — |
| 5 | Config.Luban | `Config/` 的加载链（IConfig.LoadConfigs） | IConfigProvider |
| 6 | HotUpdate.HybridCLR | `HybridCLRExtension/` + `ObfuzEncryptionInitializer/` | IHotUpdateProvider；同时实现 HotUpdate.None 空 Provider |
| 7 | Asset.YooAsset（**最后**） | `Implements/Asset/YooAsset/` + `YooAssetExtension/`（含 XOR 加密 FileStreamServices） | 渗透最深：Launcher ResUpdate 三态 + 打包链路都要跟着切接口 |
| — | Network.DragonSDK | `Implements/Network/` + `Protocol/ProtocolManager` 底层 | INetworkChannel |

## 每个 Provider 的标准组成

```
Providers/<Name>/
├── Dragon.Provider.<Name>.asmdef   # defineConstraints 门控（已生成）
├── <Name>Installer.cs              # [ProviderInstaller("<Name>", "<SYMBOL>")] : IProviderInstaller
└── <Name>Provider.cs               # 实现对应 I*Provider 接口
```

## 验收标准

- 删除任一 Provider 文件夹 + GameComposition 去勾选 → 工程编译通过
- 各 Provider 的 asmdef `references` 中 SDK 程序集名需按实际包核对（YooAsset / HybridCLR.Runtime / UnityEngine.Purchasing 已预填；DragonSDK 系 UPM 包的 asmdef 名迁移时补）

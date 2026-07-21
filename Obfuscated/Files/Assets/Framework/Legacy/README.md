# Legacy：DragonCD3 存量框架（阶段 0 原样搬入）

本目录是从 ColorBean1（DragonCD3 基线）**保留 GUID（含 .meta）原样搬入**的可运行框架实现。
阶段 0 原则「不改任何运行时行为」——命名空间仍是 `DragonCD3` / 原 asmdef 名（Game.Framework、Game.Launcher 等）。

## 与目标骨架（Core/Runtime/Providers/Launcher）的关系

Legacy 是**当前生效**的实现；`Framework/{Core,Runtime,Providers,Launcher}` 是**阶段 2/3 的目标结构**。
迁移方式是"逐步溶解"：每完成一个能力的 Provider 化（顺序见 `Providers/MIGRATION.md`），
把 Legacy 对应文件搬到目标位置、业务调用点切到接口，Legacy 目录逐步缩小直至删除。

## 搬入清单

| 目录 | 来源（ColorBean1/Assets/Scripts/） | asmdef |
|---|---|---|
| Framework/ | Framework/（226 cs：GameManager/System/Stage/Feature/UI/Implements） | Game.Framework(+Editor) |
| Launcher/ + Launcher.Attribute/ | 同名（7 状态机启动链） | Game.Launcher / Launcher.Atrribute |
| ApolloFramework/ | 同名（用户分群 RulesEngine） | Apollo.UserSegmentation |
| HybridCLRExtension/ | 同名（HybridBuild + AOT 引用） | HybridCLR.Extensions(+Editor) |
| YooAssetExtension/ | 同名（XOR 加密 FileStreamServices 等） | YooAsset.Extension(+Editor) |
| Automation/ | 同名（DragonPlus.Automation.Edtior.BuildTool——BuildEntry 各 task 的迁移来源） | Automation.Editor |
| ObfuzEncryptionInitializer/ | 同名 | — |

配套搬入（Game/Assets 下）：`ThirdParty/{YooAsset,Spine,SpineEx,ExportDllFixGuid}`（框架 asmdef 直接 GUID 引用的源码内嵌库）、
`Scene/Launcher.unity`、`Resources/`（ConfigurationController 等 Settings；google-services.json / FacebookSettings 为
ColorBean1 占位，新项目须替换）、`SDKLink/`、`Editor/`（AutoRes/AssmblyTool/ExportDllFixGuid 配置；未搬 WorldMapEdit 等项目专属工具）。
`Game/Packages/`（manifest + lock + 嵌入包 hybridclr/universaldeeplinking）与 `Game/ProjectSettings/` 也来自 ColorBean1。

## 已知待验证项（首次 Unity 打开时处理）

1. ~~Game.Launcher 引用 GUID 13ba8ce6... 未定位~~ 已确认 = 嵌入包 `Packages/com.code-philosophy.hybridclr` 的
   HybridCLR.Runtime.asmdef（已随 Packages/ 搬入，可正常解析）
2. 未搬 ColorBean1 的 Gameplay/Config/Protocol/Model/GM（项目业务）——Legacy Framework 不依赖它们，
   但 Editor/ 下 loose 脚本（AutoRes 等，无 asmdef 进 Assembly-CSharp-Editor）可能引用业务类型，编译报错时优先注释/裁剪这些脚本
3. ProjectSettings 里的 productName/bundleId 仍是 ColorBean1——由 BuildProfile.ApplyToProject 覆盖，模板发版前可改成占位值
4. Packages/manifest.json 的 UPM 私有源指向 upm.dragonplus.vip，需要内网/VPN 可达才能解析 com.dragonplus.* 包

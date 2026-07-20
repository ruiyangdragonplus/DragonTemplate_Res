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

## 美术工程（无明文代码）

主仓 dll 流水线（`Jenkins/dll.jenkinsfile`）会把 ExportDllFixGuid + Obfuscar 产物推送到本仓 `Obfuscated/`：
`Dll/`（混淆后的全部程序集 + GUID 映射）+ `Files/`（工程壳：**空 .cs + 原 GUID meta** + 场景/设置/ThirdParty）。

美术 clone 本仓库后执行 `Tools/setup_art_project.sh`，组装出可用 Unity 打开的 `ArtProject/`：
业务/框架代码只有混淆 DLL（无明文），美术资源经目录链接指向本仓 `Assets/Res` 真实源——改完直接 git 提交。

> ⚠️ 首次投产前需联调：prefab 脚本引用的 GUID 映射重写（DllGuidMap/unity-art-encrypt）与空壳 asmdef
> 同名冲突处理，见 setup_art_project.sh 内注释。

## 同步模式：link（默认）与 mirror

- **link**：主工程内的 `Game/Assets/Game/{Configs,Localization,Art}` 是指向本仓库对应目录的**目录链接**
  （Windows junction / Mac symlink，由 `Tools/sync_res.sh` 依 res_manifest.json 自动建立）。
  在主工程 Unity 里修改这些资源 = 直接修改本仓库工作区文件，`cd Res && git add && git commit` 即可提交——
  与现有项目的软链工作流一致，只是链接关系收敛到清单统一管理。资源与 .meta 都提交在本仓库。
- **mirror**：拷贝模式，仅用于加密/生成等加工产物；目标目录是只读生成物。

## 归属判据（FRAMEWORK_RULES.md 第 6 条）

- 运行时经 `IAssetSystem` 加载的一切 → Res 工程
- AOT 随包场景（Launcher、隐私弹窗）与 Settings 资产 → Game 工程

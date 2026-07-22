# Res 资源工程（git submodule 占位）

派生项目中，本目录是独立仓库 `<Project>_Res.git` 的 submodule（第二个 Unity 工程，美术在此迭代，避免撑爆主仓）。

## 固定目录结构（所有项目一致，禁止发散）

```
Res/
├── ArtProject/                     # ★ 美术工程（母体，克隆即可用 Unity 打开）
│   ├── Assets/
│   │   ├── Res/                    #   美术资源真实源（主工程 junction 指进来）
│   │   │   └── Art/Modules/<模块>/ #   模块资源按此归组
│   │   ├── Configs/                #   Luban 输出 JSON（真实源）
│   │   ├── Framework/ Game/        #   空壳代码区（0 字节 .cs 保 GUID，逻辑在混淆 DLL）
│   │   ├── Editor/UpdateScripts.cs #   「更新代码」按钮（Toolbar+菜单+30s 自动检查）
│   │   └── Resources/ Scene/ ThirdParty/ SDKLink/
│   ├── Packages/  ProjectSettings/
│   └── UpdateScripts.bat / .sh
├── Obfuscated/                     # dll 流水线产物：Dll/（混淆程序集）+ Files/（壳）+ version
├── Luban/                          # 源表 + gen.sh（配置唯一源头）
└── res_manifest.json               # Res→主工程 声明式链接映射
```

## 美术工程（无明文代码）

主仓 dll 流水线（`Jenkins/dll.jenkinsfile`）会把 ExportDllFixGuid + Obfuscar 产物推送到本仓 `Obfuscated/`：
`Dll/`（混淆后的全部程序集 + GUID 映射）+ `Files/`（工程壳：**空 .cs + 原 GUID meta** + 场景/设置/ThirdParty）。

美术 clone 本仓库后直接用 Unity 打开 `ArtProject/`（无需任何初始化脚本）。
工作流：编辑器 Toolbar 右侧「更新代码」按钮（或 Tools 菜单；每 30 秒自动检查出小红点）——
把 `Obfuscated/Dll` 覆盖到 `Library/ScriptAssemblies`（DLL 不进 Assets，MonoScript 保持与主工程一致的
源码 GUID，零转换绑定），并同步 `Obfuscated/Files` 壳。首次打开后点一次「更新代码」再重开即生效。
资源改动物理落在本仓库工作区，git add/commit 直接提交。

## 同步模式：link（默认）与 mirror

- **link**：主工程内的 `Game/Assets/{Res,Config,ProtocolMap}` 是指向本仓库/Proto 输出的**目录链接**
  （Windows junction / Mac symlink，由 `Tools/sync_res.sh` 依 res_manifest.json 自动建立）。
  在主工程 Unity 里修改这些资源 = 直接修改本仓库工作区文件，`cd Res && git add && git commit` 即可提交——
  与现有项目的软链工作流一致，只是链接关系收敛到清单统一管理。资源与 .meta 都提交在本仓库。
- **mirror**：拷贝模式，仅用于加密/生成等加工产物；目标目录是只读生成物。

## 归属判据（FRAMEWORK_RULES.md 第 6 条）

- 运行时经 `IAssetSystem` 加载的一切 → Res 工程
- AOT 随包场景（Launcher、隐私弹窗）与 Settings 资产 → Game 工程

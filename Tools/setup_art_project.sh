#!/usr/bin/env bash
# 组装"无明文美术工程"（美术 clone 本仓库后执行一次）。
#
# 前置：主仓 dll.jenkinsfile 已把 ExportDllFixGuid+Obfuscar 产物推送到本仓 Obfuscated/：
#   Obfuscated/Files/{Assets,Packages,ProjectSettings}  ← 工程壳（空 .cs + 原 GUID meta + asmdef）
#   Obfuscated/Dll/{Android,WindowsMacLinux}/           ← 混淆后的全部程序集
#
# 组装结果 ArtProject/：可用 Unity 打开的完整工程，无任何明文业务代码；
# 美术资源经目录链接指向本仓 Assets/Res 等真实源——改动直接落仓库工作区，git 提交即可。
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ART="$ROOT/ArtProject"

[[ -d "$ROOT/Obfuscated/Files" ]] || { echo "缺 Obfuscated/Files——请先跑主仓 dll 流水线"; exit 1; }

echo "==> 展开工程壳"
mkdir -p "$ART"
cp -r "$ROOT/Obfuscated/Files/." "$ART/"

echo "==> 放置 DLL（桌面平台，供编辑器打开；GUID 映射见 Obfuscated/version 与映射 json）"
mkdir -p "$ART/Assets/Plugins/GameDll"
cp "$ROOT/Obfuscated/Dll/WindowsMacLinux/"*.dll "$ART/Assets/Plugins/GameDll/" 2>/dev/null || true
cp "$ROOT/Obfuscated/Dll/WindowsMacLinux/"*.dll.meta "$ART/Assets/Plugins/GameDll/" 2>/dev/null || true

echo "==> 链接美术资源真实源（改动直接落仓库，git 提交即可）"
link_dir() {
  local link="$1" target="$2"
  [[ -e "$link" ]] && return 0
  mkdir -p "$(dirname "$link")"
  case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*) cmd //c mklink //J "$(cygpath -w "$link")" "$(cygpath -w "$target")" > /dev/null ;;
    *) ln -s "$target" "$link" ;;
  esac
  echo "  link: $link -> $target"
}
link_dir "$ART/Assets/Res"     "$ROOT/Assets/Res"
link_dir "$ART/Assets/Config"  "$ROOT/Assets/Configs"

cat <<EOF

✅ ArtProject 组装完成：用 Unity 打开 $ART
   - 业务/框架代码 = 混淆 DLL（无明文）；prefab 脚本引用经 GUID 映射解析
   - Assets/Res、Assets/Config 是仓库真实源的链接，改完 git add/commit 即提交
   - ⚠️ 首次联调注意：若 prefab 报 Missing Script，需核对 Obfuscated 映射版本
     与资源版本一致（重跑主仓 dll 流水线刷新 Obfuscated）
EOF

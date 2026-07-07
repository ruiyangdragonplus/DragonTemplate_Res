#!/usr/bin/env bash
# Luban 导表唯一入口。TODO(阶段0): 接入现有 Luban 链路（参考 ColorBean1 Res/Configs/Tools/gen_client.sh，需 .NET 8）。
# 产出固定两路：
#   JSON      → Assets/Configs/                       （进 YooAsset 资源包）
#   partial C#→ ../Game/Assets/Game/Config/Generated/ （主工程热更程序集，生成物禁手改）
set -euo pipefail
echo "TODO: 迁移 Luban 调用（Luban/Tables/*.xlsx + luban.conf → JSON + partial C#）"
exit 1

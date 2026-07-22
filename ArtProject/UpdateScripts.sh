#!/bin/bash
echo "开始更新代码"
# # 获取当前工作目录的绝对路径
# #SCRIPT_DIR=$(pwd)
# # 构造脚本的绝对路径
# #SCRIPT_PATH="$SCRIPT_DIR/$(basename "$0")"

# SCRIPT_DIR="$0"
# SCRIPT_DIR=${SCRIPT_DIR%/*}

# RuntimeDllPath="$SCRIPT_DIR/Library/RuntimeDll";
# echo "RuntimeDllPath path: $RuntimeDllPath"

# cd $SCRIPT_DIR/..
# #git fetch origin
# #git reset --hard origin/main

# if [ ! -d "$RuntimeDllPath" ]; then
#   git clone --depth 1 --single-branch --branch dll git@github.com:FlyDragonGO/MergeCooking3_Res.git $RuntimeDllPath --verbose --progress
#   if [ -d "$RuntimeDllPath" ]; then
#     echo "代码更新成功"
#   else
#     git clone --depth 1 --single-branch --branch dll https://github.com/FlyDragonGO/MergeCooking3_Res.git $RuntimeDllPath --verbose --progress
#   fi
# fi

# if [ ! -d "$RuntimeDllPath" ]; then
#     echo "代码更新失败"
# fi

# cd $RuntimeDllPath || exit

# git fetch origin dll
# git reset --hard origin/dll
# git clean -ffdx origin/dll
# #git reset --hard origin/dll
# git pull origin dll
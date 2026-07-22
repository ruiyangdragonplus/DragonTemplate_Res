echo "更新代码"
@REM set ScriptDir=%~dp0
@REM set RuntimeDllPath="%ScriptDir%Library\RuntimeDll"

@REM cd %ScriptDir%..
@REM ::git fetch origin
@REM ::git reset --hard origin/main

@REM if NOT EXIST %RuntimeDllPath% (
@REM   git clone --depth 1 --single-branch --branch dll git@github.com:FlyDragonGO/MergeCooking3_Res.git %RuntimeDllPath% --verbose --progress
@REM   if EXIST %RuntimeDllPath% (
@REM     echo 更新代码成功
@REM   ) else (
@REM     git clone --depth 1 --single-branch --branch dll https://github.com/FlyDragonGO/MergeCooking3_Res.git %RuntimeDllPath% --verbose --progress
@REM   )
@REM )

@REM if NOT EXIST %RuntimeDllPath% (
@REM   echo 更新代码失败
@REM   pause
@REM )


@REM cd %RuntimeDllPath% || exit

@REM git fetch origin dll
@REM git reset --hard origin/dll
@REM git clean -ffdx origin/dll
@REM ::git reset --hard origin/dll
@REM git pull origin dll
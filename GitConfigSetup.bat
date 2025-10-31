@echo off
chcp 65001 >nul

setlocal enabledelayedexpansion

REM Получаем путь до info/attributes
for /f %%a in ('git rev-parse --git-path info/attributes') do set "ATTR_FILE=%%a"

REM Проверка, находимся ли мы в git-репозитории
git rev-parse --git-dir >nul 2>&1
if errorlevel 1 (
    echo Ошибка: текущая папка не является git-репозиторием.
    pause
    exit /b 1
)

REM Подменяем локальный attributes-файл содержимым из .gitattributes_drivers
set DRIVER_ATTR_FILE=.gitattributes_drivers
if not exist "!DRIVER_ATTR_FILE!" (
    echo Не найден файл !DRIVER_ATTR_FILE!
    pause
    exit /b 1
)

copy /Y "!DRIVER_ATTR_FILE!" "!ATTR_FILE!" >nul
if errorlevel 1 (
    echo Не удалось скопировать !DRIVER_ATTR_FILE! в !ATTR_FILE!
    pause
    exit /b 1
)

echo Подключили правила из !DRIVER_ATTR_FILE! в !ATTR_FILE!

REM Добавление драйвера
git config --local merge.unityyamlmerge.trustExitCode true
git config --local merge.unityyamlmerge.name "Unity SmartMerge (UnityYamlMerge)"
git config --local merge.unityyamlmerge.driver "bash Tools/UnityYamlMerge/unity-lfs-merge.sh %%O %%A %%B %%P"
git config --local merge.unityyamlmerge.recursive binary

echo merge driver unityyamlmerge успешно добавлен в локальный git config.
pause
exit /b


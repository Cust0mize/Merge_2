@echo off
setlocal enabledelayedexpansion

rem Логируем вызов в консоль
echo ==== %DATE% %TIME% ====
echo ARGS: %*

rem Ожидаем: wrapper вызывается как: wrapper.bat %l %r %b %d
set L=%~1
set R=%~2
set B=%~3
set D=%~4

rem Также логируем разложенные аргументы в консоль
echo L=%L%
echo R=%R%
echo B=%B%
echo D=%D%

rem Запускаем Rider как 3-way merge и ЖДЁМ завершения
start "" /wait "C:\Program Files\JetBrains\JetBrains Rider 2025.1.4\bin\rider64.exe" merge "%L%" "%R%" "%B%" "%D%"

rem Код возврата rider64.exe недоступен через start, но UnityYAMLMerge воспринимает окончание процесса как завершение инструмента.
rem Если нужно принудительно завершать nonzero -> exit /b 1
exit /b 0
@echo off
setlocal enabledelayedexpansion
echo ==== %DATE% %TIME% ====
echo ARGS: %*

set LEFT=%~1
set RIGHT=%~2
set BASE=%~3
set OUTPUT=%~4

echo LEFT(ours)=%LEFT%
echo RIGHT(theirs)=%RIGHT%
echo BASE=%BASE%
echo OUTPUT(result)=%OUTPUT%

rem Извлекаем директорию из LEFT (current.unity)
for %%F in ("%LEFT%") do set TEMP_DIR=%%~dpF

rem Строим правильные пути к оригинальным файлам
set REAL_CURRENT=%TEMP_DIR%current.unity
set REAL_OTHER=%TEMP_DIR%other.unity
set REAL_BASE=%TEMP_DIR%base.unity
set REAL_RESULT=%TEMP_DIR%result.unity

echo === Using original files ===
echo CURRENT=%REAL_CURRENT%
echo OTHER=%REAL_OTHER%
echo BASE=%REAL_BASE%
echo RESULT=%REAL_RESULT%

rem Проверяем существование файлов
echo === File existence check ===
if exist "%REAL_CURRENT%" (
    for %%F in ("%REAL_CURRENT%") do echo CURRENT exists: %%~zF bytes
) else (
    echo ERROR: CURRENT not found!
    exit /b 1
)

if exist "%REAL_OTHER%" (
    for %%F in ("%REAL_OTHER%") do echo OTHER exists: %%~zF bytes
) else (
    echo ERROR: OTHER not found!
    exit /b 1
)

if exist "%REAL_BASE%" (
    for %%F in ("%REAL_BASE%") do echo BASE exists: %%~zF bytes
) else (
    echo ERROR: BASE not found!
    exit /b 1
)

rem Запоминаем исходный размер result.unity (если существует)
set INITIAL_SIZE=0
if exist "%REAL_RESULT%" (
    for %%F in ("%REAL_RESULT%") do set INITIAL_SIZE=%%~zF
)
echo Initial result file size: %INITIAL_SIZE% bytes

echo === Launching Rider ===
set RIDER_EXE=C:\Program Files\JetBrains\JetBrains Rider 2025.1.4\bin\rider64.exe

echo Command: "%RIDER_EXE%" merge "%REAL_CURRENT%" "%REAL_OTHER%" "%REAL_BASE%" "%REAL_RESULT%"

rem Запускаем Rider БЕЗ ожидания (он запускает дочерний процесс)
start "RiderMerge" "%RIDER_EXE%" merge "%REAL_CURRENT%" "%REAL_OTHER%" "%REAL_BASE%" "%REAL_RESULT%"

echo Rider launched. Waiting for merge to complete...
echo Press Ctrl+C to cancel, or close this window after saving merge in Rider.
echo.

rem Ждем изменения файла результата
set /a WAIT_COUNT=0
set /a MAX_WAIT=1800
rem (1800 * 2 секунды = 1 час максимум)

:wait_loop
timeout /t 2 /nobreak >nul
set /a WAIT_COUNT+=1

rem Проверяем, изменился ли файл результата
if exist "%REAL_RESULT%" (
    for %%F in ("%REAL_RESULT%") do set CURRENT_SIZE=%%~zF
    if !CURRENT_SIZE! GTR %INITIAL_SIZE% (
        echo Result file updated: !CURRENT_SIZE! bytes
        goto merge_complete
    )
)

rem Показываем прогресс каждые 30 секунд (15 итераций)
set /a PROGRESS_CHECK=WAIT_COUNT %% 15
if !PROGRESS_CHECK! EQU 0 (
    set /a ELAPSED=WAIT_COUNT * 2
    echo Still waiting... (!ELAPSED! seconds elapsed^)
)

rem Проверяем таймаут
if %WAIT_COUNT% GEQ %MAX_WAIT% (
    echo TIMEOUT: Merge took too long or was not completed
    goto merge_failed
)

goto wait_loop

:merge_complete
echo Merge appears to be complete!
timeout /t 2 /nobreak >nul

rem Копируем результат
if exist "%REAL_RESULT%" (
    for %%F in ("%REAL_RESULT%") do (
        if %%~zF GTR 0 (
            echo Copying result to Unity's output: %OUTPUT%
            copy /Y "%REAL_RESULT%" "%OUTPUT%" >nul
            if %ERRORLEVEL% EQU 0 (
                echo SUCCESS: Merge completed successfully
                exit /b 0
            ) else (
                echo ERROR: Failed to copy result
                exit /b 1
            )
        )
    )
)

:merge_failed
echo ERROR: Merge failed or was cancelled
rem Копируем CURRENT как fallback
copy /Y "%REAL_CURRENT%" "%OUTPUT%" >nul
exit /b 1
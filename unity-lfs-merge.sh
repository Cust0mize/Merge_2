#!/bin/bash
# Unity YAML Merge wrapper для работы с LFS файлами (полная версия с подробным логированием в stdout)
# Параметры: %O (base) %A (current) %B (other) %P (path)

set -euo pipefail

# -----------------------------
# Вспомогательные функции
# -----------------------------
log() { echo "[unity-lfs-merge] $*"; }
fail() { echo "[unity-lfs-merge][ERROR] $*"; exit 2; }

# -----------------------------
# Аргументы
# -----------------------------
if [ "${#}" -lt 4 ]; then
  fail "Ожидалось 4 аргумента: %O %A %B %P. Получено: ${#}"
fi
BASE="$1"
CURRENT="$2"
OTHER="$3"
PATH_FILE="$4"

log "Args: BASE=%O:'$BASE' CURRENT=%A:'$CURRENT' OTHER=%B:'$OTHER' PATH='%P:$PATH_FILE'"
log "PWD=$(pwd)"

# -----------------------------
# Пути и проверки
# -----------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TOOLS_DIR="$SCRIPT_DIR/Tools"
UNITY_MERGE="$TOOLS_DIR/UnityYAMLMerge.exe"
MERGESPEC="$TOOLS_DIR/mergespecfile.txt"
MERGERULES="$TOOLS_DIR/mergerules.txt"

log "SCRIPT_DIR=$SCRIPT_DIR"
log "TOOLS_DIR=$TOOLS_DIR"

[ -f "$UNITY_MERGE" ] || fail "Не найден UnityYAMLMerge.exe по пути: $UNITY_MERGE"
[ -f "$MERGESPEC" ] || fail "Не найден mergespecfile.txt по пути: $MERGESPEC"
[ -f "$MERGERULES" ] || fail "Не найден mergerules.txt по пути: $MERGERULES"

# -----------------------------
# Определение расширения
# -----------------------------
FILE_EXT="${PATH_FILE##*.}"
if [ -z "$FILE_EXT" ] || [ "$FILE_EXT" = "$PATH_FILE" ]; then
  FILE_EXT="unity" # по умолчанию
fi
log "Detected file extension: .$FILE_EXT"

# -----------------------------
# Временная директория
# -----------------------------
TEMP_DIR="/tmp/unity_merge_$$"
mkdir -p "$TEMP_DIR"
log "TEMP_DIR=$TEMP_DIR"

BASE_REAL="$TEMP_DIR/base.$FILE_EXT"
CURRENT_REAL="$TEMP_DIR/current.$FILE_EXT"
OTHER_REAL="$TEMP_DIR/other.$FILE_EXT"
RESULT="$TEMP_DIR/result.$FILE_EXT"

cleanup() {
  local rc=$?
  log "Cleanup TEMP_DIR=$TEMP_DIR (rc=$rc)"
  rm -rf "$TEMP_DIR" 2>/dev/null || true
}
trap cleanup EXIT

# -----------------------------
# Извлечение содержимого из LFS pointer при необходимости
# -----------------------------
extract_lfs_content() {
  local input_file="$1"
  local output_file="$2"

  log "Extract: '$input_file' -> '$output_file'"
  if [ ! -f "$input_file" ]; then
    log "Input does not exist: $input_file"
    return 1
  fi

  local first_line
  first_line=$(head -n 1 "$input_file" 2>/dev/null || true)
  log "First line of input: ${first_line:-<empty>}"

  if echo "$first_line" | grep -q "^version https://git-lfs.github.com"; then
    log "Detected LFS pointer. Running: git lfs smudge < '$input_file' > '$output_file'"
    if git lfs smudge < "$input_file" > "$output_file" 2>&1; then
      local out_first
      out_first=$(head -n 1 "$output_file" 2>/dev/null || true)
      log "First line after smudge: ${out_first:-<empty>}"
      if echo "$out_first" | grep -q "^version https://git-lfs.github.com"; then
        # Попытка прямого чтения из LFS-кэша
        local oid
        oid=$(grep "^oid sha256:" "$input_file" | cut -d: -f2 | tr -d ' ' || true)
        if [ -n "$oid" ]; then
          local lfs_cache=".git/lfs/objects/${oid:0:2}/${oid:2:2}/$oid"
          log "Smudge still pointer. Trying LFS cache: $lfs_cache"
          if [ -f "$lfs_cache" ]; then
            cp "$lfs_cache" "$output_file"
            log "Copied from LFS cache"
            return 0
          else
            log "LFS cache object not found"
            return 1
          fi
        fi
        return 1
      fi
      return 0
    else
      log "git lfs smudge failed"
      return 1
    fi
  else
    cp "$input_file" "$output_file"
    log "Copied regular file"
    return 0
  fi
}

# -----------------------------
# Готовим входные версии
# -----------------------------
extract_lfs_content "$BASE" "$BASE_REAL" || fail "Не удалось извлечь base"
extract_lfs_content "$CURRENT" "$CURRENT_REAL" || fail "Не удалось извлечь current"
extract_lfs_content "$OTHER" "$OTHER_REAL" || fail "Не удалось извлечь other"

for f in "$BASE_REAL" "$CURRENT_REAL" "$OTHER_REAL"; do
  local_size=$( (stat -c%s "$f" 2>/dev/null || stat -f%z "$f" 2>/dev/null || echo 0) )
  log "File '$f' size: $local_size bytes"
  head -n 2 "$f" | sed 's/^/[unity-lfs-merge] HEAD: /' || true
done

# -----------------------------
# Конвертация путей для Windows (если cygpath доступен)
# -----------------------------
if command -v cygpath >/dev/null 2>&1; then
  BASE_REAL_WIN=$(cygpath -w "$BASE_REAL")
  CURRENT_REAL_WIN=$(cygpath -w "$CURRENT_REAL")
  OTHER_REAL_WIN=$(cygpath -w "$OTHER_REAL")
  RESULT_WIN=$(cygpath -w "$RESULT")
else
  BASE_REAL_WIN="$BASE_REAL"
  CURRENT_REAL_WIN="$CURRENT_REAL"
  OTHER_REAL_WIN="$OTHER_REAL"
  RESULT_WIN="$RESULT"
fi
log "Paths for UnityYAMLMerge:"
log "  BASE   = $BASE_REAL_WIN"
log "  OTHER  = $OTHER_REAL_WIN"
log "  CURRENT= $CURRENT_REAL_WIN"
log "  RESULT = $RESULT_WIN"

# -----------------------------
# Запуск UnityYAMLMerge
# -----------------------------
ORIGINAL_DIR=$(pwd)
cd "$TOOLS_DIR"
log "cd '$TOOLS_DIR' (UnityYAMLMerge ищет конфиги здесь)"

# Управляемый premerge: по умолчанию ВЫКЛ (он может агрессивно авто-сливать)
PREMERGE_FLAG=""
if [ "${UNITY_YAML_PREMERGE:-0}" = "1" ]; then
  PREMERGE_FLAG="-p"
fi


log "Running: $UNITY_MERGE merge ${PREMERGE_FLAG:+$PREMERGE_FLAG }\"$BASE_REAL_WIN\" \"$OTHER_REAL_WIN\" \"$CURRENT_REAL_WIN\" \"$RESULT_WIN\""

# Исполняем с одновременным выводом и записью в файл для анализа
set +e
"$UNITY_MERGE" merge ${PREMERGE_FLAG:+$PREMERGE_FLAG }"$BASE_REAL_WIN" "$OTHER_REAL_WIN" "$CURRENT_REAL_WIN" "$RESULT_WIN" 2>&1 | tee "$TEMP_DIR/merge.out"
MERGE_EXIT_CODE=${PIPESTATUS[0]}
set -e
echo ${MERGE_EXIT_CODE}

cd "$ORIGINAL_DIR"
log "cd back: '$ORIGINAL_DIR'"

# -----------------------------
# Обработка результата
# -----------------------------
if [ $MERGE_EXIT_CODE -eq 0 ]; then
  # Чистый мердж → вернуть LFS pointer
  git lfs clean < "$RESULT" > "$CURRENT" || cp "$RESULT" "$CURRENT"
  exit 0
elif [ $MERGE_EXIT_CODE -eq 1 ]; then
  # Конфликт → записать сырой YAML с маркерами и вернуть 1
  if [ -f "$RESULT" ]; then cp "$RESULT" "$CURRENT"; else cp "$CURRENT_REAL" "$CURRENT"; fi
  exit 1
else
  # Фатальная ошибка
  exit $MERGE_EXIT_CODE
fi
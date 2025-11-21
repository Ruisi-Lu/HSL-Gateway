#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/common.sh"

require_command dotnet

print_separator "HSL Gateway Modbus 訂閱測試"

cat <<'EOF'
此腳本會協助啟動以下服務:
  • Modbus 模擬器 (HslSimulator)
  • Gateway 服務 (HslGateway)
  • 訂閱測試客戶端 (HslSubscriber)

建議測試順序:
  1. 選擇選項 5 - 列出所有設備 (預期看到 modbus_01)
  2. 選擇選項 6 - 列出設備標籤 (line_power, temperature ...)
  3. 選擇選項 3 - 讀取標籤值 (100-200 之間浮動)
  4. 選擇選項 1 - 訂閱標籤值 (每 2 秒更新，可按 Enter 停止)
  5. 另開一個客戶端選項 4 寫入數值，觀察訂閱視窗的變化
EOF

echo
build_projects \
  "HslGateway/HslGateway.csproj" \
  "HslSimulator/HslSimulator.csproj" \
  "HslSubscriber/HslSubscriber.csproj"

echo
echo "此腳本會在同一終端內啟動所有服務，按 Ctrl+C 可全部關閉。"
read -rp "是否要自動啟動所有服務? (y/N) " response

if [[ "$response" =~ ^[Yy]$ ]]; then
  echo
  echo "🚀 正在啟動服務..."
  echo
  start_background "Modbus 模擬器" dotnet run --project HslSimulator/HslSimulator.csproj
  sleep 2
  start_background "HSL Gateway" dotnet run --project HslGateway/HslGateway.csproj
  sleep 3
  start_background "訂閱測試客戶端" dotnet run --project HslSubscriber/HslSubscriber.csproj

  cat <<'EOF'

✅ 所有服務已啟動!
可另外再開一個終端執行 scripts/tests/subscription.sh 只選擇手動模式，按照指示啟動第二個客戶端進行寫入測試。

按 Ctrl+C 停止並清理所有程序。
EOF

  wait
else
  cat <<'EOF'

手動啟動步驟:
  終端 1 - 模擬器:
    dotnet run --project HslSimulator/HslSimulator.csproj

  終端 2 - Gateway:
    dotnet run --project HslGateway/HslGateway.csproj

  終端 3 - 訂閱測試客戶端:
    dotnet run --project HslSubscriber/HslSubscriber.csproj
EOF
fi

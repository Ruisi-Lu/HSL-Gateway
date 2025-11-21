#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC1091
source "$SCRIPT_DIR/common.sh"

require_command dotnet

AUTO_MODE=${HSL_TEST_AUTO:-0}

for arg in "$@"; do
  case "$arg" in
    -a|--auto)
      AUTO_MODE=1
      shift
      ;;
    *)
      ;;
  esac
done

print_separator "HSL Gateway 多設備連接測試"

cat <<'EOF'
此測試將啟動:
  • 3 個 Modbus TCP 模擬器 (Port 50502, 50503, 50504)
  • 1 個 Gateway 服務 (連接全部設備)
  • 6 個標籤 (每台 2 個)

測試配置:
  設備 1 (modbus_01 / Port 50502 / 輪詢 2000ms)
    - line_power (40001)
    - temperature (40002)
  設備 2 (modbus_02 / Port 50503 / 輪詢 1500ms)
    - motor_speed (40001)
    - pressure (40002)
  設備 3 (modbus_03 / Port 50504 / 輪詢 3000ms)
    - flow_rate (40001)
    - level (40002)
EOF

echo
build_projects \
  "HslSimulator/HslSimulator.csproj" \
  "HslGateway/HslGateway.csproj" \
  "HslMultiDeviceTest/HslMultiDeviceTest.csproj"

echo
echo "此腳本會在同一終端內啟動所有服務，按 Ctrl+C 可全部關閉。"
if (( AUTO_MODE )); then
  response="y"
  echo "(自動模式已啟用，直接啟動測試)"
else
  read -rp "是否要啟動多設備測試? (y/N) " response
fi

if [[ "$response" =~ ^[Yy]$ ]]; then
  echo
  echo "🚀 正在啟動服務..."
  echo
  start_background "多設備 Modbus 模擬器" dotnet run --project HslSimulator/HslSimulator.csproj
  sleep 3
  start_background "HSL Gateway (MultiDevice)" env ASPNETCORE_ENVIRONMENT=MultiDevice dotnet run --project HslGateway/HslGateway.csproj --launch-profile HslGateway
  sleep 4

  cat <<'EOF'

✅ 所有服務已啟動!
建議測試步驟:
  1. 選擇選項 1 - 列出所有設備
  2. 選擇選項 2 - 顯示所有標籤
  3. 選擇選項 3 - 讀取所有標籤值
  4. 選擇選項 6 - 同時訂閱所有設備標籤
  5. 選擇選項 7 - 測試多設備並發寫入

按 Ctrl+C 停止並清理所有程序。
EOF

  echo
  echo "📟 多設備測試客戶端會在此終端前景執行，結束後會自動清理背景程序。"
  (
    cd "$REPO_ROOT"
    if (( AUTO_MODE )); then
      dotnet run --project HslMultiDeviceTest/HslMultiDeviceTest.csproj -- --auto-demo
    else
      dotnet run --project HslMultiDeviceTest/HslMultiDeviceTest.csproj
    fi
  )

  echo
  echo "⏹️ 多設備測試客戶端已結束，正在停止背景服務..."
  cleanup_processes
else
  cat <<'EOF'

手動啟動步驟:
  終端 1 - 多設備模擬器:
    dotnet run --project HslSimulator/HslSimulator.csproj

  終端 2 - Gateway (MultiDevice 配置):
    ASPNETCORE_ENVIRONMENT=MultiDevice dotnet run --project HslGateway/HslGateway.csproj --launch-profile HslGateway

  終端 3 - 多設備測試客戶端:
    dotnet run --project HslMultiDeviceTest/HslMultiDeviceTest.csproj
EOF
fi

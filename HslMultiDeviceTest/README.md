# HSL Gateway 多設備連接測試

## 概述

此測試展示 HSL Gateway 如何同時連接和管理多個 Modbus 設備，並進行並發訂閱和寫入操作。

## 測試架構

### 設備配置

測試環境包含 3 個模擬的 Modbus TCP 設備：

| 設備 ID | Port | 輪詢間隔 | 標籤 |
|---------|------|----------|------|
| modbus_01 | 50502 | 2000ms | line_power (40001), temperature (40002) |
| modbus_02 | 50503 | 1500ms | motor_speed (40001), pressure (40002) |
| modbus_03 | 50504 | 3000ms | flow_rate (40001), level (40002) |

**總計**: 3 個設備，6 個標籤

### 特點

- ✅ **獨立輪詢** - 每個設備有自己的輪詢間隔，互不干擾
- ✅ **並發連接** - 所有設備同時連接和輪詢
- ✅ **多客戶端訂閱** - 支援多個客戶端同時訂閱不同設備
- ✅ **並發寫入** - 可以同時向多個設備寫入數據
- ✅ **設備狀態監控** - 即時監控所有設備的在線/離線狀態

## 快速開始

### 使用自動化腳本（推薦）

```powershell
.\TestMultiDevice.ps1
```

選擇 `y` 會自動啟動所有服務。

### 手動啟動

在三個終端機中依序執行：

#### 終端 1: 啟動多設備模擬器
```powershell
dotnet run --project HslSimulator/HslSimulator.csproj
```

會啟動 3 個 Modbus TCP 伺服器：
- Port 50502 (modbus_01)
- Port 50503 (modbus_02)
- Port 50504 (modbus_03)

#### 終端 2: 啟動 Gateway
```powershell
dotnet run --project HslGateway/HslGateway.csproj
```

Gateway 會自動連接所有 3 個設備。

#### 終端 3: 啟動多設備測試客戶端
```powershell
dotnet run --project HslMultiDeviceTest/HslMultiDeviceTest.csproj
```

## 測試功能

### 1. 列出所有設備
查看 Gateway 連接的所有設備列表。

**預期結果**: 顯示 modbus_01, modbus_02, modbus_03

### 2. 顯示所有設備的標籤
列出每個設備的所有標籤配置。

**預期結果**: 顯示 6 個標籤的詳細資訊（名稱、地址、類型）

### 3. 讀取所有標籤的當前值
一次性讀取所有設備的所有標籤值。

**預期結果**: 
```text
🔌 modbus_01:
  line_power      =   145.00 ✅
  temperature     =    62.00 ✅

🔌 modbus_02:
  motor_speed     =  2145.00 ✅
  pressure        =    45.00 ✅

🔌 modbus_03:
  flow_rate       =   287.00 ✅
  level           =    73.00 ✅
```

### 4. 訂閱單一設備的所有標籤
選擇一個設備，訂閱它的所有標籤。

**用途**: 監控特定設備的所有數據點變化

### 5. 訂閱所有設備的特定標籤
輸入標籤名稱模式（例如: "power", "speed"），訂閱所有設備中匹配的標籤。

**用途**: 跨設備監控同類型的數據（例如：所有溫度、所有壓力）

### 6. 同時訂閱所有設備的所有標籤 ⭐
同時訂閱 6 個標籤，即時顯示所有數據變化。

**預期輸出**:
```text
已啟動 6 個訂閱

[17:45:12] modbus_01/line_power    =   156.00
[17:45:12] modbus_02/motor_speed   =  2341.00
[17:45:13] modbus_03/flow_rate     =   342.00
[17:45:13] modbus_01/temperature   =    58.00
[17:45:14] modbus_02/pressure      =    67.00
...
```

按 Enter 停止後會顯示統計：
```text
📊 統計:
  modbus_01: 收到 23 筆更新
  modbus_02: 收到 31 筆更新
  modbus_03: 收到 15 筆更新
```

### 7. 測試多設備並發寫入 ⭐
同時向所有設備的所有標籤寫入隨機值。

**測試內容**:
- 並發執行 6 個寫入操作
- 每個寫入使用獨立的 Task
- 驗證所有寫入是否成功
- 讀取並驗證寫入結果

**預期輸出**:
```text
✅ modbus_01/line_power = 5432 (Success)
✅ modbus_01/temperature = 7821 (Success)
✅ modbus_02/motor_speed = 3456 (Success)
✅ modbus_02/pressure = 8923 (Success)
✅ modbus_03/flow_rate = 2341 (Success)
✅ modbus_03/level = 6789 (Success)

✅ 完成 6 個寫入操作
```

### 8. 監控所有設備狀態
訂閱所有設備的狀態變化（在線/離線）。

**用途**: 監控設備連接狀態，檢測網路問題

## 性能測試要點

### 並發性能
- ✅ 每個設備獨立輪詢，互不阻塞
- ✅ 支援多個客戶端同時訂閱
- ✅ 寫入操作不影響讀取輪詢

### 輪詢間隔差異
測試配置中故意設置不同的輪詢間隔：
- modbus_01: 2000ms (較慢)
- modbus_02: 1500ms (最快)
- modbus_03: 3000ms (最慢)

觀察不同輪詢頻率下的數據更新頻率。

### 數據同步
- 模擬器每 5 秒更新一次數據
- Gateway 根據各自的輪詢間隔獲取數據
- 客戶端通過訂閱即時收到更新

## 故障排除

### 問題: 部分設備無法連接
檢查模擬器是否已啟動所有 3 個伺服器（Port 50502, 50503, 50504）

### 問題: 訂閱沒有收到數據
- 確認 Gateway 已啟動並連接到設備
- 檢查 `appsettings.json` 中的設備配置
- 查看 Gateway 日誌輸出

### 問題: 寫入失敗
確認模擬器支援寫入操作（HslCommunication ModbusTcpServer 預設支援）

## 擴展測試

### 添加更多設備
1. 在模擬器中添加更多 `ModbusTcpServer` 實例
2. 在 `appsettings.json` 中添加設備配置
3. 重啟服務

### 測試設備離線恢復
1. 訂閱設備狀態
2. 停止模擬器
3. 觀察設備離線通知
4. 重啟模擬器
5. 觀察設備恢復連接

### 壓力測試
- 增加設備數量（10+ 設備）
- 減少輪詢間隔（< 1000ms）
- 增加標籤數量（每個設備 10+ 標籤）
- 多個客戶端同時訂閱

## 技術架構

### 設備管理
- `DeviceRegistry` - 管理所有設備客戶端
- `PollingWorker` - 為每個設備創建獨立的後台輪詢任務

### 數據緩存
- `TagValueCache` - 內存緩存，存儲所有標籤的最新值
- 使用事件機制通知訂閱者數據變更

### gRPC 流式傳輸
- Server-Side Streaming RPC
- 每個訂閱創建獨立的通道
- 支援並發多個訂閱流

## 相關檔案

- `HslSimulator/Program.cs` - 多設備模擬器
- `HslMultiDeviceTest/Program.cs` - 多設備測試客戶端
- `HslGateway/appsettings.json` - 多設備配置
- `TestMultiDevice.ps1` - 自動化測試腳本

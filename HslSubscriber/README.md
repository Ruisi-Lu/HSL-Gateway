# HSL Gateway 訂閱測試 - README

## 概述

此測試程式用於驗證 HSL Gateway 的 Modbus 訂閱功能。

## 功能特色

### HslSubscriber 客戶端提供以下功能：

1. **訂閱標籤值變化** (SubscribeTagValue)
   - 即時監控 Modbus 標籤值的變化
   - 顯示時間戳記、數值和品質狀態
   - 按 Enter 鍵停止訂閱

2. **訂閱設備狀態變化** (SubscribeDeviceStatus)
   - 監控設備在線/離線狀態
   - 即時通知狀態變更
   - 按 Enter 鍵停止訂閱

3. **讀取單個標籤值** (GetTagValue)
   - 一次性讀取標籤值
   - 顯示完整的標籤資訊

4. **寫入標籤值** (WriteTagValue)
   - 向 Modbus 設備寫入數值
   - 自動驗證寫入結果

5. **列出所有設備** (ListDevices)
   - 查看 Gateway 連接的所有設備

6. **列出設備標籤** (ListDeviceTags)
   - 查看指定設備的所有標籤配置
   - 顯示標籤名稱、地址和資料類型

7. **變更訂閱設定**
   - 動態切換要訂閱的設備和標籤

## 測試步驟

### 方法一: 使用示範腳本（推薦）

```powershell
# 顯示測試說明和步驟
.\DemoSubscription.ps1
```

然後按照說明在三個不同的終端機中手動啟動各個服務。

### 方法二: 手動啟動

#### 1. 啟動 Modbus 模擬器 (終端機 1)
```powershell
dotnet run --project HslSimulator/HslSimulator.csproj
```
模擬器會在 Port 50502 啟動，並每 5 秒自動更新 `line_power` 標籤值。

#### 2. 啟動 HSL Gateway (終端機 2)
```powershell
dotnet run --project HslGateway/HslGateway.csproj
```
Gateway 會在 Port 50051 啟動 gRPC 服務。

#### 3. 啟動訂閱測試客戶端 (終端機 3)
```powershell
dotnet run --project HslSubscriber/HslSubscriber.csproj
```

#### 4. 在客戶端中進行測試

**建議測試流程：**

1. 選擇選項 **5** - 列出所有設備
   - 確認 `modbus_01` 設備已連接

2. 選擇選項 **6** - 列出設備標籤
   - 查看 `line_power` 標籤的配置資訊

3. 選擇選項 **3** - 讀取標籤值
   - 查看當前的 `line_power` 數值

4. 選擇選項 **1** - 訂閱標籤值
   - 開始即時監控數值變化
   - 每 2 秒會看到 Gateway 輪詢的結果
   - 每 5 秒會看到模擬器更新的新數值
   - **按 Enter 鍵停止訂閱**

5. 測試寫入功能 (開啟新終端機)
   ```powershell
   dotnet run --project HslSubscriber/HslSubscriber.csproj
   ```
   - 選擇選項 **4** - 寫入標籤值
   - 輸入一個數值 (例如: 999)
   - 在訂閱視窗中觀察數值是否立即變更

## 預期結果

### 訂閱標籤值時的輸出範例：

```text
🔔 開始訂閱標籤值: modbus_01/line_power
   (按 Enter 停止訂閱)

[0001] 14:23:15.123 | modbus_01/line_power =   145.00 ✅
[0002] 14:23:17.234 | modbus_01/line_power =   145.00 ✅
[0003] 14:23:19.345 | modbus_01/line_power =   145.00 ✅
[0004] 14:23:20.456 | modbus_01/line_power =   167.00 ✅  ← 模擬器自動更新
[0005] 14:23:22.567 | modbus_01/line_power =   167.00 ✅
[0006] 14:23:24.678 | modbus_01/line_power =   999.00 ✅  ← 手動寫入的值
```

### 寫入標籤值的輸出範例：

```text
✏️  輸入要寫入 modbus_01/line_power 的數值: 999
⏳ 正在寫入...
✅ 寫入成功: 999

🔍 驗證寫入結果...

┌─────────────────────────────────────
│ 設備 ID:   modbus_01
│ 標籤名稱:  line_power
│ 數值:      999.00
│ 時間戳記:  2025-11-20 14:23:24.678
│ 品質:      good
└─────────────────────────────────────
```

## 配置參數

預設配置 (可通過命令行參數修改):
- 伺服器地址: `http://localhost:50051`
- 設備 ID: `modbus_01`
- 標籤名稱: `line_power`

自訂參數啟動:
```powershell
dotnet run --project HslSubscriber/HslSubscriber.csproj -- http://localhost:50051 siemens_01 motor_speed
```

## 故障排除

### 問題: 連接失敗
- 確認 Gateway 服務已啟動並運行在 Port 50051
- 確認模擬器已啟動並運行在 Port 50502
- 檢查防火牆設定

### 問題: 無法讀取標籤值
- 確認 `appsettings.json` 中的設備和標籤配置正確
- 查看 Gateway 的日誌輸出

### 問題: 訂閱沒有收到更新
- 確認 Gateway 的 `PollIntervalMs` 設定
- 確認標籤地址配置正確
- 檢查設備是否在線

## 技術細節

- **gRPC 通訊協定**: HTTP/2
- **序列化**: Protocol Buffers
- **流式傳輸**: Server-Side Streaming RPC
- **輪詢間隔**: 2000ms (可在 appsettings.json 修改)
- **模擬器更新間隔**: 5000ms

## 相關檔案

- `HslSubscriber/HslSubscriber.csproj` - 專案檔
- `HslSubscriber/Program.cs` - 主程式
- `HslGateway/Protos/gateway.proto` - gRPC 服務定義
- `HslGateway/appsettings.json` - Gateway 配置
- `TestSubscription.ps1` - 自動化測試腳本

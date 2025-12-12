# HSL Gateway 使用手冊

HSL Gateway 是一個高效能的工業物聯網閘道器，旨在將工業設備（如 Siemens PLC, Modbus 設備）的數據透過 gRPC 介面提供給上層 IT 系統。

## 1. 系統需求

- **作業系統**: Windows, Linux, 或 macOS
- **執行環境**: .NET 10 SDK 或 Runtime
- **容器化 (選用)**: Docker

## 2. 快速開始

### 2.1 下載與編譯

```bash
git clone https://github.com/Ruisi-Lu/HSL-Gateway.git
cd HSL-gateway
dotnet restore
dotnet build
```

### 2.2 執行模擬器 (測試用)

如果您手邊沒有實體設備，可以啟動內建的 Modbus TCP 模擬器：

```bash
dotnet run --project HslSimulator/HslSimulator.csproj
```
*模擬器將在 Port 50502 啟動。*

### 2.3 啟動 Gateway

```bash
dotnet run --project HslGateway/HslGateway.csproj
```
*Gateway 將在 Port 50051 啟動 (gRPC)。*

## 3. 設定說明 (Configuration)

設定檔位於 `HslGateway/appsettings.json`。

### 3.1 設備設定 (Devices)

在 `Gateway:Devices` 區段定義要連線的設備。

| 欄位 | 說明 | 範例 |
|------|------|------|
| `Id` | 設備唯一識別碼 (字串) | `"siemens_01"` |
| `Type` | 設備類型 (`SiemensS7` 或 `ModbusTcp`) | `"SiemensS7"` |
| `Ip` | 設備 IP 位址 | `"192.168.1.10"` |
| `Port` | 連線 Port | `102` (Siemens), `502` (Modbus) |
| `Rack` | 機架號 (僅 Siemens) | `0` |
| `Slot` | 插槽號 (僅 Siemens) | `1` |
| `PlcModel` | Siemens PLC 系列（`S300`, `S1200`, `S1500` 等） | `"S300"` |
| `PollIntervalMs` | 輪詢間隔 (毫秒) | `1000` |
| `PortName` | 序列埠名稱 (僅 Modbus RTU) | `"COM1"` 或 `"/dev/ttyUSB0"` |
| `BaudRate` | 鮑率 (僅 Modbus RTU) | `9600` |
| `DataBits` | 資料位元 (僅 Modbus RTU) | `8` |
| `StopBits` | 停止位元 (僅 Modbus RTU) | `1` |
| `Parity` | 同位檢查 (僅 Modbus RTU) | `0` (None), `1` (Odd), `2` (Even) |
| `Station` | 站號 (僅 Modbus RTU) | `1` |

**範例：**

```json
{
  "Id": "siemens_01",
  "Type": "SiemensS7",
  "Ip": "192.168.1.10",
  "Port": 102,
  "Rack": 0,
  "Slot": 1,
  "PlcModel": "S1500",
  "PollIntervalMs": 1000
}
```

### 3.2 標籤設定 (Tags)

在 `Gateway:Tags` 區段定義要讀取的數據點。

| 欄位 | 說明 | 範例 |
|------|------|------|
| `DeviceId` | 對應的設備 ID | `"siemens_01"` |
| `Name` | 標籤名稱 (自訂) | `"motor_speed"` |
| `Address` | 設備位址 | `"DB1.0"` (Siemens), `"40001"` (Modbus) |
| `DataType` | 資料型別 (`double`, `int`, `short`, `float`, `bool`) | `"double"` |

**範例：**

```json
{
  "DeviceId": "siemens_01",
  "Name": "motor_speed",
  "Address": "DB1.0",
  "DataType": "double"
}
```

### 3.3 企業版授權 (Enterprise License)

若您持有 HslCommunication 的企業版授權，可以在 `appsettings.json`（或環境專屬檔案）中設定 `EnterpriseLicense`，讓 Gateway 在啟動時自動載入授權：

```json
"EnterpriseLicense": {
  "AutoLoadOnStartup": true,
  "CertificateFilePath": "data/hsl-enterprise.cert",
  "CertificateEnvironmentVariable": "HSL_ENTERPRISE_CERT_BASE64",
  "AuthorizationCodeEnvironmentVariable": "HSL_ENTERPRISE_AUTH_CODE",
  "ContactInfo": "ops-team@example.com"
}
```

當 `AutoLoadOnStartup` 為 `true` 時，系統會依以下順序嘗試載入授權，任一成功即停止：

1. `HSL_ENTERPRISE_CERT_BASE64`: 將官方授權憑證檔案轉為 Base64 字串後寫入此環境變數。
2. `HSL_ENTERPRISE_AUTH_CODE`: 將 HslCommunication 提供的授權碼 (Authorization Code) 直接寫入此環境變數。
3. `CertificateFilePath`: 將授權檔案放在指定路徑，預設是 `data/hsl-enterprise.cert`。

內建的 `EnterpriseLicenseInitializer` 會在日誌中紀錄使用了哪個來源。如果三種來源皆不存在，Gateway 會以社群版模式啟動而不會失敗；如需停用自動授權，可將 `AutoLoadOnStartup` 設為 `false`。

### 3.4 S7-300 測試伺服器示例

Repo 內的 `s7_300_demo` 設備預設指向 HslCommunication 官方公開的 S7-300 測試 CPU（`IP = 118.24.36.220`、`Rack = 0`、`Slot = 2`）。若想在執行中的 Gateway 透過 gRPC 新增該裝置，可使用：

```powershell
grpcurl -plaintext -import-path HslGateway/Protos -proto gateway.proto \
  -d '{
        "id":"s7_demo_runtime",
        "type":"SiemensS7",
        "ip":"118.24.36.220",
        "port":102,
        "rack":0,
        "slot":2,
        "plcModel":"S300",
        "pollIntervalMs":1000
      }' \
  localhost:50051 hslgateway.ConfigManager/UpsertDevice
```

接著依官方說明新增三個測試標籤：

```powershell
grpcurl -plaintext -import-path HslGateway/Protos -proto gateway.proto \
  -d '{"deviceId":"s7_demo_runtime","name":"coil_m100_0","address":"M100.0","dataType":"bool"}' \
  localhost:50051 hslgateway.ConfigManager/UpsertTag

grpcurl -plaintext -import-path HslGateway/Protos -proto gateway.proto \
  -d '{"deviceId":"s7_demo_runtime","name":"word_mw100","address":"M100","dataType":"short"}' \
  localhost:50051 hslgateway.ConfigManager/UpsertTag

grpcurl -plaintext -import-path HslGateway/Protos -proto gateway.proto \
  -d '{"deviceId":"s7_demo_runtime","name":"db1_real","address":"DB1.DBD0","dataType":"float"}' \
  localhost:50051 hslgateway.ConfigManager/UpsertTag
```

完成後即可透過 `GetTagValue`/`SubscribeTagValue` 驗證遠端 PLC 的即時資料，無須手動編輯 `data/gateway-config.json`。

## 4. API 使用指南 (gRPC)

本服務使用 gRPC 介面，預設 Port 為 `50051`。

### 4.1 取得標籤數值 (GetTagValue)

**Request:**

```json
{
  "deviceId": "siemens_01",
  "tagName": "motor_speed"
}
```

**Response:**

```json
{
  "deviceId": "siemens_01",
  "tagName": "motor_speed",
  "value": 123.45,
  "timestampUtc": "2023-10-27T10:00:00Z",
  "quality": "good"
}
```

### 4.2 列出所有設備 (ListDevices)

**Request:** `Empty`

**Response:**

```json
{
  "devices": [
    { "id": "siemens_01" },
    { "id": "modbus_01" }
  ]
}
```

### 4.3 寫入標籤數值 (WriteTagValue)

**Request:**

```json
{
  "deviceId": "modbus_01",
  "tagName": "line_power",
  "value": 1234.5
}
```

**Response:**

```json
{
  "success": true,
  "message": "Success"
}
```

### 4.4 訂閱標籤數值 (SubscribeTagValue)

此為 Server-Streaming RPC，客戶端連線後會持續收到數據變更通知。

**Request:**

```json
{
  "deviceId": "modbus_01",
  "tagName": "line_power"
}
```

**Response Stream:**

```json
{ "deviceId": "modbus_01", "tagName": "line_power", "value": 100, ... }
{ "deviceId": "modbus_01", "tagName": "line_power", "value": 101, ... }
...
```

#### 4.4.1 使用內建訂閱客戶端

1. 依 2.2、2.3 節啟動模擬器與 Gateway。
1. 執行互動式訂閱程式：

  ```powershell
  dotnet run --project HslSubscriber/HslSubscriber.csproj http://localhost:50051 modbus_01 line_power
  ```

1. 在選單中選 `1` 以訂閱標籤值，或選 `2` 以監看設備狀態，按 Enter 可停止目前的訂閱。

此工具同時提供 `GetTagValue`、`WriteTagValue`、`ListDevices`、`ListDeviceTags` 等快捷操作，適合快速驗證整體流程。

#### 4.4.2 使用 grpcurl 或客製化客戶端

需要腳本化流程或整合至 CI 時，可使用 `grpcurl`（或任一 gRPC SDK）：

```bash
grpcurl -plaintext \
  -import-path HslGateway/Protos \
  -proto gateway.proto \
  -d '{"deviceId":"modbus_01","tagName":"line_power"}' \
  localhost:50051 hslgateway.Gateway/SubscribeTagValue
```

命令會持續輸出直到您手動停止 (Ctrl+C)。若要同時監控多個標籤，可開多條串流或自行撰寫客戶端整合邏輯。

### 4.5 訂閱設備狀態 (SubscribeDeviceStatus)

`SubscribeDeviceStatus` 會回報設備連線/離線狀態，`deviceId` 可為空字串（表示所有設備）。

```bash
grpcurl -plaintext \
  -import-path HslGateway/Protos \
  -proto gateway.proto \
  -d '{"deviceId":""}' \
  localhost:50051 hslgateway.Gateway/SubscribeDeviceStatus
```

**範例回應：**

```json
{ "deviceId": "modbus_01", "isOnline": true, "timestampUtc": "2024-12-01T08:00:12.345Z" }
```

選單項目 `2`（HslSubscriber）會使用同一 API。若想要一次啟動模擬器、Gateway 與訂閱客戶端，可在 Bash/WSL/macOS 下執行 `scripts/tests/subscription.sh`。

## 5. 驗證工具 (HslVerifier)

專案內建一個自動化驗證工具，可用於測試所有 API 功能。

```bash
dotnet run --project HslVerifier/HslVerifier.csproj
```

## 6. 動態設備與標籤註冊 (ConfigManager)

`ConfigManager` gRPC 服務允許您在不中斷 Gateway 的情況下新增、更新或刪除設備與標籤。變更會即時套用並同步寫入 `data/gateway-config.json`（或 `GatewayPersistence:ConfigFilePath` 指定的路徑）。

### 6.1 查看目前配置

```bash
grpcurl -plaintext -import-path HslGateway/Protos -proto gateway.proto \
  localhost:50051 hslgateway.ConfigManager/ListDevicesConfig

grpcurl -plaintext -import-path HslGateway/Protos -proto gateway.proto \
  -d '{"deviceId":"modbus_01"}' \
  localhost:50051 hslgateway.ConfigManager/ListTagConfigs
```

### 6.2 新增或更新設備

```bash
grpcurl -plaintext -import-path HslGateway/Protos -proto gateway.proto \
  -d '{
        "id":"modbus_runtime",
        "type":"ModbusTcp",
        "ip":"127.0.0.1",
        "port":50502,
        "pollIntervalMs":1000
      }' \
  localhost:50051 hslgateway.ConfigManager/UpsertDevice
```

- TCP 設備必須提供 `ip` 與 `port`，Modbus RTU 則需改填 `portName` 及序列埠參數。
- `pollIntervalMs` 必須大於 0，如需調整可再次呼叫 `UpsertDevice`。

### 6.3 即時新增標籤

```bash
grpcurl -plaintext -import-path HslGateway/Protos -proto gateway.proto \
  -d '{
        "deviceId":"modbus_runtime",
        "name":"line_power",
        "address":"40001",
        "dataType":"short"
      }' \
  localhost:50051 hslgateway.ConfigManager/UpsertTag
```

新增後 Gateway 馬上開始輪詢；既有訂閱會在下一次輪詢後收到資料。

### 6.4 移除設備或標籤

```bash
# 刪除整個設備（同時移除其標籤）
grpcurl -plaintext -import-path HslGateway/Protos -proto gateway.proto \
  -d '{"deviceId":"modbus_runtime"}' \
  localhost:50051 hslgateway.ConfigManager/DeleteDevice

# 僅刪除單一標籤
grpcurl -plaintext -import-path HslGateway/Protos -proto gateway.proto \
  -d '{"deviceId":"modbus_runtime","tagName":"line_power"}' \
  localhost:50051 hslgateway.ConfigManager/DeleteTag
```

### 6.5 設定檔儲存與環境檔

- 預設會在 `data/gateway-config.json` 留下最新快照，可透過 `GatewayPersistence:ConfigFilePath` 改到其他路徑。
- 若要載入多設備示例 (`appsettings.MultiDevice.json`)，可設定 `ASPNETCORE_ENVIRONMENT=MultiDevice` 或執行 `dotnet run --project HslGateway/HslGateway.csproj --launch-profile MultiDevice`。
- `scripts/tests/multi-device.sh` 會同時啟動模擬器、Gateway（MultiDevice Profile）與 `HslMultiDeviceTest`，方便驗證大量設備的動態更新。

## 7. 常見問題 (FAQ)

**Q: 如何新增支援的設備類型？**
A: 需要修改 `DeviceRegistry.cs` 並實作新的 `IHslClient`。

**Q: 輪詢速度太慢怎麼辦？**
A: 請檢查 `PollIntervalMs` 設定，並確認網路連線品質。每個設備都是獨立執行緒，互不影響。

**Q: 支援寫入功能嗎？**
A: 是的，已支援 `WriteTagValue` API。

**Q: 支援 Modbus RTU 嗎？**
A: 是的，請在設定檔中指定 `Type` 為 `ModbusRtu` 並設定 `PortName` 等參數。

**Q: 如何新增支援的設備類型？**
A: 需要修改 `DeviceRegistry.cs` 並實作新的 `IHslClient`。

**Q: 輪詢速度太慢怎麼辦？**
A: 請檢查 `PollIntervalMs` 設定，並確認網路連線品質。每個設備都是獨立執行緒，互不影響。

**Q: 支援寫入功能嗎？**
A: 是的，已支援 `WriteTagValue` API。

**Q: 支援 Modbus RTU 嗎？**
A: 是的，請在設定檔中指定 `Type` 為 `ModbusRtu` 並設定 `PortName` 等參數。

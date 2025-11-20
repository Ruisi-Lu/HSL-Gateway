# HSL Gateway 使用手冊

HSL Gateway 是一個高效能的工業物聯網閘道器，旨在將工業設備（如 Siemens PLC, Modbus 設備）的數據透過 gRPC 介面提供給上層 IT 系統。

## 1. 系統需求

- **作業系統**: Windows, Linux, 或 macOS
- **執行環境**: .NET 8 SDK 或 Runtime
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
| `PollIntervalMs` | 輪詢間隔 (毫秒) | `1000` |

**範例：**
```json
{
  "Id": "siemens_01",
  "Type": "SiemensS7",
  "Ip": "192.168.1.10",
  "Port": 102,
  "Rack": 0,
  "Slot": 1,
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
| `DataType` | 資料型別 (`double`, `int`, `short`, `float`) | `"double"` |

**範例：**
```json
{
  "DeviceId": "siemens_01",
  "Name": "motor_speed",
  "Address": "DB1.0",
  "DataType": "double"
}
```

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

## 5. 常見問題 (FAQ)

**Q: 如何新增支援的設備類型？**
A: 需要修改 `DeviceRegistry.cs` 並實作新的 `IHslClient`。

**Q: 輪詢速度太慢怎麼辦？**
A: 請檢查 `PollIntervalMs` 設定，並確認網路連線品質。每個設備都是獨立執行緒，互不影響。

**Q: 支援寫入功能嗎？**
A: 底層 `IHslClient` 已支援 `WriteAsync`，但目前 gRPC 介面尚未開放寫入 API，需自行擴充 `gateway.proto` 與 `GatewayService.cs`。

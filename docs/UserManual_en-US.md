# HSL Gateway User Manual

HSL Gateway is a high-performance Industrial IoT gateway designed to bridge industrial devices (such as Siemens PLCs, Modbus devices) with modern IT systems via a gRPC interface.

## 1. System Requirements

- **OS**: Windows, Linux, or macOS
- **Runtime**: .NET 8 SDK or Runtime
- **Containerization (Optional)**: Docker

## 2. Quick Start

### 2.1 Download and Build

```bash
git clone https://github.com/Ruisi-Lu/HSL-Gateway.git
cd HSL-gateway
dotnet restore
dotnet build
```

### 2.2 Run Simulator (Optional)

If you don't have physical devices, you can start the built-in Modbus TCP simulator:

```bash
dotnet run --project HslSimulator/HslSimulator.csproj
```
*The simulator will start on Port 50502.*

### 2.3 Start Gateway

```bash
dotnet run --project HslGateway/HslGateway.csproj
```
*The Gateway will start on Port 50051 (gRPC).*

## 3. Configuration

The configuration file is located at `HslGateway/appsettings.json`.

### 3.1 Devices

Define devices to connect to in the `Gateway:Devices` section.

| Field | Description | Example |
|-------|-------------|---------|
| `Id` | Unique Device ID (string) | `"siemens_01"` |
| `Type` | Device Type (`SiemensS7` or `ModbusTcp`) | `"SiemensS7"` |
| `Ip` | Device IP Address | `"192.168.1.10"` |
| `Port` | Connection Port | `102` (Siemens), `502` (Modbus) |
| `Rack` | Rack Number (Siemens only) | `0` |
| `Slot` | Slot Number (Siemens only) | `1` |
| `PlcModel` | Siemens PLC series (`S300`, `S1200`, `S1500`, etc.) | `"S300"` |
| `PollIntervalMs` | Polling Interval (ms) | `1000` |
| `PortName` | Serial Port Name (Modbus RTU only) | `"COM1"` or `"/dev/ttyUSB0"` |
| `BaudRate` | Baud Rate (Modbus RTU only) | `9600` |
| `DataBits` | Data Bits (Modbus RTU only) | `8` |
| `StopBits` | Stop Bits (Modbus RTU only) | `1` |
| `Parity` | Parity (Modbus RTU only) | `0` (None), `1` (Odd), `2` (Even) |
| `Station` | Station ID (Modbus RTU only) | `1` |

**Example:**

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

### 3.2 Tags

Define data points to read in the `Gateway:Tags` section.

| Field | Description | Example |
|-------|-------------|---------|
| `DeviceId` | Corresponding Device ID | `"siemens_01"` |
| `Name` | Tag Name (Custom) | `"motor_speed"` |
| `Address` | Device Address | `"DB1.0"` (Siemens), `"40001"` (Modbus) |
| `DataType` | Data Type (`double`, `int`, `short`, `float`, `bool`) | `"double"` |

**Example:**

```json
{
  "DeviceId": "siemens_01",
  "Name": "motor_speed",
  "Address": "DB1.0",
  "DataType": "double"
}
```

### 3.3 Enterprise License

Set the `EnterpriseLicense` section in `appsettings.json` (or `appsettings.<Environment>.json`) to have the gateway automatically switch to the HslCommunication enterprise edition as soon as it starts:

```json
"EnterpriseLicense": {
  "AutoLoadOnStartup": true,
  "CertificateFilePath": "data/hsl-enterprise.cert",
  "CertificateEnvironmentVariable": "HSL_ENTERPRISE_CERT_BASE64",
  "AuthorizationCodeEnvironmentVariable": "HSL_ENTERPRISE_AUTH_CODE",
  "ContactInfo": "ops-team@example.com"
}
```

When `AutoLoadOnStartup` is `true`, the gateway evaluates the sources in this order and stops at the first successful activation:

1. `HSL_ENTERPRISE_CERT_BASE64`: set this environment variable to the Base64 representation of your HslCommunication certificate file.
2. `HSL_ENTERPRISE_AUTH_CODE`: alternatively, set this variable to the plain-text authorization code supplied by HslCommunication.
3. `CertificateFilePath`: keep a certificate file on disk (default `data/hsl-enterprise.cert`).

The `EnterpriseLicenseInitializer` hosted service logs which source was applied. If none of the three inputs exist, the gateway continues in community mode without failing startup. Clear `AutoLoadOnStartup` if you want to skip license activation entirely.

### 3.4 Siemens S7-300 Sample (Test Server)

An S7-300 friendly preset (`s7_300_demo`) now ships with the repository and targets the public HslCommunication demo CPU (`IP = 118.24.36.220`, `Rack = 0`, `Slot = 2`). You can push the same configuration at runtime over gRPC:

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

Add the three demo tags highlighted in the HslCommunication quick start:

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

Use `GetTagValue` or `SubscribeTagValue` against `s7_demo_runtime` to verify the remote PLC connection without touching the persisted JSON snapshot.

## 4. API Usage (gRPC)

The service uses gRPC and listens on Port `50051` by default.

### 4.1 Get Tag Value (GetTagValue)

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

### 4.2 List Devices (ListDevices)

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

### 4.3 Write Tag Value (WriteTagValue)

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

### 4.4 Subscribe Tag Value (SubscribeTagValue)

This is a Server-Streaming RPC. The client will receive continuous updates after connection.

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

#### 4.4.1 Quick Test with the Built-in Subscriber

1. Start the simulator and gateway (see sections 2.2 and 2.3).
1. Run the interactive subscriber client:

  ```powershell
  dotnet run --project HslSubscriber/HslSubscriber.csproj http://localhost:50051 modbus_01 line_power
  ```

1. Choose option `1` in the menu to stream tag updates, or option `2` to monitor device status changes. Press `Enter` to stop the current subscription.

The tool also exposes shortcuts for `GetTagValue`, `WriteTagValue`, `ListDevices`, and `ListDeviceTags`, making it the fastest way to validate end-to-end polling.

#### 4.4.2 Subscribe via grpcurl or Custom Clients

Use `grpcurl` (or any gRPC SDK) when you need a scriptable workflow or to integrate with CI:

```bash
grpcurl -plaintext \
  -import-path HslGateway/Protos \
  -proto gateway.proto \
  -d '{"deviceId":"modbus_01","tagName":"line_power"}' \
  localhost:50051 hslgateway.Gateway/SubscribeTagValue
```

The command keeps running until you stop it (Ctrl+C). Each response contains `timestampUtc`, `quality`, and the most recent numeric value. If you want to monitor multiple tags, run one subscription per stream or embed the logic in your own client using `Gateway.GatewayClient`.

### 4.5 Subscribe Device Status (SubscribeDeviceStatus)

`SubscribeDeviceStatus` streams live online/offline transitions for either all devices (empty `deviceId`) or a single device:

```bash
grpcurl -plaintext \
  -import-path HslGateway/Protos \
  -proto gateway.proto \
  -d '{"deviceId":""}' \
  localhost:50051 hslgateway.Gateway/SubscribeDeviceStatus
```

Sample response:

```json
{ "deviceId": "modbus_01", "isOnline": true, "timestampUtc": "2024-12-01T08:00:12.345Z" }
```

This is useful for dashboards that need to alert when a PLC drops offline. The `HslSubscriber` menu option `2` exercises the same API if you prefer an interactive client. For an automated smoke test across multiple devices, run `scripts/tests/subscription.sh` (Bash/WSL/macOS) which wires up the simulator, gateway, and subscriber in one terminal.

## 5. Verification Tool (HslVerifier)

The project includes a built-in verification tool to test all API features.

```bash
dotnet run --project HslVerifier/HslVerifier.csproj
```

## 6. Runtime Configuration & Dynamic Device Registration

The `ConfigManager` gRPC service lets you add, update, or remove devices and tags without restarting the gateway. Every change is persisted to `data/gateway-config.json` (or the path defined by `GatewayPersistence:ConfigFilePath`), and the `DeviceRegistry` hot-swaps the corresponding drivers automatically.

### 6.1 List the Current Configuration

```bash
grpcurl -plaintext -import-path HslGateway/Protos -proto gateway.proto \
  localhost:50051 hslgateway.ConfigManager/ListDevicesConfig

grpcurl -plaintext -import-path HslGateway/Protos -proto gateway.proto \
  -d '{"deviceId":"modbus_01"}' \
  localhost:50051 hslgateway.ConfigManager/ListTagConfigs
```

### 6.2 Add or Update a Device

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

- `ip`/`port` are required for TCP devices, while `portName`/serial fields are required for Modbus RTU.
- `pollIntervalMs` must be > 0. You can change it later with another `UpsertDevice` call.

### 6.3 Add Tags Dynamically

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

The gateway starts polling the new tag immediately. Existing subscribers receive the next change as soon as the `PollingWorker` captures it.

### 6.4 Remove Devices or Tags

```bash
# Remove a device (also prunes its tags)
grpcurl -plaintext -import-path HslGateway/Protos -proto gateway.proto \
  -d '{"deviceId":"modbus_runtime"}' \
  localhost:50051 hslgateway.ConfigManager/DeleteDevice

# Remove a single tag
grpcurl -plaintext -import-path HslGateway/Protos -proto gateway.proto \
  -d '{"deviceId":"modbus_runtime","tagName":"line_power"}' \
  localhost:50051 hslgateway.ConfigManager/DeleteTag
```

### 6.5 Persisted Files and Environments

- The persisted snapshot lives under `data/gateway-config.json` by default. Set `GatewayPersistence:ConfigFilePath` in `appsettings*.json` to move it elsewhere.
- To preload the multi-device sample (`appsettings.MultiDevice.json`), run the gateway with `ASPNETCORE_ENVIRONMENT=MultiDevice` or use `dotnet run --project HslGateway/HslGateway.csproj --launch-profile MultiDevice`.
- The `scripts/tests/multi-device.sh` helper spins up the simulator, gateway (MultiDevice profile), and `HslMultiDeviceTest` client to showcase large-scale dynamic updates.

## 7. FAQ

**Q: How to add new device types?**
A: You need to modify `DeviceRegistry.cs` and implement a new `IHslClient`.

**Q: What if polling is too slow?**
A: Check the `PollIntervalMs` setting and network quality. Each device runs in its own thread, so they do not block each other.

**Q: Is writing supported?**
A: Yes, the `WriteTagValue` API is supported.

**Q: Is Modbus RTU supported?**
A: Yes, specify `Type` as `ModbusRtu` in the configuration and set `PortName` and other parameters.

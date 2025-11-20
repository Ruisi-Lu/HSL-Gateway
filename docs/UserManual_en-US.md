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
| `PollIntervalMs` | Polling Interval (ms) | `1000` |

**Example:**
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

### 3.2 Tags

Define data points to read in the `Gateway:Tags` section.

| Field | Description | Example |
|-------|-------------|---------|
| `DeviceId` | Corresponding Device ID | `"siemens_01"` |
| `Name` | Tag Name (Custom) | `"motor_speed"` |
| `Address` | Device Address | `"DB1.0"` (Siemens), `"40001"` (Modbus) |
| `DataType` | Data Type (`double`, `int`, `short`, `float`) | `"double"` |

**Example:**
```json
{
  "DeviceId": "siemens_01",
  "Name": "motor_speed",
  "Address": "DB1.0",
  "DataType": "double"
}
```

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

## 5. FAQ

**Q: How to add new device types?**
A: You need to modify `DeviceRegistry.cs` and implement a new `IHslClient`.

**Q: What if polling is too slow?**
A: Check the `PollIntervalMs` setting and network quality. Each device runs in its own thread, so they do not block each other.

**Q: Is writing supported?**
A: The underlying `IHslClient` supports `WriteAsync`, but the gRPC interface does not expose a write API yet. You would need to extend `gateway.proto` and `GatewayService.cs`.

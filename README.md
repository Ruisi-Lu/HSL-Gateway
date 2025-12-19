# HSL Gateway


HSL Gateway is a high-performance, production-ready gRPC service designed to bridge industrial devices (Siemens S7, Modbus TCP) with modern IT systems. Built on **.NET 10 LTS**, it leverages the **HslCommunication** library to poll devices efficiently and exposes real-time data via a **gRPC** interface.

## 🚀 Features

- **Multi-Protocol Support**: Native support for Siemens S7 (S1200, S1500, etc.) and Modbus TCP.
- **gRPC Interface**: Fast, strongly-typed API for reading tag values and device lists.
- **High Performance**: In-memory caching (`TagValueCache`) ensures low-latency data access.
- **Background Polling**: Dedicated background service (`PollingWorker`) handles device communication independently of API requests.
- **Resilient**: Automatic reconnection and error handling for device communication.
- **Docker Ready**: Includes a multi-stage Dockerfile for easy deployment on Linux/Kubernetes.
- **Simulator Included**: Comes with a Modbus TCP simulator for testing and verification.
- **Scalable**: Supports connecting to multiple devices simultaneously with parallel polling.
- **Enterprise Ready**: Boots with HslCommunication enterprise licenses supplied via environment variables or certificate files.

## 🛠️ Technology Stack

- **Framework**: .NET 10 LTS (ASP.NET Core)
- **Communication**: gRPC (HTTP/2)
- **Driver Library**: [HslCommunication](https://github.com/dathlin/HslCommunication) (NuGet)
- **Architecture**: Clean Architecture with Dependency Injection

## 📦 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Docker (optional, for containerized deployment)

### Installation

1. Clone the repository:

   ```bash
   git clone https://github.com/yourusername/HSL-gateway.git
   cd HSL-gateway
   ```

1. Restore dependencies:

   ```bash
   dotnet restore
   ```

### Running Locally

1. **Start the Simulator** (Optional, for testing):

  ```bash
  dotnet run --project HslSimulator/HslSimulator.csproj
  ```

  *Listens on port 50502.*

1. **Start the Gateway**:

  ```bash
  dotnet run --project HslGateway/HslGateway.csproj
  ```

  *Listens on port 50051.*

## ⚙️ Configuration

Configure devices and tags in `appsettings.json` (or manage them dynamically through the `ConfigManager` gRPC service, which persists to `data/gateway-config.json`).

```json
"Gateway": {
  "Devices": [
    {
      "Id": "siemens_01",
      "Type": "SiemensS7",
      "Ip": "192.168.1.10",
      "Port": 102,
      "Rack": 0,
      "Slot": 1,
      "PollIntervalMs": 1000
    },
    {
      "Id": "modbus_01",
      "Type": "ModbusTcp",
      "Ip": "127.0.0.1",
      "Port": 50502,
      "PollIntervalMs": 2000
    }
  ],
  "Tags": [
    {
      "DeviceId": "siemens_01",
      "Name": "motor_speed",
      "Address": "DB1.0",
      "DataType": "double"
    },
    {
      "DeviceId": "modbus_01",
      "Name": "line_power",
      "Address": "40001",
      "DataType": "short"
    }
  ]
}
```

### Multi-Device Support

The Gateway supports connecting to multiple devices simultaneously. Simply add more entries to the `Devices` array in `appsettings.json`.

- Each device runs in its own independent polling loop.
- A slow or disconnected device will **not** affect the performance of other devices.
- You can mix different protocols (e.g., one Siemens PLC and one Modbus device) in the same configuration.

### Enterprise License Bootstrap

Set `EnterpriseLicense` in `HslGateway/appsettings.json` (or the environment-specific file) to enable automatic activation of HslCommunication enterprise features at startup:

```json
"EnterpriseLicense": {
  "AutoLoadOnStartup": true,
  "CertificateFilePath": "data/hsl-enterprise.cert",
  "CertificateEnvironmentVariable": "HSL_ENTERPRISE_CERT_BASE64",
  "AuthorizationCodeEnvironmentVariable": "HSL_ENTERPRISE_AUTH_CODE",
  "ContactInfo": "ops-team@example.com"
}
```

On boot the gateway applies the first available artifact in this order:

1. `HSL_ENTERPRISE_CERT_BASE64` (Base64 string that represents the official HslCommunication certificate file).
2. `HSL_ENTERPRISE_AUTH_CODE` (plain-text authorization code provided by HslCommunication).
3. The file path defined by `CertificateFilePath` (default `data/hsl-enterprise.cert`).

If both `AutoLoadOnStartup` is `true` and one of the above values exists, the hosted `EnterpriseLicenseInitializer` logs the successful activation and moves on with the normal startup flow. Clear or disable `AutoLoadOnStartup` if you need to run in community mode.

## 🔌 API Usage (gRPC)

You can use any gRPC client (C#, Python, Go, Node.js, etc.) or tools like [grpcurl](https://github.com/fullstorydev/grpcurl).

**Get Tag Value:**

```bash
grpcurl -plaintext -d '{"deviceId": "modbus_01", "tagName": "line_power"}' localhost:50051 hslgateway.Gateway/GetTagValue
```

**List Devices:**

```bash
grpcurl -plaintext localhost:50051 hslgateway.Gateway/ListDevices
```

**Write Tag Value:**

```bash
grpcurl -plaintext -d '{"deviceId": "modbus_01", "tagName": "line_power", "value": 50}' localhost:50051 hslgateway.Gateway/WriteTagValue
```

**Subscribe to Tag Value (Streaming):**

```bash
grpcurl -plaintext -d '{"deviceId": "modbus_01", "tagName": "line_power"}' localhost:50051 hslgateway.Gateway/SubscribeTagValue
```

## 🧪 Test Scripts

Interactive bash scripts live under `scripts/tests` and spin up every component you need for manual verification:

- `scripts/tests/multi-device.sh`: launches three Modbus simulators, the gateway (using the `MultiDevice` environment), and the multi-device test client (foreground in your terminal) so you can validate polling/subscription across devices.
- `scripts/tests/multi-device.sh`: launches three Modbus simulators, the gateway (using the `MultiDevice` environment), and the multi-device test client (foreground in your terminal) so you can validate polling/subscription across devices. Pass `--auto` (or set `HSL_TEST_AUTO=1`) if you need it to run a fully automated demo scenario.
- `scripts/tests/subscription.sh`: launches a single simulator, the gateway, and the subscriber client with a guided flow that mirrors the subscription demo and write tests.

Run them from the repo root using Git Bash, WSL, Linux, or macOS `bash`. Each script builds the required projects, starts every process in the same terminal session, and cleans up automatically when you press `Ctrl+C`.

## 🐳 Docker Deployment

Build and run the container:

```bash
docker build -t hsl-gateway .
docker run -p 50051:50051 hsl-gateway
```

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.



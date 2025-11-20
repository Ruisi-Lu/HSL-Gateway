# HSL Gateway

> **Vibe Code** edition.

HSL Gateway is a high-performance, production-ready gRPC service designed to bridge industrial devices (Siemens S7, Modbus TCP) with modern IT systems. Built on **.NET 8**, it leverages the **HslCommunication** library to poll devices efficiently and exposes real-time data via a **gRPC** interface.

## 🚀 Features

- **Multi-Protocol Support**: Native support for Siemens S7 (S1200, S1500, etc.) and Modbus TCP.
- **gRPC Interface**: Fast, strongly-typed API for reading tag values and device lists.
- **High Performance**: In-memory caching (`TagValueCache`) ensures low-latency data access.
- **Background Polling**: Dedicated background service (`PollingWorker`) handles device communication independently of API requests.
- **Resilient**: Automatic reconnection and error handling for device communication.
- **Docker Ready**: Includes a multi-stage Dockerfile for easy deployment on Linux/Kubernetes.
- **Simulator Included**: Comes with a Modbus TCP simulator for testing and verification.
- **Scalable**: Supports connecting to multiple devices simultaneously with parallel polling.

## 🛠️ Technology Stack

- **Framework**: .NET 8 (ASP.NET Core)
- **Communication**: gRPC (HTTP/2)
- **Driver Library**: [HslCommunication](https://github.com/dathlin/HslCommunication) (NuGet)
- **Architecture**: Clean Architecture with Dependency Injection

## 📦 Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Docker (optional, for containerized deployment)

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/HSL-gateway.git
   cd HSL-gateway
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

### Running Locally

1. **Start the Simulator** (Optional, for testing):
   ```bash
   dotnet run --project HslSimulator/HslSimulator.csproj
   ```
   *Listens on port 50502.*

2. **Start the Gateway**:
   ```bash
   dotnet run --project HslGateway/HslGateway.csproj
   ```
   *Listens on port 50051.*

## ⚙️ Configuration

Configure devices and tags in `appsettings.json`.

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

## 🐳 Docker Deployment

Build and run the container:

```bash
docker build -t hsl-gateway .
docker run -p 50051:50051 hsl-gateway
```

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.


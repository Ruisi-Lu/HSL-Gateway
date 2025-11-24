using Grpc.Core;
using Grpc.Net.Client;
using HslGateway.Grpc;
using System.Linq;

namespace HslVerifier;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting HSL Verifier...");
        
        // Allow untrusted certificates for localhost
        var httpHandler = new HttpClientHandler();
        httpHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        
        using var channel = GrpcChannel.ForAddress("http://localhost:50051", new GrpcChannelOptions { HttpHandler = httpHandler });
        var client = new Gateway.GatewayClient(channel);
        var configClient = new ConfigManager.ConfigManagerClient(channel);

        try
        {
            // 1. List Devices
            Console.WriteLine("\n[1] Listing Devices...");
            var devices = await client.ListDevicesAsync(new Empty());
            foreach (var d in devices.Devices)
            {
                Console.WriteLine($" - Found Device: {d.Id}");
            }

            // 2. Get Tag Value (Read)
            Console.WriteLine("\n[2] Reading Tag 'line_power' from 'modbus_01'...");
            var tagVal = await client.GetTagValueAsync(new TagRequest { DeviceId = "modbus_01", TagName = "line_power" });
            Console.WriteLine($" - Value: {tagVal.Value}, Quality: {tagVal.Quality}, Time: {tagVal.TimestampUtc}");

            // 3. Write Tag Value (Write)
            Console.WriteLine("\n[3] Writing Value 9999 to 'line_power'...");
            var writeResp = await client.WriteTagValueAsync(new WriteTagRequest { DeviceId = "modbus_01", TagName = "line_power", Value = 9999 });
            Console.WriteLine($" - Write Success: {writeResp.Success}, Message: {writeResp.Message}");

            // 4. Verify Write
            Console.WriteLine("\n[4] Verifying Write...");
            tagVal = await client.GetTagValueAsync(new TagRequest { DeviceId = "modbus_01", TagName = "line_power" });
            Console.WriteLine($" - New Value: {tagVal.Value} (Expected 9999)");

            // 5. Subscribe (Streaming)
            Console.WriteLine("\n[5] Subscribing to 'line_power' for 5 seconds...");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var call = client.SubscribeTagValue(new TagRequest { DeviceId = "modbus_01", TagName = "line_power" }, cancellationToken: cts.Token);

            try
            {
                await foreach (var resp in call.ResponseStream.ReadAllAsync(cts.Token))
                {
                    Console.WriteLine($" [Stream] Value: {resp.Value}, Time: {resp.TimestampUtc}");
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                Console.WriteLine(" - Subscription ended.");
            }

            await RunConfigHotReloadTest(client, configClient);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static async Task RunConfigHotReloadTest(Gateway.GatewayClient gatewayClient, ConfigManager.ConfigManagerClient configClient)
    {
        const string deviceId = "modbus_dynamic";
        const string tag1 = "line_power_dynamic";
        const string tag2 = "temperature_dynamic";

        Console.WriteLine("\n[6] Testing gRPC hot-load for devices/tags...");

        var deviceResult = await configClient.UpsertDeviceAsync(new DeviceConfigDto
        {
            Id = deviceId,
            Type = "ModbusTcp",
            Ip = "127.0.0.1",
            Port = 50502,
            PollIntervalMs = 1500
        });

        if (!deviceResult.Success)
        {
            Console.WriteLine($" - Failed to upsert device: {deviceResult.Message}");
            return;
        }

        Console.WriteLine(" - Device created via ConfigManager gRPC endpoint");

        await UpsertTag(configClient, deviceId, tag1, "40001", "short");
        await UpsertTag(configClient, deviceId, tag2, "40002", "short");

        Console.WriteLine(" - Waiting for PollingWorker to start new device...");
        await Task.Delay(TimeSpan.FromSeconds(4));

        var devices = await gatewayClient.ListDevicesAsync(new Empty());
        var found = devices.Devices.Any(d => string.Equals(d.Id, deviceId, StringComparison.OrdinalIgnoreCase));
        Console.WriteLine(found
            ? " - New device is visible in ListDevices()"
            : " - WARNING: new device missing from ListDevices()!");

        var tagValue = await gatewayClient.GetTagValueAsync(new TagRequest { DeviceId = deviceId, TagName = tag1 });
        Console.WriteLine($" - Read dynamic tag '{tag1}': value={tagValue.Value}, quality={tagValue.Quality}");

        Console.WriteLine(" - Cleaning up dynamic device...");
        var deleteResult = await configClient.DeleteDeviceAsync(new DeviceRequest { DeviceId = deviceId });
        if (!deleteResult.Success)
        {
            Console.WriteLine($" - WARNING: failed to remove device: {deleteResult.Message}");
        }
        else
        {
            Console.WriteLine(" - Device removed successfully");
        }
    }

    private static async Task UpsertTag(ConfigManager.ConfigManagerClient configClient, string deviceId, string name, string address, string dataType)
    {
        var tagResult = await configClient.UpsertTagAsync(new TagConfigDto
        {
            DeviceId = deviceId,
            Name = name,
            Address = address,
            DataType = dataType
        });

        if (!tagResult.Success)
        {
            throw new InvalidOperationException($"Failed to upsert tag {deviceId}/{name}: {tagResult.Message}");
        }

        Console.WriteLine($"   • Tag {name} registered");
    }
}

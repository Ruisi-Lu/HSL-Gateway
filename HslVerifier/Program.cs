using Grpc.Core;
using Grpc.Net.Client;
using HslGateway.Grpc;

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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}

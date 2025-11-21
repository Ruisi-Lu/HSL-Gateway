using Grpc.Core;
using Grpc.Net.Client;
using HslGateway.Grpc;

namespace HslMultiDeviceTest;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var options = ParseOptions(args);

        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine("   HSL Gateway 多設備訂閱測試");
        Console.WriteLine("═══════════════════════════════════════════════\n");
        Console.WriteLine($"伺服器地址: {options.ServerAddress}\n");

        using var channel = GrpcChannel.ForAddress(options.ServerAddress);
        var client = new Gateway.GatewayClient(channel);

        if (options.AutoDemo)
        {
            await RunAutoDemo(client, options.AutoDemoStatusDuration);
            return;
        }

        while (true)
        {
            Console.WriteLine("───────────────────────────────────────────────");
            Console.WriteLine("請選擇測試:");
            Console.WriteLine("  1. 列出所有設備");
            Console.WriteLine("  2. 顯示所有設備的標籤");
            Console.WriteLine("  3. 讀取所有標籤的當前值");
            Console.WriteLine("  4. 訂閱單一設備的所有標籤");
            Console.WriteLine("  5. 訂閱所有設備的特定標籤");
            Console.WriteLine("  6. 同時訂閱多個設備的多個標籤");
            Console.WriteLine("  7. 測試多設備並發寫入");
            Console.WriteLine("  8. 監控所有設備狀態");
            Console.WriteLine("  0. 退出");
            Console.WriteLine("───────────────────────────────────────────────");
            Console.Write("輸入選項: ");

            var choice = Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1":
                        await ListAllDevices(client);
                        break;
                    case "2":
                        await ShowAllDeviceTags(client);
                        break;
                    case "3":
                        await ReadAllTagValues(client);
                        break;
                    case "4":
                        await SubscribeSingleDevice(client);
                        break;
                    case "5":
                        await SubscribeSpecificTagAcrossDevices(client);
                        break;
                    case "6":
                        await SubscribeMultipleDevicesAndTags(client);
                        break;
                    case "7":
                        await TestConcurrentWrites(client);
                        break;
                    case "8":
                        await MonitorAllDeviceStatus(client);
                        break;
                    case "0":
                        Console.WriteLine("\n👋 結束程式...");
                        return;
                    default:
                        Console.WriteLine("❌ 無效的選項");
                        break;
                }
            }
            catch (RpcException ex)
            {
                Console.WriteLine($"\n❌ gRPC 錯誤: {ex.Status.StatusCode} - {ex.Status.Detail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ 錯誤: {ex.Message}");
            }
        }
    }

    static (string ServerAddress, bool AutoDemo, TimeSpan AutoDemoStatusDuration) ParseOptions(string[] args)
    {
        var serverAddress = "http://localhost:50051";
        var autoDemo = false;
        var statusSeconds = 10;

        foreach (var arg in args)
        {
            if (arg.StartsWith("--server=", StringComparison.OrdinalIgnoreCase))
            {
                serverAddress = arg.Substring("--server=".Length);
            }
            else if (arg.Equals("--auto-demo", StringComparison.OrdinalIgnoreCase))
            {
                autoDemo = true;
            }
            else if (arg.StartsWith("--auto-status-seconds=", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(arg.Substring("--auto-status-seconds=".Length), out var seconds) && seconds > 0)
                {
                    statusSeconds = seconds;
                }
            }
            else if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                serverAddress = arg;
            }
        }

        return (serverAddress, autoDemo, TimeSpan.FromSeconds(statusSeconds));
    }

    static async Task RunAutoDemo(Gateway.GatewayClient client, TimeSpan monitorDuration)
    {
        Console.WriteLine("🤖 自動化測試情境開始...\n");
        await ListAllDevices(client);
        await ShowAllDeviceTags(client);
        await ReadAllTagValues(client);
        await MonitorDeviceStatusForDuration(client, monitorDuration);
        Console.WriteLine("\n✅ 自動化測試情境完成\n");
    }

    static async Task ListAllDevices(Gateway.GatewayClient client)
    {
        Console.WriteLine("\n📋 設備列表:");
        var response = await client.ListDevicesAsync(new Empty());
        
        Console.WriteLine($"\n找到 {response.Devices.Count} 個設備:\n");
        foreach (var device in response.Devices)
        {
            Console.WriteLine($"  🔌 {device.Id}");
        }
    }

    static async Task ShowAllDeviceTags(Gateway.GatewayClient client)
    {
        Console.WriteLine("\n📋 所有設備的標籤配置:\n");
        
        var devicesResponse = await client.ListDevicesAsync(new Empty());
        
        foreach (var device in devicesResponse.Devices)
        {
            Console.WriteLine($"🔌 設備: {device.Id}");
            var tagsResponse = await client.ListDeviceTagsAsync(new DeviceRequest { DeviceId = device.Id });
            
            if (tagsResponse.Tags.Count == 0)
            {
                Console.WriteLine("  (無標籤)\n");
            }
            else
            {
                foreach (var tag in tagsResponse.Tags)
                {
                    Console.WriteLine($"  • {tag.TagName,-15} 地址: {tag.Address,-8} 類型: {tag.DataType}");
                }
                Console.WriteLine();
            }
        }
    }

    static async Task ReadAllTagValues(Gateway.GatewayClient client)
    {
        Console.WriteLine("\n📖 讀取所有標籤值:\n");
        
        var devicesResponse = await client.ListDevicesAsync(new Empty());
        
        foreach (var device in devicesResponse.Devices)
        {
            var tagsResponse = await client.ListDeviceTagsAsync(new DeviceRequest { DeviceId = device.Id });
            
            Console.WriteLine($"🔌 {device.Id}:");
            foreach (var tag in tagsResponse.Tags)
            {
                var valueResponse = await client.GetTagValueAsync(new TagRequest 
                { 
                    DeviceId = device.Id, 
                    TagName = tag.TagName 
                });
                
                var quality = valueResponse.Quality == "good" ? "✅" : "⚠️";
                Console.WriteLine($"  {tag.TagName,-15} = {valueResponse.Value,8:F2} {quality}");
            }
            Console.WriteLine();
        }
    }

    static async Task SubscribeSingleDevice(Gateway.GatewayClient client)
    {
        Console.Write("\n輸入要訂閱的設備 ID (例如: modbus_01): ");
        var deviceId = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(deviceId)) return;

        Console.WriteLine($"\n🔔 訂閱設備 {deviceId} 的所有標籤");
        Console.WriteLine("   (按 Enter 停止訂閱)\n");

        var tagsResponse = await client.ListDeviceTagsAsync(new DeviceRequest { DeviceId = deviceId });
        var tasks = new List<Task>();
        var cts = new CancellationTokenSource();

        foreach (var tag in tagsResponse.Tags)
        {
            var tagName = tag.TagName;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var call = client.SubscribeTagValue(
                        new TagRequest { DeviceId = deviceId, TagName = tagName },
                        cancellationToken: cts.Token);

                    await foreach (var response in call.ResponseStream.ReadAllAsync(cts.Token))
                    {
                        var timestamp = DateTime.Parse(response.TimestampUtc).ToLocalTime();
                        Console.WriteLine($"[{timestamp:HH:mm:ss}] {tagName,-15} = {response.Value,8:F2}");
                    }
                }
                catch (OperationCanceledException) { }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { }
            }));
        }

        Console.ReadLine();
        cts.Cancel();
        await Task.WhenAll(tasks);
        Console.WriteLine("\n✅ 訂閱已停止");
    }

    static async Task SubscribeSpecificTagAcrossDevices(Gateway.GatewayClient client)
    {
        Console.Write("\n輸入要跨設備訂閱的標籤名稱模式 (例如: power, speed, 或留空訂閱所有): ");
        var pattern = Console.ReadLine();

        Console.WriteLine($"\n🔔 訂閱所有設備的標籤 (模式: '{pattern}')");
        Console.WriteLine("   (按 Enter 停止訂閱)\n");

        var devicesResponse = await client.ListDevicesAsync(new Empty());
        var tasks = new List<Task>();
        var cts = new CancellationTokenSource();

        foreach (var device in devicesResponse.Devices)
        {
            var tagsResponse = await client.ListDeviceTagsAsync(new DeviceRequest { DeviceId = device.Id });
            
            foreach (var tag in tagsResponse.Tags)
            {
                if (string.IsNullOrWhiteSpace(pattern) || tag.TagName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    var deviceId = device.Id;
                    var tagName = tag.TagName;
                    
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            using var call = client.SubscribeTagValue(
                                new TagRequest { DeviceId = deviceId, TagName = tagName },
                                cancellationToken: cts.Token);

                            await foreach (var response in call.ResponseStream.ReadAllAsync(cts.Token))
                            {
                                var timestamp = DateTime.Parse(response.TimestampUtc).ToLocalTime();
                                Console.WriteLine($"[{timestamp:HH:mm:ss}] {deviceId}/{tagName,-15} = {response.Value,8:F2}");
                            }
                        }
                        catch (OperationCanceledException) { }
                        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { }
                    }));
                }
            }
        }

        Console.WriteLine($"已啟動 {tasks.Count} 個訂閱\n");
        Console.ReadLine();
        cts.Cancel();
        await Task.WhenAll(tasks);
        Console.WriteLine("\n✅ 所有訂閱已停止");
    }

    static async Task SubscribeMultipleDevicesAndTags(Gateway.GatewayClient client)
    {
        Console.WriteLine("\n🔔 訂閱所有設備的所有標籤");
        Console.WriteLine("   (按 Enter 停止訂閱)\n");

        var devicesResponse = await client.ListDevicesAsync(new Empty());
        var tasks = new List<Task>();
        var cts = new CancellationTokenSource();
        var counts = new Dictionary<string, int>();

        foreach (var device in devicesResponse.Devices)
        {
            var tagsResponse = await client.ListDeviceTagsAsync(new DeviceRequest { DeviceId = device.Id });
            counts[device.Id] = 0;
            
            foreach (var tag in tagsResponse.Tags)
            {
                var deviceId = device.Id;
                var tagName = tag.TagName;
                
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using var call = client.SubscribeTagValue(
                            new TagRequest { DeviceId = deviceId, TagName = tagName },
                            cancellationToken: cts.Token);

                        await foreach (var response in call.ResponseStream.ReadAllAsync(cts.Token))
                        {
                            lock (counts)
                            {
                                counts[deviceId]++;
                            }
                            var timestamp = DateTime.Parse(response.TimestampUtc).ToLocalTime();
                            Console.WriteLine($"[{timestamp:HH:mm:ss}] {deviceId}/{tagName,-15} = {response.Value,8:F2}");
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { }
                }));
            }
        }

        Console.WriteLine($"已啟動 {tasks.Count} 個訂閱\n");
        Console.ReadLine();
        cts.Cancel();
        await Task.WhenAll(tasks);
        
        Console.WriteLine("\n✅ 所有訂閱已停止");
        Console.WriteLine("\n📊 統計:");
        foreach (var kvp in counts)
        {
            Console.WriteLine($"  {kvp.Key}: 收到 {kvp.Value} 筆更新");
        }
    }

    static async Task TestConcurrentWrites(Gateway.GatewayClient client)
    {
        Console.WriteLine("\n✏️  測試多設備並發寫入\n");

        var devicesResponse = await client.ListDevicesAsync(new Empty());
        var tasks = new List<Task>();
        var random = new Random();

        foreach (var device in devicesResponse.Devices)
        {
            var tagsResponse = await client.ListDeviceTagsAsync(new DeviceRequest { DeviceId = device.Id });
            
            foreach (var tag in tagsResponse.Tags)
            {
                var deviceId = device.Id;
                var tagName = tag.TagName;
                var value = random.Next(1000, 9999);
                
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var response = await client.WriteTagValueAsync(new WriteTagRequest
                        {
                            DeviceId = deviceId,
                            TagName = tagName,
                            Value = value
                        });
                        
                        var status = response.Success ? "✅" : "❌";
                        Console.WriteLine($"{status} {deviceId}/{tagName} = {value} ({response.Message})");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ {deviceId}/{tagName}: {ex.Message}");
                    }
                }));
            }
        }

        await Task.WhenAll(tasks);
        Console.WriteLine($"\n✅ 完成 {tasks.Count} 個寫入操作");
        
        // 等待一下再讀取驗證
        await Task.Delay(1000);
        Console.WriteLine("\n🔍 驗證寫入結果:\n");
        await ReadAllTagValues(client);
    }

    static async Task MonitorAllDeviceStatus(Gateway.GatewayClient client)
    {
        Console.WriteLine("\n🔔 監控所有設備狀態");
        Console.WriteLine("   (按 Enter 停止監控)\n");

        var devicesResponse = await client.ListDevicesAsync(new Empty());
        var tasks = new List<Task>();
        var cts = new CancellationTokenSource();

        foreach (var device in devicesResponse.Devices)
        {
            var deviceId = device.Id;
            
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var call = client.SubscribeDeviceStatus(
                        new DeviceRequest { DeviceId = deviceId },
                        cancellationToken: cts.Token);

                    await foreach (var response in call.ResponseStream.ReadAllAsync(cts.Token))
                    {
                        var timestamp = DateTime.Parse(response.TimestampUtc).ToLocalTime();
                        var status = response.IsOnline ? "🟢 在線" : "🔴 離線";
                        Console.WriteLine($"[{timestamp:HH:mm:ss}] {deviceId}: {status}");
                    }
                }
                catch (OperationCanceledException) { }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { }
            }));
        }

        Console.WriteLine($"已啟動 {tasks.Count} 個設備狀態監控\n");
        Console.ReadLine();
        cts.Cancel();
        await Task.WhenAll(tasks);
        Console.WriteLine("\n✅ 監控已停止");
    }

    static async Task MonitorDeviceStatusForDuration(Gateway.GatewayClient client, TimeSpan duration)
    {
        Console.WriteLine($"\n⏱️ 監控所有設備狀態（約 {duration.TotalSeconds:F0} 秒）\n");

        var devicesResponse = await client.ListDevicesAsync(new Empty());
        var cts = new CancellationTokenSource(duration);
        var tasks = new List<Task>();

        foreach (var device in devicesResponse.Devices)
        {
            var deviceId = device.Id;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var call = client.SubscribeDeviceStatus(new DeviceRequest { DeviceId = deviceId }, cancellationToken: cts.Token);
                    await foreach (var response in call.ResponseStream.ReadAllAsync(cts.Token))
                    {
                        var timestamp = DateTime.Parse(response.TimestampUtc).ToLocalTime();
                        var status = response.IsOnline ? "🟢 在線" : "🔴 離線";
                        Console.WriteLine($"[{timestamp:HH:mm:ss}] {deviceId}: {status}");
                    }
                }
                catch (OperationCanceledException) { }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { }
            }));
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) { }

        Console.WriteLine("\n⏹️ 自動狀態監控結束");
    }
}

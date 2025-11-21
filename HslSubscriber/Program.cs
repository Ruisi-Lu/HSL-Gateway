using Grpc.Core;
using Grpc.Net.Client;
using HslGateway.Grpc;

namespace HslSubscriber;

class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine("   HSL Gateway 訂閱測試客戶端");
        Console.WriteLine("═══════════════════════════════════════════════\n");

        // 解析命令行參數
        var serverAddress = args.Length > 0 ? args[0] : "http://localhost:50051";
        var deviceId = args.Length > 1 ? args[1] : "modbus_01";
        var tagName = args.Length > 2 ? args[2] : "line_power";

        Console.WriteLine($"伺服器地址: {serverAddress}");
        Console.WriteLine($"設備 ID: {deviceId}");
        Console.WriteLine($"標籤名稱: {tagName}\n");

        // 創建 gRPC 通道
        using var channel = GrpcChannel.ForAddress(serverAddress);
        var client = new Gateway.GatewayClient(channel);

        // 顯示選單
        while (true)
        {
            Console.WriteLine("\n───────────────────────────────────────────────");
            Console.WriteLine("請選擇操作:");
            Console.WriteLine("  1. 訂閱標籤值變化 (SubscribeTagValue)");
            Console.WriteLine("  2. 訂閱設備狀態變化 (SubscribeDeviceStatus)");
            Console.WriteLine("  3. 讀取單個標籤值 (GetTagValue)");
            Console.WriteLine("  4. 寫入標籤值 (WriteTagValue)");
            Console.WriteLine("  5. 列出所有設備 (ListDevices)");
            Console.WriteLine("  6. 列出設備標籤 (ListDeviceTags)");
            Console.WriteLine("  7. 變更訂閱設定");
            Console.WriteLine("  0. 退出");
            Console.WriteLine("───────────────────────────────────────────────");
            Console.Write("輸入選項: ");

            var choice = Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1":
                        await SubscribeTagValue(client, deviceId, tagName);
                        break;
                    case "2":
                        await SubscribeDeviceStatus(client, deviceId);
                        break;
                    case "3":
                        await GetTagValue(client, deviceId, tagName);
                        break;
                    case "4":
                        await WriteTagValue(client, deviceId, tagName);
                        break;
                    case "5":
                        await ListDevices(client);
                        break;
                    case "6":
                        await ListDeviceTags(client, deviceId);
                        break;
                    case "7":
                        Console.Write("輸入設備 ID: ");
                        var newDeviceId = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(newDeviceId))
                            deviceId = newDeviceId;
                        
                        Console.Write("輸入標籤名稱: ");
                        var newTagName = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(newTagName))
                            tagName = newTagName;
                        
                        Console.WriteLine($"\n✅ 已更新: 設備={deviceId}, 標籤={tagName}");
                        break;
                    case "0":
                        Console.WriteLine("\n👋 結束程式...");
                        return;
                    default:
                        Console.WriteLine("❌ 無效的選項，請重新輸入。");
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

    static async Task SubscribeTagValue(Gateway.GatewayClient client, string deviceId, string tagName)
    {
        Console.WriteLine($"\n🔔 開始訂閱標籤值: {deviceId}/{tagName}");
        Console.WriteLine("   (按 Enter 停止訂閱)\n");

        var request = new TagRequest
        {
            DeviceId = deviceId,
            TagName = tagName
        };

        using var cts = new CancellationTokenSource();

        // 在背景執行訂閱
        var subscriptionTask = Task.Run(async () =>
        {
            int count = 0;
            try
            {
                using var call = client.SubscribeTagValue(request, cancellationToken: cts.Token);

                await foreach (var response in call.ResponseStream.ReadAllAsync(cts.Token))
                {
                    count++;
                    var timestamp = DateTime.Parse(response.TimestampUtc).ToLocalTime();
                    var quality = response.Quality == "good" ? "✅" : "⚠️";
                    
                    Console.WriteLine($"[{count:D4}] {timestamp:HH:mm:ss.fff} | {response.DeviceId}/{response.TagName} = {response.Value,8:F2} {quality}");
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"\n✅ 訂閱已停止 (共收到 {count} 筆資料)");
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                Console.WriteLine($"\n✅ 訂閱已停止 (共收到 {count} 筆資料)");
            }
        });

        // 等待使用者按 Enter
        Console.ReadLine();
        cts.Cancel();
        
        // 等待訂閱任務完成
        await subscriptionTask;
    }

    static async Task SubscribeDeviceStatus(Gateway.GatewayClient client, string deviceId)
    {
        Console.WriteLine($"\n🔔 開始訂閱設備狀態: {deviceId}");
        Console.WriteLine("   (按 Enter 停止訂閱)\n");

        var request = new DeviceRequest
        {
            DeviceId = deviceId
        };

        using var cts = new CancellationTokenSource();

        // 在背景執行訂閱
        var subscriptionTask = Task.Run(async () =>
        {
            int count = 0;
            try
            {
                using var call = client.SubscribeDeviceStatus(request, cancellationToken: cts.Token);

                await foreach (var response in call.ResponseStream.ReadAllAsync(cts.Token))
                {
                    count++;
                    var timestamp = DateTime.Parse(response.TimestampUtc).ToLocalTime();
                    var status = response.IsOnline ? "🟢 在線" : "🔴 離線";
                    
                    Console.WriteLine($"[{count:D4}] {timestamp:HH:mm:ss.fff} | {response.DeviceId}: {status}");
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"\n✅ 訂閱已停止 (共收到 {count} 筆狀態更新)");
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                Console.WriteLine($"\n✅ 訂閱已停止 (共收到 {count} 筆狀態更新)");
            }
        });

        // 等待使用者按 Enter
        Console.ReadLine();
        cts.Cancel();
        
        // 等待訂閱任務完成
        await subscriptionTask;
    }

    static async Task GetTagValue(Gateway.GatewayClient client, string deviceId, string tagName)
    {
        Console.WriteLine($"\n📖 讀取標籤值: {deviceId}/{tagName}");

        var request = new TagRequest
        {
            DeviceId = deviceId,
            TagName = tagName
        };

        var response = await client.GetTagValueAsync(request);
        var timestamp = string.IsNullOrEmpty(response.TimestampUtc) 
            ? "N/A" 
            : DateTime.Parse(response.TimestampUtc).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");

        Console.WriteLine($"\n┌─────────────────────────────────────");
        Console.WriteLine($"│ 設備 ID:   {response.DeviceId}");
        Console.WriteLine($"│ 標籤名稱:  {response.TagName}");
        Console.WriteLine($"│ 數值:      {response.Value:F2}");
        Console.WriteLine($"│ 時間戳記:  {timestamp}");
        Console.WriteLine($"│ 品質:      {response.Quality}");
        Console.WriteLine($"└─────────────────────────────────────");
    }

    static async Task WriteTagValue(Gateway.GatewayClient client, string deviceId, string tagName)
    {
        Console.Write($"\n✏️  輸入要寫入 {deviceId}/{tagName} 的數值: ");
        var input = Console.ReadLine();
        
        if (!double.TryParse(input, out var value))
        {
            Console.WriteLine("❌ 無效的數值格式");
            return;
        }

        var request = new WriteTagRequest
        {
            DeviceId = deviceId,
            TagName = tagName,
            Value = value
        };

        Console.WriteLine($"⏳ 正在寫入...");
        var response = await client.WriteTagValueAsync(request);

        if (response.Success)
        {
            Console.WriteLine($"✅ 寫入成功: {value}");
            
            // 等待一下再讀回來驗證
            await Task.Delay(500);
            Console.WriteLine("\n🔍 驗證寫入結果...");
            await GetTagValue(client, deviceId, tagName);
        }
        else
        {
            Console.WriteLine($"❌ 寫入失敗: {response.Message}");
        }
    }

    static async Task ListDevices(Gateway.GatewayClient client)
    {
        Console.WriteLine("\n📋 設備列表:");

        var response = await client.ListDevicesAsync(new Empty());

        if (response.Devices.Count == 0)
        {
            Console.WriteLine("  (無設備)");
        }
        else
        {
            Console.WriteLine($"\n找到 {response.Devices.Count} 個設備:\n");
            foreach (var device in response.Devices)
            {
                Console.WriteLine($"  • {device.Id}");
            }
        }
    }

    static async Task ListDeviceTags(Gateway.GatewayClient client, string deviceId)
    {
        Console.WriteLine($"\n📋 設備 {deviceId} 的標籤列表:");

        var request = new DeviceRequest { DeviceId = deviceId };
        var response = await client.ListDeviceTagsAsync(request);

        if (response.Tags.Count == 0)
        {
            Console.WriteLine("  (無標籤)");
        }
        else
        {
            Console.WriteLine($"\n找到 {response.Tags.Count} 個標籤:\n");
            Console.WriteLine("┌─────────────────────┬──────────────┬──────────┐");
            Console.WriteLine("│ 標籤名稱            │ 地址         │ 類型     │");
            Console.WriteLine("├─────────────────────┼──────────────┼──────────┤");
            
            foreach (var tag in response.Tags)
            {
                Console.WriteLine($"│ {tag.TagName,-19} │ {tag.Address,-12} │ {tag.DataType,-8} │");
            }
            
            Console.WriteLine("└─────────────────────┴──────────────┴──────────┘");
        }
    }
}

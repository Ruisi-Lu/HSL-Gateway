using HslCommunication.ModBus;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HslSimulator;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("═══════════════════════════════════════════════");
        Console.WriteLine("   HSL 多設備 Modbus TCP 模擬器");
        Console.WriteLine("═══════════════════════════════════════════════\n");

        try
        {
            // 創建多個 Modbus TCP 伺服器
            var server1 = new ModbusTcpServer();
            var server2 = new ModbusTcpServer();
            var server3 = new ModbusTcpServer();
            
            // 啟動伺服器在不同 Port
            server1.ServerStart(50502);
            Console.WriteLine("✅ 設備 1 (modbus_01) 已啟動 - Port 50502");
            
            server2.ServerStart(50503);
            Console.WriteLine("✅ 設備 2 (modbus_02) 已啟動 - Port 50503");
            
            server3.ServerStart(50504);
            Console.WriteLine("✅ 設備 3 (modbus_03) 已啟動 - Port 50504");
            
            Console.WriteLine("\n模擬器配置:");
            Console.WriteLine("  modbus_01 (Port 50502):");
            Console.WriteLine("    - line_power   (40001) : 線路功率");
            Console.WriteLine("    - temperature  (40002) : 溫度");
            Console.WriteLine("  modbus_02 (Port 50503):");
            Console.WriteLine("    - motor_speed  (40001) : 馬達轉速");
            Console.WriteLine("    - pressure     (40002) : 壓力");
            Console.WriteLine("  modbus_03 (Port 50504):");
            Console.WriteLine("    - flow_rate    (40001) : 流量");
            Console.WriteLine("    - level        (40002) : 液位");
            
            Console.WriteLine("\n開始模擬數據變化...\n");
            
            // 模擬設備 1 的數據變化
            Task.Run(async () =>
            {
                var random = new Random(1);
                while (true)
                {
                    short power = (short)random.Next(100, 200);
                    short temp = (short)random.Next(20, 80);
                    
                    server1.Write("40001", power);
                    server1.Write("40002", temp);
                    
                    Console.WriteLine($"[設備1] line_power={power,3}, temperature={temp,2}");
                    
                    await Task.Delay(5000);
                }
            });

            // 模擬設備 2 的數據變化
            Task.Run(async () =>
            {
                var random = new Random(2);
                await Task.Delay(1000); // 錯開更新時間
                
                while (true)
                {
                    short speed = (short)random.Next(1000, 3000);
                    short pressure = (short)random.Next(10, 100);
                    
                    server2.Write("40001", speed);
                    server2.Write("40002", pressure);
                    
                    Console.WriteLine($"[設備2] motor_speed={speed,4}, pressure={pressure,2}");
                    
                    await Task.Delay(5000);
                }
            });

            // 模擬設備 3 的數據變化
            Task.Run(async () =>
            {
                var random = new Random(3);
                await Task.Delay(2000); // 錯開更新時間
                
                while (true)
                {
                    short flowRate = (short)random.Next(50, 500);
                    short level = (short)random.Next(0, 100);
                    
                    server3.Write("40001", flowRate);
                    server3.Write("40002", level);
                    
                    Console.WriteLine($"[設備3] flow_rate={flowRate,3}, level={level,2}");
                    
                    await Task.Delay(5000);
                }
            });

            Console.WriteLine("按任意鍵停止模擬器...\n");
            if (Console.IsInputRedirected)
            {
                Console.WriteLine("(stdin 已重定向，模擬器將持續運行，按 Ctrl+C 或終止程序即可)");
                Task.Delay(Timeout.Infinite).Wait();
            }
            else
            {
                Console.ReadLine();
            }
            
            Console.WriteLine("\n正在關閉伺服器...");
            server1.ServerClose();
            server2.ServerClose();
            server3.ServerClose();
            Console.WriteLine("✅ 所有伺服器已關閉");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 錯誤: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}

using HslCommunication.ModBus;
using System;
using System.Threading;

namespace HslSimulator;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Starting HSL Modbus TCP Simulator...");

        try
        {
            // Create a Modbus TCP Server
            ModbusTcpServer server = new ModbusTcpServer();
            
            // Initialize some data
            // 40001 corresponds to address "0" in HslCommunication server usually, or we can map it.
            // HslCommunication ModbusServer uses standard mapping.
            // Let's write some initial values.
            
            server.ServerStart(50502); // Use a non-privileged port
            
            Console.WriteLine("Modbus TCP Server started on port 50502");
            
            // Simulate data changes
            Task.Run(async () =>
            {
                var random = new Random();
                while (true)
                {
                    short power = (short)random.Next(100, 200);
                    server.Write("40001", power);
                    Console.WriteLine($"[Sim] Updated 40001 (line_power) to {power}");
                    
                    // Simulate a float value for Siemens (mapped to Modbus for simplicity in this test, 
                    // or we can start a Siemens server too if HSL supports it. 
                    // HslCommunication supports SiemensServer, let's try that too if needed.
                    // For now, let's just test Modbus.)
                    
                    await Task.Delay(2000);
                }
            });

            Console.WriteLine("Press any key to stop...");
            Console.ReadLine();
            
            server.ServerClose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}

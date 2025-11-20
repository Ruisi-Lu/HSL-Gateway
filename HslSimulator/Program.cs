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
                    // Only update if we haven't received a write recently? 
                    // Or just update less frequently so we have time to verify.
                    // Let's update every 5 seconds.
                    
                    short power = (short)random.Next(100, 200);
                    // We use a lock or just let it race, it's a sim.
                    // But to verify WRITE, we should probably use a different register 
                    // or check the console output of the simulator to see if it received a write.
                    // HslCommunication Server doesn't automatically log writes to console unless we hook it.
                    
                    // Let's hook into the server's write event if possible, or just trust the client side.
                    // For now, let's just update 40001 less often.
                    
                    server.Write("40001", power);
                    Console.WriteLine($"[Sim] Auto-updated 40001 (line_power) to {power}");
                    
                    await Task.Delay(5000);
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

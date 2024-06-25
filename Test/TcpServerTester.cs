using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    using System;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;

    public class TcpServerTester
    {
        private readonly string _serverIp = "127.0.0.1";
        private readonly int _serverPort = 9090;

        public TcpServerTester()
        {
            
        }

        public async Task SendCommandAsync(string command)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    Console.WriteLine("Connecting to server...");
                    await client.ConnectAsync(_serverIp, _serverPort);

                    using (var stream = client.GetStream())
                    {
                        // Convert the command to a byte array and send it to the server
                        byte[] commandBytes = Encoding.UTF8.GetBytes(command);
                        Console.WriteLine("Sending command to server...");
                        await stream.WriteAsync(commandBytes, 0, commandBytes.Length);
                        Console.WriteLine("Command sent.");

                        // Wait for the server's response and read it
                        byte[] responseBuffer = new byte[4096];
                        int bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
                        string response = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);

                        Console.WriteLine($"Server response: {response}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}

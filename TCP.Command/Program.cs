namespace TCP.Command
{
    internal class Program
    {
        static void Main(string[] args)
        {
            TCPServer tCPServer = new TCPServer(9090);
            Console.WriteLine("Server is Running on port 8080." +
                "Press Enter to exit.");
            Console.ReadLine();
            tCPServer.Stop();
        }
    }
}

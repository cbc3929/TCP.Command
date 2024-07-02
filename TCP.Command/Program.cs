using NLog.Config;
using NLog.Targets;
using NLog;
using TCP.Command.PCIE;
using TCP.Command.Command;

namespace TCP.Command
{
    internal class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        static void Main(string[] args)
        {
            ConfigureNLog();
            var list = PCIeCardFactory.GetDeviceList();
            if (list.Count == 0)
            {
                Logger.Error("未能查找到设备，请检查硬件连接或驱动！");
                return;
            }
            TCPServer tCPServer = new TCPServer(9090);
            Logger.Info("Server is Running on port 9090." +
                "Press Enter to exit.");
            Console.ReadLine();
            tCPServer.Stop();
            //foreach (var card in list)
            //{
            //    for (var i = 0; i < card.ChannelCount; i++) 
            //    {
            //        card.ChannelStates[i].loopRunCts.Cancel();
            //        card.ChannelStates[i].singleRunCts.Cancel();
            //    }
            //}
            CommandManager.Instance.CancelAllCommands();
        }
        private static void ConfigureNLog()
        {
            var config = new LoggingConfiguration();

            // 控制台目标
            var consoleTarget = new ConsoleTarget("console")
            {
                Layout = "${longdate} | ${level:uppercase=true} | ${logger} | ${message} ${exception:format=tostring}"
            };
            config.AddTarget(consoleTarget);

            // 文件目标
            var fileTarget = new FileTarget("file")
            {
                FileName = "${basedir}/logs/${shortdate}.log",
                Layout = "${longdate} | ${level:uppercase=true} | ${logger} | ${message} ${exception:format=tostring}"
            };
            config.AddTarget(fileTarget);

            // 定义日志规则
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, consoleTarget);
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, fileTarget);

            LogManager.Configuration = config;
        }
    }

}

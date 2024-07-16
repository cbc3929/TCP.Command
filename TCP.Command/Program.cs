using NLog.Config;
using NLog.Targets;
using NLog;
using TCP.Command.PCIE;
using TCP.Command.Command;
using Lookdata;

namespace TCP.Command
{
    internal class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        static void Main(string[] args)
        {
            ConfigureNLog();
            var istONG = true;
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
            istONG = false;
            //删除所有生成的文件
            if (PCIeCardFactory.NewFilePathList.Count != 0) 
            {
                foreach (string indexer in PCIeCardFactory.NewFilePathList) 
                {
                    if (File.Exists(indexer))
                    {
                        File.Delete(indexer);
                        Logger.Info($"{indexer} Has Be Killed");
                    }
                    else 
                    {
                        Logger.Info("已经干掉了");
                    }
                }
            
            }
            for (uint unBoardIndex = 0; unBoardIndex < list.Count; unBoardIndex++) 
            {
                for (int i = 0; i < list[(int)unBoardIndex].ChannelCount; i++) 
                {
                    list[(int)unBoardIndex].ChannelStates[i].IsRunning = false;
                    Logger.Info("Closing " + list[(int)unBoardIndex].DeviceName + "'s No." + i + " Channel");
                }
                int RepKeepRun = -99;
                int DmaChIndex = 0;

                do
                {
                    System.Threading.Thread.Sleep(100);
                    dotNetQTDrv.QTGetRegs_i32(unBoardIndex, Regs.RepKeepRun, ref RepKeepRun, DmaChIndex);//2023年3月9日23:32:23：增加DmaChIndex变量，获得当前DMA通道的变量值
                } while (RepKeepRun != 0);

                dotNetQTDrv.LDSetParam(unBoardIndex, Comm.CMD_MB_ENABLE_REPLAY_MODE, 1, 0, 0, 0xFFFFFFFF);// 选择DAC寄存器
                //dotNetQTDrv.LDReplayStop(_card.unBoardIndex, _channelNum);//固定DMA CH1回放
                dotNetQTDrv.QTWriteRegister(unBoardIndex, 0x800E0000, (uint)1 * 4, 0x13);//‘1’：复位
                //----Stop acquisition and close card handle
                try
                {
                    dotNetQTDrv.QTStart(unBoardIndex, Comm.QTFM_COMMON_TRANSMIT_DIRECTION_BRD2PC, 0, 2000);
                }
                catch (Exception err)
                {
                    Logger.Error(err);
                }
                dotNetQTDrv.QTResetBoard(unBoardIndex);//关闭回放端口输出
                dotNetQTDrv.QTWriteRegister(unBoardIndex, 0x800F0000, (uint)0 * 4, 0x0);  //disable data gen
                dotNetQTDrv.rtp1clsWriteALGSingleRegister(unBoardIndex, 1, 0);
                dotNetQTDrv.QTCloseBoard(unBoardIndex);

            }
            //CommandManager.Instance.CancelAllCommands();
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

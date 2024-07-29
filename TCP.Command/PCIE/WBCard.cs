using Lookdata;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TCP.Command.PCIE
{
    internal class WBCard : PcieCard
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private new WBconfig _config;
        private bool isPrint;
        private int _printTicTime;
        public WBCard( uint cardIndex,int numberofcards) : base(cardIndex,1, numberofcards)
        {
            FS = 2400000000;
            _configpath = "Config/WBconfig.json";
            _config =LoadConfig(_configpath);
            SetupFileWatcher(_configpath);
        }

        private async Task StartPrint() 
        {
            while (isPrint) 
            {
                PrintTimeClock();
                await Task.Delay(_printTicTime);
            }
        }

        private WBconfig LoadConfig(string filePath) 
        {
            try
            {
                string jsonString = File.ReadAllText(filePath);
                var config = JsonSerializer.Deserialize<WBconfig>(jsonString);
                Logger.Info($"读取{DeviceName}配置成功"); 
                Update_Num14(config);
                isPrint = config.openPrintAbsTimeClock;
                _printTicTime = config.printTic;
                if (isPrint)
                {
                    Task.Run(() =>StartPrint());
                }
                return config;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error reading config file: {ex.Message}");
                return null;
            }

        }

        public override int GetMappedValue(long inputValue)
        {
            foreach (var range in _config.ranges)
            {
                if (inputValue >= range.min && inputValue <= range.max)
                {
                    return range.value;
                }
            }
            // 如果未找到合适的区间，返回默认值
            Logger.Info("未能匹配到射频功率对应的功率系数，使用默认系数" + _config.defaultValue);
            return _config.defaultValue;
        }

        public void Update_Num14(WBconfig config)
        {
            uint one = config.isIntervalTime ? 1u : 0u;
            //窄带读取 之后 下发一次 14号寄存器 0-3位控制通道时码 是否是内部输入 默认 内部
            dotNetQTDrv.QTWriteRegister(unBoardIndex, DacBaseAddr, 14 * 4, one);

        }

        protected override void ReLoadJson()
        {
            lock (_lock)
            {
                Logger.Info("更新射频配置成功");
            }
        }

        public override int Initialize(uint uncardIndex)
        {
            uint[] ba = new uint[4];
            ba[0] = 0x800A0000;
            ba[1] = 0x800B0000;
            ba[2] = 0x80030000;
            ba[3] = 0x80080000;
            uint[] os = new uint[4];
            os[0] = 0x38;
            os[1] = 0x04;
            os[2] = 0x7c;
            os[3] = 0x7c;
            uint[] dac_jesd_sync = new uint[2];
            uint[] reg = new uint[32];
            do
            {
                //20240705 禁止上位机操作GPIO

                //dotNetQTDrv.LDSetParam(0,835u, 0, 0, 0, 1000);//设置为输入，高阻
                //dotNetQTDrv.LDSetParam(0, 836u, 0, 0, 0, 1000);//output=0
                //Thread.Sleep(1000);
                //dotNetQTDrv.LDSetParam(0, 836u, 1, 0, 0, 1000);//output=0
                //Thread.Sleep(1000);
                dotNetQTDrv.LDSetParam(uncardIndex, Comm.CMD_MB_RESET_ADC_INTERFACE, 0, 0, 0, 10000);
                //sync状态
                for (int j = 0; j < 2; j++)
                    dotNetQTDrv.QTReadRegister(unBoardIndex, ref ba[j], ref os[0], ref dac_jesd_sync[j]);
                //jesd regs
                for (int j = 0; j < 2; j++)
                    dotNetQTDrv.QTReadRegister(unBoardIndex, ref ba[j], ref os[1], ref reg[j]);
                //clock freq
                dotNetQTDrv.QTWriteRegister(unBoardIndex, 0x80030000, 0x0, 0x10000000);
                dotNetQTDrv.QTReadRegister(unBoardIndex, ref ba[2], ref os[2], ref reg[2]);
                //PLL lock
                dotNetQTDrv.QTReadRegister(unBoardIndex, ref ba[3], ref os[3], ref reg[3]);
                //fmc_sdr_dump_PLL(i);
            } while ((dac_jesd_sync[0] != 0x10001) || (dac_jesd_sync[1] != 0x10001));
            return 0;
        }

    }
}

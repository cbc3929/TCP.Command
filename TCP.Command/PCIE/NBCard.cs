using Lookdata;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TCP.Command.PCIE
{
    public class NBCard : PcieCard
    {

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private new NBConfig _config;
        private bool isPrint;
        private int _printTicTime;
        //TODO 一些属性可以直接定义到抽象类里去 后面再说吧
        public NBCard(uint cardIndex,int numberofcards) : base(cardIndex,4, numberofcards)
        {
            FS = 600000000;
            SampleRate = 1200000000;
            _configpath = "Config/NBconfig.json";
            _config = LoadConfig(_configpath);
            SetupFileWatcher(_configpath);
        }

        private async Task StartPrint()
        {
            while (isPrint) 
            {
                PrintTimeClock();
                Task.Delay(_printTicTime).Wait();
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
        public override int Initialize(uint unCardIdx)
        {

            double RangeVolt = Comm.QTFM_INPUT_RANGE_1;           // 输入档位选择，取值QTFM_INPUT_RANGE_1~4 对应输入档位由小到大
            double OffsetVolt = 0;                                           // 偏置设置，取值范围[-full-calce,+full-scale],单位uV
                                                                             //////////////////////////////////////////////////////////////////////////
            ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(unCardIdx, Regs.uiErrCode, 0), "错误清零");
            ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(unCardIdx, Regs.EnableStreaming, 1), "设置流盘标志位");
            ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(unCardIdx, Regs.EnableVfifo, 0), "禁止虚拟FIFO标志位");
            ld_ChkRT(dotNetQTDrv.QTResetBoard(unCardIdx), "复位板卡");
            var temp_number = this.ProductNumber;
            ld_ChkRT(dotNetQTDrv.QTGetRegs_i32(unCardIdx, Regs.product_number, ref temp_number), "读取产品编码");
            ProductNumber = temp_number;
            
            
            uint refdiv = 10;
            RefClkMode = Comm.QTFM_COMMON_CLOCK_REF_MODE_1;
            Fref = 10000000;
            refdiv = 1;
            ADCClkMode = Comm.QTFM_COMMON_ADC_CLOCK_MODE_1;
            string ext_clk = Convert.ToString(SampleRate);
            Logger.Info("当前输入的采样时钟频率为" + ext_clk + "Hz");
            {
                ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(unCardIdx, Regs.SRate, (int)SampleRate), string.Format("设置采样率{0}", SampleRate));
                if (dotNetQTDrv.QTClockSet(unCardIdx, (uint)Fref, refdiv, 0, Comm.QTFM_COMMON_CLOCK_VCO_MODE_0, RefClkMode, ADCClkMode, 1) != Error.RES_SUCCESS)
                {
                    Logger.Error("不支持的采样率，当前 " + Convert.ToString(SampleRate / 1000000) + " MHz");
                    return -1;
                }
                else
                {
                    if (EnDACWork > 0)
                    {
                        if (ChkFreq(unCardIdx, SampleRate, 1) == -1)//检查DAC时钟频率
                            return -1;
                    }
                }
            }
            var temp_bforceiodelay = 0;
            ld_ChkRT(dotNetQTDrv.QTGetRegs_i32(unCardIdx, Regs.bForceIOdelay, ref temp_bforceiodelay), "读取IOdelay标志");
            bForceIOdelay = temp_bforceiodelay;
            //----Setup AFE
            if (ProductNumber != 0x1125)
            {
                uint ModeFlag = 0;
                if (bForceIOdelay == 1)
                    ModeFlag = 0;
                else
                    ModeFlag = 256;
                ModeFlag = 1 << 8;
                ld_ChkRT(dotNetQTDrv.QTAdcModeSet(unCardIdx, 0, ModeFlag, 0), "设置ADC");
            }
            //----Setup Input range and offset
            int couple_type = 0;
            ld_ChkRT(dotNetQTDrv.QTGetRegs_i32(unCardIdx, Regs.couple_type, ref couple_type), "读取耦合方式");
            if (couple_type == 0xDC)
            {
                //----Set analog input range first then offset
                ld_ChkRT(dotNetQTDrv.QTChannelRangeSet(unCardIdx, -1, RangeVolt), string.Format("设置量程 {0}", Convert.ToInt32(RangeVolt)));
                //----Set analog offset
                ld_ChkRT(dotNetQTDrv.QTChannelOffsetSet(unCardIdx, -1, OffsetVolt), "设置偏置");
            }
            return 0;
        }


        public void Update_Num14(NBConfig config) 
        {
            uint reg = 0;
            var one = config.isChannelOneIntervalTime?1u:0u;
            var two = config.isChannelTwoIntervalTime ? 1u : 0u;
            var three = config.isChannelThreeIntervalTime ? 1u : 0u;
            var four = config.isChannelFourIntervalTime ? 1u : 0u;
            reg = four | three | two | one;
            //窄带读取 之后 下发一次 14号寄存器 0-3位控制通道时码 是否是内部输入 默认 内部
            dotNetQTDrv.QTWriteRegister(unBoardIndex, DacBaseAddr, 14 * 4, reg);

        }

        protected  NBConfig LoadConfig(string filePath)
        {
            try
            {
                string jsonString = File.ReadAllText(filePath);
                var config = JsonSerializer.Deserialize<NBConfig>(jsonString);
                Logger.Info($"读取{DeviceName}配置成功");
                Update_Num14(config);
                isPrint = config.openPrintAbsTimeClock;
                _printTicTime = config.printTic;
                if (isPrint)
                {
                    StartPrint();
                }
                return config;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error reading config file: {ex.Message}");
                return null;
            }

        }

        protected override void ReLoadJson()
        {
            lock (_lock)
            {
                _config = LoadConfig(_configpath);
                Logger.Info("更新射频配置成功");
            }
        }

    }
}

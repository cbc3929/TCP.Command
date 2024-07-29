using Lookdata;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static TCP.Command.SetStatusCommand;

namespace TCP.Command.PCIE
{
    public static class PCIeCardFactory
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static List<PcieCard> pcieCards = new List<PcieCard>();
        public static Dictionary<int,PcieCard> CardParam = new Dictionary<int,PcieCard>();
        public static List<string> NewFilePathList = new List<string>();
        private static FileSystemWatcher _fileWatcher;
        public static PcieCard CreatePcieCard(uint cardIndex, int productNumber)
        {
            switch (productNumber)
            {
                case 0x416160b:
                    Logger.Info("窄带" + productNumber.ToString());
                    return new NBCard(cardIndex,2);
                case 0x14200:
                    Logger.Info("宽带" + productNumber.ToString());
                    return new WBCard(cardIndex,2);
                default:
                    Logger.Error("尚未适配该型号:0x" + Convert.ToString(productNumber, 16) + "错误");
                    throw new ArgumentException("未知的产品编号");
            }
        }

        private static Config LoadConfig(string filePath)
        {
            try
            {
                string jsonString = File.ReadAllText(filePath);
                var config = JsonSerializer.Deserialize<Config>(jsonString);
                return config;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error reading config file: {ex.Message}");
                return null;
            }
        }

        public static int ConfigRFModule(UInt32 CardIndex, UInt32 rf_chan_type, UInt32 rf_chan_num, UInt32 Cmd_type, decimal freq, decimal RF_Atten, decimal IF_Atten, UInt32 RF_onff)
        {
            UInt32 ba = 0x800e0000;
            UInt32[] reg = new UInt32[3];
            //先写射频频率，衰减，on/off
            if (Cmd_type == (UInt32)CMD_TYPE.FREQ)
            {
                reg[1] = (UInt32)freq;


                dotNetQTDrv.QTWriteRegister(CardIndex, ba, 4 * 16, reg[1]);


            }
            else
            {
                reg[2] = (UInt32)RF_Atten + ((UInt32)IF_Atten << 6) + ((UInt32)RF_onff << 12);
                dotNetQTDrv.QTWriteRegister(CardIndex, ba, 4 * 17, reg[2]);
            }
            
            reg[0] = (rf_chan_type & 0x1F) | ((rf_chan_num & 0x1F) << 5) | ((Cmd_type & 0x1F) << 10) | (1 << 16);
            dotNetQTDrv.QTWriteRegister(CardIndex, ba, 4 * 15, reg[0]);
            reg[0] = (rf_chan_type & 0x1F) | ((rf_chan_num & 0x1F) << 5) | ((Cmd_type & 0x1F) << 10) | (0 << 16);
            dotNetQTDrv.QTWriteRegister(CardIndex, ba, 4 * 15, reg[0]);
            return 0;
        }

        public static List<PcieCard> GetDeviceList()
        {

            for (uint i = 0; i < 2; i++)
            {
                dotNetQTDrv.QTSetRegs_i32(i, Regs.EnableReplay, 0);
                dotNetQTDrv.QTSetRegs_i32(i, Regs.EnableStreaming, 1);
                dotNetQTDrv.QTSetRegs_i32(i, Regs.EnableVfifo, 0);
                
                //try to open device
                uint nRet = (uint)dotNetQTDrv.QTOpenBoard(i);
                if (nRet == Error.RES_SUCCESS)//find a device
                {
                    var temp_number = 0;
                    dotNetQTDrv.QTGetRegs_i32(i, Regs.product_number, ref temp_number);
                    PcieCard card = PCIeCardFactory.CreatePcieCard(i, temp_number);
                    if (temp_number != 0x416160B)
                    {
                        card.DeviceName = "宽带";
                    }
                    else {
                        card.DeviceName = "窄带";
                    }
                    uint result = (uint)card.Initialize(i);
                    if (Error.RES_SUCCESS != result)
                    {
                        if (result == 2)
                        {
                            Logger.Info("时钟频率错误！");

                        }
                        else if (result == Error.RES_OPEN_FAILURE)
                        {
                            Logger.Info("打开板卡失败！");
                        }
                        else if (result == Error.RES_ERROR_ALLOC_BUF)
                        {
                            Logger.Info("内存不足！");
                        }
                        else if (result == Error.RES_ERROR_DDR_INIT_FAILED)
                        {
                            Logger.Info("板载内存初始化失败！");
                        }
                        else if (result == Error.RES_ERROR_PRODUCT_INFO_UNDEF)
                        {
                            Logger.Info("读取板卡信息失败！");
                        }
                        else
                        {
                            Logger.Info(string.Format("初始化失败！0x{0:x}", result));
                        }
                    }
                    else
                    {
                        pcieCards.Add(card);
                        if (temp_number == 0x416160B)
                        {
                            CardParam.Add(2, card);
                            CardParam.Add(3, card);
                            CardParam.Add(4, card);
                            CardParam.Add(5, card);
                            for (var s = 1; s < 5; s++)
                            {
                                ConfigRFModule(card.unBoardIndex, 0, (uint)s, (uint)CMD_TYPE.FREQ, (uint)card.ChannelStates[s - 1].FreqValue / 1000,
                                    card.ChannelStates[s - 1].RF_Atten, card.ChannelStates[s - 1].IF_Atten, 0);
                                ConfigRFModule(card.unBoardIndex, 0, (uint)s, (uint)CMD_TYPE.RF_ATT, (uint)card.ChannelStates[s - 1].FreqValue / 1000,
                                    card.ChannelStates[s - 1].RF_Atten, card.ChannelStates[s - 1].IF_Atten, 0);
                                ConfigRFModule(card.unBoardIndex, 0, (uint)s, (uint)CMD_TYPE.IF_ATT, (uint)card.ChannelStates[s - 1].FreqValue / 1000,
                                    card.ChannelStates[s - 1].RF_Atten, card.ChannelStates[s - 1].IF_Atten, 0);
                            }
                            Logger.Info("NBCARD RF Has been init");
                        }
                        else {
                            CardParam.Add(1, card);
                            ConfigRFModule(card.unBoardIndex, 0, 0, (uint)CMD_TYPE.FREQ, (uint)card.ChannelStates[0].FreqValue / 1000,
                                    card.ChannelStates[0].RF_Atten, card.ChannelStates[0].IF_Atten, 0);
                            ConfigRFModule(card.unBoardIndex, 0, 0, (uint)CMD_TYPE.RF_ATT, (uint)card.ChannelStates[0].FreqValue / 1000,
                                card.ChannelStates[0].RF_Atten, card.ChannelStates[0].IF_Atten, 0);
                            ConfigRFModule(card.unBoardIndex, 0, 0, (uint)CMD_TYPE.IF_ATT, (uint)card.ChannelStates[0].FreqValue / 1000,
                                card.ChannelStates[0].RF_Atten, card.ChannelStates[0].IF_Atten, 0);
                            Logger.Info("WBCARD RF Has been init");
                        }
                    }
                    #region 有点搓,后面再改吧

                    var temp_bforce = card.bForceIOdelay;
                    var temp_couple = card.CoupleType;
                    var temp_NOB = card.NOB;
                    var temp_devmaxsampleRate = card.DevMaxSampleRate;
                    var temp_adda = card.Adda_Revision;

                    dotNetQTDrv.QTGetRegs_i32(i, Regs.bForceIOdelay, ref temp_bforce);
                    dotNetQTDrv.QTGetRegs_i32(i, Regs.couple_type, ref temp_couple);
                    dotNetQTDrv.QTGetRegs_i32(i, Regs.lResolution, ref temp_NOB);
                    dotNetQTDrv.QTGetRegs_i32(i, Regs.SRate, ref temp_devmaxsampleRate);
                    dotNetQTDrv.QTGetRegs_i32(i, Regs.adda_revision, ref temp_adda);
                    card.ProductNumber = temp_number;
                    card.bForceIOdelay = temp_bforce;
                    card.CoupleType = temp_couple;
                    card.NOB = temp_NOB;
                    card.DevMaxSampleRate = temp_devmaxsampleRate;
                    card.Adda_Revision = temp_adda;

                    #endregion
                }
                else if (nRet == Error.RES_ERROR_PRODUCT_INFO_UNDEF)
                {
                    Logger.Error("发现设备，但无法读取设备信息");
                }
                else
                {
                    Logger.Error(string.Format("CardIndex={0:D} 发现未知错误0x{1}", i, Convert.ToString(nRet, 16)));
                }
            }

            if (pcieCards.Count == 0)
            {
                Logger.Error("未发现设备，请确认设备和驱动程序已正确安装", "错误");
                Logger.Error("未发现设备，请确认设备和驱动程序已正确安装");

            }

            return pcieCards;

        }
        //绝对顺序
        public static int ConvertChannelNumber(int channelNum)
        {
            return channelNum == 1 ? 0 : channelNum - 2;
        }
    }

    public class Range
    {
        public long min { get; set; }
        public long max { get; set; }
        public int value { get; set; }
    }

    public class Config
    {
        public int defaultValue { get; set; }
        public bool openPrintAbsTimeClock { get; set; }

        public int printTic { get; set; }
        public List<Range> ranges { get; set; }
    }

    public class NBConfig : Config 
    {
        public bool isChannelOneIntervalTime { get; set; }
        public bool isChannelTwoIntervalTime { get; set; }
        public bool isChannelThreeIntervalTime { get; set; }
        public bool isChannelFourIntervalTime { get; set; }


    }

    public class WBconfig : Config
    {
        public bool isIntervalTime { get; set; }
    }
}

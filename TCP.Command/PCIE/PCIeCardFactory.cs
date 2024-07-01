using Lookdata;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCP.Command.PCIE
{
    public static class PCIeCardFactory
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static List<PcieCard> pcieCards = new List<PcieCard>();
        public static Dictionary<int,PcieCard> CardParam = new Dictionary<int,PcieCard>();
        public static PcieCard CreatePcieCard(uint cardIndex, int productNumber)
        {
            switch (productNumber)
            {
                case 68556299:
                    Logger.Info("窄带" + productNumber.ToString());
                    return new NBCard(cardIndex,2);
                case 68556298:
                    Logger.Info("宽带" + productNumber.ToString());
                    return new WBCard(cardIndex,2);
                default:
                    Logger.Error("尚未适配该型号:0x" + Convert.ToString(productNumber, 16) + "错误");
                    throw new ArgumentException("未知的产品编号");
            }
        }
        public static List<PcieCard> GetDeviceList()
        {
            //var deviceStatusManger = NBDeviceStatsManager.Instance;
            //deviceStatusManger.deviceList.Add("VitrulPcieCard0");
            //deviceStatusManger.deviceList.Add("VitrulPcieCard1");
            //IntializerAll();

            //return deviceStatusManger.deviceList;

            //dBm_offset[1, 0] = 0;
#if DEBUG
            int product_nb_number = 0x416160B;
            int product_wb_number = 68556298;
            PcieCard wb_card = CreatePcieCard(0, product_wb_number);
            PcieCard nb_card = CreatePcieCard(1, product_nb_number);
            wb_card.Initialize(0);
            nb_card.Initialize(1);
            pcieCards.Add(wb_card);
            pcieCards.Add(nb_card);
            CardParam.Add(1, wb_card);
            CardParam.Add(2, nb_card);
            CardParam.Add(3, nb_card);
            CardParam.Add(4, nb_card);
            CardParam.Add(5, nb_card);


#else
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
                        }
                        else CardParam.Add(1, card);
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

#endif
            return pcieCards;

        }
        //绝对顺序
        public static int ConvertChannelNumber(int channelNum)
        {
            return channelNum == 1 ? 0 : channelNum - 2;
        }

    }
}

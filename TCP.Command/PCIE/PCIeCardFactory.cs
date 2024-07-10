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
        public static List<PcieCard> pcieCardList = new List<PcieCard>();
        public static Dictionary<int,PcieCard> CardParam = new Dictionary<int,PcieCard>();
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
        public static List<PcieCard> GetDeviceList()
        {


            for (uint i = 1; i < 2; i++)
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
                        pcieCardList.Add(card);
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

            if (pcieCardList.Count == 0)
            {
                Logger.Error("未发现设备，请确认设备和驱动程序已正确安装", "错误");
                Logger.Error("未发现设备，请确认设备和驱动程序已正确安装");

            }

            return pcieCardList;

        }

        public static void StopAllPlayFile()
        {
            for (uint unBoardIndex = 0; unBoardIndex < pcieCardList.Count; unBoardIndex++)
            {
                for (int i = 0; i < pcieCardList[(int)unBoardIndex].ChannelCount; i++)
                {
                    pcieCardList[(int)unBoardIndex].ChannelStates[i].IsRunning = false;
                    Logger.Info("Closing " + pcieCardList[(int)unBoardIndex].DeviceName + "'s No." + i + " Channel");
                    int RepKeepRun = -99;
                    int DmaChIndex = i;
                    do
                    {
                        System.Threading.Thread.Sleep(100);
                        dotNetQTDrv.QTGetRegs_i32(unBoardIndex, Regs.RepKeepRun, ref RepKeepRun, DmaChIndex);//2023年3月9日23:32:23：增加DmaChIndex变量，获得当前DMA通道的变量值
                    } while (RepKeepRun != 0);
                }
                dotNetQTDrv.LDSetParam(unBoardIndex, Comm.CMD_MB_ENABLE_REPLAY_MODE, 1, 0, 0, 0xFFFFFFFF);// 选择DAC寄存器
                //dotNetQTDrv.LDReplayStop(_card.unBoardIndex, _channelNum);//固定DMA CH1回放
                dotNetQTDrv.QTWriteRegister(unBoardIndex, 0x800E0000, (uint)1 * 4, 0x13);//‘1’：复位
                dotNetQTDrv.QTWriteRegister(unBoardIndex, 0x800E0000, (uint)0 * 4, 0); // 控制四路窄带Add
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
                dotNetQTDrv.rtp1clsWriteALGSingleRegister(unBoardIndex, 1, 0);
                dotNetQTDrv.QTCloseBoard(unBoardIndex);

            }

        }
        //绝对顺序
        public static int ConvertChannelNumber(int channelNum)
        {
            return channelNum == 1 ? 0 : channelNum - 2;
        }

    }
}

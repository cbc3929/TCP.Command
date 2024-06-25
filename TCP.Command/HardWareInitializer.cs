using Lookdata;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCP.Command
{
    public static class HardWareInitializer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static int GetDeviceList()
        {

            var deviceStatusManger = DeviceStatsManager.Instance;
#if DEBUG
            return 0;
        #else
            //dBm_offset[1, 0] = 0;

            for (uint i = 0; i < 2; i++)
            {
                dotNetQTDrv.QTSetRegs_i32(i, Regs.EnableReplay, 0);
                dotNetQTDrv.QTSetRegs_i32(i, Regs.EnableStreaming, 1);
                dotNetQTDrv.QTSetRegs_i32(i, Regs.EnableVfifo, 0);
                //try to open device
                uint nRet = (uint)dotNetQTDrv.QTOpenBoard(i);
                if (nRet == Error.RES_SUCCESS)//find a device
                {
                    #region 有点搓,后面再改吧
                    var temp_number = deviceStatusManger.product_number;
                    var temp_bforce = deviceStatusManger.bForceIOdelay;
                    var temp_couple = deviceStatusManger.couple_type;
                    var temp_NOB = deviceStatusManger.NOB;
                    var temp_devmaxsampleRate = deviceStatusManger.DevMaxSampleRate;
                    var temp_adda = deviceStatusManger.adda_revision;
                    dotNetQTDrv.QTGetRegs_i32(i, (uint)Regs.product_number, ref temp_number);
                    dotNetQTDrv.QTGetRegs_i32(i, (uint)Regs.bForceIOdelay, ref temp_bforce);
                    dotNetQTDrv.QTGetRegs_i32(i, (uint)Regs.couple_type, ref temp_couple);
                    dotNetQTDrv.QTGetRegs_i32(i, (uint)Regs.lResolution, ref temp_NOB);
                    dotNetQTDrv.QTGetRegs_i32(i, (uint)Regs.SRate, ref temp_devmaxsampleRate);
                    dotNetQTDrv.QTGetRegs_i32(i, (uint)Regs.adda_revision, ref temp_adda);
                    deviceStatusManger.product_number = temp_number;
                    deviceStatusManger.bForceIOdelay = temp_bforce;
                    deviceStatusManger.couple_type = temp_couple;
                    deviceStatusManger.NOB = temp_NOB;
                    deviceStatusManger.DevMaxSampleRate = temp_devmaxsampleRate;
                    deviceStatusManger.adda_revision = temp_adda;

                    #endregion
                    var device = "";
                    switch (deviceStatusManger.product_number)
                    {
                        case 0x416160B:
                            device = "四通道同步回放";
                            //dBm_offset[1, 0] = 0;
                            //cmb_selWorkMode.SelectedIndex = 1;//固定回放
                            //cmb_SelDataFlow.SelectedIndex = 1;//replay
                            //cmb_SelBW.SelectedIndex = 0;//bandwidth
                            uint ret = (uint)InitCard(i);
                            if (Error.RES_SUCCESS != ret)
                            {
                                //grpBox_CH1.Enabled = false;
                                //grpBox_CH2.Enabled = false;
                                //grpBox_CH3.Enabled = false;
                                //grpBox_CH4.Enabled = false;

                                if (ret == 2)
                                {
                                    Logger.Info("时钟频率错误！");

                                }
                                else if (ret == Error.RES_OPEN_FAILURE)
                                {
                                    Logger.Info("打开板卡失败！");
                                }
                                else if (ret == Error.RES_ERROR_ALLOC_BUF)
                                {
                                    Logger.Info("内存不足！");
                                }
                                else if (ret == Error.RES_ERROR_DDR_INIT_FAILED)
                                {
                                    Logger.Info("板载内存初始化失败！");
                                }
                                else if (ret == Error.RES_ERROR_PRODUCT_INFO_UNDEF)
                                {
                                    Logger.Info("读取板卡信息失败！");
                                }
                                else
                                {
                                    Logger.Info(String.Format("初始化失败！0x{0:x}", ret));
                                }
                            }
                            else
                            {
                                Logger.Info("初始化成功！");
                                //count_f++;
                            }

                            break;
                        default:
                            device = "Not Defined";
                            Logger.Error("尚未适配该型号:0x" + Convert.ToString(deviceStatusManger.product_number, 16) + "错误");
                            return -1;
                            //break;
                    }
                    Logger.Info(String.Concat("发现设备:" + device));
                    deviceStatusManger.deviceList.Add(device);
                }
                else if (nRet == Error.RES_ERROR_PRODUCT_INFO_UNDEF)
                {
                    Logger.Error("发现设备，但无法读取设备信息");
                    //break;
                }
                else
                {
                    //PrintLog(string.Concat("发现未知错误0x"+Convert.ToString(nRet,16)));
                    Logger.Error(string.Format("CardIndex={0:D} 发现未知错误0x{1}", i, Convert.ToString(nRet, 16)));
                    //break;
                }
            }
            deviceStatusManger.NoBoard = deviceStatusManger.deviceList.Count;
            if (deviceStatusManger.deviceList.Count == 0)
            {
                Logger.Error("未发现设备，请确认设备和驱动程序已正确安装", "错误");
                Logger.Error("未发现设备，请确认设备和驱动程序已正确安装");
                return -1;
            }
            return 0;
            #endif
        }

        private static int InitCard(uint unCardIdx)
        {
            var deviceStatusManager = DeviceStatsManager.Instance;
            //////////////////////////////////////////////////////////////////////////
            //----Clock parameters
            double RangeVolt = (double)Comm.QTFM_INPUT_RANGE_1;           // 输入档位选择，取值QTFM_INPUT_RANGE_1~4 对应输入档位由小到大
            double OffsetVolt = 0;                                           // 偏置设置，取值范围[-full-calce,+full-scale],单位uV
                                                                             //////////////////////////////////////////////////////////////////////////
            ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(unCardIdx, Regs.uiErrCode, 0), "错误清零");
            ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(unCardIdx, Regs.EnableStreaming, 1), "设置流盘标志位");
            ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(unCardIdx, Regs.EnableVfifo, 0), "禁止虚拟FIFO标志位");
            ld_ChkRT(dotNetQTDrv.QTResetBoard(unCardIdx), "复位板卡");
            //----获取板卡型号
            var temp_number = deviceStatusManager.product_number;
            ld_ChkRT(dotNetQTDrv.QTGetRegs_i32(unCardIdx, (uint)Regs.product_number, ref temp_number), "读取产品编码");
            deviceStatusManager.product_number = temp_number;
            //////////////////////////////////////////////////////////////////////////
            //Take max sample rate as default. Users feel free to change it by uncomment this line then assign new value.
            //////////////////////////////////////////////////////////////////////////
            ///
            deviceStatusManager.SampleRate = 512000000;
            uint refdiv = 10;
            deviceStatusManager.RefClkMode = Comm.QTFM_COMMON_CLOCK_REF_MODE_1;
            deviceStatusManager.Fref = 10000000;
            refdiv = 1;
            deviceStatusManager.ADCClkMode = Comm.QTFM_COMMON_ADC_CLOCK_MODE_1;
            string ext_clk = Convert.ToString(deviceStatusManager.SampleRate);
            //string info = string.Concat("请确保输入的采样时钟频率为", ext_clk, "Hz");
            Logger.Info("TCPserver 已启动", "提示");
            //----Setup clock
            {
                ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(unCardIdx, Regs.SRate, (int)deviceStatusManager.SampleRate), string.Format("设置采样率{0}", deviceStatusManager.SampleRate));
                if (dotNetQTDrv.QTClockSet(unCardIdx, (uint)deviceStatusManager.Fref, refdiv, 0, Comm.QTFM_COMMON_CLOCK_VCO_MODE_0, deviceStatusManager.RefClkMode, deviceStatusManager.ADCClkMode, 1) != Error.RES_SUCCESS)
                {
                    Logger.Error("不支持的采样率，当前 " + Convert.ToString(deviceStatusManager.SampleRate / 1000000) + " MHz");
                    return -1;
                }
                else
                {
                    if (deviceStatusManager.EnDACWork > 0)
                    {
                        if (ChkFreq(unCardIdx, (uint)deviceStatusManager.SampleRate, 1) == -1)//检查DAC时钟频率
                            return -1;
                    }
                }
            }
            var temp_bforceiodelay = 0;
            ld_ChkRT(dotNetQTDrv.QTGetRegs_i32(unCardIdx, (uint)Regs.bForceIOdelay, ref temp_bforceiodelay), "读取IOdelay标志");
            deviceStatusManager.bForceIOdelay = temp_bforceiodelay;
            //----Setup AFE
            if (deviceStatusManager.product_number != 0x1125)
            {
                uint ModeFlag = 0;
                if (deviceStatusManager.bForceIOdelay == 1)
                    ModeFlag = 0;
                else
                    ModeFlag = 256;
                ModeFlag = 1 << 8;
                ld_ChkRT(dotNetQTDrv.QTAdcModeSet(unCardIdx, 0, ModeFlag, 0), "设置ADC");
            }
            //----Setup Input range and offset
            int couple_type = 0;
            ld_ChkRT(dotNetQTDrv.QTGetRegs_i32(unCardIdx, (uint)Regs.couple_type, ref couple_type), "读取耦合方式");
            if (couple_type == 0xDC)
            {
                //----Set analog input range first then offset
                ld_ChkRT(dotNetQTDrv.QTChannelRangeSet(unCardIdx, -1, RangeVolt), string.Format("设置量程 {0}", Convert.ToInt32(RangeVolt)));
                //----Set analog offset
                ld_ChkRT(dotNetQTDrv.QTChannelOffsetSet(unCardIdx, -1, OffsetVolt), "设置偏置");
            }
            return 0;
        }

        private static int ld_ChkRT(int value, string log = "")
        {
            int nRet = -1;
            nRet = value;
            string logstr = "";
            if (nRet != 0)
            {
                StackTrace st = new StackTrace(new StackFrame(true));
                //Console.WriteLine(" Stack trace for current level: {0}", st.ToString());
                StackFrame sf = st.GetFrame(0);
                //MessageBox.Show(string.Format("Caller:{0}, @Line:{1}, with error Code:{2:x}", sf.GetMethod().Name, sf.GetFileLineNumber(), nRet));
                logstr = string.Format("ERROR: {0:D} ", nRet) + log + "失败";
                //ScadaLog.RecordInfo(logstr);
                Logger.Error(logstr);
                return -1;
            }
            else
            {
                logstr = string.Format("INFO: {0:D} ", nRet) + log + "成功";
                //ScadaLog.RecordInfo(logstr);
                Logger.Info(logstr);
            }
            return 0;
        }

        private static int ChkFreq(uint unCardIdx, uint samplerate, int WorkMode)
        {
            uint BaseAddr = Comm.REGISTER_ADDA_BASEADDR;
            var Srate2Fadc = 1;
            if (WorkMode == 1)
            {
                BaseAddr = Comm.REGISTER_DAC_BASEADDR;
                Srate2Fadc = 4;//DAC两通道模式
            }
            uint Offset = 0x7c;
            uint Rdval = 0;
            Offset = 0;
            dotNetQTDrv.QTReadRegister(unCardIdx, ref BaseAddr, ref Offset, ref Rdval);
            dotNetQTDrv.QTWriteRegister(unCardIdx, BaseAddr, Offset, Rdval | 0x10000000);
            Offset = 0x7c;
            Rdval = 0;
            Thread.Sleep(100);
            dotNetQTDrv.QTReadRegister(unCardIdx, ref BaseAddr, ref Offset, ref Rdval);
            Int32 OutFreq = Convert.ToInt32(Rdval) >> 16;
            long err = System.Math.Abs(samplerate / 1000000 / Srate2Fadc - OutFreq);
            if (err > 2)
            {
                Logger.Error(string.Concat("时钟频率错误 ", Convert.ToString(DeviceStatsManager.Instance.SampleRate), " ", Convert.ToString(OutFreq)), "ERROR");
                return -1;
            }
            return 0;
        }
    }


}

using Lookdata;
using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace TCP.Command.PCIE
{
    public abstract class PcieCard
    {
        private static readonly object _lock = new object();
        private int reqSrate;
        private UInt32 MARGIN_HIGH_VALUE;
        public UInt32[] AdcUsrReg = new UInt32[32];
        public UInt32[] DacUsrReg = new UInt32[32];
        private uint MaxNumFiles;
        private int MaxFileSizeMB;
        private object PPDATABUFLEN;
        private int ReplayedLenB;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private byte[] DDCCBBuf;// = new byte[PackLenB * PackNum];
        private byte[] FFTCBBuf;// = new byte[PackLenB * PackNum];
        private uint[] FFTPackCnt;//单位时间内包个数计数值
        private uint[] DDCPackCnt;//单位时间内包个数计数值
        public int[] RepKeepRun;
        private int PackLenB = 128 * 1000;
        private int PackNum = 50;

        private Mutex mutex_cb;
        public uint SampleRate_WB;

        public int SampleRate_NB { get; private set; }
        public int FS { get; set; }
        public int NumberOfCards { get; private set; }
        public  UInt32 AdcBaseAddr { get; private set; }

        public int CoupleType { get;set; }
        public ChannelState[] ChannelStates { get; private set; }
        public int ChannelCount { get; protected set; }
        /// <summary>
        /// 设备名字
        /// </summary>
        public string DeviceName { get; set; }
        /// <summary>
        /// 设备索引uint 从0开始
        /// </summary>
        public uint unBoardIndex { get; set; }
        /// <summary>
        /// 设备GUID 实际上就是自己设计的
        /// </summary>
        public Guid Guid { get; set; }
        public int ProductNumber { get; set; }
        public int NOB { get; set; }
        public int DevMaxSampleRate { get; set; }
        public int Adda_Revision { get; set; }
        public int bForceIOdelay { get; set; }
        public uint SampleRate { get; set; }
        public uint RefClkMode { get; set; }
        public int Fref { get; set; }
        public uint ADCClkMode { get; set; }
        public int EnDACWork { get; internal set; }
        public bool EnALG { get; internal set; }
        public ulong ReqSrate { get; set; }
        public uint ExtTrigSrcPort { get; internal set; }
        public int NameRule { get; private set; }
        public int SplitFileSizeMB { get; private set; }
        public bool EnDUC { get; internal set; }
        public bool EnSim { get; internal set; }
        public uint DdcPulseUnit { get; internal set; }
        public uint DacBaseAddr { get; internal set; }
        public uint DucPulseUnit { get; internal set; }
        public int TrigSrc { get; set; }
        public int Trig_edge { get; set; }
        public int[] Len { get; private set; }
        public string[] FilePath { get; private set; }
        public uint[] NotifySizeB { get; private set; }
        public Mutex Muter_cb { get =>mutex_cb;set=>new Mutex(); }

        public dotNetQTDrv.DDCCallBackHandle CallBackAppData
        {
               get; set;
        }
        public dotNetQTDrv.FFTCallBackHandle CallBackFFTData
        {
               get; set;
        }
        public dotNetQTDrv.PUSEREVENT_CALLBACK CallBackUserEvent
        {
                  get; set;
        }
        public int EnChCnt { get; internal set; }
        public int ChEnMask { get; internal set; }
        public int DaqMode { get; internal set; }
        public uint Workmode { get; internal set; }
        public int Trig_cnt { get; internal set; }
        public bool isFixLength { get; internal set; }
        public int Timercount { get; internal set; }
        public int Oset { get; internal set; }

        public abstract int Initialize(uint unCardIdx);

        public abstract void OnOperationCompleted(int channelNo);

        public abstract void CancelOperations(int channelNo);
    


    
        public PcieCard(uint cardIndex,int channelcount,int numberofcards)
        {
            EnALG = true;
            ReqSrate = 250000;
            SampleRate_WB = 1200000000;
            SampleRate_NB = 600000000;
            FS = 0;
            NameRule = 1;
            RepKeepRun = new int[ChannelCount];
            SplitFileSizeMB = 1024;// = 1024;
            MaxNumFiles = 0xffffffff;
            MaxFileSizeMB = 13312 * 1024;
            PPDATABUFLEN = 4 << 20;
            ReplayedLenB = 0;
            SampleRate = (uint)ReqSrate;
            EnDACWork = 1;
            unBoardIndex = 0;
            MARGIN_HIGH_VALUE = 3550;
            unBoardIndex = cardIndex;
            ChannelCount = channelcount;
            NumberOfCards = numberofcards;
            TrigSrc = 0;
            Trig_edge = 0;
            ExtTrigSrcPort = 0;
            LoadConfig();
            InitializeChannelDependentArrays();
            CallBackUserEvent = CallBackFunc_UserEvent_DA;
            CallBackAppData = DataPackProcess;
            CallBackFFTData = FFTPackProcess;
            DDCCBBuf = new byte[PackLenB * PackNum];
            FFTCBBuf = new byte[PackLenB * PackNum];
            FFTPackCnt = new uint[numberofcards];
            DDCPackCnt = new uint[numberofcards];
        }
        protected void InitializeChannelDependentArrays()
        {
            ChannelStates = new ChannelState[ChannelCount];
            for (int i = 0; i < ChannelCount; i++)
            {
                ChannelStates[i] = new ChannelState(this);
            }
            FilePath = new string[ChannelCount];
            Len = new int[ChannelCount];
            NotifySizeB = new uint[ChannelCount];
            RepKeepRun = new int[ChannelCount];
        }

        //protected void InitializeChannelStates()
        //{
        //    ChannelStates = new ChannelState[ChannelNumber];
        //}
        private void CallBackFunc_UserEvent_DA(int UserEventAddr)
        {
            //软件定义中断使能
            UInt32 SWIEn = 0xFF;
            //ad的JESD接口sync丢失中断
            const UInt32 AdcNotSyncEn = 0xF << 8;
            //DDR虚拟FIFO快满中断
            const UInt32 DdrAfullEn = 1 << 12;
            //写FIFO溢出中断
            const UInt32 ChFifoFullEn = 1 << 13;
            //参考时钟切换中断
            const UInt32 RefClkSwEn = 1 << 14;

            UInt32 BA = 0x80030000;
            UInt32 OS = 0x3C;//中断控制寄存器
            UInt32 IntrCtrl = 0;
            UInt32 IntrState = 0;
            //读取并记录中断控制寄存器
            dotNetQTDrv.QTReadRegister(unBoardIndex, ref BA, ref OS, ref IntrCtrl);
            //禁止并清除所有中断
            dotNetQTDrv.QTWriteRegister(unBoardIndex, BA, OS, 0x0000FF00);
            //读取并记录中断状态
            OS = 0x7C;
            dotNetQTDrv.QTReadRegister(unBoardIndex, ref BA, ref OS, ref IntrState);
            Logger.Info(string.Format("UserEventAddr={0} IntrCtrl=0x{1:X} IntrState=0x{2:X}",
              UserEventAddr,
              IntrCtrl,
              IntrState));
            //中断处理过程
            uint x = 0xF << 8;
            x = 0xFF;
            if ((IntrCtrl & x) > 0)
            {
                //软件定义中断
                Logger.Error(string.Format("软件定义中断0x{0:X}", (IntrCtrl & 0xFF)));
                if ((IntrCtrl & 1) > 0)
                {
                    Logger.Error(string.Format("[WARNING]FPGA超温告警（高于100℃），请关闭电源或增强风扇转速，否则可能永久性损坏FPGA。", (IntrCtrl & 0xFF)));
                }
                if ((IntrCtrl & 2) > 0)
                {
                    Logger.Error(string.Format("[WARNING]VCCAUX超压告警(超出[1.746~1.854]V），请关闭电源，检查电源或联系技术支持。", (IntrCtrl & 0xFF)));
                }
                if ((IntrCtrl & 4) > 0)
                {
                    Logger.Error(string.Format("[WARNING]VCCINT超压告警(超出[1.746~1.854]V），请关闭电源，检查电源或联系技术支持。", (IntrCtrl & 0xFF)));
                    dotNetQTDrv.LDSetParam(unBoardIndex, Comm.CMD_MB_MARGIN_HIGH_VALUE, MARGIN_HIGH_VALUE, 0, 0, 0xffff);
                    Logger.Info(string.Format("尝试提高VCCINT电压，设置mantissa data {0}", MARGIN_HIGH_VALUE));
                }
                if ((IntrCtrl & 0x10) > 0)//BD/GPS 秒时间中断，每30秒一次
                {
                    UInt32 gps_ba = BA;
                    UInt32 gps_os = 0x58;
                    UInt32 gps_sec = 0;
                    dotNetQTDrv.QTReadRegister(unBoardIndex, ref gps_ba, ref gps_os, ref gps_sec);
                    Logger.Info(string.Format("[INFO]BD/GPS秒时间{0}。", gps_sec));
                    if (gps_sec > 1691488826)//大于2023年8月8日才认为是合法的
                    {
                        //更新本地时间，以保持与BD/GPS时间同步
                        DateTimeSynchronization SyncTime = new DateTimeSynchronization();
                        DateTime CurrentTime = SyncTime.ConvertTimeStampToDateTime(gps_sec);
                        if (!SyncTime.SetLocalDateTime(CurrentTime))
                        {
                            Logger.Error("修改本地时间失败{0}");
                        }
                    }
                }
                //屏蔽中断，避免频繁中断，降低系统响应速度
                SWIEn = 0xF0;//关闭温度、电压告警
            }
            x = 2 << 8;//da用通道1
            if ((IntrCtrl & x) > 0)
            {
                //ad的JESD接口sync状态，每一位代表一个通道校准完成
                Logger.Error(string.Format("[ERROR]DAC高速接口状态异常0x{0:X}，请重新执行初始化操作", ((IntrState >> 6) & 0xFF)));
                //TODO STOP DA
                //ForceStopDaq();
            }
            x = 1 << 12;
            x = 1 << 13;
            if ((IntrCtrl & x) > 0)
            {
                //DMA FIFO溢出
                Logger.Error(string.Format("读DDR溢出"));
                //todo stop da
                //ForceStopDaq();
                //MessageBox.Show(string.Format("读DDR溢出，请重新采集数据"), "ERROR");
            }
            x = 1 << 14;
            if ((IntrCtrl & x) > 0)
            {
                //参考时钟状态标志，为1表示使用内参考时钟，为0表示外参考
                Logger.Error(string.Format("[INFO]参考时钟切换为{0}，请停止采集，重新执行初始化操作", (((IntrState >> 14) & 1) == 1) ? "内参考时钟" : "外参考时钟"));
                //TODO stop DA
            }

            //使能感兴趣的中断
            OS = 0x3C;
            dotNetQTDrv.QTWriteRegister(unBoardIndex, BA, OS, ((SWIEn | AdcNotSyncEn | DdrAfullEn | ChFifoFullEn | RefClkSwEn) << 16));

        }
        public abstract void CaculateFIR();
        public void DataPackProcess(int unitId,//接收机编号
        int channelNo,//DDC通道号，调用方法设置的。
        IntPtr Buffer,//数据
        int len//数据长度，单位字节
        )
        {
            if ((unitId < 0) || (unitId >= NumberOfCards))
            {
                Logger.Error(string.Format("接收机编号错误{0}", unitId), "ERROR");
                return;
            }
            Marshal.Copy(Buffer, DDCCBBuf, 0, len);
            int packnum = len / PackLenB;
            mutex_cb.WaitOne();
            DDCPackCnt[unitId] += (uint)packnum;
            mutex_cb.ReleaseMutex();
        }
        public void FFTPackProcess(
        int unitId,//接收机编号
        IntPtr data,//频谱数据
        int len,//数据长度，单位字节
        double rate,//点前扫描速度
        short mgc,//MGC回调
        uint startFreq,//开始频率
        uint stopFreq,//结束频率
        float rbw,//分辨率
        int FftMode//0：全景；1：定频
       )
        {
            if ((unitId < 0) || (unitId >= NumberOfCards))
            {
                Logger.Error(string.Format("接收机编号错误{0}", unitId), "ERROR");
                return;
            }
            Marshal.Copy(data, FFTCBBuf, 0, len);
            int packnum = len / PackLenB;
            mutex_cb.WaitOne();
            FFTPackCnt[unitId] += (uint)packnum;
            mutex_cb.ReleaseMutex();
        }
        
        public int ld_ChkRT(int value, string log = "")
        {
            int nRet = -1;
            nRet = value;
            string logstr = "";
            if (nRet != 0)
            {
                StackTrace st = new StackTrace(new StackFrame(true));
                //Logger.Info(" Stack trace for current level: {0}", st.ToString());
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

        public  int ChkFreq(uint unCardIdx, uint samplerate, int WorkMode)
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
            int OutFreq = Convert.ToInt32(Rdval) >> 16;
            long err = Math.Abs(samplerate / 1000000 / Srate2Fadc - OutFreq);
            if (err > 2)
            {
                Logger.Error(string.Concat("时钟频率错误 ", Convert.ToString(SampleRate), " ", Convert.ToString(OutFreq)), "ERROR");
                return -1;
            }
            return 0;
        }

        private  int LoadConfig()
        {
            try
            {
                EnALG = int.Parse(ConfigurationManager.AppSettings["EnALG"]) > 0 ? true : false;
                EnDUC = int.Parse(ConfigurationManager.AppSettings["EnDUC"]) > 0 ? true : false;
                EnSim = int.Parse(ConfigurationManager.AppSettings["EnSim"]) > 0 ? true : false;
                AdcBaseAddr = Convert.ToUInt32(ConfigurationManager.AppSettings["AdcBaseAddr"], 16);
                DdcPulseUnit = Convert.ToUInt32(ConfigurationManager.AppSettings["DdcPulseUnit"], 16);
                for (int i = 0; i < 32; i++)
                {
                    AdcUsrReg[i] = Convert.ToUInt32(ConfigurationManager.AppSettings[string.Format("AdcUsrReg[{0}]", i)], 16);
                }
                DacBaseAddr = Convert.ToUInt32(ConfigurationManager.AppSettings["DacBaseAddr"], 16);
                DucPulseUnit = Convert.ToUInt32(ConfigurationManager.AppSettings["DucPulseUnit"], 16);
                for (int i = 0; i < 32; i++)
                {
                    DacUsrReg[i] = Convert.ToUInt32(ConfigurationManager.AppSettings[string.Format("DacUsrReg[{0}]", i)], 16);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("{0} in LoadConfig()", ex.Message), "ERROR");
                return -1;
                //throw;
            }
            return 0;
        }

        public ChannelState GetChannelState(int channelNumber)
        {
            lock (_lock)
            {
                if (channelNumber > 0 && channelNumber < ChannelStates.Length)
                {
                    return ChannelStates[channelNumber];
                }
                else
                {
                    throw new ArgumentOutOfRangeException("channel");
                }
            }
        }

        public void SetChannelState(int channel, ChannelState state)
        {
            //检查是否超出范围
            lock (_lock)
            {
                if (channel > 0 && channel < ChannelStates.Length)
                {
                    ChannelStates[channel] = state;
                }
                else
                {
                    throw new ArgumentOutOfRangeException("channel");
                }
            }
        }

        public abstract void StopOperation();

        public void SinglePlay(uint unBoardIndex, byte[] buffer, uint unLen, ref uint bytes_sent, uint unTimeOut, int DmaChIdx)
        {
            int bytecount = 0;
            //dotNetQTDrv.QTGetRegs_i32(unBoardIndex, Regs.PerBufByteCount, ref bytecount, DmaChIdx);
            bytecount = 0x100000;
            uint reqLen = unLen;
            uint offset = 0;
            uint PerLen = 0;
            uint SentByte = 0;
            do
            {
                PerLen = (reqLen > (uint)bytecount) ? (uint)bytecount : reqLen;
                dotNetQTDrv.QTSendData(unBoardIndex, buffer, offset, (uint)PerLen, ref SentByte, 1000, DmaChIdx);
                offset += SentByte;
                reqLen -= SentByte;
            } while (reqLen > 0);
            bytes_sent = unLen - reqLen;
        }
        
    }
}

using Lookdata;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TCP.Command.PCIE;
using static BackgroundTaskManager;

namespace TCP.Command
{
    public class DeviceStatsManager
    {
        //单例模式
        private static readonly object _lock = new object();
        private ChannelState[] channelStates;
        private static readonly Lazy<DeviceStatsManager> _instance =
        new Lazy<DeviceStatsManager>(() => new DeviceStatsManager());
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        //一些状态
        dotNetQTDrv.DDCCallBackHandle[] CallBackAppData;// = new dotNetQTDrv.DDCCallBackHandle[2];
        dotNetQTDrv.FFTCallBackHandle[] CallBackFFTData;// = new dotNetQTDrv.FFTCallBackHandle[2];
        dotNetQTDrv.PUSEREVENT_CALLBACK[] CallBackUserEvent;

       

        public FormalParams[,] WorkParams;
        #region GetDevice中输出的值
        public int couple_type { get; set; }
        public int NOB { get; set; }
        public int DevMaxSampleRate { get; set; }
        public int adda_revision { get; set; }
        public int product_number { get; set; }
        public int bForceIOdelay { get; set; }

        private int ReqSrate;

        public List<string> deviceList { get; set; }

        private int NameRule;
        private int SplitFileSizeMB;

        public int NoBoard { get; set; }
        #endregion
        #region deviceconfig中用到的
        public uint SampleRate { get;  set; }
        public uint RefClkMode { get;  set; }
        public int Fref { get; set; }
        public uint ADCClkMode { get; set; }
        #endregion
        #region loadconfig中有用到的
        public int EnDACWork { get; internal set; }

        private uint unBoardIndex;
        private UInt32 MARGIN_HIGH_VALUE;

        public bool EnALG { get; internal set; }
        public bool EnDUC { get; internal set; }
        public bool EnSim { get; internal set; }
        public uint AdcBaseAddr { get; internal set; }
        public uint DdcPulseUnit { get; internal set; }
        public uint DacBaseAddr { get; internal set; }
        public uint DucPulseUnit { get; internal set; }

        public UInt32[] AdcUsrReg = new UInt32[32];
        public UInt32[] DacUsrReg = new UInt32[32];
        private uint MaxNumFiles;
        private int MaxFileSizeMB;
        private object PPDATABUFLEN;
        private int ReplayedLenB;
        #endregion

        private DeviceStatsManager()
        {
            //目前是5个通道
            channelStates = new ChannelState[6]; 
            for (int i = 0; i < channelStates.Length; i++)
            {
                channelStates[i] = new ChannelState();
            }
            ReqSrate = 250000;
            deviceList = new List<string>();
            NameRule = 1;
            SplitFileSizeMB = 1024;// = 1024;
            MaxNumFiles = 0xffffffff;
            MaxFileSizeMB = 13312 * 1024;
            PPDATABUFLEN = 4 << 20;
            ReplayedLenB = 0;
            SampleRate = (uint)ReqSrate;
            EnDACWork = 1;
            unBoardIndex = 0;
            MARGIN_HIGH_VALUE = 3550;
            CallBackAppData = new dotNetQTDrv.DDCCallBackHandle[2];
            CallBackFFTData = new dotNetQTDrv.FFTCallBackHandle[2];
            CallBackUserEvent = new dotNetQTDrv.PUSEREVENT_CALLBACK[1];
            CallBackUserEvent[0] = CallBackFunc_UserEvent_DA;
            Logger.Info("DeviceStatsManager is created.Memory has been used");
        }
        public static DeviceStatsManager Instance =>_instance.Value;

        public ChannelState GetChannelState(int channel)
        {
            //检查是否超出范围
            lock (_lock)
            {
                if (channel > 0 && channel < channelStates.Length) {
                    return channelStates[channel];
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
                if (channel > 0 && channel < channelStates.Length)
                {
                    channelStates[channel] = state;
                }
                else
                {
                    throw new ArgumentOutOfRangeException("channel");
                }
            }
        }
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

    }

    public class DateTimeSynchronization
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SystemTime
        {
            public short year;
            public short month;
            public short dayOfWeek;
            public short day;
            public short hour;
            public short minute;
            public short second;
            public short milliseconds;
        }

        [DllImport("kernel32.dll")]
        private static extern bool SetLocalTime(ref SystemTime time);

        public uint swapEndian(ulong x)
        {
            return (uint)(((x & 0x000000ff) << 24) +
            ((x & 0x0000ff00) << 8) +
            ((x & 0x00ff0000) >> 8) +
            ((x & 0xff000000) >> 24));
        }

        /// <summary>
        /// 设置系统时间
        /// </summary>
        /// <param name="CurrentTime">需要设置的时间</param>
        /// <returns>返回系统时间设置状态，true为成功，false为失败</returns>
        public bool SetLocalDateTime(DateTime CurrentTime)
        {
            SystemTime st;
            st.year = (short)CurrentTime.Year;
            st.month = (short)CurrentTime.Month;
            st.dayOfWeek = (short)CurrentTime.DayOfWeek;
            st.day = (short)CurrentTime.Day;
            st.hour = (short)CurrentTime.Hour;
            st.minute = (short)CurrentTime.Minute;
            st.second = (short)CurrentTime.Second;
            st.milliseconds = (short)CurrentTime.Millisecond;
            bool rt = SetLocalTime(ref st);//设置本机时间
            return rt;
        }
        /// <summary>
        /// 转换时间戳为C#时间
        /// </summary>
        /// <param name="timeStamp">时间戳 单位：毫秒</param>
        /// <returns>C#时间</returns>
        public DateTime ConvertTimeStampToDateTime(long timeStamp)
        {
            DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)); // 当地时区
                                                                                                        //DateTime dt = startTime.AddMilliseconds(timeStamp);  
            DateTime dt = startTime.AddSeconds(timeStamp + 8 * 3600);//+8时区
            return dt;
        }
    }
}

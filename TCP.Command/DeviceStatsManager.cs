using Lookdata;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public List<string> deviceList { get; set; }
        public int NoBoard { get; set; }
        #endregion
        #region deviceconfig中用到的
        public int SampleRate { get;  set; }
        public uint RefClkMode { get;  set; }
        public int Fref { get; set; }
        public uint ADCClkMode { get; set; }
        #endregion

        private DeviceStatsManager()
        {
            //目前是5个通道
            channelStates = new ChannelState[6]; 
            for (int i = 0; i < channelStates.Length; i++)
            {
                channelStates[i] = new ChannelState();
            }
        }
        public static DeviceStatsManager Instance =>_instance.Value;

        public int EnDACWork { get; internal set; }

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

    }
}

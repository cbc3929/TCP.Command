using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCP.Command
{
    public class DeviceStatsManager
    {
        //单例模式
        
        private static readonly object _lock = new object();
        private ChannelState[] channelStates;
        private static readonly Lazy<DeviceStatsManager> _instance =
        new Lazy<DeviceStatsManager>(() => new DeviceStatsManager());
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

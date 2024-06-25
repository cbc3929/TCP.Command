using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TCP.Command.Interface;
using TCP.Command.PCIE;

namespace TCP.Command
{
    public class QueryStatusCommand : ICommand
    {
        private readonly string _commandText;
        private readonly int _channelNumber;
        private readonly DeviceStatsManager _deviceStatsManager;
        private readonly TCPServer _server;

        public QueryStatusCommand(string commandText, int channelNumber, TCPServer server)
        {
            _commandText = commandText;
            _channelNumber = channelNumber;
            _server = server;
            _deviceStatsManager = DeviceStatsManager.Instance;
        }

        public async Task ExecuteAsync(TcpClient client)
        {
            ChannelState state = _deviceStatsManager.GetChannelState(_channelNumber);
            string response = "";

            // 解析并处理查询命令
            if (_commandText.Contains(":BB:MODE"))
            {
                response = state.BBSwitch ? "On" : "Off";
            }
            else if (_commandText.Contains(":BB:ARB:SRATe"))
            {
                response = state.Srate.ToString() + state.SampSubUnit + "Hz";
            }
            else if (_commandText.Contains(":BB:ARB:SWITCH"))
            {
                response = state.ARBSwitch ? "On" : "Off";
            }
            else if (_commandText.Contains(":RF:SWITCH"))
            {
                response = state.RFSwitch ? "On" : "Off";
            }
            else if (_commandText.Contains(":RF:PEP"))
            {
                response = state.Power.ToString() + "dBm";
            }
            else if (_commandText.Contains(":FREQuency"))
            {
                response = state.FreqValue.ToString() + state.FreqSubUnit + "Hz";
            }
            else if (_commandText.Contains(":POWer"))
            {
                response = state.Power.ToString() + "dBm";
            }
            else if (_commandText.Contains(":PLAYBACK"))
            {
                response = state.PlaybackMethod;
            }
            else
            {
                response = "Unknown command";
            }

            await _server.SendMsgAsync(client, response);
        }


        public async Task UndoAsync()
        {
            // 查询命令通常不需要撤销逻辑
        }
    }
}

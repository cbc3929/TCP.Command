using System;
using System.Collections.Generic;
using System.Data;
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
        private readonly int _originNumber;
        private PcieCard _card;


        public QueryStatusCommand(string commandText, int channelNumber,PcieCard card )
        {
            _commandText = commandText;
            _originNumber = channelNumber==1?0:channelNumber;
            _channelNumber = channelNumber==1?channelNumber:channelNumber-2;
            _card = card;
        }

        public async Task ExecuteAsync()
        {
            ChannelState state = _card.GetChannelState(_channelNumber);
            string value = "";
            string commandType = "";

            // 解析并处理查询命令
            if (_commandText.Contains(":BB:MODE"))
            {
                value = state.BBSwitch ? "On" : "Off";
                commandType = "MODE";
                
            }
            else if (_commandText.Contains(":BB:ARB:SRATe"))
            {
                value = state.Srate.ToString() + state.SampSubUnit + "Hz";
                commandType = "SRATe";
            }
            else if (_commandText.Contains(":BB:ARB:SWITCH"))
            {
                value = state.ARBSwitch ? "On" : "Off";
                commandType = "ARB";
            }
            else if (_commandText.Contains(":RF:SWITCH"))
            {
                value = state.RFSwitch ? "On" : "Off";
                commandType = "RF";
            }
            else if (_commandText.Contains(":RF:PEP"))
            {
                value = state.Power.ToString() + "dBm";
                commandType = "PEP";
            }
            else if (_commandText.Contains(":FREQuency"))
            {
                value = state.FreqValue.ToString() + state.FreqSubUnit + "Hz";
                commandType = "FREQUENCY";
            }
            else if (_commandText.Contains(":POWer"))
            {
                value = state.Power.ToString() + "dBm";
                commandType = "POWER";
            }
            else if (_commandText.Contains(":PLAYBACK"))
            {
                switch (state.PlaybackMethod) 
                {
                    case PlaybackMethodType.SIN:
                        value = "SIN";
                        break;
                    case PlaybackMethodType.REP:
                        value = "REP";
                        break;
                    case PlaybackMethodType.TIC:
                        value = "TIC";
                        break;
                } 
                commandType = "PLAYBACK";
            }
            else
            {
                value = "Unknown command";
            }
            await TCPServer.SendMsgAsync(commandType, _originNumber, value);
        }


        public void Cancel()
        {
            // 查询命令通常不需要撤销逻辑
        }
    }
}

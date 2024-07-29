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
        private readonly int _absNumber;
        private PcieCard _card;


        public QueryStatusCommand(string commandText, int absNumber,PcieCard card )
        {
            _commandText = commandText;
            _absNumber =absNumber;
            _channelNumber = PCIeCardFactory.ConvertChannelNumber(absNumber);
            _card = card;
        }

        public async Task ExecuteAsync()
        {
            var state = _card.ChannelStates[_channelNumber];
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
                if (state.SampSubUnit != 'k' && state.SampSubUnit != 'G' && state.SampSubUnit != 'M')
                {
                    value = state.SrateOrigin.ToString() + "sps";
                }
                else 
                {
                    value = state.SrateOrigin.ToString() + state.SampSubUnit + "sps";
                }
                commandType = "SRATE";
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
                if (state.FreqSubUnit != 'k' && state.FreqSubUnit != 'M' && state.FreqSubUnit != 'G')
                {
                    value = state.FreqOrginValue.ToString()  + "Hz";
                }
                else 
                {
                    value = state.FreqOrginValue.ToString() + state.FreqSubUnit + "Hz";
                }
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
                        value = "SING";
                        break;
                    case PlaybackMethodType.REP:
                        value = "REP";
                        break;
                    case PlaybackMethodType.TIC:
                        value = "TICK";
                        break;
                } 
                commandType = "PLAYBACK";
            }
            else
            {
                value = "Unknown command";
            }
            await TCPServer.SendMsgAsync(commandType, _absNumber, value);
        }


        public void Cancel()
        {
            // 查询命令通常不需要撤销逻辑
        }
    }
}

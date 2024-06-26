using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using TCP.Command.Interface;
using TCP.Command.PCIE;

namespace TCP.Command
{
    public class SetStatusCommand : ICommand
    {
        private readonly string _commandText;
        private readonly int _channelNumber;
        private readonly int _originNumber;
        private PcieCard _card;
        //setting command

        public SetStatusCommand(string commandText, int channelNumber, PcieCard card)
        {
            _commandText = commandText;
            _originNumber = channelNumber == 1 ? 0 : channelNumber;
            _channelNumber = channelNumber==1?channelNumber:channelNumber-2;
            _card = card;
        }

        private string ParseCommandForValue(string command)
        {
            // 解析命令并获取设置值
            // 这里是一个简单的示例，您需要根据实际的命令格式来实现解析逻辑
            string[] parts = command.Split(' ');
            if (parts.Length >= 2)
            {
                return parts[1];
            }
            else
            {
                throw new ArgumentException("Invalid command format.");
            }
        }

        public async Task ExecuteAsync()
        {
            var _pBconfig = PBConfig.Instance;
            string valueUnitPart = ParseCommandForValue(_commandText);
            // 根据命令设置相应的状态
            if (_commandText.Contains(":BB:MODE"))
            {
                
                // 设置基带调制开关
                bool isOn = valueUnitPart.Contains("On") || valueUnitPart.Contains("1") || valueUnitPart.Contains("true");
                _card.GetChannelState(_channelNumber).BBSwitch = isOn;
                // 发送响应给客户端
                await TCPServer.SendMsgAsync("MODE",_originNumber, (isOn ? "On" : "Off"));

            }
            else if (_commandText.Contains("RF:SWITCH"))
            {
                bool isOn = valueUnitPart.Contains("On") || valueUnitPart.Contains("1") || valueUnitPart.Contains("true");
                if (isOn)
                {
                    //TODO 打开射频开关逻辑
                }
                _card.GetChannelState(_channelNumber).RFSwitch = isOn;
                await TCPServer.SendMsgAsync("RF", _originNumber, (isOn ? "On" : "Off"));
            }
            else if (_commandText.Contains("ARB:SWITCH"))
            {
                bool isOn = valueUnitPart.Contains("On") || valueUnitPart.Contains("1") || valueUnitPart.Contains("true");
                
                //_pBconfig.SetARBSwitch((uint)_channelNumber, isOn);

                _card.GetChannelState(_channelNumber).ARBSwitch = isOn;
                await TCPServer.SendMsgAsync("ARB", _originNumber, (isOn ? "On" : "Off"));
            }
            //下发文件
            else if (_commandText.Contains("ARB:SETTing:LOAD"))
            {
                await TCPServer.SendMsgAsync("LOAD", _originNumber, valueUnitPart);

            }
            else if (_commandText.Contains("FREQuency"))
            {
                int unitStartIndex = valueUnitPart.IndexOf("Hz");
                string valueStr = valueUnitPart.Substring(0, unitStartIndex);
                char subUnit = valueStr[valueStr.Length - 1];
                long subUnitValue = 1;
                if (subUnit == 'k')
                {
                    valueStr = valueStr.Substring(0, valueStr.Length - 1);
                    subUnitValue = 1000;
                }
                else if (subUnit == 'M')
                {
                    valueStr = valueStr.Substring(0, valueStr.Length - 1);
                    subUnitValue = 1000000;
                }
                else if (subUnit == 'G')
                {
                    valueStr = valueStr.Substring(0, valueStr.Length - 1);
                    subUnitValue = 1000000000;
                }
                if (long.TryParse(valueStr, out long value))
                {
                    _card.GetChannelState(_channelNumber).FreqValue = value * subUnitValue;
                    _card.GetChannelState(_channelNumber).FreqSubUnit = subUnit;
                    await TCPServer.SendMsgAsync("FREQUENCY", _originNumber, value+subUnit+"Hz");
                    //await _server.SendMsgAsync(client, "Frequency set to " + value + subUnit + "Hz");
                    //_pBconfig.SetFreqValue((uint)_channelNumber, value * subUnitValue);
                }
                else
                {
                    await TCPServer.SendMsgAsync("FREQUENCY", _originNumber,"ERROR");
                }
            }
            else if (_commandText.Contains(":POWer"))
            {
                int unitStartIndex = valueUnitPart.IndexOf("dBm");
                string valueStr = valueUnitPart.Substring(0, unitStartIndex);
                int value = int.Parse(valueStr);
                var state = _card.GetChannelState(_channelNumber);
                if (state != null)
                {
                    state.Power = value;
                    _card.SetChannelState(_channelNumber, state);
                }
                _pBconfig.SetPowerValue((uint)_channelNumber, value);
                await TCPServer.SendMsgAsync("POWER", _originNumber, valueStr);
            }
            else if (_commandText.Contains(":PLAYBACK"))
            {
                int typeStartPos = _commandText.IndexOf(":PLAYBACK");
                string subType = _commandText.Substring(typeStartPos + 9, 3);
                var state = _card.GetChannelState(_channelNumber);
                if (subType == "SIN")
                {
                    state = _card.GetChannelState(_channelNumber);
                    if (state != null)
                    {
                        state.PlaybackMethod = "SIN";
                        //await _server.SendMsgAsync(client, "Playback method set to SIN");
                    }
                    //_pBconfig.SetSingleReplay((uint)_channelNumber);
                }
                else if (subType == "TIC")
                {
                    state = _card.GetChannelState(_channelNumber);
                    if (state != null)
                    {
                        state.PlaybackMethod = "TIC";
                        //await _server.SendMsgAsync(client, "Playback method set to TIC");
                    }
                    //_pBconfig.SetTickClockReplay((uint)_channelNumber, valueUnitPart);
                }
                else if (subType == "REP")
                {
                    state = _card.GetChannelState(_channelNumber);
                    if (state != null)
                    {
                        state.PlaybackMethod = "REP";
                        //await _server.SendMsgAsync(client, "Playback method set to REP");
                    }
                    //_pBconfig.SetRepeatReplay((uint)_channelNumber,0);
                }
                await TCPServer.SendMsgAsync("PLAYBACK", _originNumber, state.PlaybackMethod);
            }
            else if (_commandText.Contains("ARB:SRATe"))
            {
                int unitStartIndex = valueUnitPart.IndexOf("sps");
                string valueStr = valueUnitPart.Substring(0, unitStartIndex);
                char subUnit = valueStr[valueStr.Length - 1];
                long subUnitValue = 1;
                if (subUnit == 'k')
                {
                    valueStr = valueStr.Substring(0, valueStr.Length - 1);
                    subUnitValue = 1000;
                }
                else if (subUnit == 'M')
                {
                    valueStr = valueStr.Substring(0, valueStr.Length - 1);
                    subUnitValue = 1000000;
                }
                else if (subUnit == 'G')
                {
                    valueStr = valueStr.Substring(0, valueStr.Length - 1);
                    subUnitValue = 1000000000;
                }
                if (long.TryParse(valueStr, out long value))
                {
                    _card.GetChannelState(_channelNumber).Srate = value * subUnitValue;
                    _card.GetChannelState(_channelNumber).SampSubUnit = subUnit;
                    await TCPServer.SendMsgAsync("SRATE",_originNumber, value + subUnit + "Hz");
                    //_pBconfig.SetSRateValue((uint)_channelNumber, value * subUnitValue);
                }
                else
                {
                    await TCPServer.SendMsgAsync("SRATE",_originNumber, "ERROR");
                }
            }
            else
            {
                await TCPServer.SendMsgAsync("ERROR", _originNumber,"Unknown command" + _commandText);
            }

        }

        public Task UndoAsync()
        {
            throw new NotImplementedException();
        }
        public void Cancel() 
        { }

    }
}

using Lookdata;
using NLog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using TCP.Command.Command;
using TCP.Command.Interface;
using TCP.Command.PCIE;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TCP.Command
{
    public class SetStatusCommand : ICommand
    {
        private PcieCard _card;
        private int _absChannelNum;
        private int _channelNumber;
        private string _commandText;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        //setting command

        public SetStatusCommand(string commandText, int abschannelNum)
        {
            _card = PCIeCardFactory.CardParam[abschannelNum];
            _absChannelNum = abschannelNum;
            _channelNumber = PCIeCardFactory.ConvertChannelNumber(abschannelNum);
            _commandText = commandText;
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
                return "";
            }
        }

        public async Task ExecuteAsync()
        {
            string valueUnitPart = ParseCommandForValue(_commandText);
            var channelstate = _card.ChannelStates[_channelNumber];
            // 根据命令设置相应的状态

            if (_commandText.Contains("RF:SWITCH"))
            {
                bool isOn = valueUnitPart.Contains("On") || valueUnitPart.Contains("1") || valueUnitPart.Contains("true");
                //判断是宽带还是窄带
                var channelNum = _absChannelNum > 1 ? 0 : _absChannelNum - 2;
                //先控制状态，再去检查
                channelstate.RFSwitch = isOn;
                Logger.Error("rf " + isOn);
                ConfigRFModule(_card.unBoardIndex, 0, (uint)channelNum, (UInt32)CMD_TYPE.ON_OFF, (decimal)(channelstate.FreqValue / 1000),
                    channelstate.RF_Atten, channelstate.IF_Atten, (UInt32)(channelstate.RFSwitch ? 0 : 1));
                //await TCPServer.SendMsgAsync("RF", _absChannelNum, (isOn ? "On" : "Off"));
            }
            else if (_commandText.Contains("CHE")) 
            {
                Logger.Info("自检中..");
                await Task.Delay(2000);
                Logger.Info("自检完成");
            }
            else if (_commandText.Contains(":BB:MODE"))
            {
                var value = ParseCommandForValue(_commandText);
                if (value.Contains("On") || value.Contains("true") || value.Contains("1"))
                {
                    dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, _card.DacBaseAddr, 21 * 4, 1);
                    channelstate.BBSwitch = true;
                }
                else
                {
                    dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, _card.DacBaseAddr, 21 * 4, 0);
                    channelstate.BBSwitch = false;
                }
            }
            else if (_commandText.Contains("ARB:SWITCH"))
            {
                bool isOn = valueUnitPart.Contains("On") || valueUnitPart.Contains("1") || valueUnitPart.Contains("true");

                //_pBconfig.SetARBSwitch((uint)_channelNumber, isOn);

                channelstate.ARBSwitch = isOn;
                //await TCPServer.SendMsgAsync("ARB", _absChannelNum, (isOn ? "On" : "Off"));
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
                if (double.TryParse(valueStr, out double value))
                {
                    channelstate.FreqValue = value * subUnitValue;
                    channelstate.FreqSubUnit = subUnit;
                    channelstate.FreqOrginValue = value;
                    Logger.Info("freq:" + channelstate.FreqValue);
                    ConfigRFModule(_card.unBoardIndex, 0, (uint)_channelNumber, (UInt32)CMD_TYPE.FREQ, (decimal)channelstate.FreqValue / 1000,
                        channelstate.RF_Atten, channelstate.IF_Atten, (UInt32)(channelstate.RFSwitch ? 0 : 1));
                    //await TCPServer.SendMsgAsync("FREQUENCY", _absChannelNum, value + subUnit + "Hz");
                    //await _server.SendMsgAsync(client, "Frequency set to " + value + subUnit + "Hz");
                    //_pBconfig.SetFreqValue((uint)_channelNumber, value * subUnitValue);
                }
                else
                {
                    await TCPServer.SendMsgAsync("FREQUENCY", _absChannelNum, "ERROR");
                }
            }
            else if (_commandText.Contains(":POWer"))
            {
                int unitStartIndex = valueUnitPart.IndexOf("dBm");
                string valueStr = valueUnitPart.Substring(0, unitStartIndex);
                var value = double.Parse(valueStr);
                var state = channelstate;
                if (state != null)
                {
                    state.Power = (int)value;                 
                    if (value > 90 || value < 0)
                    {
                        Logger.Error("衰减范围超过阈值!");
                        return;
                    }
                    if (value > 45)
                    {
                        channelstate.RF_Atten = 45;
                        channelstate.IF_Atten = (decimal)(value - 45);
                    }
                    else 
                    {

                        channelstate.IF_Atten = 0;
                        channelstate.RF_Atten = (decimal)value;
                    }
                    Logger.Info("power:" + value); ;

                    ConfigRFModule(_card.unBoardIndex, 0, (uint)_channelNumber, (UInt32)CMD_TYPE.RF_ATT, (decimal)channelstate.FreqValue / 1000,
                        (uint)channelstate.RF_Atten, (uint)channelstate.IF_Atten, (UInt32)(channelstate.RFSwitch ? 0 : 1));
                    ConfigRFModule(_card.unBoardIndex, 0, (uint)_channelNumber, (UInt32)CMD_TYPE.IF_ATT, (decimal)channelstate.FreqValue / 1000,
                        (uint)channelstate.RF_Atten, (uint)channelstate.IF_Atten, (UInt32)(channelstate.RFSwitch ? 0 : 1));
                }
                //await TCPServer.SendMsgAsync("POWER", _absChannelNum, valueStr);
            }
            else if (_commandText.Contains(":PLAYBACK"))
            {
                int typeStartPos = _commandText.IndexOf(":PLAYBACK");
                string subType = _commandText.Substring(typeStartPos + 9, 3);
                switch (subType)
                {
                    case "SIN":
                        _card.ChannelStates[_channelNumber].PlaybackMethod = PlaybackMethodType.SIN;
                        break;
                    case "REP":
                        _card.ChannelStates[_channelNumber].PlaybackMethod = PlaybackMethodType.REP;
                        break;
                    case "TIC":
                        _card.ChannelStates[_channelNumber].PlaybackMethod = PlaybackMethodType.TIC;
                        _card.ChannelStates[_channelNumber].TicTime = valueUnitPart;
                        // 分割时间字符串
                        string dateTimeStr = valueUnitPart;
                        string[] dateTimeParts = dateTimeStr.Split('-');

                        // 提取各部分
                        uint year = uint.Parse(dateTimeParts[0]) - 2000;
                        uint month = uint.Parse(dateTimeParts[1]);
                        uint day = uint.Parse(dateTimeParts[2]);
                        uint hour = uint.Parse(dateTimeParts[3]);
                        uint minute = uint.Parse(dateTimeParts[4]);
                        uint second = uint.Parse(dateTimeParts[5]);
                        uint us = uint.Parse(dateTimeParts[6]);
                        uint ms = uint.Parse(dateTimeParts[7]);

                        // 计算一年中的第几天
                        uint[] daysInMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
                        if ((year + 2000) % 4 == 0 && ((year + 2000) % 100 != 0 || (year + 2000) % 400 == 0))
                        {
                            daysInMonth[1] = 29; // 闰年2月有29天
                        }
                        uint dayOfYear = day;
                        for (int i = 0; i < month - 1; i++)
                        {
                            dayOfYear += (uint)daysInMonth[i];
                        }

                        // 计算寄存器19的值
                        uint timeValue = (year << 26) | (dayOfYear << 17) | (hour << 12) | (minute << 6) | (second & 0x3F);

                        // 将计算出的值写入寄存器19
                        dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, 0x800e0000, 19 * 4, timeValue);

                        // 计算寄存器20的值
                        uint timeSubValue = ((us & 0xF) << 4) | (ms & 0xF);

                        // 将计算出的值写入寄存器20
                        dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, 0x800e0000, 20 * 4, timeSubValue);
                        break;
                }
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
                if (double.TryParse(valueStr, out double value))
                {
                    channelstate.Srate = (long)(value * subUnitValue);
                    channelstate.SampSubUnit = subUnit;
                    channelstate.SrateOrigin = value;
                    //await TCPServer.SendMsgAsync("SRATE", _absChannelNum, value + subUnit.ToString() + "Hz");
                    //_pBconfig.SetSRateValue((uint)_channelNumber, value * subUnitValue);
                }
                else
                {
                    await TCPServer.SendMsgAsync("SRATE", _absChannelNum, "ERROR");
                }
            }
            else
            {
                await TCPServer.SendMsgAsync("ERROR", _absChannelNum, "Unknown command" + _commandText);
            }


        }

        public Task UndoAsync()
        {
            throw new NotImplementedException();
        }
        public void Cancel()
        { }

        private int ConfigRFModule(UInt32 CardIndex, UInt32 rf_chan_type, UInt32 rf_chan_num, UInt32 Cmd_type, decimal freq, decimal RF_Atten, decimal IF_Atten, UInt32 RF_onff)
        {
            UInt32 ba = 0x800e0000;
            UInt32[] reg = new UInt32[3];
            //先写射频频率，衰减，on/off
            if (Cmd_type == (UInt32)CMD_TYPE.FREQ)
            {
                reg[1] = (UInt32)freq;


                dotNetQTDrv.QTWriteRegister(CardIndex, ba, 4 * 16, reg[1]);


            }
            else
            {
                reg[2] = (UInt32)RF_Atten + ((UInt32)IF_Atten << 6) + ((UInt32)RF_onff << 12);
                dotNetQTDrv.QTWriteRegister(CardIndex, ba, 4 * 17, reg[2]);
            }
            reg[0] = (UInt32)rf_chan_type + (rf_chan_num << 5) + (Cmd_type << 10) + (1 << 16);
            dotNetQTDrv.QTWriteRegister(CardIndex, ba, 4 * 15, reg[0]);
            reg[0] = (UInt32)rf_chan_type + (rf_chan_num << 5) + (Cmd_type << 10) + (0 << 16);
            dotNetQTDrv.QTWriteRegister(CardIndex, ba, 4 * 15, reg[0]);
            return 0;
        }
        public enum CMD_TYPE
        {
            /// <summary>
            /// 频率控制
            /// </summary>
            FREQ = 0,
            /// <summary>
            /// 射频衰减
            /// </summary>
            RF_ATT = 1,
            /// <summary>
            /// 中频衰减
            /// </summary>
            IF_ATT = 2,
            /// <summary>
            /// 通道开关
            /// </summary>
            ON_OFF = 8
        }

    }
}

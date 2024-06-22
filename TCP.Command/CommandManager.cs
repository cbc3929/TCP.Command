using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TCP.Command.Interface;

namespace TCP.Command
{
    public class CommandManager
    {
        private ConcurrentQueue<ICommand> commandQueue = new ConcurrentQueue<ICommand>();

        private ConcurrentStack<ICommand> commandHistory = new ConcurrentStack<ICommand>();

        public void EnquueCommand(ICommand command) 
        {
            commandQueue.Enqueue(command);
        }

        public async void ProcessCommandsAsync(TcpClient client)
        {
            while (!commandQueue.IsEmpty)
            {
                if (commandQueue.TryDequeue(out ICommand command))
                {
                    try
                    {
                        await command.ExecuteAsync(client);
                        commandHistory.Push(command);
                    }
                    catch (Exception ex)
                    {
                        while (!commandHistory.IsEmpty) {
                            Console.WriteLine("Error executing command: " + ex.Message);
                            Console.WriteLine("Undo commands ...");
                            if (commandHistory.TryPop(out ICommand undoCommand))
                            {
                                await undoCommand.UndoAsync();
                            }
                        }
                        throw;
                        
                    }
                }
            }
        }

        //根据commandText来返回对应的command
        public void ParseCommand(string commandText)
        {

            string CommandPack = commandText;
            //Console.WriteLine()
            //获取通道号
            string[] infoPart = CommandPack.Split(':');
            //单次回放也有Value 但后不跟值
            //if (infoPart.Length < 3)
            //{
            //    continue;
            //}
            string chanNumPart = infoPart[0];
            uint chanNum = uint.Parse(chanNumPart[chanNumPart.Length - 1].ToString());

            //else
            //{
            //    //设置命令       //触发界面上对应控件变化
            //    //获取设置值（值 + 单位）
            //    string valueUnitPart = null;
            //    for (int index = 0; index < infoPart.Length; ++index)
            //    {
            //        if (infoPart[index].Contains("VALue"))      //值设置
            //        {
            //            //中间有空格分隔
            //            string[] ValuePart = infoPart[index].Split(' ');
            //            if (ValuePart.Length >= 2)
            //            {
            //                valueUnitPart = ValuePart[1];
            //            }
            //            else
            //            {
            //                if (!CommandPack.Contains("PLAYBACKSING"))
            //                {
            //                    TcpServer.WriteLog("控制指令中Value部分格式异常：" + CommandPack);
            //                }
            //            }
            //            break;
            //        }
            //        else if (infoPart[index].Contains("LOAD"))       //文件下发
            //        {   //下发波形文件
            //            //中间有空格分隔
            //            string[] ValuePart = infoPart[index].Split(' ');
            //            //在绝对路径之前只有一个空格

            //            if (ValuePart.Length >= 2)
            //            {
            //                int filepathStartIndex = CommandPack.IndexOf(" ");
            //                valueUnitPart = CommandPack.Substring(filepathStartIndex, CommandPack.Length - filepathStartIndex);
            //            }
            //            else
            //            {
            //                TcpServer.WriteLog("控制指令中波形文件部分格式异常：" + CommandPack);
            //            }
            //            break;
            //        }
            //        else if (infoPart[index].Contains("SWITCH"))      //值设置
            //        {
            //            //中间有空格分隔
            //            string[] ValuePart = infoPart[index].Split(' ');
            //            if (ValuePart.Length >= 2)
            //            {
            //                valueUnitPart = ValuePart[1];
            //            }
            //            else
            //            {
            //                TcpServer.WriteLog("控制指令中开关部分格式异常：" + CommandPack);
            //            }
            //            break;
            //        }
            //        else if (infoPart[index].Contains("MODE"))      //值设置
            //        {
            //            //中间有空格分隔
            //            string[] ValuePart = infoPart[index].Split(' ');
            //            if (ValuePart.Length >= 2)
            //            {
            //                valueUnitPart = ValuePart[1];
            //            }
            //            else
            //            {
            //                TcpServer.WriteLog("控制指令中开关部分格式异常：" + CommandPack);
            //            }
            //            break;
            //        }
            //    }
            //    //
            //    if (valueUnitPart == null && !CommandPack.Contains(":PLAYBACKSING"))
            //    {
            //        TcpServer.WriteLog("控制指令中Value部分无法正常解析：" + CommandPack);
            //    }
            //    if (CommandPack.Contains(":BB"))    //基带设置
            //    {
            //        if (CommandPack.Contains(":MODE"))    //基带调制开关设置
            //        {
            //            //等同于回放开关
            //            if (valueUnitPart.Contains("On") ||
            //                valueUnitPart.Contains("1") ||
            //                valueUnitPart.Contains("true"))
            //            {
            //                BBSwitch[chanNum] = true;
            //            }
            //            else
            //            {
            //                BBSwitch[chanNum] = false;
            //            }

            //            //当前无响应方法
            //        }
            //        else if (CommandPack.Contains(":ARB")) //基带回放设置
            //        {
            //            if (CommandPack.Contains(":SRATe")) //基带回放采样率设置
            //            {
            //                int unitStartIndex = valueUnitPart.IndexOf("sps");
            //                string valueStr = valueUnitPart.Substring(0, unitStartIndex);
            //                char subUnit = valueStr[valueStr.Length - 1];
            //                long subUnitValue = 1;
            //                if (subUnit == 'k')
            //                {
            //                    valueStr = valueStr.Substring(0, valueStr.Length - 1);
            //                    subUnitValue = 1000;
            //                }
            //                else if (subUnit == 'M')
            //                {
            //                    valueStr = valueStr.Substring(0, valueStr.Length - 1);
            //                    subUnitValue = 1000000;
            //                }
            //                else if (subUnit == 'G')
            //                {
            //                    valueStr = valueStr.Substring(0, valueStr.Length - 1);
            //                    subUnitValue = 1000000000;
            //                }
            //                long value = (long)(double.Parse(valueStr) * subUnitValue);
            //                SampSubUnit[chanNum] = subUnit;
            //                PBConfigDis.SetSRateValue(chanNum, value);
            //                Srate[chanNum] = double.Parse(valueStr);
            //            }
            //            else if (CommandPack.Contains(":SWITCH"))   //基带回放开关设置
            //            {
            //                //等同于回放开关
            //                if (valueUnitPart.Contains("On") ||
            //                    valueUnitPart.Contains("1") ||
            //                    valueUnitPart.Contains("true"))
            //                {
            //                    PBConfigDis.SetARBSwitch(chanNum, true);
            //                    ARBSwitch[chanNum] = true;
            //                }
            //                else
            //                {
            //                    PBConfigDis.SetARBSwitch(chanNum, false);
            //                    ARBSwitch[chanNum] = false;
            //                }
            //            }
            //            else if (CommandPack.Contains(":LOAD")) //基带回放文件设置
            //            {
            //                PBConfigDis.SetARBWaveDownLoadFile(chanNum, valueUnitPart);
            //            }
            //        }
            //    }
            //    else if (CommandPack.Contains(":RF"))   //射频设置
            //    {
            //        if (chanNum == 1)
            //        {
            //            Dev_Config.ReplaySampleRate = (Int32)(double.Parse(ConfigurationManager.AppSettings["WBSampRate"]) * 1e6);     //根据不同通道下的情况直接将该值进行设置
            //        }
            //        else
            //        {
            //            Dev_Config.ReplaySampleRate = (Int32)(double.Parse(ConfigurationManager.AppSettings["NBSampRate"]) * 1e6);     //根据不同通道下的情况直接将该值进行设置
            //        }

            //        if (CommandPack.Contains(":SWITCH"))    //射频开关设置    //等同于开始停止回放
            //        {
            //            if (valueUnitPart.Contains("On") ||
            //                valueUnitPart.Contains("1") ||
            //                valueUnitPart.Contains("true"))
            //            {
            //                Button_StartPB_Click(0, null);
            //                RFSwitch[chanNum] = true;
            //            }
            //            else
            //            {
            //                Button_StopPB_Click(0, null);
            //                RFSwitch[chanNum] = false;
            //            }
            //        }
            //    }
            //    else if (CommandPack.Contains(":FREQuency"))    //频率设置
            //    {
            //        int unitStartIndex = valueUnitPart.IndexOf("Hz");
            //        string valueStr = valueUnitPart.Substring(0, unitStartIndex);
            //        char subUnit = valueStr[valueStr.Length - 1];
            //        long subUnitValue = 1;
            //        if (subUnit == 'k')
            //        {
            //            valueStr = valueStr.Substring(0, valueStr.Length - 1);
            //            subUnitValue = 1000;
            //        }
            //        else if (subUnit == 'M')
            //        {
            //            valueStr = valueStr.Substring(0, valueStr.Length - 1);
            //            subUnitValue = 1000000;
            //        }
            //        else if (subUnit == 'G')
            //        {
            //            valueStr = valueStr.Substring(0, valueStr.Length - 1);
            //            subUnitValue = 1000000000;
            //        }
            //        FreqSubUnit[chanNum] = subUnit;
            //        long value = (long)(double.Parse(valueStr) * subUnitValue);

            //        PBConfigDis.SetFreqValue(chanNum, value);
            //        FreqValue[chanNum] = double.Parse(valueStr); ;
            //    }
            //    else if (CommandPack.Contains(":POWer"))    //功率设置
            //    {
            //        int unitStartIndex = valueUnitPart.IndexOf("dBm");
            //        string valueStr = valueUnitPart.Substring(0, unitStartIndex);
            //        int value = int.Parse(valueStr);
            //        PBConfigDis.SetPowerValue(chanNum, value);
            //        Power[chanNum] = value;
            //    }
            //    else if (CommandPack.Contains(":PLAYBACK"))
            //    {
            //        int typeStartPos = CommandPack.IndexOf(":PLAYBACK");
            //        string subType = CommandPack.Substring(typeStartPos + 9, 3); //:PLAYBACK + 后面三个字节

            //        if (subType == "SIN")
            //        {
            //            PlaybackMethod[chanNum] = "SING";
            //            PBConfigDis.SetSingleReplay(chanNum);
            //        }
            //        else if (subType == "TIC")
            //        {
            //            PlaybackMethod[chanNum] = "TICK";
            //            PBConfigDis.SetTickClockReplay(chanNum, valueUnitPart);
            //        }
            //        else if (subType == "REP")
            //        {
            //            PlaybackMethod[chanNum] = "REP";
            //            //随机间隔


            //            //定间隔


            //            PBConfigDis.SetRepeatReplay(chanNum, 0);
            //        }
            //    }
            //}

            //switch (commandText)
            //{
            //    case "1":
            //        return new Command1();
            //    case "2":
            //        return new Command2();
            //    default:
            //        return null;
            //}
        }

    }
}

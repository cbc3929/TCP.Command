using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TCP.Command.Command;
using TCP.Command.Interface;
using TCP.Command.PCIE;

namespace TCP.Command
{
    public static class CommandFactory
    {
        /// <summary>
        /// 从命令文本解析出命令对象
        /// </summary>
        /// <param name="commandText"> 单条命令语句</param>
        /// <param name="chanNum">通道号</param>
        /// <param name="tcpServer">tcpserver服务器</param>
        /// <returns></returns>
        public static ICommand ParseCommand(string commandText,int chanNum)
        {
            PcieCard? card = null;
            //命令过来要发给哪个卡如果是1则是发给宽带，第一号通道
            if (chanNum == 1)
            {
                card = PCIeCardFactory.pcieCards.Find(card => card.DeviceName == "宽带") as WBCard;
            }
            else
            {
                card = PCIeCardFactory.pcieCards.Find(card => card.DeviceName == "窄带") as NBCard;
                //转化命令的通道号 和 卡上的通道号
                chanNum = chanNum - 2;
            }
            if (commandText.Contains("?"))
            {
                return new QueryStatusCommand(commandText, chanNum, card);
            }
            else if(commandText.Contains("PLAYBACK"))
            {
                int typeStartPos = commandText.IndexOf(":PLAYBACK");
                string subType = commandText.Substring(typeStartPos + 9, 3);
                switch (subType) 
                {
                    case "SIN":
                        return new SingleRunCommand(card, chanNum);
                    case "REP":
                        return new LoopRunCommand(card, chanNum); 
                }
                
            }
            else
            {
                return new SetStatusCommand(commandText, chanNum, card);
            }
            return null;
        }
    }
}

using Lookdata;
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
        public static ICommand ParseCommand(string commandText,int abschanNum)
        {
            var card = PCIeCardFactory.CardParam[abschanNum];
            var chanNum = abschanNum == 1 ? 0 : abschanNum - 2;
            if (commandText.Contains("?"))
            {
                return new QueryStatusCommand(commandText, chanNum, card);
            }
            
            else if (commandText.Contains("ARB:SETTing:LOAD")) 
            {
                return new SetFileCommand(abschanNum,commandText);
            }
            else
            {
                return new SetStatusCommand(commandText, abschanNum);
            }
        }
        private static string ParseCommandForValue(string command)
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
    }
}

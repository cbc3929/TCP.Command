using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TCP.Command.Interface;
using TCP.Command.PCIE;

namespace TCP.Command
{
    public class CommandFactory
    {
        /// <summary>
        /// 从命令文本解析出命令对象
        /// </summary>
        /// <param name="commandText"> 单条命令语句</param>
        /// <param name="chanNum">通道号</param>
        /// <param name="tcpServer">tcpserver服务器</param>
        /// <returns></returns>
        public static ICommand ParseCommand(string commandText,int chanNum, TCPServer tcpServer)
        {
            
            
            if (commandText.Contains("?"))
            { 
                return new QueryStatusCommand(commandText,chanNum,tcpServer);
            }else return new SetStatusCommand(commandText,chanNum,tcpServer);
        }
    }
}

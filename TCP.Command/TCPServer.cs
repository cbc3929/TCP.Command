using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TCP.Command.Interface;
using TCP.Command.PCIE;
using NLog;
using TCP.Command.Command;

namespace TCP.Command
{
    public class TCPServer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private TcpListener tcpListener;
        private bool isRunning;
        private static TcpClient? currentClient;
        private readonly object lockObject = new object();
        private CommandManager commandQueueManager = new CommandManager();
        private Task[]? backgroundTasks;
        private CancellationTokenSource cts = new CancellationTokenSource();

        private void Log(string message)
        {
            Logger.Info(message);
        }

        private void LogError(string message)
        {
            Logger.Error(message);
        }

        public TCPServer(int port) {
            tcpListener = new TcpListener(IPAddress.Any,port);
            isRunning = true;
            tcpListener.Start();
            Log(DateTime.Now.ToString("g")+ " Server started");
            _ = AcceptClientAsync(tcpListener);

        }

   
        /// <summary>
        /// 发送命令给当前连接的客户端
        /// </summary>
        /// <param name="client"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static async Task SendMsgAsync(string commandType,int channelNumber,string value)
        {
            var message = ":REPLAY"+channelNumber+":TYPE"+commandType+":VALUE"+value;
            
            try
            {
                NetworkStream stream = currentClient.GetStream();
                byte[] responseBytes = Encoding.UTF8.GetBytes(message);
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
            }
            catch (Exception ex) 
            {
                Logger.Error(ex);
            }
        }
        /// <summary>
        /// 客户端连接
        /// </summary>
        /// <param name="tcpListener"></param>
        /// <returns></returns>
        private async Task AcceptClientAsync(TcpListener tcpListener)
        {
            while (isRunning) 
            {
                try {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();
                    Log(DateTime.Now.ToString("g") + " Client connected");
                    lock (lockObject)
                    { 
                        if (currentClient!=null)
                        {
                            NotifyClientOfDisconnection(currentClient);
                            currentClient.Close();
                        }

                        currentClient = client;
                        Log(DateTime.Now.ToString("g") + " Client accepted");
                        
                    }
                    _ = HandleClient(client);
                }
                catch(Exception ex) 
                {
                    Log(DateTime.Now.ToString("g") + " Error accepting client: " + ex.Message);
                }
                
            }
        
        }
        /// <summary>
        /// 通知客户端有新的客户端连接
        /// </summary>
        /// <param name="client"></param>

        private void NotifyClientOfDisconnection(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                string notification = "Another client has connected, you have been disconnected.";
                Log(DateTime.Now.ToString("g") + " The currentClient has been changed");
                byte[] data = Encoding.UTF8.GetBytes(notification);
                stream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Log(DateTime.Now.ToString("g") + " Error notifying client: " + ex.Message);
            }
        }
        /// <summary>
        /// 处理客户端请求
        /// </summary>
        /// <param name="tcpclient"></param>
        /// <returns></returns>
        private async Task HandleClient(TcpClient tcpclient) {
            try
            {
                using (NetworkStream stream = tcpclient.GetStream())
                {
                    byte[] buffer = new byte[4096];
                    while (isRunning && tcpclient == currentClient)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break;

                        string command = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Log(DateTime.Now.ToString("g") + " Received command: " + command);

                        //TODO: handle command here
                        ProcessCommand(command);
                        commandQueueManager.ProcessCommandsAsync();

                    }
                }
            }
            catch (Exception ex)
            {
                Log(DateTime.Now.ToString("g") + " Error handling client: " + ex.Message);
            }
            finally {
                if(tcpclient == currentClient)
                {
                    tcpclient.Close();
                    Log(DateTime.Now.ToString("g") + " Client disconnected");
                }
            }
        
        }
        /// <summary>
        /// 批量命令区分并压入队列
        /// </summary>
        /// <param name="commandText">单条/批量命令语句</param>
        private void ProcessCommand(string commandText) 

        {
            var matches = Regex.Matches(commandText, @"(:SOURce\d+|:OUTPut\d+)");

            int previousIndex = 0;
            foreach (Match match in matches)
            {
                if (match.Index != previousIndex)
                {
                    string command = commandText.Substring(previousIndex, match.Index - previousIndex).Trim();
                    if (!string.IsNullOrEmpty(command))
                    {
                        int channelNumber = ExtractChannelNumber(command);
                        ICommand commandobj = CommandFactory.ParseCommand(command, channelNumber);
                        commandQueueManager.EnquueCommand(commandobj);
                    }
                }
                previousIndex = match.Index;
            }

            // 添加最后一个命令
            if (previousIndex < commandText.Length)
            {
                string lastCommand = commandText.Substring(previousIndex).Trim();
                if (!string.IsNullOrEmpty(lastCommand))
                {
                    int channelNumber = ExtractChannelNumber(lastCommand);
                    try
                    {
                        ICommand commandobj = CommandFactory.ParseCommand(lastCommand, channelNumber);
                        commandQueueManager.EnquueCommand(commandobj);
                    }
                    catch (Exception ex) {
                        Logger.Error(ex);
                    }

                }
            }
        }
        /// <summary>
        /// 单独语句提取通道号
        /// </summary>
        /// <param name="command">单条语句</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private int ExtractChannelNumber(string command)
        {
            string pattern = @":SOURce(\d+)";
            if (command.Contains("OUTPut")){
                pattern = @":OUTPut(\d+)";
            }
            var match = Regex.Match(command, pattern);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int channelNumber))
            {
                return channelNumber;
            }
            throw new ArgumentException("Invalid command format. Channel number could not be extracted.");
        }
        /// <summary>
        /// 服务器关闭
        /// </summary>
        public void Stop() 
        {
            isRunning = false;
            tcpListener.Stop();
            Log(DateTime.Now.ToString("g") + " Server stopped");
        }

        
    }
}

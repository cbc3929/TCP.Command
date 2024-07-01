using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TCP.Command.Interface;

namespace TCP.Command.Command
{
    public class CommandManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private ConcurrentQueue<ICommand> commandQueue = new ConcurrentQueue<ICommand>();

        private ConcurrentStack<ICommand> commandHistory = new ConcurrentStack<ICommand>();

        public void EnquueCommand(ICommand command)
        {
            commandQueue.Enqueue(command);
        }
        public async void ProcessCommandsAsync()
        {
            while (!commandQueue.IsEmpty)
            {
                if (commandQueue.TryDequeue(out ICommand command))
                {
                    try
                    {
                        await command.ExecuteAsync();
                        commandHistory.Push(command);
                    }
                    catch (Exception ex)
                    {
                        while (!commandHistory.IsEmpty)
                        {
                           commandHistory.TryPop(out ICommand cmd);
                           cmd.Cancel();
                        }
                        throw;

                    }
                }
            }
        }
    }
}

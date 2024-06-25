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
                        while (!commandHistory.IsEmpty)
                        {
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


    }
}

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

        private static readonly Lazy<CommandManager> instance = new Lazy<CommandManager>(() => new CommandManager());
        public static CommandManager Instance => instance.Value;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private ConcurrentQueue<ICommand> commandQueue = new ConcurrentQueue<ICommand>();

        private ConcurrentStack<ICommand> commandHistory = new ConcurrentStack<ICommand>();


        private CommandManager() { }

        private List<Task> runningTasks = new List<Task>();

        private readonly object lockObject = new object();

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
                        var task = command.ExecuteAsync();
                        lock (lockObject)
                        {
                            runningTasks.Add(task);
                        }
                        await task;
                        lock (lockObject)
                        {
                            runningTasks.Remove(task);
                        }
                        commandHistory.Push(command);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex);
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


        public void CancelAllCommands()
        {
            foreach (var command in commandQueue)
            {
                command.Cancel();
            }

            lock (lockObject)
            {
                Task.WhenAll(runningTasks).Wait();
            }
        }
    }
}

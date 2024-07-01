using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TCP.Command.Interface;

namespace TCP.Command.Command
{
    internal class BlankCommand : ICommand
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public BlankCommand() { }
        public void Cancel()
        {
            throw new NotImplementedException();
        }

        public Task ExecuteAsync()
        {
            Logger.Info("nothing");
            return Task.CompletedTask;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCP.Command.Interface
{
    public interface ICommand
    {
        Task ExecuteAsync();
        void Cancel();

    }
}

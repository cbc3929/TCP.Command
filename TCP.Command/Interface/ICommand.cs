﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TCP.Command.PCIE;

namespace TCP.Command.Interface
{
    public interface ICommand
    {
        Task ExecuteAsync(TcpClient client);
        Task UndoAsync();
    }
}

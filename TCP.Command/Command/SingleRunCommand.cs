using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TCP.Command.Interface;
using TCP.Command.PCIE;

namespace TCP.Command.Command
{
    public class SingleRunCommand:ICommand
    {
        private PcieCard _card;
        private CancellationTokenSource _cts;
        private int _channelNo;

        public SingleRunCommand(PcieCard card,int channelNo)
        {
            _card = card;
            _cts = card.ChannelStates[channelNo].singleRunCts;
            _channelNo = channelNo;
        }

        public async Task ExecuteAsync()
        {
            await _card.ExecuteSingleRunAsync(_channelNo);
        }

        public void Cancel()
        {
            _cts.Cancel();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TCP.Command.Interface;
using TCP.Command.PCIE;

namespace TCP.Command.Command
{
    public class LoopRunCommand : ICommand
    {
        private PcieCard _card;
        private CancellationTokenSource _cts;
        private int _channelNo;

        public LoopRunCommand(PcieCard card,int channelNo)
        {
            _card = card;
            _cts = card.ChannelStates[channelNo].loopRunCts;
            _channelNo = channelNo;
        }

        public async Task ExecuteAsync()
        {
            await _card.ExecuteLoopRunAsync(_channelNo);
        }

        public void Cancel()
        {
            _cts.Cancel();
        }

    }
}

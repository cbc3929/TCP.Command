using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using TCP.Command.Interface;
using TCP.Command.PCIE;

namespace TCP.Command.Command
{
    public class MonitorCommand : ICommand
    {
        private PcieCard _card;
        private CancellationTokenSource _cts;
        private Func<bool> _cancelCondition;
        private int _channelNo;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public MonitorCommand(PcieCard card, Func<bool> cancelCondition,int channelNo)
        {
            _card = card;
            _cts = card.ChannelStates[channelNo].monitorCts;
            _cancelCondition = cancelCondition;
            _channelNo = channelNo;
        }

        public async Task ExecuteAsync()
        {
            await Task.Delay(1000);
            Logger.Info("Monitor Command Has been Excute");
        }

        public void Cancel()
        {
            _cts.Cancel();
        }
    }
}

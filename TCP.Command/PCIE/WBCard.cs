using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCP.Command.PCIE
{
    internal class WBCard : PcieCard
    {
        public WBCard( uint cardIndex,int numberofcards) : base(cardIndex,1, numberofcards)
        {
        }

        public override void CancelOperations(int channelNo)
        {
            throw new NotImplementedException();
        }

        public override int Initialize(uint uncardIndex)
        {
            throw new NotImplementedException();
        }

        public override void OnOperationCompleted(int channelNo)
        {
            throw new NotImplementedException();
        }

        public override void StopOperation()
        {
            throw new NotImplementedException();
        }
    }
}

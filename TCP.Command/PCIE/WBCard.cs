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

        public override int Initialize(uint uncardIndex)
        {
            throw new NotImplementedException();
        }
    }
}

using Lookdata;
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
            FS = 2400000000;
            SampleRate_WB = 2800000000;
            SplitFileSizeMB = 10 * 1024;
        }

        public override void CancelOperations(int channelNo)
        {
            throw new NotImplementedException();
        }

        public override int Initialize(uint uncardIndex)
        {
            uint[] ba = new uint[4];
            ba[0] = 0x800A0000;
            ba[1] = 0x800B0000;
            ba[2] = 0x80030000;
            ba[3] = 0x80080000;
            uint[] os = new uint[4];
            os[0] = 0x38;
            os[1] = 0x04;
            os[2] = 0x7c;
            os[3] = 0x7c;
            uint[] dac_jesd_sync = new uint[2];
            uint[] reg = new uint[32];
            do
            {
                //20240705 禁止上位机操作GPIO

                //dotNetQTDrv.LDSetParam(0,835u, 0, 0, 0, 1000);//设置为输入，高阻
                //dotNetQTDrv.LDSetParam(0, 836u, 0, 0, 0, 1000);//output=0
                //Thread.Sleep(1000);
                //dotNetQTDrv.LDSetParam(0, 836u, 1, 0, 0, 1000);//output=0
                //Thread.Sleep(1000);
                dotNetQTDrv.LDSetParam(uncardIndex, Comm.CMD_MB_RESET_ADC_INTERFACE, 0, 0, 0, 10000);
                //sync状态
                for (int j = 0; j < 2; j++)
                    dotNetQTDrv.QTReadRegister(unBoardIndex, ref ba[j], ref os[0], ref dac_jesd_sync[j]);
                //jesd regs
                for (int j = 0; j < 2; j++)
                    dotNetQTDrv.QTReadRegister(unBoardIndex, ref ba[j], ref os[1], ref reg[j]);
                //clock freq
                dotNetQTDrv.QTWriteRegister(unBoardIndex, 0x80030000, 0x0, 0x10000000);
                dotNetQTDrv.QTReadRegister(unBoardIndex, ref ba[2], ref os[2], ref reg[2]);
                //PLL lock
                dotNetQTDrv.QTReadRegister(unBoardIndex, ref ba[3], ref os[3], ref reg[3]);
                //fmc_sdr_dump_PLL(i);
            } while ((dac_jesd_sync[0] != 0x10001) || (dac_jesd_sync[1] != 0x10001));
            return 0;
        }

        public override void OnOperationCompleted(int channelNo)
        {
            throw new NotImplementedException();
        }
    }
}

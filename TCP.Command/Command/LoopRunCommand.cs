using Lookdata;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCP.Command.Interface;
using TCP.Command.PCIE;

namespace TCP.Command.Command
{
    public class LoopRunCommand : ICommand
    {

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private PcieCard _card;
        private int _channelNo;
        private int _absChannelNo;
        private CancellationTokenSource _cts;
        public LoopRunCommand(int absChannelNo)
        {
            _card = PCIeCardFactory.CardParam[absChannelNo];
            _channelNo = PCIeCardFactory.ConvertChannelNumber(absChannelNo);
            _absChannelNo = absChannelNo;
            _cts = _card.ChannelStates[_channelNo].loopRunCts;
        }

        public async Task ExecuteAsync()
        {

            Int64 TotalSent = 0;
            bool EnTrig = false;
            long FileSizeB = 0;
            await Task.Delay(1000);
            uint SentByte = 0;
            string OffLineFile = _card.FilePath[_channelNo];
            var token = _card.ChannelStates[_channelNo].loopRunCts;
            try
            {
                FileInfo fileInfo = new FileInfo(OffLineFile);
                if (fileInfo != null && fileInfo.Exists)
                {
                    FileSizeB = fileInfo.Length;
                    byte[] buffer = ReadBigFile(OffLineFile, (int)FileSizeB);
                    while (!token.IsCancellationRequested)
                    {
                        SinglePlay(_card.unBoardIndex, buffer, (uint)FileSizeB, ref SentByte, 1000, _channelNo);
                        if (!EnTrig)
                        {
                            UInt32 val = 1 + (UInt32)(1 << (_channelNo + 3));
                            dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, 0x800e0000, (uint)1 * 4, val);
                            EnTrig = true;
                        }
                        TotalSent += SentByte;
                        dotNetQTDrv.QTSetRegs_i64(_card.unBoardIndex, Regs.RepTotalMB, TotalSent, _channelNo);
                    }
                }

            }catch (Exception ex)
            {
                Logger.Info(ex);
            }
        }
        public void Cancel()
        {

        }

        private byte[] ReadBigFile(string filePath, int readByteLength)
        {
            FileStream stream = new FileStream(filePath, FileMode.Open);
            byte[] buffer = new byte[readByteLength];
            stream.Read(buffer, 0, readByteLength);
            stream.Close();
            stream.Dispose();
            return buffer;
            //string str = Encoding.Default.GetString(buffer) //如果需要转换成编码字符串的话
        }
        private void SinglePlay(uint unBoardIndex, byte[] buffer, uint unLen, ref uint bytes_sent, uint unTimeOut, int DmaChIdx)
        {
            int bytecount = 0;
            //dotNetQTDrv.QTGetRegs_i32(unBoardIndex, Regs.PerBufByteCount, ref bytecount, DmaChIdx);
            bytecount = 0x100000;
            uint reqLen = unLen;
            uint offset = 0;
            uint PerLen = 0;
            uint SentByte = 0;
            do
            {
                PerLen = (reqLen > (uint)bytecount) ? (uint)bytecount : reqLen;
                dotNetQTDrv.QTSendData(unBoardIndex, buffer, offset, (uint)PerLen, ref SentByte, 1000, DmaChIdx);
                offset += SentByte;
                reqLen -= SentByte;
            } while (reqLen > 0);
            bytes_sent = unLen - reqLen;
        }

    }
}

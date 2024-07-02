using Lookdata;
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
  internal class SetFileCommand : ICommand
  {
    private PcieCard _card;
    private int _absChannelNum;
    private int _channelNum;
    private string _commandText;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public SetFileCommand(int abschannelNum, string commandText)
    {
      _card = PCIeCardFactory.CardParam[abschannelNum];
      _absChannelNum = abschannelNum;
      _channelNum = PCIeCardFactory.ConvertChannelNumber(abschannelNum);
      _commandText = commandText;
    }
    public void Cancel()
    {
      Logger.Info("cancel setfile");
    }

    public async Task ExecuteAsync()
    {
      var filePath = ParseCommandForValue(_commandText);
      ConfigDacWorks(_card.unBoardIndex, filePath);
      if (_card.ChannelStates[_channelNum].Srate == 0) 
      {
        _card.ChannelStates[_channelNum].Srate = 256000;
      }
      await OnSrateChanged();
      Int64 TotalSent = 0;
      bool EnTrig = false;
      long FileSizeB = 0;
      uint SentByte = 0;
      string OffLineFile = _card.FilePath[_channelNum];
      var token = _card.ChannelStates[_channelNum].singleRunCts;
      try
      {
        FileInfo fileInfo = new FileInfo(OffLineFile);
        if (fileInfo != null && fileInfo.Exists)
        {
          FileSizeB = fileInfo.Length;
          byte[] buffer = ReadBigFile(OffLineFile, (int)FileSizeB);
          while (!token.IsCancellationRequested)
          {
            SentByte = await SinglePlayAsync(_card.unBoardIndex, buffer, (uint)FileSizeB, 1000, _channelNum);
            if (!EnTrig)
            {
              UInt32 val = 1 + (UInt32)(1 << (_channelNum + 3));
              dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, 0x800e0000, (uint)1 * 4, val);
              EnTrig = true;
            }
            TotalSent += SentByte;
            dotNetQTDrv.QTSetRegs_i64(_card.unBoardIndex, Regs.RepTotalMB, TotalSent, _channelNum);
          }
        }

      }
      catch (Exception ex)
      {
        Logger.Info(ex);
      }

    }

    private async Task OnSrateChanged()
    {
      uint duc_chan_num = (uint)_channelNum;
      uint cic = (uint)_card.ChannelStates[_channelNum].CICNum;
      uint regValue = (duc_chan_num << 16) | (cic & 0xFFFF);
      Logger.Info($"法罗计算完成，开始下发给11号寄存器，通道号{0}和前级插值倍数{1}", duc_chan_num.ToString(), cic.ToString());
      dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, _card.DacBaseAddr, 11 * 4, regValue);
      uint dds = _card.ChannelStates[_channelNum].DDS;
      uint farrow_inter = _card.ChannelStates[_channelNum].FarrowInterp;
      uint farrow_decim = _card.ChannelStates[_channelNum].FarrowDecim;
      uint duc_dds_pinc = dds & 0xFFFFFF;
      uint farrowValues = (farrow_decim << 16) | (farrow_inter & 0xFFFF);
      Logger.Info($"写入寄存器12,dds值位{0}", duc_dds_pinc.ToString());
      // 计算寄存器值并写入寄存器12
      dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, _card.DacBaseAddr, 12 * 4, duc_dds_pinc);

      Logger.Info($"写入寄存器13，法罗插值位{0}，法罗抽取为{1}", farrow_inter.ToString(), farrow_decim.ToString());
      // 将计算出的值写入寄存器13
      dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, _card.DacBaseAddr, 13 * 4, farrowValues);

    }
    private void ConfigDacWorks(uint CardIndex, string SingleFilePath)
    {
      uint XDMA_RING_BLOCK_SIZE = 4 << 20;
      _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.EnableReplay, 1), "使能回放标志位");
      _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.EnableStreaming, 1), "使能流盘标志位");
      _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.EnableVfifo, 0), "禁止虚拟FIFO标志位");
      //_card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.SRate, (int)_card.ReqSrate), "读取采样率");
      //ld_ChkRT(dotNetQTDrv.QTResetBoard(CardIndex), "复位板卡");
      _card.ld_ChkRT(dotNetQTDrv.LDSetParam(CardIndex, Comm.CMD_MB_ENABLE_REPLAY_MODE, 1, 0, 0, 0xFFFFFFFF), "配置DAC寄存器");

      //if (checkBox3.Checked)
      //    ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.ScopeMode, 1), "读取采样率");
      _card.ld_ChkRT(dotNetQTDrv.QTWriteRegister(CardIndex, 0x80030000, 0x44, 0), "清除通道选择");
      _card.ld_ChkRT(dotNetQTDrv.QTInputChannelSet(CardIndex, (uint)_channelNum, 0, 0, 0, 0, 1), "选择通道CH" + Convert.ToString(_channelNum + 1));

      //根据实际应用设置每个DMA通道的DMA中断长度，单位字节。可以不同取值。
      _card.NotifySizeB[_channelNum] = XDMA_RING_BLOCK_SIZE;
      _card.ld_ChkRT(dotNetQTDrv.QTSetNotifySizeB(CardIndex, _card.NotifySizeB[_channelNum], _channelNum),
          string.Format("设置DMA通道{0} {1}", _channelNum, _card.NotifySizeB[_channelNum]));

      //Select data format between offset binary code and two's complement
      Boolean EnDDS = false;//true modified by lxr
      if ((EnDDS))
        _card.ld_ChkRT(dotNetQTDrv.QTDataFormatSet(CardIndex, 1), "设置数据格式");
      else
        _card.ld_ChkRT(dotNetQTDrv.QTDataFormatSet(CardIndex, 0), "设置数据格式");
      byte[] fileloc = new byte[(int)255];
      fileloc = System.Text.Encoding.Default.GetBytes(SingleFilePath);
      /*byte[] */
      if (EnDDS)
        _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.ReplayDDS, 1), "使能回放标志为");
      else
        _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.ReplayDDS, 0), "禁止回放标志位");

      //按DMA通道设置回放文件参数
      //DmaEnChMask = 0;

      _card.FilePath[_channelNum] = SingleFilePath;


      //----Setup work mode, acquisition parameters
      var uncompressMode = 0;


      //----Setup trigger source and types
      if (_card.TrigSrc == 0)//"内触发"
      {
        //软触发
        _card.ld_ChkRT(dotNetQTDrv.QTSoftTriggerSet(CardIndex, Comm.QTFM_COMMON_TRIGGER_TYPE_RISING_EDGE, 1), "使能软触发");
      }
      else
      {
        _card.ld_ChkRT(dotNetQTDrv.QTExtTriggerSet(CardIndex, _card.ExtTrigSrcPort, (uint)_card.Trig_edge, 1), "使能外触发");
      }
      if (EnDDS)
        _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.ReplayDDS, 1), "使能回放标志为");
      else
        _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.ReplayDDS, 0), "禁止回放标志位");
      _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.IQ_en, 0), "禁止IQ回放");

      if (_card.ChannelStates[_channelNum].ARBSwitch)
        //开启循环回放单个或者文件列表功能
        _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.LoopMode, 1, _channelNum), "开启循环回放功能");
      else
        _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.LoopMode, 0, _channelNum), "关闭循环回放功能");


      Int64 trigdelay = (Int64)2 * (_card.SampleRate / 8 * 2);
      _card.ld_ChkRT(dotNetQTDrv.QTStart(CardIndex, Comm.QTFM_COMMON_TRANSMIT_DIRECTION_PC2BRD, 1, 2000), "开启DMA");
      if (_card.EnALG)
        DUCConfig(CardIndex, _channelNum);
    }

    private string ParseCommandForValue(string command)
    {
      // 解析命令并获取设置值
      // 这里是一个简单的示例，您需要根据实际的命令格式来实现解析逻辑
      string[] parts = command.Split(' ');
      if (parts.Length >= 2)
      {
        return parts[1];
      }
      else
      {
        throw new ArgumentException("Invalid command format.");
      }
    }
    public void DUCConfig(uint CardIndex, int channelNum)
    {
      if (_card.EnALG)
      {
        //运行过程中禁止修改插值倍数
        UInt32 ba = 0x800e0000;
        UInt32 os = 0;
        UInt32 val = 0;
        //数据经过处理回放（默认为1）（IQ数据）

        UInt32 dout_ctrl = 1;//回放数据选择控制
        UInt32 soft_rst_n = 1;//1：复位；
        UInt32[] valid_out = new UInt32[4];//4个通道回放开始使能
        UInt32 format_out = 0;//DA最终输出格式
        UInt32[] dds_conf = new UInt32[4];//中频DDS相位增长量和偏移量,当前保留没有用
        UInt32 format_dac = 0;//DAC数据格式
        UInt32 format_in = 0;//下发数据格式
        UInt32 format_spct = 0;//数据顺序时是否颠倒
        UInt32 duc_chan_num = (uint)channelNum;//通道编号（宽带固定位0，窄带0~3）
        UInt32[] duc_cic_num = new UInt32[4];//插值倍数=(300M/文件采样率)

        //射频控制
        //关闭所有通道使能，使能复位
        for (int i = 0; i < 4; i++)
          valid_out[i] = 0;//禁止输出
        val = dout_ctrl + (soft_rst_n << 1) + (valid_out[0] << 3) + (valid_out[1] << 4) + (valid_out[2] << 5) + (valid_out[3] << 6) + (format_out << 7);
        dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, ba, 1 * 4, val);
        //通道1上变频参数
        //DDS参数暂未使用，不设置
        //配置reg10~11
        val = format_dac + (format_in << 4) + (format_spct << 8);
        dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, ba, 10 * 4, val);
        duc_chan_num = 0;//通道0
                         //TODO 倍数问题
        duc_cic_num[0] = (UInt32)_card.ChannelStates[_channelNum].Magnitude;
        val = (duc_chan_num << 24) + (1 << 16) + duc_cic_num[duc_chan_num];
        dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, ba, 11 * 4, val);
        //FIXME!!!通道2~4上变频参数,暂时没用，以后再补充

        //for (int i = 0; i < 32; i++)
        //{
        //  dotNetQTDrv.QTWriteRegister(CardIndex, DacBaseAddr, (uint)i * 4, DacUsrReg[i]);
        //}
      }
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
    private void SinglePlaySync(uint unBoardIndex, byte[] buffer, uint unLen, ref uint bytes_sent, uint unTimeOut, int DmaChIdx)
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
    private async Task<uint> SinglePlayAsync(uint unBoardIndex, byte[] buffer, uint unLen, uint unTimeOut, int DmaChIdx)
    {
      uint bytesent = 0;
      await Task.Run(() => SinglePlaySync(unBoardIndex, buffer, unLen, ref bytesent, unTimeOut, DmaChIdx));
      return bytesent;


    }
  }
}

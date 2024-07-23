using Lookdata;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading;
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
        private ChannelState _channelState;
        private CancellationTokenSource _cts;
        private bool EnTrig;
        private bool IsNBcard;
        public SetFileCommand(int abschannelNum, string commandText)
        {
            _card = PCIeCardFactory.CardParam[abschannelNum];
            _absChannelNum = abschannelNum;
            _channelNum = PCIeCardFactory.ConvertChannelNumber(abschannelNum);
            _commandText = commandText;
            _channelState = _card.ChannelStates[_channelNum];
            _cts = _channelState.singleRunCts;
            EnTrig = false;
            IsNBcard = abschannelNum > 1 ? true : false;
        }


        public async Task Cancels()
        {
            await SetDACOnOrOff(false);
            ////把调制关闭
            //uint reg = (uint)_channelState.Props << 16 | 0;
            //dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, _card.DacBaseAddr, 21 * 4, reg);
            //_channelState.BBSwitch = false;
        }

        public void Cancel()
        {
            Logger.Info("临时cancel");
        }

        public async Task ExecuteAsync()
        {
            //宽带 每次要stopplay  然后重新config dacwork
            //窄带 第一次配置config dacwork 后面 只要 cancels
            //检查 是否是第一次
            if (IsNBcard)
            {
                Logger.Info("窄带");
                //是窄带 有没有配置过dacwork?
                if (_card.HasDac)
                {
                    //配置过 那不是firstrun
                    await Cancels();
                    _channelState.BBSwitch = false;
                    _card.Update_Num21((uint)_channelNum, (uint)_channelState.Props);
                }
                else
                {
                    //没配置过 配置dac 即可
                    await ConfigDacWorks(_card.unBoardIndex);
                }
            }
            else 
            {
                Logger.Info("宽带");
                //是宽带 唯一的频道上有在回放么？
                if (!_channelState.IsFirstRun)
                {
                    // 有回放 关闭回放
                    await StopPlay();
                    _channelState.BBSwitch = false;
                    _card.Update_Num21((uint)_channelNum, (uint)_channelState.Props);
                    Logger.Info("DAC 寄存器 复位");
                    //重新配置dacwork
                    //await ConfigDacWorks(_card.unBoardIndex);
                }
                await ConfigDacWorks(_card.unBoardIndex);
            }
            var filePath = ParseCommandForValue(_commandText);

            if (_card.ChannelStates[_channelNum].Srate == 0)
            {
                _card.ChannelStates[_channelNum].Srate = 256000;
            }
            _card.FilePath[_channelNum] = filePath;
            Int64 TotalSent = 0;
            EnTrig = false;  
            UInt64 SentByte = 0;
            uint SentByteNB = 0;
            string OffLineFile = _card.FilePath[_channelNum];
            var prop = await CaculateProportionFromFreqence();
            _channelState.Props = prop;
            Logger.Info("prop is " + prop);
            var newPath = await ReadAndProcessBinFileAsync(OffLineFile,IsNBcard,prop);
            PCIeCardFactory.NewFilePathList.Add(newPath);
            _channelState.IsRunning = true;
            //await InitChan();
            _channelState.IsFirstRun = false;
            try
            {
                FileInfo fileInfo = new FileInfo(newPath);
                if (fileInfo != null && fileInfo.Exists)
                {
                    if (IsNBcard)
                    {
                        long FileSizeB = 0;
                        FileSizeB = fileInfo.Length;
                        byte[] buffer = await ReadBigFile(newPath, (int)FileSizeB);
                        SetSendDDRDataConfig(fileInfo.Length);
                        SentByteNB = await SinglePlayAsync(_card.unBoardIndex, buffer, (uint)FileSizeB, 1000, _channelNum);
                        TotalSent += (Int64)SentByteNB;
                    }
                    else {
                        UInt64 FileSizeB = 0;
                        FileSizeB = (UInt64)fileInfo.Length;
                        byte[][]buffer = await ReadBigFileWB(newPath, FileSizeB);
                        SetSendDDRDataConfig(fileInfo.Length);
                        SentByte = await SinglePlayWBAsync(_card.unBoardIndex, buffer, (uint)FileSizeB, 1000, _channelNum);
                        TotalSent += (Int64)SentByte;
                    }
                    dotNetQTDrv.QTSetRegs_i64(_card.unBoardIndex, Regs.RepTotalMB, TotalSent, _channelNum);
                    //配置寄存器，使能打开
                    await SetDACOnOrOff(true);
                    //打开调制
                    _channelState.BBSwitch = true;
                    _card.Update_Num21((uint)_channelNum, (uint)prop);
                }

            }
            catch (Exception ex)
            {
                Logger.Info(ex);
            }

        }
        private async Task InitChan()
        {
            await Task.Run(async () =>
            {
                _channelState.mutex.WaitOne();//保证释放锁之前操作的都是DAC相关寄存器
                dotNetQTDrv.LDSetParam(_card.unBoardIndex, Comm.CMD_MB_ENABLE_REPLAY_MODE, 1, 0, 0, 0xFFFFFFFF);// 选择DAC寄存器
                dotNetQTDrv.LDReplayData(_card.unBoardIndex, _channelNum);
                _channelState.mutex.ReleaseMutex();

            });
        }

        private void SetSendDDRDataConfig(long fileSize)
        {
            uint DacBaseAddr1 = 0x80080000;//基地址

            uint offAddr_DACDataSartAddr = 0;//偏移地址 回放数据的起始地址
            uint offAddr_DACDataLen = 0;     //偏移地址 回放数据的长度
            uint offAddr_DACDataSartSend = 0;//偏移地址 开始下发数据信号

            uint regValue_DACDataSartAddr = 0;//值 回放数据的起始地址;由ddr硬件容量决定。之后换片后，第1，3DMA通道会改成1GiB(1*1024*1024*1024)
            uint regValue_DACDataLen = 0;     //值 回放数据的长度，单位字节，步长256（32字节的的整数倍），设置的长度 = 实际长度-1
            uint regValue_DACDataSartSend = 0;//值 开始下发数据信号
            switch (_channelNum)
            {
                case 0:
                    //dma "n" 回放数据的起始地址; 地址0x80080018，写入值0 都为固定值
                    offAddr_DACDataSartAddr = 0x18;//偏移地址 固定
                    regValue_DACDataSartAddr = 0;//固定

                    //dma“n” 回放数据的长度，单位字节，步长256（32字节的的整数倍），设置的长度 = 实际长度-1
                    offAddr_DACDataLen = 0x1c;//偏移地址 固定
                    
                    regValue_DACDataLen = (uint)fileSize - 1;// 40960000 - 1;//104857600 - 1;//测试数据，固定100MiB；根据实际调整补齐
                    if (!IsNBcard) {
                        regValue_DACDataLen = (uint)(fileSize / 2) - 1;
                    }

                    //dma"n" 开始下发数据信号
                    offAddr_DACDataSartSend = 0x14;//偏移地址 固定
                    regValue_DACDataSartSend = 1;//固定
                    break;
                case 1:
                    //dma "n" 回放数据的起始地址; 地址0x80080018，写入值0 都为固定值
                    offAddr_DACDataSartAddr = 0x24;//偏移地址 固定 
                    regValue_DACDataSartAddr = 1 * 1024 * 1024 * 1024;//固定 2024年7月20日 change

                    //dma“n” 回放数据的长度，单位字节，步长256（32字节的的整数倍），设置的长度 = 实际长度-1
                    offAddr_DACDataLen = 0x28;//偏移地址 固定
                    regValue_DACDataLen = (uint)fileSize - 1;//40960000 - 1;// 40960000 - 1;//104857600 - 1;//测试数据，固定100MiB；根据实际调整补齐                      

                    //dma"n" 开始下发数据信号
                    offAddr_DACDataSartSend = 0x20;//偏移地址 固定
                    regValue_DACDataSartSend = 1;//固定
                    break;
                case 2:
                    //dma "n" 回放数据的起始地址; 地址0x80080018，写入值0 都为固定值
                    offAddr_DACDataSartAddr = 0x30;//偏移地址 固定
                    regValue_DACDataSartAddr = 0;//固定

                    //dma“n” 回放数据的长度，单位字节，步长256（32字节的的整数倍），设置的长度 = 实际长度-1
                    offAddr_DACDataLen = 0x34;//偏移地址 固定
                    regValue_DACDataLen = (uint)fileSize - 1;//40960000 - 1;//40960000 - 1;//104857600 - 1;//测试数据，固定100MiB；根据实际调整补齐                      

                    //dma"n" 开始下发数据信号
                    offAddr_DACDataSartSend = 0x2c;//偏移地址 固定
                    regValue_DACDataSartSend = 1;//固定
                    break;
                case 3:
                    //dma "n" 回放数据的起始地址; 地址0x80080018，写入值0 都为固定值
                    offAddr_DACDataSartAddr = 0x3c;//偏移地址 固定
                    regValue_DACDataSartAddr = 1 * 1024 * 1024 * 1024;//固定 2024年7月20日 change

                    //dma“n” 回放数据的长度，单位字节，步长256（32字节的的整数倍），设置的长度 = 实际长度-1
                    offAddr_DACDataLen = 0x40;//偏移地址 固定
                    regValue_DACDataLen = (uint)fileSize - 1;//40960000 - 1;//40960000 - 1;//104857600 - 1;//测试数据，固定100MiB；根据实际调整补齐                      

                    //dma"n" 开始下发数据信号
                    offAddr_DACDataSartSend = 0x38;//偏移地址 固定
                    regValue_DACDataSartSend = 1;//固定
                    break;
                default:
                    Logger.Info($"设置发送DDR数据参数，DMA通道:{_channelNum}");
                    return;
            }
            //dma "n" 回放数据的起始地址;
            dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, DacBaseAddr1, offAddr_DACDataSartAddr, regValue_DACDataSartAddr);
            //dma“n” 回放数据的长度
            dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, DacBaseAddr1, offAddr_DACDataLen, regValue_DACDataLen);

            //告诉逻辑，回放数据的起始地址回到0 add 2024-7-21
            dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, DacBaseAddr1, offAddr_DACDataSartSend, 0);
            //dma"n" 开始下发数据信号
            dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, DacBaseAddr1, offAddr_DACDataSartSend, regValue_DACDataSartSend);
        }

        private async Task SetDACOnOrOff( bool IsEnablePlay)
        {
            var channelstate = _channelState;

            Logger.Info("onoff is " + IsEnablePlay);
            uint DacBaseAddr1 = 0x80080000;
            uint offAddr = 0x14;//ddr 开始读取数据
            uint regValue = 2;//ddr 开始读取数据

            switch (_channelNum)
            {
                case 0:
                    offAddr = 0x14;//ddr 开始/停止读取数据
                    break;
                case 1:
                    offAddr = 0x20;//ddr 开始/停止读取数据
                    break;
                case 2:
                    offAddr = 0x2c;//ddr 开始/停止读取数据
                    break;
                case 3:
                    offAddr = 0x38;//ddr 开始/停止读取数据
                    break;
                default:
                    Logger.Info($"设置DAC打开或关闭，DMA通道{_channelNum}");
                    return;
            }

            if (IsEnablePlay)
            {
                regValue = 2;//ddr 开始读取数据
            }
            else
            {
                regValue = 0;//ddr 停止读取数据
            }
            dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, DacBaseAddr1, offAddr, regValue);
            if (!IsEnablePlay)
            {
                await Task.Delay(100);//100ms
                Logger.Info("关闭");
            }
            if (IsEnablePlay)
            {
                //下发法罗
                await OnSrateChanged();
            }

            uint ba = 0x800e0000;
            uint os = 1 * 4;
            uint reg = 0;
            uint value = 0;
            uint vald_out = 0;//通道n回放开始使能


            dotNetQTDrv.QTReadRegister(_card.unBoardIndex, ref ba, ref os, ref reg);
            
            if (IsEnablePlay)
            {
                vald_out = (reg | ((uint)1 << (_channelNum +3 )));
            }
            else
            {
                vald_out = (reg & (~((uint)1 << (_channelNum +3))));
            }
            //给DA数据开关
            dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, 0x800e0000, os, vald_out);

        }

        private async Task<int> ReportFileInfo()
        {
            int TotalMB = 0;
            await Task.Run(() =>
            {
                dotNetQTDrv.QTGetRegs_i32(_card.unBoardIndex, Regs.RepTotalMB, ref TotalMB, _channelNum);

            });
            return TotalMB;
        }
        private async Task<int> CaculateProportionFromFreqence() 
        {
            var freq = _channelState.FreqValue;
            int prop = 10000;
            if (0 < freq && freq <= 1000000000)
            {
                prop = 32767;
            }
            else if (freq > 1000000000 && freq <= 2000000000) 
            {
                prop = 13000;
            }
            else if (freq > 2000000000 && freq <= 2700000000)
            {
                prop = 14000;
            }
            //2900-3300 有个凹陷 4200-4600(28000 14-12) 4700-5100(28000 9.5)
            else if (freq > 2700000000 && freq <= 3700000000)
            {
                prop = 28000;
            }
            else if (freq > 3700000000 && freq <= 5000000000)
            {
                prop = 29000;
            }
            else if (freq > 5000000000 && freq <= 6000000000)
            {
                prop = 22000;
            }
            return prop;

        }
        private async Task StopPlay()
        {

            ///停止板卡回放
            #region ///停止板卡回放
            {

                //停止回放文件线程
                //todo ...
                //if (backgroundWorker_writefile_DoWork != stop)
                //{while(1)}
                int RepKeepRun = -99;
                int DmaChIndex = 0;

                do
                {
                    System.Threading.Thread.Sleep(100);
                    dotNetQTDrv.QTGetRegs_i32(_card.unBoardIndex, Regs.RepKeepRun, ref RepKeepRun, DmaChIndex);//2023年3月9日23:32:23：增加DmaChIndex变量，获得当前DMA通道的变量值
                } while (RepKeepRun != 0);

                _channelState.mutex.WaitOne();//保证释放锁之前操作的都是DAC相关寄存器
                dotNetQTDrv.LDSetParam(_card.unBoardIndex, Comm.CMD_MB_ENABLE_REPLAY_MODE, 1, 0, 0, 0xFFFFFFFF);// 选择DAC寄存器
                //dotNetQTDrv.LDReplayStop(_card.unBoardIndex, _channelNum);//固定DMA CH1回放
                dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, _card.DacBaseAddr, (uint)1 * 4, 0x13);//‘1’：复位
                _channelState.mutex.ReleaseMutex();
                //----Stop acquisition and close card handle
                try
                {
                    dotNetQTDrv.QTStart(_card.unBoardIndex, Comm.QTFM_COMMON_TRANSMIT_DIRECTION_BRD2PC, 0, 2000);
                }
                catch (Exception err)
                {
                    Logger.Error(err);
                }
                dotNetQTDrv.QTResetBoard(_card.unBoardIndex);//关闭回放端口输出
                dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, 0x800F0000, (uint)0 * 4, 0x0);  //disable data gen
                dotNetQTDrv.rtp1clsWriteALGSingleRegister(_card.unBoardIndex, 1, 0);  //reset ku115 register

            }
            #endregion
            //_card.Muter_cb.WaitOne();
            //dotNetQTDrv.LDSetParam(_card.unBoardIndex, Comm.CMD_MB_ENABLE_REPLAY_MODE, 1, 0, 0, 0xFFFFFFFF);// 选择DAC寄存器
            //dotNetQTDrv.LDReplayStop(_card.unBoardIndex, 0);
            //dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, 0x800E0000, (uint)1 * 4, 0x3);
            //_card.Muter_cb.ReleaseMutex();
            //dotNetQTDrv.QTStart(_card.unBoardIndex, Comm.QTFM_COMMON_TRANSMIT_DIRECTION_BRD2PC, 0, 2000);
            //dotNetQTDrv.QTResetBoard(_card.unBoardIndex);
            //dotNetQTDrv.rtp1clsWriteALGSingleRegister(_card.unBoardIndex, 1, 0);

            //uint[] valid_out = new uint[_card.ChannelCount];
            //if (_card.ChannelCount > 1)
            //{
            //    for (int i = 0; i < _card.ChannelCount; i++)
            //        valid_out[i] = 0;//禁止输出
            //    uint val = 1 + (1 << 1) + (valid_out[0] << 3) + (valid_out[1] << 4) + (valid_out[2] << 5) + (valid_out[3] << 6) + (0 << 7);
            //    dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, _card.DacBaseAddr, 1 * 4, val);
            //    val = 1 + (0 << 1) + (valid_out[0] << 3) + (valid_out[1] << 4) + (valid_out[2] << 5) + (valid_out[3] << 6) + (0 << 7);
            //    dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, _card.DacBaseAddr, 1 * 4, val);
            //}
            //else
            //{
            //    for (int i = 0; i < _card.ChannelCount; i++)
            //        valid_out[i] = 0;//禁止输出
            //    uint val = 1 + (1 << 1) + (valid_out[0] << 3) + (0 << 4) + (0 << 5) + (0 << 6) + (0 << 7);
            //    dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, _card.DacBaseAddr, 1 * 4, val);
            //    val = 1 + (0 << 1) + (valid_out[0] << 3) + (0 << 4) + (0 << 5) + (0 << 6) + (0 << 7);
            //    dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, _card.DacBaseAddr, 1 * 4, val);
            //}


        }

        private async Task OnSrateChanged()
        {
            uint duc_chan_num = (uint)_channelNum;
            uint cic = (uint)_card.ChannelStates[_channelNum].CICNum;
            uint regValue = (duc_chan_num << 16) | (cic & 0xFFFF);
            Logger.Info("法罗计算完成，开始下发给11号寄存器，通道号" + duc_chan_num.ToString() + "和前级插值倍数" + cic.ToString());
            dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, _card.DacBaseAddr, 11 * 4, regValue);
            uint dds = _card.ChannelStates[_channelNum].DDS;
            uint farrow_inter = _card.ChannelStates[_channelNum].FarrowInterp;
            uint farrow_decim = _card.ChannelStates[_channelNum].FarrowDecim;
            uint duc_dds_pinc = dds;
            uint farrowValues = (farrow_decim << 16) | (farrow_inter & 0xFFFF);
            Logger.Info("写入寄存器12,dds值位" + duc_dds_pinc.ToString());
            // 计算寄存器值并写入寄存器12
            dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, _card.DacBaseAddr, 12 * 4, duc_dds_pinc);

            Logger.Info("写入寄存器13，法罗插值位" + farrow_inter.ToString() + "，法罗抽取为" + farrow_decim.ToString());
            // 将计算出的值写入寄存器13
            dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, _card.DacBaseAddr, 13 * 4, farrowValues);

            //7号寄存器 样本点数量
            dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, _card.DacBaseAddr, 7 * 4, (uint)_channelState.FilePointCount);

            //8号寄存器 循环回放时间间隔
            //宽带以300M为例周期 窄带以150M为周期
            double intervalTimeClockCounts = _channelState.IntervalTimeUs * 300;
            if (IsNBcard) {
                intervalTimeClockCounts = _channelState.IntervalTimeUs * 150;
            }
            if (intervalTimeClockCounts > uint.MaxValue) {
                intervalTimeClockCounts = uint.MaxValue;
            }
            dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, _card.DacBaseAddr, 8 * 4, (uint)intervalTimeClockCounts);

            //9号寄存器 回放模式选择
            uint IsSinglePlayMode = _channelState.PlaybackMethod == PlaybackMethodType.SIN ? 1u : 0u;
            dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, _card.DacBaseAddr, 9 * 4, (IsSinglePlayMode));
        }
        private async Task ConfigDacWorks(uint CardIndex)
        {
            _card.HasDac = true;
            //xdma 单位大小 ，如果判断文件 过小  调整 该值 保证效果
            uint XDMA_RING_BLOCK_SIZE = 4 << 20;
            //uint XDMA_RING_BLOCK_SIZE = 4 << 5;
            _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.EnableReplay, 1), "使能回放标志位");
            _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.EnableStreaming, 1), "使能流盘标志位");
            _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.EnableVfifo, 0), "禁止虚拟FIFO标志位");
            //_card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.SRate, (int)_card.ReqSrate), "读取采样率");
            //ld_ChkRT(dotNetQTDrv.QTResetBoard(CardIndex), "复位板卡");
            _card.ld_ChkRT(dotNetQTDrv.LDSetParam(CardIndex, Comm.CMD_MB_ENABLE_REPLAY_MODE, 1, 0, 0, 0xFFFFFFFF), "配置DAC寄存器");

            //if (checkBox3.Checked)
            //    ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.ScopeMode, 1), "读取采样率");
            _card.ld_ChkRT(dotNetQTDrv.QTWriteRegister(CardIndex, 0x80030000, 0x44, 0), "清除通道选择");
            _card.ld_ChkRT(dotNetQTDrv.QTInputChannelSet(CardIndex, (uint)0, 0, 0, 0, 0, 1), "选择通道CH" + Convert.ToString(_channelNum + 1));

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
            /*byte[] */
            if (EnDDS)
                _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.ReplayDDS, 1), "使能回放标志为");
            else
                _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.ReplayDDS, 0), "禁止回放标志位");

            //按DMA通道设置回放文件参数
            //DmaEnChMask = 0;
            Logger.Info("CC重置回放通道");
            //dotNetQTDrv.LDReplayInit(CardIndex, _channelNum);

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

            if (false)
                //开启循环回放单个或者文件列表功能
                _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.LoopMode, 1, _channelNum), "开启循环回放功能");
            else
                _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.LoopMode, 0, _channelNum), "关闭循环回放功能");


            Int64 trigdelay = (Int64)2 * (_card.SampleRate / 8 * 2);
            _card.ld_ChkRT(dotNetQTDrv.QTStart(CardIndex, Comm.QTFM_COMMON_TRANSMIT_DIRECTION_PC2BRD, 1, 2000), "开启DMA");
            if (!IsNBcard) {
                await DUCConfig(_card.unBoardIndex, _channelNum);
            }
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

        private async Task<string> ReadAndProcessBinFileAsync(string filePath, bool IsNbcard, int proportion = 5000, bool insertZero = false)
        {
            // 读取二进制文件
            byte[] data = await File.ReadAllBytesAsync(filePath);

            // 将二进制数据解析为整数，每个数由两个字节组成
            int numSamples = data.Length / 2;
            short[] samples = new short[numSamples];

            Parallel.For(0, numSamples, i =>
            {
                samples[i] = BitConverter.ToInt16(data, i * 2);
            });

            Console.WriteLine($"样本数量：{samples.Length}");
            var count = samples.Length;

            // 计算最大值
            short maxValue = samples.Max();
            Console.WriteLine("最大值:", maxValue);

            // 归一化和缩放
            short[] scaledSamples = new short[numSamples];

            Parallel.For(0, numSamples, i =>
            {
                double normalizedSample = (double)samples[i] / maxValue;
                scaledSamples[i] = (short)Math.Clamp(normalizedSample * proportion, short.MinValue, short.MaxValue);
            });

            // 插入0并还原为原来的格式，即2个有符号短整数字节
            byte[] packedData;
            if (insertZero)
            {
                packedData = new byte[scaledSamples.Length * 4];
                Parallel.For(0, scaledSamples.Length, i =>
                {
                    BitConverter.GetBytes(scaledSamples[i]).CopyTo(packedData, i * 4);
                    short PreValue = i > 0 ? scaledSamples[i - 1] : (short)0;
                    BitConverter.GetBytes((short)0).CopyTo(packedData, i * 4 + 2);
                });
                count = count * 2;
            }
            else
            {
                packedData = new byte[scaledSamples.Length * 2];
                Parallel.For(0, scaledSamples.Length, i =>
                {
                    BitConverter.GetBytes(scaledSamples[i]).CopyTo(packedData, i * 2);
                });
            }

            int originalLength = packedData.Length;
            int fillNumber = 32;
            if (!IsNbcard) 
            {
                fillNumber = 64;
            }
            int padding = (fillNumber - (packedData.Length % fillNumber)) % fillNumber;
            if (padding < 0) 
            {
                Array.Resize(ref packedData, packedData.Length + padding);
            }
            Logger.Info($"原始长度：{originalLength} 填充了 ：{ padding} 字节长度" );


            // 生成新的文件名并保存在原目录中
            string directory = Path.GetDirectoryName(filePath);
            string baseName = Path.GetFileNameWithoutExtension(filePath);
            string fileExt = Path.GetExtension(filePath);
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string outputFileName = $"{baseName}_{proportion}_{timestamp}{fileExt}";
            string outputFilePath = Path.Combine(directory, outputFileName);

            // 保存到新文件
            await File.WriteAllBytesAsync(outputFilePath, packedData);
            _channelState.FilePointCount = count;

            return outputFilePath;
        }


        public async Task DUCConfig(uint CardIndex, int channelNum)
        {
            await Task.Run(() =>
            {
                {
                    //宽带配置614.4 子卡里的芯片需要上位机配置
                    uint ba = _card.DacBaseAddr;
                    uint os = 5 * 4;
                    uint val = 0;
                    dotNetQTDrv.QTWriteRegister(CardIndex, ba, 0 * 4, (uint)0); //bit[2:0]选择带宽，先清零
                    ////触发脉冲周期:根据带宽计算差值倍数，寄存器0x14=插值倍数*触发段长/8，使能DUC时触发段长固定位4096
                    //// 插值倍数 就是8 ，老黄头说的
                    uint reg = 4096;
                    dotNetQTDrv.QTWriteRegister(CardIndex, ba, 0x14, reg);
                    // 寄存器0x18：
                    //触发脉冲宽度，小于脉冲周期且不为0即可
                    dotNetQTDrv.QTWriteRegister(CardIndex, ba, 0x18, 32);
                    //寄存器0x2C：
                    //触发延时，用于触发和数据对齐，应该设置成0
                    dotNetQTDrv.QTWriteRegister(CardIndex, ba, 0x2c, 0);
                    dotNetQTDrv.LDSetParam(CardIndex, Comm.CMD_MB_SET_IF, 2, (UInt32)614400000, 0, 0xFFFF);//修改DAC输出中心频率
                    dotNetQTDrv.QTWriteRegister(CardIndex, ba, 0 * 4, (uint)0);
                    //dotNetQTDrv.QTWriteRegister(CardIndex, ba, 0x14, reg);
                    //// 寄存器0x18：
                    ////触发脉冲宽度，小于脉冲周期且不为0即可
                    //dotNetQTDrv.QTWriteRegister(CardIndex, ba, 0x18, 32);
                    ////寄存器0x2C：
                    ////触发延时，用于触发和数据对齐，应该设置成0
                    //dotNetQTDrv.QTWriteRegister(CardIndex, ba, 0x2c, 0);
                    //dotNetQTDrv.QTWriteRegister(CardIndex, ba, 0 * 4, (uint)1); //偏移地址0,bit[3：0]:0~5依次对应6种带宽，0为500M带宽，需要用强制触发，其它的用用户触发
                }
            }
            );
        }

        private async Task<byte[]> ReadBigFile(string filePath, int readByteLength)
        {
            byte[] buffer = new byte[readByteLength];
            await Task.Run(() =>
            {
                FileStream stream = new FileStream(filePath, FileMode.Open);
                stream.Read(buffer, 0, readByteLength);
                stream.Close();
                stream.Dispose();
            });
            return buffer;
            //string str = Encoding.Default.GetString(buffer) //如果需要转换成编码字符串的话
        }

        public async Task<byte[][]> ReadBigFileWB(string filePath, UInt64 readByteLength)
        {
            int bytecount = 0;
            //dotNetQTDrv.QTGetRegs_i32(unBoardIndex, Regs.PerBufByteCount, ref bytecount, DmaChIdx);
            bytecount = 1 * 1024 * 1024 * 1024;
            UInt64 reqLen = readByteLength;
            long offset = 0;
            int PerLen = 0;
            int SentByte = 0;
            uint bufNum = 1;//buf数量
            uint bufNumIndex = 0;//buf序号
            byte[][] buffer;
            if (readByteLength > (uint)bytecount)
            {
                bufNum = (uint)(readByteLength / (uint)bytecount);
                if ((readByteLength % (uint)bytecount) > 0)
                {
                    bufNum = +1;
                }
                buffer = new byte[bufNum][];
                for (int i = 0; i < bufNum; i++)
                {
                    buffer[i] = new byte[bytecount];
                }
            }
            else
            {
                buffer = new byte[bufNum][];
                buffer[0] = new byte[readByteLength];
            }

            //打开文件
            FileStream stream = new FileStream(filePath, FileMode.Open);
            do
            {
                PerLen = (readByteLength > (uint)bytecount) ? bytecount : (int)readByteLength;
                //dotNetQTDrv.QTSendData(unBoardIndex, buffer, offset, (uint)PerLen, ref SentByte, 1000, DmaChIdx);
                stream.Seek(offset, SeekOrigin.Begin);
                SentByte = stream.Read(buffer[bufNumIndex], 0, PerLen);
                offset += SentByte;
                bufNumIndex++;
                reqLen -= Convert.ToUInt64(SentByte);
            } while ((reqLen > 0) && (bufNumIndex <= bufNum));

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

        public void SinglePlayWB(uint unBoardIndex, byte[][] buffer, UInt64 unLen, ref UInt64 bytes_sent, uint unTimeOut, int DmaChIdx)
        {
            int bytecount = 0;
            //dotNetQTDrv.QTGetRegs_i32(unBoardIndex, Regs.PerBufByteCount, ref bytecount, DmaChIdx);
            bytecount = 0x100000;
            UInt64 reqLen = unLen;
            uint offset = 0;
            uint PerLen = 0;
            uint SentByte = 0;
            int iRank = 1;//buffer 维度
            uint MaxOutputLenByte = 1 * 1024 * 1024 * 1024;//最大数组长度
            uint iRankIndex = 0;//维度序列号

            iRank = buffer.Rank;

            do
            {
                PerLen = (reqLen > (uint)bytecount) ? (uint)bytecount : (uint)reqLen;
                //写文件
                dotNetQTDrv.QTSendData(unBoardIndex, buffer[iRankIndex], offset, (uint)PerLen, ref SentByte, 1000, DmaChIdx);
                //dotNetQTDrv.QTSendData(unBoardIndex, buffer[iRankIndex], offset, (uint)PerLen, ref SentByte, 1000, DmaChIdx);
                offset += SentByte;
                if (offset >= MaxOutputLenByte)
                {
                    offset = 0;
                    iRankIndex = +1;
                }
                reqLen -= SentByte;
            } while ((reqLen > 0) && (iRankIndex <= iRank));
            bytes_sent = unLen - reqLen;
        }

        private async Task<uint> SinglePlayAsync(uint unBoardIndex, byte[] buffer, uint unLen, uint unTimeOut, int DmaChIdx)
        {
            uint bytesent = 0;
            SinglePlaySync(unBoardIndex, buffer, unLen, ref bytesent, unTimeOut, DmaChIdx);
            uint totalsent = +bytesent;
            return bytesent;
        }

        private async Task<UInt64> SinglePlayWBAsync(uint unBoardIndex, byte[][] buffer, UInt64 unLen, uint unTimeOut, int DmaChIdx)
        {
            UInt64 bytesent = 0;
            SinglePlayWB(unBoardIndex, buffer, unLen, ref bytesent, unTimeOut, DmaChIdx);
            UInt64 totalsent = +bytesent;
            Logger.Info(totalsent);
            return totalsent;
        }
    }
}

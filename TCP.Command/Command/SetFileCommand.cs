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
        public SetFileCommand(int abschannelNum, string commandText)
        {
            _card = PCIeCardFactory.CardParam[abschannelNum];
            _absChannelNum = abschannelNum;
            _channelNum = PCIeCardFactory.ConvertChannelNumber(abschannelNum);
            _commandText = commandText;
            _channelState = _card.ChannelStates[_channelNum];
            _cts = _channelState.singleRunCts;
            EnTrig = false;
        }


        public async Task Cancels()
        {
            _channelState.IsRunning = false;

            Logger.Info("等待1s");
            await Task.Delay(1000);

            Logger.Info("等待结束");
        }

        public void Cancel()
        {
            Logger.Info("临时cancel");
        }

        public async Task ExecuteAsync()
        {
            if (!_channelState.IsFirstRun)
            {
                await StopPlay();
            }
            var filePath = ParseCommandForValue(_commandText);
            //if (!_card.HasDac)
            //{
                await ConfigDacWorks(_card.unBoardIndex, filePath);
        //}
            if (_card.ChannelStates[_channelNum].Srate == 0)
            {
                _card.ChannelStates[_channelNum].Srate = 256000;
            }
            await OnSrateChanged();
            await Task.Delay(1000);
            Logger.Info("1s");
            Int64 TotalSent = 0;
            EnTrig = false;
            long FileSizeB = 0;
            uint SentByte = 0;
            string OffLineFile = _card.FilePath[_channelNum];
            var prop = await CaculateProportionFromFreqence();
            Logger.Info("prop is " + prop);
            string newPath = await ReadAndProcessBinFileAsync(OffLineFile,prop);
            PCIeCardFactory.NewFilePathList.Add(newPath);
            _channelState.IsRunning = true;
            await InitChan();
            _channelState.IsFirstRun = false;
            
            try
            {
                FileInfo fileInfo = new FileInfo(newPath);
                if (fileInfo != null && fileInfo.Exists)
                {
                    FileSizeB = fileInfo.Length;
                    byte[] buffer = await ReadBigFile(newPath, (int)FileSizeB);
                    if (_channelState.PlaybackMethod == PlaybackMethodType.SIN)
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
                    else
                    {
                        
                        while (_channelState.IsRunning)
                        {
                            SentByte = await SinglePlayAsync(_card.unBoardIndex, buffer, (uint)FileSizeB, 1000, _channelNum);
                            if (EnTrig == false)
                            {
                                UInt32 val = 1 + (UInt32)(1 << (_channelNum + 3));
                                dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, 0x800e0000, (uint)1 * 4, val);
                                EnTrig = true;
                                Logger.Info("开发 使能");
                            }
                            TotalSent += SentByte;
                            Logger.Info(TotalSent);
                            dotNetQTDrv.QTSetRegs_i64(_card.unBoardIndex, Regs.RepTotalMB, TotalSent, _channelNum);
                            //var total = await ReportFileInfo();
                            //Logger.Info(total);
                        }
                    }
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

        private async Task<double> MeasureAndCalculateTransferRateAsync(byte[] buffer, uint unBoardIndex, int channelNum)
        {
            Stopwatch stopwatch = new Stopwatch();
            uint sentByte = 0;

            // 开始计时
            stopwatch.Start();

            sentByte = await SinglePlayAsync(unBoardIndex, buffer, (uint)buffer.Length, 1000, channelNum);

            // 停止计时
            stopwatch.Stop();

            double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
            double transferRate = sentByte / elapsedSeconds; // 字节/秒

            // 返回字节/微秒的速率
            return transferRate / 1_000_000;
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
                prop = 11000;
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
            await Cancels();

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

        }
        private async Task ConfigDacWorks(uint CardIndex, string SingleFilePath)
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
            Logger.Info("CC重置回放通道");
            dotNetQTDrv.LDReplayInit(CardIndex, _channelNum);
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

            if (true)
                //开启循环回放单个或者文件列表功能
                _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.LoopMode, 1, _channelNum), "开启循环回放功能");
            else
                _card.ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(CardIndex, Regs.LoopMode, 0, _channelNum), "关闭循环回放功能");


            Int64 trigdelay = (Int64)2 * (_card.SampleRate / 8 * 2);
            _card.ld_ChkRT(dotNetQTDrv.QTStart(CardIndex, Comm.QTFM_COMMON_TRANSMIT_DIRECTION_PC2BRD, 1, 2000), "开启DMA");
            if (_card.EnALG)
                await DUCConfig(CardIndex, _channelNum);
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

        static async Task<string> ReadAndProcessBinFileAsync(string filePath, int proportion = 11000, bool insertZero = false)
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
                    BitConverter.GetBytes((short)0).CopyTo(packedData, i * 4 + 2);
                });
            }
            else
            {
                packedData = new byte[scaledSamples.Length * 2];
                Parallel.For(0, scaledSamples.Length, i =>
                {
                    BitConverter.GetBytes(scaledSamples[i]).CopyTo(packedData, i * 2);
                });
            }

            // 生成新的文件名并保存在原目录中
            string directory = Path.GetDirectoryName(filePath);
            string baseName = Path.GetFileNameWithoutExtension(filePath);
            string fileExt = Path.GetExtension(filePath);
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string outputFileName = $"{baseName}_{proportion}_{timestamp}{fileExt}";
            string outputFilePath = Path.Combine(directory, outputFileName);

            // 保存到新文件
            await File.WriteAllBytesAsync(outputFilePath, packedData);

            return outputFilePath;
        }


        public async Task DUCConfig(uint CardIndex, int channelNum)
        {
            await Task.Run(() =>
            {
                if (CardIndex != 0)
                {
                    //窄带逻辑，窄带不需要配置614.4这个问题，由逻辑 老黄头 控制
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
                        soft_rst_n = 0;
                        val = dout_ctrl + (soft_rst_n << 1) + (valid_out[0] << 3) + (valid_out[1] << 4) + (valid_out[2] << 5) + (valid_out[3] << 6) + (format_out << 7);
                        dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, ba, 1 * 4, val);

                        //通道1上变频参数
                        //DDS参数暂未使用，不设置
                        //配置reg10~11
                        val = format_dac + (format_in << 4) + (format_spct << 8);
                        dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, ba, 10 * 4, val);

                        //FIXME!!!通道2~4上变频参数,暂时没用，以后再补充

                        //for (int i = 0; i < 32; i++)
                        //{
                        //  dotNetQTDrv.QTWriteRegister(CardIndex, DacBaseAddr, (uint)i * 4, DacUsrReg[i]);
                        //}
                    }
                }
                else
                {
                    //宽带配置614.4 子卡里的芯片需要上位机配置
                    uint ba = _card.DacBaseAddr;
                    uint os = 5 * 4;
                    uint val = 0;
                    dotNetQTDrv.QTWriteRegister(CardIndex, ba, 0 * 4, (uint)0); //bit[2:0]选择带宽，先清零
                    ////触发脉冲周期:根据带宽计算差值倍数，寄存器0x14=插值倍数*触发段长/8，使能DUC时触发段长固定位4096
                    //// 插值倍数 就是8 ，老黄头说的
                    uint reg = 4096;
                    //dotNetQTDrv.QTWriteRegister(CardIndex, ba, 0x14, reg);
                    //// 寄存器0x18：
                    ////触发脉冲宽度，小于脉冲周期且不为0即可
                    //dotNetQTDrv.QTWriteRegister(CardIndex, ba, 0x18, 32);
                    ////寄存器0x2C：
                    ////触发延时，用于触发和数据对齐，应该设置成0
                    //dotNetQTDrv.QTWriteRegister(CardIndex, ba, 0x2c, 0);
                    dotNetQTDrv.LDSetParam(CardIndex, Comm.CMD_MB_SET_IF, 2, (UInt32)614400000, 0, 0xFFFF);//修改DAC输出中心频率
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

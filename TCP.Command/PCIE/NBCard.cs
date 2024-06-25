﻿using Lookdata;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCP.Command.PCIE
{
    public class NBCard : PcieCard
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public NBCard(uint cardIndex,int numberofcards) : base(cardIndex,4, numberofcards)
        {
        }

        public override int Initialize(uint unCardIdx)
        {
            double RangeVolt = Comm.QTFM_INPUT_RANGE_1;           // 输入档位选择，取值QTFM_INPUT_RANGE_1~4 对应输入档位由小到大
            double OffsetVolt = 0;                                           // 偏置设置，取值范围[-full-calce,+full-scale],单位uV
                                                                             //////////////////////////////////////////////////////////////////////////
            ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(unCardIdx, Regs.uiErrCode, 0), "错误清零");
            ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(unCardIdx, Regs.EnableStreaming, 1), "设置流盘标志位");
            ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(unCardIdx, Regs.EnableVfifo, 0), "禁止虚拟FIFO标志位");
            ld_ChkRT(dotNetQTDrv.QTResetBoard(unCardIdx), "复位板卡");
            var temp_number = this.ProductNumber;
            ld_ChkRT(dotNetQTDrv.QTGetRegs_i32(unCardIdx, Regs.product_number, ref temp_number), "读取产品编码");
            ProductNumber = temp_number;
            
            SampleRate = 512000000;
            uint refdiv = 10;
            RefClkMode = Comm.QTFM_COMMON_CLOCK_REF_MODE_1;
            Fref = 10000000;
            refdiv = 1;
            ADCClkMode = Comm.QTFM_COMMON_ADC_CLOCK_MODE_1;
            string ext_clk = Convert.ToString(SampleRate);
            Logger.Info("当前输入的采样时钟频率为" + ext_clk + "Hz");
            {
                ld_ChkRT(dotNetQTDrv.QTSetRegs_i32(unCardIdx, Regs.SRate, (int)SampleRate), string.Format("设置采样率{0}", SampleRate));
                if (dotNetQTDrv.QTClockSet(unCardIdx, (uint)Fref, refdiv, 0, Comm.QTFM_COMMON_CLOCK_VCO_MODE_0, RefClkMode, ADCClkMode, 1) != Error.RES_SUCCESS)
                {
                    Logger.Error("不支持的采样率，当前 " + Convert.ToString(SampleRate / 1000000) + " MHz");
                    return -1;
                }
                else
                {
                    if (EnDACWork > 0)
                    {
                        if (ChkFreq(unCardIdx, SampleRate, 1) == -1)//检查DAC时钟频率
                            return -1;
                    }
                }
            }
            var temp_bforceiodelay = 0;
            ld_ChkRT(dotNetQTDrv.QTGetRegs_i32(unCardIdx, Regs.bForceIOdelay, ref temp_bforceiodelay), "读取IOdelay标志");
            bForceIOdelay = temp_bforceiodelay;
            //----Setup AFE
            if (ProductNumber != 0x1125)
            {
                uint ModeFlag = 0;
                if (bForceIOdelay == 1)
                    ModeFlag = 0;
                else
                    ModeFlag = 256;
                ModeFlag = 1 << 8;
                ld_ChkRT(dotNetQTDrv.QTAdcModeSet(unCardIdx, 0, ModeFlag, 0), "设置ADC");
            }
            //----Setup Input range and offset
            int couple_type = 0;
            ld_ChkRT(dotNetQTDrv.QTGetRegs_i32(unCardIdx, Regs.couple_type, ref couple_type), "读取耦合方式");
            if (couple_type == 0xDC)
            {
                //----Set analog input range first then offset
                ld_ChkRT(dotNetQTDrv.QTChannelRangeSet(unCardIdx, -1, RangeVolt), string.Format("设置量程 {0}", Convert.ToInt32(RangeVolt)));
                //----Set analog offset
                ld_ChkRT(dotNetQTDrv.QTChannelOffsetSet(unCardIdx, -1, OffsetVolt), "设置偏置");
            }
            return 0;
        }

        public override async Task ExecuteSingleRunAsync(int channelNo)
        {
            ChannelStates[channelNo].singleRunCts = new CancellationTokenSource();
            Logger.Info($"Channel {channelNo} ({DeviceName}): Starting single run operation.");
            try
            {
                await Task.Delay(10000, ChannelStates[channelNo].singleRunCts.Token); // Simulate 10 seconds operation
            }
            catch (TaskCanceledException)
            {
                Logger.Info($"Channel {channelNo} ({DeviceName}): Single run operation cancelled.");
            }
            finally
            {
                OnOperationCompleted(channelNo);
            }
            Logger.Info($"Channel {channelNo} ({DeviceName}): Single run operation completed.");
        }

        public async Task ExecuteLoopRunAsync(int channelNo)
        {
            if (ChannelStates[channelNo].singleRunCts != null && !ChannelStates[channelNo].singleRunCts.IsCancellationRequested)
            {
                Logger.Info($"Channel {channelNo} ({DeviceName}): Waiting for single run to complete.");
                await Task.Delay(1000); // Wait for single run to complete
            }

            ChannelStates[channelNo].loopRunCts = new CancellationTokenSource();
            try
            {
                while (!ChannelStates[channelNo].loopRunCts.Token.IsCancellationRequested)
                {
                    Logger.Info($"Channel {channelNo} ({DeviceName}): Starting loop run operation.");
                    await Task.Delay(10000, ChannelStates[channelNo].loopRunCts.Token); // Simulate 10 seconds operation
                }
            }
            catch (TaskCanceledException)
            {
                Logger.Info($"Channel {channelNo} ({DeviceName}): Loop run operation cancelled.");
            }
            finally
            {
                OnOperationCompleted(channelNo);
            }
        }

        public async Task MonitorHardwareAsync(Func<bool> cancelCondition, int channelNo)
        {
            
            Logger.Info($"Channel {channelNo} ({DeviceName}): Starting hardware monitoring.");
            try
            {
                while (!ChannelStates[channelNo].monitorCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(1000); // Check hardware state every second
                    if (cancelCondition())
                    {
                        Logger.Info($"Channel {channelNo} ({DeviceName}): Cancel condition met. Cancelling operations.");
                        ChannelStates[channelNo].singleRunCts?.Cancel();
                        ChannelStates[channelNo].loopRunCts?.Cancel();
                        break;
                    }
                }
            }
            catch (TaskCanceledException)
            {
                Logger.Info($"Channel {channelNo} ({DeviceName}): Hardware monitoring cancelled.");
            }
            finally
            {
                OnOperationCompleted(channelNo);
            }
            Logger.Info($"Channel {channelNo} ({DeviceName}): Hardware monitoring stopped.");
        }

        public override void OnOperationCompleted(int channelNo)
        {
            // Place code here to restore hardware state or perform other cleanup operations
            Logger.Info($"Channel {channelNo} ({DeviceName}): Performing cleanup operations.");
        }

        public override void CancelOperations(int channelNo)
        {
            ChannelStates[channelNo].singleRunCts?.Cancel();
            ChannelStates[channelNo].loopRunCts?.Cancel();
            ChannelStates[channelNo].monitorCts?.Cancel();
        }

        public override void StopOperation()
        {
            throw new NotImplementedException();
        }
    }
}

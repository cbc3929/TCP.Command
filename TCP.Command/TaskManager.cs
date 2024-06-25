using Lookdata;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public class BackgroundTaskManager
{
    private ConcurrentDictionary<int, Task> backgroundTasks = new ConcurrentDictionary<int, Task>();
    private CancellationTokenSource cts = new CancellationTokenSource();

    public void StartAllTasks(int numberOfBoards)
    {
        for (int i = 0; i < numberOfBoards; i++)
        {
            int boardIndex = i;
            backgroundTasks[boardIndex] = Task.Run(() => DoWork_cj(boardIndex, cts.Token));
        }
    }

    public void StopAllTasks()
    {
        cts.Cancel();
    }

    public async Task WaitAllTasksAsync()
    {
        await Task.WhenAll(backgroundTasks.Values);
    }

    private async Task DoWork_cj(int boardIndex, CancellationToken token)
    {
        //uint CurCardIndex = (uint)boardIndex;
        //float[] bps = new float[NoDmaCh];
        //long t2;

        //if (EnADCWork > 0)
        //    Console.WriteLine("正在采集...");
        //if (EnDACWork > 0)
        //    Console.WriteLine("正在回放...");
        //if ((EnADCWork > 0) && (EnDACWork > 0))
        //    Console.WriteLine("同时收发...");

        //int PingPangBufIsOverFlow = 0, RepPingPangBufIsOverFlow = 0;
        //int[] TotalMB = new int[NoDmaCh];
        //UInt32 BA = 0x80010000;
        //UInt32 OS = 0x7c;
        //UInt32 rdval = 0;
        //bool EnTrig = false;
        //await Task.Delay(2000);

        //while (!token.IsCancellationRequested)
        //{
        //    if (EnADCWork > 0)
        //    {
        //        await Task.Delay(1000, token);
        //        for (int i = 0; i < NoDmaCh; i++)
        //            dotNetQTDrv.QTGetRegs_i32(CurCardIndex, Regs.TotalMB, ref TotalMB[i], i);
        //        // 采集模式下，在此添加读写寄存器的操作
        //    }

        //    if (EnDACWork > 0)
        //    {
        //        await Task.Delay(1000, token);
        //        for (int i = 0; i < NoDmaCh; i++)
        //            dotNetQTDrv.QTGetRegs_i32(CurCardIndex, Regs.RepTotalMB, ref TotalMB[i], i);

        //        // 采集模式下，在此添加读写寄存器的操作
        //        if ((TotalMB[0] >= 256 /*|| TotalMB[1] >= 256*/) && (EnTrig == false))
        //        {
        //            if (EnALG && (EnSim == false))
        //                dotNetQTDrv.QTWriteRegister(CurCardIndex, 0x800e0000, (uint)1 * 4, 0x9);
        //            EnTrig = true;
        //        }
        //    }

        //    // 获得磁盘剩余容量等操作
        //}

        //// 停止工作时，等待所有写文件任务完成
        //for (int i = 0; i < NoDmaCh; i++)
        //{
        //    while (!token.IsCancellationRequested)
        //        await Task.Delay(100, token);
        //}
    }

    private async Task DoWork_writefile(FormalParams mywork_params, CancellationToken token)
    {
        //int CurWorkMode = 0;//0:当前后台负责采集；1：当前后台负责回放
        //uint CurCardIndex = (uint)mywork_params.CardIndex;
        //int DmaChIndex = mywork_params.DmaChIndex;
        //if (DmaChIndex == 0)
        //    CurWorkMode = 0;
        //else
        //    CurWorkMode = 1;
        //#region 时长问题
        ////    if (CurWorkMode == 0 && radioButton3.Checked)//设定时长
        ////    {
        ////        if (checkBox1.Checked)//预约开始
        ////        {
        ////            int CountDownTime = Convert.ToInt32(this.numericUpDown6.Value * 3600 + this.numericUpDown7.Value * 60 + this.numericUpDown8.Value);
        ////            var sw2 = Stopwatch.StartNew();
        ////            do
        ////            {
        ////                if (sw2.Elapsed.Seconds >= CountDownTime)
        ////                {
        ////                    isTimeSchStart = true;
        ////                    Console.WriteLine("开始采集");
        ////                    break;
        ////                }
        ////                else
        ////                {
        ////                    await Task.Delay(100, token);
        ////                    Console.WriteLine("(" + Convert.ToString(CountDownTime - sw2.Elapsed.Seconds) + ")");
        ////                }
        ////            } while (!token.IsCancellationRequested);
        ////            sw2.Stop();
        ////        }
        ////        else
        ////        {
        ////            isTimeSchStart = true;
        ////        }
        ////        if (!isTimeSchStart)
        ////            return;
        ////    //}
        //#endregion
        //mre.Set();

        //if (token.IsCancellationRequested)
        //    return;

        //if (!token.IsCancellationRequested)
        //{
            
        //    if (EnDACWork > 0)
        //    {
        //        if (!EnSim)
        //        {
        //            mutex.WaitOne();
        //            dotNetQTDrv.LDSetParam(CurCardIndex, Comm.CMD_MB_ENABLE_REPLAY_MODE, 1, 0, 0, 0xFFFFFFFF);
        //            dotNetQTDrv.LDReplayData(CurCardIndex, DmaChIndex);
        //            mutex.ReleaseMutex();
        //        }
        //        else
        //        {
        //            while (task_SignalSim[CurCardIndex, DmaChIndex].Status == TaskStatus.Running) ;
        //            dotNetQTDrv.QTSetRegs_i32(unBoardIndex, Regs.RepKeepRun, 1, DmaChIndex);
        //            await DoWork_SignalSim(mywork_params, token);
        //        }
        //    }
        //    dotNetQTDrv.RegisterCallBackPCIeUserEvent(unBoardIndex, CallBackUserEvent[CurCardIndex], 1);
        //    dotNetQTDrv.RegisterCallBackPCIeUserEvent(unBoardIndex, CallBackUserEvent[CurCardIndex], 2);
        //    if (!sw.IsRunning)
        //    {
        //        sw.Start();
        //    }
        //}

        //if (!token.IsCancellationRequested)
        //{
        //    if (EnADCWork > 0)
        //    {
        //        dotNetQTDrv.QTWriteFileDone(CurCardIndex, DmaChIndex);
        //    }
        //    if (EnDACWork > 0)
        //    {
        //        string RepFile = FileLocation + "\\" + "sin_data.bin";
        //        byte[] byte_RepFile = new byte[(int)255];
        //        byte_RepFile = System.Text.Encoding.Default.GetBytes(RepFile);

        //        while (!token.IsCancellationRequested)
        //        {
        //            await Task.Delay(100, token);
        //            dotNetQTDrv.QTGetRegs_i32(CurCardIndex, Regs.RepKeepRun, ref RepKeepRun[DmaChIndex], DmaChIndex);
        //            if (RepKeepRun[DmaChIndex] == 0)
        //            {
        //                break;
        //            }
        //        }
        //    }
        //}
    }

    private async Task DoWork_SignalSim(FormalParams mywork_params, CancellationToken token)
    {
        //    uint CurCardIndex = (uint)mywork_params.CardIndex;
        //    int DmaChIndex = mywork_params.DmaChIndex;
        //    Int64 TotalSent = 0;
        //    bool EnTrig = false;

        //    long FileSizeB = 0;
        //    uint SentByte = 0;

        //    try
        //    {
        //        var fileInfo = new System.IO.FileInfo(OfflineFileName);
        //        if (fileInfo.Exists)
        //        {
        //            FileSizeB = fileInfo.Length;
        //            byte[] buffer = ReadBigFile(OfflineFileName, (int)FileSizeB);
        //            while (!token.IsCancellationRequested)
        //            {
        //                SinglePlay(unBoardIndex, buffer, (uint)FileSizeB, ref SentByte, 1000, DmaChIndex);
        //                if (!EnTrig)
        //                {
        //                    UInt32 val = 1 + (UInt32)(1 << (DmaChIndex + 3));
        //                    dotNetQTDrv.QTWriteRegister(CurCardIndex, 0x800e0000, (uint)1 * 4, val);
        //                    EnTrig = true;
        //                }
        //                TotalSent += SentByte;
        //                dotNetQTDrv.QTSetRegs_i64(unBoardIndex, Regs.RepTotalMB, TotalSent, DmaChIndex);
        //            }
        //        }
        //    }
        //    catch (Exception err)
        //    {
        //        Console.WriteLine("读取仿真文件遇到异常：" + err.Message);
        //        return;
        //    }

        //    dotNetQTDrv.QTWriteRegister(unBoardIndex, 0x800e0000, 4 * 4, 0);

        //    dotNetQTDrv.QTSetRegs_i32(unBoardIndex, Regs.RepKeepRun, 0);
        //    while (!token.IsCancellationRequested)
        //        await Task.Delay(100, token);
    }
    public class FormalParams
    {
        public int CardIndex;//板卡编号，从0开始
        public int DmaChIndex;//DMA通道编号，从0开始
    }
}

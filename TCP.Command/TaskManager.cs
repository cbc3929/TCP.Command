using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCP.Command
{
    using Lookdata;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using TCP.Command.PCIE;

    public class TaskManager
    {
        private CancellationTokenSource cts;
        private List<Task> backgroundTasks;
        private ManualResetEvent mre = new ManualResetEvent(false);

        public void InitializeTasks()
        {
            cts = new CancellationTokenSource();
            backgroundTasks = new List<Task>();

            backgroundTasks.Add(Task.Run(() => TaskCjWork(cts.Token)));

            for (int i = 0; i < Dev_Config.NoDmaCh; i++)
            {
                int index = i; // To avoid closure issue in loop
                backgroundTasks.Add(Task.Run(() => TaskWriteFileWork(index, cts.Token)));
            }

            backgroundTasks.Add(Task.Run(() => TaskSignalSimWork(cts.Token)));
        }

        public void StartTasks()
        {
            InitializeTasks();
        }

        public void CancelTasks()
        {
            cts.Cancel();
            Task.WaitAll(backgroundTasks.ToArray());
        }

        private async Task TaskCjWork(CancellationToken token)
        {
            while (!mre.WaitOne(100) && (!token.IsCancellationRequested)) ;

            if ((Dev_Config.WorkSate == 1) || (Dev_Config.WorkSate == 3))
                ReportProgress(1, "正在采集...");
            else
                ReportProgress(1, "正在回放...");

            int[] TotalMB = new int[Dev_Config.NoDmaCh];
            float[] bps = new float[Dev_Config.NoDmaCh];
            int[] len = new int[Dev_Config.NoDmaCh];

            UInt32 BA = 0x80010000;
            UInt32 OS = 0x7c;
            UInt32 rdval = 0;
            bool EnTrig = false;

            await Task.Delay(2000);

            while (!token.IsCancellationRequested)
            {
                if ((Dev_Config.WorkSate == 1) || (Dev_Config.WorkSate == 3))
                {
                    Dev_Config.ReadRegisterValue(Dev_Config.unBoardIndex, ref BA, ref OS, ref rdval);
                    await Task.Delay(1000);
                    for (int i = 0; i < Dev_Config.NoDmaCh; i++)
                    {
                        dotNetQTDrv.QTGetRegs_i32(Dev_Config.unBoardIndex, Regs.TotalMB, ref TotalMB[i], i);
                    }
                }
                else
                {
                    await Task.Delay(1000);
                    BA = 0x80030000;
                    OS = 0x7c;
                    rdval = 0;
                    dotNetQTDrv.QTReadRegister(Dev_Config.unBoardIndex, ref BA, ref OS, ref rdval);

                    if ((rdval & 0x2) == 0x2)
                    {
                        Dev_Config.errov = 1;
                        ReportProgress(1, "板卡缓存读空！");
                        cts.Cancel();
                        break;
                    }

                    for (int i = 0; i < Dev_Config.NoDmaCh; i++)
                    {
                        dotNetQTDrv.QTGetRegs_i32(Dev_Config.unBoardIndex, Regs.RepTotalMB, ref TotalMB[i], i);
                    }

                    if ((TotalMB[0] > 256 || TotalMB[1] > 256) && !EnTrig)
                    {
                        uint slv_reg_r2_1 = Dev_Config.vald_out_1 << 3 | Dev_Config.vald_out_2 << 4 | Dev_Config.dout_ctrl;
                        dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 1 * 4, slv_reg_r2_1);
                        EnTrig = true;
                    }
                }

                string volume = Dev_Config.FileCatalog.Substring(0, Dev_Config.FileCatalog.IndexOf(':'));
                long freespace = Dev_Config.GetHardDiskSpace(volume);
                long t2 = Dev_Config.sw.ElapsedMilliseconds;
                for (int i = 0; i < Dev_Config.NoDmaCh; i++)
                {
                    len[i] = TotalMB[i] / 1024;
                    bps[i] = t2 > 0 ? (float)TotalMB[i] * 1000 / t2 : 0;
                }

                ReportProgress(2, $"{len[0]}/{len[1]}");
                ReportProgress(3, $"{(Int64)bps[0]}/{(Int64)bps[1]}");
                ReportProgress(6, (freespace >> 10).ToString());
                ReportProgress(7, "0x0");
            }

            // 退出前读取溢出状态
            await HandleOverflowState(token);
            Dev_Config.sw.Stop();
        }

        private async Task TaskWriteFileWork(int DmaChIndex, CancellationToken token)
        {
            mre.Set();
            if (token.IsCancellationRequested) return;

            if (!token.IsCancellationRequested)
            {
                if ((Dev_Config.WorkSate == 1) || (Dev_Config.WorkSate == 3))
                {
                    if (Dev_Config.dout_sel != 0)
                    {
                        dotNetQTDrv.QTStoreData(Dev_Config.unBoardIndex, ref Dev_Config.usrname[0], 0, DmaChIndex);
                        UInt32 slv_reg_r2_1 = 0;
                        Dev_Config.UPdataRegisterValue(Dev_Config.unBoardIndex, 0x800F0000, 1 * 4, slv_reg_r2_1 | 0x0C);
                    }
                    else
                    {
                        dotNetQTDrv.QTStoreData(Dev_Config.unBoardIndex, ref Dev_Config.usrname[0], 0);
                    }
                }
                else
                {
                    if (!Dev_Config.SignalFileState)
                        dotNetQTDrv.LDReplayData(Dev_Config.unBoardIndex, DmaChIndex);
                    else
                    {
                        await Task.Run(() => TaskSignalSimWork(token));
                        dotNetQTDrv.QTSetRegs_i32(Dev_Config.unBoardIndex, Regs.RepKeepRun, 1, DmaChIndex);
                    }
                }
                if (!Dev_Config.sw.IsRunning)
                    Dev_Config.sw.Start();
            }

            if (!token.IsCancellationRequested)
            {
                if ((Dev_Config.WorkSate == 1) || (Dev_Config.WorkSate == 3))
                    dotNetQTDrv.QTWriteFileDone(Dev_Config.unBoardIndex, DmaChIndex);
                else
                    await HandleFileReplaying(DmaChIndex, token);
            }
        }

        private async Task TaskSignalSimWork(CancellationToken token)
        {
            // Implementation of SignalSimWork
            // Placeholder: Implement your logic here.
            await Task.Delay(1000, token); // Simulate some work
        }

        private async Task HandleOverflowState(CancellationToken token)
        {
            UInt32 BA = 0x80010000;
            UInt32 OS = 0x7c;
            UInt32 rdval = 0;

            if ((Dev_Config.WorkSate == 1) || (Dev_Config.WorkSate == 3))
            {
                Dev_Config.ReadRegisterValue(Dev_Config.unBoardIndex, ref BA, ref OS, ref rdval);
                int PingPangBufIsOverFlow = 0;
                dotNetQTDrv.QTGetRegs_i32(Dev_Config.unBoardIndex, Regs.PingPangBufIsOverFlow, ref PingPangBufIsOverFlow);
                if ((rdval & 0x2) == 0x2)
                {
                    Dev_Config.errov = 1;
                    ReportProgress(1, "板卡乒乓缓存数据溢出！");
                    //ErrorProcess(Dev_Config.WorkSate);
                    cts.Cancel();
                }
            }
            else
            {
                BA = 0x80030000;
                OS = 0x7c;
                rdval = 0;
                Dev_Config.ReadRegisterValue(Dev_Config.unBoardIndex, ref BA, ref OS, ref rdval);
                if ((rdval & 0x2) == 0x2)
                {
                    Dev_Config.errov = 1;
                    ReportProgress(1, "板卡缓存读空！");
                    cts.Cancel();
                }
            }
            await Task.Delay(0); // Ensure method is async
        }

        private async Task HandleFileReplaying(int DmaChIndex, CancellationToken token)
        {
            string RepFile = Dev_Config.FileCatalog + "\\" + "sin_data.bin";
            byte[] byte_RepFile = System.Text.Encoding.Default.GetBytes(RepFile);

            while (!token.IsCancellationRequested)
            {
                await Task.Delay(100);
                dotNetQTDrv.QTGetRegs_i32(Dev_Config.unBoardIndex, Regs.RepKeepRun, ref Dev_Config.RepKeepRun[DmaChIndex], DmaChIndex);
            }
        }

        private void ReportProgress(int progressType, string message)
        {
            // Implement the logic to handle progress reporting here
            // For example, logging the message or sending it to a monitoring service
            Console.WriteLine($"Progress {progressType}: {message}");
        }
    }

}

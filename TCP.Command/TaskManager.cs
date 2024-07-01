using Lookdata;
using NLog;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TCP.Command.Interface;
using TCP.Command.PCIE;

public class TaskManager
{
    private Dictionary<int, List<ICommand>> _commands;
    private Dictionary<int, Task> _runningTasks;
    private Dictionary<int, Action<string>> _callbacks;
    private PcieCard? _card;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly Lazy<TaskManager> instance = new Lazy<TaskManager>(() => new TaskManager());
    public static TaskManager Instance => instance.Value;

    private TaskManager()
    {
        _commands = new Dictionary<int, List<ICommand>>();
        _runningTasks = new Dictionary<int, Task>();
        _callbacks = new Dictionary<int, Action<string>>();
    }

    public static void RegisterCardCommand()
    {


    }

    public void RegisterCallback(int channelNo, Action<string> callback)
    {
        if (!_callbacks.ContainsKey(channelNo))
        {
            _callbacks[channelNo] = callback;
        }
    }

    //public async Task StartCommandAsync(int channelNo, ICommand command)
    //{
    //    if (!_commands.ContainsKey(channelNo))
    //    {
    //        _commands[channelNo] = new List<ICommand>();
    //    }

    //    _commands[channelNo].Add(command);
    //    var task = command.ExecuteAsync(this, channelNo);
    //    _runningTasks[channelNo] = task;

    //    // Handle progress reporting and completion callback
    //    await task.ContinueWith(t =>
    //    {
    //        _runningTasks.Remove(channelNo);
    //        _commands[channelNo].Remove(command);

    //        if (_callbacks.ContainsKey(channelNo))
    //        {
    //            _callbacks[channelNo].Invoke("Task completed or cancelled.");
    //        }
    //    });
    //}

    public void CancelCommand(int channelNo)
    {
        if (_commands.ContainsKey(channelNo))
        {
            foreach (var command in _commands[channelNo])
            {
                command.Cancel();
            }

            _commands[channelNo].Clear();
        }
    }

    public bool IsTaskRunning(int channelNo)
    {
        return _runningTasks.ContainsKey(channelNo);
    }

    //public async Task ExecuteSingleRunAsync(int channelNo, CancellationToken token)
    //{
    //    Int64 TotalSent = 0;
    //    bool EnTrig = false;
    //    long FileSizeB = 0;
    //    uint SentByte = 0;
    //    string OffLineFile = _card.FilePath[channelNo];
    //    try
    //    {
    //        FileInfo fileInfo = new FileInfo(OffLineFile);
    //        if (fileInfo != null && fileInfo.Exists)
    //        {
    //            FileSizeB = fileInfo.Length;
    //            byte[] buffer = ReadBigFile(OffLineFile, (int)FileSizeB);
    //            while (!token.IsCancellationRequested)
    //            {
    //                SinglePlay(_card.unBoardIndex, buffer, (uint)FileSizeB, ref SentByte, 1000, channelNo);
    //                if (!EnTrig)
    //                {
    //                    UInt32 val = 1 + (UInt32)(1 << (channelNo + 3));
    //                    dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, 0x800e0000, (uint)1 * 4, val);
    //                    EnTrig = true;
    //                }
    //                TotalSent += SentByte;
    //                dotNetQTDrv.QTSetRegs_i64(_card.unBoardIndex, Regs.RepTotalMB, TotalSent, channelNo);
    //            }
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        Logger.Info(ex);
    //        return;
    //    }
    //    finally
    //    {
    //        dotNetQTDrv.QTWriteRegister(_card.unBoardIndex, 0x800e0000, 4 * 4, 0);
    //        OnOperationCompleted(channelNo);
    //    }
    //}

    public async Task ExecuteLoopRunAsync(int channelNo, CancellationToken token)
    {
        if (_card.ChannelStates[channelNo].singleRunCts != null && !_card.ChannelStates[channelNo].singleRunCts.IsCancellationRequested)
        {
            Logger.Info($"Channel {channelNo} ({_card.DeviceName}): Waiting for single run to complete.");
            await Task.Delay(1000); // Wait for single run to complete
        }

        _card.ChannelStates[channelNo].loopRunCts = new CancellationTokenSource();
        try
        {
            while (!token.IsCancellationRequested)
            {
                Logger.Info($"Channel {channelNo} ({_card.DeviceName}): Starting loop run operation.");
                await Task.Delay(10000, token); // Simulate 10 seconds operation
            }
        }
        catch (TaskCanceledException)
        {
            Logger.Info($"Channel {channelNo} ({_card.DeviceName}): Loop run operation cancelled.");
        }
        finally
        {
            OnOperationCompleted(channelNo);
        }
    }


    //public async Task MonitorHardwareAsync(int channelNo, Func<bool> cancelCondition, CancellationToken token)
    //{
    //    Logger.Info($"Channel {channelNo} ({_card.DeviceName}): Starting hardware monitoring.");
    //    try
    //    {
    //        while (!token.IsCancellationRequested)
    //        {
    //            await Task.Delay(1000); // Check hardware state every second
    //            if (cancelCondition())
    //            {
    //                Logger.Info($"Channel {channelNo} ({_card.DeviceName}): Cancel condition met. Cancelling operations.");
    //                _card.ChannelStates[channelNo].singleRunCts?.Cancel();
    //                _card.ChannelStates[channelNo].loopRunCts?.Cancel();
    //                break;
    //            }
    //        }
    //    }
    //    catch (TaskCanceledException)
    //    {
    //        Logger.Info($"Channel {channelNo} ({_card.DeviceName}): Hardware monitoring cancelled.");
    //    }
    //    finally
    //    {
    //        OnOperationCompleted(channelNo);
    //    }
    //    Logger.Info($"Channel {channelNo} ({_card.DeviceName}): Hardware monitoring stopped.");
    //}

    private void OnOperationCompleted(int channelNo)
    {
        // Place code here to restore hardware state or perform other cleanup operations
        Logger.Info($"Channel {channelNo} ({_card.DeviceName}): Performing cleanup operations.");
    }
}
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lookdata;

namespace TCP.Command.PCIE
{
    public static class Dev_Config
    {
        public static int deviceType = 1;
        public static int OnePacketLength = 32768;//单包数据长度
        //配置数据回调相关参数，影响上位机刷新速率
        public static int CallbackChannalCnt = 32;
        public static int[] ChEnMask = new int[CallbackChannalCnt];//每个通道的回调使能，1：使能回调；0：关闭回调功能
        public static int[] TimeOunUs = new int[CallbackChannalCnt];//超时回调时间，单位微秒。
        public static int[] QueueWaterLine = new int[CallbackChannalCnt];//队列深度水线，超过则回调一次
        public static int[] MaxSize = new int[CallbackChannalCnt];//定义每个队列缓存最大深度，最大不超过500。值越大，缓存数据越多

        //GPS信息
        public static DateTime GpsDate = new DateTime();
        public static string GpsPositional;

        //时间信息下设  modified by shaobiao 2020.5.2
        public static ushort Year;
        public static byte Month;
        public static UInt16 Day;
        public static byte Hour;
        public static byte Minute;
        public static byte Second;
        public static bool TimeGetFlag = false;

        public static int CutLog = 7;//计算cic截位所需的固定值

        public static int MulTemp = 1;//不同中频滤波下的乘数因子
        //每次采集任务开始的时间，作为采集路径的最后一个子文件夹  modified by shaobiao 2021.5.1
        public static string SubCatalog = DateTime.Now.ToString("yyyyMMddHHmmssfff");
        //6路Ddc和宽带的选择项 modified by shaobiao 2021.5.2
        public static int DdcSelectedIndex = 0;
        public static bool DDC_SelectedIndexChanged = false;

        //系统启动状态                              设备开始采集、回放或扫频状态
        public static bool ClockRefChanged = false, SampleRefChanged = false, isEqupRun = false;

        //底层PCIE需配置的参数项
        public static Int32 SampleRate;     //采样率
        public static Int32 ReplaySampleRate = 921600000;     //回放采样率
                                                              //采集链路存储参数
        public static bool ClockRef;    //时钟源 true 为内采样 false为外采样
        public static bool PBClockRef;  //回放链路时钟参考源

        public static uint SignalFileSize;//单包文件存储大小
        public static string FileCatalog;//文件存储路径
        public static string PrefixFileName = "cj";//存储文件前缀名
        public static string UsrFileName = "cj";   //存储文件名
        public static bool ifSaveEnable = false; //采集模式 true为存储  false 为示波器模式不存储

        //回放链路参数配置
        public static Int32 firValue;   //½FIR/CIC插值滤波
        public static double dacValue;  //DAC
        public static double CH1BW, CH2BW, WD1BW, WD2BW, ND1BW, ND2BW, moduSignal1BW, moduSignal2BW;  //中频滤波器 宽带KHz  窄带KHz  调制信号带宽KHz
        public static uint WDCIC1, WDCIC2, NDCIC1, NDCIC2, moduSignalCIC1, moduSignalCIC2;//抽取
        public static uint AD1Enable = 0;  //回放文件是否是AD1数据
        public static uint AD2Enable = 0;  //回放文件是否是AD2数据
        public static Int32 SendCnt;    //突发次数
        public static Int32 SendInterval;//突发间隔 
        public static uint SigTimType;  //回放模式//回放突发类型  0 单次 1连续 2突发
        //单文件回放文件名
        public static string SignalFileName = "";                //回放文件路径 + 名字
        //多文件回放时
        public static string[][] ReplayFiles = new string[NoDmaCh][];//回放文件路径 + 文件名字
        //回放文件列表
        public static bool SignalFileState = false;         //回放文件是否为单文件
        public static bool LoopPlayBack = false;           //循环回放标志
        public static int ReplaySignalMode;//回放文件类型  循环1 单次0       
        public static uint deleteHeader = 0;            //数据去帧头
        public static bool FileReadDown = false;        //上位机是否读完文件
        public static int playBackType = 0; //回放类型 1产生调制信号 2回放文件
        //系统参数配置(白利参数)
        public static int product_number;               //产品序列号
        public static int NOB = 16;
        public static uint SigGenType = 1;              //回放类型  0 动态仿真  1 静态文件

        public static string SaveIdexes;            //落盘存储盘阵位置
        public static int SaveSpeed;                //罗盘存储速率
        public static int SaveFileSize;             //已存储文件大小

        public static byte WorkSate = 0;                // 0  未启动  1 采集模式  2 回放模式  3 扫频模式
        public static uint unBoardIndex = 0;            // 板卡逻辑编号 取值0~N 代表有多少个板卡
        public static uint Fref = 100000000;            // 参考时钟频率,单位Hz
        public static uint RefClkMode = Comm.QTFM_COMMON_CLOCK_REF_MODE_2;                      // Change to QTFM_COMMON_CLOCK_REF_MODE_1 if external reference clock is required.
        public static uint ADCClkMode = Comm.QTFM_COMMON_ADC_CLOCK_MODE_0;                  // Change to QTFM_COMMON_ADC_CLOCK_MODE_1 if external sampling clock is required.

        public const uint NoDmaCh = 2;
        public static uint[] NotifySizeB = new uint[NoDmaCh];                                    //DMA size  这个参数需要依据Pcie的速率与采集的速率进行计算，最大为XDMA_RING_BLOCK_SIZE
        public static uint XDMA_RING_BLOCK_SIZE = 32 << 20;

        public static uint SegmentLen = 0;                                          //段长基于算法计算，具体含义未知
        public static uint PreTriggerLen = 0;                                       // 预触发
        public static uint trig_cnt = 1;
        public static uint workmode = Comm.QTFM_COMMON_BOARD_WORK_MODE_FIFO_SINGLE_LOOPBUF | Comm.QTFM_COMMON_TRIGGER_MODE_EDGE;
        //public static uint ifADC1Enable = 1;                                                                //ADC1是否使能
        //public static uint ifADC2Enable = 1;                                                                 //ADC2是否使能
        public static uint EnChCnt = 2;                                                                     //使能通道个数
        public static uint PulseUnit = 2048 * 8 / 2;                                                            //DUC模块触发一次请求的点数 为常量
        public static int TrigSrc = 0;                                                                    //0:内触发 1:外触发
        public static uint ExtTrigSrcPort = Comm.QTFM_COMMON_TRIGGER_SOURCE_EXTERNAL_5;                  //0:内触发 1:外触发
        public static Int64 trigdelay = 2;                                                              //触发延时

        public static int errov = 0;
        public static Stopwatch sw = new Stopwatch();
        public static int[] RepKeepRun = new int[NoDmaCh];
        public static byte[] usrname = new byte[(int)255];

        //发送寄存器参数配置   （参数定义待整理。。。。。。。。。。。。。。。。。。。。）
        public static uint PulseUnitCnt = 0;                        //每个文件/波形产生多少个触发脉冲

        //FPGA寄存器参数配置（建军寄存器配置）
        public static UInt32[] slv_reg_r2 = new UInt32[35];
        //寄存器0
        public static uint dout_sel = 1;                                        //‘1’：DDC数据 ‘0’：原始ADC数据
        //寄存器1
        public static uint dout_ctrl = 1;                                       //回放数据选择控制‘0’：数据不作处理直接回放；‘1’：数据经过处理回放（默认为1）
        public static uint soft_rst_n = 1;                                      //系统复位  ‘1’：复位，‘0’：不复位；每次复位，需要先置高，再置低
        public static uint dds_cfg_valid = 1;                                   //DDS配置有效	‘1’有效
        public static uint vald_out_1 = 1;                                      //回放通道1使能 ‘1’，使能输出‘0’，不使能输出
        public static uint vald_out_2 = 1;                                      //回放通道2使能  ‘1’，使能输出‘0’，不使能输出
        //回放1DDS控制 寄存器2-5
        //寄存器2
        public static uint DDS0_POFF1 = 0;                                  //中频DDS0的相位偏移量POFF  [31:16]
        public static uint DDS0_PINC1 = 0;                                  //中频DDS0的相位增长量PINC(和中心频率相关)  [15:0]
        //寄存器3
        public static uint DDS1_POFF1 = 0;                                  //中频DDS1的相位偏移量POFF  [31:16]
        public static uint DDS1_PINC1 = 0;                                  //中频DDS1的相位增长量PINC(和中心频率相关)  [15:0]
        //寄存器4
        public static uint DDS2_POFF1 = 0;                                  //中频DDS2的相位偏移量POFF  [31:16]
        public static uint DDS2_PINC1 = 0;                                  //中频DDS2的相位增长量PINC(和中心频率相关)   [15:0]
        //寄存器5
        public static uint DDS3_POFF1 = 0;                                  //中频DDS3的相位偏移量POFF  [31:16]
        public static uint DDS3_PINC1 = 0;                                  //中频DDS3的相位增长量PINC(和中心频率相关)  [15:0]

        //回放1DDS控制 寄存器6-9
        //寄存器6
        public static uint DDS0_POFF2 = 0;                                  //中频DDS0的相位偏移量POFF  [31:16]
        public static uint DDS0_PINC2 = 0;                                  //中频DDS0的相位增长量PINC(和中心频率相关)   [15:0]
        //寄存器7
        public static uint DDS1_POFF2 = 0;                                  //中频DDS1的相位偏移量POFF  [31:16]
        public static uint DDS1_PINC2 = 0;                                  //中频DDS1的相位增长量PINC(和中心频率相关)   [15:0]
        //寄存器8
        public static uint DDS2_POFF2 = 0;                                  //中频DDS2的相位偏移量POFF  [31:16]
        public static uint DDS2_PINC2 = 0;                                  //中频DDS2的相位增长量PINC(和中心频率相关)  [15:0]
        //寄存器9
        public static uint DDS3_POFF2 = 0;                                  //中频DDS3的相位偏移量POFF  [31:16]
        public static uint DDS3_PINC2 = 0;                                  //中频DDS3的相位增长量PINC(和中心频率相关) [15:0]
        //回放1 寄存器10-11
        //寄存器10
        public static uint format_dac_1 = 0;                                    //DAC数据格式    4  [3:0]
        public static uint fromat_in_1 = 2;                                     //下发数据格式  4  [7:4]
        public static uint format_spct_1 = 0;                                   //数据顺序是否颠倒  1  [8]
        public static uint DataType_ch1 = 0;                                   //下发数据类型与采集帧头中的通道号相同 2  [15:9]
                                                                               //1:AD1  2:AD2  3:WDDC1  4:WDDC2   5:WFFT1  6:WFFT2  7:NDDC1  8:NDDC2
                                                                               //寄存器11
        public static uint duc_chan_num_1 = 0;                                  //通道编号 7   [31:24]
        public static uint duc_cic_cut_1 = 0;                                 //DUC-CIC插值后截位控制   [23:16]
                                                                              //duc_cic_num=16，建议值=7
                                                                              //duc_cic_num=32，建议值=6
                                                                              //duc_cic_num=64，建议值=5
                                                                              //duc_cic_num=128，建议值=4
                                                                              //duc_cic_num=256，建议值=3
                                                                              //duc_cic_num=512，建议值=2
                                                                              //duc_cic_num=1024，建议值=1
                                                                              //duc_cic_num=2048，建议值=0
        public static uint duc_cic_num_1 = 1;                                   //DUC-CIC插值倍数0，1，4，8…8N    [15:0]

        //回放2 寄存器12-13
        //寄存器12
        public static uint format_dac_2 = 0;                                    //DAC数据格式    4
        public static uint fromat_in_2 = 2;                                     //下发数据格式  4
        public static uint format_spct_2 = 0;                                   //数据顺序是否颠倒  1
        public static uint DataType_ch2 = 0;                                   //下发数据类型与采集帧头中的通道号相同 2
                                                                               //1:AD1  2:AD2  3:WDDC1  4:WDDC2   5:WFFT1  6:WFFT2  7:NDDC1  8:NDDC2
                                                                               //寄存器13
        public static uint duc_chan_num_2 = 0;                                  //通道编号 7   [31:24]
        public static uint duc_cic_cut_2 = 0;                                 //DUC-CIC插值后截位控制   [23:16]
                                                                              //duc_cic_num=16，建议值=7
                                                                              //duc_cic_num=32，建议值=6
                                                                              //duc_cic_num=64，建议值=5
                                                                              //duc_cic_num=128，建议值=4
                                                                              //duc_cic_num=256，建议值=3
                                                                              //duc_cic_num=512，建议值=2
                                                                              //duc_cic_num=1024，建议值=1
                                                                              //duc_cic_num=2048，建议值=0
        public static uint duc_cic_num_2 = 1;                                   //DUC-CIC插值倍数0，1，4，8…8N    [15:0]

        //寄存器14  延时、触发设置
        public static uint trig_out = 0;                                      //下发数据有效延时   [3:0]
        public static uint trig_sel = 0;                                       //触发选择   [4]
        //射频寄存器 回放1 寄存器15-16
        //寄存器15
        public static uint rf_Freq1;                                  //射频中心频点，单位为10kHz  [31:8]

        //寄存器16
        public static uint rf_Att1;                            //射频衰减，0~60dB,步进为1dB  [7:0]
        public static uint If_Att1;                            //中频衰减，0~60dB,步进为1dB  [7:0]
        public static uint rf_BW1;
        public static uint rf_Enable_1;                                   //射频配置使能（x”0001”为有效x”0000”为无效）[31:0]

        //射频寄存器 回放2 
        public static uint rf_Freq2;                                  //射频中心频点，单位为10kHz  [31:8]

        public static uint rf_Att2;                            //射频衰减，0~60dB,步进为1dB  [7:0]
        public static uint If_Att2;                            //中频衰减，0~60dB,步进为1dB  [7:0]
        public static uint rf_BW2;
        public static uint rf_Enable2;                             	  //射频配置使能（x”0001”为有效x”0000”为无效）[31:0]

        //寄存器19
        public static uint file_rept_num = 0;                                  //文件回放次数（目前没用）[31:0]
        public static uint CH_fra_Num_0 = 0;                                    //通道1数据占比
        //寄存器20
        public static uint file_trig_num = 0;                                  //文件所需触发读取次数，每次读取16*64bits=128B（目前没用）[31:0]
        public static uint CH_fra_Num_1 = 0;                                    //通道2数据占比
        //寄存器21
        public static uint once_trig_num = 0;                                  //每个文件发送的占用触发读取次数，包含空的次数。占空比=file_trig_num/once_trig_num（目前没用）[31:0]

        public static uint CH_fra_flag = 1;                                     //开始回放1，在通道数据占比后配置；停止回放0，在停止回放时配置
                                                                                //寄存器22
        public static uint file_tx_flag = 0;                                    //文件发送开始标记 每次发送文件，需要置高。 发送完成后，需要置低，可用于终止回放（目前没用）

        public static int g_iCurrentReplayFileMax = 0;      //两个通道都是用同一个文件，文件最大值通用

        public static double g_dPowerSetValue1 = 0;              //通道1界面设置值
        public static double g_dPowerSetValue2 = 0;              //通道2界面设置值

        public static long g_lPlayBackStartTime = 0;             //记录回放起始时间
        public static bool g_bTickPlayBackEnable = false;           //定时回放使能标志位

        public static long GetHardDiskSpace(string str_HardDiskName)
        {
            long totalSize = 0;
            str_HardDiskName = str_HardDiskName + ":\\";
            System.IO.DriveInfo[] drives = System.IO.DriveInfo.GetDrives();
            foreach (System.IO.DriveInfo drive in drives)
            {
                if (drive.Name.ToLower() == str_HardDiskName.ToLower())
                {
                    totalSize = drive.TotalFreeSpace >> 20;
                }
            }
            return totalSize;
        }
        #region 读写单个寄存器通用函数
        public static Mutex MutexReg = new Mutex();
        public static void UPdataRegisterValue(uint CardId, uint BaseAddr, uint Offset, uint Value)
        {
            MutexReg.WaitOne();
            dotNetQTDrv.QTWriteRegister(CardId, BaseAddr, Offset, Value);
            MutexReg.ReleaseMutex();
        }
        public static void ReadRegisterValue(uint CardId, ref uint BaseAddr, ref uint Offset, ref uint Value)
        {
            MutexReg.WaitOne();
            dotNetQTDrv.QTReadRegister(CardId, ref BaseAddr, ref Offset, ref Value);
            MutexReg.ReleaseMutex();
        }

        #endregion

        #region 下设时间信息 modified by shaobiao 2021.5.2
        public static void UPdateTimeRegisterValue()
        {
            if (!TimeGetFlag)
            {
                //MessageBox.Show("缺少时标信息");
                return;
            }
            MutexReg.WaitOne();
            //当前存在冲突
            //slv_reg_r2[19] = (uint)Day << 24 | (uint)Hour << 16 | (uint)Minute << 8 | (uint)Second;
            //dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 19 * 4, slv_reg_r2[19]);
            //slv_reg_r2[20] = (uint)Year << 8 | (uint)Month;
            //dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 20 * 4, slv_reg_r2[20]);
            MutexReg.ReleaseMutex();
        }
        #endregion

        //下设寄存器
        public static void DevRegConfig()
        {
            MutexReg.WaitOne();
            if (WorkSate == 2)//回放
            {
                // 回放数据选择控制        ‘0’：原始中频数据直接回放        ‘1’：I/Q基带数据DUC回放
                slv_reg_r2[1] = vald_out_1 << 3 | vald_out_2 << 4 | dout_ctrl;
                if (dout_ctrl == 0)
                {
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 1 * 4, slv_reg_r2[1]);
                }
                else//使能上变频
                {
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 1 * 4, 0x02);//‘1’：复位

                    UInt64 fpinc;
                    UInt64 fpoff;
                    //宽带回放DDS控制 寄存器2-5
                    //Dev_Config.ReplaySampleRate = (Int32)(double.Parse(ConfigurationManager.AppSettings["WBSampRate"]) * 1e6);     //根据不同通道下的情况直接将该值进行设置
                    Dev_Config.ReplaySampleRate = (Int32)(307.2 * 1e6);
                    fpinc = (UInt64)((1 << 16) * 4 * 230.4 * 1000000 / Dev_Config.ReplaySampleRate);
                    fpoff = fpinc / 4;
                    //确保能用16bit表示
                    DDS0_PINC1 = (uint)(fpinc % 65536);
                    DDS1_PINC1 = (uint)(fpinc % 65536);
                    DDS2_PINC1 = (uint)(fpinc % 65536);
                    DDS3_PINC1 = (uint)(fpinc % 65536);
                    DDS0_POFF1 = (uint)((0 * fpoff) % 65536);
                    DDS1_POFF1 = (uint)((1 * fpoff) % 65536);
                    DDS2_POFF1 = (uint)((2 * fpoff) % 65536);
                    DDS3_POFF1 = (uint)((3 * fpoff) % 65536);
                    slv_reg_r2[2] = DDS0_POFF1 << 16 | DDS0_PINC1;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 2 * 4, slv_reg_r2[2]);
                    slv_reg_r2[3] = DDS1_POFF1 << 16 | DDS1_PINC1;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 3 * 4, slv_reg_r2[3]);
                    slv_reg_r2[4] = DDS2_POFF1 << 16 | DDS2_PINC1;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 4 * 4, slv_reg_r2[4]);
                    slv_reg_r2[5] = DDS3_POFF1 << 16 | DDS3_PINC1;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 5 * 4, slv_reg_r2[5]);
                    //Dev_Config.ReplaySampleRate = (Int32)(double.Parse(ConfigurationManager.AppSettings["WBSampRate"]) * 1e6);
                    Dev_Config.ReplaySampleRate = (Int32)(307.2 * 1e6);     //根据不同通道下的情况直接将该值进行设置
                    //窄带回放DDS控制 寄存器6-9                    
                    //确保能用16bit表示
                    fpinc = (UInt64)((1 << 16) * 4 * (70.0 * 1000000 / Dev_Config.ReplaySampleRate));
                    fpoff = fpinc / 4;
                    DDS0_PINC2 = (uint)(fpinc % 65536);
                    DDS1_PINC2 = (uint)(fpinc % 65536);
                    DDS2_PINC2 = (uint)(fpinc % 65536);
                    DDS3_PINC2 = (uint)(fpinc % 65536);
                    DDS0_POFF2 = (uint)((0 * fpoff) % 65536);
                    DDS1_POFF2 = (uint)((1 * fpoff) % 65536);
                    DDS2_POFF2 = (uint)((2 * fpoff) % 65536);
                    DDS3_POFF2 = (uint)((3 * fpoff) % 65536);
                    slv_reg_r2[6] = DDS0_POFF2 << 16 | DDS0_PINC2;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 6 * 4, slv_reg_r2[6]);
                    slv_reg_r2[7] = DDS1_POFF2 << 16 | DDS1_PINC2;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 7 * 4, slv_reg_r2[7]);
                    slv_reg_r2[8] = DDS2_POFF2 << 16 | DDS2_PINC2;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 8 * 4, slv_reg_r2[8]);
                    slv_reg_r2[9] = DDS3_POFF2 << 16 | DDS3_PINC2;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 9 * 4, slv_reg_r2[9]);
                    //回放DDS控制 寄存器1   7-0 : 拉高再拉低   (110  -> 010)
                    slv_reg_r2[1] = 0x06;
                    dotNetQTDrv.QTWriteRegister(unBoardIndex, 0x800e0000, 1, slv_reg_r2[1]); //‘1’：有效
                    slv_reg_r2[1] = 0x02;
                    dotNetQTDrv.QTWriteRegister(unBoardIndex, 0x800e0000, 1, slv_reg_r2[1]); //‘0’：无效
                    //回放1 寄存器10-11
                    //slv_reg_r2[10] = DataType_ch1 << 9 | format_spct_1 << 8 | fromat_in_1 << 4 | format_dac_1;
                    //该位置暂时写死
                    slv_reg_r2[10] = 9 << 9 | format_spct_1 << 8 | fromat_in_1 << 4 | format_dac_1;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 10 * 4, slv_reg_r2[10]);
                    slv_reg_r2[11] = duc_chan_num_1 << 24 | duc_cic_cut_1 << 16 | duc_cic_num_1;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 11 * 4, slv_reg_r2[11]);
                    //回放2 寄存器12-13
                    //该位置暂时写死
                    slv_reg_r2[12] = 10 << 9 | format_spct_2 << 8 | fromat_in_2 << 4 | format_dac_2;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 12 * 4, slv_reg_r2[12]);
                    //当前采样率一致，设置为一样的cic_num
                    slv_reg_r2[13] = duc_chan_num_2 << 24 | duc_cic_cut_2 << 16 | duc_cic_num_1;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 13 * 4, slv_reg_r2[13]);
                    //寄存器14   延时、触发设置
                    slv_reg_r2[14] = trig_sel << 4 | trig_out;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 14 * 4, slv_reg_r2[14]);
                    //射频寄存器 回放1 寄存器15-16    通道编号X”0”ch1，X”1”ch2，X”2”双通道
                    slv_reg_r2[15] = rf_Freq1;
                    dotNetQTDrv.QTWriteRegister(unBoardIndex, 0x800e0000, 15 * 4, slv_reg_r2[15]);
                    slv_reg_r2[16] = If_Att1 | rf_BW1 << 16 | 0 << 24 | 1 << 28;
                    dotNetQTDrv.QTWriteRegister(unBoardIndex, 0x800e0000, 16 * 4, slv_reg_r2[16]); //‘1’：有效
                    slv_reg_r2[16] = If_Att1 | rf_BW1 << 16 | 0 << 24 | 0 << 28;
                    dotNetQTDrv.QTWriteRegister(unBoardIndex, 0x800e0000, 16 * 4, slv_reg_r2[16]); //‘0’：无效
                    //射频寄存器 回放2 寄存器17  (16进制) 31-24:01   23-16:衰减值      15-8:01     7-0:00
                    slv_reg_r2[17] = 0x00 | 0x00 << 8 | If_Att2 << 16 | 0x00 << 24;
                    dotNetQTDrv.QTWriteRegister(unBoardIndex, 0x800e0000, 17 * 4, slv_reg_r2[17]);
                    //射频寄存器 回放2 寄存器18   31-24:0x10   23-0 频率
                    slv_reg_r2[18] = rf_Freq2 | 0x10 << 24;
                    dotNetQTDrv.QTWriteRegister(unBoardIndex, 0x800e0000, 18 * 4, slv_reg_r2[18]);
                    //射频寄存器 回放2 寄存器19   7-0 : 拉高再拉低
                    slv_reg_r2[19] = 0x01;
                    dotNetQTDrv.QTWriteRegister(unBoardIndex, 0x800e0000, 19 * 4, slv_reg_r2[19]); //‘1’：有效
                    slv_reg_r2[19] = 0x00;
                    dotNetQTDrv.QTWriteRegister(unBoardIndex, 0x800e0000, 19 * 4, slv_reg_r2[19]); //‘0’：无效
                    slv_reg_r2[22] = file_tx_flag;
                    dotNetQTDrv.QTWriteRegister(unBoardIndex, 0x800e0000, 22 * 4, slv_reg_r2[22]);
                }
            }
            MutexReg.ReleaseMutex();
        }

        /// <summary>
        /// 计算最大公约数
        /// </summary>
        /// <param name="value1"></param>
        /// <param name="value2"></param>
        public static int GetGCDvalue(int value1, int value2)
        {
            int min = Math.Min(value1, value2);
            int max = Math.Max(value1, value2);
            int rem = 1;
            while (rem != 0)
            {
                rem = max % min;
                max = min;
                min = rem;
            }
            return max;
        }

        //读取App.config参数
        public static void DevConfigLoad()
        {
            try
            {
                SampleRate = 186660000;
                //ReplaySampleRate = Int32.Parse(ConfigurationManager.AppSettings["ReplaySampleRate"]);
                //采集链路存储参数
                ClockRef = true;    //时钟源 true 为内采样 false为外采样
                SignalFileSize = 512;  //单包文件存储大小
                FileCatalog = "C:\\"; //文件存储路径
                PrefixFileName = "signalwave";//文件前缀名
                UsrFileName = ""; //存储文件名
                ifSaveEnable = false;

                //回放链路参数配置
                firValue = 0;//½FIR/CIC插值滤波
                //PB1bandwidth = Int32.Parse(ConfigurationManager.AppSettings["iff2Value"]);//中频滤波器
                rf_Freq1 = 1000000;//射频输出中心频率
                rf_Freq2 = 1000000;//射频输出中心频率
                rf_Att1 = 0;//发送链路增益
                rf_Att2 = 0;//发送链路增益
                PBClockRef = true;//回放链路时钟参考源
                vald_out_1 = 0;//回放链路通道1使能
                vald_out_2 = 0;//回放链路通道2使能

                SendCnt = 0;//突发次数
                SendInterval = 0;//突发间隔
                SaveIdexes = FileCatalog.Substring(0, 1);

                ReplaySignalMode = 0;

                dout_sel = 1;        //‘1’：DDC数据 ‘0’：原始ADC数据

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        //参数写入App.config
        public static void DevConfigSave()
        {
            try
            {
                //存入配置文件
                System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings["SampleRate"].Value = SampleRate.ToString();
                config.AppSettings.Settings["ReplaySampleRate"].Value = ReplaySampleRate.ToString();

                //采集链路存储参数
                config.AppSettings.Settings["ClockRef"].Value = ClockRef.ToString();
                //config.AppSettings.Settings["ifADC1Enable"].Value = ifADC1Enable.ToString();
                //config.AppSettings.Settings["ifADC2Enable"].Value = ifADC2Enable.ToString();
                config.AppSettings.Settings["SignalFileSize"].Value = SignalFileSize.ToString();
                config.AppSettings.Settings["FileCatalog"].Value = FileCatalog.ToString();
                config.AppSettings.Settings["UsrFileName"].Value = UsrFileName.ToString();
                config.AppSettings.Settings["ifSaveEnable"].Value = ifSaveEnable.ToString();

                //回放链路参数配置
                config.AppSettings.Settings["firValue"].Value = firValue.ToString();
                //config.AppSettings.Settings["iff2Value"].Value = PB1bandwidth.ToString();
                config.AppSettings.Settings["outCh1Enable"].Value = vald_out_1.ToString();
                config.AppSettings.Settings["outCh2Enable"].Value = vald_out_2.ToString();
                config.AppSettings.Settings["RePlayFreq1"].Value = rf_Freq1.ToString();
                config.AppSettings.Settings["RePlayFreq2"].Value = rf_Freq2.ToString();
                config.AppSettings.Settings["RePlayAtt1"].Value = rf_Att1.ToString();
                config.AppSettings.Settings["RePlayAtt2"].Value = rf_Att2.ToString();

                config.AppSettings.Settings["PBClockRef"].Value = PBClockRef.ToString();

                config.AppSettings.Settings["SendCnt"].Value = SendCnt.ToString();
                config.AppSettings.Settings["SendInterval"].Value = SendInterval.ToString();


                //百利参数配置
                config.AppSettings.Settings["ReplaySignalMode"].Value = ReplaySignalMode.ToString();
                config.AppSettings.Settings["dout_sel"].Value = dout_sel.ToString();

                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSetting");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }

    }
}

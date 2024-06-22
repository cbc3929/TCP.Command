using Lookdata;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TCP.Command.PCIE
{
    public partial class PBConfig 
    {
        private static readonly Lazy<PBConfig> _instance =
        new Lazy<PBConfig>(() => new PBConfig());
        public ConcurrentQueue<string[][]> ReplayNameQueue = new ConcurrentQueue<string[][]>();
        //public PlayBackData PlayData;
        //public MSForm MSFormDis;
        //public MSDataProcess dataProcess;
        //LogProcess logXML;
        private uint m_currentDealChanNum = 0;      //1-5有效
        public uint currentReadPEPValue = 32768;

        private PBConfig()
        {
            //InitializeComponent();
            //InitUIControl();
            //PlayData = playBack;
            //MSFormDis = new MSForm();
            //dataProcess = new MSDataProcess(playBack);
            //logXML = new LogProcess();

        }
        public static PBConfig Instance =>_instance.Value;

        #region 导出对控件的控制
        public void SetFreqValue(uint chanNum, long value)
        {
            m_currentDealChanNum = chanNum;
            //设置时单位转为MHz
            if (chanNum == 1)   //宽带单位为10k
            {
                Dev_Config.rf_Freq1 = (uint)(value / 10000);
                if (Dev_Config.isEqupRun)
                {
                    Dev_Config.MutexReg.WaitOne();
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 15 * 4, Dev_Config.rf_Freq1);
                    uint slv_reg_r2_16 = Dev_Config.If_Att1 | Dev_Config.rf_BW1 << 16 | 0 << 24 | 1 << 28;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 16 * 4, slv_reg_r2_16); //‘1’：有效
                    slv_reg_r2_16 = Dev_Config.If_Att1 | Dev_Config.rf_BW1 << 16 | 0 << 24 | 0 << 28;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 16 * 4, slv_reg_r2_16); //‘0’：无效
                    Dev_Config.MutexReg.ReleaseMutex();
                }
            }
            else
            {              //宽带单位为1k
                Dev_Config.rf_Freq2 = (uint)(value / 1000);
                if (Dev_Config.isEqupRun)
                {
                    Dev_Config.MutexReg.WaitOne();

                    //射频寄存器 回放2 寄存器17  (16进制) 31-24:01   23-16:衰减值      15-8:01     7-0:00
                    uint slv_reg_r2_17 = 0x00 | 0x00 << 8 | Dev_Config.If_Att2 << 16 | 0x00 << 24;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 17 * 4, slv_reg_r2_17);

                    //射频寄存器 回放2 寄存器18   31-24:0x10   23-0 频率
                    uint slv_reg_r2_18 = Dev_Config.rf_Freq2 | 0x10 << 24;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 18 * 4, slv_reg_r2_18);

                    //射频寄存器 回放2 寄存器19   7-0 : 拉高再拉低
                    uint slv_reg_r2_19 = 0x01;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 19 * 4, slv_reg_r2_19); //‘1’：有效
                    slv_reg_r2_19 = 0x00;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 19 * 4, slv_reg_r2_19); //‘0’：无效

                    Dev_Config.MutexReg.ReleaseMutex();
                }
            }
        }

        public void SetPowerValue(uint chanNum, int value)
        {
            m_currentDealChanNum = chanNum;
            if (chanNum == 1)   //宽带单位为10k
            {
                Dev_Config.g_dPowerSetValue1 = value;
                //文件数据幅度最大值与16384进行比值，PEP计算方法为：
                //最大功率 + 功率衰减值 + 当前基带数据与16384输出间的差值（log计算化除为减）
                double controlValue = -Dev_Config.g_dPowerSetValue1 - 1 + 20 * Math.Log10(16384.0 / Dev_Config.g_iCurrentReplayFileMax);
                Dev_Config.If_Att1 = controlValue < 0 ? 0 : (uint)controlValue;
                //运行过程中更改，立刻生效
                if (Dev_Config.isEqupRun)
                {
                    Dev_Config.MutexReg.WaitOne();
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 15 * 4, Dev_Config.rf_Freq1);
                    uint slv_reg_r2_16 = Dev_Config.If_Att1 | Dev_Config.rf_BW1 << 16 | 0 << 24 | 1 << 28;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 16 * 4, slv_reg_r2_16); //‘1’：有效
                    slv_reg_r2_16 = Dev_Config.If_Att1 | Dev_Config.rf_BW1 << 16 | 0 << 24 | 0 << 28;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 16 * 4, slv_reg_r2_16); //‘0’：无效
                    Dev_Config.MutexReg.ReleaseMutex();
                }
            }
            else
            {
                Dev_Config.g_dPowerSetValue1 = value;
                //-1 + 20log10(16384 / max) - atten =  power
                double controlValue = -Dev_Config.g_dPowerSetValue2 - 1 + 20 * Math.Log10(16384.0 / Dev_Config.g_iCurrentReplayFileMax);
                Dev_Config.If_Att2 = controlValue < 0 ? 0 : (uint)controlValue;
                //运行过程中更改，立刻生效
                if (Dev_Config.isEqupRun)
                {
                    Dev_Config.MutexReg.WaitOne();

                    //射频寄存器 回放2 寄存器17  (16进制) 31-24:01   23-16:衰减值      15-8:01     7-0:00
                    uint slv_reg_r2_17 = 0x00 | 0x00 << 8 | Dev_Config.If_Att2 << 16 | 0x00 << 24;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 17 * 4, slv_reg_r2_17);

                    //射频寄存器 回放2 寄存器18   31-24:0x10   23-0 频率
                    uint slv_reg_r2_18 = Dev_Config.rf_Freq2 | 0x10 << 24;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 18 * 4, slv_reg_r2_18);

                    //射频寄存器 回放2 寄存器19   7-0 : 拉高再拉低
                    uint slv_reg_r2_19 = 0x01;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 19 * 4, slv_reg_r2_19); //‘1’：有效
                    slv_reg_r2_19 = 0x00;
                    dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 19 * 4, slv_reg_r2_19); //‘0’：无效

                    Dev_Config.MutexReg.ReleaseMutex();
                }
            }
        }

        public void SetARBSwitch(uint chanNum, bool flag)
        {
            m_currentDealChanNum = chanNum;
            if (chanNum == 1)   //宽带单位为10k
            {
                Dev_Config.ReplaySampleRate = (Int32)(double.Parse(ConfigurationManager.AppSettings["WBSampRate"]) * 1e6);     //根据不同通道下的情况直接将该值进行设置
            }
            else
            {
                //当前与宽带保持一致
                Dev_Config.ReplaySampleRate = (Int32)(double.Parse(ConfigurationManager.AppSettings["NBSampRate"]) * 1e6);     //根据不同通道下的情况直接将该值进行设置
            }

        }

        public void SetSRateValue(uint chanNum, long value)
        {
            m_currentDealChanNum = chanNum;
            //根据通道号进行区分，通道1为宽带，其他通道为窄带
            if (chanNum == 1)
            {
                //使用sps设置进入的带宽
                Dev_Config.WD1BW = value / 1000;
                //宽带采样率
                double WBSampRate = double.Parse(ConfigurationManager.AppSettings["WBSampRate"]) * 1e6;
                Dev_Config.WDCIC1 = (uint)Math.Floor(WBSampRate / 2 / value);
                Dev_Config.duc_cic_num_1 = Dev_Config.WDCIC1;
                Dev_Config.CH1BW = Dev_Config.WD1BW;
            }
            else
            {
                ////使用sps设置进入的带宽
                //Dev_Config.ND1BW = value / 1000;
                //使用sps设置进入的带宽
                Dev_Config.WD1BW = value / 1000;
                //窄带采样率和宽带采样率设   置方式相同
                double NBSampRate = double.Parse(ConfigurationManager.AppSettings["WBSampRate"]) * 1e6;
                //Dev_Config.NDCIC1 = (uint)Math.Floor(NBSampRate / 4 / value);
                //Dev_Config.duc_cic_num_1 = Dev_Config.NDCIC1;
                //Dev_Config.CH1BW = Dev_Config.ND1BW;
                Dev_Config.WDCIC1 = (uint)Math.Floor(NBSampRate / 4 / value);
                Dev_Config.duc_cic_num_1 = Dev_Config.WDCIC1;
                Dev_Config.CH1BW = Dev_Config.WD1BW;
            }
        }

        //执行原打开文件的相关逻辑，仅使用单个文件进行处理
        public void SetARBWaveDownLoadFile(uint chanNum, string fileFullPath)
        {
            m_currentDealChanNum = chanNum;

            //TODO:回放文件可能为多个文件，按;隔开

            Dev_Config.playBackType = 0;//初始化回放类型
            String names = fileFullPath;        //单个文件处理
            names = names.Trim('\0');
            if (!File.Exists(names))
            {
                //MessageBox.Show(names + "文件不存在");
                return;
            }
            //清空回放文件相关缓存
            ClearPlayBackParm();
            //ChangedUIShowState(true);
            Dev_Config.ReplayFiles = null;
            Dev_Config.ReplayFiles = new string[Dev_Config.NoDmaCh][];
            string[][] strDelete;
            while (ReplayNameQueue.Count > 0)
                ReplayNameQueue.TryDequeue(out strDelete);
            #region 获取文件路径和文件名
            //names  eg:  E:\\testdata\\202105011223\\testWave92160MHz_20210501_3.bin
            string[] tempFilePathArr = names.Split("\\".ToCharArray());
            string nameWithoutPath = tempFilePathArr[tempFilePathArr.Length - 1];//testWave92160MHz_20210501_3.bin
            if (nameWithoutPath.Split("_".ToCharArray()).Length < 3)//认为是去过帧头的文件
                Dev_Config.deleteHeader = 1;
            else
                Dev_Config.deleteHeader = 0;

            //去除文件名末尾的额外元素
            names = names.Trim('\0');
            names = names.Trim('\r');
            names = names.Trim('\n');

            long fileSize = new FileInfo(names).Length;
            //大于100m的文件也按单次读取走，直接申请对应内存即可
            Dev_Config.SignalFileState = true;//调制信号   用单文件形式回放（传递数据）
            Dev_Config.SignalFileName = names;
            #endregion
            GetFileMsg(names);//获取回放文件信息 => 得到采集时的相关配置参数（带宽、频点等）
            ReplayNameQueue.Enqueue(Dev_Config.ReplayFiles);
            Dev_Config.playBackType = 2;

            //去除必须依赖的界面样式设置

            //增加对当前读取文件的PEP计算
            FileStream stream = new FileStream(names, FileMode.Open);
            //读取1k个原始数据，IQ编成数据，两个short拼起来共32个字节
            int readByteLength = 1000 * 32;
            byte[] buffer = new byte[readByteLength];
            Int16[] outdata = new short[readByteLength / 2];
            stream.Read(buffer, 0, readByteLength);
            stream.Close();
            stream.Dispose();
            Buffer.BlockCopy(buffer, 0, outdata, 0, buffer.Length);//元素个数缩小一半
                                                                   //获取绝对值最大值作为PEP参考值
            uint maxValue = 0;
            uint currentValue = 0;
            for (int index = 0; index < readByteLength / 2; ++index)
            {
                currentValue = (uint)(Math.Abs(outdata[index]));
                if (maxValue < currentValue)
                {
                    maxValue = currentValue;
                }
            }
            //切换文件时，更新记录的基带输出最大值，触发功率设置值更新-》触发PEP值更新
            Dev_Config.g_iCurrentReplayFileMax = (int)maxValue;
            SetPowerValue(chanNum, (int)Dev_Config.g_dPowerSetValue1);
        }

        public void SetSingleReplay(uint chanNum)
        {
            m_currentDealChanNum = chanNum;
        }

        //timeStamp:时间戳，距离1970-1-1时间
        public void SetTickClockReplay(uint chanNum, string timePointStr)
        {
            //时间格式2024-04-22-19-55-56-7
            m_currentDealChanNum = chanNum;
            string[] timeParts = timePointStr.Split('-');
            if (timeParts.Length != 7)
            {
                return;
            }
            System.DateTime startTime = new DateTime(int.Parse(timeParts[0]), int.Parse(timeParts[1]), int.Parse(timeParts[2]),
                        int.Parse(timeParts[3]), int.Parse(timeParts[4]), int.Parse(timeParts[5]), int.Parse(timeParts[6]));
            Dev_Config.g_bTickPlayBackEnable = true;




        }

        //time:毫秒级计数
        public void SetRepeatReplay(uint chanNum, uint duration)
        {
            m_currentDealChanNum = chanNum;
        }

        #endregion

        //开始回放的时候建立回放参数
        public void SetPlayBackParm()
        {
            //用户如果未重新选择文件，但是修改参数的话需要重新获取数据
            if (Dev_Config.playBackType == 2 && ReplayNameQueue.IsEmpty)
                ReplayNameQueue.Enqueue(Dev_Config.ReplayFiles);
            
        }
        //停止回放的时候清空输出通道
        public void ClearPlayBackParm()
        {
            //dataProcess.MSDataFileQueue.ClearQueue();
            //dataProcess.MSCh1DataFileQueue.ClearQueue();
            //dataProcess.MSCh2DataFileQueue.ClearQueue();

            //if (Dev_Config.playBackType == 1)
            //{
            //    logXML.StartFileMsg();
            //    logXML.StopFileMsg_CDB(MSFormDis.ch1modeType + "_" + MSFormDis.ch2modeType);
            //    textBox_remark.BeginInvoke(new Action(() =>
            //    {
            //        textBox_remark.Clear();
            //    }));
            //}
            Thread.Sleep(1000);
            //PlayData.BufferClear();
        }


        //matlab参数--模式类型统一化
        private string GetMatlabModeType(string strType)
        {
            string modeType = "";
            if (strType.IndexOf("QAM") >= 0)
                modeType = "MQAM";
            else if (strType.IndexOf("PSK") == 1)
                modeType = "MPSK";
            else if (strType.IndexOf("FSK") == 1)
                modeType = "MFSK";
            else
                modeType = strType;
            return modeType;
        }
        //matlab参数--采样率
        private double GetMatlabSamprate(double bandwidth)
        {
            double samp = 2;
            if (bandwidth / 1000 > 250)
                samp = 1;
            return samp;
        }

        //matlab参数--基础幅值
        private double GetMatlabBaseamp(double bandwidth)
        {
            double amp = 2000;
            double bandwidth_m = bandwidth / 2000;
            if ((bandwidth_m >= 1 && bandwidth_m <= 4) || (bandwidth_m >= 6 && bandwidth_m <= 8) || (bandwidth_m >= 13 && bandwidth_m <= 15) || bandwidth_m == 30)
                amp = 1418;
            else if (bandwidth_m == 5 || (bandwidth_m >= 9 && bandwidth_m <= 12))
                amp = 2000;
            else if (bandwidth_m >= 16 && bandwidth_m <= 25)
                amp = 2000 * 1.41;
            else
            {
                amp = 2000 * 1.12;
            }
            return amp;
        }


        #region 回放文件选择

        //获取回放文件信息参数
        private void GetFileMsg(string filename)
        {
            Dev_Config.dout_sel = 1;
            Dev_Config.dout_ctrl = 1;

            Dev_Config.AD1Enable = 0;
            Dev_Config.AD2Enable = 0;

            #region 读取带宽信息,计算抽取倍数
            double WBSampRate = double.Parse(ConfigurationManager.AppSettings["WBSampRate"]) * 1e6;
            Dev_Config.WDCIC1 = (uint)Math.Floor(WBSampRate / 4 / Dev_Config.WD1BW);
            Dev_Config.duc_cic_num_1 = Dev_Config.WDCIC1;

            double NBSampRate = double.Parse(ConfigurationManager.AppSettings["NBSampRate"]) * 1e6;
            Dev_Config.NDCIC1 = (uint)Math.Floor(NBSampRate / 4 / Dev_Config.ND1BW);
            Dev_Config.duc_cic_num_2 = Dev_Config.NDCIC1;

            #endregion
        }

        //得到回放数据的抽取倍数
        private uint GetCICvalue(int CH, double WDBW, double NDBW)
        {
            uint WDExtraction = 0, NDExtraction = 0;
            double BandWidthValue = 0;//以MHz为单位
            switch (WDBW)
            {
                case 180:
                    WDExtraction = 1;
                    BandWidthValue = 250;
                    break;
                case 80:
                    WDExtraction = 2;
                    BandWidthValue = 125;
                    break;
                case 30:
                    WDExtraction = 4;
                    BandWidthValue = 62.5;
                    break;
                case 10:
                    WDExtraction = 10;
                    BandWidthValue = 25;
                    break;
                default: break;
            }
            if (CH == 1)
                Dev_Config.WD1BW = BandWidthValue * 1000;
            else if (CH == 2)
                Dev_Config.WD2BW = BandWidthValue * 1000;//单位KHz

            if (NDBW == 0)
                return WDExtraction;

            NDExtraction = (uint)Math.Floor(BandWidthValue * 1000 / NDBW) * WDExtraction;
            return NDExtraction;
        }

        #endregion

        //#region 常规配置 （回放通道使能、回放数据类型、回放频点、回放衰减、回放带宽、循环回放、内外参考）
        ////回放通道使能
        //private void CheckBox_PB1Enable_CheckedChanged(object sender, EventArgs e)
        //{
        //    if (checkBox_PB1Enable.Checked)
        //    {
        //        Dev_Config.vald_out_1 = 1;
        //        comboBox_dataType1.Enabled = true;
        //        //if (Dev_Config.AD1Enable == 1)
        //        //{
        //        //    comboBox_dataType1.SelectedIndex = 0;
        //        //    comboBox_dataType1.Enabled = false;
        //        //}
        //        //else if (Dev_Config.AD2Enable == 1)
        //        //{
        //        //    comboBox_dataType1.SelectedIndex = 1;
        //        //    comboBox_dataType1.Enabled = false;
        //        //}
        //        //选定调制信号，不可编辑
        //        if (Dev_Config.playBackType == 1)
        //        {
        //            comboBox_dataType1.SelectedIndex = 3;
        //            comboBox_dataType1.Enabled = false;
        //            comboBox_RFBW1.Enabled = true;
        //            //带宽向上靠近
        //            if (Dev_Config.moduSignal1BW / 2000 <= 10)
        //                comboBox_RFBW1.SelectedIndex = 3;
        //            else if (Dev_Config.moduSignal1BW / 2000 <= 30)
        //                comboBox_RFBW1.SelectedIndex = 2;
        //            else if (Dev_Config.moduSignal1BW / 2000 <= 80)
        //                comboBox_RFBW1.SelectedIndex = 1;
        //            else if (Dev_Config.moduSignal1BW / 2000 <= 180)
        //                comboBox_RFBW1.SelectedIndex = 0;
        //            comboBox_RFBW1.Enabled = false;
        //        }
        //        else
        //            comboBox_RFBW1.Enabled = true;

        //        numericUpDown_Freq1.Enabled = true;
        //        numericUpDown_Att1.Enabled = true;

        //    }
        //    else
        //    {
        //        Dev_Config.vald_out_1 = 0;
        //        comboBox_dataType1.SelectedIndex = -1;
        //        numericUpDown_Freq1.Enabled = false;
        //        numericUpDown_Att1.Enabled = false;
        //        comboBox_RFBW1.Enabled = false;
        //        comboBox_dataType1.Enabled = false;
        //    }
        //}

        //private void CheckBox_PB2Enable_CheckedChanged(object sender, EventArgs e)
        //{
        //    if (checkBox_PB2Enable.Checked)
        //    {
        //        Dev_Config.vald_out_2 = 1;
        //        comboBox_dataType2.Enabled = true;
        //        //if (Dev_Config.AD2Enable == 1)
        //        //{
        //        //    comboBox_dataType2.SelectedIndex = 1;
        //        //    comboBox_dataType2.Enabled = false;
        //        //}
        //        //else if (Dev_Config.AD1Enable == 1)
        //        //{
        //        //    comboBox_dataType2.SelectedIndex = 0;
        //        //    comboBox_dataType2.Enabled = false;
        //        //}
        //        //选定调制信号，不可编辑
        //        if (Dev_Config.playBackType == 1)
        //        {
        //            comboBox_dataType2.SelectedIndex = 3;
        //            comboBox_dataType2.Enabled = false;
        //            comboBox_RFBW2.Enabled = true;
        //            //带宽向上靠近
        //            if (Dev_Config.moduSignal2BW / 2000 <= 10)
        //                comboBox_RFBW2.SelectedIndex = 3;
        //            else if (Dev_Config.moduSignal2BW / 2000 <= 30)
        //                comboBox_RFBW2.SelectedIndex = 2;
        //            else if (Dev_Config.moduSignal2BW / 2000 <= 80)
        //                comboBox_RFBW2.SelectedIndex = 1;
        //            else if (Dev_Config.moduSignal2BW / 2000 <= 180)
        //                comboBox_RFBW2.SelectedIndex = 0;
        //            comboBox_RFBW2.Enabled = false;
        //        }
        //        else
        //            comboBox_RFBW2.Enabled = true;

        //        numericUpDown_Freq2.Enabled = true;
        //        numericUpDown_Att2.Enabled = true;
        //    }
        //    else
        //    {
        //        Dev_Config.vald_out_2 = 0;
        //        comboBox_dataType2.SelectedIndex = -1;
        //        numericUpDown_Freq2.Enabled = false;
        //        numericUpDown_Att2.Enabled = false;
        //        comboBox_RFBW2.Enabled = false;
        //        comboBox_dataType2.Enabled = false;
        //    }
        //}

        ////回放数据类型
        //private void ComboBox_dataType1_SelectedIndexChanged(object sender, EventArgs e)
        //{
        //    switch (comboBox_dataType1.SelectedIndex)
        //    {
        //        case 0:
        //            Dev_Config.DataType_ch1 = 1;//AD1
        //            Dev_Config.duc_cic_num_1 = 1;
        //            Dev_Config.CH1BW = 0;
        //            break;
        //        case 1:
        //            Dev_Config.DataType_ch1 = 3;//WDDDC1
        //            Dev_Config.duc_cic_num_1 = Dev_Config.WDCIC1;
        //            Dev_Config.CH1BW = Dev_Config.WD1BW;
        //            break;
        //        case 2:
        //            Dev_Config.DataType_ch1 = 7;//NDDDC1
        //            Dev_Config.duc_cic_num_1 = Dev_Config.NDCIC1;
        //            Dev_Config.CH1BW = Dev_Config.ND1BW;
        //            break;
        //        case 3:
        //            Dev_Config.DataType_ch1 = 9;//调制信号1
        //            Dev_Config.duc_cic_num_1 = Dev_Config.moduSignalCIC1;
        //            Dev_Config.CH1BW = 0;
        //            break;
        //        default:
        //            Dev_Config.DataType_ch1 = 0;
        //            Dev_Config.duc_cic_num_1 = 0;
        //            Dev_Config.CH1BW = 0;
        //            break;
        //    }
        //}

        //private void ComboBox_dataType2_SelectedIndexChanged(object sender, EventArgs e)
        //{
        //    switch (comboBox_dataType2.SelectedIndex)
        //    {
        //        case 0:
        //            Dev_Config.DataType_ch2 = 2;//AD2
        //            Dev_Config.duc_cic_num_2 = 1;
        //            Dev_Config.CH2BW = 0;
        //            break;
        //        case 1:
        //            Dev_Config.DataType_ch2 = 4;//WDDC2
        //            Dev_Config.duc_cic_num_2 = Dev_Config.WDCIC2;
        //            Dev_Config.CH2BW = Dev_Config.WD2BW;
        //            break;
        //        case 2:
        //            Dev_Config.DataType_ch2 = 8;//NDDC2
        //            Dev_Config.duc_cic_num_2 = Dev_Config.NDCIC2;
        //            Dev_Config.CH2BW = Dev_Config.ND2BW;
        //            break;
        //        case 3:
        //            Dev_Config.DataType_ch2 = 10;//调制信号2
        //            Dev_Config.duc_cic_num_2 = Dev_Config.moduSignalCIC2;
        //            Dev_Config.CH2BW = 0;
        //            break;
        //        default:
        //            Dev_Config.DataType_ch2 = 0;
        //            Dev_Config.duc_cic_num_2 = 0;
        //            Dev_Config.CH2BW = 0;
        //            break;
        //    }
        //}

        ////回放衰减
        //private void NumericUpDown_Att2_ValueChanged(object sender, EventArgs e)
        //{
        //    uint att = 0;
        //    if (Dev_Config.playBackType == 1)
        //        att = (uint)(numericUpDown_Att2.Value * (-1) - 20);
        //    else
        //        att = (uint)numericUpDown_Att2.Value;
        //    if (Dev_Config.rf_Att2 + Dev_Config.If_Att2 != att)
        //    {
        //        if (att > 30)
        //        {
        //            Dev_Config.rf_Att2 = 30; Dev_Config.If_Att2 = att - 30;
        //        }
        //        else
        //        {
        //            Dev_Config.rf_Att2 = att; Dev_Config.If_Att2 = 0;
        //        }
        //        if (Dev_Config.isEqupRun)
        //        {
        //            Dev_Config.MutexReg.WaitOne();
        //            dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 15 * 4, Dev_Config.rf_Freq2);
        //            uint slv_reg_r2_16 = Dev_Config.If_Att2 | Dev_Config.rf_Att2 << 8 | Dev_Config.rf_BW2 << 16 | 1 << 24 | 1 << 28;
        //            dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 16 * 4, slv_reg_r2_16); //‘1’：有效
        //            slv_reg_r2_16 = Dev_Config.If_Att2 | Dev_Config.rf_Att2 << 8 | Dev_Config.rf_BW2 << 16 | 1 << 24 | 0 << 28;
        //            dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 16 * 4, slv_reg_r2_16); //‘0’：无效
        //            Dev_Config.MutexReg.ReleaseMutex();
        //        }
        //    }
        //}

        //private void NumericUpDown_Att1_ValueChanged(object sender, EventArgs e)
        //{
        //    uint att = 0;
        //    if (Dev_Config.playBackType == 1)
        //        att = (uint)(numericUpDown_Att1.Value * (-1) - 20);
        //    else
        //        att = (uint)numericUpDown_Att1.Value;
        //    if (Dev_Config.rf_Att1 + Dev_Config.If_Att1 != att)
        //    {
        //        //原为两段式配置功率，当前仅保留一段If_Att1
        //        Dev_Config.If_Att1 = att;
        //        if (Dev_Config.isEqupRun)
        //        {
        //            Dev_Config.MutexReg.WaitOne();
        //            dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 15 * 4, Dev_Config.rf_Freq1);
        //            uint slv_reg_r2_16 = Dev_Config.If_Att1 | Dev_Config.rf_BW1 << 16 | 0 << 24 | 1 << 28;
        //            dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 16 * 4, slv_reg_r2_16); //‘1’：有效
        //            slv_reg_r2_16 = Dev_Config.If_Att1 | Dev_Config.rf_BW1 << 16 | 0 << 24 | 0 << 28;
        //            dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 16 * 4, slv_reg_r2_16); //‘0’：无效
        //            Dev_Config.MutexReg.ReleaseMutex();
        //        }
        //    }
        //}


        ////回放带宽
        //private void ComboBox_RFBW1_SelectedIndexChanged(object sender, EventArgs e)
        //{
        //    if (comboBox_RFBW1.SelectedIndex < 0)
        //        return;
        //    Dev_Config.rf_BW1 = (uint)comboBox_RFBW1.SelectedIndex;
        //    if (Dev_Config.isEqupRun)
        //    {
        //        Dev_Config.MutexReg.WaitOne();
        //        dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 15 * 4, Dev_Config.rf_Freq1);
        //        uint slv_reg_r2_16 = Dev_Config.If_Att1 | Dev_Config.rf_Att1 << 8 | Dev_Config.rf_BW1 << 16 | 0 << 24 | 1 << 28;
        //        dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 16 * 4, slv_reg_r2_16); //‘1’：有效
        //        slv_reg_r2_16 = Dev_Config.If_Att1 | Dev_Config.rf_Att1 << 8 | Dev_Config.rf_BW1 << 16 | 0 << 24 | 0 << 28;
        //        dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 16 * 4, slv_reg_r2_16); //‘0’：无效
        //        Dev_Config.MutexReg.ReleaseMutex();
        //    }
        //}

        //private void ComboBox_RFBW2_SelectedIndexChanged(object sender, EventArgs e)
        //{
        //    if (comboBox_RFBW2.SelectedIndex < 0)
        //        return;
        //    Dev_Config.rf_BW2 = (uint)comboBox_RFBW2.SelectedIndex;
        //    if (Dev_Config.isEqupRun)
        //    {
        //        Dev_Config.MutexReg.WaitOne();
        //        dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 15 * 4, Dev_Config.rf_Freq2);
        //        uint slv_reg_r2_16 = Dev_Config.If_Att2 | Dev_Config.rf_Att2 << 8 | Dev_Config.rf_BW2 << 16 | 1 << 24 | 1 << 28;
        //        dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 16 * 4, slv_reg_r2_16); //‘1’：有效
        //        slv_reg_r2_16 = Dev_Config.If_Att2 | Dev_Config.rf_Att2 << 8 | Dev_Config.rf_BW2 << 16 | 1 << 24 | 0 << 28;
        //        dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 16 * 4, slv_reg_r2_16); //‘0’：无效
        //        Dev_Config.MutexReg.ReleaseMutex();
        //    }
        //}

        ////回放频点
        //private void NumericUpDown_Freq1_ValueChanged(object sender, EventArgs e)
        //{
        //    uint freq = (uint)((double)numericUpDown_Freq1.Value * 1000);
        //    //直接设置
        //    Dev_Config.rf_Freq1 = freq;
        //    if (Dev_Config.isEqupRun)
        //    {
        //        Dev_Config.MutexReg.WaitOne();
        //        dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 15 * 4, Dev_Config.rf_Freq1);
        //        uint slv_reg_r2_16 = Dev_Config.If_Att1 | Dev_Config.rf_Att1 << 8 | Dev_Config.rf_BW1 << 16 | 0 << 24 | 1 << 28;
        //        dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 16 * 4, slv_reg_r2_16); //‘1’：有效
        //        slv_reg_r2_16 = Dev_Config.If_Att1 | Dev_Config.rf_Att1 << 8 | Dev_Config.rf_BW1 << 16 | 0 << 24 | 0 << 28;
        //        dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 16 * 4, slv_reg_r2_16); //‘0’：无效
        //        Dev_Config.MutexReg.ReleaseMutex();
        //    }
        //}

        //private void NumericUpDown_Freq2_ValueChanged(object sender, EventArgs e)
        //{
        //    uint freq = (uint)((double)numericUpDown_Freq2.Value * 1000);

        //    if (Dev_Config.rf_Freq2 != freq)
        //    {
        //        Dev_Config.rf_Freq2 = freq;
        //        if (Dev_Config.isEqupRun)
        //        {
        //            Dev_Config.MutexReg.WaitOne();
        //            dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 15 * 4, Dev_Config.rf_Freq2);
        //            uint slv_reg_r2_16 = Dev_Config.If_Att2 | Dev_Config.rf_Att2 << 8 | Dev_Config.rf_BW2 << 16 | 1 << 24 | 1 << 28;
        //            dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 16 * 4, slv_reg_r2_16); //‘1’：有效
        //            slv_reg_r2_16 = Dev_Config.If_Att2 | Dev_Config.rf_Att2 << 8 | Dev_Config.rf_BW2 << 16 | 1 << 24 | 0 << 28;
        //            dotNetQTDrv.QTWriteRegister(Dev_Config.unBoardIndex, 0x800e0000, 16 * 4, slv_reg_r2_16); //‘0’：无效
        //            Dev_Config.MutexReg.ReleaseMutex();
        //        }
        //    }
        //}

        ////内外参考
        //private void RadioButton_InRef_CheckedChanged(object sender, EventArgs e)
        //{
        //    if (radioButton_InRef.Checked && Dev_Config.PBClockRef != true)
        //    {
        //        Dev_Config.PBClockRef = true;
        //        Dev_Config.ClockRefChanged = true;
        //    }
        //    else if (radioButton_OutRef.Checked && Dev_Config.PBClockRef != false)
        //    {
        //        Dev_Config.PBClockRef = false;
        //        Dev_Config.ClockRefChanged = true;
        //    }
        //}

        ////循环回放
        //private void CheckBox_LoopPlayBack_CheckedChanged(object sender, EventArgs e)
        //{
        //    if (checkBox_LoopPlayBack.CheckState == CheckState.Checked)
        //    {
        //        Dev_Config.LoopPlayBack = true;
        //        Dev_Config.ReplaySignalMode = 1;
        //    }
        //    else
        //    {
        //        Dev_Config.LoopPlayBack = false;
        //        Dev_Config.ReplaySignalMode = 0;
        //    }
        //}

        ////建立回放时间
        //public void SetPlayBackTime(string strTime)
        //{
        //    //label_PlayBackTime.BeginInvoke(new Action(() =>
        //    //{
        //    //    label_PlayBackTime.Text = strTime;
        //    //}));
        //}

        ////定时回放使能
        //private void CheckBox_FixedTimePB_CheckedChanged(object sender, EventArgs e)
        //{
        //    if ((sender as CheckBox).Checked)
        //    {
        //        waitTimePB = true;
        //        dateEdit_start.Visible = true;
        //        dateEdit1_end.Visible = true;
        //        dateEdit_start.DateTime = DateTime.Now;
        //        dateEdit1_end.DateTime = DateTime.Now;
        //    }
        //    else
        //    {
        //        waitTimePB = false;
        //        dateEdit_start.Visible = false;
        //        dateEdit1_end.Visible = false;
        //    }
        //}
        ////回放计时
        //public bool waitTimePB = false;
        //public DateTime STime, ETime;
        //private void DateEdit1_end_EditValueChanged(object sender, EventArgs e)
        //{
        //    ETime = dateEdit1_end.DateTime;
        //}

        //private void DateEdit_start_EditValueChanged(object sender, EventArgs e)
        //{
        //    STime = dateEdit_start.DateTime.AddSeconds(-5);
        //}


        //#endregion

    }
}

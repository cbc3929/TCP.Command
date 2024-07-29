using Lookdata;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TCP.Command.Interface;

namespace TCP.Command.PCIE
{
    public class ChannelState
    {
        public CancellationTokenSource singleRunCts;
        public CancellationTokenSource loopRunCts;
        public CancellationTokenSource monitorCts;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private long _srate;
        public bool IsFirstRun { get; set; }

        public string TicTime { get; set; }
        public bool IsRunning { get; set; }

        public int IntervalTimeUs { get; set; }
        public long Srate
        {
            get { return _srate; }
            set
            {
                if (_srate != value)
                {
                    _srate = value;
                    onSrateChange();
                }
            }
        }

        public double SrateOrigin { get; set; }

        public double FreqOrginValue { get; set; }
        public bool IsLoop { get; set; }

        public bool ARBSwitch { get; set; }

        public bool BBSwitch { get; set; }

        public bool RFSwitch { get; set; }

        public double FreqValue { get; set; }
        /// <summary>
        /// 根据条件确定的当前的FIR
        /// </summary>
        public int CurrentFIR { get; set; }

        public int Props { get; set; }

        public decimal RF_Atten { get; set; }
        public decimal IF_Atten { get; set; }
        /// <summary>
        /// 插值
        /// </summary>
        public UInt16 FarrowInterp { get; set; }
        /// <summary>
        /// 抽取
        /// </summary>
        public UInt16 FarrowDecim { get; set; }
        /// <summary>
        /// cic+fir
        /// </summary>
        public int CICNum { get; set; }
        private PcieCard _card;
        public int Power { get; set; }

        public char FreqSubUnit { get; set; }

        public long FilePointCount { get; set; }

        public char SampSubUnit { get; set; }

        public UInt32 DDS { get; set; }

        public PlaybackMethodType PlaybackMethod { get; set; }

        public int Magnitude { get; set; }
        public Mutex mutex { get; internal set; }

        public ChannelState(PcieCard card)
        {
            _card = card;
            mutex = new Mutex();
            Srate = 0;
            ARBSwitch = false;
            BBSwitch = false;
            RFSwitch = false;
            FreqValue = 500000000;
            Power = 0;
            FreqSubUnit = Char.Parse("M");
            SampSubUnit = Char.Parse("M");
            PlaybackMethod = PlaybackMethodType.REP;
            IsLoop = false;
            Magnitude = 500;
            FilePointCount = 0;
            DDS = 0;
            FreqOrginValue = 100;
            Props = 10000;
            RF_Atten = 10;
            IF_Atten = 0;
            IsRunning = false;
            IsFirstRun = true;
            FarrowDecim = 0; FarrowInterp = 0; CICNum = 0;
            singleRunCts = new CancellationTokenSource();
            loopRunCts = new CancellationTokenSource();
            monitorCts = new CancellationTokenSource();
        }
        private void onSrateChange()
        {
            if (Srate == 0)
            { return; }
            //先计算法罗
            if (_card.ChannelCount > 1)
            {
                //窄带
                //最先判断落到了哪个FIR

                if (Srate > 10000 && Srate <= 9375000)
                {
                    CurrentFIR = 16;
                    var cic = (int)FindNearestPowerOfTwo(_card.FS / Srate);
                    CalculateFarrowValues(_card.FS, Srate, cic);
                    CICNum = cic;
                }
                else if (Srate > 9375000 && Srate <= 18750000)
                {
                    CurrentFIR = 32;
                    CalculateFarrowValues(_card.FS, Srate, CurrentFIR);
                    CICNum = CurrentFIR;
                }
                else if (Srate > 18750000 && Srate <= 37500000)
                {
                    CurrentFIR = 16;
                    CalculateFarrowValues(_card.FS, Srate, CurrentFIR);
                    CICNum = CurrentFIR;
                }
                else if (Srate > 37500000 && Srate <= 75000000)
                {
                    CurrentFIR = 8;
                    CalculateFarrowValues(_card.FS, Srate, CurrentFIR);
                    CICNum = CurrentFIR;
                }
                else
                {
                    CurrentFIR = 4;
                    CalculateFarrowValues(_card.FS, Srate, CurrentFIR);
                    CICNum = CurrentFIR;
                }
            }
            else
            {
                //宽带
               
                if (Srate > 25000000 && Srate <= 75000000)
                {
                    CurrentFIR = 32;

                }
                else if (Srate > 75000000 && Srate <= 150000000)
                {
                    CurrentFIR = 16;

                }
                else if (Srate > 150000000 && Srate <= 300000000)
                {
                    CurrentFIR = 8;
                }
                else 
                {
                    CurrentFIR = 4;

                }
                CalculateFarrowValues(_card.FS, Srate, CurrentFIR);
                CICNum = CurrentFIR;

            }
            //通知 eventBus 法罗 dds 和 cnc已经计算结束
            EventBus.Instance.Publish(EventTypes.SrateChanged, EventArgs.Empty);
        }


        public long interept(long a, long b)
        {
            var c = a % b;
            if (c == 0)
            {
                return b;
            }
            else
            {
                return interept(b, c);
            }
        }
        /// <summary>
        /// 返回与这个值最接近的2的幂次方的值
        /// </summary>
        /// <param name="number">给定需要和2^n次方比较的数</param>
        /// <returns></returns>
        public double FindNearestPowerOfTwo(double number)
        {
            // 使用数学方法计算最接近的2的幂
            int exponent = (int)Math.Round(Math.Log(number, 2),MidpointRounding.ToNegativeInfinity);
            return Math.Pow(2, exponent);
        }
        /// <summary>
        /// 保证比值的情况下，精度最高的且分母最接近16384 的组合 精度优先
        /// </summary>
        /// <param name="radio">原始比值</param>
        /// <returns></returns>
        public static (int, int) FindClosestFraction(double radio)
        {
            int maxLarge = 16384;
            int bestLarge = 0;
            int bestSmall = 0;
            double closestDifference = double.MaxValue;

            int originalPrecision = 0;

            // 如果radio是整数，直接计算最大精度情况
            if (radio == (int)radio)
            {
                bestLarge = (int)radio;
                bestSmall = 1;
                return (bestLarge, bestSmall);
            }

            for (int small = 1; small <= maxLarge; small++)
            {
                int large = (int)Math.Round(radio * small);
                if (large > 0 && large <= maxLarge)
                {
                    double actualRatio = (double)large / small;
                    double difference = Math.Abs(actualRatio - radio);

                    if (difference < closestDifference)
                    {
                        closestDifference = difference;
                        bestLarge = large;
                        bestSmall = small;
                        originalPrecision = (int)Math.Floor(-Math.Log10(difference));

                        if (originalPrecision >= 10)
                        {
                            return (bestLarge, bestSmall);
                        }
                    }
                }
            }

            return (bestLarge, bestSmall);
        }
        /// <summary>
        /// 计算法罗的差值和抽取以及DSS 触发了16384要还原采样率后计算DDS
        /// </summary>
        /// <param name="fs">卡的采样率 宽带位1.2 窄带为600m</param>
        /// <param name="srate">下发的文件采样率</param>
        /// <param name="factor">一般是fir 如果 是窄带的第一个区间则为cic和fir的混合</param>
        private void CalculateFarrowValues(uint fs, long srate, int factor)
        {
            //最大公约数
            var biggest = interept(fs, srate * factor);
            double temp_farrow_interp = fs/biggest;
            double temp_farrow_decim = srate * factor / biggest;
            if (fs / biggest > 16384)
            {
                double radtio = temp_farrow_interp / temp_farrow_decim;
                var (large, small) = FindClosestFraction(radtio);
                FarrowInterp = (UInt16)large;
                FarrowDecim = (UInt16)small;
                double b = (double)large / (double)small;
                //  srate  
                var newSrate = fs / factor / b;
                Logger.Info("触发16384 限制，还原采样率为" + (double)(newSrate/1000) + "kHz");
                Logger.Info("原始采样率为" + (double)(srate/1000) + "kHz");
                //还原下发的采样率
                
                double value = 0;
                if (fs > 600000000)
                {
                    value = newSrate * Math.Pow(2, 21) / 1171875;
                }
                else 
                {
                    value = newSrate * Math.Pow(2, 22) / 1171875;
                }
                
                
                
                DDS = (uint)value;
            }
            else
            {
                FarrowInterp = (UInt16)(fs / biggest);
                FarrowDecim = (UInt16)(srate * factor / biggest);
                double value = 0;
                if (fs > 600000000)
                {
                    value = Srate * Math.Pow(2, 21) / 1171875;
                }
                else
                {
                    value = Srate * Math.Pow(2, 22) / 1171875;
                }

                DDS = (uint)value;
            }
        }
    }
    public enum PlaybackMethodType
    {
        SIN,
        REP,
        TIC

    }
}

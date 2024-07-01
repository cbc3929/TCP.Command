using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCP.Command.PCIE
{
    public class ChannelState
    {
        public CancellationTokenSource singleRunCts;
        public CancellationTokenSource loopRunCts;
        public CancellationTokenSource monitorCts;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public long Srate
        {
            get { return Srate; }
            set
            {
                if (Srate != value)
                {
                    Srate = value;
                    onSrateChange();
                }

            }
        }
        public bool IsLoop { get; set; }

        public bool ARBSwitch { get; set; }

        public bool BBSwitch { get; set; }

        public bool RFSwitch { get; set; }

        public double FreqValue { get; set; }
        /// <summary>
        /// 根据条件确定的当前的FIR
        /// </summary>
        public int CurrentFIR { get; set; }

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

        public char SampSubUnit { get; set; }

        public UInt32 DDS { get; set; }

        public PlaybackMethodType PlaybackMethod { get; set; }

        public int Magnitude { get; set; }

        public ChannelState(PcieCard card)
        {
            Srate = 0;
            ARBSwitch = false;
            BBSwitch = false;
            RFSwitch = false;
            FreqValue = 0;
            Power = 0;
            FreqSubUnit = Char.Parse("k");
            SampSubUnit = Char.Parse("d");
            PlaybackMethod = PlaybackMethodType.SIN;
            IsLoop = false;
            Magnitude = 500;
            DDS = 0;
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
                    CurrentFIR = 4;
                    var cic = (int)FindNearestPowerOfTwo(_card.FS / Srate);
                    CalculateFarrowValues(_card.FS, Srate, cic);
                }
                else if (Srate > 9375000 && Srate <= 18750000)
                {
                    CurrentFIR = 32;
                    CalculateFarrowValues(_card.FS, Srate, CurrentFIR);
                }
                else if (Srate > 18750000 && Srate <= 37500000)
                {
                    CurrentFIR = 16;
                    CalculateFarrowValues(_card.FS, Srate, CurrentFIR);
                }
                else if (Srate > 37500000 && Srate <= 75000000)
                {
                    CurrentFIR = 8;
                    CalculateFarrowValues(_card.FS, Srate, CurrentFIR);
                }
                else
                {
                    CurrentFIR = 4;
                    CalculateFarrowValues(_card.FS, Srate, CurrentFIR);
                }
            }
            else
            {
                //宽带

            }




            var value = Srate * Math.Pow(2, 20) / 1171875;
            long round = (long)Math.Round(value);
            long limitedValue = round & 0xFFFFFF;
            DDS = (uint)limitedValue;

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
            int exponent = (int)Math.Round(Math.Log(number, 2));
            return Math.Pow(2, exponent);
        }
        /// <summary>
        /// 保证比值的情况下，精度最高的且分母最接近16384 的组合
        /// 损失精度策略：精度不可以损失到低于比值的4位
        /// 精度最多可以损失两位，且必须保证新的分母比原来的分母大1000
        /// 触发损失精度策略后会logger出对比
        /// </summary>
        /// <param name="radio">原始比值</param>
        /// <returns></returns>
        public static (int, int) FindClosestFraction(double radio)
        {
            int maxLarge = 16384;
            int bestLarge = 0;
            int bestSmall = 0;
            double closestDifference = double.MaxValue;
            int originalLarge = 0;
            int originalSmall = 0;
            double originalRatio = 0.0;
            int originalPrecision = 0;
            bool precisionCompromised = false;

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

            originalLarge = bestLarge;
            originalSmall = bestSmall;
            originalRatio = (double)bestLarge / bestSmall;

            for (int precision = originalPrecision - 1; precision >= 4; precision--)
            {
                for (int small = 1; small <= maxLarge; small++)
                {
                    int large = (int)Math.Round(radio * small);
                    if (large > 0 && large <= maxLarge)
                    {
                        double actualRatio = (double)large / small;
                        double difference = Math.Abs(actualRatio - radio);

                        if (Math.Abs(difference - Math.Pow(10, -precision)) < Math.Pow(10, -precision + 1))
                        {
                            if (large > bestLarge && (large - originalLarge) > 1000)
                            {
                                bestLarge = large;
                                bestSmall = small;
                                precisionCompromised = true;

                                if (precision <= originalPrecision - 2)
                                {
                                    Logger.Info($"Precision was compromised to achieve a larger value.");
                                    Logger.Info($"Original Large: {originalLarge}");
                                    Logger.Info($"Original Small: {originalSmall}");
                                    Logger.Info($"Original Ratio: {originalRatio}");
                                    Logger.Info($"Original Precision: {originalPrecision} decimal places");
                                    return (bestLarge, bestSmall);
                                }
                            }
                        }
                    }
                }
            }

            if (precisionCompromised)
            {
                Logger.Info($"Precision was compromised to achieve a larger value.");
                Logger.Info($"Original Large: {originalLarge}");
                Logger.Info($"Original Small: {originalSmall}");
                Logger.Info($"Original Ratio: {originalRatio}");
                Logger.Info($"Original Precision: {originalPrecision} decimal places");
            }

            return (bestLarge, bestSmall);
        }
        /// <summary>
        /// 计算法罗的差值和抽取以及DSS 触发了16384要还原采样率后计算DDS
        /// </summary>
        /// <param name="fs">卡的采样率 宽带位1.2 窄带为600m</param>
        /// <param name="srate">下发的文件采样率</param>
        /// <param name="factor">一般是fir 如果 是窄带的第一个区间则为cic和fir的混合</param>
        private void CalculateFarrowValues(int fs, long srate, int factor)
        {
            //最大公约数
            var biggest = interept(fs, srate * factor);
            if (fs / biggest > 16384)
            {
                var (large, small) = FindClosestFraction(fs / biggest);
                FarrowInterp = (UInt16)large;
                FarrowDecim = (UInt16)small;
                //还原下发的采样率
                double b = large / small;

                var newSrate = fs / (b * factor);
                var value = newSrate * Math.Pow(2, 20) / 1171875;
                long round = (long)Math.Round(value);
                long limitedValue = round & 0xFFFFFF;
                DDS = (uint)limitedValue;
            }
            else
            {
                FarrowInterp = (UInt16)(fs / biggest);
                FarrowDecim = (UInt16)(srate * factor / biggest);
                var value = Srate * Math.Pow(2, 20) / 1171875;
                long round = (long)Math.Round(value);
                long limitedValue = round & 0xFFFFFF;
                DDS = (uint)limitedValue;
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

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
        public double Srate { get { return Srate; }
            set {
                if (Srate != value)
                {
                    Srate = value;
                    onSrateChange();
                }
            
            } }
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

        public UInt32 DDS {  get; set; }

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
            FarrowDecim = 0; FarrowInterp = 0;CICNum = 0;
            singleRunCts = new CancellationTokenSource();
            loopRunCts = new CancellationTokenSource();
            monitorCts = new CancellationTokenSource();
        }
        private void onSrateChange() 
        {
            //先计算法罗
            if (_card.ChannelCount > 1) 
            {
                //窄带
                _card.FS/Srate

            }


            var value = Srate * Math.Pow(2, 20)/1171875;
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

        private Tuple<double, double> AdjustValues(double large, double small)
        {

            // 计算比值
            double ratio = (double)small / large;

            // 调整后的大的数应该小于16384
            double adjustedLarge = 16383;

            // 根据比值计算新的小的数
            double adjustedSmall = (int)Math.Round(adjustedLarge * ratio);

            return Tuple.Create(adjustedLarge, adjustedSmall);
        }
    }
    public enum PlaybackMethodType 
    {
        SIN,
        REP,
        TIC
    
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCP.Command
{
    public class ChannelState
    {
        public double Srate { get; set; }

        public bool ARBSwitch { get; set; }

        public bool BBSwitch { get; set; }

        public bool RFSwitch { get; set; }

        public double FreqValue { get; set; }

        public int Power { get; set; }

        public char FreqSubUnit { get; set; }

        public char SampSubUnit { get; set; }

        public string PlaybackMethod { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TCP.Command
{
    public class DateTimeSynchronization
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SystemTime
        {
            public short year;
            public short month;
            public short dayOfWeek;
            public short day;
            public short hour;
            public short minute;
            public short second;
            public short milliseconds;
        }

        [DllImport("kernel32.dll")]
        private static extern bool SetLocalTime(ref SystemTime time);

        public uint swapEndian(ulong x)
        {
            return (uint)(((x & 0x000000ff) << 24) +
            ((x & 0x0000ff00) << 8) +
            ((x & 0x00ff0000) >> 8) +
            ((x & 0xff000000) >> 24));
        }

        /// <summary>
        /// 设置系统时间
        /// </summary>
        /// <param name="CurrentTime">需要设置的时间</param>
        /// <returns>返回系统时间设置状态，true为成功，false为失败</returns>
        public bool SetLocalDateTime(DateTime CurrentTime)
        {
            SystemTime st;
            st.year = (short)CurrentTime.Year;
            st.month = (short)CurrentTime.Month;
            st.dayOfWeek = (short)CurrentTime.DayOfWeek;
            st.day = (short)CurrentTime.Day;
            st.hour = (short)CurrentTime.Hour;
            st.minute = (short)CurrentTime.Minute;
            st.second = (short)CurrentTime.Second;
            st.milliseconds = (short)CurrentTime.Millisecond;
            bool rt = SetLocalTime(ref st);//设置本机时间
            return rt;
        }
        /// <summary>
        /// 转换时间戳为C#时间
        /// </summary>
        /// <param name="timeStamp">时间戳 单位：毫秒</param>
        /// <returns>C#时间</returns>
        public DateTime ConvertTimeStampToDateTime(long timeStamp)
        {
            DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new System.DateTime(1970, 1, 1)); // 当地时区
                                                                                                        //DateTime dt = startTime.AddMilliseconds(timeStamp);  
            DateTime dt = startTime.AddSeconds(timeStamp + 8 * 3600);//+8时区
            return dt;
        }
    }
}

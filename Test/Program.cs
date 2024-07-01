using System.IO;

namespace Test
{
    public class Program
    {
        static void Main(string[] args)
        {
            //var tester = new TcpServerTester();
            //设置
            //_ =tester.SendCommandAsync(":SOURce1:FREQuency:VALue 140MHz");
            //_ = tester.SendCommandAsync(":SOURce1:BB:ARB:SRATe:VALue 256ksps");
            //Console.WriteLine();
            //_ = tester.SendCommandAsync(":SOURce1:BB:ARB:SWITCH On");
            //_ = tester.SendCommandAsync(":OUTPut1:RF:SWITCH On");

            var names = "d:\\WD03_256k_filted_Dealed.bin";
            //查询
            //_ = tester.SendCommandAsync(":SOURce1:BB:ARB:SWITCH ?");
            using (FileStream stream = new FileStream(names, FileMode.Open, FileAccess.Read))
            {
                if (stream != null)
                {
                    // 定义每次读取的字节长度
                    int readByteLength = 1000 * 32;
                    byte[] buffer = new byte[readByteLength];
                    Int16[] outdata = new short[readByteLength / 2];

                    uint maxValue = 0;
                    uint currentValue = 0;

                    // 循环读取文件，直到文件结束
                    int bytesRead;
                    while ((bytesRead = stream.Read(buffer, 0, readByteLength)) > 0)
                    {
                        // 根据读取的字节数调整outdata的大小
                        int shortCount = bytesRead / 2;
                        if (bytesRead < readByteLength)
                        {
                            outdata = new short[shortCount];
                        }

                        // 将字节缓冲区转换为16位有符号整数数组
                        Buffer.BlockCopy(buffer, 0, outdata, 0, bytesRead); // 元素个数缩小一半

                        // 获取绝对值最大值作为PEP参考值
                        for (int index = 0; index < shortCount; ++index)
                        {
                            currentValue = (uint)Math.Abs(outdata[index]);
                            if (maxValue < currentValue)
                            {
                                maxValue = currentValue;
                            }
                        }
                    }

                    Console.WriteLine("最大值: " + maxValue);

                    // 假设 SendCommandAsync 方法是异步的，且你需要传递读取到的数据
                    // 示例：_ = await tester.SendCommandAsync(":SOURce1:BB:ARB:SWITCH ?", buffer);

                    Console.WriteLine("文件读取成功.");
                }
                else
                {
                    Console.WriteLine("无法打开文件.");
                }
            }

        }
    }
}

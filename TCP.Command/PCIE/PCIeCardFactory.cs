using Lookdata;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCP.Command.PCIE
{
    public static class PCIeCardFactory
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public static List<PcieCard> pcieCards = new List<PcieCard>();
        public static Dictionary<int,PcieCard> CardParam = new Dictionary<int,PcieCard>();
        public static List<string> NewFilePathList = new List<string>();
        public static PcieCard CreatePcieCard(uint cardIndex, int productNumber)
        {
            switch (productNumber)
            {
                case 0x416160b:
                    Logger.Info("窄带" + productNumber.ToString());
                    return new NBCard(cardIndex,2);
                case 0x14200:
                    Logger.Info("宽带" + productNumber.ToString());
                    return new WBCard(cardIndex,2);
                default:
                    Logger.Error("尚未适配该型号:0x" + Convert.ToString(productNumber, 16) + "错误");
                    throw new ArgumentException("未知的产品编号");
            }
        }
        public static List<PcieCard> GetDeviceList()
        {


            for (uint i = 1; i < 2; i++)
            {
                dotNetQTDrv.QTSetRegs_i32(i, Regs.EnableReplay, 0);
                dotNetQTDrv.QTSetRegs_i32(i, Regs.EnableStreaming, 1);
                dotNetQTDrv.QTSetRegs_i32(i, Regs.EnableVfifo, 0);
                
                //try to open device
                uint nRet = (uint)dotNetQTDrv.QTOpenBoard(i);
                if (nRet == Error.RES_SUCCESS)//find a device
                {
                    var temp_number = 0;
                    dotNetQTDrv.QTGetRegs_i32(i, Regs.product_number, ref temp_number);
                    PcieCard card = PCIeCardFactory.CreatePcieCard(i, temp_number);
                    if (temp_number != 0x416160B)
                    {
                        card.DeviceName = "宽带";
                    }
                    else {
                        card.DeviceName = "窄带";
                    }
                    uint result = (uint)card.Initialize(i);
                    if (Error.RES_SUCCESS != result)
                    {
                        if (result == 2)
                        {
                            Logger.Info("时钟频率错误！");

                        }
                        else if (result == Error.RES_OPEN_FAILURE)
                        {
                            Logger.Info("打开板卡失败！");
                        }
                        else if (result == Error.RES_ERROR_ALLOC_BUF)
                        {
                            Logger.Info("内存不足！");
                        }
                        else if (result == Error.RES_ERROR_DDR_INIT_FAILED)
                        {
                            Logger.Info("板载内存初始化失败！");
                        }
                        else if (result == Error.RES_ERROR_PRODUCT_INFO_UNDEF)
                        {
                            Logger.Info("读取板卡信息失败！");
                        }
                        else
                        {
                            Logger.Info(string.Format("初始化失败！0x{0:x}", result));
                        }
                    }
                    else
                    {
                        pcieCards.Add(card);
                        if (temp_number == 0x416160B) 
                        {
                            CardParam.Add(2, card);
                            CardParam.Add(3, card);
                            CardParam.Add(4, card);
                            CardParam.Add(5, card);
                        }
                        else CardParam.Add(1, card);
                    }
                    #region 有点搓,后面再改吧

                    var temp_bforce = card.bForceIOdelay;
                    var temp_couple = card.CoupleType;
                    var temp_NOB = card.NOB;
                    var temp_devmaxsampleRate = card.DevMaxSampleRate;
                    var temp_adda = card.Adda_Revision;

                    dotNetQTDrv.QTGetRegs_i32(i, Regs.bForceIOdelay, ref temp_bforce);
                    dotNetQTDrv.QTGetRegs_i32(i, Regs.couple_type, ref temp_couple);
                    dotNetQTDrv.QTGetRegs_i32(i, Regs.lResolution, ref temp_NOB);
                    dotNetQTDrv.QTGetRegs_i32(i, Regs.SRate, ref temp_devmaxsampleRate);
                    dotNetQTDrv.QTGetRegs_i32(i, Regs.adda_revision, ref temp_adda);
                    card.ProductNumber = temp_number;
                    card.bForceIOdelay = temp_bforce;
                    card.CoupleType = temp_couple;
                    card.NOB = temp_NOB;
                    card.DevMaxSampleRate = temp_devmaxsampleRate;
                    card.Adda_Revision = temp_adda;

                    #endregion
                }
                else if (nRet == Error.RES_ERROR_PRODUCT_INFO_UNDEF)
                {
                    Logger.Error("发现设备，但无法读取设备信息");
                }
                else
                {
                    Logger.Error(string.Format("CardIndex={0:D} 发现未知错误0x{1}", i, Convert.ToString(nRet, 16)));
                }
            }

            if (pcieCards.Count == 0)
            {
                Logger.Error("未发现设备，请确认设备和驱动程序已正确安装", "错误");
                Logger.Error("未发现设备，请确认设备和驱动程序已正确安装");

            }

            return pcieCards;

        }
        //绝对顺序
        public static int ConvertChannelNumber(int channelNum)
        {
            return channelNum == 1 ? 0 : channelNum - 2;
        }

        public static async Task<string> ReadAndProcessBinFileAsync(string filePath, int proportion, bool insertZero)
        {
            // 读取二进制文件
            byte[] data = await File.ReadAllBytesAsync(filePath);

            // 将二进制数据解析为整数，每个数由两个字节组成
            int numSamples = data.Length / 2;
            short[] samples = new short[numSamples];

            Parallel.For(0, numSamples, i =>
            {
                samples[i] = BitConverter.ToInt16(data, i * 2);
            });

            Console.WriteLine($"样本数量：{samples.Length}");

            // 计算最大值
            short maxValue = samples.Max();
            Console.WriteLine("最大值:", maxValue);

            // 归一化和缩放
            short[] scaledSamples = new short[numSamples];

            Parallel.For(0, numSamples, i =>
            {
                double normalizedSample = (double)samples[i] / maxValue;
                scaledSamples[i] = (short)Math.Clamp(normalizedSample * proportion, short.MinValue, short.MaxValue);
            });

            // 插入0并还原为原来的格式，即2个有符号短整数字节
            byte[] packedData;
            if (insertZero)
            {
                packedData = new byte[scaledSamples.Length * 4];
                Parallel.For(0, scaledSamples.Length, i =>
                {
                    BitConverter.GetBytes(scaledSamples[i]).CopyTo(packedData, i * 4);
                    BitConverter.GetBytes((short)0).CopyTo(packedData, i * 4 + 2);
                });
            }
            else
            {
                packedData = new byte[scaledSamples.Length * 2];
                Parallel.For(0, scaledSamples.Length, i =>
                {
                    BitConverter.GetBytes(scaledSamples[i]).CopyTo(packedData, i * 2);
                });
            }

            // 生成新的文件名并保存在原目录中
            string directory = Path.GetDirectoryName(filePath);
            string baseName = Path.GetFileNameWithoutExtension(filePath);
            string fileExt = Path.GetExtension(filePath);
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string outputFileName = $"{baseName}_{proportion}_{timestamp}{fileExt}";
            string outputFilePath = Path.Combine(directory, outputFileName);

            // 保存到新文件
            await File.WriteAllBytesAsync(outputFilePath, packedData);

            Console.WriteLine($"归一化后的数据已保存到 {outputFilePath}");
            return outputFilePath;
        }

    }
}

using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using TCP.Command;

namespace Test
{
  public class Program
  {
    static void Main()
    {
      var str = ":OUTPut2:FREQuency:VALue?";
      CommandFactory.ParseCommand(str, 2);

    }

    static bool IsSignificantDifference(SortedList<double, (int, int)> ratioList, double ratio, double threshold)
    {
      foreach (var storedRatio in ratioList.Keys)
      {
        if (Math.Abs(storedRatio - ratio) < threshold)
        {
          return false;
        }
      }
      return true;
    }


    public static long interept(long a, long b)
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

    static (int, int, int) FindClosestFraction(double radio)
    {
      int maxLarge = 16384;
      int precision = 10;

      while (precision > 0)
      {
        for (int small = 1; small <= maxLarge; small++)
        {
          int large = (int)Math.Round(radio * small);
          if (large > 0 && large <= maxLarge)
          {
            double actualRatio = (double)large / small;
            if (Math.Abs(actualRatio - radio) < Math.Pow(10, -precision))
            {
              return (large, small, precision);
            }
          }
        }
        precision--;
      }

      // 如果找不到符合条件的值，返回一个默认值
      return (0, 0, 0);
    }

    public static (int, int, int) FindClosestFractionNew(double radio)
    {
      int maxLarge = 16384;
      int bestLarge = 16383;
      int bestSmall = 1;
      double closestDifference = double.MaxValue;

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
          }

          if (difference < Math.Pow(10, -10))
          {
            return (large, small, 10);
          }
        }
      }

      return (bestLarge, bestSmall, (int)Math.Floor(-Math.Log10(closestDifference)));
    }

    public static (int, int, int, bool, int, int, double, int) FindClosestFractionSS(double radio)
    {
      int maxLarge = 16384;
      int bestLarge = 0;
      int bestSmall = 0;
      int finalPrecision = 0;
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
        finalPrecision = 15; // 最大精度
        return (bestLarge, bestSmall, finalPrecision, precisionCompromised, originalLarge, originalSmall, originalRatio, originalPrecision);
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
            finalPrecision = (int)Math.Floor(-Math.Log10(difference));

            if (finalPrecision >= 10)
            {
              return (bestLarge, bestSmall, finalPrecision, precisionCompromised, originalLarge, originalSmall, originalRatio, originalPrecision);
            }
          }
        }
      }

      originalLarge = bestLarge;
      originalSmall = bestSmall;
      originalRatio = (double)bestLarge / bestSmall;
      originalPrecision = finalPrecision;

      for (int precision = finalPrecision - 1; precision >= 4; precision--)
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
                finalPrecision = precision;
                precisionCompromised = true;

                if (finalPrecision <= originalPrecision - 2)
                {
                  return (bestLarge, bestSmall, finalPrecision, precisionCompromised, originalLarge, originalSmall, originalRatio, originalPrecision);
                }
              }
            }
          }
        }
      }

      return (bestLarge, bestSmall, finalPrecision, precisionCompromised, originalLarge, originalSmall, originalRatio, originalPrecision);
    }

    public static (int, int) FindClosestFractionlast(double radio)
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

  }
}

namespace Test
{
    public class Program
    {
        static void Main(string[] args)
        {
            var tester = new TcpServerTester();
            //设置
            //_ =tester.SendCommandAsync(":SOURce1:FREQuency:VALue 140MHz");
            _ = tester.SendCommandAsync(":SOURce1:BB:ARB:SRATe:VALue 256ksps");
            //_ = tester.SendCommandAsync(":SOURce1:BB:ARB:SWITCH On");
            //_ = tester.SendCommandAsync(":OUTPut1:RF:SWITCH On");


            //查询
            //_ = tester.SendCommandAsync(":SOURce1:BB:ARB:SWITCH ?");
        }
    }
}

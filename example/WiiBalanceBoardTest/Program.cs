using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wii;

namespace WiiBalanceBoardTest
{
    class Program
    {
        static void Main(string[] args)
        {
            WiiBalanceBoard w = new WiiBalanceBoard();
            if(!w.connect())
            {
                Console.WriteLine("Device not found.");
                return;
            }
            w.setLED(true);
        }
    }
}

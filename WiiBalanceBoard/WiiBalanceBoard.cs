using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UsbHid;

namespace Wii
{
    //<summary>
    //簡易的にWiiBalanceBoardを管理するクラス。
    //Wiimote非対応。
    //</summary>
    public class WiiBalanceBoard
    {
        HidDevice device;
        StatusChanged s;

        //<summary>
        //WiiBalanceBoardと接続します。
        //成功した場合はtrueを返します。
        //</summary>
        public bool connect()
        {
            HidDeviceMgr mgr = new HidDeviceMgr();
            HidDeviceInfo[] infos = mgr.GetDeviceList();
            int index = 0;
            for (int i = 0; i != infos.Length; i++)
                if (infos[i].ProductID.ToString("x4") == "0306" &&
                    infos[i].VendorID.ToString("x4") == "057e")
                {
                    index = i;
                    break;
                }

            if (infos.Count() == 0) return false;
            device = mgr.TargetDevice(index);
            device.InterruptInStart(report);
            
            return true;
        }

        public static int cabRf = 0;
        public static int cabRb = 0;
        public static int cabLf = 0;
        public static int cabLb = 0;

        public static int rf = 0;
        public static int rb = 0;
        public static int lf = 0;
        public static int lb = 0;

        void report(byte[] data)
        {
            if ((uint)data[2] == 8)
            {
                set0kg();
                return;
            }
            rf = (int)data[3];
            rb = (int)data[5];
            lf = (int)data[7];
            lb = (int)data[9];

            if (cabRf == 0 && cabRb == 0 && cabLf == 0 && cabLb == 0)
                set0kg();

            int right = rf + rb - cabRf - cabRb;
            int left  = lf + lb - cabLf - cabLb;

            if (right < 0) right = 0;
            if (left < 0) left = 0;

            s?.Invoke(right, left);
        }

        public void set0kg()
        {
            setLED(false);
            Thread.Sleep(500);
            setLED(true);
            cabRf = rf;
            cabRb = rb;
            cabLf = lf;
            cabLb = lb;
        }

        public void setDelegate(StatusChanged status)
        {
            s = status;
        }

        public delegate void StatusChanged(int right,int left);

        //<summary>
        //重さの情報を報告するモードに変更します。
        //</summary>
        public void setReportModeWBC()
        {
            byte[] data = { 0x0 , 0x32 };
            device.SetOutputReport(0x12, data, true);
        }

        //<summary>
        //WiiBalanceBoardのLEDを設定します。
        //引数がtrueの時に光ります。
        //</summary>
        public void setLED(bool led)
        {
            byte[] data = new Byte[1];
            if (led) data[0] = 0x16; else data[0] = 0x0;
            device.SetOutputReport(0x11, data, true);
        }

        //<summary>
        //WiiBalanceBoardに情報を要求します。
        //</summary>
        public void requestStatus()
        {
            byte[] data = { 0x0 };
            device.SetOutputReport(0x15, data, true);
        }
    }
}

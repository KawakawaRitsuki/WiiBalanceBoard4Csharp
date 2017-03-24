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
            return true;
        }

        //<summary>
        //WiiBalanceBoardのLEDを設定します。
        //引数がtrueの時に光ります。
        //</summary>
        public void setLED(bool led)
        {
            byte[] l = new Byte[1];
            if (led) l[0] = 0x16; else l[0] = 0x0;
            device.SetOutputReport(0x11, l, true);
        }
    }
}

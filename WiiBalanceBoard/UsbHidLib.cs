/*	V-USB向け USB-HIDライブラリ
	『昼夜逆転』工作室 http://jsdiy.web.fc2.com
	V-USB http://www.obdev.at/products/vusb/index.html

【開発環境】
WindowsXP Pro. 32bit SP3 (SP1[OEM/CD-ROM] -> SP3[online update])
VisualStudio2008 Exp.Edition - C#
※本プログラムはWindowsに組み込まれているDLLを使用します。hid.dll, setupapi.dll, kernel32.dll

【更新履歴】
2012/04/02	ver 1.00	初版
2012/08/05	ver 1.10	64bit版OS対応のためSP_DEVICE_INTERFACE_DETAIL_DATA構造体を変更。
						上記開発環境でビルドしたものを Windows7 Pro. SP1 64bit で動作確認。
*/

/*	DLLのリファレンス

Win32 ライブラリとその他のライブラリの使用 [C-C#間の型の置換の詳細解説]
	http://msdn.microsoft.com/ja-jp/library/dd296860.aspx

[hid.dll]
Human Input Devices
	http://msdn.microsoft.com/en-us/library/ff543301(v=vs.85).aspx
Human Input Devices - Reference - HIDClass Support Routines
	http://msdn.microsoft.com/en-us/library/ff539956(v=vs.85).aspx

[setupapi.dll]
SetupAPI Reference
	http://msdn.microsoft.com/en-us/library/windows/hardware/ff550897(v=vs.85).aspx
SetupAPI Reference - Device Installation Functions - Public Device Installation Functions
	http://msdn.microsoft.com/en-us/library/windows/hardware/ff549791(v=vs.85).aspx
SetupAPI Reference - Device Installation Structures
	http://msdn.microsoft.com/en-us/library/windows/hardware/ff541316(v=vs.85).aspx
デバイス管理 - リファレンス - 関数
	http://msdn.microsoft.com/ja-jp/library/cc429159.aspx

[kernel32.dll]
MSDN Library - Windows Development - Data Access and Storage - Local File Systems -
File Management - File Management Reference - File Management Functions
	http://msdn.microsoft.com/en-us/library/aa364232(v=vs.85).aspx
MSDN Library - ... - File Management Functions - CreateFile
	http://msdn.microsoft.com/en-us/library/aa363858(v=vs.85).aspx
CreateFile
	http://msdn.microsoft.com/ja-jp/library/cc429198.aspx
*/

using System;
using System.Collections;
using System.Text;
using System.Runtime.InteropServices;	//DllImportAttribute
using Microsoft.Win32.SafeHandles;	//SafeFileHandle
using System.Collections.Specialized;	//StringCollection
using System.Threading;		//Thread, Sleep
using UsbHid;
using UsbHid_private;

//名前空間UsbHidからのみ参照し、その他の名前空間へは非公開とする
namespace UsbHid_private
{
	//定数
	#region
	
	//SDK - setupapi.h
	//SetupDiGetClassDevs(Flags)
	struct DIGCF
	{
		public const int
			DIGCF_DEFAULT			= 0x00000001,
			DIGCF_PRESENT			= 0x00000002,
			DIGCF_ALLCLASSES		= 0x00000004,
			DIGCF_PROFILE			= 0x00000008,
			DIGCF_DEVICEINTERFACE	= 0x00000010;
	}

	//SDK - winerror.h
	struct WINERROR
	{
		public const int
			ERROR_SUCCESS				= 0x00000000,	//この操作を正しく終了しました。 
			ERROR_INSUFFICIENT_BUFFER	= 0x0000007A;	//システム コールに渡されるデータ領域が小さすぎます。
		
		//その他
		//・構造体のcbSizeをセットしていなかった。
		//・DllImportにCharSet.Auto指定がなく、ANSI/Unicodeのうち期待と異なる方で関数が動作した。
		//→ ERROR_INVALID_USER_BUFFER 0x000006F8 要求された操作に対して与えられたバッファが無効です。
	}

	//SDK - winnt.h
	//CreateFile Flag
	struct CFF
	{
		public const int INVALID_HANDLE_VALUE = -1;

		//dwDesiredAccess 
		public const uint
			//device query	= 0
			GENERIC_READ	= 0x80000000,
			GENERIC_WRITE	= 0x40000000;

		//dwShareMode 
		public const uint
			FILE_SHARE_READ		= 0x00000001,
			FILE_SHARE_WRITE	= 0x00000002,
			FILE_SHARE_DELETE	= 0x00000004;

		//dwCreationDisposition 
		public const uint
			CREATE_NEW		= 1,
			CREATE_ALWAYS	= 2,
			OPEN_EXISTING	= 3,
			OPEN_ALWAYS		= 4,
			TRUNCATE_EXISTING = 5;

		//dwFlagsAndAttributes
		public const uint
			FILE_ATTRIBUTE_READONLY	= 0x00000001,
			FILE_ATTRIBUTE_HIDDEN	= 0x00000002,
			FILE_ATTRIBUTE_SYSTEM	= 0x00000004,
			//volume label ?		= 0x00000008,
			//FILE_ATTRIBUTE_DIRECTORY	= 0x00000010,
			FILE_ATTRIBUTE_ARCHIVE	= 0x00000020,
			//FILE_ATTRIBUTE_DEVICE	= 0x00000040,
			FILE_ATTRIBUTE_NORMAL	= 0x00000080,
			FILE_ATTRIBUTE_TEMPORARY		= 0x00000100,
			//FILE_ATTRIBUTE_SPARSE_FILE	= 0x00000200,
			//FILE_ATTRIBUTE_REPARSE_POINT	= 0x00000400,
			FILE_ATTRIBUTE_COMPRESSED		= 0x00000800,
			FILE_ATTRIBUTE_OFFLINE				= 0x00001000,
			FILE_ATTRIBUTE_NOT_CONTENT_INDEXED	= 0x00002000;
			//FILE_ATTRIBUTE_ENCRYPTED			= 0x00004000
	}

	//レポートタイプ
	enum ReportType
	{
		Feature,
		Input,
		Output
	};

	#endregion
	
	//構造体
	#region

	/*	使用せず
	struct SP_DEVINFO_DATA
	{
		public int		cbSize;
		public Guid		ClassGuid;
		public int		DevInst;
		public IntPtr	Reserved;
	}
	*/
	/*
	typedef struct _SP_DEVINFO_DATA {
	  DWORD     cbSize;
	  GUID      ClassGuid;
	  DWORD     DevInst;
	  ULONG_PTR Reserved;
	} SP_DEVINFO_DATA, *PSP_DEVINFO_DATA;
	*/

	struct SP_DEVICE_INTERFACE_DATA
	{
		public int		cbSize;
		public Guid		InterfaceClassGuid;
		public int		Flags;
		public IntPtr	Reserved;
	}
	/*
	typedef struct _SP_DEVICE_INTERFACE_DATA {
	  DWORD     cbSize;
	  GUID      InterfaceClassGuid;
	  DWORD     Flags;
	  ULONG_PTR Reserved;
	} SP_DEVICE_INTERFACE_DATA, *PSP_DEVICE_INTERFACE_DATA;
	*/

	//この構造体をnewすることはない。メモリブロックを読み取る上でのメンバ配置図として使う。
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	struct SP_DEVICE_INTERFACE_DETAIL_DATA
	{
		public int	cbSize;
		public char	DevicePath;

		//LayoutKind.Sequentialが指定されているので、プロパティはアンマネージド側から無視される
		public static int OffsetOfDevicePath { get { return sizeof(int); } }
		public static int SizeOfThis { get { return OffsetOfDevicePath + (IntPtr.Size == 4 ? 2 : 4); } }
		//SizeOfThis: 32bit-OSでは(4+2)byteを、64bit-OSでは(4+4)byteを返すようにする。
	}
	/*
	typedef struct _SP_DEVICE_INTERFACE_DETAIL_DATA {
	  DWORD cbSize;
	  TCHAR DevicePath[ANYSIZE_ARRAY];	//ANYSIZE_ARRAYの定義値は1
	} SP_DEVICE_INTERFACE_DETAIL_DATA, *PSP_DEVICE_INTERFACE_DETAIL_DATA;
	*/

	struct HIDD_ATTRIBUTES
	{
		public int		Size;
		public ushort	VendorID;
		public ushort	ProductID;
		public ushort	VersionNumber;
	}
	/*
	typedef struct _HIDD_ATTRIBUTES {
	  ULONG  Size;
	  USHORT VendorID;
	  USHORT ProductID;
	  USHORT VersionNumber;
	} HIDD_ATTRIBUTES, *PHIDD_ATTRIBUTES;
	*/

	#endregion

	//デバイスの読み書き操作
	class DeviceAccess
	{
		//[DllImport("kernel32.dll")]
		#region

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr CreateFile(
			[MarshalAs(UnmanagedType.LPTStr)]
			string lpFileName,
			uint dwDesiredAccess,
			uint dwShareMode,
			IntPtr lpSecurityAttributes,
			uint dwCreationDisposition,
			uint dwFlagsAndAttributes,
			IntPtr hTemplateFile
			);
		/*
		HANDLE WINAPI CreateFile(
		  __in      LPCTSTR lpFileName,
		  __in      DWORD dwDesiredAccess,
		  __in      DWORD dwShareMode,
		  __in_opt  LPSECURITY_ATTRIBUTES lpSecurityAttributes,
		  __in      DWORD dwCreationDisposition,
		  __in      DWORD dwFlagsAndAttributes,
		  __in_opt  HANDLE hTemplateFile
		);
		*/

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool ReadFile(
			SafeFileHandle hFile,
			SafeGlobalMemoryPtr lpBuffer,	//IntPtr
			int nNumberOfBytesToRead,
			ref int lpNumberOfBytesRead,
			IntPtr lpOverlapped		//ref LPOVERLAPPED
			);
		/*
		BOOL WINAPI ReadFile(
		  __in         HANDLE hFile,
		  __out        LPVOID lpBuffer,
		  __in         DWORD nNumberOfBytesToRead,
		  __out_opt    LPDWORD lpNumberOfBytesRead,
		  __inout_opt  LPOVERLAPPED lpOverlapped
		);
		*/

		#endregion

		//問い合わせモードでデバイスをオープンする
		public static SafeFileHandle OpenQueryMode(string devicePath) { return Open(devicePath, 0); }

		//デバイスをオープンする
		public static SafeFileHandle Open(string devicePath, uint accessMode)
		{
			IntPtr pHandle = CreateFile(devicePath, accessMode, CFF.FILE_SHARE_READ | CFF.FILE_SHARE_WRITE,
				IntPtr.Zero, CFF.OPEN_EXISTING, 0, IntPtr.Zero);

			//CreateFile() が INVALID_HANDLE_VALUE を返した場合でも new SafeFileHandle(pHandle) は成功する。
			//ただし SafeFileHandle.IsInvalid は true である。
			SafeFileHandle hHidDev = new SafeFileHandle(pHandle, true);
			return hHidDev;
		}

		//デバイスから読み込む
		public static bool ReadFile(SafeFileHandle hFile, SafeGlobalMemoryPtr lpBuffer, int nNumberOfBytesToRead, ref int lpNumberOfBytesRead)
		{
			bool isOK = ReadFile(hFile, lpBuffer, nNumberOfBytesToRead, ref lpNumberOfBytesRead, IntPtr.Zero);
			return isOK;
		}

	}	//end of class DeviceAccess

	//Marshal.AllocHGlobal()したグローバル領域のメモリを確実に開放するためのクラス
	class SafeGlobalMemoryPtr : SafeHandleZeroOrMinusOneIsInvalid
	{
		public SafeGlobalMemoryPtr(int size)
			: base(true)
		{
			try
			{
				IntPtr pHandle = Marshal.AllocHGlobal(size);
				SetHandle(pHandle);
			}
			catch { SetHandle(IntPtr.Zero); }	//もちろん「IsInvalid==false」となる
		}
		
		protected override bool ReleaseHandle()
		{
			//handleフィールドを直接使用しても同じことだが、一応DangerousGetHandle()を使う
			IntPtr pHandle = DangerousGetHandle();
			Marshal.FreeHGlobal(pHandle);
			return true;
		}

	}	//end of class SafeGlobalMemoryPtr

	//インタラプト転送(IN)を受信するスレッド制御クラス
	class InterruptInCtrl
	{
		//スレッドに渡すパラメータ
		private struct TThParam
		{
			public string devicePath;
			public int reportLength;
			public InterruptInCB userMethod;
		}

		private TThParam thParam;
		private Thread thObj;
		
		//コンストラクタ
		public InterruptInCtrl(string devicePath, int reportLength)
		{
			thParam.devicePath = devicePath;
			thParam.reportLength = reportLength;
			thParam.userMethod = null;

			thObj = null;
		}

		//スレッドを開始する
		/*
		・IsBackground=trueは必須。
			メインスレッド終了に伴ってこちらのスレッドも終了するようにする。
			Abort()でスレッドを終了できるようにする。
		・理由：
			ReadFile()でスレッドがブロック中のとき、メインスレッドが終了してもこのスレッドは残り、
			アプリが終了できないので。その状態のときはAbort()も効果がない。
		*/
		public bool Start(InterruptInCB callbackMethod)
		{
			if (thObj != null) return false;	//二重実行禁止
			if (callbackMethod == null) return false;
			thParam.userMethod = callbackMethod;

			thObj = new Thread(ReadThread);
			thObj.IsBackground = true;
			thObj.Start(thParam);
			Thread.Sleep(1);	//Start()した瞬間にIsAliveを見ても正しい状態が得られないので間(ま)を置く
			return thObj.IsAlive;
		}

		//スレッドを停止／破棄する
		/*
		・ReadFile()でスレッドがブロック中かもしれないのでJoin()してはいけない。
		・ブロック中だろうがなかろうが構わず強制終了させる。
		・スレッドで確保したハンドルとメモリはSafeFileHandle,SafeGlobalMemoryPtrにより解放されるので心配無用。
		*/
		public void Stop()
		{
			if (thObj != null) thObj.Abort();
			thObj = null;
		}

		//【スレッド】インタラプト転送(IN)を受信する
		/*
		ReadFile()は受信完了するまでスレッドをブロックする
		・reqThreadStop=trueとなってもReadFile()が完了しない限りwhile()を抜けることはできない。
			途切れることなくデータを受信するような動作をしているなら問題ないが、
			いつ来るか分からない（来ないかもしれない）データを待ち続けるような動作をしているなら、
			いつまでもwhile()を抜けることができないということ。
		・そのような待ち続けの動作があることを考慮すると、タイムアウトを設定するのは適切と言えない。
			タイムアウト発生でReadFile()を終了した瞬間に、運悪くデータが送信されるかもしれないので。
			例えすぐに次のReadFile()を開始しようとも、それを受信することはできない。
		
		ReadFile()がアンマネージコードであるためか、Abort()してもfinally{}を実行せずにスレッドが破棄される。
		すると、Marshal.AllocHGlobal()で確保したメモリが開放できない。
		解決策：確保したメモリのIntPtrを、SafeFileHandle同様の仕組みで扱えるようにする。
				→SafeFileHandle同様の仕組みの中でメモリを確保する。
		*/
		/*	コールバック関数への戻り値
			成功：レポートIDを含むInputReport（byte配列）
			失敗：成功時より長さが足りないか、長さ0のbyte配列（nullではない）
		*/
		private static void ReadThread(object oParam)
		{
			TThParam thParam = (TThParam)oParam;	//内容は妥当であるものとする

			SafeFileHandle hHidDev = DeviceAccess.Open(thParam.devicePath, CFF.GENERIC_READ);
			if (hHidDev.IsInvalid) { hHidDev.Close(); return; }

			int bufSize = thParam.reportLength;
			SafeGlobalMemoryPtr pGlobalBuf = new SafeGlobalMemoryPtr(bufSize);
			if (pGlobalBuf.IsInvalid) { pGlobalBuf.Close(); hHidDev.Close(); return; }
			
			try
			{
				int readLength = 0;
				byte[] rcvBytes;
				while (0 < bufSize)		//無限ループする条件なら何でもよい
				{
					rcvBytes = null;
					if (DeviceAccess.ReadFile(hHidDev, pGlobalBuf, bufSize, ref readLength))
					{
						rcvBytes = new byte[readLength];
						if (0 < readLength)
							Marshal.Copy(pGlobalBuf.DangerousGetHandle(), rcvBytes, 0, readLength);
						thParam.userMethod(rcvBytes);
					}

					/*	debug
					for (int i = 0; rcvBytes != null && i < rcvBytes.Length; i++)
						Console.Write("{0:X2} ", rcvBytes[i]);
					Console.WriteLine();
					//*/
				}
			}
			catch { }
			
			pGlobalBuf.Close();
			hHidDev.Close();
		}

	}	//end of class InterruptInCtrl

}	//end of namespace

namespace UsbHid
{
	//InterruptInCtrlクラスでコールバック関数を呼ぶためのデリゲート
	delegate void InterruptInCB(byte[] data);

	//構造体
	#region
	
	[StructLayout(LayoutKind.Sequential)]
	struct HIDP_CAPS
	{
		public ushort	Usage;
		public ushort	UsagePage;
		public ushort	InputReportByteLength;
		public ushort	OutputReportByteLength;
		public ushort	FeatureReportByteLength;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
		public ushort[]	Reserved;
		public ushort	NumberLinkCollectionNodes;
		public ushort	NumberInputButtonCaps;
		public ushort	NumberInputValueCaps;
		public ushort	NumberInputDataIndices;
		public ushort	NumberOutputButtonCaps;
		public ushort	NumberOutputValueCaps;
		public ushort	NumberOutputDataIndices;
		public ushort	NumberFeatureButtonCaps;
		public ushort	NumberFeatureValueCaps;
		public ushort	NumberFeatureDataIndices;
	}
	/*
	typedef struct _HIDP_CAPS {
	  USAGE  Usage;
	  USAGE  UsagePage;
	  USHORT InputReportByteLength;
	  USHORT OutputReportByteLength;
	  USHORT FeatureReportByteLength;
	  USHORT Reserved[17];
	  USHORT NumberLinkCollectionNodes;
	  USHORT NumberInputButtonCaps;
	  USHORT NumberInputValueCaps;
	  USHORT NumberInputDataIndices;
	  USHORT NumberOutputButtonCaps;
	  USHORT NumberOutputValueCaps;
	  USHORT NumberOutputDataIndices;
	  USHORT NumberFeatureButtonCaps;
	  USHORT NumberFeatureValueCaps;
	  USHORT NumberFeatureDataIndices;
	} HIDP_CAPS, *PHIDP_CAPS;
	*/

	#endregion

	//HIDデバイスの基本情報
	class HidDeviceInfo
	{
		public string DevicePath { get; private set; }

		public ushort VendorID { get; private set; }	//V-USB: USB_CFG_VENDOR_ID (ex. 0x16c0)
		public ushort ProductID { get; private set; }	//V-USB: USB_CFG_DEVICE_ID (ex. 0x05df)
		public ushort VersionNumber { get; private set; }	//V-USB: USB_CFG_DEVICE_VERSION (ex. 0x0100 -> Ver. 1.00)
		public string VenderName { get; private set; }	//V-USB: USB_CFG_VENDOR_NAME (ex. "obdev.at")
		public string DeviceName { get; private set; }	//V-USB: USB_CFG_DEVICE_NAME (ex. "MyDevice")
		public string SerialNumber { get; private set; }	//V-USB: USB_CFG_SERIAL_NUMBER (option)

		public HidDeviceInfo(string devicePath, ref HIDD_ATTRIBUTES HiddAttributes, string venderName, string deviceName, string serialNumber)
		{
			DevicePath = devicePath;

			VendorID = HiddAttributes.VendorID;
			ProductID = HiddAttributes.ProductID;
			VersionNumber = HiddAttributes.VersionNumber;
			VenderName = venderName;
			DeviceName = deviceName;
			SerialNumber = serialNumber;
		}

	}	//end of class HidDeviceInfo

	//PCに接続されたHIDデバイス全体に関する操作
	class HidDeviceMgr
	{
		//[DllImport("hid.dll")]
		#region

		[DllImport("hid.dll", SetLastError = true)]
		private static extern void HidD_GetHidGuid(ref Guid HidGuid);
		/*
		void __stdcall HidD_GetHidGuid(
		  __out  LPGUID HidGuid
		);
		*/

		[DllImport("hid.dll", SetLastError = true)]
		private static extern bool HidD_GetAttributes(
			SafeFileHandle HidDeviceObject,
			ref HIDD_ATTRIBUTES Attributes
			);
		/*
		BOOLEAN __stdcall HidD_GetAttributes(
		  __in   HANDLE HidDeviceObject,
		  __out  PHIDD_ATTRIBUTES Attributes
		);
		*/

		[DllImport("hid.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern bool HidD_GetManufacturerString(
			SafeFileHandle HidDeviceObject,
			[MarshalAs(UnmanagedType.LPTStr)]
			StringBuilder Buffer,
			int BufferLength
			);
		/*
		BOOLEAN __stdcall HidD_GetManufacturerString(
		  __in   HANDLE HidDeviceObject,
		  __out  PVOID Buffer,
		  __in   ULONG BufferLength
		);
		*/

		[DllImport("hid.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern bool HidD_GetProductString(
			SafeFileHandle HidDeviceObject,
			[MarshalAs(UnmanagedType.LPTStr)]
			StringBuilder Buffer,
			int BufferLength
			);
		/*
		BOOLEAN __stdcall HidD_GetProductString(
		  __in   HANDLE HidDeviceObject,
		  __out  PVOID Buffer,
		  __in   ULONG BufferLength
		);
		*/

		[DllImport("hid.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern bool HidD_GetSerialNumberString(
			SafeFileHandle HidDeviceObject,
			[MarshalAs(UnmanagedType.LPTStr)]
			StringBuilder Buffer,
			int BufferLength
			);
		/*
		BOOLEAN __stdcall HidD_GetSerialNumberString(
		  __in   HANDLE HidDeviceObject,
		  __out  PVOID Buffer,
		  __in   ULONG BufferLength
		);
		*/

		#endregion

		//[DllImport("setupapi.dll")]
		#region
		
		[DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr SetupDiGetClassDevs(
			ref Guid ClassGuid,
			[MarshalAs(UnmanagedType.LPTStr)]
			string Enumerator,
			IntPtr hwndParent,
			int Flags
			);
		/*
		HDEVINFO SetupDiGetClassDevs(
		  __in_opt  const GUID *ClassGuid,
		  __in_opt  PCTSTR Enumerator,
		  __in_opt  HWND hwndParent,
		  __in      DWORD Flags
		);
		*/

		[DllImport("setupapi.dll", SetLastError = true)]
		private static extern bool SetupDiEnumDeviceInterfaces(
			IntPtr DeviceInfoSet,
			IntPtr _Null,	//ref SP_DEVINFO_DATA DeviceInfoData,
			ref Guid InterfaceClassGuid,
			int MemberIndex,
			ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData
			);
		/*
		BOOL SetupDiEnumDeviceInterfaces(
		  __in      HDEVINFO DeviceInfoSet,
		  __in_opt  PSP_DEVINFO_DATA DeviceInfoData,
		  __in      const GUID *InterfaceClassGuid,
		  __in      DWORD MemberIndex,
		  __out     PSP_DEVICE_INTERFACE_DATA DeviceInterfaceData
		);
		*/

		[DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern bool SetupDiGetDeviceInterfaceDetail(
			IntPtr DeviceInfoSet,
			ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
			IntPtr DeviceInterfaceDetailData,	//ref SP_DEVICE_INTERFACE_DETAIL_DATA
			int DeviceInterfaceDetailDataSize,
			ref int RequiredSize,
			IntPtr DeviceInfoData	//ref SP_DEVINFO_DATA
			);
		/*
		BOOL SetupDiGetDeviceInterfaceDetail(
		  __in       HDEVINFO DeviceInfoSet,
		  __in       PSP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
		  __out_opt  PSP_DEVICE_INTERFACE_DETAIL_DATA DeviceInterfaceDetailData,
		  __in       DWORD DeviceInterfaceDetailDataSize,
		  __out_opt  PDWORD RequiredSize,
		  __out_opt  PSP_DEVINFO_DATA DeviceInfoData
		);
		*/

		[DllImport("setupapi.dll", SetLastError = true)]
		private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);
		/*
		BOOL SetupDiDestroyDeviceInfoList(
		  __in  HDEVINFO DeviceInfoSet
		);
		*/

		#endregion

		private HidDeviceInfo[] hidDevInfoList;		//GetDeviceList()の結果

		//コンストラクタ
		public HidDeviceMgr()
		{
			hidDevInfoList = new HidDeviceInfo[0];
		}

		//デバイスを検索し、基本情報を取得する
		public HidDeviceInfo[] GetDeviceList()
		{
			HidDeviceInfo[] devInfoList;
			try { devInfoList = GetDeviceList(true, 0, 0); }
			catch { devInfoList = new HidDeviceInfo[0]; }
			hidDevInfoList = devInfoList;
			return devInfoList;
		}
		//
		public HidDeviceInfo[] GetDeviceList(int vendorID, int productID)
		{
			HidDeviceInfo[] devInfoList;
			try { devInfoList = GetDeviceList(false, vendorID, productID); }
			catch { devInfoList = new HidDeviceInfo[0]; }
			hidDevInfoList = devInfoList;
			return devInfoList;
		}
		//
		private HidDeviceInfo[] GetDeviceList(bool isFindAll, int vendorID, int productID)
		{
			//デバイス情報セットを取得する
			Guid hidGuid = new Guid();
			HidD_GetHidGuid(ref hidGuid);
			IntPtr hDevInfoSet = SetupDiGetClassDevs(
				ref hidGuid, null, IntPtr.Zero, DIGCF.DIGCF_DEVICEINTERFACE | DIGCF.DIGCF_PRESENT);

			//各デバイス情報からデバイスのパス名を取得する
			SP_DEVICE_INTERFACE_DATA DeviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();
			DeviceInterfaceData.cbSize = Marshal.SizeOf(DeviceInterfaceData);
			int MemberIndex = 0;
			StringCollection scDevicePath = new StringCollection();
			while (SetupDiEnumDeviceInterfaces(hDevInfoSet, IntPtr.Zero, ref hidGuid, MemberIndex++, ref DeviceInterfaceData))
			{
				string devicePath = GetDevicePath(hDevInfoSet, ref DeviceInterfaceData);
				if (string.IsNullOrEmpty(devicePath) || scDevicePath.Contains(devicePath)) continue;
				scDevicePath.Add(devicePath);
			}

			//デバイス情報セットを破棄する
			SetupDiDestroyDeviceInfoList(hDevInfoSet);
			
			//目的のデバイスを検索する
			HIDD_ATTRIBUTES HiddAttributes = new HIDD_ATTRIBUTES();
			HiddAttributes.Size = Marshal.SizeOf(HiddAttributes);
			ArrayList alHidDevInfos = new ArrayList();
			foreach (string devicePath in scDevicePath)
			{
				//問い合わせモードでデバイスをオープンする
				SafeFileHandle hHidDev = DeviceAccess.OpenQueryMode(devicePath);
				if (hHidDev.IsInvalid) continue;

				//デバイスの属性を取得する
				if (HidD_GetAttributes(hHidDev, ref HiddAttributes))
				{
					//目的のデバイスであれば追加情報を取得する
					if (isFindAll
						|| (HiddAttributes.VendorID == vendorID && HiddAttributes.ProductID == productID))
					{
						string devVenderName, devDeviceName, devSerialNumber;
						GetAdditionalInfo(hHidDev, out devVenderName, out devDeviceName, out devSerialNumber);
						HidDeviceInfo hidDevInfo = new HidDeviceInfo(devicePath, ref HiddAttributes, devVenderName, devDeviceName, devSerialNumber);
						alHidDevInfos.Add(hidDevInfo);
					}
				}
				
				//クローズ
				hHidDev.Close();
			}
			
			//取得したデバイス情報を配列に格納する（長さ0以上の配列となる）
			HidDeviceInfo[] hidDevInfos = new HidDeviceInfo[alHidDevInfos.Count];
			alHidDevInfos.CopyTo(hidDevInfos);
			
			return hidDevInfos;
		}

		//デバイスのパス名を取得する
		private string GetDevicePath(IntPtr hDevInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData)
		{
			//これから取得しようとするDeviceInterfaceDetail構造体の必要サイズを取得する
			//・isSuccessはfalseでよい。ERROR_INSUFFICIENT_BUFFERが発生する。
			int requiredSize = 0;
			bool isSuccess = SetupDiGetDeviceInterfaceDetail(hDevInfoSet, ref DeviceInterfaceData,
				IntPtr.Zero, 0, ref requiredSize, IntPtr.Zero);
			int errorCodeWin32 = Marshal.GetLastWin32Error();
			if (errorCodeWin32 != WINERROR.ERROR_INSUFFICIENT_BUFFER || requiredSize == 0) return null;

			//DeviceInterfaceDetail構造体を取得する
			IntPtr pDeviceInterfaceDetailData = Marshal.AllocHGlobal(requiredSize);		//構造体のバッファを確保する
			Marshal.WriteInt32(pDeviceInterfaceDetailData, 0, SP_DEVICE_INTERFACE_DETAIL_DATA.SizeOfThis);	//0はcbSizeのオフセットを指す
			isSuccess = SetupDiGetDeviceInterfaceDetail(hDevInfoSet, ref DeviceInterfaceData,
				pDeviceInterfaceDetailData, requiredSize, ref requiredSize, IntPtr.Zero);
			if (!isSuccess)
			{
				Marshal.FreeHGlobal(pDeviceInterfaceDetailData);	//バッファ開放
				return null;
			}

			//デバイスのパス名を取得する
			//・IntPtrが指すポインタの値を直接加減算する方法はないので、
			//	ポインタをint型の具体値に変換し、DevicePathメンバへのオフセットを加算して、IntPtr型に戻す。
			int nPtrValue = pDeviceInterfaceDetailData.ToInt32() + SP_DEVICE_INTERFACE_DETAIL_DATA.OffsetOfDevicePath;
			IntPtr pDevicePath = new IntPtr(nPtrValue);
			string devicePath = Marshal.PtrToStringAuto(pDevicePath);
			Marshal.FreeHGlobal(pDeviceInterfaceDetailData);	//バッファ開放

			return devicePath;
		}

		//デバイスの追加情報を取得する
		private void GetAdditionalInfo(SafeFileHandle hHidDev, out string devVenderName, out string devDeviceName, out string devSerialNumber)
		{
			const int STRBUF_LENGTH = 1024;
			StringBuilder strBuf = new StringBuilder(STRBUF_LENGTH);
			bool isSuccess;

			strBuf = strBuf.Remove(0, strBuf.Length);
			isSuccess = HidD_GetManufacturerString(hHidDev, strBuf, STRBUF_LENGTH);
			devVenderName = isSuccess ? strBuf.ToString() : "";

			strBuf = strBuf.Remove(0, strBuf.Length);
			isSuccess = HidD_GetProductString(hHidDev, strBuf, STRBUF_LENGTH);
			devDeviceName = isSuccess ? strBuf.ToString() : "";

			strBuf = strBuf.Remove(0, strBuf.Length);
			isSuccess = HidD_GetSerialNumberString(hHidDev, strBuf, STRBUF_LENGTH);
			devSerialNumber = isSuccess ? strBuf.ToString() : "";
		}

		//操作するデバイスを決める
		//引数：	hidDevInfoList[]のインデックス
		public HidDevice TargetDevice(int index)
		{
			if (index < 0 || hidDevInfoList.Length <= index) return null;
			HidDevice hidDev = new HidDevice(hidDevInfoList[index]);
			return hidDev;
		}

	}	//end of class HidDeviceMgr

	//HIDデバイス個別の操作
	class HidDevice
	{
		//[DllImport("hid.dll")]
		#region

		[DllImport("hid.dll", SetLastError = true)]
		private static extern bool HidD_GetPreparsedData(
			SafeFileHandle HidDeviceObject,
			out IntPtr PreparsedData
			);
		/*
		BOOLEAN __stdcall HidD_GetPreparsedData(
		  __in   HANDLE HidDeviceObject,
		  __out  PHIDP_PREPARSED_DATA *PreparsedData
		);
			PHIDP_PREPARSED_DATAについて http://msdn.microsoft.com/en-us/library/ff543586(v=vs.85).aspx
			The internal structure of a _HIDP_PREPARSED_DATA structure is reserved for internal system use.
		*/

		[DllImport("hid.dll", SetLastError = true)]
		private static extern bool HidD_FreePreparsedData(IntPtr PreparsedData);
		/*
		BOOLEAN __stdcall HidD_FreePreparsedData(
		  __in  PHIDP_PREPARSED_DATA PreparsedData
		);
		*/

		[DllImport("hid.dll", SetLastError = true)]
		private static extern bool HidP_GetCaps(
			IntPtr PreparsedData,
			ref HIDP_CAPS Capabilities
			);
		/*
		NTSTATUS __stdcall HidP_GetCaps(
		  __in   PHIDP_PREPARSED_DATA PreparsedData,
		  __out  PHIDP_CAPS Capabilities
		);
		*/

		[DllImport("hid.dll", SetLastError = true)]
		private static extern bool HidD_GetFeature(
			SafeFileHandle HidDeviceObject,
			IntPtr ReportBuffer,
			int ReportBufferLength
			);
		/*
		BOOLEAN __stdcall HidD_GetFeature(
		  __in   HANDLE HidDeviceObject,
		  __out  PVOID ReportBuffer,
		  __in   ULONG ReportBufferLength
		);
		*/

		[DllImport("hid.dll", SetLastError = true)]
		private static extern bool HidD_SetFeature(
			SafeFileHandle HidDeviceObject,
			IntPtr ReportBuffer,
			int ReportBufferLength
			);
		/*
		BOOLEAN __stdcall HidD_SetFeature(
		  __in  HANDLE HidDeviceObject,
		  __in  PVOID ReportBuffer,
		  __in  ULONG ReportBufferLength
		);
		*/

		[DllImport("hid.dll", SetLastError = true)]
		private static extern bool HidD_GetInputReport(
			SafeFileHandle HidDeviceObject,
			IntPtr ReportBuffer,
			int ReportBufferLength
			);
		/*
		BOOLEAN __stdcall HidD_GetInputReport(
		  __in   HANDLE HidDeviceObject,
		  __out  PVOID ReportBuffer,
		  __in   ULONG ReportBufferLength
		);
		*/

		[DllImport("hid.dll", SetLastError = true)]
		private static extern bool HidD_SetOutputReport(
			SafeFileHandle HidDeviceObject,
			IntPtr ReportBuffer,
			int ReportBufferLength
			);
		/*
		BOOLEAN __stdcall HidD_SetOutputReport(
		  __in  HANDLE HidDeviceObject,
		  __in  PVOID ReportBuffer,
		  __in  ULONG ReportBufferLength
		);
		*/

		#endregion

		private readonly HidDeviceInfo devInfo;
		private HIDP_CAPS tCaps;
		private readonly InterruptInCtrl intrInCtrl;
		
		//コンストラクタ
		public HidDevice(HidDeviceInfo devInfo)
		{
			this.devInfo = devInfo;
			InitDevInfo();

			intrInCtrl = new InterruptInCtrl(devInfo.DevicePath, tCaps.InputReportByteLength);
		}

		//デバイスの基本情報をセットする
		private bool InitDevInfo()
		{
			SafeFileHandle hHidDev = DeviceAccess.OpenQueryMode(devInfo.DevicePath);
			if (hHidDev.IsInvalid) return false;

			//各種レポートのバッファサイズを取得する
			bool isOK = false;
			IntPtr pPreparsedData;
			if (HidD_GetPreparsedData(hHidDev, out pPreparsedData))
			{
				tCaps = new HIDP_CAPS();
				isOK = HidP_GetCaps(pPreparsedData, ref tCaps);
				HidD_FreePreparsedData(pPreparsedData);
			}

			//その他、必要な情報があればここで処理する
			//

			hHidDev.Close();
			return isOK;
		}

		//コントロール転送でレポートを取得する
		/*
		引数
			reportType:	Featureレポート、Inputレポートの区別。
			reportID:	レポートID。
			reportLength:	取得するレポートの長さ。レポートIDの分1byteを含めた長さであること。
			cutReportIdByte:	取得したbyte配列からレポートIDの要素をカットするかどうか。
		*/
		private byte[] GetReport(ReportType reportType, byte reportID, int reportLength, bool cutReportIdByte)
		{
			if (!(reportType == ReportType.Feature || reportType == ReportType.Input)) return null;
			
			SafeFileHandle hHidDev = DeviceAccess.Open(devInfo.DevicePath, CFF.GENERIC_READ);
			if (hHidDev.IsInvalid) return null;
			
			IntPtr pGlobalBuf = IntPtr.Zero;
			byte[] rBuf = null;
			try
			{
				pGlobalBuf = Marshal.AllocHGlobal(reportLength);
				Marshal.WriteByte(pGlobalBuf, 0, reportID);
				if (HiddGetReport(reportType, hHidDev, pGlobalBuf, reportLength))
				{
					IntPtr pStart = cutReportIdByte ? new IntPtr(pGlobalBuf.ToInt32() + 1) : pGlobalBuf;
					rBuf = new byte[cutReportIdByte ? reportLength - 1 : reportLength];
					Marshal.Copy(pStart, rBuf, 0, rBuf.Length);
				}
			}
			catch { rBuf = null; }
			finally { Marshal.FreeHGlobal(pGlobalBuf); }
			
			hHidDev.Close();
			return rBuf;
		}
		//GetReport()の一部
		private bool HiddGetReport(ReportType reportType, SafeFileHandle hHidDev, IntPtr pGlobalBuf, int reportLength)
		{
			switch (reportType)
			{
			case ReportType.Feature:
				return HidD_GetFeature(hHidDev, pGlobalBuf, reportLength);
			case ReportType.Input:
				return HidD_GetInputReport(hHidDev, pGlobalBuf, reportLength);
			default:	//ReportType.Output
				break;
			}
			return false;
		}

		//コントロール転送でレポートをセットする
		/*
		引数
			reportType:	Featureレポート、Outputレポートの区別。
			reportID:	レポートID。
			dataBytes:	送信するデータ。下記備考参照。
			addReportIdByte:	dataBytesの先頭にレポートIDを加えるかどうか。
		備考
			addReportIdByteをtrueとするとき、dataBytesはレポートIDを含まないbyte配列であること。
			addReportIdByteをfalseとするとき、dataBytesはレポートIDを含んだbyte配列であること。
		*/
		private bool SetReport(ReportType reportType, byte reportID, byte[] dataBytes, bool addReportIdByte)
		{
			if (!(reportType == ReportType.Feature || reportType == ReportType.Output)) return false;
			
			SafeFileHandle hHidDev = DeviceAccess.Open(devInfo.DevicePath, CFF.GENERIC_WRITE);
			if (hHidDev.IsInvalid) return false;
			
			int reportLength = dataBytes.Length + (addReportIdByte ? 1 : 0);
			IntPtr pGlobalBuf = IntPtr.Zero;
			bool isOK = false;
			try
			{
				pGlobalBuf = Marshal.AllocHGlobal(reportLength);
				IntPtr pStart = addReportIdByte ? new IntPtr(pGlobalBuf.ToInt32() + 1) : pGlobalBuf;
				Marshal.Copy(dataBytes, 0, pStart, dataBytes.Length);
				Marshal.WriteByte(pGlobalBuf, 0, reportID);
				isOK = HiddSetReport(reportType, hHidDev, pGlobalBuf, reportLength);
			}
			catch { }
			finally { Marshal.FreeHGlobal(pGlobalBuf); }
			
			hHidDev.Close();
			return isOK;
		}
		//SetReport()の一部
		private bool HiddSetReport(ReportType reportType, SafeFileHandle hHidDev, IntPtr pGlobalBuf, int reportLength)
		{
			switch (reportType)
			{
			case ReportType.Feature:
				return HidD_SetFeature(hHidDev, pGlobalBuf, reportLength);
			case ReportType.Output:
				return HidD_SetOutputReport(hHidDev, pGlobalBuf, reportLength);
			default:	//ReportType.Input
				break;
			}
			return false;
		}
		
		//コントロール転送でFeatureReportを取得する
		//引数	dataLength:	取得するデータの長さ。レポートIDを含まない長さ。
		public byte[] GetFeatureReport(int dataLength) { return GetFeatureReport(0, dataLength + 1, true); }
		public byte[] GetFeatureReport(byte reportID, int reportLength, bool cutReportIdByte)
		{
			return GetReport(ReportType.Feature, reportID, reportLength, cutReportIdByte);
		}

		//コントロール転送でFeatureReportをセットする
		//引数	dataBytes:	送信するデータ。レポートIDを含まないデータであること。
		public bool SetFeatureReport(byte[] dataBytes) { return SetFeatureReport(0, dataBytes, true); }
		public bool SetFeatureReport(byte reportID, byte[] dataBytes, bool addReportIdByte)
		{
			return SetReport(ReportType.Feature, reportID, dataBytes, addReportIdByte);
		}

		//コントロール転送でInputReportを取得する
		//引数	dataLength:	取得するデータの長さ。レポートIDを含まない長さ。
		public byte[] GetInputReport(int dataLength) { return GetInputReport(0, dataLength + 1, true); }
		public byte[] GetInputReport(byte reportID, int reportLength, bool cutReportIdByte)
		{
			return GetReport(ReportType.Input, reportID, reportLength, cutReportIdByte);
		}

		//コントロール転送でOutputReportをセットする
		//引数	dataBytes:	送信するデータ。レポートIDを含まないデータであること。
		public bool SetOutputReport(byte[] dataBytes) { return SetOutputReport(0, dataBytes, true); }
		public bool SetOutputReport(byte reportID, byte[] dataBytes, bool addReportIdByte)
		{
			return SetReport(ReportType.Output, reportID, dataBytes, addReportIdByte);
		}

		//インタラプト転送(IN)の受信スレッドを開始する
		public bool InterruptInStart(InterruptInCB callbackMethod)
		{
			return intrInCtrl.Start(callbackMethod);
		}

		//インタラプト転送(IN)の受信スレッドを停止／破棄する
		public void InterruptInStop()
		{
			intrInCtrl.Stop();
		}

	}	//end of class HidDevice
	
}	//end of namespace

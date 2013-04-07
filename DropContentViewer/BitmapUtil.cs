using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;


namespace DropContentViewer
{
	static class BitmapUtil
	{

		public static BitmapSource ToBitmapSourceDip(MemoryStream stream) {
			// var stream = e.Data.GetData("DeviceIndependentBitmap") as MemoryStream;

			byte[] dibBuffer = new byte[stream.Length];
			stream.Read(dibBuffer, 0, dibBuffer.Length);

			var infoHeader = BinaryStructConverter.FromByteArray<BITMAPINFOHEADER>(dibBuffer);

			int fileHeaderSize = Marshal.SizeOf(typeof(BITMAPFILEHEADER));
			int infoHeaderSize = infoHeader.biSize;
			int fileSize = fileHeaderSize+infoHeader.biSize+infoHeader.biSizeImage;

			var fileHeader = new BITMAPFILEHEADER();
			fileHeader.bfType = BITMAPFILEHEADER.BM;
			fileHeader.bfSize = fileSize;
			fileHeader.bfReserved1 = 0;
			fileHeader.bfReserved2 = 0;
			fileHeader.bfOffBits = fileHeaderSize+infoHeaderSize+infoHeader.biClrUsed*4;

			byte[] fileHeaderBytes = BinaryStructConverter.ToByteArray<BITMAPFILEHEADER>(fileHeader);

			var msBitmap = new MemoryStream();
			msBitmap.Write(fileHeaderBytes, 0, fileHeaderSize);
			msBitmap.Write(dibBuffer, 0, dibBuffer.Length);
			msBitmap.Seek(0, SeekOrigin.Begin);

			return BitmapFrame.Create(msBitmap);
		}

		[StructLayout(LayoutKind.Sequential, Pack = 2)]
		private struct BITMAPFILEHEADER
		{
			public static readonly short BM = 0x4d42; // BM
 
			public short bfType;
			public int bfSize;
			public short bfReserved1;
			public short bfReserved2;
			public int bfOffBits;
		}
 
		[StructLayout(LayoutKind.Sequential)]
		private struct BITMAPINFOHEADER
		{
			public int biSize;
			public int biWidth;
			public int biHeight;
			public short biPlanes;
			public short biBitCount;
			public int biCompression;
			public int biSizeImage;
			public int biXPelsPerMeter;
			public int biYPelsPerMeter;
			public int biClrUsed;
			public int biClrImportant;
		}

		private static class BinaryStructConverter
		{

			public static T FromByteArray<T>(byte[] bytes) where T:struct {
				IntPtr ptr = IntPtr.Zero;
				try {
					int size = Marshal.SizeOf(typeof(T));
					ptr = Marshal.AllocHGlobal(size);
					Marshal.Copy(bytes, 0, ptr, size);
					object obj = Marshal.PtrToStructure(ptr, typeof(T));
					return (T)obj;
				} finally {
					if(ptr!=IntPtr.Zero)
						Marshal.FreeHGlobal(ptr);
				}
			}

			public static byte[] ToByteArray<T>(T obj) where T:struct {
				IntPtr ptr = IntPtr.Zero;
				try {
					int size = Marshal.SizeOf(typeof(T));
					ptr = Marshal.AllocHGlobal(size);
					Marshal.StructureToPtr(obj, ptr, true);
					byte[] bytes = new byte[size];
					Marshal.Copy(ptr, bytes, 0, size);
					return bytes;
				} finally {
					if(ptr!=IntPtr.Zero)
						Marshal.FreeHGlobal(ptr);
				}
			}

		}

		[StructLayout(LayoutKind.Sequential)]
		public struct BITMAPV5HEADER
		{
		  public uint bV5Size;
		  public int bV5Width;
		  public int bV5Height;
		  public UInt16 bV5Planes;
		  public UInt16 bV5BitCount;
		  public uint bV5Compression;
		  public uint bV5SizeImage;
		  public int bV5XPelsPerMeter;
		  public int bV5YPelsPerMeter;
		  public UInt16 bV5ClrUsed;
		  public UInt16 bV5ClrImportant;
		  public UInt16 bV5RedMask;
		  public UInt16 bV5GreenMask;
		  public UInt16 bV5BlueMask;
		  public UInt16 bV5AlphaMask;
		  public UInt16 bV5CSType;
		  public IntPtr bV5Endpoints;
		  public UInt16 bV5GammaRed;
		  public UInt16 bV5GammaGreen;
		  public UInt16 bV5GammaBlue;
		  public UInt16 bV5Intent;
		  public UInt16 bV5ProfileData;
		  public UInt16 bV5ProfileSize;
		  public UInt16 bV5Reserved;
		}

		public static System.Drawing.Bitmap CF_DIBV5ToBitmap(MemoryStream stream) {
			// var stream = e.Data.GetData("DeviceIndependentBitmap") as MemoryStream;

			byte[] dibBuffer = new byte[stream.Length];
			stream.Read(dibBuffer, 0, dibBuffer.Length);

			return CF_DIBV5ToBitmap(dibBuffer);
		}

		public static System.Drawing.Bitmap CF_DIBV5ToBitmap(byte[] data) {
			// CF_DIBV5 (Format 17) 

			GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
			var bmi = (BITMAPV5HEADER)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(BITMAPV5HEADER));
			var bitmap = new System.Drawing.Bitmap(
				(int)bmi.bV5Width, (int)bmi.bV5Height, 
				-(int)(bmi.bV5SizeImage / bmi.bV5Height), 
				System.Drawing.Imaging.PixelFormat.Format32bppArgb,
									   new IntPtr(handle.AddrOfPinnedObject().ToInt32()
									   + bmi.bV5Size + (bmi.bV5Height - 1) 
									   * (int)(bmi.bV5SizeImage / bmi.bV5Height)));
			handle.Free();
			return bitmap;
		}

		public static BitmapSource ToBitmapSourceDipV5(MemoryStream stream) {
			var bmp = CF_DIBV5ToBitmap(stream);

			IntPtr ip = bmp.GetHbitmap();
			BitmapSource bs = null;
			try {
				bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
					ip,IntPtr.Zero, System.Windows.Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
			} finally {
				DeleteObject(ip);
			}

			return bs;
		}

		[DllImport("gdi32.dll")]
		static extern bool DeleteObject(IntPtr hObject);

	}
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

//http://stackoverflow.com/questions/1845654/how-to-use-filegroupdescriptor-to-drag-file-to-explorer-c-sharp

namespace DropContentViewer
{

	internal class FileGroupDescriptor
	{

		public static void Drag(string path) {
			DataObject dataObject = new DataObject();
			DragFileInfo filesInfo = new DragFileInfo(path);

			using (MemoryStream infoStream = GetFileDescriptor(filesInfo),contentStream = GetFileContents(filesInfo)) {
				dataObject.SetData(CFSTR_FILEDESCRIPTORW, infoStream);
				dataObject.SetData(CFSTR_FILECONTENTS, contentStream);
				dataObject.SetData(CFSTR_PERFORMEDDROPEFFECT, null);

				// DoDragDrop(dataObject, DragDropEffects.All);
			}
		}

		public const string CFSTR_PREFERREDDROPEFFECT = "Preferred DropEffect";
		public const string CFSTR_PERFORMEDDROPEFFECT = "Performed DropEffect";
		public const string CFSTR_FILEDESCRIPTORW = "FileGroupDescriptorW";
		public const string CFSTR_FILECONTENTS = "FileContents";

		public const Int32 FD_WRITESTIME = 0x00000020;
		public const Int32 FD_FILESIZE = 0x00000040;
		public const Int32 FD_PROGRESSUI = 0x00004000;

		public struct DragFileInfo
		{

			public string FileName;
			public string SourceFileName;
			public DateTime WriteTime;
			public Int64 FileSize;

			public DragFileInfo(string fileName) {
				FileName = Path.GetFileName(fileName);
				SourceFileName = fileName;
				WriteTime = DateTime.Now;
				FileSize = (new FileInfo(fileName)).Length;
			}

		}

		private static MemoryStream GetFileDescriptor(DragFileInfo fileInfo) {
			var stream = new MemoryStream();
			stream.Write(BitConverter.GetBytes(1), 0, sizeof(UInt32));

			var fileDescriptor = new FILEDESCRIPTORW();

			fileDescriptor.cFileName = fileInfo.FileName;
			Int64 fileWriteTimeUtc = fileInfo.WriteTime.ToFileTimeUtc();
			fileDescriptor.ftLastWriteTime=new System.Runtime.InteropServices.ComTypes.FILETIME() {
				dwHighDateTime =(Int32)(fileWriteTimeUtc>>32),
				dwLowDateTime = (Int32)(fileWriteTimeUtc&0xFFFFFFFF)
			};
			fileDescriptor.nFileSizeHigh = (UInt32)(fileInfo.FileSize>>32);
			fileDescriptor.nFileSizeLow = (UInt32)(fileInfo.FileSize&0xFFFFFFFF);
			fileDescriptor.dwFlags = FileDescriptorFlags.FD_WRITESTIME|FileDescriptorFlags.FD_FILESIZE|FileDescriptorFlags.FD_PROGRESSUI;

			var fileDescriptorSize = Marshal.SizeOf(fileDescriptor);
			var fileDescriptorPointer = Marshal.AllocHGlobal(fileDescriptorSize);
			var fileDescriptorByteArray = new Byte[fileDescriptorSize];

			try {
				Marshal.StructureToPtr(fileDescriptor, fileDescriptorPointer, true);
				Marshal.Copy(fileDescriptorPointer, fileDescriptorByteArray, 0, fileDescriptorSize);
			} finally {
				Marshal.FreeHGlobal(fileDescriptorPointer);
			}
			stream.Write(fileDescriptorByteArray, 0, fileDescriptorByteArray.Length);
			return stream;
		}

		private static MemoryStream GetFileContents(DragFileInfo fileInfo) {
			var stream = new MemoryStream();
			using(var reader = new BinaryReader(File.OpenRead(fileInfo.SourceFileName))) {
				var buffer = new Byte[fileInfo.FileSize];
				reader.Read(buffer, 0, (Int32)fileInfo.FileSize);
				if(buffer.Length==0) buffer = new Byte[1];
				stream.Write(buffer, 0, buffer.Length);
			}
			return stream;
		}

		// http://www.codeproject.com/Articles/28209/Outlook-Drag-and-Drop-in-C

		//[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
		public sealed class FILEGROUPDESCRIPTORA:FILEGROUPDESCRIPTOR
		{

			public static FILEGROUPDESCRIPTORA FromPointer(IntPtr ptr) {
				return FromPointer<FILEGROUPDESCRIPTORA>(ptr);
			}

			public static FILEGROUPDESCRIPTORA FromStream(Stream stream) {
				return FromStream<FILEGROUPDESCRIPTORA>(stream);
			}
		}

		//[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
		public sealed class FILEGROUPDESCRIPTORW:FILEGROUPDESCRIPTOR
		{

			public static FILEGROUPDESCRIPTORW FromPointer(IntPtr ptr) {
				return FromPointer<FILEGROUPDESCRIPTORW>(ptr);
			}

			public static FILEGROUPDESCRIPTORW FromStream(Stream stream) {
				return FromStream<FILEGROUPDESCRIPTORW>(stream);
			}
		}

		public class FILEGROUPDESCRIPTOR
		{
			public uint cItems;
			public FILEDESCRIPTOR[] fgd;

			public static T FromPointer<T>(IntPtr ptr) where T:FILEGROUPDESCRIPTOR,new(){
				var fdType=(typeof(T) == typeof(FILEGROUPDESCRIPTORW) ? typeof(FILEDESCRIPTORW) : typeof(FILEDESCRIPTORA));
				var fdSize = Marshal.SizeOf(fdType);
				var fgd = new T();
				fgd.cItems = (uint)Marshal.ReadInt32(ptr+0);
				fgd.fgd=new FILEDESCRIPTOR[fgd.cItems];
				var p = new IntPtr(ptr.ToInt64()+4);
				for(int i = 0; i<fgd.cItems; i++) {
					var ms = Marshal.PtrToStructure(p, fdType);
					fgd.fgd[i] = (FILEDESCRIPTOR)ms;
					p=new IntPtr(p.ToInt64()+fdSize);
				}
				return fgd;
			}

			public static T FromStream<T>(Stream stream) where T:FILEGROUPDESCRIPTOR,new() {
				byte[] fgdBytes = new byte[stream.Length];
				stream.Read(fgdBytes, 0, fgdBytes.Length);	
				var fgdaPtr = Marshal.AllocHGlobal(fgdBytes.Length);
				Marshal.Copy(fgdBytes, 0, fgdaPtr, fgdBytes.Length);
				var fgd = FromPointer<T>(fgdaPtr);
				Marshal.FreeHGlobal(fgdaPtr);
				return fgd;
			}
		}

		[StructLayout(LayoutKind.Sequential)]
        public sealed class POINTL{public int x;public int y;}

        [StructLayout(LayoutKind.Sequential)]
        public sealed class SIZEL{public int cx;public int cy;}

		public abstract class Dump
		{

			private static bool F(FileGroupDescriptor.FileDescriptorFlags flag, FileGroupDescriptor.FileDescriptorFlags dwFlags) {
				return ((uint)dwFlags&(uint)flag)!=0;
			}

			public static string ToString(FILEDESCRIPTOR fd){
				var s = new StringBuilder();
				/*                                              */ //s.AppendFormat("dwFlags: {0}\r\n",fd.dwFlags);
				if(F(FileDescriptorFlags.FD_CLSID     , fd.dwFlags)) s.AppendFormat("clsid           : {0}\r\n",fd.clsid);
				if(F(FileDescriptorFlags.FD_SIZEPOINT , fd.dwFlags)) s.AppendFormat("sizel           : {0}\r\n",fd.sizel);
				if(F(FileDescriptorFlags.FD_SIZEPOINT , fd.dwFlags)) s.AppendFormat("pointl          : {0}\r\n",fd.pointl);
				if(F(FileDescriptorFlags.FD_ATTRIBUTES, fd.dwFlags)) s.AppendFormat("dwFileAttributes: {0}\r\n",fd.dwFileAttributes);
				if(F(FileDescriptorFlags.FD_CREATETIME, fd.dwFlags)) s.AppendFormat("ftCreationTime  : {0}\r\n",fd.ftCreationTime);
				if(F(FileDescriptorFlags.FD_ACCESSTIME, fd.dwFlags)) s.AppendFormat("ftLastAccessTime: {0}\r\n",fd.ftLastAccessTime);
				if(F(FileDescriptorFlags.FD_WRITESTIME, fd.dwFlags)) s.AppendFormat("ftLastWriteTime : {0}\r\n",fd.ftLastWriteTime);
				if(F(FileDescriptorFlags.FD_FILESIZE  , fd.dwFlags)) s.AppendFormat("nFileSize       : {0}\r\n",(((UInt64)fd.nFileSizeHigh)<<32)+fd.nFileSizeLow);
				/*                                                */ s.AppendFormat("cFileName       : {0}\r\n",fd.cFileName);
				if(s.Length>0) s.Remove(s.Length-2, 2);
				return s.ToString();
			}

		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
		public class FILEDESCRIPTORA:FILEDESCRIPTOR
		{
			FileDescriptorFlags _dwFlags;
            Guid _clsid;
            SIZEL _sizel;
            POINTL _pointl;
            uint _dwFileAttributes;
            System.Runtime.InteropServices.ComTypes.FILETIME _ftCreationTime;
            System.Runtime.InteropServices.ComTypes.FILETIME _ftLastAccessTime;
            System.Runtime.InteropServices.ComTypes.FILETIME _ftLastWriteTime;
            uint _nFileSizeHigh;
            uint _nFileSizeLow;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            string _cFileName;

			public FileDescriptorFlags dwFlags{ get { return _dwFlags; } set { _dwFlags = value; }}
            public Guid clsid{ get { return _clsid; } set { _clsid = value; }}
            public SIZEL sizel{ get { return _sizel; } set { _sizel = value; }}
            public POINTL pointl{ get { return _pointl; } set { _pointl = value; }}
            public uint dwFileAttributes{ get { return _dwFileAttributes; } set { _dwFileAttributes = value; }}
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime{ get { return _ftCreationTime; } set { _ftCreationTime = value; }}
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime{ get { return _ftLastAccessTime; } set { _ftLastAccessTime = value; }}
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime{ get { return _ftLastWriteTime; } set { _ftLastWriteTime = value; }}
            public uint nFileSizeHigh{ get { return _nFileSizeHigh; } set { _nFileSizeHigh = value; }}
            public uint nFileSizeLow{ get { return _nFileSizeLow; } set { _nFileSizeLow = value; }}
			public string cFileName { get { return _cFileName; } set { _cFileName = value; }}
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public class FILEDESCRIPTORW:FILEDESCRIPTOR
		{
			FileDescriptorFlags _dwFlags;
            Guid _clsid;
            SIZEL _sizel;
            POINTL _pointl;
            uint _dwFileAttributes;
            System.Runtime.InteropServices.ComTypes.FILETIME _ftCreationTime;
            System.Runtime.InteropServices.ComTypes.FILETIME _ftLastAccessTime;
            System.Runtime.InteropServices.ComTypes.FILETIME _ftLastWriteTime;
            uint _nFileSizeHigh;
            uint _nFileSizeLow;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            string _cFileName;

			public FileDescriptorFlags dwFlags{ get { return _dwFlags; } set { _dwFlags = value; }}
            public Guid clsid{ get { return _clsid; } set { _clsid = value; }}
            public SIZEL sizel{ get { return _sizel; } set { _sizel = value; }}
            public POINTL pointl{ get { return _pointl; } set { _pointl = value; }}
            public uint dwFileAttributes{ get { return _dwFileAttributes; } set { _dwFileAttributes = value; }}
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime{ get { return _ftCreationTime; } set { _ftCreationTime = value; }}
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime{ get { return _ftLastAccessTime; } set { _ftLastAccessTime = value; }}
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime{ get { return _ftLastWriteTime; } set { _ftLastWriteTime = value; }}
            public uint nFileSizeHigh{ get { return _nFileSizeHigh; } set { _nFileSizeHigh = value; }}
            public uint nFileSizeLow{ get { return _nFileSizeLow; } set { _nFileSizeLow = value; }}
			public string cFileName { get { return _cFileName; } set { _cFileName = value; }}
		}

		public interface FILEDESCRIPTOR
		{
			FileDescriptorFlags dwFlags  { get; set; }
            Guid clsid  { get; set; }
            SIZEL sizel { get; set; }
            POINTL pointl { get; set; }
            uint dwFileAttributes { get; set; }
            System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime { get; set; }
            System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime { get; set; }
            System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime { get; set; }
            uint nFileSizeHigh { get; set; }
            uint nFileSizeLow { get; set; }
            string cFileName { get; set; }
		}
		
		[Flags]
		internal enum FileDescriptorFlags : uint {
			   FD_CLSID         = 0x00000001,
			   FD_SIZEPOINT     = 0x00000002,
			   FD_ATTRIBUTES    = 0x00000004,
			   FD_CREATETIME    = 0x00000008,
			   FD_ACCESSTIME    = 0x00000010,
			   FD_WRITESTIME    = 0x00000020,
			   FD_FILESIZE      = 0x00000040,
			   FD_PROGRESSUI    = 0x00004000,
			   FD_LINKUI        = 0x00008000,
			   FD_UNICODE       = 0x80000000 //Windows Vista and later
		}

	}


}

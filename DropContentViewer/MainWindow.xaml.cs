using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DropContentViewer
{
	/// <summary>
	/// Interaktionslogik für MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{

		private IDataObject _dataObject;
		private DispatcherTimer _pollIntervall;

		public MainWindow() {
			new FileGroupDescriptor.FILEDESCRIPTORA();
			new FileGroupDescriptor.FILEDESCRIPTORW();

			This = this;
			OverViewItems=new ObservableCollection<OverViewItem>();
			InitializeComponent();
		}

		public static MainWindow This { get; private set; }

		private void UIElement_OnDragOver(object sender, DragEventArgs e) {
			e.Effects=DragDropEffects.Link;
		}

		private void UIElement_OnDrop(object sender, DragEventArgs e) {
			e.Effects=DragDropEffects.Link;
			var formats=e.Data.GetFormats(false);
			OverViewItems.Clear();
			foreach(var format in formats) {
				OverViewItems.Add(new OverViewItem(e.Data,format));
			}
		}

		public ObservableCollection<OverViewItem> OverViewItems { get; private set; }

		private void AtReadClipboardOnClick(object sender, RoutedEventArgs e) {
			AtPollClipboard(null, null);
		}

		private void AtPollChanged(object sender, RoutedEventArgs e) {
			if(_pollIntervallCheckBox.IsChecked==true) {
				double milliseconds;
				if(!double.TryParse(_pollIntervallTextBox.Text, out milliseconds) || milliseconds<25) {
					Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() => _pollIntervallCheckBox.IsChecked=false));
					return;
				}
				if(_pollIntervall==null) {
					_pollIntervall = new DispatcherTimer(TimeSpan.FromMilliseconds(milliseconds),DispatcherPriority.Normal, AtPollClipboard, Dispatcher.CurrentDispatcher);
				}
				_pollIntervall.Start();
				_pollIntervallTextBox.IsEnabled = false;
			} else {
				if(_pollIntervall!=null) _pollIntervall.Stop();
				_pollIntervallTextBox.IsEnabled = true;
			}
		}

		private void AtPollClipboard(object sender, EventArgs e) {
			try {
				//if(_dataObject!=null && Clipboard.IsCurrent(_dataObject))return;
				OverViewItems.Clear();
				_dataObject = Clipboard.GetDataObject();
				//Clipboard.SetDataObject(_dataObject,false);
				var formats=_dataObject.GetFormats(false);
				foreach(var format in formats) {
					OverViewItems.Add(new OverViewItem(_dataObject,format));
				}
			} catch(Exception ex) {
				_errorTextBlock.Text = ex.ToString();
			}
		}

	}

	public class OverViewItem
	{
		static readonly string[] UnicodeFormats =new string[] {
			"text/x-moz-url","text/x-moz-url-priv","text/_moz_htmlcontext",
			"UniformResourceLocatorW",
			"text/x-moz-url-data",
			"text/x-moz-url-desc",
			"text/uri-list",
			"text/html"
		};
		static readonly string[] NonUnicodeFormats=new string[] {
			"UniformResourceLocator",
//			"FileContents"
		};

		public string Format { get; private set; }

		public OverViewItem(IDataObject store, string format) {
			Format = format;
			try {
				Data = store.GetData(format, false);
				Type = Data==null ? "{Null}" : Data.GetType().Name;
			} catch(Exception ex) {
				Type = "(Error! "+ex.Message+")";
			}

			if(Data is MemoryStream) {
				Length = ((MemoryStream)Data).Length+" Bytes";
			}else if(Data is string[]) {
				Length = ((string[])Data).Length+" Items";
			}else if(Data is string) {
				Length = ((string)Data).Length+" Chars";
			}else {
				Length = "";
			}

			BitmapContent = null;
			DisplayContent = null;
			if(Data is MemoryStream) {
				var stream = (MemoryStream)Data;
				if(UnicodeFormats.Contains(Format))
					DisplayContent = ToUnicodeString(stream);
				else if(NonUnicodeFormats.Contains(Format))
					DisplayContent = ToString(stream);
				else if(Format=="FileGroupDescriptor")
					DisplayContent = ToDumpFileGroupDescriptor(stream);
				else if(Format=="FileGroupDescriptorW")
					DisplayContent = ToDumpFileGroupDescriptorW(stream);
				else if(Format=="DragImageBits")
					BitmapContent = ToBitmapSource(stream);
				else if(Format=="DeviceIndependentBitmap")
					BitmapContent = ToBitmapSourceDip(stream);
				else if(Format=="Format17")
					BitmapContent = ToBitmapSourceDipV5(stream);
				else if(Format=="Locale" && stream.Length==4)
					DisplayContent = ToDumpLocale(stream);
				else 
					DisplayContent = ToDump(stream);
			} else if(Data is string[]) {
				DisplayContent = string.Join("\r\n",(string[])Data);
			}else if(Data is string) {
				DisplayContent = ((string)Data);
			}else {
				
			}
		}

		private string ToDumpLocale(MemoryStream stream) {
			var v = new BinaryReader(stream).ReadInt32();
			var cultureInfo = CultureInfo.GetCultureInfo(v);
			var s = new StringBuilder();
			s.AppendFormat("LCID      : {0}\r\n", cultureInfo.LCID);
			s.AppendFormat("Name      : {0}\r\n", cultureInfo.Name);
			s.AppendFormat("NativeName: {0}\r\n", cultureInfo.NativeName);
			if(s.Length>0) s.Remove(s.Length-2, 2);
			return s.ToString();
		}

		public BitmapSource BitmapContent { get; private set; }

		private string ToDumpFileGroupDescriptor(MemoryStream stream) {
			var s = new StringBuilder();
			var filegroupdescriptora = FileGroupDescriptor.FILEGROUPDESCRIPTORA.FromStream(stream);
			foreach(var cItem in filegroupdescriptora.fgd) {
				s.AppendLine(ToDumpFileDescriptor(cItem));
			}
			return s.ToString();
		}

		private string ToDumpFileGroupDescriptorW(MemoryStream stream) {
			var s = new StringBuilder();
			var filegroupdescriptora = FileGroupDescriptor.FILEGROUPDESCRIPTORW.FromStream(stream);
			foreach(var cItem in filegroupdescriptora.fgd) {
				s.AppendLine(ToDumpFileDescriptor(cItem));
			}
			return s.ToString();
		}

		private string ToDumpFileDescriptor(FileGroupDescriptor.FILEDESCRIPTOR fd) {
			return FileGroupDescriptor.Dump.ToString(fd);
		}

		private BitmapSource ToBitmapSource(MemoryStream stream) {
			// var stream = e.Data.GetData("DragImageBits") as MemoryStream;

			var buffer = new byte[24];
			stream.Read(buffer, 0, 24);
			int w = buffer[0] + (buffer[1] << 8) + (buffer[2] << 16) + (buffer[3] << 24);
			int h = buffer[4] + (buffer[5] << 8) + (buffer[6] << 16) + (buffer[7] << 24);
			// Stride accounts for any padding bytes at the end of each row. For 32 bit
			// bitmaps there are none, so stride = width * size of each pixel in bytes.
			int stride = w * 4;
			// x and y is the relative position between the top left corner of the image and
			// the mouse cursor.
			int x = buffer[8] + (buffer[9] << 8) + (buffer[10] << 16) + (buffer[11] << 24);
			int y = buffer[12] + (buffer[13] << 8) + (buffer[14] << 16) + (buffer[15] << 24);
			buffer = new byte[stride * h];
			// The image is stored upside down, so we flip it as we read it.
			for (int i = (h - 1) * stride; i >= 0; i -= stride) stream.Read(buffer, i, stride);
			var bitmapSource = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, buffer, stride);
			return bitmapSource;
		}

		private BitmapSource ToBitmapSourceDip(MemoryStream stream) {
			// var stream = e.Data.GetData("DeviceIndependentBitmap") as MemoryStream;
			return BitmapUtil.ToBitmapSourceDip(stream);
		}

		private BitmapSource ToBitmapSourceDipV5(MemoryStream stream) {
			return BitmapUtil.ToBitmapSourceDipV5(stream);
		}

		private string ToUnicodeString(MemoryStream stream) {
			var r=new StreamReader(stream,new UnicodeEncoding(false,false),false);
			return r.ReadToEnd();
		}

		private string ToString(MemoryStream stream) {
			var r=new StreamReader(stream,Encoding.Default,false);
			return r.ReadToEnd();
		}

		private string ToDump(MemoryStream stream) {
			var s = new StringBuilder();

			int b=-1;
			for(int l = 0; ; l++) {
				var s0 = new StringBuilder();
				var s1 = new StringBuilder();
				for(int i = 0; i<16; i++) {
					b = stream.ReadByte();
					if(b<0) {
						s0.Append("  ");
					} else {
						s0.Append((b<16?"0":"")+string.Format("{0:X}", b));
					}
					s0.Append(" ");
					if(i!=0 && (i+1)%8==0) s0.Append(" ");

					if(b>=0) s1.Append(b<0x20 ? "." : new string((char)b, 1));
					else s1.Append(" ");
				}
				s.Append(s0.ToString());
				s.Append(" ");
				s.Append(s1.ToString());
				if(b<0) break;
				s.AppendLine();
			}
			return  s.ToString();
		}

		public string Length {get; private set; }

		public string Type { get; private set; }

		public object Data { get; private set; }

		public string DisplayContent { get; private set; }

	}

}

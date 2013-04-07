using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
using FastFileFolderRename.Annotations;
using Path = System.IO.Path;

namespace FastFileFolderRename
{
	/// <summary>
	/// Interaktionslogik für MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{

		private List<FileSystemObjectVM> _entries;
		private FileSystemWatcher _watcher;

		public MainWindow() {
			InitializeComponent();
			Loaded += delegate {
				var args = Environment.GetCommandLineArgs();
				if(args.Length==2) OpenDirectory(args[1]);
			};
		}

		private void OpenDirectory(string path) {
			if(_watcher!=null) {
				_watcher.Dispose();
				_watcher = null;
			}


			_pathTextBox.Text = path;
			if(!Directory.Exists(path))return;

			try {
				var root = new DirectoryInfo(path);
				_entries = new List<FileSystemObjectVM>();
				foreach(var directory in root.EnumerateDirectories()) {
					_entries.Add(new FileSystemObjectVM(directory));
				}
				foreach(var file in root.EnumerateFiles()) {
					_entries.Add(new FileSystemObjectVM(file));
				}
				_editor.ItemsSource = _entries;

				_watcher=new FileSystemWatcher(path,"*.*");
				_watcher.NotifyFilter=NotifyFilters.DirectoryName|NotifyFilters.FileName|NotifyFilters.Attributes;
				_watcher.EnableRaisingEvents = true;
				_watcher.Deleted+=AtWatcherOnDeleted;
				_watcher.Renamed+=AtWatcherOnRenamed;
				_watcher.Changed+=AtWatcherOnChanged;
			} catch(Exception ex) {
				MessageBox.Show(ex.ToString(), ex.Message, MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private void AtWatcherOnChanged(object sender, FileSystemEventArgs e) {
			
		}

		private void AtWatcherOnRenamed(object sender, RenamedEventArgs e) {
			
		}

		private void AtWatcherOnDeleted(object sender, FileSystemEventArgs e) {
			var entry = _entries.First(vm => vm.Name.Equals(e.Name, StringComparison.InvariantCultureIgnoreCase));
			entry.IsExisting = false;
		}

		private void UIElement_OnPreviewKeyDown(object sender, KeyEventArgs e) {
			if(e.Key==Key.Enter) {
				var uie = e.Source as UIElement;
				if (uie == null) return;

				var binding = BindingOperations.GetBindingExpression(uie, TextBox.TextProperty);
				if (binding != null) binding.UpdateSource();				
			} else if(e.Key==Key.Down) {
				((FrameworkElement)sender).MoveFocus(new TraversalRequest(FocusNavigationDirection.Down));
			}else if(e.Key==Key.Up) {
				((FrameworkElement)sender).MoveFocus(new TraversalRequest(FocusNavigationDirection.Up));
			}

		}

		private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e) {
			((FileSystemObjectVM)((FrameworkElement)sender).DataContext).ValidateName(((TextBox)sender).Text);
		}

		private void _pathTextBox_OnTextChanged(object sender, TextChangedEventArgs e) {
			var exists = Directory.Exists(_pathTextBox.Text);
			_pathTextBox.Foreground = exists ? Brushes.Black : Brushes.Red;
		}

		private void _pathTextBox_OnKeyDown(object sender, KeyEventArgs e) {
			if(e.Key==Key.Enter) {
				var exists = Directory.Exists(_pathTextBox.Text);
				if(exists) {
					OpenDirectory(_pathTextBox.Text);
				}
			}
		}

		private void _pathTextBox_OnDragOver(object sender, DragEventArgs e) {
			e.Handled = true;
			if(e.Data.GetDataPresent("FileNameW")) {
				var fileNameW = (e.Data.GetData("FileNameW") as string[])[0];
				if(Directory.Exists(fileNameW)) {
					e.Effects = DragDropEffects.Link;return;
				}
			}
			e.Effects=DragDropEffects.None;
		}

		private void _pathTextBox_OnDrop(object sender, DragEventArgs e) {
			e.Handled = true;
			if(e.Data.GetDataPresent("FileNameW")) {
				var fileNameW = (e.Data.GetData("FileNameW") as string[])[0];
				if(Directory.Exists(fileNameW)) {
					e.Effects = DragDropEffects.Link;
					_pathTextBox.Text = fileNameW;
					OpenDirectory(fileNameW);
					return;
				}
			}
			e.Effects=DragDropEffects.None;
		}

	}

	public class FileSystemObjectVM:INotifyPropertyChanged{

		private string _name;
		private bool _nameIsValid;
		private bool _isExisting;

		public FileSystemObjectVM(DirectoryInfo directory) {
			_name = directory.Name;
			_nameIsValid = true;
			FileSystemInfo = directory;
			IsDirectory = true;
			_isExisting = directory.Exists;
		}

		public FileSystemObjectVM(FileInfo file) {
			_name = file.Name;
			_nameIsValid = true;
			FileSystemInfo = file;
			IsFile = true;
			_isExisting = file.Exists;
		}

		public bool IsFile { get; private set; }
		public bool IsDirectory { get; private set; }

		public FileSystemInfo FileSystemInfo { get; private set; }

		public string Name {
			get { return _name; }
			set {
				_name = value;
				ValidateName(value);
				OnPropertyChanged("Name");

				if(NameIsValid && value!=FileSystemInfo.Name) Rename();
			}
		}

		public bool NameIsValid {
			get { return _nameIsValid; }
			set { _nameIsValid = value; OnPropertyChanged("NameIsValid");}
		}

		public bool IsExisting {
			get { return _isExisting; }
			set { _isExisting = value; OnPropertyChanged("IsExisting");}
		}

		public void Rename() {
			if(FileSystemInfo is DirectoryInfo) {
				var directoryInfo = (DirectoryInfo)FileSystemInfo;
				var newPath = Path.Combine(Path.GetDirectoryName(directoryInfo.FullName),Name);
				directoryInfo.MoveTo(newPath);
			}else if(FileSystemInfo is FileInfo) {
				var fileInfo = (FileInfo)FileSystemInfo;
				var newPath = Path.Combine(Path.GetDirectoryName(fileInfo.FullName),Name);
				fileInfo.MoveTo(newPath);
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
			var handler = PropertyChanged;
			if(handler!=null) handler(this, new PropertyChangedEventArgs(propertyName));
		}

		public void ValidateName(string preview) {
			var directoryInfo = FileSystemInfo;
			var newPath = Path.Combine(Path.GetDirectoryName(directoryInfo.FullName),preview);
			if(directoryInfo.Name.Equals(preview, StringComparison.InvariantCultureIgnoreCase)) {
				NameIsValid = true;return;
			}

			if(Path.GetInvalidFileNameChars().Any(preview.Contains)) {
				NameIsValid = false;return;
			}
			if(Directory.Exists(newPath)) {
				NameIsValid=false;return;
			}
			
		}

	}
}

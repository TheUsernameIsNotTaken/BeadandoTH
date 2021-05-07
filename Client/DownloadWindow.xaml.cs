using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MessageBox = System.Windows.Forms.MessageBox;

namespace Client
{
    /// <summary>
    /// Interaction logic for DownloadWindow.xaml
    /// </summary>
    public partial class DownloadWindow : Window
    {

        private IList<string> _fileNames;

        public string selectedFile { get; private set; }

        private delegate void UpdateDelegate(string strFiles);

        private void UpdateFiles(string strFiles)
        {
            _fileNames = strFiles.Split('*').ToList();
            FilesListBox.ItemsSource = _fileNames;
            selectedFile = null;
        }

        public DownloadWindow()
        {
            InitializeComponent();
            DialogResult = false;
            Close();
        }

        public DownloadWindow(string files)
        {
            InitializeComponent();

            //Frissíteni kell a listát
            UpdateDelegate update = new UpdateDelegate(UpdateFiles);
            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, update, files);

        }

        private void FilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedFile = FilesListBox.SelectedItem as string;
        }

        private void PickReceiverButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedFile == null)
            {
                MessageBox.Show("Kérem válasszon ki egy létező fájlt a letöltéshez!",
                                        "Fájl nem található!",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Warning);
            }
            else
            {
                DialogResult = true;
                Close();
            }

        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

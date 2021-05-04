using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
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
using DataLibrary;

namespace Client
{
    /// <summary>
    /// Interaction logic for ReceiverWindow.xaml
    /// </summary>
    public partial class ReceiverWindow : Window
    {

        private string _username;
        private IList<string> _users;
        byte[] _buffer;

        public string selectedUser { get; private set; }

        private delegate void UpdateDelegate(string StrUsers);

        public ReceiverWindow()
        {
            InitializeComponent();
            DialogResult = false;
            Close();
        }

        public ReceiverWindow(string userName, string strList)
        {
            InitializeComponent();

            //Save the variables
            _username = userName;
            _buffer = new byte[1024];

            //Frissíteni kell a listát
            UpdateDelegate update = new UpdateDelegate(UpdateUsers);
            this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, update, strList);
        }

        private void UsersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedUser = UsersListBox.SelectedItem as string;
        }

        private void PublicChatButton_Click(object sender, RoutedEventArgs e)
        {
            selectedUser = Data.PUBLIC_ID;
            DialogResult = true;
            Close();
        }

        private void PickReceiverButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedUser == null)
            {
                MessageBox.Show("Beszélgetés előtt kérem válasszon ki egy létező partnert!",
                                        "Partner nem található!",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Warning);
            }
            else
            {
                DialogResult = true;
                Close();
            }
        }

        private void UpdateUsers(string StrUsers)
        {
            _users = StrUsers.Split('*');
            UsersListBox.ItemsSource = _users;
            selectedUser = null;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

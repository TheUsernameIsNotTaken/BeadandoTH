using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using DataLibrary;

namespace Server
{
    /// <summary>
    /// Interaction logic for RegisterWindow.xaml
    /// </summary>
    public partial class RegisterWindow : Window
    {

        User _newUser = new User();

        public RegisterWindow()
        {
            InitializeComponent();
            SetFocusable(true);
        }

        private void SaveUser_ButtonClick(object sender, RoutedEventArgs e)
        {
            //Cannot allow editing while saving
            SetFocusable(false);

            //Set the new user data
            _newUser = new User(UsernameTextBox.Text, PasswordTextBox.Text);

            //Close the dialog window
            DialogResult = true;
            //Close();
        }

        public User NewUser
        {
            get { return _newUser; }
        }

        private void SetFocusable(bool isAllowed)
        {
            UsernameTextBox.Focusable = isAllowed;
            PasswordTextBox.Focusable = isAllowed;
            SaveUserButton.Focusable = isAllowed;
        }
    }
}

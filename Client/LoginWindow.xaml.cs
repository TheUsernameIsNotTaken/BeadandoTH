using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using DataLibrary;

namespace Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>


    public partial class LoginWindow : Window
    {
        
        public Socket clientSocket;
        public string strName;

        public delegate string getNameDelegate();
        public delegate string getPassDelegate();
        public delegate void UjFormDelegate();

        private bool _loggedOut = false;
        private IPEndPoint ipEndPoint;

        byte[] _byteData = new byte[1024];

        public LoginWindow()
        {
            InitializeComponent();
        }

        public LoginWindow(String userName)
        {
            InitializeComponent();
            strName = userName;

            new Thread(() =>
            {
                MessageBox.Show(strName + " felhasználó kilépett.", "SGSclient");
            }).Start();
        }

        public string getLoginName() 
        {
            return this.UserNameTextBox.Text.ToString();
        }

        public string getLoginPass()
        {
            return this.UserPassPassBox.Password.ToString();
        }

        public string getIP()
        {
            return this.ServerIPTextBox.Text.ToString();
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);                
                //IPAddress ipAddress = IPAddress.Parse(this.textBox2.Text);

                //getNameDelegate IP = new getNameDelegate(getIP);
                //l_ip = (string)this.Dispatcher.Invoke(IP, null);
                IPAddress ipAddress = IPAddress.Parse(this.ServerIPTextBox.Text);
                //Server is listening on port 1000
                ipEndPoint = new IPEndPoint(ipAddress, 1000);

                //Connect to the server
                //clientSocket.Connect(ipEndPoint);
                clientSocket.BeginConnect(ipEndPoint, new AsyncCallback(OnConnect), null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SGSclient");
            } 
        }

        private void OnReceive(IAsyncResult ar)
        {
            clientSocket.EndReceive(ar);

            //Transform the array of bytes received from the user into an
            //intelligent form of object Data
            Data msgReceived = new Data(_byteData);

            //Csak a nekem szóló üzenet kell. Ez akkor szükésges, ha I SOCKETEN TÖBB KAPCSOLAT IS VAN - Ez az 1 port per IP miatt lehet probléma, 1 gépes bemutatás esetén.
            string local_fhNev;
            getNameDelegate fhNev = new getNameDelegate(getLoginName);
            local_fhNev = (string)this.UserNameTextBox.Dispatcher.Invoke(fhNev, null);
            if (msgReceived.strName.Equals(local_fhNev))
            {
                //Ha tudok csatlakozni
                if (msgReceived.cmdCommand == Command.Accept)
                {
                    new Thread(() =>
                    {
                        MessageBox.Show("Sikeres kapcsolódás!", "SGSclient connect");
                    }).Start();

                    UjFormDelegate pForm = new UjFormDelegate(UjForm);
                    this.Dispatcher.Invoke(pForm, null);
                }
                //Ha nem tudok csatlakozni
                else
                {
                    new Thread(() =>
                    {
                        MessageBox.Show("Kapcsolat megtagadva!", "SGSclient connect");
                    }).Start();
                }
            }
        }

        private void OnSend(IAsyncResult ar)
        {
            try
            {
                clientSocket.EndSend(ar);
                if (!_loggedOut)
                {
                    //Várunk a válaszra
                    clientSocket.BeginReceive(_byteData, 0, 1024, SocketFlags.None, new AsyncCallback(OnReceive), clientSocket);
                }
                _loggedOut = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SGSclient");
            }
        }

        private void UjForm()
        {
            CliensMessage uj_form;
            uj_form = new CliensMessage(clientSocket, UserNameTextBox.Text, ipEndPoint);
            uj_form.Show();
            Close();
        }

        private void OnConnect(IAsyncResult ar)
        {
            try
            {
                clientSocket.EndConnect(ar);

                //We are connected so we login into the server
                string l_fhName;
                string l_fhPass;
                Data msgToSend = new Data();
                msgToSend.cmdCommand = Command.Login;

                //Username
                getNameDelegate fhName = new getNameDelegate(getLoginName);
                l_fhName = (string)this.UserNameTextBox.Dispatcher.Invoke(fhName, null);
                
                //Pass
                getPassDelegate fhPass = new getPassDelegate(getLoginPass);
                l_fhPass = (string)this.UserNameTextBox.Dispatcher.Invoke(fhPass, null);

                //Data set
                msgToSend.strName = l_fhName;
                msgToSend.strMessage = l_fhPass;
                msgToSend.strRec = Data.PUBLIC_ID;

                byte[] b = msgToSend.ToByte();

                //Send the message to the server
                clientSocket.BeginSend(b, 0, b.Length, SocketFlags.None, new AsyncCallback(OnSend), null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SGSclient");
            }
        }
    }
}

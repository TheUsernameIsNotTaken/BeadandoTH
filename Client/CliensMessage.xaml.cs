using DataLibrary;
using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows;

namespace Client
{
    /// <summary>
    /// Interaction logic for CliensMessage.xaml
    /// </summary>

    public partial class CliensMessage : Window
    {
        public Socket ClientSocket;
        public string LoginName;
        byte[] byteData = new byte[1024];
        public string _partner;
        private bool _safeClose = false;
        private string _filename;
        private IPEndPoint _endPoint;

        private delegate void UpdateDelegate(string pMessage);
        private delegate void PartnerTextDelegate();
        private delegate void RecFormDelegate(string msgReceivedMsg);
        private delegate void FoFormDelegate();
        private delegate void LogOutDelegate();

        private void UpdateMessage(string pMessage)
        {
            messageTextBox.Text += pMessage;
            messageTextBox.Focus();
            messageTextBox.CaretIndex = messageTextBox.Text.Length;
            messageTextBox.ScrollToEnd();
        }

        public void SetPartnerText()
        {
            labelList.Content = "Chat: " + ((_partner.Equals(Data.PUBLIC_ID)) ? "Publikus" : _partner);
        }

        public CliensMessage()
        {
            InitializeComponent();
        }

        public CliensMessage(Socket pSocket, String pName, IPEndPoint ipEndPoint)
        {
            InitializeComponent();

            ClientSocket = pSocket;
            LoginName = pName;
            this.Title = pName;
            _partner = Data.PUBLIC_ID;
            _filename = null;
            _endPoint = ipEndPoint;

            ClientSocket.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None,
                    new AsyncCallback(OnReceive), ClientSocket);

            //ClientSocket.Receive(byteData,SocketFlags.None);

        }

        private void OnReceive(IAsyncResult ar)
        {
            Socket clientSocket = (Socket)ar.AsyncState;
            clientSocket.EndReceive(ar);
            
            //Transform the array of bytes received from the user into an
            //intelligent form of object Data
            Data msgReceived = new Data(byteData);

            //Csak a nekem szóló üzenet kell. Ez akkor szükésges, ha I SOCKETEN TÖBB KAPCSOLAT IS VAN - Ez az 1 port per IP miatt lehet probléma, 1 gépes bemutatás esetén.
            if (msgReceived.cmdCommand.Equals(Command.Logout) && msgReceived.strName.Equals(LoginName))
            {
                //Lecsatlakozás
                ClientSocket.Shutdown(SocketShutdown.Both);
                ClientSocket.Close();

                _safeClose = true;

                FoFormDelegate foForm = new FoFormDelegate(FoForm);
                this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, foForm);
            }
            else
            {
                ClientSocket.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None,
                    new AsyncCallback(OnReceive), ClientSocket);

                if (msgReceived.cmdCommand.Equals(Command.List) && msgReceived.strName.Equals(LoginName))
                {
                    //Új ablak nyitása a megfelelő adatok átadásával.
                    RecFormDelegate recForm = new RecFormDelegate(RecForm);
                    this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, recForm,
                        msgReceived.strMessage);
                }
                else
                {
                    UpdateDelegate update = new UpdateDelegate(UpdateMessage);
                    this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, update,
                        msgReceived.strMessage + "\r\n");
                }
            }
        }

        private void OnSend(IAsyncResult ar)
        {
            Socket clientSocket = (Socket)ar.AsyncState;
            clientSocket.EndSend(ar);
        }

        //Send a message
        private void buttonSend_Click(object sender, RoutedEventArgs e)
        {
            Data msgToSend = new Data();
            msgToSend.cmdCommand = Command.Message;
            msgToSend.strName = LoginName;
            msgToSend.strRec = _partner;
            msgToSend.strMessage = textBox2.Text;

            byte[] b = msgToSend.ToByte();
            ClientSocket.BeginSend(b, 0, b.Length, SocketFlags.None,
                    new AsyncCallback(OnSend), ClientSocket);
            //ClientSocket.Send(b);
        }

        //Send a logout message
        private void buttonLogout_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void FoForm()
        {
            LoginWindow fo_form;
            fo_form = new LoginWindow(LoginName);
            fo_form.Show();
            this.Close();
        }

        //Send a Listing message
        private void buttonList_Click(object sender, RoutedEventArgs e)
        {
            //Lista lekérés küldése
            Data msgToSend = new Data();
            msgToSend.cmdCommand = Command.List;
            msgToSend.strName = LoginName;
            msgToSend.strMessage = null;
            msgToSend.strRec = null;
            byte[] b = msgToSend.ToByte();
            ClientSocket.BeginSend(b, 0, b.Length, SocketFlags.None, new AsyncCallback(OnSend), ClientSocket);
            //ClientSocket.Send(b);
        }

        private void RecForm(string msgReceivedMsg)
        {
            ReceiverWindow rec_window;
            rec_window = new ReceiverWindow(LoginName, msgReceivedMsg);
            if (rec_window.ShowDialog() ?? false)
            {
                _partner = rec_window.selectedUser;
                PartnerTextDelegate partnerUpdate = new PartnerTextDelegate(SetPartnerText);
                this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, partnerUpdate);
                new Thread(() =>
                {
                    MessageBox.Show("Új beszélgetőpartner: " + ( (_partner.Equals(Data.PUBLIC_ID)) ? "Mindenki" : _partner) , "SGSclient");
                }).Start();
            }
            else
            {
                new Thread(() =>
                {
                    MessageBox.Show("Sikertelen partnerválasztás.", "SGSclient");
                }).Start();
            }
        }

        void ClientsWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!_safeClose)
            {
                //Event leállítás
                e.Cancel = true;

                //Kilépési adat
                Data msgToSend = new Data();
                msgToSend.cmdCommand = Command.Logout;
                msgToSend.strName = LoginName;
                msgToSend.strRec = Data.PUBLIC_ID;
                msgToSend.strMessage = null;

                //Kilépés küldése
                byte[] b = msgToSend.ToByte();
                ClientSocket.Send(b);
            }
        }

        private void buttonOpen_Click(object sender, RoutedEventArgs e)
        {
            //Lista lekérés küldése
            Data msgToSend = new Data();
            msgToSend.cmdCommand = Command.File;
            msgToSend.strName = LoginName;
            msgToSend.strMessage = "test1.png";
            msgToSend.strRec = _partner;
            byte[] b = msgToSend.ToByte();
            ClientSocket.BeginSend(b, 0, b.Length, SocketFlags.None, new AsyncCallback(OnSend), ClientSocket);

            // Configure open file dialog box
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.FileName = "Picture"; // Default file name
            dialog.DefaultExt = ".png"; // Default file extension
            dialog.Filter = "Pictures(.png)|*.png"; // Filter files by extension

            // Show open file dialog box
            bool? result = dialog.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                // Open document
                _filename = dialog.FileName;

                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    socket.Connect(_endPoint.Address.ToString(), 1001);
                    // Send Length (Int64)
                    socket.SendFile(_filename);
                }
            }
        }
    }
}

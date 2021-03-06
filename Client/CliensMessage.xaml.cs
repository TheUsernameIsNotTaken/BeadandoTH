using DataLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

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
        private bool _isUploading = false;
        private bool _isDownloading = false;

        private delegate void UpdateDelegate(string pMessage);
        private delegate void PartnerTextDelegate();
        private delegate void RecFormDelegate(string msgReceivedMsg);
        private delegate void FoFormDelegate();
        private delegate void LogOutDelegate();
        private delegate void CloseDelegate();
        private delegate void DownloadDelegate(string fileNames);

        private void CloseRun()
        {
            this.Close();
        }

        ////-------------
        ////TEST
        ////Test Task
        //void TestTask()
        //{
        //    Console.WriteLine("Thread '{0}' OPENED.", Thread.CurrentThread.ManagedThreadId);
        //    while (_testRun)
        //    {
        //        Thread.Sleep(2000);
        //    }
        //    _testRun = true;
        //    Console.WriteLine("Thread '{0}' START.", Thread.CurrentThread.ManagedThreadId);
        //    Thread.Sleep(2500);
        //    Console.WriteLine("Thread '{0}' END.", Thread.CurrentThread.ManagedThreadId);
        //    _testRun = false;
        //}
        //bool _testRun = false;
        ////-------------

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

        }

        private void OnReceive(IAsyncResult ar)
        {
            try
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
                    this.Dispatcher.Invoke(DispatcherPriority.Normal, foForm);
                }
                else
                {
                    ClientSocket.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None,
                        new AsyncCallback(OnReceive), ClientSocket);

                    if (msgReceived.cmdCommand.Equals(Command.List) && msgReceived.strName.Equals(LoginName))
                    {
                        //Új ablak nyitása a megfelelő adatok átadásával.
                        RecFormDelegate recForm = new RecFormDelegate(RecForm);
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, recForm,
                            msgReceived.strMessage);
                    }
                    else if (msgReceived.cmdCommand.Equals(Command.DownloadList) && msgReceived.strName.Equals(LoginName))
                    {
                        //Új ablak nyitása a megfelelő adatok átadásával.
                        DownloadDelegate downForm = new DownloadDelegate(DownForm);
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, downForm,
                            msgReceived.strMessage);
                    }
                    else
                    {
                        UpdateDelegate update = new UpdateDelegate(UpdateMessage);
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, update,
                            msgReceived.strMessage + "\r\n");

                        if (msgReceived.cmdCommand.Equals(Command.Close))
                        {
                            CloseDelegate closeRun = new CloseDelegate(CloseRun);
                            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, closeRun);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Client Receive");
            }
        }

        private void OnSend(IAsyncResult ar)
        {
            try
            {
                Socket clientSocket = (Socket)ar.AsyncState;
                clientSocket.EndSend(ar);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Client Send");
            }
        }

        //Send a message
        private void buttonSend_Click(object sender, RoutedEventArgs e)
        {
            Data msgToSend = new Data();
            msgToSend.cmdCommand = Command.Message;
            msgToSend.strName = LoginName;
            msgToSend.strRec = _partner;
            msgToSend.strMessage = MessageTextBox.Text;

            byte[] b = msgToSend.ToByte();
            ClientSocket.BeginSend(b, 0, b.Length, SocketFlags.None,
                    new AsyncCallback(OnSend), ClientSocket);
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
                    MessageBox.Show("Új beszélgetőpartner: " + ( (_partner.Equals(Data.PUBLIC_ID)) ? "Mindenki" : _partner) , "Client Receiver");
                }).Start();
            }
            else
            {
                new Thread(() =>
                {
                    MessageBox.Show("Sikertelen partnerválasztás.", "Client Partner");
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

        private async void UploadTask()
        {
            //Wait for a free space
            while (_isUploading)
            {
                Thread.Sleep(1000);
            }

            //Start uploading
            _isUploading = true;

            //Try to upload, and catch if it's not successfull. 
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    try
                    {
                        await socket.ConnectAsync(_endPoint.Address.ToString(), Data.UPLOAD_PORT);
                        // Send Length (Int64)
                        socket.SendFile(_filename);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Client Upload");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Client Upload Using");
            }

            //Upload's end
            _isUploading = false;
        }

        private void buttonOpen_Click(object sender, RoutedEventArgs e)
        {

            //Test task list
            //Task.Factory.StartNew(() => TestTask());

            // Configure open file dialog box
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.FileName = "Picture";                // Default file name
            dialog.DefaultExt = ".png";                 // Default file extension
            dialog.Filter = "Pictures(.png)|*.png";     // Filter files by extension

            // Show open file dialog box
            bool? result = dialog.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                // Open document
                _filename = dialog.FileName;

                //Send Upload Data
                Data msgToSend = new Data();
                msgToSend.cmdCommand = Command.Upload;
                msgToSend.strName = LoginName;
                msgToSend.strRec = _partner;
                msgToSend.strMessage = Path.GetFileName(_filename);
                byte[] b = msgToSend.ToByte();
                ClientSocket.BeginSend(b, 0, b.Length, SocketFlags.None, new AsyncCallback(OnSend), ClientSocket);

                Task.Factory.StartNew(() => UploadTask());
            }
        }

        private async void DownloadTask(string filename, string sender, string receiver)
        {

            //Wait for others to finish
            if (_isDownloading)
            {
                Thread.Sleep(2000);
            }

            //Set the pooling flag
            _isDownloading = true;

            //Send download starter
            try
            {
                byte[] message;
                Data msgToSend = new Data();
                msgToSend.cmdCommand = Command.StartDownload;
                msgToSend.strName = sender;
                msgToSend.strRec = receiver;
                msgToSend.strMessage = Path.GetFileName(filename);
                message = msgToSend.ToByte();
                ClientSocket.BeginSend(message, 0, message.Length, SocketFlags.None,
                    new AsyncCallback(OnSend), ClientSocket);
            }
            catch (Exception ex)
            {
                new Thread(() =>
                {
                    MessageBox.Show(ex.Message, "Client Download Start");
                }).Start();
            }

            var listener = new TcpListener(_endPoint.Address, Data.DOWNLOAD_PORT);
            listener.Start();
            try
            {
                using (var client = await listener.AcceptTcpClientAsync())
                using (var stream = client.GetStream())
                using (var output = File.Create(filename))
                {
                    try
                    {
                        //Console.WriteLine("Server connected. Starting to receive the file.");

                        // read the file in chunks of 1KB
                        var buffer = new byte[1024];
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            output.Write(buffer, 0, bytesRead);
                        }
                    }
                    catch (Exception ex)
                    {
                        new Thread(() =>
                        {
                            MessageBox.Show(ex.Message, "Client Download");
                        }).Start();
                    }
                }
            }
            catch (Exception ex)
            {
                new Thread(() =>
                {
                    MessageBox.Show(ex.Message, "Client Download Using");
                }).Start();
            }
            listener.Stop();
            //Console.WriteLine("Server Disconnected.");

            _isDownloading = false;

            //Send an ack message
            try
            {
                byte[] message;
                Data msgToSend = new Data();
                msgToSend.cmdCommand = Command.DownloadAck;
                msgToSend.strName = sender;
                msgToSend.strRec = receiver;
                msgToSend.strMessage = Path.GetFileName(filename) ;
                message = msgToSend.ToByte();
                ClientSocket.BeginSend(message, 0, message.Length, SocketFlags.None,
                    new AsyncCallback(OnSend), ClientSocket);
            }
            catch (Exception ex)
            {
                new Thread(() =>
                {
                    MessageBox.Show(ex.Message, "Client Download End");
                }).Start();
            }
        }

        private void DownForm(string fileNames)
        {
            //Set the sender and receiver
            string sender = _partner;
            string receiver = LoginName;

            //Select a file to download

            //null esetén meg se nyitom
            if (string.IsNullOrEmpty(fileNames))
            {

                MessageBox.Show("Nem találhatóak fájlok!", "Fáljletöltés");
            }
            //Adat esetén megnyitom
            else
            {
                DownloadWindow down_window;
                down_window = new DownloadWindow(fileNames);
                if (down_window.ShowDialog() ?? false)
                {
                    string toDownload = down_window.selectedFile;
                    string pathDown = Data.FILES_FOLDER + (sender.Equals(Data.PUBLIC_ID) ? Data.PUBLIC_ID : (sender + "-" + receiver));
                    Directory.CreateDirectory(pathDown);

                    //Download the selected file
                    Task.Factory.StartNew(() => DownloadTask(pathDown + "\\" + toDownload, sender, receiver));
                }
                else
                {
                    new Thread(() =>
                    {
                        MessageBox.Show("Sikertelen fájlválsztás.", "Client Download");
                    }).Start();
                }
            }
        }

        private void buttonDownload_Click(object sender, RoutedEventArgs e)
        {
            //FájlLista lekérés küldése
            Data msgToSend = new Data();
            msgToSend.cmdCommand = Command.DownloadList;
            msgToSend.strName = LoginName;
            msgToSend.strMessage = null;
            msgToSend.strRec = _partner;
            byte[] b = msgToSend.ToByte();
            ClientSocket.BeginSend(b, 0, b.Length, SocketFlags.None, new AsyncCallback(OnSend), ClientSocket);
        }
    }
}

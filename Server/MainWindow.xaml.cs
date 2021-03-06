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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Xml.Serialization;
using System.IO;
using DataLibrary;

namespace Server
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public partial class MainWindow : Window
    {
        struct ClientInfo
        {
            public Socket socket;   //Socket of the client
            public string strName;  //Name by which the user logged into the chat room
        }

        //Users.
        string _userFileName = "Users.xml";
        List<User> _users;

        //Active users.
        ArrayList clientList;

        //Open port.
        Socket serverSocket;

        //Buffer
        byte[] byteData = new byte[1024];

        //Delegate
        public delegate void RegFormDelegate();
        public delegate void UploadDelegate(IPAddress address, string filename, string sender, string receiver);
        public delegate void DownloadDelegate(IPAddress address, string filename);

        //Pooling
        private bool _uploading = false;
        private bool _downloading = false;

        //Closing
        bool _closeStarted;

        public MainWindow()
        {
            InitializeComponent();
            clientList = new ArrayList();
            _closeStarted = false;
        }

        private delegate void UpdateDelegate(string pMessage);

        private void UpdateMessage(string pMessage)
        {
            this.textBox1.Text += pMessage;
        }

        public delegate void CloseWriteDelegate(bool value);
        public delegate bool CloseReadDelegate();
        public delegate void CloseDelegate();

        private void SetCloseStarted(bool value)
        {
            _closeStarted = value;
        }

        private bool GetCloseStarted()
        {
            return _closeStarted;
        }

        private void CloseRun()
        {
            this.Close();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                //Load in the user data.
                _users = new List<User>();
                if (File.Exists(_userFileName))
                {
                    _users = await AsyncReadData();
                }

                //We are using TCP sockets
                //Control.CheckForIllegalCrossThreadCalls = false;
                serverSocket = new Socket(AddressFamily.InterNetwork,
                                          SocketType.Stream,
                                          ProtocolType.Tcp);

                //Assign the any IP of the machine and listen on port number 1000
                IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 1000);

                //Bind and listen on the given address
                serverSocket.Bind(ipEndPoint);
                serverSocket.Listen(4);

                //Accept the incoming clients
                serverSocket.BeginAccept(new AsyncCallback(OnAccept), null);
                //serverSocket.Accept();

            }
            catch (Exception ex)
            {
                new Thread(() =>
                {
                    MessageBox.Show(ex.Message, "Server Loaded");
                }).Start();
            }   
        }

        private void OnAccept(IAsyncResult ar)
        {
            try
            {
                Socket clientSocket = serverSocket.EndAccept(ar);

                //Start listening for more clients
                serverSocket.BeginAccept(new AsyncCallback(OnAccept), null);

                //Once the client connects then start receiving the commands from her
                clientSocket.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None,
                    new AsyncCallback(OnReceive), clientSocket);

            }
            catch (Exception ex)
            {
                new Thread(() =>
                {
                    MessageBox.Show(ex.Message, "Server Connect");
                }).Start();
            }
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

                //We will send this object in response the users request
                Data msgToSend = new Data();

                byte[] message;

                //If the message is to login, logout, or simple text message
                //then when send to others the type of the message remains the same
                msgToSend.cmdCommand = msgReceived.cmdCommand;
                msgToSend.strName = msgReceived.strName;
                msgToSend.strRec = msgReceived.strRec;

                switch (msgReceived.cmdCommand)
                {
                    case Command.Login:

                        //Check if the user is authorized for access
                        User connectedUser = new User(msgReceived.strName, msgReceived.strMessage);
                        if(connectedUser.SearchInList(_users) >= 0)
                        {
                            //When a user logs in to the server then we add her to our
                            //list of clients
                            msgToSend.cmdCommand = Command.Accept;
                            //message = msgToSend.ToByte();
                            //clientSocket.Send(message);

                            ClientInfo clientInfo = new ClientInfo();
                            clientInfo.socket = clientSocket;
                            clientInfo.strName = msgReceived.strName;

                            clientList.Add(clientInfo);

                            //Set the text of the message that we will broadcast to all users
                            msgToSend.strMessage = "<<<" + msgReceived.strName + " csatlakozott a szobahoz>>>";
                        }
                        else
                        {
                            //Decline the user
                            msgToSend.cmdCommand = Command.Decline;
                            msgToSend.strName = msgReceived.strName;
                            msgToSend.strMessage = null;
                            message = msgToSend.ToByte();

                            //Log it only on the server-side
                            UpdateDelegate update = new UpdateDelegate(UpdateMessage);
                            this.textBox1.Dispatcher.BeginInvoke(DispatcherPriority.Normal, update,
                                "<<<" + msgReceived.strName + " megprobalt a szobahoz csatlakozni engedely nelkul>>>" + "\r\n");

                            //Send the message and close the connection
                            clientSocket.Send(message, 0, message.Length, SocketFlags.None);
                            clientSocket.Shutdown(SocketShutdown.Both);
                            clientSocket.Close();
                        }

                        break;

                    case Command.Logout:

                        //When a user wants to log out of the server then we search for her 
                        //in the list of clients and close the corresponding connection

                        int nIndex = 0;
                        foreach (ClientInfo client in clientList)
                        {
                            if (client.socket == clientSocket && client.strName.Equals(msgReceived.strName))
                            {
                                clientList.RemoveAt(nIndex);
                                break;
                            }
                            ++nIndex;
                        }

                        //Send ack data before closing - Only for the disconnectiong client
                        //msgToSend.cmdCommand = Command.Logout;
                        //msgToSend.strName = msgReceived.strName;
                        msgToSend.strMessage = null;    //A parancs és név marad, az üzenet törlődik.
                        message = msgToSend.ToByte();
                        clientSocket.Send(message, 0, message.Length, SocketFlags.None);

                        //Kapcsolat leállítása
                        clientSocket.Shutdown(SocketShutdown.Both);
                        clientSocket.Close();

                        msgToSend.strMessage = "<<<" + msgReceived.strName + " elhagyta a szobat>>>";
                        break;

                    case Command.Message:

                        //Set the text of the message that we will broadcast to all  (if public)
                        msgToSend.strMessage = msgReceived.strName + " -> " +
                            (msgReceived.strRec.Equals(Data.PUBLIC_ID) ? "Mindenki" : msgReceived.strRec) +
                            " : " + msgReceived.strMessage;
                        break;

                    case Command.Upload:
                        //Get the correct IP
                        IPEndPoint upEndPoint = clientSocket.RemoteEndPoint as IPEndPoint;

                        //Create a delegate for upload handling
                        string pathUp = Data.FILES_FOLDER + (msgReceived.strRec.Equals(Data.PUBLIC_ID) ? Data.PUBLIC_ID : (msgReceived.strName + "-" + msgReceived.strRec));
                        Directory.CreateDirectory(pathUp);
                        UploadDelegate upload = new UploadDelegate(BeginUpload);
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, 
                            upload, upEndPoint.Address, pathUp + "\\" + msgReceived.strMessage, msgReceived.strName, msgReceived.strRec);

                        msgToSend.strMessage = "<<<" + msgReceived.strName + " megkezdte a '" + System.IO.Path.GetFileName(msgReceived.strMessage) + "' nevu falj feltolteset>>>";

                        break;

                    case Command.StartDownload:
                        //Get the correct IP
                        IPEndPoint downEndPoint = clientSocket.RemoteEndPoint as IPEndPoint;

                        //Delete message
                        msgToSend.strMessage = null;

                        //Create a delegate for upload handling
                        string pathDown = Data.FILES_FOLDER + (msgReceived.strName.Equals(Data.PUBLIC_ID) ? Data.PUBLIC_ID : (msgReceived.strName + "-" + msgReceived.strRec));
                        Directory.CreateDirectory(pathDown);
                        DownloadDelegate download = new DownloadDelegate(BeginDownload);
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal,
                            download, downEndPoint.Address, pathDown + "\\" + msgReceived.strMessage);

                        msgToSend.strMessage = "<<<" + msgReceived.strRec + " megkezdte a '" + System.IO.Path.GetFileName(msgReceived.strMessage) + "' nevu falj letolteset>>>";

                        break;

                    case Command.DownloadAck:

                        //Set the text of the message that we will broadcast to all  (if public)
                        msgToSend.strMessage = "<<<" + msgReceived.strRec + " befejezte a '" + System.IO.Path.GetFileName(msgReceived.strMessage) + "' nevu falj letolteset>>>";
                        break;

                    case Command.DownloadList:
                        //Delete message
                        msgToSend.strMessage = null;

                        //Create a delegate for upload handling
                        string pathDirectory = Data.FILES_FOLDER + (msgReceived.strRec.Equals(Data.PUBLIC_ID) ? Data.PUBLIC_ID : (msgReceived.strRec + "-" + msgReceived.strName));
                        Directory.CreateDirectory(pathDirectory);

                        //Collect all filenames
                        bool notFirstDownFiles = false;
                        foreach (string file in Directory.GetFiles(pathDirectory))
                        {
                            if (notFirstDownFiles)
                                msgToSend.strMessage += "*";
                            else
                                notFirstDownFiles = true;
                            msgToSend.strMessage += System.IO.Path.GetFileName(file);
                        }

                        message = msgToSend.ToByte();

                        //Send the filenames
                        clientSocket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                new AsyncCallback(OnSend), clientSocket);

                        break;

                    case Command.List:

                        //Send the names of all users in the chat room to the new user
                        msgToSend.cmdCommand = Command.List;
                        msgToSend.strName = msgReceived.strName;
                        msgToSend.strMessage = null;

                        //Collect the names of the user in the chat 
                        bool notFirstList = false;
                        foreach (ClientInfo client in clientList)
                        {
                            //To keep things simple we use asterisk as the marker to separate the user names
                            if (notFirstList)
                                msgToSend.strMessage += "*";
                            else
                                notFirstList = true;
                            msgToSend.strMessage += client.strName;

                        }

                        message = msgToSend.ToByte();

                        //Send the name of the users in the chat room
                        clientSocket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                new AsyncCallback(OnSend), clientSocket);
                        break;
                }

                if ( !(msgToSend.cmdCommand == Command.List || msgToSend.cmdCommand == Command.DownloadList || msgToSend.cmdCommand == Command.Decline) )   //List and decline messages are not broadcasted
                {
                    message = msgToSend.ToByte();

                    foreach (ClientInfo clientInfo in clientList)
                    {
                        if (clientInfo.socket != clientSocket || msgToSend.cmdCommand != Command.Login)
                        {
                            //A publikus az broadcast, amúgy csak a 2 partnernek megy. -> Figyelni kell, hogy PUBLIC legyen alapból a cél, és feladó is legyen mindig.
                            if (msgToSend.strRec.Equals(Data.PUBLIC_ID) || clientInfo.strName.Equals(msgReceived.strRec) || clientInfo.strName.Equals(msgReceived.strName))
                            {
                                //Send the message to all users
                                clientInfo.socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                    new AsyncCallback(OnSend), clientInfo.socket);
                                //clientInfo.socket.Send(message, 0, message.Length, SocketFlags.None);
                            }
                        }
                    }
                    
                    UpdateDelegate update = new UpdateDelegate(UpdateMessage);
                    this.textBox1.Dispatcher.BeginInvoke(DispatcherPriority.Normal, update, 
                        msgToSend.strMessage + "\r\n");
                }

                //If the user is logging out or declined, then we need not listen from her
                if ( !(msgReceived.cmdCommand == Command.Logout || msgToSend.cmdCommand == Command.Decline) )
                {
                    //Start listening to the message send by the user
                    clientSocket.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(OnReceive), clientSocket);
                }

                //Check if it's the last logout
                if (msgReceived.cmdCommand == Command.Logout && clientList.Count <= 0)
                {
                    //Check if we started a close
                    CloseReadDelegate closeRead = new CloseReadDelegate(GetCloseStarted);
                    bool closeStarted = (bool)this.Dispatcher.Invoke(DispatcherPriority.Normal, closeRead);

                    //Finish the close
                    if (closeStarted)
                    {
                        CloseDelegate closeRun = new CloseDelegate(CloseRun);
                        this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, closeRun);
                    }
                }
            }
            catch (Exception ex)
            {
                new Thread(() =>
                {
                    MessageBox.Show(ex.Message, "Server Receive");
                }).Start();
            }
        }

        public void OnSend(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;
                client.EndSend(ar);
            }
            catch (Exception ex)
            {
                //Ha sikertelen küldés, akkor a kilépést újra kell kezdeni.
                new Thread(() =>
                {
                    MessageBox.Show(ex.Message, "Server Send");
                }).Start();
            }
        }

        //Users
        List<User> UpdateUsers()
        {
            List<User> users = new List<User>();

            XmlSerializer sr = new XmlSerializer(users.GetType());
            using (TextReader writer = new StreamReader(_userFileName))
            {
                users = (List<User>)sr.Deserialize(writer);
            }

            return users;
        }

        void SaveUsers()
        {
            XmlSerializer sr = new XmlSerializer(_users.GetType());
            using (TextWriter writer = new StreamWriter(_userFileName))
            {
                sr.Serialize(writer, _users);
            }
        }

        async Task<List<User>> AsyncReadData()
        {
            var users = await Task.Run(UpdateUsers);
            return users;
        }

        async Task AsyncSaveData()
        {
            await Task.Run(SaveUsers);
        }

        private void buttonAddUser_Click(object sender, RoutedEventArgs e)
        {
            RegFormDelegate regForm = new RegFormDelegate(RegForm);
            this.Dispatcher.Invoke(regForm);
        }

        private async void RegForm()
        {
            RegisterWindow reg_form;
            reg_form = new RegisterWindow();
            if (reg_form.ShowDialog() ?? false)
            {
                //Get new data
                User userToAdd = reg_form.NewUser;

                //Check if the username is allowed.
                if (userToAdd.username.Contains('*') ||
                    userToAdd.username.Contains('-') ||
                    userToAdd.username.Equals(Data.PUBLIC_ID) ||
                    userToAdd.username.Equals(Data.SERVER_NAME))
                {
                    new Thread(() =>
                    {
                        MessageBox.Show("Új felhasználó létrehozása sikertelen. Ok: Érvénytelen felhasználónév.", "Server User Create");
                    }).Start();
                }
                else if (_users.Where(user => user.username.Equals(userToAdd.username)).Count() > 0)
                {
                    new Thread(() =>
                    {
                        MessageBox.Show("Új felhasználó létrehozása sikertelen. Ok: Létező felhasználónév.", "Server User Create");
                    }).Start();
                }
                //Update with the authorized new user's data.
                else
                {
                    _users.Add(userToAdd);
                    await AsyncSaveData();
                }
            }
        }

        private async void BeginUpload(IPAddress address, string filename, string sender, string receiver)
        {

            //Wait for others to finish
            if (_uploading)
            {
                Thread.Sleep(2000);
            }

            //Set the pooling flag
            _uploading = true;

            try
            {
                var listener = new TcpListener(address, Data.UPLOAD_PORT);
                listener.Start();
                using (var client = await listener.AcceptTcpClientAsync())
                using (var stream = client.GetStream())
                using (var output = File.Create(filename))
                {
                    try
                    {
                        //Console.WriteLine("Client connected. Starting to receive the file.");

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
                            MessageBox.Show(ex.Message, "Server Upload");
                        }).Start();
                    }
                }
                listener.Stop();
                //Console.WriteLine("Client Disconnected.");
            }
            catch (Exception ex)
            {
                new Thread(() =>
                {
                    MessageBox.Show(ex.Message, "Server Upload Using");
                }).Start();
            }

            _uploading = false;

            //Send an upload ack message
            byte[] message;
            Data msgToSend = new Data();

            msgToSend.cmdCommand = Command.Message;
            msgToSend.strName = sender;
            msgToSend.strRec = receiver;
            msgToSend.strMessage = sender + " -> " +
                (receiver.Equals(Data.PUBLIC_ID) ? "Mindenki" : receiver) +
                " : '" + System.IO.Path.GetFileName(filename) + "' nevu fajl sikeresen megosztva.";

            message = msgToSend.ToByte();

            try
            {
                foreach (ClientInfo clientInfo in clientList)
                {
                    //A publikus az broadcast, amúgy csak a 2 partnernek megy. -> Figyelni kell, hogy PUBLIC legyen alapból a cél, és feladó is legyen mindig.
                    if (receiver.Equals(Data.PUBLIC_ID) || clientInfo.strName.Equals(sender) || clientInfo.strName.Equals(receiver))
                    {
                        //Send the message to all users
                        clientInfo.socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                            new AsyncCallback(OnSend), clientInfo.socket);
                    }
                }
            }
            catch (Exception ex)
            {
                new Thread(() =>
                {
                    MessageBox.Show(ex.Message, "Server Upload Message");
                }).Start();
            }

            UpdateDelegate update = new UpdateDelegate(UpdateMessage);
            await this.textBox1.Dispatcher.BeginInvoke(DispatcherPriority.Normal, update,
                msgToSend.strMessage + "\r\n");
        }

        private async void BeginDownload(IPAddress address, string filename)
        {
            //Wait for a free space
            while (_downloading)
            {
                Thread.Sleep(1000);
            }

            //Start downloading
            _downloading = true;

            //Try to download, and catch if it's not successfull. 
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    try
                    {
                        await socket.ConnectAsync(address, Data.DOWNLOAD_PORT);
                        // Send Length (Int64)
                        socket.SendFile(filename);
                    }
                    catch (Exception ex)
                    {
                        new Thread(() =>
                        {
                            MessageBox.Show(ex.Message, "Server Download");
                        }).Start();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Server Download Using");
            }

            //Download's end
            _downloading = false;
        }

        async void ServerWindow_Closing(object sender, CancelEventArgs e)
        {
            if (clientList.Count > 0)
            {
                //Event leállítás
                e.Cancel = true;

                //Read in closed value
                CloseReadDelegate closeRead = new CloseReadDelegate(GetCloseStarted);
                bool closeStarted =  (bool) this.Dispatcher.Invoke(DispatcherPriority.Normal, closeRead);

                //Ha még nincs elindítva, elindítom
                if (!closeStarted)
                {
                    //Kilépés elindítva
                    CloseWriteDelegate closeSet = new CloseWriteDelegate(SetCloseStarted);
                    await this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, closeSet, true);

                    //Kilépési adat
                    Data msgToSend = new Data();
                    msgToSend.cmdCommand = Command.Close;
                    msgToSend.strName = Data.SERVER_NAME;
                    msgToSend.strRec = Data.PUBLIC_ID;
                    msgToSend.strMessage = "<<<A szerver bezar>>>";
                    byte[] message = msgToSend.ToByte();

                    //Minden felhasználónak elküldöm
                    foreach (ClientInfo clientInfo in clientList)
                    {
                        clientInfo.socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                            new AsyncCallback(OnSend), clientInfo.socket);
                    }
                }
                else
                {
                    new Thread(() =>
                    {
                        MessageBox.Show("Várakozás a kliensek lecsatlakozására.");
                    }).Start();
                }
            }
        }
    }
}
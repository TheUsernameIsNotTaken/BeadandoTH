using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DataLibrary
{

    public enum Command
    {
        Login,          //Log into the server
        Logout,         //Logout of the server
        Message,        //Send a text message to all the chat clients
        List,           //Get a list of users in the chat room from the server
        Accept,         //Accept a connection
        Decline,        //Decline a connection
        Upload,         //Upload a file
        DownloadList,   //Download a file
        StartDownload,  //Starts a specific file's download
        DownloadAck,  //Starts a specific file's download
        Close,          //Close all clients
        Null            //No command
    }

    public class Data
    {

        public static string FILES_FOLDER = "Files\\";
        public static string SERVER_NAME = "SERVER";
        public static string PUBLIC_ID = "PUBLIC";
        public static int UPLOAD_PORT = 1001;
        public static int DOWNLOAD_PORT = 1002;

        //Default constructor
        public Data()
        {
            this.cmdCommand = Command.Null;
            this.strName = null;
            this.strRec = null;
            this.strMessage = null;
        }

        //Converts the bytes into an object of type Data
        public Data(byte[] data)
        {
            //The first four bytes are for the Command
            this.cmdCommand = (Command)BitConverter.ToInt32(data, 0);

            //The next four store the length of the name
            int nameLen = BitConverter.ToInt32(data, 4);

            //The next four store the length of the receiver
            int recLen = BitConverter.ToInt32(data, 8);

            //The next four store the length of the message
            int msgLen = BitConverter.ToInt32(data, 12);

            //This check makes sure that strName has been passed in the array of bytes
            if (nameLen > 0)
                this.strName = Encoding.UTF8.GetString(data, 16, nameLen);
            else
                this.strName = null;

            //This check makes sure that strRec has been passed in the array of bytes
            if (recLen > 0)
                this.strRec = Encoding.UTF8.GetString(data, 16 + nameLen, recLen);
            else
                this.strRec = null;

            //This checks for a null message field
            if (msgLen > 0)
                this.strMessage = Encoding.UTF8.GetString(data, 16 + nameLen + recLen, msgLen);
            else
                this.strMessage = null;
        }

        //Converts the Data structure into an array of bytes
        public byte[] ToByte()
        {
            List<byte> result = new List<byte>();

            //First four are for the Command
            result.AddRange(BitConverter.GetBytes((int)cmdCommand));

            //Add the length of the name
            if (strName != null)
                result.AddRange(BitConverter.GetBytes(strName.Length));
            else
                result.AddRange(BitConverter.GetBytes(0));

            //Add the length of the receiver
            if (strRec != null)
                result.AddRange(BitConverter.GetBytes(strRec.Length));
            else
                result.AddRange(BitConverter.GetBytes(0));

            //Length of the message
            if (strMessage != null)
                result.AddRange(BitConverter.GetBytes(strMessage.Length));
            else
                result.AddRange(BitConverter.GetBytes(0));

            //Add the name
            if (strName != null)
                result.AddRange(Encoding.UTF8.GetBytes(strName));

            //Add the receiver
            if (strRec != null)
                result.AddRange(Encoding.UTF8.GetBytes(strRec));

            //And, lastly we add the message text to our array of bytes
            if (strMessage != null)
                result.AddRange(Encoding.UTF8.GetBytes(strMessage));

            return result.ToArray();
        }

        public string strName;      //Name by which the client logs into the room
        public string strRec;
        public string strMessage;   //Message text
        public Command cmdCommand;  //Command type (login, logout, send message, etcetera)
    }
}

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using Windows_Forms_CORE_CHAT_UGH;

namespace Windows_Forms_Chat
{
    public class ClientSocket
    {
        //add other attributes to this, e.g username, what state the client is in etc
        public Socket socket;
        public const int BUFFER_SIZE = 2048;
        /// <summary>
        /// buffer has data
        /// </summary>
        public byte[] buffer = new byte[BUFFER_SIZE];
        /// <summary>
        /// name of client
        /// </summary>
        public string name;
        /// <summary>
        /// role of client
        /// </summary>
        public bool isMod = false;
        /// <summary>
        /// config of client
        /// </summary>
        public bool isTime = false;
        /// <summary>
        /// state of client
        /// </summary>
        public State state = State.Offline;
        /// <summary>
        /// name of player: !player1 or !player2
        /// </summary>
        public string name_player = null;
        /// <summary>
        /// point
        /// </summary>
        public int win = 0;
        public int draw = 0;
        public int lose = 0;
    }
}

﻿// From http://weblogs.asp.net/cumpsd/pages/79260.aspx
using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace System.Net {
	#region Delegates
	public delegate void CommandReceived(string IrcCommand);
	public delegate void TopicSet(string IrcChannel, string IrcTopic);
	public delegate void TopicOwner(string IrcChannel, string IrcUser, string TopicDate);
	public delegate void NamesList(string UserNames);
	public delegate void ServerMessage(string ServerMessage);
	public delegate void Join(string IrcChannel, string IrcUser);
	public delegate void Part(string IrcChannel, string IrcUser);
	public delegate void Mode(string IrcChannel, string IrcUser, string UserMode);
	public delegate void NickChange(string UserOldNick, string UserNewNick);
	public delegate void Kick(string IrcChannel, string UserKicker, string UserKicked, string KickMessage);
	public delegate void Quit(string UserQuit, string QuitMessage);
    public delegate void EndMOTD();
    public delegate void Invite(string IrcChannel, string IrcUser);
	#endregion
	
	public class IRC {
		#region Events
		public event CommandReceived eventReceiving;
		public event TopicSet eventTopicSet;
		public event TopicOwner eventTopicOwner;
		public event NamesList eventNamesList;
		public event ServerMessage eventServerMessage;
		public event Join eventJoin;
		public event Part eventPart;
		public event Mode eventMode;
		public event NickChange eventNickChange;
		public event Kick eventKick;
		public event Quit eventQuit;
        public event Invite eventInvite;
		#endregion
		
		#region Private Variables
		private string ircServer;
		private int ircPort;
		private string ircNick;
		private string ircUser;
		private string ircRealName;
		private string ircChannel;
		private bool isInvisible;
		private TcpClient ircConnection;
		private NetworkStream ircStream;
		private StreamWriter ircWriter;
		private StreamReader ircReader;

        private bool ircNickServ;
        private string ircNSUser;
        private string ircNSPass;
        private bool ircBitlbee;
        private string ircBitlbeePass;
		#endregion
		
		#region Properties
		public string IrcServer {
			get { return this.ircServer; }
			set { this.ircServer = value; }
		} /* IrcServer */

		public int IrcPort {
			get { return this.ircPort; }
			set { this.ircPort = value; }
		} /* IrcPort */
		
		public string IrcNick {
			get { return this.ircNick; }
			set { this.ircNick = value; }
		} /* IrcNick */
		
		public string IrcUser {
			get { return this.ircUser; }
			set { this.ircUser = value; }
		} /* IrcUser */
		
		public string IrcRealName {
			get { return this.ircRealName; }
			set { this.ircRealName = value; }
		} /* IrcRealName */
		
		public string IrcChannel {
			get { return this.ircChannel; }
			set { this.ircChannel = value; }
		} /* IrcChannel */
		
		public bool IsInvisble {
			get { return this.isInvisible; }
			set { this.isInvisible = value; }
		} /* IsInvisible */
		
		public TcpClient IrcConnection {
			get { return this.ircConnection; }
			set { this.ircConnection = value; }
		} /* IrcConnection */
		
		public NetworkStream IrcStream {
			get { return this.ircStream; }
			set { this.ircStream = value; }
		} /* IrcStream */
		
		public StreamWriter IrcWriter {
			get { return this.ircWriter; }
			set { this.ircWriter = value; }
		} /* IrcWriter */
		
		public StreamReader IrcReader {
			get { return this.ircReader; }
			set { this.ircReader = value; }
		} /* IrcReader */

        public bool IrcNickServ
        {
            get { return this.ircNickServ; }
            set { this.ircNickServ = value; }
        }

        public string IrcNSUser
        {
            get { return this.ircNSUser; }
            set { this.ircNSUser = value; }
        }

        public string IrcNSPass
        {
            get { return this.ircNSPass; }
            set { this.ircNSPass = value; }
        }

        public bool IrcBitlbee
        {
            get { return this.ircBitlbee; }
            set { this.ircBitlbee = value; }
        }

        public string IrcBitlbeePass
        {
            get { return this.ircBitlbeePass; }
            set { this.ircBitlbeePass = value; }
        }
		#endregion	
		
		#region Constructor
		public IRC(string IrcNick, string IrcChannel) {
			this.IrcNick = IrcNick;
			this.IrcUser = System.Environment.MachineName;
			this.IrcRealName = "ShadowBot";
			this.IrcChannel = IrcChannel;
			this.IsInvisble = false;
            this.IrcNickServ = false;
            this.IrcBitlbee = false;
		} /* IRC */
		#endregion
		
		#region Public Methods
		public void Connect(string IrcServer, int IrcPort) {
			this.IrcServer = IrcServer;
			this.IrcPort = IrcPort;

			// Connect with the IRC server.
			this.IrcConnection = new TcpClient(this.IrcServer, this.IrcPort);
			this.IrcStream = this.IrcConnection.GetStream();
			this.IrcReader = new StreamReader(this.IrcStream);
			this.IrcWriter = new StreamWriter(this.IrcStream);
		
			// Authenticate our user

            Thread.Sleep(100);
			string isInvisible = this.IsInvisble ? "8" : "0";
            this.IrcWriter.WriteLine(String.Format("NICK {0}", this.IrcNick));
            this.IrcWriter.Flush();
            Thread.Sleep(100);
			this.IrcWriter.WriteLine(String.Format("USER {0} * * :{2}", this.IrcUser, isInvisible, this.IrcRealName));
			this.IrcWriter.Flush();
            Thread.Sleep(100);
            if (this.ircNickServ)
            {
                this.IrcWriter.WriteLine(String.Format("PRIVMSG NickServ :identify {0} {1}", this.ircNSUser, this.ircNSPass));
                this.IrcWriter.Flush();
            }
            Thread.Sleep(100);
            if (this.ircBitlbee)
            {
                this.IrcWriter.WriteLine(String.Format("PRIVMSG " + this.ircChannel + " :identify {0}", this.ircBitlbeePass));
                this.IrcWriter.Flush();
            }
            else
            {
                this.IrcWriter.WriteLine(String.Format("JOIN {0}", this.IrcChannel));
                this.IrcWriter.Flush();
            }
			// Listen for commands
			while (true) {
				string ircCommand;
                try
                {
                    while ((ircCommand = this.IrcReader.ReadLine()) != null)
                    {
                        if (eventReceiving != null) { this.eventReceiving(ircCommand); }

                        string[] commandParts = new string[ircCommand.Split(' ').Length];
                        commandParts = ircCommand.Split(' ');
                        if (commandParts[0].Substring(0, 1) == ":")
                        {
                            commandParts[0] = commandParts[0].Remove(0, 1);
                        }

                        //if (commandParts[0] == this.IrcServer) {
                        if (commandParts.Length >= 2)
                        {
                            // Server message
                            switch (commandParts[1])
                            {
                                case "332": this.IrcTopic(commandParts); break;
                                case "333": this.IrcTopicOwner(commandParts); break;
                                case "353": this.IrcNamesList(commandParts); break;
                                case "366": /*this.IrcEndNamesList(commandParts);*/ break;
                                case "372": /*this.IrcMOTD(commandParts);*/ break;
                                case "376": /*this.IrcEndMOTD(commandParts);*/ break;
                                default: this.IrcServerMessage(commandParts); break;
                            }
                        }
                        if (commandParts[0] == "PING")
                        {
                            // Server PING, send PONG back
                            this.IrcPing(commandParts);
                        }
                        else
                        {
                            // Normal message
                            string commandAction = commandParts[1];
                            switch (commandAction)
                            {
                                case "JOIN": this.IrcJoin(commandParts); break;
                                case "PART": this.IrcPart(commandParts); break;
                                case "MODE": this.IrcMode(commandParts); break;
                                case "NICK": this.IrcNickChange(commandParts); break;
                                case "KICK": this.IrcKick(commandParts); break;
                                case "QUIT": this.IrcQuit(commandParts); break;
                                case "INVITE": this.IrcInvite(commandParts); break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.IrcWriter.Close();
                    this.IrcReader.Close();
                    this.IrcConnection.Close();
                    Console.WriteLine("Error: " + ex.Message);
                    Environment.Exit(0);
                }
			
				this.IrcWriter.Close();
				this.IrcReader.Close();
				this.IrcConnection.Close();
			}
		} /* Connect */
		#endregion
		
		#region Private Methods
		#region Server Messages
		private void IrcTopic(string[] IrcCommand) {
			string IrcChannel = IrcCommand[3];
			string IrcTopic = "";
			for (int intI = 4; intI < IrcCommand.Length; intI++) {
				IrcTopic += IrcCommand[intI] + " ";
			}
			if (eventTopicSet != null) { this.eventTopicSet(IrcChannel, IrcTopic.Remove(0, 1).Trim()); }
		} /* IrcTopic */
		
		private void IrcTopicOwner(string[] IrcCommand) {
			string IrcChannel = IrcCommand[3];
			string IrcUser = IrcCommand[4].Split('!')[0];
			string TopicDate = IrcCommand[5];
			if (eventTopicOwner != null) { this.eventTopicOwner(IrcChannel, IrcUser, TopicDate); }
		} /* IrcTopicOwner */
		
		private void IrcNamesList(string[] IrcCommand) {
		  string UserNames = "";
			for (int intI = 5; intI < IrcCommand.Length; intI++) {
				UserNames += IrcCommand[intI] + " ";
			}
			if (eventNamesList != null) { this.eventNamesList(UserNames.Remove(0, 1).Trim()); }
		} /* IrcNamesList */
		
		private void IrcServerMessage(string[] IrcCommand) {
			string ServerMessage = "";
			for (int intI = 1; intI < IrcCommand.Length; intI++) {
				ServerMessage += IrcCommand[intI] + " ";
			}
			if (eventServerMessage != null) { this.eventServerMessage(ServerMessage.Trim()); }
		} /* IrcServerMessage */
		#endregion
		
		#region Ping
		private void IrcPing(string[] IrcCommand) {
			string PingHash = "";
			for (int intI = 1; intI < IrcCommand.Length; intI++) {
				PingHash += IrcCommand[intI] + " ";
			}
			//PingHash = IrcCommand[1];
			PingHash = PingHash.Remove(PingHash.Length - 1, 1);
			this.IrcWriter.WriteLine("PONG " + PingHash);
			this.IrcWriter.Flush();
			//Console.WriteLine("PONG " + PingHash);
		} /* IrcPing */
		#endregion
		
		#region User Messages
		private void IrcJoin(string[] IrcCommand) {
			string IrcChannel = IrcCommand[2];
			string IrcUser = IrcCommand[0].Split('!')[0];
			if (eventJoin != null) { this.eventJoin(IrcChannel.Remove(0, 1), IrcUser); }
		} /* IrcJoin */
		
		private void IrcPart(string[] IrcCommand) {
			string IrcChannel = IrcCommand[2];
			string IrcUser = IrcCommand[0].Split('!')[0];
			if (eventPart != null) { this.eventPart(IrcChannel, IrcUser); }
		} /* IrcPart */
		
		private void IrcMode(string[] IrcCommand) {
			string IrcChannel = IrcCommand[2];
			string IrcUser = IrcCommand[0].Split('!')[0];
			string UserMode = "";
			for (int intI = 3; intI < IrcCommand.Length; intI++) {
				UserMode += IrcCommand[intI] + " ";
			}
			if (UserMode.Substring(0, 1) == ":") {
				UserMode = UserMode.Remove(0, 1);
			}
			if (eventMode != null) { this.eventMode(IrcChannel, IrcUser, UserMode.Trim()); }
		} /* IrcMode */
		
		private void IrcNickChange(string[] IrcCommand) {
			string UserOldNick = IrcCommand[0].Split('!')[0];
			string UserNewNick = IrcCommand[2].Remove(0, 1);
			if (eventNickChange != null) { this.eventNickChange(UserOldNick, UserNewNick); }
		} /* IrcNickChange */
		
		private void IrcKick(string[] IrcCommand) {
			string UserKicker = IrcCommand[0].Split('!')[0];
			string UserKicked = IrcCommand[3];
			string IrcChannel = IrcCommand[2];
			string KickMessage = "";
			for (int intI = 4; intI < IrcCommand.Length; intI++) {
				KickMessage += IrcCommand[intI] + " ";
			}
			if (eventKick != null) { this.eventKick(IrcChannel, UserKicker, UserKicked, KickMessage.Remove(0, 1).Trim()); }
		} /* IrcKick */
		
		private void IrcQuit(string[] IrcCommand) {
			string UserQuit = IrcCommand[0].Split('!')[0];
			string QuitMessage = "";
			for (int intI = 2; intI < IrcCommand.Length; intI++) {
				QuitMessage += IrcCommand[intI] + " ";
			}
			if (eventQuit != null) { this.eventQuit(UserQuit, QuitMessage.Remove(0, 1).Trim()); }
		} /* IrcQuit */

        private void IrcInvite(string[] IrcCommand)
        {
            string IrcChannel = IrcCommand[3];
            string IrcUser = IrcCommand[0].Split('!')[0];
            if (eventInvite != null) { this.eventInvite(IrcChannel.Remove(0, 1), IrcUser); }
        } /* IrcJoin */
		#endregion
		#endregion
	} /* IRC */
<<<<<<< .mine
} /* System.Net */
=======
} /* System.Net */
>>>>>>> .r6

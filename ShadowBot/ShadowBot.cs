using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Nini.Config;

namespace ShadowBot
{
    class ShadowBot
    {
        private IRC IrcObject;
        private string IniFilename;
        private Stopwatch sw = new Stopwatch();
        private int roulette = 0;
        private List<string> Registered = new List<string>();
        private List<string> Userlist = new List<string>();
        private bool started = false;
        private bool cont = true;
        private bool joined = false;
        private static bool debug = false;

        //create our FeedManager & FeedList items
        RssManager reader = new RssManager();
        //Collection<Rss.Items> list;
        //ListViewItem row;
        DateTime LastCheck = DateTime.Now;

        /// <summary>
        /// Point d'entrée principal de l'application.
        /// </summary>
        static void Main(string[] args)
        {
            ArgvConfigSource argv = new ArgvConfigSource(args);
            argv.AddSwitch("Options", "config", "c");
            argv.AddSwitch("Options", "help", "h");
            //argv.AddSwitch("Options", "help", "?");
            argv.AddSwitch("Options", "debug", "d");
            if (argv.Configs["Options"].Contains("help"))
            {
                Console.WriteLine("Usage: IRCBot [options]");
                Console.WriteLine("-c <filename>, --config=<filename>: Loads <filename> as configuration file. Default: ShadowBot.ini");
                Console.WriteLine("-h, --help: Displays this help.");
                Console.WriteLine("-d, --debug: Debug mode. To be used by the developer.");
                return;
            }
            if (argv.Configs["Options"].Contains("debug"))
                debug = true;
            ShadowBot IRCApp = new ShadowBot(argv.Configs["Options"].Get("config", "ShadowBot.ini"));
        }

        public ShadowBot(string IniFilename)
        {
            IConfigSource ini = new IniConfigSource(IniFilename);
            this.IniFilename = IniFilename;
            //IniFile ini = new IniFile(Application.StartupPath + "/conf.ini");
            IrcObject = new IRC(ini.Configs["Server"].Get("Nick", "ShadowBot"), ini.Configs["Server"].Get("Channel", "#5709-games")); //new IRC(ini.IniReadValue("Server", "Nick"), ini.IniReadValue("Server", "Channel"));

            IrcObject.IrcRealName = ini.Configs["Server"].Get("RealName", "ShadowBot"); //ini.IniReadValue("Server", "RealName");
            IrcObject.IrcUser = ini.Configs["Server"].Get("User", "shadowbot"); //ini.IniReadValue("Server", "User");
            IrcObject.IrcNickServ = ini.Configs["Server"].GetBoolean("NickServ", false); //Convert.ToBoolean(ini.IniReadValue("Server", "NickServ"));
            IrcObject.IrcNSUser = ini.Configs["Server"].Get("NSUser", ""); //ini.IniReadValue("Server", "NSUser");
            IrcObject.IrcNSPass = ini.Configs["Server"].Get("NSPass", ""); //ini.IniReadValue("Server", "NSPass");
            IrcObject.IrcBitlbee = ini.Configs["Server"].GetBoolean("Bitlbee", false);
            IrcObject.IrcBitlbeePass = ini.Configs["Server"].Get("BPass", "");

            IrcObject.eventReceiving += new CommandReceived(IrcObject_eventReceiving);
            IrcObject.eventKick += new Kick(IrcObject_eventKick);
            IrcObject.eventJoin += new Join(IrcObject_eventJoin);
            IrcObject.eventPart += new Part(IrcObject_eventPart);
            IrcObject.eventNamesList += new NamesList(IrcObject_eventNamesList);
            //IrcObject.eventQuit += new Quit(IrcObject_eventQuit);

            Thread t = new Thread(CheckForNewFeeds);
            t.Start();

            IrcObject.Connect(ini.Configs["Server"].Get("Server", "irc.freenode.net"), ini.Configs["Server"].GetInt("Port", 6667)); //(ini.IniReadValue("Server", "Server"), Convert.ToInt32(ini.IniReadValue("Server", "Port")));

        }

        void IrcObject_eventNamesList(string UserNames)
        {
            string[] users = UserNames.Split(" ~&@%+:".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            foreach (string user in users)
                Userlist.Add(user);
        }

        void CheckForNewFeeds()
        {
            while (cont)
            {
                if (joined)
                    try
                    {
                        IConfigSource ini = new IniConfigSource(IniFilename);
                        //execute the GetRssFeeds method in out
                        //FeedManager class to retrieve the feeds
                        //for the specified URL
                        reader.Url = ini.Configs["Config"].Get("Feed", "");
                        reader.GetFeed();
                        //list = reader.RssItems;
                        if (reader.RssItems[0].Date >= LastCheck)
                        {
                            IrcObject.IrcWriter.WriteLine("PRIVMSG " + IrcObject.IrcChannel + " :" + reader.RssItems[0].Date.ToString() + ": New edit to ShadowWiki: " + reader.RssItems[0].Title + " by " + reader.RssItems[0].Creator +/* ": " + reader.RssItems[0].Description + */" (" + reader.RssItems[0].Link + ")");
                            IrcObject.IrcWriter.Flush();
                            //LastCheck = reader.RssItems[0].Date;
                        }
                        else if (debug)
                        {
                            IrcObject.IrcWriter.WriteLine("PRIVMSG " + IrcObject.IrcChannel + " :Debug Info: " + reader.RssItems[0].Date.ToString() + " <= " + LastCheck.ToString());
                            IrcObject.IrcWriter.Flush();
                        }
                        //if (LastCheck <= DateTime.Now)
                            LastCheck = DateTime.Now;
                        //list = reader
                        //now populate out ListBox
                        //loop through the count of feed items returned
                        /*
                        for (int i = 0; i < list.Count; i++)
                        {
                            //add the title, link and public date
                            //of each feed item to the ListBox
                            row = new ListViewItem();
                            row.Text = list[i].Title;
                            row.SubItems.Add(list[i].Link);
                            row.SubItems.Add(list[i].Date.ToShortDateString());
                            lstNews.Items.Add(row);
                        }*/
                    }
                    catch (Exception ex)
                    {
                        //MessageBox.Show(ex.ToString());
                        //IrcObject.IrcWriter.WriteLine("PRIVMSG " + IrcObject.IrcChannel + " :Error: " + ex.Message);
                        //IrcObject.IrcWriter.Flush();
                    }
                Thread.Sleep(2000);
            }
        }

        void IrcObject_eventQuit(string UserQuit, string QuitMessage)
        {
            //IrcObject.IrcWriter.WriteLine("PRIVMSG " + IrcChannel + " :Awww, " + UserQuit + " quitted...");
            //IrcObject.IrcWriter.Flush();
            if (Registered.Contains(UserQuit))
                Registered.Remove(UserQuit);
            Userlist.Remove(UserQuit);
        }

        void IrcObject_eventPart(string IrcChannel, string IrcUser)
        {
            IrcObject.IrcWriter.WriteLine("PRIVMSG " + IrcChannel + " :Awww, " + IrcUser + " parted...");
            IrcObject.IrcWriter.Flush();
            IrcObject.IrcWriter.WriteLine("NOTICE " + IrcUser + " :Awww, " + IrcUser + " parted...");
            IrcObject.IrcWriter.Flush();
            if (Registered.Contains(IrcUser))
                Registered.Remove(IrcUser);
            if (IrcChannel == IrcObject.IrcChannel)
                Userlist.Remove(IrcUser);
        }

        void IrcObject_eventJoin(string IrcChannel, string IrcUser)
        {
            IConfigSource ini = new IniConfigSource(IniFilename);
            if (IrcUser != IrcObject.IrcNick)
            {
                string Mode = " +v ";
                if (ini.Configs["Access"].GetInt(IrcUser, ini.Configs["Config"].GetInt("DefaultLevel", 0)) > 0)
                {
                    switch (ini.Configs["Access"].GetInt(IrcUser, ini.Configs["Config"].GetInt("DefaultLevel", 0)))
                    {
                        case 5:
                            Mode = " +aov ";
                            break;
                        case 4:
                            Mode = " +aov ";
                            break;
                        case 3:
                            Mode = " +ov ";
                            break;
                        case 2:
                            Mode = " +hv ";
                            break;
                        case 1:
                            Mode = " +v ";
                            break;
                        default:
                            Mode = " +v ";
                            break;
                    }
                    //IrcObject.IrcWriter.WriteLine("MODE " + IrcChannel + " " + Mode + " " + IrcUser + " " + IrcUser + " " + IrcUser);
                    //IrcObject.IrcWriter.Flush();
                }
                IrcObject.IrcWriter.WriteLine("PRIVMSG " + IrcChannel + " :Hi " + IrcUser + "!");
                IrcObject.IrcWriter.Flush();
                if (IrcChannel == IrcObject.IrcChannel)
                    Userlist.Add(IrcUser);
            }
            else
            {
                IrcObject.IrcWriter.WriteLine("PRIVMSG " + IrcChannel + " :Hello everyone!");
                IrcObject.IrcWriter.Flush();
                joined = true;
            }
        }

        void IrcObject_eventKick(string IrcChannel, string UserKicker, string UserKicked, string KickMessage)
        {
            if (UserKicked == IrcObject.IrcNick)
            {
                IrcObject.IrcWriter.WriteLine("JOIN " + IrcChannel);
                IrcObject.IrcWriter.Flush();
                IrcObject.IrcWriter.WriteLine("PRIVMSG " + IrcChannel + " :Hey, " + UserKicker + ", you kicked me? Bad " + UserKicker + ".");
                IrcObject.IrcWriter.Flush();
            }
            else
            {
                if (IrcChannel == IrcObject.IrcChannel)
                    Userlist.Remove(UserKicked);
            }
        }

        void IrcObject_eventReceiving(string IrcCommand)
        {
            // should listen to
            // :<nick>!n=<user>@<host> PRIVMSG <channel> :!<command>
            Console.WriteLine(IrcCommand); // Debug
            string[] Command = IrcCommand.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            //IniFile ini = new IniFile(Application.StartupPath + "/conf.ini");
            IConfigSource ini = new IniConfigSource(IniFilename);
            string Value = "";
            string Call = ":" + ini.Configs["Config"].Get("Call", "!");
            string Nick = Command[0].Substring(1).Split('!')[0];

            
            try
            {
                if (Command.Length >= 4)
                    if (Command[1] == "PRIVMSG")
                    {
                        string cmd = "";
                        int LengthParams = 4;
                        int AccessLevel = ini.Configs["Access"].GetInt(Nick, ini.Configs["Config"].GetInt("DefaultLevel", 0));
                        for (int i = 3; i < Command.Length; i++)
                            cmd += Command[i] + " ";
                        cmd = cmd.Remove(cmd.Length - 1); // Enlève l'espace final
                        ini.AutoSave = true;
                        //cmd = cmd.Remove(0, 1); // Enlève le : du début
                        string CallCheck;
                        try
                        {
                            CallCheck = cmd.Substring(0, Call.Length);
                        }
                        catch
                        {
                            CallCheck = "";
                        }

                        if (!Command[2].StartsWith("#") && !Command[2].StartsWith("&"))
                        {
                            Command[2] = Nick;
                        }

                        //string[] Commande = Command;
                        //Commande[3] = Commande[3].Remove(0, 1);

                        #region CTCP answers
                        if (Command[3] == ":\u0001VERSION\u0001")
                        {
                            IrcObject.IrcWriter.WriteLine("NOTICE " + Nick + " :\u0001VERSION ShadowBot v0.0alpha\u0001");
                            IrcObject.IrcWriter.Flush();
                        }
                        if (Command[3] == ":\u0001TIME\u0001")
                        {
                            IrcObject.IrcWriter.WriteLine("NOTICE " + Nick + " :\u0001TIME " + DateTime.Now.ToString() + "\u0001");
                            IrcObject.IrcWriter.Flush();
                        }
                        if (Command[3].StartsWith(":\u0001PING"))
                        {
                            IrcObject.IrcWriter.WriteLine("NOTICE " + Nick + " :\u0001PING" + cmd.Substring(6)); // + Math.Floor((DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime()).TotalSeconds).ToString() + "\u0001");
                            IrcObject.IrcWriter.Flush();
                        }
                        #endregion

                        for (int i = 3; i < Command.Length; i++)
                        {
                            if (i == 3)
                                Command[i] = Command[i].Remove(0, 1);

                            if (Command[i].StartsWith("http://") || Command[i].StartsWith("https://"))
                            {
                                try
                                {
                                    WebClient web = new WebClient();
                                    string webpage = web.DownloadString(new Uri(Command[i]));
                                    Match m = Regex.Match(webpage, @"(?<=<title.*>)([\s\S]*)(?=</title>)");
                                    if (m.Success)
                                    {
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Title: " + m.Groups[1].Value);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                catch (WebException ex)
                                {
                                    if (debug)
                                    {
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Nick + ", I think your link is not valid.");
                                        IrcObject.IrcWriter.Flush();
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Debug Info: " + ex.Message + ", Status: " + ex.Status.ToString() + ", Inner exception: " + ex.InnerException.Message + " Type: " + ex.InnerException.ToString());
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                            }
                            if (i == 3)
                                Command[i] = Command[i].Insert(0, ":");
                        }

                        if (CallCheck == Call)
                        {
                            Command[3] = Command[3].Remove(0, Call.Length);

                            string Notice;
                            if (ini.Configs["Config"].GetBoolean("Notice", false))
                                Notice = "NOTICE " + Nick + " ";
                            else
                                Notice = "PRIVMSG " + Command[2] + " ";

                            if (AccessLevel >= 0)
                            {
                                #region Utilities commands (dns, ping)
                                if (Command[3] == "md5" && Command.Length >= LengthParams + 1)
                                {
                                    string password = cmd.Substring(Command[3].Length + 3);
                                    byte[] original_bytes = Encoding.UTF8.GetBytes(password);
                                    byte[] encoded_bytes = new MD5CryptoServiceProvider().ComputeHash(original_bytes);
                                    StringBuilder result = new StringBuilder();
                                    for (int i = 0; i < encoded_bytes.Length; i++)
                                    {
                                        result.Append(encoded_bytes[i].ToString("x2"));
                                    }

                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + password + " - " + result.ToString());
                                    IrcObject.IrcWriter.Flush();
                                }
                                if (Command[3] == "dns" && Command.Length >= LengthParams + 1)
                                {
                                    try
                                    {
                                        string list = Command[4] + " =";
                                        IPAddress[] addresses = Dns.GetHostAddresses(Command[4]);
                                        foreach (IPAddress address in addresses)
                                        {
                                            list += " " + address.ToString();
                                        }
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + list);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    catch (Exception ex)
                                    {
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Error: " + ex.Message);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if (Command[3] == "rdns" && Command.Length >= LengthParams + 1)
                                {
                                    try
                                    {
                                        IPHostEntry ip = Dns.GetHostEntry(Command[4]);
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Command[4] + " = " + ip.HostName);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    catch (Exception ex)
                                    {

                                    }
                                }
                                if (Command[3] == "ping" && Command.Length >= LengthParams + 1)
                                {
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Sending ping to \"" + Command[4] + "\"...");
                                    IrcObject.IrcWriter.Flush();
                                    Ping ping = new Ping();
                                    PingReply reply;
                                    try
                                    {
                                        reply = ping.Send(Command[4]);
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Ping to " + String.Format("{0} {1} in {2}ms", reply.Address.ToString(), reply.Status, reply.RoundtripTime));
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    catch (Exception ex)
                                    {
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Ping to " + Command[4] + " failed: " + ex.Message);
                                        IrcObject.IrcWriter.Flush();
                                        return;
                                    }
                                }
                                if (Command[3] == "time")
                                {
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + DateTime.Now.ToString("dddd d MMMM yyyy HH:mm:sszzz"));
                                    IrcObject.IrcWriter.Flush();
                                }
                                if (Command[3] == "calc" && Command.Length >= LengthParams + 3)
                                {
                                    double Calc1, Calc2;
                                    try
                                    {
                                        Calc1 = Convert.ToDouble(Command[4]);
                                        Calc2 = Convert.ToDouble(Command[6]);
                                    }
                                    catch (Exception ex)
                                    {
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Error: " + ex.Message);
                                        IrcObject.IrcWriter.Flush();
                                        return;
                                    }
                                    try
                                    {
                                        switch (Command[5])
                                        {
                                            case "+":
                                                IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + (Calc1 + Calc2).ToString());
                                                IrcObject.IrcWriter.Flush();
                                                break;
                                            case "-":
                                                IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + (Calc1 - Calc2).ToString());
                                                IrcObject.IrcWriter.Flush();
                                                break;
                                            case "*":
                                                IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + (Calc1 * Calc2).ToString());
                                                IrcObject.IrcWriter.Flush();
                                                break;
                                            case "/":
                                                IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + (Calc1 / Calc2).ToString());
                                                IrcObject.IrcWriter.Flush();
                                                break;
                                            case "^":
                                                IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Math.Pow(Calc1, Calc2).ToString());
                                                IrcObject.IrcWriter.Flush();
                                                break;
                                            case "%":
                                                IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + (Calc1 % Calc2).ToString());
                                                IrcObject.IrcWriter.Flush();
                                                break;
                                            case "log":
                                                IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Math.Log(Calc2, Calc1).ToString());
                                                IrcObject.IrcWriter.Flush();
                                                break;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Error: " + ex.Message);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                #endregion
                                #region Game commands (coin, dice, roulette, money, give, love, time, start, stop, elapsed, calc)
                                #region Store commands
                                if (Command[3] == "store")
                                {
                                    string[] Items = ini.Configs["Store"].GetKeys();
                                    string[] Values = ini.Configs["Store"].GetValues();
                                    string[] Inventory = ini.Configs["Inventory"].GetValues();

                                    IrcObject.IrcWriter.WriteLine(Notice + " :Welcome to the ShadowStore!");
                                    IrcObject.IrcWriter.Flush();
                                    IrcObject.IrcWriter.WriteLine(Notice + " :The items are as follow. Type .buy <item> to buy one of the items.");
                                    IrcObject.IrcWriter.Flush();

                                    for (int i = 0; i < Items.Length; i++)
                                    {
                                        if (Inventory[i] != "0")
                                        {
                                            Thread.Sleep(1000);
                                            IrcObject.IrcWriter.WriteLine(Notice + " :" + Inventory[i] + " " + Items[i] + ": $" + Values[i] + " each");
                                            IrcObject.IrcWriter.Flush();
                                        }
                                    }
                                }
                                if (Command[3] == "buy" && Command.Length >= LengthParams + 1)
                                {
                                    if (ini.Configs["Store"].GetLong(Command[4], 0) != 0 && ini.Configs["Store"].GetLong(Command[4], 0) <= ini.Configs["Money"].GetLong(Nick, 0) && ini.Configs["Inventory"].GetLong(Command[4], 0) > 0)
                                    {
                                        ini.Configs["Money"].Set(Nick, ini.Configs["Money"].GetLong(Nick, 0) - ini.Configs["Store"].GetLong(Command[4], 0));
                                        ini.Configs["Items"].Set(Nick, ini.Configs["Items"].Get(Nick, "") + Command[4] + ",");
                                        ini.Configs["Inventory"].Set(Command[4], ini.Configs["Inventory"].GetLong(Command[4], 0) - 1);
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :You successfully bought " + Command[4] + "!");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :You can't buy " + Command[4] + "!");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }/*
                                if (Command[3] == "sell" && Command.Length >= LengthParams + 1)
                                {
                                    if (ini.Configs["Store"].GetLong(Command[4], 0) != 0 && ini.Configs["Store"].GetLong(Command[4], 0) <= ini.Configs["Money"].GetLong(Nick, 0))
                                    {
                                        ini.Configs["Money"].Set(Nick, ini.Configs["Money"].GetLong(Nick, 0) + ini.Configs["Store"].GetLong(Command[4], 0));
                                        ini.Configs["Items"].Set(Nick, ini.Configs["Items"].Get(Nick, "") + "," + Command[4]);
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :You successfully bought " + Command[4] + "!");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :You can't buy " + Command[4] + "!");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }*/
                                if (Command[3] == "inv" || Command[3] == "inventory")
                                {
                                    string[] inv = ini.Configs["Items"].Get(Nick, "").Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                                    IrcObject.IrcWriter.WriteLine(Notice + " :Inventory of " + Nick);
                                    IrcObject.IrcWriter.Flush();
                                    foreach (string Item in inv)
                                    {
                                        Thread.Sleep(1000);
                                        IrcObject.IrcWriter.WriteLine(Notice + " :" + Item);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if ((Command[3] == "inv" || Command[3] == "inventory") && Command.Length >= LengthParams + 1)
                                {
                                    string[] inv = ini.Configs["Items"].Get(Command[4], "").Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                                    IrcObject.IrcWriter.WriteLine(Notice + " :Inventory of " + Command[4]);
                                    IrcObject.IrcWriter.Flush();
                                    foreach (string Item in inv)
                                    {
                                        Thread.Sleep(1000);
                                        IrcObject.IrcWriter.WriteLine(Notice + " :" + Item);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                #endregion
                                #region Gambling commands (coin, dice)
                                if (Command[3] == "coin" && Command.Length >= LengthParams + 1)
                                {
                                    long bet;
                                    try
                                    {
                                        bet = Convert.ToInt64(Command[4]);
                                    }
                                    catch
                                    {
                                        return;
                                    }
                                    if (ini.Configs["Money"].GetLong(Nick, 0) >= bet && bet >= 0)
                                    {
                                        Random r = new Random();
                                        int num = r.Next(0, 2);
                                        if (bet == 1337 || bet == 31337 || bet == 1337000)
                                            num = 0;
                                        if (num == 0)
                                        {
                                            ini.Configs["Money"].Set(Nick, ini.Configs["Money"].GetLong(Nick, 0) + bet);
                                            IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Congrats, " + Nick + ", the coin landed on heads!!! You won " + bet.ToString() + " dollars!");
                                            IrcObject.IrcWriter.Flush();
                                        }
                                        else
                                        {
                                            ini.Configs["Money"].Set(Nick, ini.Configs["Money"].GetLong(Nick, 0) - bet);
                                            IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Aw, " + Nick + ", the coin landed on tails... You lose " + bet.ToString() + " dollars!");
                                            IrcObject.IrcWriter.Flush();
                                        }
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Nick + ", you don't have " + bet.ToString() + " dollars to bet on this!");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if (Command[3] == "koinz" && Command.Length >= LengthParams + 1)
                                {
                                    long bet;
                                    try
                                    {
                                        bet = Convert.ToInt64(Command[4]);
                                    }
                                    catch
                                    {
                                        return;
                                    }
                                    ini.Configs["Money"].Set(Nick, ini.Configs["Money"].GetLong(Nick, 0) + bet);
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Congrats, " + Nick + ", the fake coin with 2 heads landed on heads!!! You won " + bet.ToString() + " dollars!");
                                    IrcObject.IrcWriter.Flush();
                                }
                                if (Command[3] == "dice" && Command.Length == LengthParams + 1)
                                {
                                    long bet;
                                    string Nick2;
                                    try
                                    {
                                        bet = Convert.ToInt32(Command[4]);
                                    }
                                    catch
                                    {
                                        return;
                                    }

                                    if (bet > 0)
                                    {
                                        if (ini.Configs["Money"].GetLong(Nick, 0) >= bet)
                                        {
                                            IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Waiting for another player... Type " + Call.Substring(1) + "dice to join.");
                                            IrcObject.IrcWriter.Flush();

                                            string[] JoinCmd;

                                        game:
                                            while (true)
                                            {
                                                JoinCmd = IrcObject.IrcReader.ReadLine().Split(' ');
                                                if (JoinCmd[3] == Call + "dice")
                                                {
                                                    Nick2 = JoinCmd[0].Split('!')[0].Substring(1);
                                                    if (ini.Configs["Money"].GetLong(Nick2, 0) >= bet)
                                                    {
                                                        break;
                                                    }
                                                    else
                                                    {
                                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Nick2 + ", you don't have " + bet.ToString() + " dollars to bet on this! Type " + Call + "dice to join.");
                                                        IrcObject.IrcWriter.Flush();
                                                        goto game;
                                                    }
                                                }

                                                if (JoinCmd[3] == Call + "stop")
                                                {
                                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :The game is stopped, type " + Call.Substring(1) + "dice <bet> to start a new game.");
                                                    IrcObject.IrcWriter.Flush();
                                                    return;
                                                }
                                            }

                                            if (Nick == Nick2)
                                            {
                                                IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Nick + ", you want to play with yourself? Type " + Call.Substring(1) + "dice to join.");
                                                IrcObject.IrcWriter.Flush();
                                                goto game;
                                            }
                                            IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Nick2 + " joined " + Nick + " for a game of dice!");
                                            IrcObject.IrcWriter.Flush();

                                            Random r = new Random();
                                            int num11 = r.Next(1, 7);
                                            int num12 = r.Next(1, 7);
                                            int num1 = num11 + num12;
                                            int num21 = r.Next(1, 7);
                                            int num22 = r.Next(1, 7);
                                            int num2 = num21 + num22;



                                            if (num1 > num2)
                                            {
                                                ini.Configs["Money"].Set(Nick, ini.Configs["Money"].GetLong(Nick, 0) + bet);
                                                ini.Configs["Money"].Set(Nick2, ini.Configs["Money"].GetLong(Nick2, 0) - bet);

                                                IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Nick + "'s die landed on " + num1.ToString() + " and " + Nick2 + "'s die landed on " + num2.ToString() + "! Congrats, " + Nick + ", you won " + bet.ToString() + " dollars!");
                                                IrcObject.IrcWriter.Flush();
                                            }
                                            else if (num1 < num2)
                                            {
                                                ini.Configs["Money"].Set(Nick, ini.Configs["Money"].GetLong(Nick, 0) - bet);
                                                ini.Configs["Money"].Set(Nick2, ini.Configs["Money"].GetLong(Nick2, 0) + bet);

                                                IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Nick + "'s die landed on " + num1.ToString() + " and " + Nick2 + "'s die landed on " + num2.ToString() + "! Congrats, " + Nick2 + ", you won " + bet.ToString() + " dollars!");
                                                IrcObject.IrcWriter.Flush();
                                            }
                                            else
                                            {
                                                IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Both " + Nick + " and " + Nick2 + " got " + num1.ToString() + "... It's a tie! Nobody won the prize of " + bet.ToString() + " dollars!");
                                                IrcObject.IrcWriter.Flush();
                                            }
                                        }
                                        else
                                        {
                                            IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Nick + ", you don't have " + bet.ToString() + " dollars to bet on this!");
                                            IrcObject.IrcWriter.Flush();
                                        }
                                    }
                                }
                                if (Command[3] == "dice" && Command.Length >= LengthParams + 2)
                                {
                                    int number;
                                    long bet;
                                    try
                                    {
                                        number = Convert.ToInt32(Command[4]);
                                        bet = Convert.ToInt64(Command[5]);
                                    }
                                    catch
                                    {
                                        return;
                                    }
                                    if (number >= 2 && number <= 12 && bet > 0)
                                    {
                                        if (ini.Configs["Money"].GetLong(Nick, 0) >= bet)
                                        {
                                            Random r = new Random();
                                            int num1 = r.Next(1, 7);
                                            int num2 = r.Next(1, 7);
                                            int num = num1 + num2;
                                            if (num == number)
                                            {
                                                ini.Configs["Money"].Set(Nick, ini.Configs["Money"].GetLong(Nick, 0) + bet);
                                                IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Congrats, " + Nick + ", the die landed on " + num.ToString() + ", you won " + bet.ToString() + " dollars!");
                                                IrcObject.IrcWriter.Flush();
                                            }
                                            else
                                            {
                                                ini.Configs["Money"].Set(Nick, ini.Configs["Money"].GetLong(Nick, 0) - bet);
                                                IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Aw, " + Nick + ", the die landed on " + num.ToString() + "... You lose " + bet.ToString() + " dollars!");
                                                IrcObject.IrcWriter.Flush();
                                            }
                                        }
                                        else
                                        {
                                            IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Nick + ", you don't have " + bet.ToString() + " dollars to bet on this!");
                                            IrcObject.IrcWriter.Flush();
                                        }
                                    }
                                }
                                #endregion
                                #region Money commands (money, give)
                                if (Command[3] == "money" && Command.Length == LengthParams)
                                {
                                    long money = ini.Configs["Money"].GetLong(Nick, 0);

                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Nick + " has " + money.ToString() + " dollars.");
                                    IrcObject.IrcWriter.Flush();
                                }
                                if (Command[3] == "money" && Command.Length >= LengthParams + 1)
                                {
                                    long money = ini.Configs["Money"].GetLong(Command[4], 0);

                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Command[4] + " has " + money.ToString() + " dollars.");
                                    IrcObject.IrcWriter.Flush();
                                }
                                if (Command[3] == "give" && Command.Length >= LengthParams + 2)
                                {
                                    long money1 = ini.Configs["Money"].GetLong(Nick, 0);
                                    long money2 = ini.Configs["Money"].GetLong(Command[4], 0);
                                    long GivenMoney;
                                    try
                                    {
                                        GivenMoney = Convert.ToInt64(Command[5]);
                                    }
                                    catch
                                    {
                                        return;
                                    }
                                    if (money1 >= GivenMoney && GivenMoney >= 0 && Nick != Command[4])
                                    {
                                        money1 -= GivenMoney;
                                        money2 += GivenMoney;
                                        ini.Configs["Money"].Set(Nick, money1);
                                        ini.Configs["Money"].Set(Command[4], money2);
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Nick + " has given " + Command[4] + " " + GivenMoney.ToString() + " dollars.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Nick + " doesn't have " + GivenMoney.ToString() + " dollars to give to " + Command[4] + ".");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                
                                if (Command[3] == "steal" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= 5)
                                    {
                                        long money1 = ini.Configs["Money"].GetLong(Nick, 0);
                                        long money2 = ini.Configs["Money"].GetLong(Command[4], 0);
                                        long GivenMoney;
                                        try
                                        {
                                            GivenMoney = Convert.ToInt64(Command[5]);
                                        }
                                        catch
                                        {
                                            return;
                                        }
                                        if (money2 >= GivenMoney && GivenMoney >= 0 && Nick != Command[4])
                                        {
                                            money1 += GivenMoney;
                                            money2 -= GivenMoney;
                                            ini.Configs["Money"].Set(Nick, money1);
                                            ini.Configs["Money"].Set(Command[4], money2);
                                            IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Command[4] + " has given " + Nick + " " + GivenMoney.ToString() + " dollars.");
                                            IrcObject.IrcWriter.Flush();
                                        }
                                        else
                                        {
                                            IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Command[4] + " doesn't have " + GivenMoney.ToString() + " dollars to give to " + Nick + ".");
                                            IrcObject.IrcWriter.Flush();
                                        }
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                #endregion
                                #region Roulette commands
                                if (Command[3] == "reg")
                                {
                                    if (!started)
                                    {
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " +v " + Nick);
                                        IrcObject.IrcWriter.Flush();
                                        if (!Registered.Contains(Nick))
                                        {
                                            Registered.Add(Nick);
                                            IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :\x02\x03\x00036You are now registered for the roulette game! Type .startroulette to start the game!\x03\x02");
                                            IrcObject.IrcWriter.Flush();
                                        }
                                    }
                                }
                                if (Command[3] == "del")
                                {
                                    IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " -v " + Nick);
                                    IrcObject.IrcWriter.Flush();
                                    if (Registered.Contains(Nick))
                                    {
                                        Registered.Remove(Nick);
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :\x02\x03\x00036You unregistered for the roulette game! Type .reg to register again!\x03\x02");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if (Command[3] == "startroulette")
                                {
                                    if (Registered.Count >= 2)
                                    {
                                        started = true;
                                        Random r = new Random();
                                        roulette = r.Next(0, 6);
                                        string cs = "";
                                        foreach (string person in Registered)
                                            cs += " " + person;
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " +m");
                                        IrcObject.IrcWriter.Flush();
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :\x02\x03\x00036Roulette game started! Type .roulette to play! Are you ready," + cs + "?\x03\x02");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :\x02\x03\x00036The game didn't start because there not enough people registered! Type .reg to register!\x03\x02");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if (Command[3] == "roulette")
                                {
                                    if (started && Registered.Contains(Nick))
                                    {
                                        if (roulette == 0)
                                        {
                                            IrcObject.IrcWriter.WriteLine("KICK " + Command[2] + " " + Nick + " :BAM!!! You're dead! ^_^");
                                            IrcObject.IrcWriter.Flush();
                                            Registered.Remove(Nick);
                                            if (Registered.Count == 1)
                                            {
                                                IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :\x02\x03\x00036Congrats, " + Registered.ToArray()[0] + ", you are the last person alive! You won!\x03\x02");
                                                IrcObject.IrcWriter.Flush();
                                                IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " -v " + Registered.ToArray()[0]);
                                                IrcObject.IrcWriter.Flush();
                                                IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " -m");
                                                IrcObject.IrcWriter.Flush();
                                                Registered.Clear();
                                                started = false;
                                            }
                                            Random r = new Random();
                                            roulette = r.Next(0, 6);
                                        }
                                        else
                                        {
                                            IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :\x02\x03\x00036CLICK\x03\x02");
                                            IrcObject.IrcWriter.Flush();
                                            roulette--;
                                        }
                                    }
                                }
                                if (Command[3] == "stoproulette")
                                {
                                    if (AccessLevel >= 2)
                                    {
                                        if (started)
                                        {
                                            started = false;
                                            IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " -m");
                                            IrcObject.IrcWriter.Flush();
                                            foreach (string User in Registered)
                                            {
                                                IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " -v " + User);
                                                IrcObject.IrcWriter.Flush();
                                                Thread.Sleep(1000);
                                            }
                                            Registered.Clear();
                                            IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :\x02\x03\x00036Roulette game stopped!\x03\x02");
                                            IrcObject.IrcWriter.Flush();
                                        }
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if (Command[3] == "roulettestats")
                                {
                                    string cs = "";
                                    foreach (string person in Registered)
                                        cs += " " + person;
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :\x02\x03\x00036Still alive people are" + cs + ".\x03\x02");
                                    IrcObject.IrcWriter.Flush();
                                }
                                if (Command[3] == "goat")
                                {
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :\x02\x03\x00036" + cmd.Substring(Command[3].Length + 3) + " has been kicked by a goat. (by " + Nick + ")\x03\x02");
                                    IrcObject.IrcWriter.Flush();
                                }
                                #endregion
                                //<Enflamed> DANCE PARTAY TIME!!!! (bleu-rouge-jaune-vert)
                                //<Enflamed> o|-< 
                                //<Enflamed> o\-< 
                                //<Enflamed> o|-< 
                                //<Enflamed> o/-< 
                                //<Enflamed> o|-< 
                                //<Enflamed> o\-<
                                
                                //<Enflamed> ITS KIRBY TIME YOU MO FOS! 
                                //<Enflamed> <(^.^<) (pink)
                                //<Enflamed> <(^.^)> 
                                //<Enflamed> (>^.^)> (/pink)
                                //<Enflamed> I LOVE KIRBY SOOOO MUCH! 
                                //<Enflamed> <(^.^<) (pink)
                                //<Enflamed> <(^.^)> 
                                //<Enflamed> (>^.^)> (/pink)

                                if (Command[3] == "dance")
                                {
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :\u0002\u000302DANCE \u000305PARTAY \u000308TIME\u000311!!!!");
                                    IrcObject.IrcWriter.Flush();
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :o|-<");
                                    IrcObject.IrcWriter.Flush();
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :o\\-<");
                                    IrcObject.IrcWriter.Flush();
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :o|-<");
                                    IrcObject.IrcWriter.Flush();
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :o/-<");
                                    IrcObject.IrcWriter.Flush();
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :o|-<");
                                    IrcObject.IrcWriter.Flush();
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :o\\-<");
                                    IrcObject.IrcWriter.Flush();
                                }
                                if (Command[3] == "kirby")
                                {
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :ITS KIRBY TIME YOU MO FOS!");
                                    IrcObject.IrcWriter.Flush();
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :\u0002\u000313<(^.^<)");
                                    IrcObject.IrcWriter.Flush();
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :\u0002\u000313<(^.^)>");
                                    IrcObject.IrcWriter.Flush();
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :\u0002\u000313(>^.^)>");
                                    IrcObject.IrcWriter.Flush();
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :I LOVE KIRBY SOOOO MUCH!");
                                    IrcObject.IrcWriter.Flush();
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :\u0002\u000313<(^.^<)");
                                    IrcObject.IrcWriter.Flush();
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :\u0002\u000313<(^.^)>");
                                    IrcObject.IrcWriter.Flush();
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :\u0002\u000313(>^.^)>");
                                    IrcObject.IrcWriter.Flush();
                                }
                                if (Command[3] == "love" && Command.Length >= LengthParams + 2)
                                {
                                    string fname = Command[4];
                                    string sname = Command[5];
                                    string love;
                                    string first = fname.ToUpper();
                                    int firstlength = fname.Length;
                                    string second = sname.ToUpper();
                                    int secondlength = sname.Length;
                                    int LoveCount = 0;
                                    for (int Count = 0; Count < firstlength; Count++)
                                    {
                                        string letter1 = first.Substring(Count, 1);
                                        if (letter1.Equals("A")) LoveCount += 2;
                                        if (letter1.Equals("E")) LoveCount += 2;
                                        if (letter1.Equals("I")) LoveCount += 2;
                                        if (letter1.Equals("O")) LoveCount += 2;
                                        if (letter1.Equals("U")) LoveCount += 3;
                                        if (letter1.Equals("A")) LoveCount += 1;
                                        if (letter1.Equals("E")) LoveCount += 3;
                                    }
                                    for (int Count = 0; Count < secondlength; Count++)
                                    {
                                        string letter2 = second.Substring(Count, 1);
                                        if (letter2.Equals("A")) LoveCount += 2;
                                        if (letter2.Equals("E")) LoveCount += 2;
                                        if (letter2.Equals("I")) LoveCount += 2;
                                        if (letter2.Equals("O")) LoveCount += 2;
                                        if (letter2.Equals("U")) LoveCount += 3;
                                        if (letter2.Equals("A")) LoveCount += 1;
                                        if (letter2.Equals("E")) LoveCount += 3;
                                    }
                                    int amount = 0;
                                    if (LoveCount > 0) amount = 5 - ((firstlength + secondlength) / 2);
                                    if (LoveCount > 2) amount = 10 - ((firstlength + secondlength) / 2);
                                    if (LoveCount > 4) amount = 20 - ((firstlength + secondlength) / 2);
                                    if (LoveCount > 6) amount = 30 - ((firstlength + secondlength) / 2);
                                    if (LoveCount > 8) amount = 40 - ((firstlength + secondlength) / 2);
                                    if (LoveCount > 10) amount = 50 - ((firstlength + secondlength) / 2);
                                    if (LoveCount > 12) amount = 60 - ((firstlength + secondlength) / 2);
                                    if (LoveCount > 14) amount = 70 - ((firstlength + secondlength) / 2);
                                    if (LoveCount > 16) amount = 80 - ((firstlength + secondlength) / 2);
                                    if (LoveCount > 18) amount = 90 - ((firstlength + secondlength) / 2);
                                    if (LoveCount > 20) amount = 100 - ((firstlength + secondlength) / 2);
                                    if (LoveCount > 22) amount = 110 - ((firstlength + secondlength) / 2);
                                    if (firstlength == 0 || secondlength == 0) amount = 0;
                                    if (amount < 0) amount = 0;
                                    if (amount > 100) amount = 100;
                                    love = amount.ToString();
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :\x03\x00035" + fname + " + " + sname + " = " + love + "%\x03");
                                    IrcObject.IrcWriter.Flush();
                                }
                                #region Stopwatch commands (stopwatch, elapsed)
                                if (Command[3] == "stopwatch")
                                {
                                    if (!sw.IsRunning)
                                    {
                                        sw.Reset();
                                        sw.Start();
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Stopwatch started.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        sw.Stop();
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Stopwatch stopped: " + sw.Elapsed.ToString() + " elapsed.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if (Command[3] == "elapsed")
                                {
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Nick + ": " + sw.Elapsed.ToString() + " elapsed.");
                                    IrcObject.IrcWriter.Flush();
                                }
                                #endregion
                                #endregion
                                #region Op commands
                                #region Mute commands (mute, unmute)
                                if (Command[3] == "mute" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= 2)
                                    {
                                        ini.Configs["Mute"].Set(Command[4], true);
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " +b %" + Command[4]);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                else if (Command[3] == "unmute" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= 2)
                                    {
                                        //ini.Configs["Mute"].Set(Command[4], false);
                                        ini.Configs["Mute"].Remove(Command[4]);
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " -b %" + Command[4]);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                #endregion
                                #region Super-operator commands (aop, deaop)
                                if (Command[3] == "aop" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= 4)
                                    {
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " +a " + Command[4]);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                else if (Command[3] == "deaop" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= 4)
                                    {
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " -a " + Command[4]);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                #endregion
                                #region Operator commands (op, deop)
                                if (Command[3] == "op" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= 3)
                                    {
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " +o " + Command[4]);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                else if (Command[3] == "deop" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= 3)
                                    {
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " -o " + Command[4]);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                #endregion
                                #region Half-operator commands (hop, dehop)
                                if (Command[3] == "hop" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= 2)
                                    {
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " +h " + Command[4]);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                else if (Command[3] == "dehop" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= 2)
                                    {
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " -h " + Command[4]);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                #endregion
                                #region Voice commands (voice, devoice)
                                if (Command[3] == "voice" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= 2)
                                    {
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " +v " + Command[4]);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                else if (Command[3] == "devoice" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= 2)
                                    {
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " -v " + Command[4]);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                #endregion
                                #endregion
                                #region Access list management (adduser, deluser)
                                if (Command[3] == "identify" && Command.Length >= LengthParams + 2)
                                {

                                }
                                if (Command[3] == "adduser" && Command.Length >= LengthParams + 2)
                                {
                                    int Param;
                                    try
                                    {
                                        Param = Convert.ToInt32(Command[4]);
                                    }
                                    catch
                                    {
                                        return;
                                    }
                                    if (AccessLevel >= Param)
                                    {
                                        ini.Configs["Access"].Set(Command[5], Param);
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Command[5] + " added to access list with level " + Param.ToString() + "!");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                else if (Command[3] == "deluser" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= ini.Configs["Access"].GetInt(Command[4], 2) && AccessLevel >= 2)
                                    {
                                        //ini.Configs["Access"].Set(Command[4], false);
                                        ini.Configs["Access"].Remove(Command[4]);
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Command[4] + " removed from access list!");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                #endregion
                                #region Self-op commands
                                #region Super-operator commands (aopme, deaopme)
                                if (Command[3] == "aopme")
                                {
                                    if (AccessLevel >= 4)
                                    {
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " +a " + Nick);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if (Command[3] == "deaopme")
                                {
                                    //if (AccessLevel)
                                    //{
                                    IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " -a " + Nick);
                                    IrcObject.IrcWriter.Flush();
                                    //}
                                    //else
                                    //{
                                    //IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Sorry, " + Command[4] + ", you're not on the access list!");
                                    //IrcObject.IrcWriter.Flush();
                                    //}
                                }
                                #endregion
                                #region Operator commands (opme, deopme)
                                if (Command[3] == "opme")
                                {
                                    if (AccessLevel >= 3)
                                    {
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " +o " + Nick);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if (Command[3] == "deopme")
                                {
                                    //if (AccessLevel)
                                    //{
                                    IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " -o " + Nick);
                                    IrcObject.IrcWriter.Flush();
                                    //}
                                    //else
                                    //{
                                    //IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Sorry, " + Command[4] + ", you're not on the access list!");
                                    //IrcObject.IrcWriter.Flush();
                                    //}
                                }
                                #endregion
                                #region Half-operator commands (hopme, dehopme)
                                if (Command[3] == "hopme")
                                {
                                    if (AccessLevel >= 2)
                                    {
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " +h " + Nick);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if (Command[3] == "dehopme")
                                {
                                    //if (AccessLevel)
                                    //{
                                    IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " -h " + Nick);
                                    IrcObject.IrcWriter.Flush();
                                    //}
                                    //else
                                    //{
                                    //IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Sorry, " + Command[4] + ", you're not on the access list!");
                                    //IrcObject.IrcWriter.Flush();
                                    //}
                                }
                                #endregion
                                #region Voice commands (voiceme, devoiceme)
                                if (Command[3] == "voiceme")
                                {
                                    if (AccessLevel >= 1)
                                    {
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " +v " + Nick);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if (Command[3] == "devoiceme")
                                {
                                    //if (AccessLevel)
                                    //{
                                    IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " -v " + Nick);
                                    IrcObject.IrcWriter.Flush();
                                    //}
                                    //else
                                    //{
                                    //IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Sorry, " + Command[4] + ", you're not on the access list!");
                                    //IrcObject.IrcWriter.Flush();
                                    //}
                                }
                                #endregion
                                #endregion
                                #region Channel management
                                #region Say commands (say, sayc)
                                if (Command[3] == "say" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= 2)
                                    {
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + cmd.Substring(Command[3].Length + 3));
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if (Command[3] == "sayc" && Command.Length >= LengthParams + 2)
                                {
                                    if (AccessLevel >= 2)
                                    {
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[4] + " :" + cmd.Substring(Command[3].Length + Command[4].Length + 4));
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if (Command[3] == "ctcp" && Command.Length >= LengthParams + 2)
                                {
                                    if (AccessLevel >= 2)
                                    {
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[4] + " :\x0001" + cmd.Substring(Command[3].Length + Command[4].Length + 4) + "\x0001");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                #endregion
                                #region Act commands (act, actc)
                                if (Command[3] == "act" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= 2)
                                    {
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :\u0001ACTION " + cmd.Substring(Command[3].Length + 3) + "\u0001");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if (Command[3] == "actc" && Command.Length >= LengthParams + 2)
                                {
                                    if (AccessLevel >= 2)
                                    {
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[4] + " :\u0001ACTION " + cmd.Substring(Command[3].Length + Command[4].Length + 4) + "\u0001");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                #endregion
                                #region Bot nick commands (nick)
                                if (Command[3] == "nick" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= 2)
                                    {
                                        IrcObject.IrcWriter.WriteLine("NICK " + Command[4]);
                                        IrcObject.IrcWriter.Flush();
                                        IrcObject.IrcNick = Command[4];
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                #endregion
                                #region Join/part commands (join, part)
                                if (Command[3] == "join" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= 2)
                                    {
                                        IrcObject.IrcWriter.WriteLine("JOIN " + Command[4]);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if (Command[3] == "part" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= 2)
                                    {
                                        IrcObject.IrcWriter.WriteLine("PART " + Command[4]);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                #endregion
                                if (Command[3] == "mode" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= 4)
                                    {
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + cmd.Substring(Command[3].Length + 3));
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if (Command[3] == "names")
                                {
                                    string cs = "Ping from " + Nick + ":";
                                    string[] users = Userlist.ToArray();
                                    foreach (string user in users)
                                        cs += " " + user;
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + cs);
                                    IrcObject.IrcWriter.Flush();
                                }
                                #endregion
                                #region Kick/ban commands
                                if ((Command[3] == "kick" || Command[3] == "k") && Command.Length >= LengthParams + 2)
                                {
                                    if (AccessLevel >= ini.Configs["Access"].GetInt(Command[4], 2) && AccessLevel >= 2)
                                    {
                                        IrcObject.IrcWriter.WriteLine("KICK " + Command[2] + " " + Command[4] + " :" + cmd.Substring(Command[3].Length + Command[4].Length + 4));
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if ((Command[3] == "ban" || Command[3] == "b") && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= ini.Configs["Access"].GetInt(Command[4], 2) && AccessLevel >= 2)
                                    {
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " +b " + Command[4]);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if ((Command[3] == "unban" || Command[3] == "ub") && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= ini.Configs["Access"].GetInt(Command[4], 2) && AccessLevel >= 2)
                                    {
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " -b " + Command[4]);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if ((Command[3] == "kickban" || Command[3] == "kb") && Command.Length >= LengthParams + 2)
                                {
                                    if (AccessLevel >= ini.Configs["Access"].GetInt(Command[4], 2) && AccessLevel >= 2)
                                    {
                                        IrcObject.IrcWriter.WriteLine("MODE " + Command[2] + " +b " + Command[4] + "!*@*");
                                        IrcObject.IrcWriter.Flush();
                                        IrcObject.IrcWriter.WriteLine("KICK " + Command[2] + " " + Command[4] + " :" + cmd.Substring(Command[3].Length + Command[4].Length + 4));
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                #endregion
                                #region Help commands (man, accesslist, userlist, mutelist, opcmds, ophelp, commands, help, cmds)
                                if ((Command[3] == "man" || Command[3] == "help") && Command.Length >= LengthParams + 1)
                                {
                                    string[] lines = new string[4];
                                    lines[0] = "Usage: " + Call.Substring(1) + Command[4] + " ";
                                    lines[1] = "Access level: ";
                                    lines[2] = "Package: ";
                                    lines[3] = "";

                                    switch (Command[4])
                                    {
                                        case "man":
                                        case "help":
                                            lines[0] += "<command>";
                                            lines[1] += "0";
                                            lines[2] += "help";
                                            lines[3] += "This command gives help and usage of <command>. This command is still in construction, so ask for juju2143 for the usages.";
                                            break;
                                        case "accesslist":
                                        case "userlist":
                                            lines[0] += "";
                                            lines[1] += "0";
                                            lines[2] += "help";
                                            lines[3] += "This command gives the access list.";
                                            break;
                                        default:
                                            lines[0] = "This command doesn't exist or is not documented. Type !cmds or !opcmds for a list of commands. This command is still in construction, so ask for juju2143 for the usages.";
                                            lines[1] = ""; lines[2] = ""; lines[3] = "";
                                            break;
                                    }
                                    foreach (string line in lines)
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :" + line);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                if (Command[3] == "accesslist" || Command[3] == "userlist")
                                {
                                    string list = "List of users:";
                                    string[] defs = ini.Configs["Access"].GetKeys();
                                    foreach (string def in defs)
                                    {
                                        list += " " + def + " (" + ini.Configs["Access"].Get(def) + ")";
                                    }
                                    //IrcObject.IrcWriter.WriteLine("NOTICE " + Command[0].Substring(1).Split('!')[0] + " :" + list);
                                    IrcObject.IrcWriter.WriteLine(Notice + " :" + list);
                                    IrcObject.IrcWriter.Flush();
                                }
                                if (Command[3] == "mutelist")
                                {
                                    string list = "Muted people:";
                                    string[] defs = ini.Configs["Mute"].GetKeys();
                                    foreach (string def in defs)
                                    {
                                        list += " " + def;
                                    }
                                    //IrcObject.IrcWriter.WriteLine("NOTICE " + Command[0].Substring(1).Split('!')[0] + " :" + list);
                                    IrcObject.IrcWriter.WriteLine(Notice + " :" + list);
                                    IrcObject.IrcWriter.Flush();
                                }
                                if (Command[3] == "opcmds" || Command[3] == "opcommands")
                                {
                                    IrcObject.IrcWriter.WriteLine(Notice + " :List of restricted commands: add (2), adduser (2), (de)aop(me) (4), del (2), deluser (2), (de)hop(me) (2), join (2), (un)mute (2), nick (2), (de)op(me) (3), part (2), say (2), sayc (2), (de)voice(me) (2) (de-commands are (0))");
                                    IrcObject.IrcWriter.Flush();
                                    IrcObject.IrcWriter.WriteLine(Notice + " :For help on a command, type !help <command>.");
                                    IrcObject.IrcWriter.Flush();
                                }
                                if (Command[3] == "commands" || Command[3] == "cmds")
                                {
                                    StreamReader sr = new StreamReader(IniFilename);
                                    string str = sr.ReadLine();
                                    string list = "List of commands: accesslist, calc, cmds, coin, commands, dice, dns, elapsed, help, give, love, man, money, mutelist, opcmds, opcommands, ping, roulette, stopwatch, time, userlist";
                                    string[] defs = ini.Configs["Defs"].GetKeys();
                                    /*
                                    while (str != "[Defs]")
                                    {
                                        str = sr.ReadLine();
                                    }

                                    while (str != "" && str != null)
                                    {
                                        str = sr.ReadLine();
                                        if (str != "" && str != null)
                                        {
                                            list += ", ";
                                            list += str.Split(' ', '=')[0];
                                        }

                                        //IrcObject.IrcWriter.WriteLine("NOTICE " + Command[0].Substring(1).Split('!')[0] + " :" + str.Split(' ', '=')[0]);
                                        //IrcObject.IrcWriter.Flush();
                                        //Thread.Sleep(2000);
                                    }
                                    */
                                    Array.Sort(defs);
                                    foreach (string def in defs)
                                    {
                                        list += ", " + def;
                                    }
                                    //IrcObject.IrcWriter.WriteLine("NOTICE " + Command[0].Substring(1).Split('!')[0] + " :" + list);
                                    IrcObject.IrcWriter.WriteLine(Notice + " :" + list);
                                    IrcObject.IrcWriter.Flush();
                                    IrcObject.IrcWriter.WriteLine(Notice + " :For help on a command, type " + Call.Substring(1) + "help <command>.");
                                    IrcObject.IrcWriter.Flush();

                                    sr.Close();
                                }
                                #endregion
                                #region Bot management
                                /*if (Command[3] == ":!die")
                    {
                        IrcObject.IrcWriter.WriteLine("QUIT Bye everyone!");
                        IrcObject.IrcWriter.Flush();
                    }*/
                                /*
                                if (Command[3] == ":!restart")
                                {
                                    IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Restarting... brb!");
                                    IrcObject.IrcWriter.Flush();
                                    IrcObject.IrcWriter.WriteLine("QUIT");
                                    IrcObject.IrcWriter.Flush();
                                    IrcObject.IrcReader.Close();
                                    IrcObject.IrcWriter.Close();
                                    IrcObject.IrcStream.Close();
                                    System.Windows.Forms.Application.Restart();
                                }
                                if (Command[3] == "exec" && Command.Length >= LengthParams + 1)
                                {
                                    string buffer;
                                    Process p = Process.Start(cmd.Substring(Command[3].Length + 2));
                                    while (!p.HasExited)
                                    {
                                        buffer = IrcObject.IrcReader.ReadLine().Split(' ');
                                        if (buffer.Length >= 5)
                                        {
                                            if (buffer[3] == ":" + Call + "z")
                                            {

                                            }
                                            else if (buffer[3] == ":" + Call + "kill")
                                            {
                                                p.Kill();
                                            }
                                        }
                                    }
                                IrcObject.IrcWriter.WriteLine("PRIVMSG" + Command[2] + " :The process has ended.");
                                IrcObject.IrcWriter.Flush();
                                }*/
                                #endregion
                                #region Definition commands (add, del, )
                                if (Command[3] == "add" && Command.Length >= LengthParams + 2)
                                {
                                    if (AccessLevel >= 2)
                                    {
                                        //ini.IniWriteValue(/*Command[2]*/"Defs", Command[4], cmd.Substring(Command[3].Length + Command[4].Length + 2));
                                        ini.Configs["Defs"].Set(Command[4].ToLower(), cmd.Substring(Command[3].Length + Command[4].Length + 4));
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Definition of " + Command[4] + " added.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                else if (Command[3] == "del" && Command.Length >= LengthParams + 1)
                                {
                                    if (AccessLevel >= 2)
                                    {
                                        //ini.IniWriteValue(/*Command[2]*/"Defs", Command[4], null);
                                        //ini.Configs["Defs"].Set(Command[4].ToLower(), "");
                                        ini.Configs["Defs"].Remove(Command[4].ToLower());
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :Definition of " + Command[4] + " deleted.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                    else
                                    {
                                        IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                else //if (Command[3].StartsWith(""))
                                {
                                    Value = ini.Configs["Defs"].Get(Command[3].ToLower(), ""); //ini.IniReadValue(/*Command[2]*/"Defs", Command[3].Substring(2));
                                    if (Value != "" && Value != null)
                                    {
                                        IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" /*+ Command[3] + ": " */+ Value);
                                        IrcObject.IrcWriter.Flush();
                                    }
                                }
                                #endregion
                            }
                            else
                            {
                                IrcObject.IrcWriter.WriteLine(Notice + " :Access denied.");
                                IrcObject.IrcWriter.Flush();
                            }
                            ini.Save();
                        }
                        else if (cmd.Contains(IrcObject.IrcNick))
                        {
                            //IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :yes?");
                            //IrcObject.IrcWriter.Flush();
                        }
                    }
            }
            catch (Exception ex)
            {
                IrcObject.IrcWriter.WriteLine("PRIVMSG " + Command[2] + " :" + Nick + ", I think you broke me, you should tell my master what's happened and tell him this message: " + ex.Message);
                IrcObject.IrcWriter.Flush();
            }
        }
    }
}

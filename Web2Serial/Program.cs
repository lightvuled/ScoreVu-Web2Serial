using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.IO.Ports;
using System.Security.Cryptography;
using Newtonsoft.Json;

namespace Web2Serial
{
    class Program
    {
        struct gameSettings
        {
            public int clockLeft;
            public int clockRight;
            public string clockMiddle;
            public int period;
            public double shotClock;
            public double timeOutClock;
            public bool timeOut;
            public bool clockRunning;
            public bool shotClockRunning;
            public bool shotClockDisabled;
        }
        struct scoreSet
        {
            public string name;
            public int score;
            public int fouls;
            public int tol;
            public bool poss;
        }
        struct gameState
        {
            public gameSettings game;
            public scoreSet away;
            public scoreSet home;
        }
        static void Main(string[] args)
        {
            String port = "COM4";
            int baud = 19200;
            int databits = 8;
            Parity parity = (Parity)Enum.Parse(typeof(Parity), "None", true);
            StopBits stop = (StopBits)Enum.Parse(typeof(StopBits), "One", true);

            int tcport = 8888;
            String ip = "127.0.0.1";

            UdpClient client;
            gameState gs;
            SerialPort serial;
            IPEndPoint remote;
            Stopwatch watch;

            if (args.Length == 0)
            {
                Console.WriteLine("No arguments detected, continuing with defaults.");
                Console.WriteLine("Argument list is: ip[localhost] port[8888] serial[COM3] baud[19200] databits[8] parity[None] stopbits[1]");

            }
            else
            {
                if (args.Length >= 1) //IP
                    if (args[0].ToLower() == "localhost")
                        ip = "127.0.0.1";
                    else
                        ip = args[0];
                if (args.Length >= 2) //Port
                    try { tcport = int.Parse(args[1]); } catch (Exception ex) { Console.WriteLine("Unrecognized Port: \"{0}\". Port must be a number.", args[1]); }
                if (args.Length >= 3) //Serial
                    port = args[2];
                if (args.Length >= 4) //Baud
                    try { baud = int.Parse(args[3]); } catch (Exception ex) { Console.WriteLine("Unrecognized baud rate: \"{0}\". Baud must be a number.", args[3]); }
                if (args.Length >= 5) //DataBits
                    try { databits = int.Parse(args[4]); } catch (Exception ex) { Console.WriteLine("Unrecognized Databit count: \"{0}\". Databits must be a number.", args[4]); }
                if (args.Length >= 6) //Parity
                    try { parity = (Parity)Enum.Parse(typeof(Parity), args[5], true); } catch (Exception ex) { Console.WriteLine("Unrecognized baud rate: \"{0}\". Parity must be between 0 and 4.", args[5]); }
                if (args.Length >= 7) //Stop
                    try { stop = (StopBits)Enum.Parse(typeof(StopBits), args[6], true); } catch (Exception ex) { Console.WriteLine("Unrecognized StopBit: \"{0}\". StopBit must be 0, 1, 1.5 or 2.", args[6]); }
            }

            try
            {
                client = new UdpClient(ip, tcport);
                remote = new IPEndPoint(IPAddress.Parse(ip), tcport);
                watch = new Stopwatch();

                Byte[] sendBytes = Encoding.ASCII.GetBytes("handshake");
                client.Send(sendBytes, sendBytes.Length);
                Console.WriteLine("Connected...");

                serial = new SerialPort(port, baud, parity, databits, stop);
                serial.Open();
                watch.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }
            long timePass = 20;
            int timeWait = 200;

            do {
                Byte[] received = client.Receive(ref remote);
                string data = Encoding.UTF8.GetString(received);
                try
                {
                    gs = JsonConvert.DeserializeObject<gameState>(data);
                    drawGameState(gs);
                    timePass = watch.ElapsedMilliseconds;
                    Console.WriteLine("Time passed: {0}", timePass);
                    if (timePass > timeWait) {
                        sendSerial(ref serial, gs);
                        watch.Reset();
                        watch.Start();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            } while (true) ;

            watch.Stop();
            Console.WriteLine("Closing.");
            Console.ReadLine();

            Console.WriteLine("Hellow World");
            Console.ReadLine();
        }
        //Draws the current game state to the console.
        static private void drawGameState(gameState gs)
        {
            Console.CursorLeft = 0;
            Console.CursorTop = 4;

            Console.WriteLine("Home: {" +
                "\n  Name: " + gs.home.name +
                "\n  Score: " + gs.home.score.ToString("00") +
                "\n  Fouls: " + gs.home.fouls.ToString() +
                "\n  TOL: " + gs.home.tol.ToString() +
                "\n  Poss? " + gs.home.poss.ToString() +
                "\n}");

            Console.WriteLine("Away: {" +
                "\n  Name: " + gs.away.name +
                "\n  Score: " + gs.away.score.ToString("00") +
                "\n  Fouls: " + gs.away.fouls.ToString() +
                "\n  TOL: " + gs.away.tol.ToString() +
                "\n  Poss? " + gs.home.poss.ToString() +
            "\n}");

            Console.WriteLine("Game: {" +
                "\n  Time: " + gs.game.clockLeft.ToString("00") + gs.game.clockMiddle + gs.game.clockRight.ToString("00") +
                "\n  Period: " + gs.game.period.ToString() +
                "\n  Shotclock: " + gs.game.shotClock.ToString() +
                "\n  TimeOut? " + gs.game.timeOut.ToString() +
            "\n}");

            return;
        }

        static private void sendSerial(ref SerialPort serial, gameState gs)
        {
            if (serial == null || !serial.IsOpen) return;
            int bonus1 = 5;
            int bonus2 = 7;

            string output = "";
            output = "03";
            output += Math.Abs(gs.game.clockLeft).ToString("00");
            output += Math.Abs(gs.game.clockRight).ToString("00");
            output += (Math.Abs(gs.home.fouls) > bonus2) ? "B" : " ";
            output += (Math.Abs(gs.away.fouls) > bonus2) ? "B" : " ";
            output += Math.Abs(gs.game.shotClock).ToString("00");
            output += Math.Abs(gs.home.score).ToString("000");
            output += Math.Abs(gs.away.score).ToString("000");
            output += (gs.home.poss) ? "<" : " ";
            output += (gs.away.poss) ? ">" : " ";
            output += Math.Abs(gs.home.tol).ToString("0");
            output += Math.Abs(gs.away.tol).ToString("0");
            output += (gs.home.fouls > bonus1) ? "B" : " ";
            output += (gs.away.fouls > bonus1) ? "B" : " ";
            output += Math.Abs(gs.home.fouls).ToString("00");
            output += Math.Abs(gs.away.fouls).ToString("00");
            output += "00"; // Math.Abs(foulPlayer).ToString("00");
            output += "0";  // Math.Abs(foulCount).ToString("0");
            output += Math.Abs(gs.game.period).ToString("0");
            output += " ";//(Buzzer) ? "H" : " ";
                          //if (Home.Name == null)
                          //output += "Home".PadRight(8, ' ');
                          //else
            output += gs.home.name.PadRight(8, ' ').Substring(0, 8).PadRight(8, ' ');
            //if (Away.Name == null)
            //output += "Away".PadRight(8, ' ');
            //else
            output += gs.away.name.PadRight(8, ' ').Substring(0, 8).PadRight(8, ' ');

            //Player Stats
            output += " 00 0 00 0";//"_**_*_**_*";//Home 1&2
            output += "   0 0 0 0";//"___*_*_*_*";//Timeouts
            output += " 00 0 00 0 00 0 ";//"_**_*_**_*_**_*_";//Home 4-5 + homebottomcenter
            output += " 00 0 00 0 00 0"; //"_**_*_**_*_**_*";//Away 1-3

            //Camera control
            output += new string(' ', 18);

            //Clock
            if (gs.game.clockRunning)
            {
                output += "R";
                output += gs.game.clockMiddle;
                output += gs.game.shotClockRunning ? "R" : "S";
            }
            else
            {
                output += "S";
                output += gs.game.clockMiddle;
                output += "S";
            }

            output += "  ";
            output += " 00 0 00 0 ";// "_**_*_**_*_"; //Away 4 & 5 + guestbottomcenter

            output += new string(' ', 6); //Scorebot info
            output += new string(' ', 12); //Time & Date

            if (serial != null && serial.IsOpen)
                serial.Write(String.Format("{0}\r", output));
            //serial.Write(String.Format("{0} \"{1}\"\r\n",output.Length, output));
        }
    }
}

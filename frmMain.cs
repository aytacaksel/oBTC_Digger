using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Timers;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Runtime;
using System.Security.Permissions;
using OpenCL.Net;

namespace oBTC_Digger
{

    public partial class frmMain : Form
    {
        string softver = "V1.0";

        private static Digger CDigger;
        private static Queue<Job> IncomingJobs = new Queue<Job>();
        private static Ztratum ztratum;
        private static BackgroundWorker worker;
        public int SharesSubmitted = 0;
        public int SharesAccepted = 0;
        public static string Server = "";
        public static int Port = 0;
        public static string Username = "";
        public static string Password = "";
        public static int threadCount = 0;

        public int cpuMode = 0;

        private static System.Timers.Timer KeepaliveTimer;

        string settingsFile = Application.StartupPath + "\\Settings.dat";

        public long[] totalHashFoundList;
        public int totalShareSubmited = 0;
        public int totalShareAccepted = 0;
        public int totalShareRejected = 0;
        public DateTime workStartTime;
        public bool statsReset = false;

        public List<string> submitList = new List<string>();
        public List<string> submitListThread = new List<string>();

        public string lastJob = "";

        public bool[] runList;

        double exp32 = 4294967296;


        public frmMain()
        {

            uint mmm = 65536 * 36 * 2;
            for (int i = 0; i < 32; ++i)
            {
                int xxx = i / 4;
            }

            uint zzz = 22568845;
            byte[] zzzb = BitConverter.GetBytes(zzz);

            uint xxd = (uint)(zzzb[0] >> 4);
            uint b111 = (zzz >> (8 * 0)) & 0xff;



            ushort x = 518;
            ushort y = 2410;

            int z = (x | y) ^ (x & y);




            //if (IsAdministrator())
            //{
            //    Process.GetCurrentProcess().Kill();
            //    return;
            //}


            InitializeComponent();

            Text += " " + softver;

            cbPriority.SelectedIndex = 1;
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            nThreadCount.Minimum = 1;
            nThreadCount.Maximum = System.Environment.ProcessorCount;
            nThreadCount.Value = System.Environment.ProcessorCount;

            nThreadCount.Value = 1;

            LoadFromFile();
        }

        public static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public void WriteLog(string log)
        {
            DateTime dtNow = DateTime.Now;

            string timeStamp = dtNow.Hour.ToString("00") + ":" + dtNow.Minute.ToString("00") + ":" + dtNow.Second.ToString("00") + "." + dtNow.Millisecond.ToString("000") + "> ";

            if (log == "")
            {
                timeStamp = "";
            }

            Action a = () => txtLog.AppendText(timeStamp + log + "\r\n"); txtLog.Invoke(a);
        }

        private void LoadFromFile()
        {
            if (File.Exists(settingsFile))
            {
                using (StreamReader sr = new StreamReader(settingsFile, Encoding.Default))
                {
                    string s1 = sr.ReadLine().Trim();
                    string s2 = sr.ReadLine().Trim();
                    string s3 = sr.ReadLine().Trim();
                    string s4 = sr.ReadLine().Trim();

                    Server = s1;
                    Port = Convert.ToInt32(s2);
                    Username = s3;
                    Password = s4;
                }

                txtPoolUrl.Text = Server;
                nPoolPort.Value = Port;
                txtUser.Text = Username;
                txtPassword.Text = Password;
            }
        }

        private void SaveToFile()
        {
            if (File.Exists(settingsFile))
            {
                File.Delete(settingsFile);
            }

            using (StreamWriter sw = new StreamWriter(settingsFile, false, Encoding.Default))
            {
                sw.WriteLine(txtPoolUrl.Text.Trim());
                sw.WriteLine(nPoolPort.Value.ToString());
                sw.WriteLine(txtUser.Text.Trim());
                sw.WriteLine(txtPassword.Text.Trim());
            }
        }

        private void txtPoolUrl_Leave(object sender, EventArgs e)
        {
            string[] serverport = txtPoolUrl.Text.Replace("s" + "t" + "ratum+", "").Replace("http://", "").Replace("tcp://", "").Trim().Split(':');
            txtPoolUrl.Text = serverport[0].Trim();

            if (serverport.Length > 1)
            {
                int port = 0;
                try { port = Convert.ToInt32(serverport[1]); } catch { }

                if ((port > 0) && (port < 65535))
                {
                    nPoolPort.Value = port;
                }
            }
            SaveToFile();
        }

        private void nPoolPort_Leave(object sender, EventArgs e)
        {
            SaveToFile();
        }

        private void txtUser_Leave(object sender, EventArgs e)
        {
            SaveToFile();
        }

        private void txtPassword_Leave(object sender, EventArgs e)
        {
            SaveToFile();
        }

        private void nThreadCount_Leave(object sender, EventArgs e)
        {
            SaveToFile();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = false;
            gSettings.Enabled = false;

            statsReset = true;

            GV.StopMining = false;
            GV.ResetMining = false;

            Server = txtPoolUrl.Text.Trim();
            Port = (int)nPoolPort.Value;
            Username = txtUser.Text.Trim();
            Password = txtPassword.Text.Trim();
            threadCount = (int)nThreadCount.Value;

            GV.oldgpumatrix = new List<uint[]>();
            for (int i = 0; i < threadCount; i++)
            {
                GV.oldgpumatrix.Add(new uint[64 * 64]);
            }

            txtPoolUrl.Text = Server;

            SharesSubmitted = 0;
            SharesAccepted = 0;

            BackgroundWorker bwMain = new BackgroundWorker();
            bwMain.DoWork += BwMain_DoWork;
            bwMain.RunWorkerAsync();

            while (bwMain.IsBusy)
            {
                Application.DoEvents();
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (KeepaliveTimer != null)
            {
                if (KeepaliveTimer.Enabled)
                {
                    KeepaliveTimer.Stop();
                }
            }

            GV.StopMining = true;

            if (ztratum != null)
            {
                ztratum.Close();
            }

            CDigger.done = true;

            btnStop.Enabled = false;

        }

        private void BwMain_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (GV.Bench)
                {
                    WriteLog("Benchmark starting..");
                    WriteLog("");
                }
                else
                {
                    if (Server == "")
                    {
                        WriteLog("Missing server URL. URL should be in the format of rtm.suprnova.cc");
                        throw new Exception();
                    }
                    else if (Port == 0)
                    {
                        WriteLog("Missing server port.");
                        return;
                    }
                    else if (Username == "")
                    {
                        WriteLog("Missing username");
                        throw new Exception();
                    }
                    else if (Password == "")
                    {
                        WriteLog("Missing password");
                        throw new Exception();
                    }

                    WriteLog("Connecting to '" + Server + "' on port '" + Port + "' with username '" + Username + "' and password '" + Password + "'");
                    WriteLog("");

                    ztratum = new Ztratum(this);
                    KeepaliveTimer = new System.Timers.Timer(45000);
                    KeepaliveTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
                    KeepaliveTimer.Start();

                    ztratum.GotResponse += ztratum_GotResponse;
                    ztratum.GotSetDifficulty += ztratum_GotSetDifficulty;
                    ztratum.GotNotify += ztratum_GotNotify;

                    ztratum.ConnectToServer(Server, Port, Username, Password);
                }


                CDigger = new Digger(threadCount);
                StartCDigger();

                Action a = () => btnStop.Enabled = true; btnStop.Invoke(a);

            }
            catch
            {
                Action a = () => btnStart.Enabled = true; btnStart.Invoke(a);
                a = () => gSettings.Enabled = true; gSettings.Invoke(a);
            }

        }

        private void StartCDigger()
        {
            Job ThisJob = null;

            if (!GV.Bench)
            {
                while (IncomingJobs.Count == 0)
                    Thread.Sleep(500);


                ThisJob = IncomingJobs.Dequeue();

                if (ThisJob.CleanJobs)
                    ztratum.ExtraNonce2 = 0;

                ztratum.ExtraNonce2++;

                string MerkleRoot = Utilities.GenerateMerkleRoot(ThisJob.Coinb1, ThisJob.Coinb2, ztratum.ExtraNonce1, LittleEndian(ztratum.ExtraNonce2), ThisJob.MerkleNumbers);

                uint len = uint.Parse(ThisJob.NetworkDifficulty.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);

                double diff = Convert.ToDouble(GV.CurrentDifficulty);
                GV.CurrentTarget = diff_to_hash(GV.CurrentDifficulty);

                ThisJob.Target = GV.CurrentTarget[0].ToString() + GV.CurrentTarget[1].ToString() + GV.CurrentTarget[2].ToString() + GV.CurrentTarget[3].ToString();
                ThisJob.Data = ThisJob.Version + ThisJob.PreviousHash + MerkleRoot + ThisJob.NetworkTime + ThisJob.NetworkDifficulty;
            }

            worker = new BackgroundWorker();
            worker.DoWork += new DoWorkEventHandler(CDigger.Dig);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(CDiggerCompleted);
            worker.RunWorkerAsync(new object[] { ThisJob, ztratum, this });
        }

        ulong[] diff_to_hash(double diff)
        {

            ulong[] targ = new ulong[4];

            double m = (1d / diff) * exp32;
            targ[1] = targ[0] = 0;
            targ[3] = (ulong)m;
            targ[2] = (ulong)((m - (double)targ[3]) * (exp32 * exp32));


            return targ;
        }


        private void CDiggerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (GV.ResetMining)
            {
                GV.ResetMining = false;
                GV.StopMining = false;
            }

            if (!GV.StopMining)
            {
                StartCDigger();
            }
            else
            {
                Action a = () => btnStart.Enabled = true; btnStart.Invoke(a);
                a = () => gSettings.Enabled = true; gSettings.Invoke(a);
            }
        }

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            if (GV.StopMining)
            {
                return;
            }

            if (!GV.Bench)
            {
                WriteLog("Keepalive");
                ztratum.SendAUTHORIZE();
            }
        }

        private string LittleEndian(uint number)
        {
            byte[] bytes = BitConverter.GetBytes(number);
            string retval = "";
            foreach (byte b in bytes)
                retval += b.ToString("X2");
            return retval;
        }

        private void ztratum_GotResponse(object sender, ZtratumEventArgs e)
        {
            ZtratumResponse Response = (ZtratumResponse)e.MiningEventArg;

            WriteLog("Got Response to " + (string)sender);

            switch ((string)sender)
            {
                case "mining.authorize":
                    if ((bool)Response.result)
                        WriteLog("Worker authorized");
                    else
                    {
                        WriteLog("Worker rejected");
                        return;
                    }
                    break;

                case "mining.subscribe":
                    ztratum.ExtraNonce1 = (string)((object[])Response.result)[1];
                    WriteLog("Subscribed. ExtraNonce1 set to " + ztratum.ExtraNonce1);
                    break;

                case "mining.submit":
                    if (Response.result != null && (bool)Response.result)
                    {
                        SharesAccepted++;
                        totalShareAccepted++;
                        WriteLog("Share accepted (" + SharesAccepted + " of " + SharesSubmitted + ")");
                    }
                    else
                    {
                        totalShareRejected++;
                        WriteLog("Share rejected. " + Response.error[1]);

                        if (Response.error[1].ToString().Contains("not found"))
                        {
                            CDigger.done = true;
                        }

                        if (Response.error[1].ToString().Contains("duplicate"))
                        {

                        }
                    }

                    break;
                default:
                    break;
            }
        }

        private void ztratum_GotSetDifficulty(object sender, ZtratumEventArgs e)
        {
            ZtratumCommand Command = (ZtratumCommand)e.MiningEventArg;
            GV.CurrentDifficulty = Convert.ToDouble(Command.parameters[0]);
            double diff = Convert.ToDouble(GV.CurrentDifficulty);
            GV.CurrentTarget = diff_to_hash(GV.CurrentDifficulty);


            WriteLog("Got Set_Difficulty " + GV.CurrentDifficulty);
        }

        private void ztratum_GotNotify(object sender, ZtratumEventArgs e)
        {
            Job ThisJob = new Job();
            ZtratumCommand Command = (ZtratumCommand)e.MiningEventArg;
            ThisJob.JobID = (string)Command.parameters[0];

            lastJob = ThisJob.JobID;


            ThisJob.PreviousHash = (string)Command.parameters[1];
            ThisJob.Coinb1 = (string)Command.parameters[2];
            ThisJob.Coinb2 = (string)Command.parameters[3];
            Array a = (Array)Command.parameters[4];
            ThisJob.Version = (string)Command.parameters[5];
            ThisJob.NetworkDifficulty = (string)Command.parameters[6];
            ThisJob.NetworkTime = (string)Command.parameters[7];
            ThisJob.CleanJobs = (bool)Command.parameters[8];

            ThisJob.MerkleNumbers = new string[a.Length];

            int i = 0;
            foreach (string s in a)
                ThisJob.MerkleNumbers[i++] = s;

            if (ThisJob.CleanJobs)
            {
                WriteLog("Detected a new block. Stopping old threads.");

                IncomingJobs.Clear();
                CDigger.done = true;

                //submitList = new List<string>();
            }
            else
            {
                WriteLog("Detected a new job. Stopping old threads.");
                WriteLog("Job: " + ThisJob.JobID);

                IncomingJobs.Clear();
                CDigger.done = true;
            }

            IncomingJobs.Enqueue(ThisJob);
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            Process.GetCurrentProcess().Kill();
        }

        private void chkBench_CheckedChanged(object sender, EventArgs e)
        {
            GV.Bench = chkBench.Checked;
        }

        private void cbPriority_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbPriority.SelectedIndex == 0)
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
            }

            if (cbPriority.SelectedIndex == 1)
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;
            }

            if (cbPriority.SelectedIndex == 2)
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
            }

            if (cbPriority.SelectedIndex == 3)
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Idle;
            }


        }

        private void txtLog_TextChanged(object sender, EventArgs e)
        {
            if (txtLog.Lines.Length > 25)
            {
                List<string> logLines = txtLog.Lines.ToList();

                while (logLines.Count > 25)
                {
                    logLines.RemoveAt(0);
                }

                txtLog.Lines = logLines.ToArray();

                txtLog.SelectionStart = txtLog.TextLength;
                txtLog.ScrollToCaret();
            }
        }
    }

    public class Submit
    {
        public string JobID = "";
        public string nTime = "";
        public uint Nonce = 0;
        public double Difficulty = 0;

        public Submit(string JobID, string nTime, uint Nonce, double Difficulty)
        {
            this.JobID = JobID;
            this.nTime = nTime;
            this.Nonce = Nonce;
            this.Difficulty = Difficulty;
        }
    }

    public class Job
    {

        public string JobID;
        public string PreviousHash;
        public string Coinb1;
        public string Coinb2;
        public string[] MerkleNumbers;
        public string Version;
        public string NetworkDifficulty;
        public string NetworkTime;
        public bool CleanJobs;


        public string Target;
        public string Data;


        public uint Answer;
    }

    public class Work
    {
        public Work(byte[] data)
        {
            Data = data;
            Current = (byte[])data.Clone();
            _nonceOffset = Data.Length - 4;
            _ticks = DateTime.Now.Ticks;
            _hasher = new SHA256Managed();

        }
        private SHA256Managed _hasher;
        private long _ticks;
        private long _nonceOffset;
        public byte[] Data;
        public byte[] Current;

        internal bool FindShare(ref uint nonce, uint batchSize)
        {
            for (; batchSize > 0; batchSize--)
            {
                BitConverter.GetBytes(nonce).CopyTo(Current, _nonceOffset);
                byte[] doubleHash = Sha256(Sha256(Current));

                //count trailing bytes that are zero
                int zeroBytes = 0;
                for (int i = 31; i >= 28; i--, zeroBytes++)
                    if (doubleHash[i] > 0)
                        break;

                //standard share difficulty matched! (target:ffffffffffffffffffffffffffffffffffffffffffffffffffffffff00000000)
                if (zeroBytes == 4)
                    return true;

                //increase
                if (++nonce == uint.MaxValue)
                    nonce = 0;
            }
            return false;
        }

        private byte[] Sha256(byte[] input)
        {
            byte[] crypto = _hasher.ComputeHash(input, 0, input.Length);
            return crypto;
        }

        public byte[] Hash
        {
            get { return Sha256(Sha256(Current)); }
        }

        public long Age
        {
            get { return DateTime.Now.Ticks - _ticks; }
        }
    }
}

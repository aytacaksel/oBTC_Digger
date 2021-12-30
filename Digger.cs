using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using OpenCL.Net;
using SHA3.Net;

namespace oBTC_Digger
{

    class Digger
    {
        frmMain main;

        public volatile bool done = false;

        Thread[] threads;
        ulong[] hashCountList;
        DateTime[] hashStartList;

        public Digger(int threadCount)
        {
            threads = new Thread[threadCount];
            hashCountList = new ulong[threadCount];
            hashStartList = new DateTime[threadCount];
        }

        List<double> hashStatList = new List<double>();

        bool stopQ = false;

        public void Dig(object sender, DoWorkEventArgs e)
        {
            Job ThisJob = (Job)((object[])e.Argument)[0];
            Ztratum ztratum = (Ztratum)((object[])e.Argument)[1];
            main = (frmMain)((object[])e.Argument)[2];

            main.WriteLog("Starting " + threads.Length + " threads for new job...");

            byte[] databyte = new byte[76];
            ulong targetbyte = 0;

            if (GV.Bench)
            {
                databyte = Utilities.ReverseByteArrayByFours(Utilities.HexStringToByteArray("200000006590fb54b49bab4076ac98b4c3cc502bec9d1789d2f8e28000007b5600000000aeed77ddea695631eb177a22a1df5cd3b3acd4fcea6b715c82f81ce4fb8646bf61a37ae31b00984d"));
                string datatest = "00 00 00 32 105 38 75 42 126 19 72 82 60 109 222 243 63 189 78 248 91 178 78 15 137 94 221 157 49 00 00 00 00 00 00 00 19 99 237 241 60 04 226 168 27 166 135 48 192 92 65 109 210 120 33 135 224 147 244 172 164 177 102 189 141 71 113 80 211 57 195 97 112 143 39 26 32 01 190 47";
                string[] datatestarr = datatest.Split(' ');
                GV.CurrentTarget = new ulong[4];
                GV.CurrentTarget[3] = 429496729600;

                databyte = new byte[80];
                for (int i = 0; i < databyte.Length; i++)
                {
                    databyte[i] = Convert.ToByte(datatestarr[i]);
                }


                //536985135
                //429496729600
            }
            else
            {
                databyte = Utilities.ReverseByteArrayByFours(Utilities.HexStringToByteArray(ThisJob.Data));
                //targetbyte = Convert.ToUInt64(ThisJob.Target);
            }

            if (main.statsReset)
            {
                main.statsReset = false;

                main.totalHashFoundList = new long[threads.Length];
                main.totalShareSubmited = 0;
                main.totalShareAccepted = 0;
                main.totalShareRejected = 0;
                main.workStartTime = DateTime.Now;
            }

            done = false;

            hashCountList = new ulong[threads.Length];
            hashStartList = new DateTime[threads.Length];
            hashStatList = new List<double>();

            if (main.submitList.Count == 0)
            {
                GV.lastNonceList = new uint[threads.Length];
            }



            stopQ = false;
            Thread thz = new Thread(new ParameterizedThreadStart(submitQ));
            thz.IsBackground = false;
            thz.Priority = ThreadPriority.BelowNormal;
            thz.Start(ztratum);

            uint workSize = (uint)(uint.MaxValue / threads.Length);

            for (int i = 0; i < threads.Length; i++)
            {
                ArrayList args = new ArrayList();
                args.Add(ThisJob);
                args.Add(ztratum);
                args.Add(i);
                args.Add(databyte);
                args.Add(targetbyte);
                args.Add((uint)((i * workSize) + 1));
                args.Add((uint)((i + 1) * workSize));

                threads[i] = new Thread(new ParameterizedThreadStart(doGR));

                threads[i].IsBackground = false;
                threads[i].Priority = ThreadPriority.BelowNormal;
                threads[i].Start(args);

                
            }


            for (int i = 0; i < threads.Length; i++)
            {
                threads[i].Join();
            }

            stopQ = true;

            Thread.Sleep(500);

            while (thz.ThreadState == System.Threading.ThreadState.Running)
            {
                Thread.Sleep(10);
            }

            e.Result = null;

            main.WriteLog("Current Hashrate: " + (hashStatList.Sum() / 1000000d).ToString("0.00") + " Hash/s");
            UpdateStats((hashStatList.Sum() / 1000000d).ToString("0.00"));

            // ztratum.SendAUTHORIZE();
        }

        static byte[] be32enc(uint x)
        {
            byte[] p = new byte[4];
            p[3] = (byte)(x & 0xff);
            p[2] = (byte)((x >> 8) & 0xff);
            p[1] = (byte)((x >> 16) & 0xff);
            p[0] = (byte)((x >> 24) & 0xff);
            return p;
        }

        public void submitQ(object o)
        {
            Ztratum ztratum = (Ztratum)o;

            while (true)
            {
                if (stopQ)
                {
                    return;
                }
                try
                {
                    int submitQListCount = GV.submitQList.Count;

                    for (int i = 0; i < submitQListCount; i++)
                    {
                        if (!GV.submitQListX.Contains(GV.submitQList[i].Nonce))
                        {


                            GV.submitQListX.Add(GV.submitQList[i].Nonce);
                            GV.submitQListX.Sort();
                            ztratum.SendSUBMIT(GV.submitQList[i].JobID, GV.submitQList[i].nTime, GV.submitQList[i].Nonce, GV.submitQList[i].Difficulty);
                        }

                    }
                }
                catch { }

                Thread.Sleep(5);
            }

        }

        volatile bool alldone = true;

        /*private string getname(int a)
        {
            return "a" + 1
        }*/

        public void doGR(object o)
        {
            try
            {

                SHA3.Net.Sha3 sha = SHA3.Net.Sha3.Sha3256();

                ArrayList args = (ArrayList)o;

                Job ThisJob = (Job)args[0];
                Ztratum ztratum = (Ztratum)args[1];
                int threadId = (int)args[2];
                byte[] Tempdata = (byte[])args[3];
                ulong Target = (ulong)args[4];
                uint Nonce = (uint)args[5];
                uint MaxNonce = (uint)args[6];

                if (GV.Bench)
                    Target = 0x00ff;

                ulong Hashcount = 0;

                byte[] input = new byte[80];

                Array.Copy(Tempdata, input, 76);

                byte[] n = be32enc(Nonce);
                Array.Copy(n, 0, input, 76, 4);

                byte[] _input = sha.ComputeHash(input, 4, 32);

                xoshiro_state state;
                state.s = new ulong[4];

                int[][] matrix = new int[64][];

                for (int i = 0; i < 4; ++i)
                {
                    int j = i * 8;
                    state.s[i] = BitConverter.ToUInt64(new byte[] { _input[j + 0], _input[j + 1], _input[j + 2], _input[j + 3], _input[j + 4], _input[j + 5], _input[j + 6], _input[j + 7] }, 0);
                }

                generate_matrix(ref matrix, state);

                byte[] output = new byte[32];

                uint[] gpumatrix = new uint[64 * 64];

                List<string> mulList = new List<string>();

                int gpumatrixindex = 0;
                for (int i = 0; i < 64; i++)
                {
                    int j = 0;
                    for (int ii = 0; ii < 64; ii++)
                    {
                        uint tmp = (uint)matrix[i][j++];
                        gpumatrix[gpumatrixindex++] = tmp;

                        /*if (!mulList.Contains("a_" + tmp + "_" + ii))
                            mulList.Add("a_" + tmp + "_" + ii);*/
                    }
                }

                if (GV.buildProgram[threadId])
                {
                    if (!gpumatrix.ToList().SequenceEqual(GV.oldgpumatrix[threadId]))
                    {
                        GV.buildProgram[threadId] = false;
                        Cl.ReleaseCommandQueue(GV.commandQueue[threadId]);
                    }
                }

                if (!GV.buildProgram[threadId])
                {
                    for (int i = 0; i < gpumatrix.Length; i++)
                    {
                        GV.oldgpumatrix[threadId][i] = gpumatrix[i];
                    }

                    GV.errorCode[threadId] = new ErrorCode();

                    Platform[] platforms = Cl.GetPlatformIDs(out GV.errorCode[threadId]);

                    GV.platform[threadId] = platforms[0];

                    Device[] devices = Cl.GetDeviceIDs(GV.platform[threadId], DeviceType.Gpu, out GV.errorCode[threadId]);

                    GV.device[threadId] = devices[0];

                    GV.context[threadId] = Cl.CreateContext(null, 1, new Device[] { GV.device[threadId] }, null, IntPtr.Zero, out GV.errorCode[threadId]);


                    GV.commandQueue[threadId] = Cl.CreateCommandQueue(GV.context[threadId], GV.device[threadId], CommandQueueProperties.None, out GV.errorCode[threadId]);

                    string kernel = File.ReadAllText(Application.StartupPath + "\\heavyhash.cl");
                    kernel = kernel.Replace("WORKSIZE", "256");

                    /*string kernelconst = "\r\n__constant uint matrix[4096] = {\r\n";

                    for (int i = 0; i < 4096; i++)
                    {
                        kernelconst += gpumatrix[i].ToString() + ",\r\n";
                    }
                    kernelconst = kernelconst.Substring(0, kernelconst.Length - 3);
                    kernelconst += "};\r\n\r\n";
                    kernel =  kernelconst + kernel;
                    */

                    /*string kernelconst = "\r\n__constant uint4 matrix[1024] = {\r\n";

                    int gindex = 0;
                    for (int i = 0; i < 1024; i++)
                    {
                        kernelconst += "(uint4)(" + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "),\r\n";
                    }
                    kernelconst = kernelconst.Substring(0, kernelconst.Length - 3);
                    kernelconst += "};\r\n\r\n";
                    kernel = kernelconst + kernel;*/

                    /*string kernelconst = "\r\n__constant uint2 matrix[2048] = {\r\n";

                    int gindex = 0;
                    for (int i = 0; i < 2048; i++)
                    {
                        kernelconst += "(uint2)(" + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "),\r\n";
                    }
                    kernelconst = kernelconst.Substring(0, kernelconst.Length - 3);
                    kernelconst += "};\r\n\r\n";
                    kernel = kernelconst + kernel;*/

                    /*string kernelconst = "\r\n__constant uint8 matrix[512] = {\r\n";

                    int gindex = 0;
                    for (int i = 0; i < 512; i++)
                    {
                        kernelconst += "(uint8)(" + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "),\r\n";
                    }
                    kernelconst = kernelconst.Substring(0, kernelconst.Length - 3);
                    kernelconst += "};\r\n\r\n";
                    kernel = kernelconst + kernel;*/


                    /*string kernelconst = "\r\n__constant uint16 matrix[256] = {\r\n";

                    int gindex = 0;
                    for (int i = 0; i < 256; i++)
                    {
                        kernelconst += "(uint16)(" + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "," + gpumatrix[gindex++] + "),\r\n";
                    }
                    kernelconst = kernelconst.Substring(0, kernelconst.Length - 3);
                    kernelconst += "};\r\n\r\n";
                    kernel = kernelconst + kernel;*/

                    //__attribute__((aligned(256)))

                    ////for (int i = 0; i < 32; i++)
                    ////{
                    ////    //kernelmatrix += "sum1_1 = 0;\r\n";
                    ////    //kernelmatrix += "sum1_2 = 0;\r\n";

                    ////    for (int j = 0; j < 64; j++)
                    ////    {
                    ////        kernelmatrix += "sum1_1[" + i + "] = mad24(" + gpumatrix[(i * 2 + 0) * 64 + j].ToString() + ", vector[" + j.ToString() + "],sum1_1[" + i + "]);\r\n";

                    ////        //if (gpumatrix[(i * 2 + 0) * 64 + j] != 0)
                    ////        //{
                    ////        //    if (gpumatrix[(i * 2 + 0) * 64 + j] != 1)
                    ////        //    {
                    ////        //        kernelmatrix += "sum1_1 = mad24(" + gpumatrix[(i * 2 + 0) * 64 + j].ToString() + ", vector[" + j.ToString() + "],sum1_1);\r\n";
                    ////        //    }
                    ////        //    else
                    ////        //    {
                    ////        //        kernelmatrix += "sum1_1 += vector[" + j.ToString() + "];\r\n";
                    ////        //    }
                    ////        //}
                    ////    }

                    ////    for (int j = 0; j < 64; j++)
                    ////    {
                    ////        kernelmatrix += "sum1_2[" + i + "] = mad24(" + gpumatrix[(i * 2 + 1) * 64 + j].ToString() + ", vector[" + j.ToString() + "],sum1_2[" + i + "]);\r\n";

                    ////        /* if (gpumatrix[(i * 2 + 1) * 64 + j] != 0)
                    ////         {
                    ////             if (gpumatrix[(i * 2 + 1) * 64 + j] != 1)
                    ////             {
                    ////                 kernelmatrix += "sum1_2 = mad24(" + gpumatrix[(i * 2 + 1) * 64 + j].ToString() + ", vector[" + j.ToString() + "],sum1_2);\r\n";
                    ////             }
                    ////             else
                    ////             {
                    ////                 kernelmatrix += "sum1_2 += vector[" + j.ToString() + "];\r\n";
                    ////             }
                    ////         }*/
                    ////    }

                    ////    // kernelmatrix += "pdata_uchar[" + (i).ToString() + "] ^= (((sum1_1 >> 10) << 4) | (sum1_2 >> 10));\r\n";
                    ////}



                    string kernelmatrix = "";

                    kernelmatrix += "int sum1_1[32] = {0};\r\n";
                    kernelmatrix += "int sum1_2[32] = {0};\r\n";

                    for (int i = 0; i < 32; i++)
                    {
                        for (int j = 0; j < 64; j++)
                        {
                            kernelmatrix += "sum1_1[" + i + "] = mad24(" + gpumatrix[(i * 2 + 0) * 64 + j].ToString() + ", vector[" + j.ToString() + "],sum1_1[" + i + "]);\r\n";
                        }
                    }

                    for (int i = 0; i < 32; i++)
                    {
                        for (int j = 0; j < 64; j++)
                        {
                            kernelmatrix += "sum1_2[" + i + "] = mad24(" + gpumatrix[(i * 2 + 1) * 64 + j].ToString() + ", vector[" + j.ToString() + "],sum1_2[" + i + "]);\r\n";
                        }
                    }


                    /*for (int i = 0; i < 32; i++)
                    {
                        kernelmatrix += "pdata_uchar[" + (i).ToString() + "] ^= (((sum1_1[" + i + "] >> 10) << 4) | (sum1_2[" + i + "] >> 10));\r\n";
                    }*/

                    kernel = kernel.Replace("CALCMATRIX", kernelmatrix);


                    GV.program[threadId] = Cl.CreateProgramWithSource(GV.context[threadId], 1, new[] { kernel }, null, out GV.errorCode[threadId]);

                    GV.errorCode[threadId] = Cl.BuildProgram(GV.program[threadId], 1, new[] { GV.device[threadId] }, "-cl-std=CL1.2 -O2", null, IntPtr.Zero);

                    if (GV.errorCode[threadId] != ErrorCode.Success)
                    {
                        OpenCL.Net.InfoBuffer logInfoBuffer = Cl.GetProgramBuildInfo(GV.program[threadId], GV.device[threadId], ProgramBuildInfo.Log, out GV.errorCode[threadId]);
                        string xxx = logInfoBuffer.ToString();

                    }

                    GV.search[threadId] = Cl.CreateKernel(GV.program[threadId], "search", out GV.errorCode[threadId]);

                    GV.buildProgram[threadId] = true;
                }


                uint2[] input2 = new uint2[10];

                int inputindex = 0;
                for (int i = 0; i < 10; i++)
                {
                    input2[i] = new uint2(BitConverter.ToUInt32(input, inputindex), BitConverter.ToUInt32(input, inputindex + 4));
                    inputindex += 8;
                }

                uint[] _output = new uint[255];
                ulong targ = GV.CurrentTarget[3];

                Event _event = new Event();

                IMem dp1 = Cl.CreateBuffer(GV.context[threadId], MemFlags.CopyHostPtr | MemFlags.ReadWrite, (IntPtr)(sizeof(uint) * 20), input, out GV.errorCode[threadId]);
                IMem dp2 = Cl.CreateBuffer(GV.context[threadId], MemFlags.CopyHostPtr | MemFlags.ReadWrite, (IntPtr)(sizeof(uint) * _output.Length), _output, out GV.errorCode[threadId]);

                Cl.EnqueueWriteBuffer(GV.commandQueue[threadId], dp1, Bool.True, IntPtr.Zero, (IntPtr)(sizeof(uint) * 20), input2, 0, null, out _event);
                Cl.EnqueueWriteBuffer(GV.commandQueue[threadId], dp2, Bool.True, IntPtr.Zero, (IntPtr)(sizeof(uint) * _output.Length), _output, 0, null, out _event);


                GV.errorCode[threadId] = Cl.SetKernelArg(GV.search[threadId], 0, dp1);
                GV.errorCode[threadId] = Cl.SetKernelArg(GV.search[threadId], 1, dp2);
                GV.errorCode[threadId] = Cl.SetKernelArg(GV.search[threadId], 2, (IntPtr)(sizeof(ulong)), targ);

                DateTime StartTime = DateTime.Now;

                DateTime reportTimeStart = DateTime.Now;

                hashStartList[threadId] = StartTime;

                DateTime dtLastNonce = DateTime.Now;

                try
                {

                    uint gws = 65536 * 36 * 8;

                    while (!done)
                    {
                        if (GV.StopMining)
                        {
                            break;
                        }

                        DateTime dtStart = DateTime.Now;


                        GV.errorCode[threadId] = Cl.EnqueueNDRangeKernel(GV.commandQueue[threadId], GV.search[threadId], 1, new IntPtr[] { (IntPtr)Nonce }, new IntPtr[] { (IntPtr)gws }, new IntPtr[] { (IntPtr)256 }, 0, null, out _event);


                        if (GV.errorCode[threadId] != ErrorCode.Success)
                        {
                            OpenCL.Net.InfoBuffer logInfoBuffer = Cl.GetProgramBuildInfo(GV.program[threadId], GV.device[threadId], ProgramBuildInfo.Log, out GV.errorCode[threadId]);
                            string err = logInfoBuffer.ToString();
                        }

                        Cl.EnqueueBarrier(GV.commandQueue[threadId]);

                        GV.errorCode[threadId] = Cl.EnqueueReadBuffer(GV.commandQueue[threadId], dp2, Bool.True, IntPtr.Zero, (IntPtr)(sizeof(uint) * _output.Length), _output, 0, null, out _event);

                        Nonce += gws;

                        Hashcount += gws;
                        main.totalHashFoundList[threadId] += gws;
                        hashCountList[threadId] = Hashcount;

                        for (int ii = 0; ii < _output.Length; ii++)
                        {
                            if (_output[ii] > 0)
                            {
                                if (!main.submitList.Contains(_output[ii].ToString()))
                                {
                                    if (main.lastJob == ThisJob.JobID)
                                    {
                                        main.submitList.Add(_output[ii].ToString());
                                        main.submitListThread.Add(_output[ii].ToString() + "_" + threadId.ToString() + "_" + ThisJob.JobID.ToString());

                                        GV.submitQList.Add(new Submit(ThisJob.JobID, ThisJob.Data.Substring(68 * 2, 8), _output[ii], GV.CurrentDifficulty));
                                        dtLastNonce = DateTime.Now;
                                    }
                                    else
                                    {
                                        done = true;
                                    }
                                }
                            }
                        }


                        if ((threadId == 0) && (Nonce > 1) && ((DateTime.Now - reportTimeStart).TotalSeconds > 5))
                        {
                            reportTimeStart = DateTime.Now;
                            List<double> hashStatListSub = new List<double>();
                            for (int i = 0; i < hashStartList.Length; i++)
                            {
                                double ElapsedtimeSub = (DateTime.Now - hashStartList[i]).TotalSeconds;
                                hashStatListSub.Add(hashCountList[i] / ElapsedtimeSub);
                            }

                            main.WriteLog("Current Hashrate: " + (hashStatListSub.Sum() / 1000000d).ToString("0.00") + " MH/s");

                            UpdateStats((hashStatList.Sum() / 1000000d).ToString("0.00"));
                        }

                        GV.lastNonceList[threadId] = Nonce;

                        if (Nonce > MaxNonce)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    main.WriteLog(ex.Message);
                    done = true;
                }

                double Elapsedtime = (DateTime.Now - StartTime).TotalSeconds;


                main.WriteLog("Thread" + threadId + " finished - " + (Hashcount / 1000000d).ToString("0.00") + " hashes in " + Elapsedtime.ToString("0.00") + " s. Speed: " + ((Hashcount / Elapsedtime) / 1000000d).ToString("0.00") + " MH/s");

                hashStatList.Add(Hashcount / Elapsedtime);


                Cl.Finish(GV.commandQueue[threadId]);
                Cl.Flush(GV.commandQueue[threadId]);
                _event.Dispose();

            }
            catch (Exception ex)
            {
                main.WriteLog(ex.Message);
                done = true;
            }





        }

        void heavyhash(Sha3 sha, int[][] matrix, byte[] pdata, int pdata_len, ref byte[] output)
        {
            byte[] hash_first = new byte[32];
            byte[] hash_second = new byte[32];
            byte[] hash_xored = new byte[32];

            int[] vector = new int[64];
            uint[] product = new uint[64];



            hash_first = sha.ComputeHash(pdata, 0, pdata.Length);


            for (int i = 0; i < 32; ++i)
            {
                vector[2 * i] = (byte)(hash_first[i] >> 4);
                vector[2 * i + 1] = (byte)(hash_first[i] & 0xF);
            }



            for (int i = 0; i < 64; ++i)
            {
                uint sum = 0;

                for (int j = 0; j < 64; ++j)
                {
                    sum += (uint)(matrix[i][j] * vector[j]);


                }
                product[i] = (sum / 1000);
            }


            for (int i = 0; i < 32; ++i)
            {
                hash_second[i] = (byte)((product[2 * i] << 4) | (product[2 * i + 1]));
            }

            for (int i = 0; i < 32; ++i)
            {
                hash_xored[i] = (byte)(hash_first[i] ^ hash_second[i]);
            }

            output = sha.ComputeHash(hash_xored, 0, 32);


        }


        bool valid_hash(ulong[] h, ulong[] t)
        {
            if (h[3] > t[3]) return false;
            if (h[3] < t[3]) return true;
            if (h[2] > t[2]) return false;
            if (h[2] < t[2]) return true;
            if (h[1] > t[1]) return false;
            if (h[1] < t[1]) return true;
            if (h[0] > t[0]) return false;
            return true;
        }

        bool prodone = false;
        uint[] productmem = new uint[64];




        uint bswap32(uint x)
        {
            return ((x << 24) & 0xff000000) | ((x << 8) & 0x00ff0000) | ((x >> 8) & 0x0000ff00) | ((x >> 24) & 0x000000ff);
        }
        void mm128_bswap32_80(byte[] d, byte[] s)
        {
            int dindex = 0;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;
            Array.Copy(BitConverter.GetBytes(bswap32(BitConverter.ToUInt32(s, dindex))), 0, d, dindex, 4); dindex += 4;




            //((uint32_t*)d)[0] = bswap_32(((uint32_t*)s)[0]);
            //((uint32_t*)d)[1] = bswap_32(((uint32_t*)s)[1]);
            //((uint32_t*)d)[2] = bswap_32(((uint32_t*)s)[2]);
            //((uint32_t*)d)[3] = bswap_32(((uint32_t*)s)[3]);
            //((uint32_t*)d)[4] = bswap_32(((uint32_t*)s)[4]);
            //((uint32_t*)d)[5] = bswap_32(((uint32_t*)s)[5]);
            //((uint32_t*)d)[6] = bswap_32(((uint32_t*)s)[6]);
            //((uint32_t*)d)[7] = bswap_32(((uint32_t*)s)[7]);
            //((uint32_t*)d)[8] = bswap_32(((uint32_t*)s)[8]);
            //((uint32_t*)d)[9] = bswap_32(((uint32_t*)s)[9]);
            //((uint32_t*)d)[10] = bswap_32(((uint32_t*)s)[10]);
            //((uint32_t*)d)[11] = bswap_32(((uint32_t*)s)[11]);
            //((uint32_t*)d)[12] = bswap_32(((uint32_t*)s)[12]);
            //((uint32_t*)d)[13] = bswap_32(((uint32_t*)s)[13]);
            //((uint32_t*)d)[14] = bswap_32(((uint32_t*)s)[14]);
            //((uint32_t*)d)[15] = bswap_32(((uint32_t*)s)[15]);
            //((uint32_t*)d)[16] = bswap_32(((uint32_t*)s)[16]);
            //((uint32_t*)d)[17] = bswap_32(((uint32_t*)s)[17]);
            //((uint32_t*)d)[18] = bswap_32(((uint32_t*)s)[18]);
            //((uint32_t*)d)[19] = bswap_32(((uint32_t*)s)[19]);
        }

        public struct xoshiro_state
        {
            public ulong[] s;
        }

        ulong le64dec(byte[] pp)
        {
            byte[] p = pp;
            return ((ulong)(p[0]) | ((ulong)(p[1]) << 8) | ((ulong)(p[2]) << 16) | ((ulong)(p[3]) << 24)) | ((ulong)(p[4]) << 32) | ((ulong)(p[5]) << 40) | ((ulong)(p[6]) << 48) | ((ulong)(p[7]) << 56);
        }

        ulong rotl64(ulong x, int k)
        {
            return (x << k) | (x >> (64 - k));
        }

        ulong xoshiro_gen(xoshiro_state state)
        {
            ulong result = rotl64(state.s[0] + state.s[3], 23) + state.s[0];

            ulong t = state.s[1] << 17;

            state.s[2] ^= state.s[0];
            state.s[3] ^= state.s[1];
            state.s[1] ^= state.s[2];
            state.s[0] ^= state.s[3];

            state.s[2] ^= t;

            state.s[3] = rotl64(state.s[3], 45);

            return result;
        }

        int compute_rank(int[][] A)
        {
            double EPS = 1e-9;

            double[][] B = new double[64][];

            for (int i = 0; i < 64; ++i)
            {
                B[i] = new double[64];

                for (int j = 0; j < 64; ++j)
                {
                    B[i][j] = A[i][j];
                }
            }

            int rank = 0;
            int[] row_selected = new int[64];

            for (int i = 0; i < 64; ++i)
            {
                int j;
                for (j = 0; j < 64; ++j)
                {
                    if (!Convert.ToBoolean(row_selected[j]) && Math.Abs(B[j][i]) > EPS)
                        break;
                }
                if (j != 64)
                {
                    ++rank;
                    row_selected[j] = 1;
                    for (int p = i + 1; p < 64; ++p)
                        B[j][p] /= B[j][i];
                    for (int k = 0; k < 64; ++k)
                    {
                        if (k != j && Math.Abs(B[k][i]) > EPS)
                        {
                            for (int p = i + 1; p < 64; ++p)
                                B[k][p] -= B[j][p] * B[k][i];
                        }
                    }
                }
            }
            return rank;
        }

        int is_full_rank(int[][] matrix)
        {
            return Convert.ToInt32(compute_rank(matrix) == 64);
        }

        void generate_matrix(ref int[][] matrix, xoshiro_state state)
        {
            matrix = new int[64][];
            do
            {
                for (int i = 0; i < 64; ++i)
                {
                    matrix[i] = new int[64];
                    for (int j = 0; j < 64; j += 16)
                    {
                        ulong value = xoshiro_gen(state);
                        for (int shift = 0; shift < 16; ++shift)
                        {
                            matrix[i][j + shift] = (byte)((value >> (4 * shift)) & 0xF);
                        }
                    }
                }
            } while (!Convert.ToBoolean(is_full_rank(matrix)));
        }



        private void UpdateStats(string currentHashRate)
        {
            Action a;

            double Elapsedtime = (DateTime.Now - main.workStartTime).TotalSeconds;

            double avgHash = (main.totalHashFoundList.ToList().Sum() / 1000000d) / Elapsedtime;

            int totalShare = main.totalShareSubmited;
            int acShare = main.totalShareAccepted;
            int rejShare = main.totalShareRejected;

            try
            {
                a = () => main.lblCurrentHashrate.Text = currentHashRate + " MH/s"; main.lblCurrentHashrate.Invoke(a);
                a = () => main.lblAverageHashrate.Text = avgHash.ToString("0.00") + " MH/s"; main.lblAverageHashrate.Invoke(a);
                a = () => main.lblTotalShares.Text = totalShare.ToString(); main.lblTotalShares.Invoke(a);
                a = () => main.lblAcceptedShares.Text = acShare.ToString(); main.lblAcceptedShares.Invoke(a);
                a = () => main.lblRejectedShares.Text = rejShare.ToString(); main.lblRejectedShares.Invoke(a);
                a = () => main.lblCurrentDifficulty.Text = GV.CurrentDifficulty.ToString("0.00000"); main.lblCurrentDifficulty.Invoke(a);
                a = () => main.lblUptime.Text = Elapsedtime.ToString("0.00") + " s"; main.lblUptime.Invoke(a);
            }
            catch { }
        }

    }

}

//GCHandle[] handles1 = new GCHandle[matrix.Length];
//for (int i = 0; i < matrix.Length; i++)
//{
//    handles1[i] = GCHandle.Alloc(matrix[i], GCHandleType.Pinned);
//}

//IntPtr[] matrix_pointer = new IntPtr[handles1.Length];
//for (int i = 0; i < handles1.Length; i++)
//{
//    matrix_pointer[i] = handles1[i].AddrOfPinnedObject();
//}
//_GhostriderWork(matrix_pointer, _input, output);

//int Devices = AcceleratorDevice.All.Length;

//AcceleratorDevice Device = null;

//foreach (var AcceleratorDevice in AcceleratorDevice.All)
//{
//    if (AcceleratorDevice.Name.Contains("Ellesmere"))
//    {
//        Device = AcceleratorDevice;
//        break;
//    }
//}

//string kernel = File.ReadAllText(Application.StartupPath + "\\hash.cl");

//kernel= kernel.Replace("WORKSIZE","4");
//kernel = kernel.Replace("ACCESSES", "64");
//kernel = kernel.Replace("MAX_OUTPUTS", "10");
//kernel = kernel.Replace("PLATFORM", "1");
//kernel = kernel.Replace("COMPUTE", "0");



//double testt = EasyCL.GetDeviceGFlops_Single(Device, kernel);

//object[] _params = new object[3];
//_params[0] = (ulong)10;
//_params[1] = (ulong)1000000;
//_params[2] = (ulong)0;

//ComputeMethod method = new ComputeMethod(kernel, "search", Device);
//method.Invoke(4, _params);


//ulong ou = BitConverter.ToUInt64(output, 24);
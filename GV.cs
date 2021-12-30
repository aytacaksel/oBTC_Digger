using OpenCL.Net;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;

namespace oBTC_Digger
{
    public static class GV
    {
        public static double CurrentDifficulty;
        public static ulong[] CurrentTarget;
        public static bool StopMining = false;
        public static bool ResetMining = false;

        public static bool Bench = false;

        public static Thread[] threads;
        public static bool[] threadStateList;
        public static uint[] NonceList;
        public static uint[] MaxNonceList;

        public static uint[] lastNonceList;

        public static bool largeMemAccess = false;

        public static uint largePageMinimum = 0;

        public static ErrorCode[] errorCode = new ErrorCode[8];
        public static Platform[] platform = new Platform[8];
        public static Device[] device = new Device[8];
        public static Context[] context = new Context[8];
        public static CommandQueue[] commandQueue = new CommandQueue[8];
        public static Kernel[] search = new Kernel[8];
        public static OpenCL.Net.Program[] program = new OpenCL.Net.Program[8];

        public static List<uint[]> oldgpumatrix = new List<uint[]>();

        public static bool[] buildProgram = new bool[8];

        public static List<Submit> submitQList = new List<Submit>();
        public static List<uint> submitQListX = new List<uint>();





    }

    public class HT
    {
        public int? id;
        public string method;

        public HT(int id, string method)
        {
            this.id = id;
            this.method = method;
        }

        public HT(int? id, string method)
        {
            this.id = id;
            this.method = method;
        }
    }


}

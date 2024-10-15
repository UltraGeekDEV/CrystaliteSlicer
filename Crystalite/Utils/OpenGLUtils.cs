using OpenTK.Graphics.ES30;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Crystalite.Utils
{
    public static class OpenGLUtils
    {
        public const int positionLocation = 0;
        public const int normalLocation = 1;

        private static Queue<Action> syncQueue = new Queue<Action>();
        private static object syncQueueLock = new object();

        public static void QueueAction(Action a)
        {
            lock (syncQueueLock)
            {
                syncQueue.Enqueue(a);
            }
        }

        public static void ExecuteSyncQueue()
        {
            lock (syncQueueLock)
            {
                while (syncQueue.Count > 0)
                {
                    syncQueue.Dequeue().Invoke();
                }
            }
        }

        public static void CheckError(string name)
        {
            ErrorCode err;
            while ((err = GL.GetError()) != ErrorCode.NoError)
                Debug.WriteLine(name + ": " + err);
        }
    }
}

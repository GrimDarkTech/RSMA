using System.Collections;
using System.Collections.Generic;
using System;

namespace RSMALogger
{
    /// <summary>
    /// Universal class for displaying logs to OS terminal or Unity console
    /// </summary>
    public class Logger
    {
        /// <summary>
        /// Backend used for text logging.
        /// </summary>
        public static Backends backend;

        /// <summary>
        /// Logs text
        /// </summary>
        /// <param name="text">Text to log</param>
        public static void Log(string text)
        {
            switch (backend)
            {
                case Backends.None:
                    break;
                case Backends.Unity:
#if UNITY_5_3_OR_NEWER
                        UnityEngine.Debug.Log(text);
#endif
                    break;
                case Backends.DotNet:
                    System.Console.ForegroundColor= ConsoleColor.Yellow;
                    System.Console.WriteLine(text);
                    System.Console.ForegroundColor = ConsoleColor.White;
                    break;

            }
        }
    }

    public enum Backends
    {
        None,
        Unity,
        DotNet
    }
}




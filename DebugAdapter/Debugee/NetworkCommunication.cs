﻿using System;
using System.Net.Sockets;
using System.Text;

namespace VSCodeDebug
{
    class NetworkCommunication : IDebugeeSender
    {
        IDebugeeListener debugeeListener;
        NetworkStream networkStream;
        ByteBuffer recvBuffer = new ByteBuffer();

        public NetworkCommunication(IDebugeeListener debugeeListener, NetworkStream networkStream)
        {
            this.debugeeListener = debugeeListener;
            this.networkStream = networkStream;
        }

        public void StartThread()
        {
            new System.Threading.Thread(() => SocketStreamLoop()).Start();
        }

        void SocketStreamLoop()
        {
            try
            {
                while (true)
                {
                    var buffer = new byte[10000];
                    var read = networkStream.Read(buffer, 0, buffer.Length);

                    if (read == 0) { break; } // end of stream
                    if (read > 0)
                    {
                        recvBuffer.Append(buffer, read);
                        while (ProcessData()) { }
                    }
                }
            }
            catch (Exception /*e*/)
            {
                //Program.MessageBox(IntPtr.Zero, e.ToString(), "LuaDebug", 0);
            }

            lock (debugeeListener)
            {
                debugeeListener.DebugeeHasGone();
            }
        }

        bool ProcessData()
        {
            string s = recvBuffer.GetString(System.Text.Encoding.UTF8);
            int headerEnd = s.IndexOf('\n');
            if (headerEnd < 0) { return false; }

            string header = s.Substring(0, headerEnd);
            if (header[0] != '#') { throw new Exception("헤더 이상함:" + header); }
            var bodySize = int.Parse(header.Substring(1));

            // 헤더는 모두 0~127 아스키 문자로만 이루어지기 때문에
            // 문자열 길이로 계산했을 때와 바이트 개수로 계산했을 때의 결과가 같다.
            if (recvBuffer.Length < headerEnd + 1 + bodySize) { return false; }

            recvBuffer.RemoveFirst(headerEnd + 1);
            byte[] bodyBytes = recvBuffer.RemoveFirst(bodySize);

            string body = Encoding.UTF8.GetString(bodyBytes);
            //MessageBox.OK(body);

            lock (debugeeListener)
            {
                debugeeListener.FromDebuggee(bodyBytes);
            }
            return true;
        }

        void IDebugeeSender.Send(string reqText)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(reqText);
            string header = '#' + bodyBytes.Length.ToString() + "\n";
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            networkStream.Write(headerBytes, 0, headerBytes.Length);
            networkStream.Write(bodyBytes, 0, bodyBytes.Length);
        }
    }
}
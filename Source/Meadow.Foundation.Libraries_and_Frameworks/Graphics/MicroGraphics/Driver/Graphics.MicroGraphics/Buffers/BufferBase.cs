﻿using System;

namespace Meadow.Foundation.Graphics.Buffers
{
    public abstract class BufferBase : IPixelBuffer
    {
        public int Width { get; protected set; }
        public int Height { get; protected set; }

        public byte[] Buffer { get; protected set; }

        public abstract int ByteCount { get; }

        public abstract ColorType ColorMode { get; }

        public BufferBase()
        {
        }

        public BufferBase(int width, int height)
        {
            Width = width;
            Height = height;

            Buffer = new byte[ByteCount];
        }

        public BufferBase(int width, int height, byte[] buffer)
        {
            Width = width;
            Height = height;

            if(buffer.Length != ByteCount)
            {
                throw new ArgumentException("buffer length doesn't match width, height and bit depth of buffer");
            }

            Buffer = buffer;
        }

        public abstract void SetPixel(int x, int y, Color color);
        public abstract Color GetPixel(int x, int y);

        public void Clear()
        {
            Array.Clear(Buffer, 0, Buffer.Length);
        }

        //return true if the write has been handled
        public bool WriteBuffer(int x, int y, IPixelBuffer buffer)
        {
            if (x < 0 || x + buffer.Width > Width ||
                y < 0 || y + buffer.Height > Height)
            {
                throw new Exception("WriteBuffer: new buffer out of range of target buffer");
            }

            if (this.GetType() != buffer.GetType())
            {
                WriteBufferSlow(x, y, buffer);
                return true;
            }
            return false;
        }

        public abstract void Fill(Color color);
        public abstract void Fill(Color color, int x, int y, int width, int height);

        protected void WriteBufferSlow(int x, int y, IPixelBuffer buffer)
        {
            Color color;

            for (int i = 0; i < buffer.Width; i++)
            {
                for (int j = 0; j < buffer.Height; j++)
                {   //uses Color as the intermediary
                    color = buffer.GetPixel(i, j);

                    SetPixel(x + i, y + j, color);
                }
            }
        }
    }
}

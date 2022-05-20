using System;

namespace Meadow.Foundation.Graphics.Buffers
{
    public abstract class BufferBase : IPixelBuffer
    {
        public int Width { get; protected set; }

        public int Height { get; protected set; }

        public abstract ColorType ColorMode { get; }

        /// <summary>
        /// BitDepth is automatically determined based on ColorMode
        /// </summary>
        public virtual int BitDepth
        {
            get
            {
                switch (ColorMode)
                {
                    case ColorType.Format1bpp:
                        return 1;
                        break;
                    case ColorType.Format2bpp:
                        return 2;
                        break;
                    case ColorType.Format4bppGray:
                        return 4;
                        break;
                    case ColorType.Format8bppRgb332:
                        return 8;
                        break;
                    case ColorType.Format12bppRgb444:
                        return 12;
                        break;
                    case ColorType.Format16bppRgb555:
                        return 15;
                        break;
                    case ColorType.Format16bppRgb565:
                        return 16;
                        break;
                    case ColorType.Format18bppRgb666:
                        return 18;
                        break;
                    case ColorType.Format24bppRgb888:
                        return 24;
                        break;
                    case ColorType.Format32bppRgba8888:
                        return 32;
                        break;
                    default:
                        throw new Exception($"Unknown/unsupported bit depth for {ColorMode}");
                }
            }
        }

        /// <summary>
        /// ByteCount is automatically calculated based on the width, height, and bit depth
        /// </summary>
        public virtual int ByteCount => (Height * Width * BitDepth) / 8;

        public byte[] Buffer { get; protected set; }


        public BufferBase() { }

        /// <summary>
        /// Initialize the buffer at a specific height and width. Will
        /// default to 16-bit color if no ColorType is provided
        /// </summary>
        /// <param name="width">The width of the buffer</param>
        /// <param name="height">The height of the buffer</param>
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
                throw new ArgumentException($"Buffer length({buffer.Length}) doesn't match byte count of {ByteCount} width({Width}), height({Height}) and bit depth ({BitDepth}) of buffer.");
            }

            Buffer = buffer;
        }

        public abstract void SetPixel(int x, int y, Color color);

        public abstract Color GetPixel(int x, int y);

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

        public void Clear()
        {
            Array.Clear(Buffer, 0, Buffer.Length);
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

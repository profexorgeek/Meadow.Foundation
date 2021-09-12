using System;
using System.Threading;
using System.Threading.Tasks;
using Meadow.Hardware;
using Meadow.Peripherals.Sensors.Light;
using Meadow.Units;

namespace Meadow.Foundation.Sensors.Light
{
    // TODO: B: i converted this sensor, but it doesn't work for me, and i'm
    // not sure it ever worked. there's lots of console.writelines in here.

    /// <summary>
    /// Driver for the Tcs3472x light-to-digital converter.
    /// </summary>
    public partial class Tcs3472x
        : ByteCommsSensorBase<(Illuminance? AmbientLight, Color? Color, bool Valid)>,
            ILightSensor//, IColorSensor
    {
        // TODO: missing event for ColorUpdated
        //==== events
        public event EventHandler<IChangeResult<Illuminance>> LuminosityUpdated = delegate { };

        //==== internals
        private byte integrationTimeByte;
        private double integrationTime;
        private bool isLongTime;
        private GainType gain;

        //==== properties
        /// <summary>
        /// 
        /// </summary>
        public Illuminance? Illuminance => Conditions.AmbientLight;

        /// <summary>
        /// Set/Get the time to wait for the sensor to read the data
        /// Minimum time is 0.0024 s
        /// Maximum time is 7.4 s
        /// Be aware that it is not a linear function
        /// </summary>
        public double IntegrationTime
        {
            get => integrationTime;
            set
            {
                integrationTime = value;
                SetIntegrationTime(integrationTime);
            }
        }

        /// <summary>
        /// Set/Get the gain
        /// </summary>
        public GainType Gain
        {
            get => gain;
            set
            {
                gain = value;
                WriteRegister(Register.CONTROL, (byte)gain);
            }
        }

        /// <summary>
        /// Get the type of sensor
        /// </summary>
        public DeviceType Device { get; internal set; }


        /// <summary>
        /// Get true if RGBC is clear channel interrupt
        /// </summary>
        public bool IsClearInterrupt
        {
            get
            {
                var status = ReadRegister8(Register.STATUS);
                return ((StatusBit)(status & (byte)StatusBit.STATUS_AINT) == StatusBit.STATUS_AINT);
            }
        }

        public const byte DEFAULT_ADDRESS = 0x29;

        //==== ctors

        /// <summary>
        ///     Create a new instance of the Tcs3472x class with the specified I2C address.
        /// </summary>
        /// <remarks>
        ///     By default the sensor will be set to low gain.
        /// <remarks>
        /// <param name="i2cBus">I2C bus.</param>
        public Tcs3472x(
            II2cBus i2cBus, byte address = DEFAULT_ADDRESS,
            double integrationTime = 0.700, GainType gain = GainType.Gain60X)
                : base(i2cBus, address)
        {
            Initialize();
        }

        private void Initialize()
        {
            Console.WriteLine($"Reading ID");

            //detect device type
            Device = (DeviceType)ReadRegister8(Register.ID);

            Console.WriteLine($"Device: {Device}");

            isLongTime = false;
            IntegrationTime = Math.Clamp(integrationTime, 0.0024, 0.7);

            Console.WriteLine($"Integration time: {IntegrationTime}");

            SetIntegrationTime(integrationTime);
            Gain = gain;
            PowerOn();
        }

        //==== internal methods

        protected override Task<(Illuminance? AmbientLight, Color? Color, bool Valid)> ReadSensor()
        {
            return Task.Run(async () =>
            {
                (Illuminance? AmbientLight, Color? Color, bool Valid) conditions;


                // To have a new reading, you need to wait for integration time to happen
                // If you don't wait, then you'll read the previous value
                await Task.Delay((int)(IntegrationTime * 1000.0));

                var divide = (256 - integrationTimeByte) * 1024.0;

                // If we are in long wait, we'll need to divide even more
                if (isLongTime)
                {
                    divide *= 12.0;
                }

                var rd = ReadRegister8(Register.RDATAL);
                var gd = ReadRegister(Register.GDATAL);
                var bd = ReadRegister(Register.BDATAL);
                var cd = ReadRegister(Register.CDATAL);

                Console.WriteLine($"Red: 0x{rd:x4}");
                Console.WriteLine($"Green: 0x{gd:x4}");
                Console.WriteLine($"Blue: 0x{bd:x4}");
                Console.WriteLine($"Clear: 0x{cd:x4}");

                double r = rd / divide;
                double g = gd / divide;
                double b = bd / divide;
                double a = cd / divide;

                conditions.Color = Color.FromRgba(r, g, b, a);

                // TODO: how to get this? is it just the alpha channel?
                conditions.AmbientLight = new Illuminance(0);

                conditions.Valid = IsValidData();

                return conditions;
            });
        }

        protected override void RaiseEventsAndNotify(IChangeResult<(Illuminance? AmbientLight, Color? Color, bool Valid)> changeResult)
        {
            if (changeResult.New.AmbientLight is { } ambient)
            {
                LuminosityUpdated?.Invoke(this, new ChangeResult<Illuminance>(ambient, changeResult.Old?.AmbientLight));
            }
            base.RaiseEventsAndNotify(changeResult);
        }


        /// <summary>
        /// Set the integration (sampling) time for the sensor
        /// </summary>
        /// <param name="timeSeconds">Time in seconds for each sample. 0.0024 second(2.4ms) increments.Clipped to the range of 0.0024 to 0.6144 seconds.</param>
        private void SetIntegrationTime(double timeSeconds)
        {
            if (timeSeconds <= 700)
            {
                if (isLongTime)
                {
                    SetConfigLongTime(false);
                }

                isLongTime = false;
                var timeByte = Math.Clamp((int)(0x100 - (timeSeconds / 0.0024)), 0, 255);
                WriteRegister(Register.ATIME, (byte)timeByte);
                integrationTimeByte = (byte)timeByte;
            }
            else
            {
                if (!isLongTime)
                {
                    SetConfigLongTime(true);
                }

                isLongTime = true;
                var timeByte = (int)(0x100 - (timeSeconds / 0.029));
                timeByte = Math.Clamp(timeByte, 0, 255);
                WriteRegister(Register.WTIME, (byte)timeByte);
                integrationTimeByte = (byte)timeByte;
            }
        }

        private void SetConfigLongTime(bool setLong)
        {
            Peripheral.WriteRegister((byte)Register.CONFIG, setLong ? CONFIG_WLONG : (byte)0x00);
        }

        private void PowerOn()
        {
            WriteRegister(Register.ENABLE, (byte)EnableBit.ENABLE_PON);
            Thread.Sleep(3);
            WriteRegister(Register.ENABLE, (byte)(EnableBit.ENABLE_PON | EnableBit.ENABLE_AEN));
        }

        private void PowerOff()
        {
            byte powerState = ReadRegister8((byte)Register.ENABLE);
            powerState = (byte)(powerState & ~(byte)(EnableBit.ENABLE_PON | EnableBit.ENABLE_AEN));
            WriteRegister(Register.ENABLE, powerState);
        }

        /// <summary>
        /// Set/Clear the colors and clear interrupts
        /// </summary>
        /// <param name="state">true to set all interrupts, false to clear</param>
        public void SetInterrupt(bool state)
        {
            SetInterrupt(InterruptState.All, state);
        }

        /// <summary>
        /// Set/clear a specific interrupt persistence
        /// This is used to have more than 1 cycle before generating an
        /// interruption.
        /// </summary>
        /// <param name="interupt">The persistence cycles</param>
        /// <param name="state">True to set the interrupt, false to clear</param>
        public void SetInterrupt(InterruptState interupt, bool state)
        {
            WriteRegister(Register.PERS, (byte)interupt);
            byte enable = ReadRegister8(Register.ENABLE);

            if(state)
            {
                enable |= (byte)EnableBit.ENABLE_AIEN;
            }
            else
            {
                enable &= unchecked((byte)~EnableBit.ENABLE_AIEN);
            }

            WriteRegister((byte)Register.ENABLE, enable);
        }

        private void ShowAllRegisters()
        {
            foreach(Register r in Enum.GetValues(typeof(Register)))
            {
                Console.WriteLine($"{r} = 0x{ReadRegister8(r):x2}");
            }
        }

        protected bool IsValidData()
        {
            var status = ReadRegister8(Register.STATUS);
            return ((StatusBit)(status & (byte)StatusBit.STATUS_AVALID) == StatusBit.STATUS_AVALID);
        }

        private void WriteRegister(Register register, byte value)
        {
            Console.WriteLine($"WRITE {register}: 0x{value:x2}");
            Peripheral.WriteRegister((byte)register, value);
        }

        private byte ReadRegister8(Register register)
        {
            var data = Peripheral.ReadRegister((byte)(COMMAND_BIT | (byte)register));
            Console.WriteLine($"READ {register}: 0x{data:x2}");
            return data;
        }

        private ushort ReadRegister(Register register)
        {
            var data = Peripheral.ReadRegisterAsUShort((byte)(COMMAND_BIT | (byte)register), ByteOrder.BigEndian);
            Console.WriteLine($"READ {register}: 0x{data:x4}");
            return data;
        }
    }
}
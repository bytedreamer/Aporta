namespace OSDP.Shared
{
    public class Bus
    {
        public string PortName { get; set; }
        public int BaudRate { get; set; }
        public Device[] devices { get; set; }
    }

    public class Device
    {
    }
}
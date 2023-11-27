using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO.Ports;

namespace mock_serial_port
{
    public partial class MainForm : Form
    {
        public MainForm() =>InitializeComponent();
        BindingList<DataReceived> DataSource { get; } = new BindingList<DataReceived>();
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            dataGridView.DataSource = DataSource;
            dataGridView.AllowUserToAddRows = false; // Critical for this example.
            dataGridView.Columns[nameof(DataReceived.Timestamp)].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            dataGridView.Columns[nameof(DataReceived.Data)].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            MockSerialPort mySerialPort = new MockSerialPort("COM5");
            mySerialPort.BaudRate = 115200;
            mySerialPort.Parity = Parity.None;
            mySerialPort.StopBits = StopBits.One;
            mySerialPort.DataBits = 8;
            mySerialPort.Handshake = Handshake.None;
            mySerialPort.RtsEnable = true;
            mySerialPort.DataReceived += DataReceivedHandler;
            mySerialPort.Open();
        }
        SemaphoreSlim _criticalSection = new SemaphoreSlim(1, 1);
        private async void DataReceivedHandler(object sender, MockSerialDataReceivedEventArgs e)
        {
            await _criticalSection.WaitAsync();
            if(!IsDisposed) BeginInvoke((MethodInvoker)delegate 
            {
                try
                {
                    if (sender is MockSerialPort port)
                    {
                        while (port.BytesToRead > 0)
                        {
                            byte[] buffer = new byte[16];
                            int success = port.Read(buffer, 0, buffer.Length);
                            string display = BitConverter.ToString(buffer, 0, success).Replace("-", " ");
                            var data = new DataReceived { Data = display };
                            DataSource.Add(data);
                        }
                    }
                }
                finally
                {                    
                    _criticalSection.Release();
                }
            });
        }
    }
    class DataReceived
    {
        public string Timestamp { get; } = DateTime.Now.ToString(@"hh\:mm\:ss\.fff tt");
        public string? Data { get; set; }
    }
    class MockSerialPort
    {
        public event MockSerialDataReceivedEventHandler DataReceived;
        public MockSerialPort(string port) { }
        public async void Open()
        {
            _open = true;
            while(_open)
            {
                await Task.Delay(_rando.Next(1000, 5000));
                foreach (
                    var @byte in 
                    Enumerable
                    .Range(0, _rando.Next(1,32))
                    .Select(i => (byte)_rando.Next(256)))
                { 
                    dataBuffer.Enqueue( @byte );
                }
                DataReceived?.Invoke(this, new MockSerialDataReceivedEventArgs(SerialData.Chars));
            }
        }
        public Queue<byte> dataBuffer { get; } = new Queue<byte>();
        public int BytesToRead => dataBuffer.Count;
        bool _open = false;
        public void Close() => _open = false;        
        public int Read(byte[] buffer, int offset, int count)
        {
            if (offset < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (dataBuffer.Count == 0) return 0;
            int bytesRead = Math.Min(count, dataBuffer.Count); 
            for (int i = 0; i < bytesRead; i++)
            {
                buffer[offset + i] = dataBuffer.Dequeue();
            }
            return bytesRead;
        }
        public Parity Parity { get; set; }
        public StopBits StopBits { get; set; }
        public int DataBits { get; set; }
        public Handshake Handshake { get; set; }
        public bool RtsEnable { get;  set; }
        public int BaudRate { get; set; }
        Random _rando = new Random(1); // Seed to make repeatable
    }

    public delegate void MockSerialDataReceivedEventHandler(object sender, MockSerialDataReceivedEventArgs e);
    public class MockSerialDataReceivedEventArgs
    {
        public SerialData EventType { get; }
        public MockSerialDataReceivedEventArgs(SerialData eventType)
        {
            EventType = eventType;
        }
    }
}

# Mock Serial Port

In addition to the excellent comment by Olivier about 'not closing' the serial port, there is also the likelihood of `DataReceived` being on a different thread. You may have to `BeginInvoke` to prevent a cross-threading exception if you plan to "Do some work to show data in datagridview". I used to do this sort of thing quite a lot and here's an example of what worked for me for receiving the event and then locking a critical section while the handler loops to get "all" (may require some kind of throttling) of the data available in the buffer while displaying chunks <= 16 bytes in the DGV.

[![data grid view][1]][1]

___

**Set up DataGridView**
```
public partial class MainForm : Form
{
    public MainForm() =>InitializeComponent();
    .
    .
    .
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
    .
    .
    .
```
**Handle DataReceived**
```
    .
    .
    .
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
```
___
```
class DataReceived
{
    public string Timestamp { get; } = DateTime.Now.ToString(@"hh\:mm\:ss\.fff tt");
    public string? Data { get; set; }
}
```

 


  [1]: https://i.stack.imgur.com/aSd8w.png
  [2]: https://i.stack.imgur.com/v40Ln.png